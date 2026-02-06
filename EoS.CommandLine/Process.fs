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

/// Simple subprocess handling.
module EoS.CommandLine.Process
open System
open System.Diagnostics
open System.Text.RegularExpressions

let private ArgumentMetaRx =
  Regex("""["'\s]""")

let private ArgumentEscapeRx =
  Regex("""[\\"]""")

/// Quote a single argument for use in a ProcessStartInfo.
let quote s =
  if ArgumentMetaRx.IsMatch(s) then
    "\"" + ArgumentEscapeRx.Replace(s, MatchEvaluator(fun m -> "\\" + m.Value)) + "\""
  else
    s

let private arguments args =
  List.map quote args |> String.concat " "

/// Exception representing a non-zero process exit code.
exception ExitException of int

/// Wait for a process to exit. Closes all redirected I/O channels for
/// the process, closes the process handle and raises an ExitException
/// in case the process returned a non-zero exit code.
let wait (proc:Process) =
  let info = proc.StartInfo
  if info.RedirectStandardInput then proc.StandardInput.Close()
  if info.RedirectStandardOutput then proc.StandardOutput.Close()
  if info.RedirectStandardError then proc.StandardError.Close()
  proc.WaitForExit()
  let rc = proc.ExitCode
  proc.Close()
  if rc <> 0 then raise (ExitException rc)

/// Run a command with a list of arguments. Quotes the arguments, runs
/// the command in a new process and waits for it to return. Raises an
/// ExitException in case the process returns a non-zero exit code.
let run command args =
  use proc = Process.Start(command, arguments args)
  wait proc

/// Start a command with a list of arguments and with I/O
/// redirections. If dir contains "w", standard input will be
/// redirected; if dir contains "r", standard output will be
/// redirected; if dir contains "e", standard error will be
/// redirected. Quotes the arguments, runs the command in a new
/// process and returns the process object.
let popen (dir:string) command args =
  Process.Start(
    ProcessStartInfo(
      command, arguments args,
      UseShellExecute = false,
      RedirectStandardInput = dir.Contains("w"),
      RedirectStandardOutput = dir.Contains("r"),
      RedirectStandardError = dir.Contains("e")))
