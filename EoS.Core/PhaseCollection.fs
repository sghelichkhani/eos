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

namespace EoS.Phases
open System
open System.Xml.Linq
open EoS.Math
open EoS.Xml

/// A collection of phases with a generic, linear-programming-based optimization strategy.
type PhaseCollection(phases : seq<IPhase>) =
  let items, xlength =
    let xoffsets = phases |> Seq.scan (fun xoffset phase -> xoffset + phase.XLength) 0
    Seq.map2 (fun phase xoffset -> PhaseCollectionItem(phase, xoffset)) phases xoffsets |> Seq.toArray,
    Seq.last xoffsets

  /// The length of composition vectors for this phase collection.
  member this.XLength = xlength

  /// Fold over the phases and their composition slices.
  /// Phases with vanishing composition contribution are excluded from the fold.
  member this.XFold folder init x =
    items
    |> Seq.fold (fun acc it ->
      let it, x = it.Phase, it.XSlice(x)
      let f = Array.sum x
      if f > 0.0 then folder acc it f x else acc) init

  interface Collections.Generic.ICollection<PhaseCollectionItem> with
    member this.IsReadOnly = true
    member this.Count = items.Length

    member this.Contains(it) = Array.exists ((=) it) items
    member this.GetEnumerator() = (items :> Collections.Generic.IEnumerable<PhaseCollectionItem>).GetEnumerator()
    member this.GetEnumerator() = (items :> Collections.IEnumerable).GetEnumerator()
    member this.CopyTo(buffer, start) = items.CopyTo(buffer, start)

    member this.Add(it) = invalidOp "Phase collection is read-only"
    member this.Remove(it) = invalidOp "Phase collection is read-only"
    member this.Clear() = invalidOp "Phase collection is read-only"

  interface IThermoElastic with
    member this.XLength = xlength

    member this.Volume(p, T, x) =
      this.XFold (fun V it f x ->
        V + f * it.Volume(p, T, x)) 0.0<_> x

    member this.Density(p, T, x) =
      let m, V =
        this.XFold (fun (m, V) it f x ->
          m + f * it.Mass(x),
          V + f * it.Volume(p, T, x)) (0.0<_>, 0.0<_>) x
      m / V

    member this.Compressibility(p, T, x) =
      let V, dV =
        this.XFold (fun (V, dV) it f x ->
          let Vi = it.Volume(p, T, x)
          V + f * Vi,
          dV + f * it.Compressibility(p, T, x) * Vi) (0.0<_>, 0.0<_>) x
      dV / V

    member this.Expansivity(p, T, x) =
      let V, dV =
        this.XFold (fun (V, dV) it f x ->
          let Vi = it.Volume(p, T, x)
          V + f * Vi,
          dV + f * it.Expansivity(p, T, x) * Vi) (0.0<_>, 0.0<_>) x
      dV / V

    member this.Moduli(p, T, x) =
      let norm = Array.sum x
      let V, ĸ0, ĸ1, µ0, µ1 =
        this.XFold (fun (V, ĸ0, ĸ1, µ0, µ1) it f x ->
          let Vf = f * it.Volume(p, T, x)
          let ĸi, µi = it.Moduli(p, T, x)
          V + Vf,
          ĸ0 + Vf * ĸi, ĸ1 + Vf / ĸi,
          µ0 + Vf * µi, µ1 + Vf / µi) (0.0<_>, 0.0<_>, 0.0<_>, 0.0<_>, 0.0<_>) x
      (ĸ0/V + V/ĸ1) / 2.0, (µ0/V + V/µ1) / 2.0

    member this.Velocities(p, T, x) =
      ThermoElastic.VelocitiesFromModuli(this, p, T, x)

    member this.Energy(p, T, x) =
      this.XFold (fun G it f x ->
        G + f * it.Energy(p, T, x)) 0.0<_> x

    member this.Entropy(p, T, x) =
      this.XFold (fun S it f x ->
        S + f * it.Entropy(p, T, x)) 0.0<_> x

    member this.IsobaricHeatCapacity(p, T, x) =
      this.XFold (fun Cp it f x ->
        Cp + f * it.IsobaricHeatCapacity(p, T, x)) 0.0<_> x

    member this.IsochoricHeatCapacity(p, T, x) =
      this.XFold (fun Cp it f x ->
        Cp + f * it.IsochoricHeatCapacity(p, T, x)) 0.0<_> x

    member this.Grueneisen(p, T, x) =
      ThermoElastic.GrueneisenFromVAKC(this, p, T, x)

  static member FromXElement(context : XFormatter, element : XElement) =
    PhaseCollection(
      element.Elements(XName.EoSPhase)
      |> Seq.map context.Deserialize)

  interface IXSerializable with
    member this.XElementName = XName.EoSCollection

    member this.ToXElement(context : XFormatter) =
      let element = XElement(XName.EoSCollection)
      for it in items do element.Add(context.Serialize(downcast box it.Phase))
      element

  override this.Equals(that) =
    Object.ReferenceEquals(this, that) ||
    match that with
    | :? PhaseCollection as that ->
      Seq.zip this that
      |> Seq.forall (fun (a, b) ->
        a.Phase = b.Phase)
    | _ ->
      false

  override this.GetHashCode() =
    items
    |> Array.fold (fun acc it ->
      acc + it.Phase.GetHashCode()) 0
