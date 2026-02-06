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

module EoS.IndexTool
open System
open EoS.Phases
open EoS.CommandLine

let init (comm : Lazy<EoS.MPI.Communicator>) =
  let ofs = Flag.StringOpt "d" "\t" "Output field separator"
  let database = Flag.DatabaseOpt "db" "Thermodynamic database"
  let offset = Flag.IntOpt "o" 0 "Offset added to all indices"

  let descend = ref None
  do Flag.Opts.Add("r", {
    new IFlag with
      override this.IsArgRequired = false
      override this.ArgName = "INDENT"
      override this.Description = "Recursively list members of collections"

      override this.Parse(arg) =
        let arg = if String.IsNullOrEmpty(arg) then "  " else arg
        descend := Some arg
  })

  let rec describe offset indent (items : seq<PhaseCollectionItem>) =
    for it in items do
      let offset = offset + it.XOffset
      printfn "%d%s%d%s%s%O" <|
      offset <| ofs.Value <| offset + it.XLength - 1 <| ofs.Value <|
      indent <| it.Phase

      match it.Phase, !descend with
      | :? seq<PhaseCollectionItem> as items, Some descend ->
        describe offset (indent + descend) items
      | _ ->
        ()

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

    printfn "#Composition vector indices for %s phases" blurb
    describe offset.Value String.Empty phases

    0

  main
