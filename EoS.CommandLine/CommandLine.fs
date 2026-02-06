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

/// Basic command line parsing support.
namespace EoS.CommandLine
open System
open System.Xml.Linq
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS
open EoS.Units
open EoS.Xml

/// Format exception signalling bad command line options.
type FlagException(message, arg) =
  inherit FormatException(message)

  /// The argument that caused trouble.
  member this.Arg = arg

/// Interface of value containers for command line arguments.
type IFlag =
  /// Whether an option argument is always required when parsing a
  /// command line option into this container.
  abstract IsArgRequired : bool

  /// Descriptive name of an argument parsed into this container.
  abstract ArgName : string

  /// Functional description of the container.
  abstract Description : string

  /// Parses an option or argument into the container. When parsing an
  /// option, the given argument may be the empty string in case the
  /// option has no argument.
  abstract Parse : arg:string -> unit

/// Boolean container.
type BoolFlag(description : string) =
  let mutable set = false

  /// Whether the flag was set to a true value during parsing.
  member this.IsSet = set

  interface IFlag with
    override this.IsArgRequired = false
    override this.ArgName = "BOOL"
    override this.Description = description

    override this.Parse(arg : string) =
      set <-
        String.IsNullOrEmpty(arg) ||
        match arg.ToLowerInvariant() with
        | "0" | "f" | "false" | "no" | "-" -> false
        | _ -> true

/// Arbitrary value container with custom parser.
type ValueFlag<'T>(init : Lazy<'T>, description : string, convert : string -> 'T) =
  let mutable set = false
  let mutable value = init

  /// Whether the flag was set during parsing.
  member this.IsSet = set

  /// Flag value.
  member this.Value = value.Value

  interface IFlag with
    override this.IsArgRequired = true
    override this.ArgName = typeof<'T>.Name.ToUpper()
    override this.Description = description

    override this.Parse(arg : string) =
      value <- Lazy<'T>.CreateFromValue(convert arg)
      set <- true

/// Arbitrary value set container with custom parser.
type ValueSetFlag<'T when 'T : comparison>(description : string, convert : string -> 'T) =
  let mutable set = false
  let mutable values : Set<'T> = Set.empty

  /// Whether the flag was set during parsing.
  member this.IsSet = set

  /// Flag value.
  member this.Values = values

  interface IFlag with
    override this.IsArgRequired = true
    override this.ArgName = typeof<'T>.Name.ToUpper()
    override this.Description = description

    override this.Parse(arg : string) =
      values <- Set.add (convert arg) values
      set <- true

/// Value range wrapper.
type ValueRange<'T>(start : ValueFlag<'T>, stop : ValueFlag<'T>, steps : ValueFlag<int>, interp : 'T -> 'T -> float -> 'T) =
  /// Start flag.
  member this.Start = start

  /// Stop flag.
  member this.Stop = stop

  /// Steps flag.
  member this.Steps = steps

  /// Whether at least one endpoint of the range was set during parsing.
  member this.IsSet = start.IsSet || stop.IsSet

  /// Values in the range.
  member this.Values =
    if start.IsSet && stop.IsSet && steps.Value > 1 then
      let df = 1.0 / float(steps.Value - 1)
      seq { for f in 0.0 .. df .. 1.0 -> interp start.Value stop.Value f }
    elif start.IsSet then
      Seq.singleton start.Value
    elif stop.IsSet then
      Seq.singleton stop.Value
    else
      Seq.empty

/// Extension methods for phase databases.
[<AutoOpen>]
module XFormatterExtensions =
  type XFormatter with
    /// Load a phase database. The given argument may reference a file
    /// through an absolute path or relative to any directory in the
    /// environment variable EOS_DATABASE_PATH. The file name
    /// extension ".xml" may be omitted from the argument. If
    /// EOS_DATABASE_PATH is not set or empty, the current working
    /// directory and the base directory of the current application
    /// domain are searched.
    static member LoadDatabase(arg : string, ?unitsystem : Lazy<UnitSystem>) =
      let unitsystem =
        defaultArg unitsystem (lazy UnitSystem.Load())

      let path =
        Environment.GetEnvironmentVariable("EOS_DATABASE_PATH")
      let path =
        if String.IsNullOrEmpty(path) then
          [| Environment.CurrentDirectory; AppDomain.CurrentDomain.BaseDirectory |]
        else
          path.Split(IO.Path.PathSeparator)
      let path =
        seq {
          for it in path do
            yield IO.Path.Combine(it, arg)
            yield IO.Path.Combine(it, arg + ".xml")
        }
        |> Seq.find IO.File.Exists

      let root =
        XDocument.Load(Uri(path, UriKind.Absolute).AbsoluteUri).Root

      let context = XFormatter(unitsystem)
      for it in root.Elements(XName.EoSPhase) do
        context.Deserialize<Phases.IPhase>(it) |> ignore
      for it in root.Elements(XName.EoSCollection) do
        context.Deserialize<Phases.PhaseCollection>(it) |> ignore

      context

