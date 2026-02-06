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
open System.Reflection
open System.Xml.Linq
open System.Text.RegularExpressions
open FSharp.Reflection
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.CommandLine

let out = Flag.StringOpt "o" "." "Output directory"

let private XNone = XNamespace.None

let private XHtml = XNamespace.op_Implicit("http://www.w3.org/1999/xhtml")

let private NamePrefixPattern =
  Regex("""^([A-Z]):""")

let private GenericSuffixPattern =
  Regex("""``[0-9]+""")

let summaryText (doc : Map<string, XElement>) (key : string) =
  match doc.TryFind(GenericSuffixPattern.Replace(key, String.Empty)) with
  | Some elt ->
    match elt.Element(XNone + "summary") with
    | null -> String.Empty
    | elt -> elt.Value.Trim()
  | None ->
    String.Empty

let moduleName (name : string) =
  let name = name.Replace('+', '.')
  if name.EndsWith("Module") then name[.. name.Length-7] else name

let rec typeSignature (typ : Type) full =
  if typ = typeof<unit> || typ = typeof<Void> then
    "unit"
  elif FSharpType.IsTuple typ then
    FSharpType.GetTupleElements typ
    |> Seq.map (fun elt -> typeSignature elt full)
    |> String.concat " * "
  elif FSharpType.IsFunction typ then
    let arg, ret = FSharpType.GetFunctionElements typ
    (typeSignature arg full) + " -> " + (typeSignature ret full)
  else
    let name =
      if typ.IsGenericParameter then
        "'" + typ.Name
      elif full then
        typ.FullName.Replace('+', '.')
      else
        let name = typ.Name
        match name.LastIndexOf('+') with
        | -1 -> name
        | ix -> name[ix+1 ..]
    match name.LastIndexOf('`') with
    | -1 -> name
    | ix -> name[.. ix-1] + "<" + (typ.GetGenericArguments()
                                    |> Seq.map (fun arg -> typeSignature arg full)
                                    |> String.concat ", ") + ">"

let typeMembers (typ : Type) =
  typ.GetMembers(
    BindingFlags.Public |||
    BindingFlags.Static ||| BindingFlags.Instance |||
    BindingFlags.DeclaredOnly)
  |> Seq.filter (function
    | :? MethodInfo as mem ->
      not (mem.IsSpecialName &&
           (mem.Name.StartsWith("get_") || mem.Name.StartsWith("set_")))
    | _ ->
      true)
  |> Seq.sortBy (fun it -> it.Name)
  |> Seq.toList

let typeKey (typ : Type) =
  "T+" + typ.FullName.Replace('+', '.')

let typeLabel (typ : Type) =
  if FSharpType.IsModule typ then
    "module " + moduleName typ.FullName
  else
    "type " + typeSignature typ false

let containerKey (typ : Type) =
  if typ.IsNested then
    typeKey typ.DeclaringType
  else
    "N+" + typ.Namespace

let argumentsSignature (args : ParameterInfo[]) =
  if args.Length > 0 then
    args
    |> Seq.map (fun it ->
       let isFun = FSharpType.IsFunction it.ParameterType
       it.Name + ":" +
       (if isFun then "(" else String.Empty) +
       (typeSignature it.ParameterType false) +
       (if isFun then ")" else String.Empty))
    |> String.concat " * "
  else
    "unit"

let fieldSignature (mem : MemberInfo) =
  let mut, typ =
    match mem with
    | :? FieldInfo as mem -> not mem.IsInitOnly, mem.FieldType
    | :? PropertyInfo as mem -> mem.CanWrite, mem.PropertyType
    | _ -> failwithf "Cannot determine type of field %O" mem
  (if mut then "mutable " else String.Empty) + mem.Name + " : " + (typeSignature typ false)

let fieldKey (mem : MemberInfo) =
  "F+" + mem.DeclaringType.FullName.Replace('+', '.') + "." + mem.Name

