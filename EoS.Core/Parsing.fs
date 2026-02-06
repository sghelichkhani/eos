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

module EoS.Parsing
open System.Text.RegularExpressions

/// Type representing a parse result: Either some value and a following position
/// or a position and a human-readable message.
type ParseResult<'T> =
  | Success of 'T * int
  | Failure of int * string

let private SpacePattern =
  Regex("""\G\s+""")

/// Skip whitespace in input[start .. stop-1] and return a new start position.
let skipSpace input start stop =
  let m = SpacePattern.Match(input, start, stop - start)
  if m.Success then
    start + m.Length
  else
    start
