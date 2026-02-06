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
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.PhysicalConstants
open EoS.Chemistry
open EoS.Xml

/// Representation of a phase using the regular solution model.
type RegularSolution(endmembers : seq<IPhase>, ?sizes : seq<IPhase * float>, ?interactions : seq<IPhase * IPhase * float<J/mol>>, ?allowsNegativeComponents : bool, ?excludesEndmemberConfigurationEntropy : bool, ?blurb : string) =
  let blurb =
    match blurb with
    | Some v ->
      v
    | None ->
      let buf = Text.StringBuilder("RegularSolution[")
      endmembers
      |> Seq.iteri (fun i phase ->
        if i > 0 then buf.Append("; ") |> ignore
        buf.Append(string phase) |> ignore)
      buf.Append(']').ToString()

  let items =
    endmembers
    |> Seq.mapi (fun xindex phase ->
      if phase.XLength = 1 then
        PhaseCollectionItem(phase, xindex)
      else
        invalidArg (nameof endmembers) "Endmembers with variable composition not supported")
    |> Seq.toArray

  let index =
    dict <| seq { for it in items -> it.Phase, it.XOffset }

  let sizes =
    let tmp = Array.create items.Length 1.0
    for phase, s in defaultArg sizes Seq.empty do
      tmp[index[phase]] <- s
    tmp

  let interindex i j =
    let i, j = if i <= j then i, j else j, i
    (i * items.Length) - ((i + 1) * i / 2) + (j - i - 1)

  let interactions =
    let tmp = Array.create (items.Length * (items.Length - 1) / 2) 0.0<J/mol>
    for phase0, phase1, x in defaultArg interactions Seq.empty do
      tmp[interindex index[phase0] index[phase1]] <- x
    tmp

  /// Whether the solution should allow negative endmember amounts.
  member val AllowsNegativeComponents =
    defaultArg allowsNegativeComponents false

  /// Whether the solution should assume that configurational entropy of pure
  /// endmembers is already accounted for in the endmember model.
  member val ExcludesEndmemberConfigurationEntropy =
    defaultArg excludesEndmemberConfigurationEntropy false

  /// Fold over the endmembers and their relative composition contributions.
  /// Endmembers with vanishing composition contribution are excluded from the fold.
  member this.XFold folder init x =
    let norm = Array.sum x
    items
    |> Seq.fold (fun acc it ->
      let it, f = it.Phase, x[it.XOffset] / norm
      if f <> 0.0 then folder acc it f else acc) init

  /// The number of sites indicated by groups in the endmember formulas.
  member val Sites =
    items
    |> Seq.map (fun it ->
      let formula = it.Phase.Formula()
      formula
      |> Seq.tryFindIndex (fun (f, _) -> f.Length < 1)
      |> function
        | Some i -> i
        | None -> formula.Length)
    |> Seq.max

  /// Endmember size parameter accessors.
  member this.Size
    with get phase = sizes[index[phase]]

  /// Endmember interaction parameter accessors.
  member this.Interaction
    with get (phase0, phase1) = interactions[interindex index[phase0] index[phase1]]

  /// Configurational entropy for the solution. Reduced by pure endmember
  /// contributions, if ExcludesEndmemberConfigurationEntropy is set.
  member this.ConfigurationEntropy(x : float[]) : float<J/mol/K> =
    let contrib (x : float[]) =
      seq {
        for i in 0 .. this.Sites-1 do
          let groups =
            Collections.Generic.Dictionary<Formula, float>()
          let m, n =
            this.XFold (fun (m, n) it f ->
              let group, m1 = it.Formula()[i]
              for group, k in group * f do
                groups[group] <-
                  match groups.TryGetValue(group) with
                  | true, k0 -> k0 + k
                  | false, _ -> k
              m + f * m1,
              n + f * group.Atoms) (0.0, 0.0) x
          for KeyValue (group, k) in groups do
            let xk = k * group.Atoms / n
            if xk > 0.0 then 
              yield -m * xk * log xk
            elif xk < 0.0 then
              invalidArg (nameof x) "Composition results in negative amounts of matter"
      }
      |> Seq.sum

    MolarGas * (
      contrib x -
      if this.ExcludesEndmemberConfigurationEntropy then
        this.XFold (fun (acc, i) _ f ->
          let x = Array.init x.Length (fun j -> if i = j then 1.0 else 0.0)
          acc + f * contrib x, i + 1) (0.0, 0) x
        |> fst
      else
        0.0
    )

  /// Interaction energy for the solution.
  member this.InteractionEnergy(x : float[]) : float<J/mol> =
    let norm = Array.sum x

    let dx, dn =
      items
      |> Seq.fold (fun (dx, dn) it ->
        let dxi = x[it.XOffset] * sizes[it.XOffset] / norm
        if dxi <> 0.0 then
          dx + dxi, dn + dxi * it.Phase.Atoms()
        else
          dx, dn) (0.0, 0.0)

    seq {
      for it0 in items do
        let f0 = x[it0.XOffset] / norm
        if f0 <> 0.0 then
          let s0 = sizes[it0.XOffset]
          let ϕ0 = f0 * s0 * it0.Phase.Atoms() / dn
          for it1 in items[it0.XOffset+it0.XLength .. items.Length-1] do
            let f1 = x[it1.XOffset] / norm
            if f1 <> 0.0 then
              let s1 = sizes[it1.XOffset]
              let ϕ1 = f1 * s1 * it1.Phase.Atoms() / dn
              yield ϕ0 * ϕ1 * 2.0 * dx * interactions[interindex it0.XOffset it1.XOffset] / (s0 + s1)
    }
    |> Seq.sum

  interface Collections.Generic.ICollection<PhaseCollectionItem> with
    member this.IsReadOnly = true
    member this.Count = items.Length

    member this.Contains(it) = Array.exists ((=) it) items
    member this.GetEnumerator() = (items :> Collections.Generic.IEnumerable<PhaseCollectionItem>).GetEnumerator()
    member this.GetEnumerator() = (items :> Collections.IEnumerable).GetEnumerator()
    member this.CopyTo(buffer, start) = items.CopyTo(buffer, start)

    member this.Add(it) = invalidOp "Regular solution is read-only"
    member this.Remove(it) = invalidOp "Regular solution is read-only"
    member this.Clear() = invalidOp "Regular solution is read-only"

  interface IPhase with
    member this.AllowsNegativeComponents = this.AllowsNegativeComponents

    member this.XLength = items.Length

    member this.Mass(x) =
      this.XFold (fun m it f ->
        m + f * it.Mass()) 0.0<_> x

    member this.Atoms(x) =
      this.XFold (fun n it f ->
        n + f * it.Atoms()) 0.0 x

    member this.Formula(x) =
      this.XFold (fun gs it f ->
        (it.Formula(), f) :: gs) [] x
      |> List.rev
      |> Formula.combineGrouped

    member this.Volume(p, T, x) =
      this.XFold (fun V it f ->
        V + f * it.Volume(p, T)) 0.0<_> x

    member this.Density(p, T, x) =
      let m, V =
        this.XFold (fun (m, V) it f ->
          m + f * it.Mass(),
          V + f * it.Volume(p, T)) (0.0<_>, 0.0<_>) x
      m / V

    member this.Compressibility(p, T, x) =
      let V, dV =
        this.XFold (fun (V, dV) it f ->
          let Vi = it.Volume(p, T)
          V + f * Vi,
          dV + f * it.Compressibility(p, T) * Vi) (0.0<_>, 0.0<_>) x
      dV / V

    member this.Expansivity(p, T, x) =
      let V, dV =
        this.XFold (fun (V, dV) it f ->
          let Vi = it.Volume(p, T)
          V + f * Vi,
          dV + f * it.Expansivity(p, T) * Vi) (0.0<_>, 0.0<_>) x
      dV / V

    member this.Moduli(p, T, x) =
      let ĸ0, ĸ1, µ0, µ1 =
        this.XFold (fun (ĸ0, ĸ1, µ0, µ1) it f ->
          let ĸi, µi = it.Moduli(p, T)
          ĸ0 + f * ĸi, ĸ1 + f / ĸi,
          µ0 + f * µi, µ1 + f / µi) (0.0<_>, 0.0<_>, 0.0<_>, 0.0<_>) x
      (ĸ0 + 1.0/ĸ1) / 2.0, (µ0 + 1.0/µ1) / 2.0

    member this.Velocities(p, T, x) =
      ThermoElastic.VelocitiesFromModuli(this, p, T, x)

    member this.Energy(p, T, x) =
      this.XFold (fun G it f ->
        G + f * it.Energy(p, T)) 0.0<_> x -
      T * this.ConfigurationEntropy(x) +
      this.InteractionEnergy(x)

    member this.Entropy(p, T, x) =
      this.XFold (fun S it f ->
        S + f * it.Entropy(p, T)) 0.0<_> x +
      this.ConfigurationEntropy(x)

    member this.IsobaricHeatCapacity(p, T, x) =
      this.XFold (fun Cp it f ->
        Cp + f * it.IsobaricHeatCapacity(p, T)) 0.0<_> x

    member this.IsochoricHeatCapacity(p, T, x) =
      this.XFold (fun Cp it f ->
        Cp + f * it.IsochoricHeatCapacity(p, T)) 0.0<_> x

    member this.Grueneisen(p, T, x) =
      ThermoElastic.GrueneisenFromVAKC(this, p, T, x)

  static member FromXElement(context : XFormatter, element : XElement) =
    let blurb = element.Element(XNamespace.EoS + "blurb").Value
    let allowsNegativeComponents = Boolean.Parse(element.Binding("allows-negative-components").Value)
    let excludesEndmemberConfigurationEntropy = Boolean.Parse(element.Binding("excludes-endmember-configuration-entropy").Value)

    let endmembers =
      [
        for it in element.Elements(XName.EoSPhase) ->
          context.Deserialize<IPhase>(it)
      ]

    let sizes =
      seq {
        for it in element.Elements(XNamespace.EoS + "size") ->
          context.Deserialize<IPhase>(it.Element(XName.EoSPhase)),
          context.GetQuantity<1>("1", it)
      }

    let interactions =
      seq {
        for it in element.Elements(XNamespace.EoS + "interaction") ->
          let phases = it.Elements(XName.EoSPhase) |> Seq.toArray
          context.Deserialize<IPhase>(phases[0]),
          context.Deserialize<IPhase>(phases[1]),
          context.GetQuantity<J/mol>("J/mol", it)
      }

    RegularSolution(endmembers, sizes, interactions, allowsNegativeComponents, excludesEndmemberConfigurationEntropy, blurb)

  interface IXSerializable with
    member this.XElementName = XName.EoSPhase

    member this.ToXElement(context : XFormatter) =
      let element =
        XElement(
          XName.EoSPhase,
          XFormatter.TypeAttribute(this.GetType()),
          XElement(XNamespace.EoS + "blurb", blurb),
          XElement(
            XNamespace.EoS + "let",
            XAttribute(XNamespace.None + "name", "allows-negative-components"),
            string this.AllowsNegativeComponents),
          XElement(
            XNamespace.EoS + "let",
            XAttribute(XNamespace.None + "name", "excludes-endmember-configuration-entropy"),
            string this.ExcludesEndmemberConfigurationEntropy))

      for it in items do
        let phase : IXSerializable = downcast box it.Phase
        element.Add(context.Serialize(phase))
        let size = sizes[it.XOffset]
        if size <> 1.0 then
          let child = XFormatter.Quantity<1>("1", size, "size", true)
          child.Add(context.Serialize(phase))
          element.Add(child)

      for it0 in items do
        for it1 in items[it0.XOffset+it0.XLength .. items.Length-1] do
          let interaction = interactions[interindex it0.XOffset it1.XOffset]
          if interaction <> 0.0<J/mol> then
            let child = XFormatter.Quantity<J/mol>("J/mol", interaction, "interaction", true)
            child.Add(context.Serialize(downcast box it0.Phase))
            child.Add(context.Serialize(downcast box it1.Phase))
            element.Add(child)

      element

  override this.Equals(that) =
    Object.ReferenceEquals(this, that) ||
    match that with
    | :? RegularSolution as that ->
      ((this :> Collections.Generic.ICollection<_>).Count =
        (that :> Collections.Generic.ICollection<_>).Count) &&
      (Seq.zip this that
       |> Seq.forall (fun (a, b) -> a.Phase = b.Phase)) &&
      (items
       |> Array.forall (fun it0 ->
         let phase0 = it0.Phase
         this.Size(phase0) = that.Size(phase0) &&
         (items[it0.XOffset+it0.XLength .. items.Length-1]
          |> Array.forall (fun it1 ->
            let phase1 = it1.Phase
            this.Interaction(phase0, phase1) = that.Interaction(phase0, phase1)))))
    | _ ->
      false

  override this.GetHashCode() =
    items
    |> Array.fold (fun acc it ->
      let phase = it.Phase
      acc +
      (phase.GetHashCode() * hash(this.Size(phase)))) 0

  override this.ToString() =
    blurb
