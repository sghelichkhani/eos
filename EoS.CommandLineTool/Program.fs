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
open EoS.MPI
open EoS.CommandLine

let tools =
  Map.ofList [
    "form", EoS.FormulaTool.init
    "pidx", EoS.IndexTool.init
    "prop", EoS.PropertyTool.init
    "opti", EoS.OptimizationTool.init
    "adib", EoS.AdiabatTool.init
    "bmpv", EoS.BitmapTool.init
    "fitf", EoS.FittingTool.init
  ]

[<STAThread; EntryPoint>]
let entry args =
  let comm = lazy Communicator.Create()
  try
    try
      if args.Length >= 1 then
        match tools.TryFind args[0] with
        | Some toolInit ->
          let main = toolInit comm
          ArraySegment(args, 1, args.Length - 1)
          |> Flag.WithArgsSegment main
        | None ->
          eprintfn "Unknown tool: %s" args[0]
          printfn "Available tools:"
          (tools :> Collections.Generic.IDictionary<_, _>).Keys
          |> Seq.iter (printfn "  %s")
          1
      else
        printfn "Available tools:"
        (tools :> Collections.Generic.IDictionary<_, _>).Keys
        |> Seq.iter (printfn "  %s")
        0
    with
    | err ->
      eprintfn "Unhandled exception: %O" err
      #if DEBUG
      Console.WriteLine(err.StackTrace)
      #endif
      if comm.IsValueCreated then
        comm.Value.Abort(1)
      else
        1
  finally
    if comm.IsValueCreated then
      (comm.Value :> IDisposable).Dispose()
