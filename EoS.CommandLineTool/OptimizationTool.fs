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

module EoS.OptimizationTool
open System
open FSharp.Core.Printf
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.RegularExpressionConstants
open EoS.Chemistry
open EoS.Phases
open EoS.Optimization
open EoS.CommandLine
open EoS.MPI

type PropertyInfo =
  { Name : string
    Unit : string
    Eval : PhaseCollection -> float<Pa> -> float<K> -> Formula -> float[] -> string }

type Request =
  | TSync of p:float<Pa> * T:float<K> * formula:Formula
  | TDone
  
type Response =
  | RSync of string

let init (comm : Lazy<Communicator>) =
  let ofs = Flag.StringOpt "d" "\t" "Output field separator"

  let PropertiesByName : Map<string, PropertyInfo> =
    [{ Name = "p"
       Unit = "Pa"
       Eval = fun _ p _ _ _ ->
         sprintf "%g" (p/1.0<Pa>) }
     { Name = "T"
       Unit = "K"
       Eval = fun _ _ T _ _ ->
         sprintf "%g" (T/1.0<K>) }
     { Name = "bulk"
       Unit = "formula"
       Eval = fun _ _ _ bulk _ ->
         string bulk }
     { Name = "xbulk"
       Unit = "1"
       Eval = fun _ _ _ bulk _ ->
         if bulk.Length = 2 then
           snd bulk[1] |> string
         else
           "1" }
     { Name = "x"
       Unit = "1"
       Eval = fun _ _ _ _ x ->
         x |> Seq.map string |> String.concat ofs.Value }
     { Name = "xtree"
       Unit = "1"
       Eval = fun phases _ _ _ x ->
         let f = Array.sum x
         let buf = Text.StringBuilder("(")
         bprintf buf "%g" f
         let rec repr x f0 (it : PhaseCollectionItem) =
           let x = it.XSlice(x)
           let f = Array.sum x
           if f <> 0.0 then
             bprintf buf " (%A" (string it.Phase)
             match it.Phase with
             | :? seq<PhaseCollectionItem> as children ->
               bprintf buf " %g" (f / f0)
               Seq.iter (repr x f) children
             | phase ->
               Seq.iter (fun xi -> bprintf buf " %g" (xi / f0)) x
             buf.Append(')') |> ignore
         Seq.iter (repr x f) phases
         buf.Append(')').ToString() }
     { Name = "phases"
       Unit = "bitmap"
       Eval = fun phases _ _ _ x ->
         phases
         |> Seq.fold (fun (bits, mask) it ->
           (bits ||| if it.XSlice(x) |> Array.sum > 0.0 then mask else 0UL),
           (mask <<< 1)) (0UL, 1UL)
         |> fst |> string }
     { Name = "endmembers"
       Unit = "bitmap"
       Eval =
         let rec bitmap ioffset (items : seq<PhaseCollectionItem>) x =
           items
           |> Seq.fold (fun bits it ->
             let ioffset = ioffset + it.XOffset
             let x = it.XSlice(x)
             match it.Phase with
             | :? seq<PhaseCollectionItem> as children ->
               bits ||| bitmap ioffset children x
             | _ ->
               bits ||| if Array.sum x > 0.0 then 1UL <<< ioffset else 0UL) 0UL
         fun phases _ _ _ x ->
           bitmap 0 phases x |> string }]
    |> Seq.map (fun it -> it.Name.ToLowerInvariant(), it)
    |> Map.ofSeq

  let database = Flag.DatabaseOpt "db" "Thermodynamic database"
  let pressure = Flag.PressureRange "P" "Pressure range"
  let temperature = Flag.TemperatureRange "T" "Temperature range"
  let composition = Flag.FormulaRange "bulk" "Composition range"

  let steps = Flag.IntOpt "steps" 64 "Minimum number of pseudocompound steps"
  let levels = Flag.IntOpt "levels" 2 "Maximum number of pseudocompound refinement levels"
  let keep = Flag.BoolOpt "keep" "Never remove any pseudocompounds when adding new ones"
  let relax = Flag.BoolOpt "relax" "Allow partial matches of bulk composition"

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

  let progress = Flag.IntOpt "progress" 100 "Number of points per progress report"

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
        seq { yield SpaceRx.Split(line)
              yield! readLines () }

  let main (comm : Communicator) args =
    // Use synchronized stdout to prevent interleaved writes
    let syncOut = IO.TextWriter.Synchronized(stdout)

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
      properties.Add(PropertiesByName["x"])

    let printHeader () =
      syncOut.WriteLine(sprintf "#Stable composition for %s phases" blurb)

      let headerLine =
        properties
        |> Seq.map (fun { Name = name; Unit = unit } -> sprintf "%s(%s)" name unit)
        |> String.concat ofs.Value
      syncOut.WriteLine("#" + headerLine + ofs.Value)

      match properties.IndexOf(PropertiesByName["x"]) with
      | -1 -> ()
      | xoffset ->
        let rec infoLoop xoffset (it : PhaseCollectionItem) =
          let xoffset = xoffset + it.XOffset
          match it.Phase with
          | :? seq<PhaseCollectionItem> as children ->
            Seq.iter (infoLoop xoffset) children
          | phase ->
            syncOut.WriteLine(sprintf "#[%d] : %O" xoffset phase)
        Seq.iter (infoLoop xoffset) phases

      match properties.IndexOf(PropertiesByName["phases"]) with
      | -1 -> ()
      | xoffset ->
        phases
        |> Seq.iteri (fun i it ->
          syncOut.WriteLine(sprintf "#[%d] >> %d : %O" xoffset i it.Phase))

      match properties.IndexOf(PropertiesByName["endmembers"]) with
      | -1 -> ()
      | xoffset ->
        let rec infoLoop ioffset (it : PhaseCollectionItem) =
          let ioffset = ioffset + it.XOffset
          match it.Phase with
          | :? seq<PhaseCollectionItem> as children ->
            Seq.iter (infoLoop ioffset) children
          | phase ->
            syncOut.WriteLine(sprintf "#[%d] >> %d : %O" xoffset ioffset phase)
        Seq.iter (infoLoop 0) phases

    let getInputs () =
      if pressure.IsSet && temperature.IsSet && composition.IsSet then
        seq { for bulk in composition.Values do
              for p in pressure.Values do
              for T in temperature.Values do
              yield (p, T, bulk) }
      elif pressure.IsSet && temperature.IsSet then
        seq { for line in readLines () do
              let bulk = Formula.ofString line[0]
              for p in pressure.Values do
              for T in temperature.Values do
              yield (p, T, bulk) }
      elif pressure.IsSet && composition.IsSet then
        seq { for line in readLines () do
              let T = 1.0<K> * float line[0]
              for bulk in composition.Values do
              for p in pressure.Values do
              yield (p, T, bulk) }
      elif temperature.IsSet && composition.IsSet then
        seq { for line in readLines () do
              let p = 1.0<Pa> * float line[0]
              for bulk in composition.Values do
              for T in temperature.Values do
              yield (p, T, bulk) }
      elif pressure.IsSet then
        seq { for line in readLines () do
              let T = 1.0<K> * float line[0]
              let bulk = Formula.ofString line[1]
              for p in pressure.Values do
              yield (p, T, bulk) }
      elif temperature.IsSet then
        seq { for line in readLines () do
              let p = 1.0<Pa> * float line[0]
              let bulk = Formula.ofString line[1]
              for T in temperature.Values do
              yield (p, T, bulk) }
      elif composition.IsSet then
        seq { for line in readLines () do
              let p = 1.0<Pa> * float line[0]
              let T = 1.0<K> * float line[1]
              for bulk in composition.Values do
              yield (p, T, bulk) }
      else
        seq { for line in readLines () do
              let p = 1.0<Pa> * float line[0]
              let T = 1.0<K> * float line[1]
              let bulk = Formula.ofString line[2]
              yield (p, T, bulk) }

    let evalProperties p T bulk =
      try
        let x = phases.Optimize(p, T, bulk, steps.Value, levels.Value, keep.IsSet, relax.IsSet)
        properties
        |> Seq.map (fun prop ->
          try prop.Eval phases p T bulk x with
          | exn -> sprintf "(%s %A)" (exn.GetType().Name) exn.Message)
        |> String.concat ofs.Value
      with
      | EoS.LPSolve.LPSolveException (_, msg) ->
        sprintf "# p = %g Pa, T = %g K, bulk = %O: LPSolveException %A" (p/1.0<Pa>) (T/1.0<K>) bulk msg

    if comm.Rank = 0 then
      printHeader ()
      match comm.Size with
      | 1 ->
        for p, T, bulk in getInputs () do
          syncOut.WriteLine(evalProperties p T bulk)
      | n ->
        let queue = (getInputs ()).GetEnumerator()
        let t0 = DateTime.Now
        let rec init n p rank =
          if 0 < rank && rank < n then
            if queue.MoveNext() then
              comm.Send(TSync queue.Current, rank)
              eprintfn "# Processor %d/%d starting" rank n
              init n (p + 1) (rank - 1)
            else
              comm.Send(TDone, rank)
              init (n - 1) p (rank - 1)
          else
            loop n p
        and loop n p =
          if n > 1 then
            let RSync line, rank, _ = comm.Receive<Response>()
            syncOut.WriteLine(line)
            if p % progress.Value = 0 then
              let dt = DateTime.Now - t0
              eprintfn "# Processed %d points in %O at %.2f points/s" p dt (float p / dt.TotalSeconds)
            if queue.MoveNext() then
              comm.Send(TSync queue.Current, rank)
              loop n (p + 1)
            else
              comm.Send(TDone, rank)
              eprintfn "# Processor %d/%d stopping" rank n
              loop (n - 1) p
          else
            let dt = DateTime.Now - t0
            eprintfn "# Done with %d points in %O at %.2f points/s" p dt (float p / dt.TotalSeconds)
        init n 0 (n - 1)
    else
      let rec loop () =
        match comm.Receive<Request>(0) with
        | TSync (p, T, bulk), rank, _ ->
          comm.Send(RSync (evalProperties p T bulk), rank)
          loop ()
        | TDone, _, _ ->
          ()
      loop ()

    0

  comm.Force()
  |> main
