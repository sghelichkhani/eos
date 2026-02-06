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

open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.RegularExpressionConstants
open EoS.Units
open EoS.Phases
open EoS.CommandLine
open EoS.Slackbot

let database = Flag.DatabaseOpt "db" "Thermodynamic database"
let token = Flag.StringSetOpt "token" "Authorization token"
let listen = Flag.StringOpt "listen" "http://localhost:8080/" "Listen on this address"

type PropertyInfo =
  { Name : string
    Unit : string
    Eval : IThermoElastic -> float<Pa> -> float<K> -> float[] -> float[] }

let PropertiesByName : Map<string, PropertyInfo> =
  [{ Name = "V"
     Unit = "m^3/mol"
     Eval = fun it p T x ->
       Array.singleton (it.Volume(p, T, x)/1.0<m^3/mol>) }
   { Name = "rho"
     Unit = "kg/m^3"
     Eval = fun it p T x ->
       Array.singleton (it.Density(p, T, x)/1.0<kg/m^3>) }
   { Name = "beta"
     Unit = "1/Pa"
     Eval = fun it p T x ->
       Array.singleton (it.Compressibility(p, T, x)/1.0<1/Pa>) }
   { Name = "alpha"
     Unit = "1/K"
     Eval = fun it p T x ->
       Array.singleton (it.Expansivity(p, T, x)/1.0<1/K>) }
   { Name = "kappa&mu"
     Unit = "Pa"
     Eval = fun it p T x ->
       let κ, μ = it.Moduli(p, T, x)
       [| (κ/1.0<Pa>); (μ/1.0<Pa>) |] }
   { Name = "kappa"
     Unit = "Pa"
     Eval = fun it p T x ->
       let κ, _ = it.Moduli(p, T, x)
       Array.singleton (κ/1.0<Pa>) }
   { Name = "mu"
     Unit = "Pa"
     Eval = fun it p T x ->
       let _, μ = it.Moduli(p, T, x)
       Array.singleton (μ/1.0<Pa>) }
   { Name = "vp&vs"
     Unit = "m/s"
     Eval = fun it p T x ->
       let vp, vs = it.Velocities(p, T, x)
       [| (vp/1.0<m/s>); (vs/1.0<m/s>) |] }
   { Name = "vp"
     Unit = "m/s"
     Eval = fun it p T x ->
       let vp, _ = it.Velocities(p, T, x)
       Array.singleton (vp/1.0<m/s>) }
   { Name = "vs"
     Unit = "m/s"
     Eval = fun it p T x ->
       let _, vs = it.Velocities(p, T, x)
       Array.singleton (vs/1.0<m/s>) }
   { Name = "G"
     Unit = "J/mol"
     Eval = fun it p T x ->
       Array.singleton (it.Energy(p, T, x)/1.0<J/mol>) }
   { Name = "S"
     Unit = "J/mol/K"
     Eval = fun it p T x ->
       Array.singleton (it.Entropy(p, T, x)/1.0<J/mol/K>) }
   { Name = "Cp"
     Unit = "J/mol/K"
     Eval = fun it p T x ->
       Array.singleton (it.IsobaricHeatCapacity(p, T, x)/1.0<J/mol/K>) }
   { Name = "Cv"
     Unit = "J/mol/K"
     Eval = fun it p T x ->
       Array.singleton (it.IsochoricHeatCapacity(p, T, x)/1.0<J/mol/K>) }
   { Name = "gamma"
     Unit = "1"
     Eval = fun it p T x ->
       Array.singleton (it.Grueneisen(p, T, x)) }]
  |> Seq.map (fun it -> it.Name.ToLowerInvariant(), it)
  |> Map.ofSeq

let describe (db:EoS.Xml.XFormatter) o =
  match db.TryIdentify(o) with
  | Some id -> sprintf "%O (%s)" o id
  | None -> string o

type CommandInfo =
  { Regex : Text.RegularExpressions.Regex
    Eval : Text.RegularExpressions.Match -> string }

let inline private CommandRx pattern =
  Text.RegularExpressions.Regex(pattern, Text.RegularExpressions.RegexOptions.IgnoreCase)