let memberSignature (mem : MemberInfo) =
  match mem with
  | :? Type as typ ->
    "type " + (typeSignature typ false)
  | :? PropertyInfo as mem ->
    let argi =
      mem.GetIndexParameters() |> argumentsSignature
    "member " + mem.Name +
    " : " + (if argi <> "unit" then argi + " -> " else String.Empty) +
    (typeSignature mem.PropertyType false) +
    if mem.CanRead then
      if mem.CanWrite then
        " with get, set"
      elif argi <> "unit" then
        " with get"
      else
        String.Empty
    elif mem.CanWrite then
      " with set"
    else
      String.Empty
  | :? MethodBase as mem ->
    let argi =
      mem.GetParameters() |> argumentsSignature
    match mem with
    | :? ConstructorInfo ->
      "new : " + argi
    | :? MethodInfo as mem ->
      let reti =
        if mem.ReturnType <> typeof<unit> then
          typeSignature mem.ReturnType false
        else
          "unit"
      "member " + mem.Name + " : " + argi + " -> " + reti
    | _ ->
      failwithf "Method of unknown type: %O" mem
  | _ ->
    "member " + mem.Name

let rec parameterKey (typ : Type) =
  if typ.IsGenericParameter then
    sprintf "``%d" typ.GenericParameterPosition
  elif typ.IsGenericType then
    let name =
      typ.GetGenericTypeDefinition().FullName.Replace('+', '.')
    let stop =
      match name.LastIndexOf('`') with
      | -1 -> name.Length
      | ix -> ix
    name[.. stop-1] + "{" + (
      typ.GetGenericArguments()
      |> Seq.map parameterKey
      |> String.concat ",") + "}"
  else
    typ.FullName.Replace('+', '.')

let memberKey (mem : MemberInfo) =
  let tk () = mem.DeclaringType.FullName.Replace('+', '.')
  match mem with
  | :? Type as typ ->
    typeKey typ
  | :? PropertyInfo as mem ->
    "P+" + tk () + "." + mem.Name +
    match mem.GetIndexParameters() with
    | null | [||] ->
      String.Empty
    | args ->
      "(" + (
        args
        |> Seq.map (fun it -> parameterKey it.ParameterType)
        |> String.concat ",") + ")"
  | :? MethodInfo as mem ->
    "M+" + tk () + "." + mem.Name +
    (if mem.IsGenericMethod then sprintf "``%d" (mem.GetGenericArguments().Length) else String.Empty) +
    "(" + (
      mem.GetParameters()
      |> Seq.map (fun it -> parameterKey it.ParameterType)
      |> String.concat ",") + ")"
  | :? EventInfo ->
    "E+" + tk () + "." + mem.Name
  | _ ->
    "M+" + tk () + "." + mem.Name

let makeLink (key : string) (label : string) =
  XElement(
    XHtml + "a",
    XAttribute(XNone + "href", Uri.EscapeDataString(key + ".xml")),
    label)

let makeLinkItem key label =
  XElement(XHtml + "li", makeLink key label)

let documentProvided (typ : Type) =
  typ.GetInterfaces()
  |> Seq.filter (fun typ ->
    typ.IsPublic && not(typ.Name.StartsWith("_")))
  |> Seq.map (fun typ ->
    XElement(
      XHtml + "li",
      "interface " + typeSignature typ false))
  |> Seq.toList

let documentMember doc (mem : MemberInfo) =
  match mem with
  | :? Type as typ ->
    [ XElement(
        XHtml + "dt",
        makeLink (typeKey typ) (typeLabel typ))
      XElement(
        XHtml + "dd",
        summaryText doc (typeKey typ)) ]
  | mem ->
    [ XElement(
        XHtml + "dt",
        memberSignature mem)
      XElement(
        XHtml + "dd",
        summaryText doc (memberKey mem)) ]

let documentNamespace doc (key : string) (tys : Type list) =
  [ XElement(XHtml + "h2", "namespace " + key[2..])
    XElement(XHtml + "p", summaryText doc key)
    XElement(XHtml + "h3", "Members")
    XElement(
      XHtml + "ul",
      tys |> List.map (fun typ ->
        makeLinkItem (typeKey typ) (typeLabel typ))) ]

let documentModule doc (typ : Type) =
  [ XElement(XHtml + "h2", "module " + moduleName typ.FullName)
    XElement(XHtml + "p", summaryText doc (typeKey typ))
    XElement(XHtml + "h3", "Members")
    XElement(
      XHtml + "dl",
      typeMembers typ
      |> List.collect (documentMember doc)) ]

