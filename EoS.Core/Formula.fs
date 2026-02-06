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
module EoS.Chemistry.Formula
open System
open System.Text.RegularExpressions
open EoS.Parsing
open EoS.Chemistry

/// Check whether a formula is a group of just one atom.
let isSingletonGroup (formula : Formula) =
  formula.Length = 1 &&
  let f, n = formula[0] in n = 1.0 && f :? Element

let private ElementPattern =
  Regex("""\G[A-Z][a-z]*""")

let private CountPattern =
  Regex("""\G_?([-+]?[0-9]+(?:\.[0-9]*)?)""")

let private ElementModule =
  Type.GetType("EoS.Chemistry.ElementModule")

/// Parse a chemical formula into a group.
let parse input : ParseResult<Composite> =
  let parseElement start stop : ParseResult<Element> =
    let m = ElementPattern.Match(input, start, stop - start)
    if m.Success then
      let name = m.Value
      match ElementModule.GetProperty(name, Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Static) with
      | null -> Failure (start, sprintf "Unknown element %A" name)
      | prop -> Success (downcast prop.GetValue(null, null), start + m.Length)
    else
      Failure (start, "Invalid element")

  let parseCount start stop : ParseResult<float> =
    let m = CountPattern.Match(input, start, stop - start)
    if m.Success then
      match Double.TryParse(m.Groups[1].Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
      | true, n -> Success (n, start + m.Length)
      | false, _ -> Failure (start, "Invalid formula count")
    else
      Success (1.0, start)

  let rec parseGroup start stop : ParseResult<Composite> =
    let rec track level start =
      if level <= 0 then
        Some start
      else
        let start = start + 1
        if start < stop then
          match input[start] with
          | '(' -> track (level + 1) start
          | ')' -> track (level - 1) start
          | _ -> track level start
        else
          None
    if start < stop && input[start] = '(' then
      match track 1 start with
      | Some stop ->
        match parseContent [] (start + 1) stop with
        | Success (items, _) -> Success (Composite(items), stop + 1)
        | Failure (pos, msg) -> Failure (pos, msg)
      | None ->
        Failure (start, "Unmatched parenthesis")
    else
      Failure (start, "Parenthesis expected")

  and parseContent acc start stop : ParseResult<list<Formula * float>> =
    let parseCountAndMore head start =
      match parseCount start stop with
      | Success (n, start) ->
        parseContent ((head, n) :: acc) start stop
      | Failure (pos, msg) ->
        Failure (pos, msg)
    let start = skipSpace input start stop
    if start < stop then
      match parseElement start stop with
      | Success (element, start) ->
        parseCountAndMore (element :> Formula) start
      | Failure (pos0, msg0) ->
        match parseGroup start stop with
        | Success (group, start) ->
          parseCountAndMore (group :> Formula) start
        | Failure (pos1, msg1) ->
          if pos1 > pos0 then
            Failure (pos1, msg1)
          else
            Failure (pos0, msg0)
    else
      Success (List.rev acc, start)

  match parseContent [] 0 input.Length with
  | Success (items, start) -> Success (Composite(items), start)
  | Failure (pos, msg) -> Failure (pos, msg)

/// Parse a chemical formula into a group, unwrap the contents of a potential singleton group,
/// throw a format exception in case of parser failure.
let ofString input : Formula =
  match parse input with
  | Success (formula, _) ->
    if isSingletonGroup formula then
      fst formula[0]
    else
      upcast formula
  | Failure (pos, msg) ->
    FormatException(sprintf "Formula %A failed to parse: %s at position %d" input msg pos) |> raise

/// Combine a sequence of formula, count pairs into a single flat
/// formula. If the result contains exactly one unit of a single
/// element, that element is returned.
let combineFlat (contributions : seq<Formula * float>) : Formula =
  let acc = Collections.Generic.SortedList<Element, float>(256)
  for formula, n in contributions do
    formula.FlattenTo(acc, n)
  if acc.Keys.Count = 1 && acc.Values[0] = 1.0 then
    upcast acc.Keys[0]
  else
    upcast Composite(Seq.map (fun (KeyValue (it, n)) -> upcast it, n) acc)

/// Combine a sequence of formula, count pairs into a single
/// formula. Each input formula is considered as a list of atomic
/// components. In the result, components occurring in multiple input
/// formulas are folded into one component with a total count.
let combineShallow (contributions : seq<Formula * float>) : Composite =
  let acc = Collections.Generic.Dictionary<Formula, float>()
  let collect n0 (formula, n1) =
    let mutable n = 0.0
    acc.TryGetValue(formula, &n) |> ignore
    acc[formula] <- n + n0 * n1

  for formula, n in contributions do
    if formula.Length > 0 then
      Seq.iter (collect n) formula
    else
      collect 1.0 (formula, n)

  Composite(Seq.map (|KeyValue|) acc)

/// Combine a sequence of formula, count pairs into a single
/// formula. Each input formula is considered as an ordered list of
/// subformula groups followed by a list of other components. The
/// subformula groups in the same position in each of the input
/// formulas are folded into corresponding subformula groups in the
/// output as if using combineShallow. The remaining other components
/// of all input formulas are folded into the tail of the output as if
/// using combineFlat. If the entire result is a singleton group, it
/// is unwrapped.
let combineGrouped (contributions : seq<Formula * float>) : Formula =
  let head = Collections.Generic.List<Formula>()
  let tail = Collections.Generic.SortedList<Element, float>(256)

  for formula, n0 in contributions do
    if formula.Length > 0 then
      let rec next i =
        if i < formula.Length then
          let group, n1 = formula[i]
          if group.Length > 0 then
            if i < head.Count then
              head[i] <- combineShallow [head[i], 1.0; group, n0 * n1]
            else
              head.Add(group * (n0 * n1))
            next (i + 1)
          else
            rest i
      and rest i0 =
        for i in i0 .. formula.Length - 1 do
          let part, n1 = formula[i]
          part.FlattenTo(tail, n0 * n1)
      next 0
    else
      formula.FlattenTo(tail, n0)

  let result =
    Composite(
      Seq.append <|
      (Seq.map (fun it -> it, 1.0) head) <|
      (Seq.map (fun (KeyValue (it, n)) -> upcast it, n) tail))

  if isSingletonGroup result then
    fst result[0]
  else
    upcast result