let Commands : List<CommandInfo> =
  [{ Regex = CommandRx @"convert\s+([-+\d.e]+)(?:<([^<>]+)>|\s*(\S+))\s+to\s+(\S+)"
     Eval = fun it ->
       let us = database.Value.UnitSystem
       let v = float it.Groups[1].Value
       let src = us.Unit(if it.Groups[2].Success then it.Groups[2].Value else it.Groups[3].Value)
       let dst = us.Unit(it.Groups[4].Value)
       let c = UnitConverter(src, dst)
       let w = c.Convert(v)
       sprintf "%g<%O> = %g<%O>\nout = %s" v src w dst (c.ToString "in") }
   { Regex = CommandRx @"phases"
     Eval = fun it ->
       let db = database.Value
       db.TryGetObject<PhaseCollection>("ALL").Value
       |> Seq.map (fun it -> describe db it.Phase)
       |> String.concat(", ") }
   { Regex = CommandRx @"endmembers\s+of\s+(\S+)"
     Eval = fun it ->
       let db = database.Value
       match db.TryGetObject<IPhase>(it.Groups[1].Value) with
       | Some it ->
         describe db it + " : " +
         match it with
         | :? seq<PhaseCollectionItem> as it ->
           it
           |> Seq.map (fun it -> describe db it.Phase)
           |> String.concat(", ")
         | _ ->
           failwith "Not a solution phase"
       | None ->
         failwith "No such phase" }
   { Regex = CommandRx @"formula\s+of\s+([^][\s]+)(?:\s*\[([^][]+)\])?"
     Eval = fun it ->
       let db = database.Value
       match db.TryGetObject<IPhase>(it.Groups[1].Value) with
       | Some phase ->
         let x =
           if it.Groups[2].Success then
             CommaRx.Split(it.Groups[2].Value)
             |> Array.map double
           else
             null
         let formula = phase.Formula(x)
         sprintf "%O : %O, atoms = %g, mass = %g<kg>" phase formula formula.Atoms (formula.Mass/1.0<kg>)
       | None ->
         failwith "No such phase" }
   { Regex = CommandRx @"(\S+)\s+of\s+([^][\s]+)(?:\s*\[([^][]+)\])?\s+at\s+([-+\d.e]+)(?:<([^<>]+)>|\s*(\S+))(?:\s*,\s*|\s+and\s+)([-+\d.e]+)(?:<([^<>]+)>|\s*(\S+))(?:\s+in\s+(\S+))?"
     Eval = fun it ->
       let db = database.Value
       let us = db.UnitSystem
       match PropertiesByName.TryFind(it.Groups[1].Value.ToLowerInvariant()) with
       | Some property ->
         match db.TryGetObject<IThermoElastic>(it.Groups[2].Value) with
         | Some thel ->
           let x =
             if it.Groups[3].Success then
               CommaRx.Split(it.Groups[3].Value)
               |> Array.map double
             else
               null
           let P, T =
             let v0 = float it.Groups[4].Value
             let u0 = us.Unit(if it.Groups[5].Success then it.Groups[5].Value else it.Groups[6].Value)
             let v1 = float it.Groups[7].Value
             let u1 = us.Unit(if it.Groups[8].Success then it.Groups[8].Value else it.Groups[9].Value)
             let Pa = us.Unit("Pa")
             let K = us.Unit("K")
             try
               let cP, cT = UnitConverter(u0, Pa), UnitConverter(u1, K)
               cP.Convert(v0)*1.0<Pa>, cT.Convert(v1)*1.0<K>
             with
             | err0 ->
               try
                 let cP, cT = UnitConverter(u1, Pa), UnitConverter(u0, K)
                 cP.Convert(v1)*1.0<Pa>, cT.Convert(v0)*1.0<K>
               with
               | err1 ->
                 failwithf "%s\n%s" err0.Message err1.Message
           let u0 = us.Unit(property.Unit)
           let u1 = if it.Groups[10].Success then us.Unit(it.Groups[10].Value) else u0
           let cv = UnitConverter(u0, u1)
           property.Eval thel P T x
           |> Array.map (fun it -> sprintf "%g<%O>" (cv.Convert it) u1)
           |> String.concat ", "
           |> sprintf "%O : %s(%g<Pa>, %g<K>) = %s" thel property.Name (P/1.0<Pa>) (T/1.0<K>)
         | None ->
           failwith "No such thermoelastic object"
       | None ->
         failwith "No such property" }]

let onCommandEvent (evt : CommandEventArgs) =
  let cmd =
    Commands
    |> List.tryPick (fun it ->
      let m = it.Regex.Match(evt.Text)
      if m.Success then
        Some (it.Eval, m)
      else
        None)
  match cmd with
  | Some (proc, it) ->
    try
      evt.Response <- Some (Message.Channel <| proc it)
    with
    | err ->
      evt.Response <- Some (Message.Error err.Message)
  | None ->
    evt.Response <- Some (Message.Error "Command not recognized")

let main args =
  use listener = new Net.HttpListener(IgnoreWriteExceptions = true)
  listener.Prefixes.Add(listen.Value)

  let dispatch = SlackEventDispatcher(Tokens = token.Values)
  dispatch.CommandEvent.Add(onCommandEvent)

  Console.CancelKeyPress.Add(fun evt ->
    listener.Stop()
    evt.Cancel <- true)

  Console.WriteLine("Press Ctrl-C to quit...")
  listener.Start()
  dispatch.Run(listener)

  Console.WriteLine("Done.")
  0

[<STAThread; EntryPoint>]
let entry args =
  try Flag.WithArgs main args with
  | exn ->
    eprintfn "Unhandled exception: %O" exn
    1