/// Command line parser and utility methods.
module Flag =
  /// The name of the command line program for usage
  /// messages. Defaults to the friendly name of the application
  /// domain.
  let mutable CommandName = AppDomain.CurrentDomain.FriendlyName

  /// The default unit system, loaded lazily. This is used for unit
  /// conversion when interpreting pressure and temperature arguments
  /// and when loading phase databases into XFormatter contexts.
  let mutable UnitSystem = lazy UnitSystem.Load()

  /// The default database of well-known chemical formulas. This is used
  /// to look up frequently used complex formulas when interpreting
  /// bulk composition arguments.
  let mutable FormulaDatabase =
    lazy
      let path =
        Environment.GetEnvironmentVariable("EOS_DATABASE_PATH")
      let path =
        if String.IsNullOrEmpty(path) then
          [| Environment.CurrentDirectory; AppDomain.CurrentDomain.BaseDirectory |]
        else
          path.Split(IO.Path.PathSeparator)
      let path =
        seq { for it in path -> IO.Path.Combine(it, "Formula.xml") }
        |> Seq.find IO.File.Exists

      let root =
        XDocument.Load(Uri(path, UriKind.Absolute).AbsoluteUri).Root

      root.Elements(XNamespace.EoS + "formula")
      |> Seq.map (fun it ->
        it.Attribute(XNamespace.None + "id").Value,
        Chemistry.Formula.ofString it.Value)
      |> Map.ofSeq

  /// Options recognized by the parser.
  let Opts = Collections.Generic.SortedList<string, IFlag>()

  /// Register a new boolean option.
  let BoolOpt name description =
    let p = BoolFlag(description)
    Opts.Add(name, p)
    p

  /// Register a new string option.
  let StringOpt name init description =
    let p = ValueFlag(lazy init, description, id)
    Opts.Add(name, p)
    p

  /// Register a new string set option.
  let StringSetOpt name description =
    let p = ValueSetFlag<string>(description, id)
    Opts.Add(name, p)
    p

  let internal ParseInt arg =
    match Int32.TryParse(arg, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture) with
    | true, v -> v
    | false, _ -> raise(FlagException("Invalid integer", arg))

  /// Register a new integer option.
  let IntOpt name init description =
    let p = ValueFlag(lazy init, description, ParseInt)
    Opts.Add(name, p)
    p

  let internal ParseFloat arg =
    match Double.TryParse(arg, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
    | true, v -> v
    | false, _ -> raise(FlagException("Invalid float", arg))

  /// Register a new float option.
  let FloatOpt name init description =
    let p = ValueFlag(lazy init, description, ParseFloat)
    Opts.Add(name, p)
    p

  let private QuantityRx =
    Text.RegularExpressions.Regex(
      @"^([-+]?\d*(?:\.\d*)?(?:[eE][-+]?\d+)?)\s*(.*)$")

  let private ParseQuantity arg =
    let m = QuantityRx.Match(arg)
    if m.Success then
      let v =
        match Double.TryParse(m.Groups[1].Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
        | true, v -> v
        | false, _ -> raise(FlagException("Invalid amount", arg))
      v, m.Groups[2].Value.Trim()
    else
      raise(FlagException("Invalid quantity", arg))

  let internal ParsePressure arg =
    let v, u = ParseQuantity arg
    1.0<Pa> *
    match u with
    | "Pa" | "" ->
      v
    | u ->
      let sys = UnitSystem.Value
      try UnitConverter(sys.Unit(u), sys.Unit("Pa")).Convert(v) with
      | UnitsException _ -> raise(FlagException("Pressure unit conversion error", arg))

  /// Register a new pressure option.
  let PressureOpt name init description =
    let p = ValueFlag(lazy init, description, ParsePressure)
    Opts.Add(name, p)
    p

  let internal InterpolateFloat (p0 : float<'u>) (p1 : float<'u>) (f : float) : float<'u> =
    p0 + (p1 - p0) * f

  /// Register a new pressure range. The stop and steps options
  /// will be called "to" + name and "n" + name.
  let PressureRange name description =
    let start = ValueFlag(lazy 0.0<_>, description + " (start value)", ParsePressure)
    let stop = ValueFlag(lazy 0.0<_>, description + " (stop value)", ParsePressure)
    let steps = ValueFlag(lazy 10, description + " (step count)", ParseInt)
    Opts.Add(name, start)
    Opts.Add("to" + name, stop)
    Opts.Add("n" + name, steps)
    ValueRange(start, stop, steps, InterpolateFloat)

  let internal ParseTemperature arg =
    let v, u = ParseQuantity arg
    1.0<K> *
    match u with
    | "K" | "" ->
      v
    | u ->
      let sys = UnitSystem.Value
      try UnitConverter(sys.Unit(u), sys.Unit("K")).Convert(v) with
      | UnitsException _ -> raise(FlagException("Temperature unit conversion error", arg))

  /// Register a new temperature option.
  let TemperatureOpt name init description =
    let p = ValueFlag(lazy init, description, ParseTemperature)
    Opts.Add(name, p)
    p

  /// Register a new temperature range. The stop and steps options
  /// will be called "to" + name and "n" + name.
  let TemperatureRange name description =
    let start = ValueFlag(lazy 0.0<_>, description + " (start value)", ParseTemperature)
    let stop = ValueFlag(lazy 0.0<_>, description + " (stop value)", ParseTemperature)
    let steps = ValueFlag(lazy 10, description + " (step count)", ParseInt)
    Opts.Add(name, start)
    Opts.Add("to" + name, stop)
    Opts.Add("n" + name, steps)
    ValueRange(start, stop, steps, InterpolateFloat)

  let internal ParseMass arg =
    let v, u = ParseQuantity arg
    1.0<kg> *
    match u with
    | "kg" | "" ->
      v
    | u ->
      let sys = UnitSystem.Value
      try UnitConverter(sys.Unit(u), sys.Unit("kg")).Convert(v) with
      | UnitsException _ -> raise(FlagException("Mass unit conversion error", arg))

  /// Register a new mass option.
  let MassOpt name init description =
    let p = ValueFlag(lazy init, description, ParseMass)
    Opts.Add(name, p)
    p

  let internal ParseLength arg =
    let v, u = ParseQuantity arg
    1.0<m> *
    match u with
    | "m" | "" ->
      v
    | u ->
      let sys = UnitSystem.Value
      try UnitConverter(sys.Unit(u), sys.Unit("m")).Convert(v) with
      | UnitsException _ -> raise(FlagException("Length unit conversion error", arg))

  /// Register a new length option.
  let LengthOpt name init description =
    let p = ValueFlag(lazy init, description, ParseLength)
    Opts.Add(name, p)
    p

  let internal ParseFormula arg =
    match Map.tryFind arg FormulaDatabase.Value with
    | Some formula ->
      formula
    | None ->
      match Chemistry.Formula.parse arg with
      | Parsing.Success (formula, _) ->
        if Chemistry.Formula.isSingletonGroup formula then
          fst formula[0]
        else
          upcast formula
      | Parsing.Failure (pos, msg) ->
        raise(FlagException(sprintf "Invalid chemical formula (%s at %d)" msg pos, arg))

  /// Register a new formula option.
  let FormulaOpt name init description =
    let p = ValueFlag(Lazy<Chemistry.Formula>.CreateFromValue(Chemistry.Formula.ofString init), description, ParseFormula)
    Opts.Add(name, p)
    p

  let internal InterpolateFormula (f0 : Chemistry.Formula) (f1 : Chemistry.Formula) x : Chemistry.Formula =
    upcast Chemistry.Composite[f0, 1.0 - x; f1, x]

  /// Register a new formula range. The stop and steps options will be
  /// called "to" + name and "n" + name.
  let FormulaRange name description =
    let start = ValueFlag(lazy (Chemistry.Composite[] :> Chemistry.Formula), description + " (start value)", ParseFormula)
    let stop = ValueFlag(lazy (Chemistry.Composite[] :> Chemistry.Formula), description + " (stop value)", ParseFormula)
    let steps = ValueFlag(lazy 10, description + " (step count)", ParseInt)
    Opts.Add(name, start)
    Opts.Add("to" + name, stop)
    Opts.Add("n" + name, steps)
    ValueRange(start, stop, steps, InterpolateFormula)

  let internal ParseDatabase (arg : string) =
    XFormatter.LoadDatabase(arg, UnitSystem)

  /// Register a new phase database option. Default flag value is the
  /// first configured phase database.
  let DatabaseOpt name description =
    let p = ValueFlag(lazy ParseDatabase "default", description, ParseDatabase)
    Opts.Add(name, p)
    p

  /// Print a usage description to the given output stream. When full
  /// is true, descriptions of all known options and arguments are
  /// printed.
  let PrintUsage full (w : IO.TextWriter) =
    w.Write("Usage:")
    w.Write(if full then "\n  " else " ")
    w.Write(CommandName)
    if Opts.Count > 0 then w.Write(" OPTIONS...")
    w.WriteLine(" ARGUMENTS...")

    if full && Opts.Count > 0 then
      w.WriteLine("Options:")
      for KeyValue (name, p) in Opts do
        let arg = if p.IsArgRequired then "=" + p.ArgName else ""
        fprintfn w "  -%s%s: %s" name arg p.Description

  let private OptionRx =
    Text.RegularExpressions.Regex(
      @"^-{1,2}([^:=]+?)([-+]|[:=](.*))?$")

  let private dropArgs n (args : ArraySegment<string>) =
    ArraySegment(args.Array, args.Offset + n, args.Count - n)

  /// Parses the given arguments into known containers.
  let ParseArgsSegment (args : ArraySegment<string>) =
    let rest = Collections.Generic.List<string>(args.Count)

    let rec loop maybeOpt (args : ArraySegment<string>) =
      let (| Option | _ |) arg =
        if maybeOpt then
          let m = OptionRx.Match(arg)
          if m.Success then
            Some (
              m.Groups[1].Value,
              if m.Groups[3].Success then
                Some m.Groups[3].Value
              elif m.Groups[2].Success then
                Some m.Groups[2].Value
              else
                None)
          else
            None
        else
          None

      if args.Count > 0 then
        match args.Array[args.Offset] with
        | "--" ->
          loop false (dropArgs 1 args)
        | Option (opt, arg) as arg0 ->
          match Opts.TryGetValue(opt) with
          | true, p ->
            match arg with
            | Some arg ->
              p.Parse(arg)
              loop true (dropArgs 1 args)
            | None ->
              if p.IsArgRequired then
                if args.Count > 1 then
                  p.Parse(args.Array[args.Offset + 1])
                  loop true (dropArgs 2 args)
                else
                  raise (FlagException("Option without argument", arg0))
              else
                p.Parse(String.Empty)
                loop true (dropArgs 1 args)
          | false, _ ->
            raise (FlagException("Unknown option provided", arg0))
        | arg ->
          rest.Add(arg)
          loop maybeOpt (dropArgs 1 args)

    loop true args
    rest.ToArray()

  /// Parses the given arguments into known containers.
  let inline ParseArgs args =
    ParseArgsSegment (ArraySegment args)

  let private dropProgram args =
    let rec next (args : ArraySegment<string>) =
      if args.Count > 0 then
        let arg = args.Array[args.Offset]
        match IO.Path.GetExtension(arg).ToLowerInvariant() with
        | ".fsx" | ".csx" ->
          Some args
        | ".exe" ->
          match next (dropArgs 1 args) with
          | None -> Some args
          | args -> args
        | _ ->
          next (dropArgs 1 args)
      else
        None

    match next args with
    | Some args ->
      if args.Count > 0 then
        let cmd = args.Array[args.Offset]
        CommandName <- IO.Path.GetFileName(cmd)
        dropArgs 1 args
      else
        args
    | None ->
      args

  /// Parses command line arguments obtained from the environment into
  /// known containers. Drops the initial script or executable name
  /// but uses it to set the command name.
  let ParseCommandLineArgs () =
    ArraySegment(Environment.GetCommandLineArgs())
    |> dropProgram
    |> ParseArgsSegment

  let internal Help = BoolOpt "help" "Display this informative message"

  /// Parses the given arguments into known containers, handling
  /// parsing problems and the help option. Passes additional
  /// arguments to a processing function. Returns an exit code.
  let WithArgsSegment proc args =
    try
      let args = ParseArgsSegment args
      if Help.IsSet then
        PrintUsage true stdout
        0
      else
        proc args
    with
    | :? FlagException as exn ->
      PrintUsage false stderr
      if not(String.IsNullOrEmpty(exn.Arg)) then
        eprintfn "Error processing argument %A: %s" exn.Arg exn.Message
      else
        eprintfn "Error processing arguments: %s" exn.Message
      1

  /// Parses the given arguments into known containers, handling
  /// parsing problems and the help option. Passes additional
  /// arguments to a processing function. Returns an exit code.
  let inline WithArgs proc args =
    WithArgsSegment proc (ArraySegment args)

  /// Parses command line arguments obtained from the environment into
  /// known containers, handling parsing problems and the help
  /// option. Passes additional arguments to a processing
  /// function. Exits the process with the return value from WithArgs.
  let WithCommandLineArgs proc =
    ArraySegment(Environment.GetCommandLineArgs())
    |> dropProgram
    |> WithArgsSegment proc
    |> Environment.Exit
