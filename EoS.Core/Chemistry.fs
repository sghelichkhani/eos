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

namespace EoS.Chemistry
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open Murphy.ByteString.Bare

/// Base class of chemical elements and formulas.
type [<AbstractClass; Encoding(typeof<FormulaEncoding>)>] Formula internal () =
  /// Whether the formula contains only non-negative amounts of material.
  abstract IsValid : bool

  /// Molar mass of the formula.
  abstract Mass : float<kg/mol>

  /// Count of atoms in the formula.
  abstract Atoms : float

  /// Number of items constituting the formula or zero for an element.
  abstract Length : int

  /// Extract a single formula, count pair from a composite formula.
  abstract Item : index:int -> Formula * float with get

  /// Obtain the constituent formulas and counts as a sequence of pairs.
  member private this.Items =
    Seq.init this.Length (fun i -> this[i])

  /// Flatten a formula into a sorted list of element, count pairs.
  /// Atom counts are multiplied by n and added to existing entries.
  abstract FlattenTo : acc:Collections.Generic.SortedList<Element, float<'u>> * n:float<'u> -> unit

  /// Flatten a formula into a sorted list of element, count pairs.
  /// Atom counts are multiplied by n, if given.
  member this.Flatten(?n) =
    let acc = Collections.Generic.SortedList<Element, float>(256)
    this.FlattenTo(acc, defaultArg n 1.0)
    acc

  interface Collections.Generic.IList<Formula * float> with
    override this.Count =
      this.Length

    override this.IsReadOnly =
      true

    override this.Item
      with get index = this[index]
      and set index item = invalidOp "Chemical formulas are immutable"

    override this.Insert(index, item) =
      invalidOp "Chemical formulas are immutable"

    override this.Add(item) =
      invalidOp "Chemical formulas are immutable"

    override this.RemoveAt(index) =
      invalidOp "Chemical formulas are immutable"

    override this.Remove(item) =
      invalidOp "Chemical formulas are immutable"

    override this.Clear() =
      invalidOp "Chemical formulas are immutable"

    override this.Contains(item) =
      Seq.exists ((=) item) this.Items

    override this.IndexOf(item) =
      match Seq.tryFindIndex ((=) item) this.Items with
      | Some i -> i
      | None -> -1

    override this.CopyTo(target, start) =
      Seq.iteri (fun i item -> target[start + i] <- item) this.Items

    override this.GetEnumerator() =
      this.Items.GetEnumerator()

    override this.GetEnumerator() =
      (this.Items :> Collections.IEnumerable).GetEnumerator()

  /// Scale each item in a formula by a given factor.
  static member ( * ) (formula : Formula, scale : float) : Formula =
    match scale with
    | 0.0 -> upcast Composite[]
    | 1.0 -> formula
    | scale -> upcast Composite(Seq.map (fun (f, n) -> f, n * scale) formula)

  /// Scale each item in a formula by a given factor.
  static member inline ( * ) (scale : float, formula : Formula) : Formula =
    formula * scale

  /// Scale each item in a formula by a given inverse factor.
  static member (/) (formula : Formula, scale : float) : Formula =
    match scale with
    | 1.0 -> formula
    | scale when Double.IsInfinity(scale) -> upcast Composite[]
    | scale -> upcast Composite(Seq.map (fun (f, n) -> f, n / scale) formula)

/// Class of chemical elements.
and [<Sealed>] Element internal (name : string, ordinal : int, blurb : string, mass : float<kg/mol>) =
  inherit Formula()

  /// Short, symbolic name of the element.
  member this.Name = name

  /// Ordinal number of the element.
  member this.Ordinal = ordinal

  /// Long, human readable name of the element.
  member this.Blurb = blurb

  override this.IsValid = true

  override this.Mass = mass

  override this.Atoms = 1.0

  override this.Length = 0

  override this.Item
    with get index =
      IndexOutOfRangeException("Attempt to reference constituent of atomic formula") |> raise

  override this.FlattenTo(acc, n) =
    if n <> 0.0<_> then
      let mutable v = 0.0<_>
      acc.TryGetValue(this, &v) |> ignore
      acc[this] <- v + n

  override this.ToString() =
    name

  override this.Equals(that) =
    (this :> IComparable).CompareTo(that) = 0

  override this.GetHashCode() =
    ordinal

  interface IComparable<Element> with
    override this.CompareTo(that) =
      sign (this.Ordinal - that.Ordinal)

  interface IComparable with
    override this.CompareTo(that) =
      match that with
      | :? Element as that -> (this :> IComparable<Element>).CompareTo(that)
      | _ -> invalidArg (nameof that) "Attempt to compare an atomic formula with something else"

/// Class of composite formulas
and [<Sealed>] Composite(items : seq<Formula * float>) =
  inherit Formula()

  let items = Array.ofSeq items

  override this.IsValid =
    Array.forall (fun (f : Formula, n) -> f.IsValid && n >= 0.0) items

  override this.Mass =
    Array.fold (fun acc (f : Formula, n) -> acc + n * f.Mass) 0.0<_> items

  override this.Atoms =
    Array.fold (fun acc (f : Formula, n) -> acc + n * f.Atoms) 0.0 items

  override this.Length =
    items.Length

  override this.Item
    with get index =
      items[index]

  override this.FlattenTo(acc, n0) =
    if n0 <> 0.0<_> then
      for f, n1 in items do
        f.FlattenTo(acc, n0 * n1)

  override this.ToString() =
    let acc = Text.StringBuilder(4 * items.Length)
    for f, n in items do
      if f.Length > 0 then acc.Append('(') |> ignore
      acc.Append(f) |> ignore
      if f.Length > 0 then acc.Append(')') |> ignore
      if n <> 1.0 then acc.Append(n) |> ignore
    acc.ToString()

  override this.Equals(that) =
    Object.ReferenceEquals(this, that) ||
    match that with
    | :? Composite as that -> Seq.forall2 (=) this that
    | _ -> false

  override this.GetHashCode() =
    Array.fold (fun acc (f, n) -> acc + (int n) * (hash f)) (items.Length <<< 24) items

/// BARE serialization support for chemical formulas.
and [<Sealed>] FormulaEncoding() as this =
  inherit Encoding<Formula>(nameof Formula)
  
  let tag = Encoding.ofUInt

  let name = Encoding.ofString

  let ElementModule = Type.GetType("EoS.Chemistry.ElementModule")

  let items : Encoding<seq<Formula * float>> =
    [|this.ToErased(); Encoding.ofFloat64.ToErased()|]
    |> Encoding.ofTuple $"%s{this.Name} * f64" (fun (f, x) -> [|f; box x|]) (fun o -> (downcast o[0], unbox o[1]))
    |> Encoding.ofSeq

  override this.Decode(inp) =
    tag.Decode(inp)
    |> Result.bind (fun (tag, inp) ->
      match tag with
      | 0UL ->
        name.Decode(inp)
        |> Result.map (fun (name, rest) ->
          match ElementModule.GetProperty(name, Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Static) with
          | null -> failwithf "Unknown element %A" name
          | prop -> downcast prop.GetValue(null, null), rest)
      | 1UL ->
        items.Decode(inp)
        |> Result.map (fun (items, rest) ->
          Composite(items), rest)
      | _ ->
        Error "Invalid union tag")

  override this.Encode(out, v) =
    match v with
    | :? Element ->
      tag.Encode(out, 0UL)
      name.Encode(out, string v)
    | _ ->
      tag.Encode(out, 1UL)
      items.Encode(out, v)
