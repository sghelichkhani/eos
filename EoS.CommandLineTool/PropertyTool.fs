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

module EoS.PropertyTool
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.RegularExpressionConstants
open EoS.Phases
open EoS.CommandLine
open EoS.MPI

type PropertyInfo =
  { Name : string
    Unit : string
    Eval : IThermoElastic -> float<Pa> -> float<K> -> float[] -> string }

type Request =
  | TSync of p:float<Pa> * T:float<K> * x:float[]
  | TDone
  
type Response =
  | RSync of string

let init (comm : Lazy<Communicator>) =
  let ofs = Flag.StringOpt "d" "\t" "Output field separator"

  let PropertiesByName : Map<string, PropertyInfo> =
    [{ Name = "p"
       Unit = "Pa"
       Eval = fun _ p _ _ ->
         sprintf "%g" (p/1.0<Pa>) }
     { Name = "T"
       Unit = "K"
       Eval = fun _ _ T _ ->
         sprintf "%g" (T/1.0<K>) }
     { Name = "x"
       Unit = "1"
       Eval = fun _ _ _ x ->
         x |> Seq.map string |> String.concat ofs.Value }
     { Name = "atoms"
       Unit = "1"
       Eval = fun it _ _ x ->
         string <|
         match it with
         | :? IPhase as it ->
           it.Formula(x).Atoms
         | :? PhaseCollection as it ->
           it.XFold (fun acc it ni xi ->
             acc + ni * it.Formula(xi).Atoms) 0.0 x
         | _ ->
           nan }
     { Name = "V"
       Unit = "m^3/mol"
       Eval = fun it p T x ->
         sprintf "%g" (it.Volume(p, T, x)/1.0<m^3/mol>) }
     { Name = "rho"
       Unit = "kg/m^3"
       Eval = fun it p T x ->
         sprintf "%g" (it.Density(p, T, x)/1.0<kg/m^3>) }
     { Name = "beta"
       Unit = "1/Pa"
       Eval = fun it p T x ->
         sprintf "%g" (it.Compressibility(p, T, x)/1.0<1/Pa>) }
     { Name = "alpha"
       Unit = "1/K"
       Eval = fun it p T x ->
         sprintf "%g" (it.Expansivity(p, T, x)/1.0<1/K>) }
     { Name = "kappa&mu"
       Unit = "Pa"
       Eval = fun it p T x ->
         let κ, μ = it.Moduli(p, T, x)
         sprintf "%g%s%g" (κ/1.0<Pa>) ofs.Value (μ/1.0<Pa>) }
     { Name = "vp&vs"
       Unit = "m/s"
       Eval = fun it p T x ->
         let vp, vs = it.Velocities(p, T, x)
         sprintf "%g%s%g" (vp/1.0<m/s>) ofs.Value (vs/1.0<m/s>) }
     { Name = "G"
       Unit = "J/mol"
       Eval = fun it p T x ->
         sprintf "%g" (it.Energy(p, T, x)/1.0<J/mol>) }
     { Name = "S"
       Unit = "J/mol/K"
       Eval = fun it p T x ->
         sprintf "%g" (it.Entropy(p, T, x)/1.0<J/mol/K>) }
     { Name = "Cp"
       Unit = "J/mol/K"
       Eval = fun it p T x ->
         sprintf "%g" (it.IsobaricHeatCapacity(p, T, x)/1.0<J/mol/K>) }
     { Name = "Cv"
       Unit = "J/mol/K"
       Eval = fun it p T x ->
         sprintf "%g" (it.IsochoricHeatCapacity(p, T, x)/1.0<J/mol/K>) }
     { Name = "gamma"
       Unit = "1"
       Eval = fun it p T x ->
         sprintf "%g" (it.Grueneisen(p, T, x)) }]
    |> Seq.map (fun it -> it.Name.ToLowerInvariant(), it)
    |> Map.ofSeq

  let database = Flag.DatabaseOpt "db" "Thermodynamic database"
  let pressure = Flag.PressureRange "P" "Pressure range"
  let temperature = Flag.TemperatureRange "T" "Temperature range"
  let composition = Flag.StringOpt "x" String.Empty "Composition vector or grid"

  let properties = Collections.Generic.List<PropertyInfo>()
  do Flag.Opts.Add("o", {
    new IFlag with
      override this.IsArgRequired = true
      override this.ArgName = "PROPERTY[]"
      override this.Description = "Add properties to evaluate"

      override this.Parse(arg) =
        CommaRx.Split(arg)
        |> Array.map (fun it ->
          match PropertiesByName.TryFind(it.ToLowerInvariant()) with
          | Some prop -> prop
          | None -> raise(FlagException("Unknown property", arg)))
        |> properties.AddRange
  })

  let progress = Flag.IntOpt "progress" 1000 "Number of points per progress report"

  let rec readLines (reader : IO.TextReader) =
    match reader.ReadLine() with
    | null ->
      Seq.empty
    | line ->
      let line =
        let pos = line.IndexOf('#')
        (if pos >= 0 then line[0 .. pos-1] else line).Trim()
      if String.IsNullOrEmpty(line) then
        readLines reader
      else
        seq { yield (SpaceRx.Split(line) |> Array.map float)
              yield! readLines reader }

  let compositionVector (arg : string) =
    CommaRx.Split(arg)
    |> Array.map (fun it ->
      match Double.TryParse(it, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
      | true, v -> v
      | _ -> raise(FlagException("Invalid composition", arg)))

  let nearestIndex (xs : float<'T>[]) (x : float<'T>) =
    let i = Array.BinarySearch(xs, x)
    if i >= 0 then
      i
    else
      let ub = ~~~i
      if ub <= 0 then
        0
      elif ub >= xs.Length then
        xs.Length - 1
      else
        let lb = ub - 1
        if abs (x - xs[lb]) < abs (x - xs[ub]) then
          lb
        else
          ub

  let compositionGrid (path : string) =
    use stream = IO.File.OpenRead(path)
    eprintfn "# Reading grid..."
    let newReader () =
      stream.Seek(0L, IO.SeekOrigin.Begin) |> ignore
      new IO.StreamReader(stream, Text.Encoding.UTF8, true, 1024, true)

    let ps = Collections.Generic.SortedSet()
    let Ts = Collections.Generic.SortedSet()
    for x in newReader() |> readLines do
      ps.Add(x[0] * 1.0<Pa>) |> ignore
      Ts.Add(x[1] * 1.0<K>) |> ignore

    let ps = Array.ofSeq ps
    let Ts = Array.ofSeq Ts
    let xs = Array2D.zeroCreate ps.Length Ts.Length
    for x in newReader() |> readLines do
      let p = x[0] * 1.0<Pa>
      let T = x[1] * 1.0<K>
      let ip = nearestIndex ps p
      let iT = nearestIndex Ts T
      xs[ip, iT] <- x[2 ..]

    stream.Close()
    ps, Ts, xs

  let main (comm : Communicator) args =
    let thelit : IThermoElastic =
      let db = database.Value
      match args with
      | [||] ->
        match db.TryGetObject<PhaseCollection>("ALL") with
        | Some it -> upcast it
        | None -> raise(FlagException("No default phase collection", String.Empty))
      | [|arg|] ->
        match db.TryGetObject<IThermoElastic>(arg) with
        | Some it -> it
        | None -> raise(FlagException("Unknown thermo-elastic object", arg))
      | args ->
        upcast PhaseCollection(
          args
          |> Array.map (fun arg ->
            match db.TryGetObject<IPhase>(arg) with
            | Some it -> it
            | None -> raise(FlagException("Unknown phase", arg))))

    let getX =
      if composition.IsSet && comm.Rank = 0 then
        try
          let ps, Ts, xs = compositionGrid composition.Value
          eprintfn "# Starting computations..."
          Some (fun p T ->
            let ip = nearestIndex ps p
            let iT = nearestIndex Ts T
            xs[ip, iT])
        with
        | _ ->
          let x = compositionVector composition.Value
          if x.Length <> thelit.XLength then
            raise(FlagException("Composition vector length mismatch", String.Empty))
          Some (fun p T -> x)
      else
        None

    if properties.Count = 0 then
      properties.Add(PropertiesByName["p"])
      properties.Add(PropertiesByName["t"])
      properties.Add(PropertiesByName["g"])

    let printHeader () =
      printfn "#Thermoelastic properties for %O" thelit

      stdout.Write('#')
      for { Name = name; Unit = unit } in properties do
        printf "%s(%s)%s" name unit ofs.Value
      stdout.WriteLine()

    let getInputs () =
      if pressure.IsSet && temperature.IsSet && getX.IsSome then
        seq { for p in pressure.Values do
              for T in temperature.Values do
              let x = getX.Value p T
              yield (p, T, x) }
      elif pressure.IsSet && temperature.IsSet then
        if thelit.XLength > 1 then
          seq { for x in readLines stdin do
                for p in pressure.Values do
                for T in temperature.Values do
                yield (p, T, x) }
        else
          seq { for p in pressure.Values do
                for T in temperature.Values do
                yield (p, T, Array.empty) }
      elif pressure.IsSet then
        seq { for x in readLines stdin do
              let T = x[0] * 1.0<K>
              for p in pressure.Values do
              let x = match getX with Some getX -> getX p T | None -> x[1..]
              yield (p, T, x) }
      elif temperature.IsSet then
        seq { for x in readLines stdin do
              let p = x[0] * 1.0<Pa>
              for T in temperature.Values do
              let x = match getX with Some getX -> getX p T | None -> x[1..]
              yield (p, T, x) }
      else
        seq { for x in readLines stdin do
              let p = x[0] * 1.0<Pa>
              let T = x[1] * 1.0<K>
              let x = match getX with Some getX -> getX p T | None -> x[2..]
              yield (p, T, x) }

    let evalProperties p T x =
      properties
      |> Seq.map (fun prop ->
        try prop.Eval thelit p T x with
        | exn -> sprintf "(%s %A)" (exn.GetType().Name) exn.Message)
      |> String.concat ofs.Value

    if comm.Rank = 0 then
      printHeader ()
      match comm.Size with
      | 1 ->
        for p, T, x in getInputs () do
          stdout.WriteLine(evalProperties p T x)
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
            stdout.WriteLine(line)
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
        | TSync (p, T, x), rank, _ ->
          comm.Send(RSync (evalProperties p T x), rank)
          loop ()
        | TDone, _, _ ->
          ()
      loop ()

    0

  comm.Force()
  |> main
