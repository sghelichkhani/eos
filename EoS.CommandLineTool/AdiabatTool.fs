// This file is part of EoS
// Copyright (c) 2009-2025 Thomas Chust
//               2009-2017 Bayerisches Geoinstitut, Bayreuth
//               2009-2017 Ludwig-Maximilians-Universität, München
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

module EoS.AdiabatTool
open System
open FSharp.Core.Printf
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.RegularExpressionConstants
open EoS.Chemistry
open EoS.Phases
open EoS.Optimization
open EoS.CommandLine

type PropertyInfo =
  { Name : string
    Unit : string
    Eval : float<Pa> -> float<K> -> float[] -> float<J/mol/K> -> string }

let init (comm : Lazy<EoS.MPI.Communicator>) =
  let ofs = Flag.StringOpt "d" "\t" "Output field separator"

  let PropertiesByName : Map<string, PropertyInfo> =
    [{ Name = "p"
       Unit = "Pa"
       Eval = fun p _ _ _ ->
         sprintf "%g" (p/1.0<Pa>) }
     { Name = "T"
       Unit = "K"
       Eval = fun _ T _ _ ->
         sprintf "%g" (T/1.0<K>) }
     { Name = "x"
       Unit = "1"
       Eval = fun _ _ x _ ->
         x |> Seq.map string |> String.concat ofs.Value }
     { Name = "S"
       Unit = "J/mol/K"
       Eval = fun _ _ _ S ->
         sprintf "%g" (S/1.0<J/mol/K>) }]
    |> Seq.map (fun it -> it.Name.ToLowerInvariant(), it)
    |> Map.ofSeq

  let database = Flag.DatabaseOpt "db" "Thermodynamic database"
  let pressure = Flag.PressureRange "P" "Pressure range"
  let temperature = Flag.TemperatureRange "T" "Temperature range"
  let composition = Flag.FormulaOpt "bulk" "H" "Composition"

  let steps = Flag.IntOpt "steps" 64 "Minimum number of pseudocompound steps"
  let levels = Flag.IntOpt "levels" 2 "Maximum number of pseudocompound refinement levels"
  let keep = Flag.BoolOpt "keep" "Never replace any pseudocompounds"

  let footPressure = Flag.PressureOpt "footP" 1.0e5<Pa> "Starting pressure for the adiabat"
  let footTemperature = Flag.TemperatureOpt "footT" 300.0<K> "Starting temperature for the adiabat"

  let properties = Collections.Generic.List<PropertyInfo>()
  do Flag.Opts.Add("o", {
    new IFlag with
      override this.IsArgRequired = true
      override this.ArgName = "PROPERTY[]"
      override this.Description = "Add properties to output"

      override this.Parse(arg) =
        CommaRx.Split(arg)
        |> Array.map (fun it ->
          match PropertiesByName.TryFind(it.ToLowerInvariant()) with
          | Some prop -> prop
          | None -> raise(FlagException("Unknown property", arg)))
        |> properties.AddRange
  })

  let rec readLines () =
    match stdin.ReadLine() with
    | null ->
      Seq.empty
    | line ->
      let line =
        let pos = line.IndexOf('#')
        (if pos >= 0 then line[0 .. pos-1] else line).Trim()
      if String.IsNullOrEmpty(line) then
        readLines ()
      else
        seq { yield (SpaceRx.Split(line) |> Array.map float)
              yield! readLines () }

  let getInputs () =
    seq { for x in readLines () do
          let p = x[0] * 1.0<Pa>
          let T = x[1] * 1.0<K>
          let x = x[2..]
          yield (p, T, x) }

  let getIndex (vs : float<'U>[]) (v : float<'U>) truncate : int =
    let i = Array.BinarySearch(vs, v)
    if i >= 0 then
      i
    else
      let i = ~~~i
      if i > 0 then
        if truncate || i >= vs.Length || abs(v - vs[i]) >= abs(v - vs[i - 1]) then
          i - 1
        else
          i
      else
        0

  let main args =
    let blurb, phases =
      let db = database.Value
      match args with
      | [||] ->
        match db.TryGetObject<PhaseCollection>("ALL") with
        | Some phases -> "ALL", phases
        | None -> raise(FlagException("No default phase collection", String.Empty))
      | [|arg|] ->
        match db.TryGetObject<PhaseCollection>(arg) with
        | Some phases -> arg, phases
        | None -> raise(FlagException("Unknown phase collection", arg))
      | args ->
        args |> String.concat ", ",
        PhaseCollection(
          args
          |> Seq.map (fun arg ->
            match db.TryGetObject<IPhase>(arg) with
            | Some phase -> phase
            | None -> raise(FlagException("Unknown phase", arg))))

    if properties.Count = 0 then
      properties.Add(PropertiesByName["p"])
      properties.Add(PropertiesByName["t"])
      properties.Add(PropertiesByName["s"])

    let printHeader () =
      printfn "#Adiabat for %s phases" blurb

      stdout.Write('#')
      for { Name = name; Unit = unit } in properties do
        printf "%s(%s)%s" name unit ofs.Value
      stdout.WriteLine()

    let ps = Seq.toArray pressure.Values
    let Ts = Seq.toArray temperature.Values
    let xs = Array2D.zeroCreate ps.Length Ts.Length

    let dT = Ts[1] - Ts[0]

    if not composition.IsSet then
      eprintfn "# Reading grid..."
      for p, T, x in getInputs () do
        let ip, iT = getIndex ps p false, getIndex Ts T false
        xs[ip, iT] <- x
      eprintfn "# Starting computations..."

    let getComposition ip iT =
      match xs[ip, iT] with
      | null ->
        let p, T, bulk = ps[ip], Ts[iT], composition.Value
        if composition.IsSet then
          try
            let x = phases.Optimize(p, T, bulk, steps.Value, levels.Value, keep.IsSet)
            xs[ip, iT] <- x
            Some x
          with
          | EoS.LPSolve.LPSolveException (_, msg) ->
            printfn "# p = %g Pa, T = %g K, bulk = %O: LPSolveException %A" (p/1.0<Pa>) (T/1.0<K>) bulk msg
            None
        else
          printfn "# p = %g Pa, T = %g K: Missing composition information" (p/1.0<Pa>) (T/1.0<K>)
          None
      | x ->
        Some x

    let pmin, pmax = ps[0], ps[ps.Length-1]
    let Tmin, Tmax = Ts[0], Ts[Ts.Length-1]

    let phases = phases :> IThermoElastic

    let footP = if footPressure.IsSet then footPressure.Value else pmin
    let footT = if footTemperature.IsSet then footTemperature.Value else Tmin
    let footX = getComposition (getIndex ps footP true) (getIndex Ts footT true) |> Option.get
    let S0 = phases.Entropy(footP, footT, footX)

    let optimalTemperature ip iT x0 =
      let p, x = ps[ip], defaultArg (getComposition ip iT) x0
      let Tmin = Ts[iT]
      let Tmax = Ts[iT] + dT

      let rec improveTemperature T S relax =
        let T' = T - relax * T * (S - S0) / phases.IsobaricHeatCapacity(p, T, x)
        let T' = max Tmin (min T' Tmax)
        let S' = phases.Entropy(p, T', x)
        if abs(S' - S0) < abs(S - S0) then
          improveTemperature T' S' relax
        elif relax >= 0.125 then
          improveTemperature T S (relax / 2.0)
        else
          p, T, x, S

      let T = Ts[iT]
      let S = phases.Entropy(p, T, x)
      improveTemperature T S 1.0

    let evalProperties p T x S =
      properties
      |> Seq.map (fun prop ->
        prop.Eval p T x S)
      |> String.concat ofs.Value

    let rec nextTemperature p0 T0 x0 S =
      stdout.WriteLine(evalProperties p0 T0 x0 S)

      let ip0, iT0 = getIndex ps p0 true, getIndex Ts T0 true
      let ip1 = ip0 + 1
      if ip1 < ps.Length then
        let adx =
          let α0 = phases.Expansivity(p0, T0, x0)
          let V0 = phases.Volume(p0, T0, x0)
          let Cp0 = phases.IsobaricHeatCapacity(p0, T0, x0)
          α0 * V0 / Cp0

        if adx > 0.0<_> then
          let iT1 = iT0 + 1
          if iT1 < Ts.Length then
            optimalNeighbour ip0 iT0 ip1 iT1 x0
        else
          let iT1 = iT0 - 1
          if iT1 >= 0 then
            optimalNeighbour ip0 iT0 ip1 iT1 x0

    and optimalNeighbour ip0 iT0 ip1 iT1 x0 =
      let p1, T1, x1, S1 =
        [ optimalTemperature ip1 iT0 x0
          optimalTemperature ip1 iT1 x0
          optimalTemperature ip0 iT1 x0 ]
        |> List.minBy (fun (p, T, x, S) -> abs (S - S0))
      nextTemperature p1 T1 x1 S1

    printHeader ()
    nextTemperature footP footT footX S0

    0

  main