let documentUnion doc (typ : Type) =
  let cases =
    FSharpType.GetUnionCases typ
    |> Seq.map (fun it -> it.Name, it)
    |> Map.ofSeq
  let cases, more =
    let all = typeMembers typ
    all
    |> List.choose (function
      | :? Type as typ -> cases.TryFind(typ.Name)
      | _ -> None),
    all
    |> List.filter (function
      | :? Type as typ -> not(cases.ContainsKey(typ.Name))
      | _ -> true)
  [ XElement(XHtml + "h2", "union " + (typeSignature typ true))
    XElement(XHtml + "ul", documentProvided typ)
    XElement(XHtml + "p", summaryText doc (typeKey typ))
    XElement(XHtml + "h3", "Cases")
    XElement(
      XHtml + "ul",
      cases
      |> Seq.map (fun it ->
        let fis = it.GetFields()
        XElement(
          XHtml + "li",
          it.Name +
          if fis.Length > 0 then
            " of " + (
              fis
              |> Seq.map (fun fld ->
                typeSignature fld.PropertyType false)
              |> String.concat " * ")
          else
            String.Empty))
      |> Seq.toList)
    XElement(XHtml + "h3", "Other Members")
    XElement(
      XHtml + "dl",
      more
      |> Seq.collect (fun mem ->
        [ XElement(
            XHtml + "dt",
            memberSignature mem)
          XElement(
            XHtml + "dd",
            summaryText doc (memberKey mem)) ])
      |> Seq.toList) ]

let documentRecord doc (typ : Type) =
  let fields =
    Collections.Generic.HashSet(FSharpType.GetRecordFields typ)
  let fields, more =
    typeMembers typ
    |> List.partition (function
      | :? PropertyInfo as mem -> fields.Contains(mem)
      | _ -> false)
  [ XElement(XHtml + "h2", "record " + (typeSignature typ true))
    XElement(XHtml + "ul", documentProvided typ)
    XElement(XHtml + "p", summaryText doc (typeKey typ))
    XElement(XHtml + "h3", "Fields")
    XElement(
      XHtml + "dl",
      fields
      |> Seq.collect (fun mem ->
        [ XElement(
            XHtml + "dt",
            fieldSignature mem)
          XElement(
            XHtml + "dd",
            summaryText doc (memberKey mem)) ])
      |> Seq.toList)
    XElement(XHtml + "h3", "Other Members")
    XElement(
      XHtml + "dl",
      more
      |> Seq.collect (fun mem ->
        [ XElement(
            XHtml + "dt",
            memberSignature mem)
          XElement(
            XHtml + "dd",
            summaryText doc (memberKey mem)) ])
      |> Seq.toList) ]

let documentInterface doc (typ : Type) =
  [ XElement(XHtml + "h2", "interface " + (typeSignature typ true))
    XElement(XHtml + "ul", documentProvided typ)
    XElement(XHtml + "p", summaryText doc (typeKey typ))
    XElement(XHtml + "h3", "Members")
    XElement(
      XHtml + "dl",
      typeMembers typ
      |> List.collect (documentMember doc)) ]

let documentStruct doc (typ : Type) =
  let fields, more =
    typeMembers typ
    |> List.partition (function
      | :? PropertyInfo as mem ->
        match mem.GetIndexParameters() with
        | null | [||] -> true
        | _ -> false
      | _ ->
        false)
  [ XElement(XHtml + "h2", "struct " + (typeSignature typ true))
    XElement(XHtml + "ul", documentProvided typ)
    XElement(XHtml + "p", summaryText doc (typeKey typ))
    XElement(XHtml + "h3", "Fields")
    XElement(
      XHtml + "dl",
      fields
      |> Seq.collect (fun mem ->
        [ XElement(
            XHtml + "dt",
            fieldSignature mem)
          XElement(
            XHtml + "dd",
            summaryText doc (fieldKey mem)) ])
      |> Seq.toList)
    XElement(XHtml + "h3", "Other Members")
    XElement(
      XHtml + "dl",
      more
      |> List.collect (documentMember doc)) ]

