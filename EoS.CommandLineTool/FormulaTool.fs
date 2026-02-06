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

module EoS.FormulaTool
open System
open EoS.RegularExpressionConstants
open EoS.Chemistry
open EoS.CommandLine

let FromFormats =
  ["molar",  id
   "mass",   Fractions.MassToMolar
   "atomic", Fractions.AtomicToMolar]
  |> Map.ofList

let ToFormats =
  ["molar",  id
   "mass",   Fractions.MolarToMass
   "atomic", Fractions.MolarToAtomic]
  |> Map.ofList

let init (comm : Lazy<EoS.MPI.Communicator>) =
  let ofs = Flag.StringOpt "d" "\t" "Output field separator"
  let flat = Flag.BoolOpt "flat" "Flatten the resulting composition"
  let table = Flag.BoolOpt "table" "Output a table rather than a single formula"

  let fromFormat = ref id
  do Flag.Opts.Add("from", {
    new IFlag with
      override this.IsArgRequired = true
      override this.ArgName = "FORMAT"
      override this.Description = "Format of input (molar, mass, atomic)"

      override this.Parse(arg) =
        match FromFormats.TryFind(arg.ToLowerInvariant()) with
        | Some proc -> fromFormat := proc
        | None -> raise(FlagException("Unknown input format", arg))
  })

  let toFormat = ref id
  do Flag.Opts.Add("to", {
    new IFlag with
      override this.IsArgRequired = true
      override this.ArgName = "FORMAT"
      override this.Description = "Format of output (molar, mass, atomic)"

      override this.Parse(arg) =
        match ToFormats.TryFind(arg.ToLowerInvariant()) with
        | Some proc -> toFormat := proc
        | None -> raise(FlagException("Unknown output format", arg))
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
        seq { yield SpaceRx.Split(line, 2)
              yield! readLines () }

  let main args =
    let components =
      if Array.isEmpty args then
        readLines ()
        |> Seq.map (fun x ->
          let formula = Formula.ofString x[1]
          let x = float x[0]
          formula, Some x)
      else
        seq {
          for i in 0 .. 2 .. args.Length-1 do
            if i+1 < args.Length then
              let formula = Formula.ofString args[i + 1]
              let x = float args[i]
              yield formula, Some x
            else
              let formula = Formula.ofString args[i]
              yield formula, None }

    let components =
      components
      |> Seq.fold (fun (total, acc) (formula, x) ->
        match x with
        | Some x ->
          (total + x), (formula, x) :: acc
        | None ->
          0.0, (formula, 1.0 - total) :: acc) (0.0, List.empty)
      |> snd
      |> List.rev
      |> Seq.ofList
      |> !fromFormat

    let components =
      if flat.IsSet then
        Composite(components).Flatten()
        |> Seq.map (fun (KeyValue (element, x)) -> element :> Formula, x)
      else
        components

    let components =
      components
      |> !toFormat

    if table.IsSet then
      for formula, x in components do
        printfn "%g%s%O" x ofs.Value formula
    else
      Composite(components)
      |> printfn "%O"

    0

  main
