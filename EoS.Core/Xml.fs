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

namespace EoS.Xml
open System
open System.Xml.Linq
open FSharp.Data.UnitSystems.SI.UnitSymbols
open FSharp.Linq
open EoS.Units

/// XML namespace constants.
module XNamespace =
  let EoS = XNamespace.op_Implicit "http://chust.org/eos"

/// XML name constants.
module XName =
  let EoSPhase = XNamespace.EoS + "phase"
  let EoSCollection = XNamespace.EoS + "collection"

/// XML serialization interface. Classes that implement this interface
/// should also have a static deserialization method.
type IXSerializable =
  /// Name that should be used for XML elements representing the
  /// object.
  abstract XElementName : XName

  /// Convert the object into an XML element.
  abstract ToXElement : context:XFormatter -> XElement

  /// Convert an XML element into an object.
  //static abstract FromXElement : context:XFormatter * element:XElement -> obj

/// XML serialization context.
and XFormatter(?unitsystem : Lazy<UnitSystem>) =
  let unitsystem = defaultArg unitsystem (lazy UnitSystem.Load())

  let identities = Collections.Generic.Dictionary<obj, string>()
  let objects = Collections.Generic.Dictionary<string, obj>()

  /// Register an object that can be referenced by identifier.
  member this.RegisterObject(o, ?id) =
    let id =
      match id with
      | Some null | None -> sprintf "%s-%08x" (o.GetType().Name.ToString()) (o.GetHashCode())
      | Some id -> id
    identities.Add(o, id)
    objects.Add(id, o)
    id

  /// Try to obtain the registered identifier of an object, if any.
  member this.TryIdentify(o) =
    match identities.TryGetValue(o) with
    | true, id -> Some id
    | false, _ -> None

  /// Obtain a registered object.
  member this.TryGetObject<'T>(id) : Option<'T> =
    match objects.TryGetValue(id) with
    | true, (:? 'T as o) -> Some o
    | _ -> None

  /// The unit system used for conversions.
  member this.UnitSystem = unitsystem.Value

  /// Convert an element with optional "unit" attribute into a
  /// quantity. The stringified target unit must match the unit of
  /// measure 'U.
  member this.GetQuantity<[<Measure>] 'U>(target, element : XElement) : float<'U> =
    let source =
      query {
        for ut in element.Attributes(XNamespace.None + "unit") do
        select (Some ut.Value)
        exactlyOneOrDefault
      }
    let source =
      defaultArg source "1"

    let v =
      query {
        for va in element.Attributes(XNamespace.None + "value") do
        select (Some va.Value)
        exactlyOneOrDefault
      }
    let v =
      float (defaultArg v element.Value)
    let v =
      if target = source then
        v
      else
        UnitConverter(this.UnitSystem.Unit(source), this.UnitSystem.Unit(target)).Convert(v)

    unbox (box v)

  /// Convert a quantity into a "let" element with "name" and "unit"
  /// attributes. The stringified target unit must match the unit of
  /// measure 'U.
  static member Quantity<[<Measure>] 'U>(target : string, v : float<'U>, name : string, ?container) =
    let container = defaultArg container false

    let element = XElement(XNamespace.EoS + (if container then name else "let"))
    if not container then element.Add(XAttribute(XNamespace.None + "name", name))

    element.Add(XAttribute(XNamespace.None + "unit", target))
    if container then
      element.Add(XAttribute(XNamespace.None + "value", string v))
    else
      element.Add(string v)

    element

  /// Convert an XML element into an object.
  member this.Deserialize<'T>(element : XElement) : 'T =
    query {
      for id in element.Attributes(XNamespace.None + "ref") do
      select (Some id.Value)
      exactlyOneOrDefault
    }
    |> Option.bind (fun id ->
      match objects.TryGetValue(id) with
      | true, o -> Some o
      | false, _ -> None)
    |> function
      | Some o ->
        downcast o
      | None ->
        let id =
          query {
            for id in element.Attributes(XNamespace.None + "id") do
            select id.Value
            exactlyOneOrDefault
          }
        let typ =
          query {
            for typ in element.Attributes(XNamespace.None + "type") do
            select (Some typ.Value)
            exactlyOneOrDefault
          }
          |> function
            | Some typ ->
              typ
            | None ->
              let name = element.Name
              let here = Reflection.Assembly.GetExecutingAssembly().FullName
              if name = XName.EoSCollection then
                "EoS.Phases.PhaseCollection, " + here
              else
                failwithf "Element %O has no default type" element
        let o =
          Type.GetType(typ, true)
            .InvokeMember("FromXElement", Reflection.BindingFlags.InvokeMethod, null,
                          null, [| this; element |])
        this.RegisterObject(o, id) |> ignore
        downcast o

  /// Create a type attribute for a serialized object.
  static member TypeAttribute(t : Type) =
    XAttribute(XNamespace.None + "type", t.AssemblyQualifiedName)

  /// Convert an object into an XML element.
  member this.Serialize(o : #IXSerializable) =
    match this.TryIdentify(o) with
    | Some id ->
      XElement(
        o.XElementName,
        XAttribute(XNamespace.None + "ref", id))
    | None ->
      let element = o.ToXElement(this)
      element.Add(XAttribute(XNamespace.None + "id", this.RegisterObject(o)))
      element

/// Extension methods for XML elements.
[<AutoOpen>]
module XElementExtensions =
  type XElement with
    /// Retrieve a "let" binding element from this element or the
    /// document root, if possible.
    member this.TryGetBinding(name) =
      let from (element : XElement) =
        query {
          for it in element.Elements(XNamespace.EoS + "let") do
          where (it.Attribute(XNamespace.None + "name").Value = name)
          select it
          exactlyOneOrDefault
        }

      match from this with
      | null ->
        match this.Document with
        | null ->
          None
        | doc ->
          match from doc.Root with
          | null ->
            None
          | it ->
            Some it
      | it ->
        Some it

    /// Retrieve a "let" binding element from this element or the
    /// document root, throw a KeyNotFoundException if no matching
    /// binding could be found.
    member this.Binding(name) =
      match this.TryGetBinding(name) with
      | Some element -> element
      | None -> raise (Collections.Generic.KeyNotFoundException(sprintf "No binding for %A" name))