let documentClass doc (typ : Type) =
  [ XElement(XHtml + "h2", "class " + (typeSignature typ true))
    XElement(
      XHtml + "ul",
      List.append <|
      (match typ.BaseType with
       | null ->
         List.empty
       | typ ->
         [ XElement(
             XHtml + "li",
             "inherit " + typeSignature typ false) ]) <|
      (documentProvided typ))
    XElement(XHtml + "p", summaryText doc (typeKey typ))
    XElement(XHtml + "h3", "Members")
    XElement(
      XHtml + "dl",
      typeMembers typ
      |> List.collect (documentMember doc)) ]

let documentType doc (typ : Type) =
  if FSharpType.IsModule typ then
    documentModule doc typ
  elif FSharpType.IsUnion typ then
    documentUnion doc typ
  elif FSharpType.IsRecord typ then
    documentRecord doc typ
  elif typ.IsInterface then
    documentInterface doc typ
  elif typ.IsSubclassOf(typeof<ValueType>) then
    documentStruct doc typ
  else
    documentClass doc typ

let dumpDocument (name : string) (elts : XElement list) =
  XDocument(
    XDocumentType("html", null, null, null),
    XElement(
      XHtml + "html",
      XElement(
        XHtml + "head",
        XElement(
          XHtml + "meta",
          XAttribute(XNone + "http-equiv", "content-type"),
          XAttribute(XNone + "content", "application/xhtml+xml;charset=utf-8")),
        XElement(
          XHtml + "meta",
          XAttribute(XNone + "charset", "utf-8")),
        XElement(
          XHtml + "title",
          name)),
      XElement(
        XHtml + "body",
        elts)))
    .Save(IO.Path.Combine(out.Value, name + ".xml"))

let documentAssembly (name : string) =
  let name = IO.Path.GetFullPath(name)
  let asm = Assembly.LoadFile(name)
  let xml = IO.Path.ChangeExtension(name, ".xml")

  let tys =
    asm.GetExportedTypes()
    |> Seq.filter (fun typ ->
      not typ.IsNested
      || FSharpType.IsModule typ.DeclaringType)

  let doc =
    XDocument.Load(Uri(xml, UriKind.Absolute).AbsoluteUri)
      .Descendants(XNone + "member")
    |> Seq.choose (fun elt ->
      match elt.Attribute(XNone + "name") with
      | null ->
        None
      | name ->
        let name = GenericSuffixPattern.Replace(NamePrefixPattern.Replace(name.Value, "$1+"), String.Empty)
        Some (name, elt))
    |> Map.ofSeq

  let cns =
    Collections.Generic.Dictionary<string, Type list>()

  for typ in tys do
    if FSharpType.IsModule typ then
      let cnk = typeKey typ
      if not (cns.ContainsKey(cnk)) then cns[cnk] <- []
    else
      let cnk = containerKey typ
      cns[cnk] <- typ :: match cns.TryGetValue(cnk) with
                          | false, _ -> []
                          | true, v -> v
    documentType doc typ
    |> dumpDocument (typeKey typ)

  for KeyValue (key, tys) in cns do
    if key.StartsWith("N+") then
      documentNamespace doc key tys
      |> dumpDocument key

  let key = "A+" + asm.GetName().Name
  let attr = asm.GetCustomAttributes(false)
  [ XElement(
      XHtml + "h1",
      attr |> Array.pick (function
        | :? AssemblyTitleAttribute as v -> Some v.Title
        | _ -> None))
    XElement(
      XHtml + "p",
      attr |> Array.pick (function
        | :? AssemblyDescriptionAttribute as v -> Some v.Description
        | _ -> None))
    XElement(XHtml + "h3", "Members")
    XElement(
      XHtml + "ul",
      cns.Keys
      |> Seq.map (fun key ->
        makeLinkItem key (
          if key.StartsWith("T+") then
            "module " + moduleName key[2..]
          else
            "namespace " + key[2..].Replace('+', '.')))
      |> Seq.toList) ]
  |> dumpDocument key

  key

let main args =
  [ XElement(XHtml + "h1", "Index")
    XElement(
      XHtml + "ul",
      args
      |> Seq.map documentAssembly
      |> Seq.map (fun key ->
        makeLinkItem key ("assembly " + key[2..]))
      |> Seq.toList) ]
  |> dumpDocument "index"
  0

[<EntryPoint>]
let entry args =
  Flag.WithArgs main args
