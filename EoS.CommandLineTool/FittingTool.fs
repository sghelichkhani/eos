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

module EoS.FittingTool
open System
open System.Xml.Linq
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.RegularExpressionConstants
open EoS.Chemistry
open EoS.DebyeModel
open EoS.Xml
open EoS.CommandLine
open EoS.MPI

let ParsePhase arg =
  let root = XDocument.Load(Uri(IO.Path.GetFullPath(arg), UriKind.Absolute).AbsoluteUri).Root
  let context = XFormatter(Flag.UnitSystem)
  context.Deserialize<DebyeSolid>(root)

let DefaultPhase =
  lazy {
    Blurb = "Parameter Fit"
    Formula = Element.H
    T0 = 300.0<K>
    V0 = 1.0e-6<m^3/mol>
    K0 = 1.0e10<Pa>
    K0_p = 4.0
    G0 = 0.0<Pa>
    G0_p = 0.0
    θ0 = 1000.0<K>
    γ0 = 1.0
    q0 = 0.0
    η0 = 0.0
    F0 = -1000.0e3<J/mol> }

type Observation = float<m^3/mol> * float<K> * float<J/mol>

type Request =
  | TPhase of e0:float<J/mol> * phase:DebyeSolid
  | TDone
  
type RPhase = list<float<J/mol> * DebyeSolid>

let init (comm : Lazy<Communicator>) =
  let phase = ValueFlag(DefaultPhase, "Initial guess of model parameters", ParsePhase)
  do Flag.Opts.Add("i", phase)

  let output = Flag.StringOpt "o" null "Final fit of model parameters"

  let fixelastic = Flag.BoolOpt "fixelastic" "Do not vary the elastic model parameters"
  let fixthermal = Flag.BoolOpt "fixthermal" "Do not vary the thermal model parameters"
  let fixF0 = Flag.BoolOpt "fixF0" "Do not vary the Helmholtz energy of the reference state"

  let scatter = Flag.FloatOpt "scatter" 1.0 "Parameter scatter adjustment factor"
  let samples = Flag.IntOpt "samples" 100 "Number of samples per generation"
  let generations = Flag.IntOpt "generations" 1000 "Number of generations to run"

  let rec readObservations () : seq<Observation> =
    match stdin.ReadLine() with
    | null ->
      Seq.empty
    | line ->
      let line =
        let pos = line.IndexOf('#')
        (if pos >= 0 then line[0 .. pos-1] else line).Trim()
      if String.IsNullOrEmpty(line) then
        readObservations ()
      else
        seq {
          let x = SpaceRx.Split(line)
          let V = (float x[0]) * 1.0<m^3/mol>
          let T = (float x[1]) * 1.0<K>
          let F = (float x[2]) * 1.0<J/mol>
          yield (V, T, F)
          yield! readObservations ()
        }

  let writePhase (phase : DebyeSolid) =
    let context = XFormatter(Flag.UnitSystem)
    let element = context.Serialize(phase)
    if output.IsSet then
      element.Save(output.Value)
    else
      element.Save(stdout)

  let rmsErrorF (observations : Observation[]) (phase : DebyeSolid) =
    observations
    |> Seq.choose (fun (V, T, F) ->
      let dF = phase.FreeEnergy(phase.VolumeToStrain(V), T) - F
      if Double.IsNaN(float dF) then
        None
      else
        Some (dF * dF))
    |> Seq.average
    |> sqrt

  let rng = Random()

  let random (v0 : float<'u>) (dv : float<'u>) : float<'u> =
    let rec next () =
      let x = 2.0 * (rng.NextDouble() - 0.5)
      let p = exp(-x*x / 2.0) / sqrt(2.0 * Math.PI)
      if rng.NextDouble() <= p then
        v0 + x * scatter.Value * dv
      else
        next ()
    next ()

  let randomPhase phase =
    let phase =
      if fixelastic.IsSet then
        phase
      else
        { phase with
            K0 = random phase.K0 100.0e9<Pa> |> max 0.0<Pa>
            K0_p = random phase.K0_p 2.0 }
    let phase =
      if fixthermal.IsSet then
        phase
      else
        { phase with
            θ0 = random phase.θ0 500.0<K> |> max phase.T0
            γ0 = random phase.γ0 2.0 |> max 0.0
            q0 = random phase.q0 2.0 |> max 0.0 }
    let phase =
      if fixF0.IsSet then
        phase
      else
        { phase with
            F0 = random phase.F0 1000.0e3<J/mol> |> min 0.0<J/mol> }
    phase

  let improvePhase nsamples observations e0 phase =
    nsamples
    |> Seq.unfold (fun n ->
      if n > 0 then
        Some (randomPhase phase, n - 1)
      else
        None)
    |> Seq.choose (fun phase ->
      let e1 = rmsErrorF observations phase
      if e1 < e0 then Some (e1, phase) else None)
    |> Seq.toList

  let main (comm : Communicator) args =
    let nsamples = samples.Value / comm.Size
    if comm.Rank = 0 then
      let observations = readObservations () |> Seq.toArray
      let phase = phase.Value
      let t0 = DateTime.Now

      let e0, phase =
        if comm.Size > 1 then
          for rank in 1 .. comm.Size-1 do
            eprintfn "# Processor %d/%d starting" rank comm.Size
            comm.Send(observations, rank)
          let rec loop n e0 phase =
            if n > 0 then
              do
                let dt = DateTime.Now - t0
                eprintfn "# Running for %O, %d generations left" dt n
              for rank in 1 .. comm.Size-1 do
                comm.Send(TPhase (e0, phase), rank)
              seq {
                yield improvePhase nsamples observations e0 phase
                for rank in 1 .. comm.Size-1 do
                  let data, _, _ = comm.Receive<RPhase>(rank)
                  yield data
              }
              |> List.concat
              |> function
                | [] ->
                  loop (n - 1) e0 phase
                | better ->
                  let e1, phase = Seq.minBy fst better
                  let n = n - 1
                  eprintfn "# Error improved by %g J/mol to %g J/mol" <|
                  (e0 - e1) / 1.0<J/mol> <| e1 / 1.0<J/mol>
                  loop n e1 phase
            else
              e0, phase
          let result = loop generations.Value (rmsErrorF observations phase) phase
          for rank in 1 .. comm.Size-1 do
            comm.Send(TDone, rank)
            eprintfn "# Processor %d/%d stopping" rank comm.Size
          result
        else
          let rec loop n e0 phase =
            if n > 0 then
              do
                let dt = DateTime.Now - t0
                eprintfn "# Running for %O, %d generations left" dt n
              match improvePhase nsamples observations e0 phase with
              | [] ->
                loop (n - 1) e0 phase
              | better ->
                let e1, phase = Seq.minBy fst better
                let n = n - 1
                eprintfn "# Error improved by %g J/mol to %g J/mol" <|
                (e0 - e1) / 1.0<J/mol> <| e1 / 1.0<J/mol>
                loop n e1 phase
            else
              e0, phase
          loop generations.Value (rmsErrorF observations phase) phase

      eprintfn "# Final error measure %g J/mol" (e0/1.0<J/mol>)
      writePhase phase
    else
      let observations, _, _ = comm.Receive<Observation[]>(0)

      let rec loop () =
        match comm.Receive(0) with
        | TPhase (e0, phase), rank, _ ->
          comm.Send(improvePhase nsamples observations e0 phase, rank)
          loop ()
        | TDone, _, _ ->
          ()
      loop ()

    0

  comm.Force()
  |> main
