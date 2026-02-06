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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EoS.PolynomialModel.Math.Polynomial
open System
open System.Text.RegularExpressions
open EoS.Parsing
open EoS.Math

/// Check whether a polynomial is a constant and extract its value in that case.
let tryGetConstant (poly : Polynomial<'A, 'R>) : float<'R> option =
  match List.ofSeq poly.Terms with
  | [0.0, false, c] -> Some (c |> box |> unbox)
  | [] -> Some 0.0<_>
  | _ -> None

/// Check whether a polynomial is a constant.
let isConstant poly =
  tryGetConstant poly
  |> Option.isSome

/// Check whether a polynomial is constantly zero.
let isZero poly =
  match tryGetConstant poly with
  | Some 0.0 -> true
  | _ -> false

let private AddPattern =
  Regex("""\G[-+]""")

let private MulPattern =
  Regex("""\G[*/]?""")

let private PowPattern =
  Regex("""\G\^|\*\*""")

let private NumPattern =
  Regex("""\G[-+]?[0-9]+(?:\.[0-9]*)?(?:[eE][-+]?[0-9]+)?""")

let private NamePattern =
  Regex("""\G[A-Za-z]+""")

/// Parse a polynomial from a string.
let parse input : ParseResult<Polynomial<'A, 'R>> =
  let parseAdd start : ParseResult<float> =
    let m = AddPattern.Match(input, start)
    if m.Success then
      let s = if m.Value = "-" then -1.0 else +1.0
      Success (s, start + m.Length)
    else
      Failure (start, "Additive operator expected")

  let parseMul start : ParseResult<float> =
    let m = MulPattern.Match(input, start)
    if m.Success then
      let s = if m.Value = "/" then -1.0 else +1.0
      Success (s, start + m.Length)
    else
      Failure (start, "Multiplicative operator expected")

  let parsePow start : ParseResult<float> =
    let m = PowPattern.Match(input, start)
    if m.Success then
      let start = skipSpace input (start + m.Length) input.Length
      let m = NumPattern.Match(input, start)
      if m.Success then
        match Double.TryParse(m.Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
        | true, p -> Success (p, start + m.Length)
        | false, _ -> Failure (start, "Invalid number")
      else
        Failure (start, "Invalid number")
    else
      Success (1.0, start)

  let parseNum start : ParseResult<float> =
    let m = NumPattern.Match(input, start)
    if m.Success then
      match Double.TryParse(m.Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
      | true, x -> Success (x, start + m.Length)
      | false, _ -> Failure (start, "Invalid number")
    else
      Success (1.0, start)

  let parseName start : ParseResult<string> =
    let m = NamePattern.Match(input, start)
    if m.Success then
      Success (m.Value, start + m.Length)
    else
      Failure (start, "Name expected")

  let rec parseTerms acc variable start : ParseResult<list<float * bool * float> * option<string>> =
    let parseHeadAndMore cs start =
      let start = skipSpace input start input.Length
      match parseNum start with
      | Success (c, start) ->
        let start = skipSpace input start input.Length
        match parseMul start with
        | Success (ps, start) ->
          let start = skipSpace input start input.Length
          match parseName start with
          | Success ("log", start) | Success ("ln", start) when ps > 0.0 ->
            let start = skipSpace input start input.Length
            match parseName start with
            | Success (name, start) when Option.forall ((=) name) variable ->
              let saved = skipSpace input start input.Length
              let variable = Some name
              match parseMul saved with
              | Success (ps, start) ->
                let start = skipSpace input start input.Length
                match parseName start with
                | Success (name, start) when Option.forall ((=) name) variable ->
                  let start = skipSpace input start input.Length
                  let variable = Some name
                  match parsePow start with
                  | Success (p, start) ->
                    parseTerms ((ps * p, true, cs * c) :: acc) variable start
                  | Failure (pos, msg) ->
                    Failure (pos, msg)
                | Success _ ->
                  Failure (start, "Invalid variable name")
                | Failure (pos, msg) ->
                  parseTerms ((0.0, true, cs * c) :: acc) variable saved
              | Failure _ ->
                parseTerms ((0.0, true, cs * c) :: acc) variable saved
            | Success _ ->
              Failure (start, "Invalid variable name")
            | Failure (pos, msg) ->
              Failure (pos, msg)
          | Success (name, start) when Option.forall ((=) name) variable ->
            let start = skipSpace input start input.Length
            let variable = Some name
            match parsePow start with
            | Success (p, start) ->
              let saved = skipSpace input start input.Length
              match parseMul saved with
              | Success (ps', start) ->
                let start = skipSpace input start input.Length
                match parseName start with
                | Success ("log", start) | Success ("ln", start) when ps' > 0.0 ->
                  let start = skipSpace input start input.Length
                  match parseName start with
                  | Success (name, start) when Option.forall ((=) name) variable ->
                    let start = skipSpace input start input.Length
                    let variable = Some name
                    parseTerms ((ps * p, true, cs * c) :: acc) variable start
                  | Success _ ->
                    Failure (start, "Invalid variable name")
                  | Failure (pos, msg) ->
                    Failure (pos, msg)
                | _ ->
                  parseTerms ((ps * p, false, cs * c) :: acc) variable saved
              | Failure _ ->
                parseTerms ((ps * p, false, cs * c) :: acc) variable saved
            | Failure (pos, msg) ->
              Failure (pos, msg)
          | Success _ ->
            Failure (start, "Invalid variable name")
          | Failure _ ->
            parseTerms ((0.0, false, cs * c) :: acc) variable start
        | Failure (pos, msg) ->
          Failure (pos, msg)
      | Failure (pos, msg) ->
        Failure (pos, msg)

    let start = skipSpace input start input.Length
    if start < input.Length then
      match parseAdd start with
      | Success (cs, start) -> parseHeadAndMore cs start
      | Failure _ when List.isEmpty acc -> parseHeadAndMore +1.0 start
      | Failure (pos, msg) -> Failure (pos, msg)
    else
      Success ((List.rev acc, variable), start)

  match parseTerms [] None 0 with
  | Success ((terms, variable), start) -> Success (Polynomial(terms, defaultArg variable "x"), start)
  | Failure (pos, msg) -> Failure (pos, msg)

/// Parse a polynomial, throw a format exception in case of parser failure.
let ofString input : Polynomial<'A, 'R> =
  match parse input with
  | Success (poly, _) ->
    poly
  | Failure (pos, msg) ->
    FormatException(sprintf "Polynomial %A failed to parse: %s at position %d" input msg pos) |> raise
