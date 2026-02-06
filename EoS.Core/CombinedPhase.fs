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

/// A phase whose Gibbs energy is a linear combination of other phases.
type CombinedPhase(contributions : seq<IPhase * float>, ?energyOffset : float<J/mol>, ?blurb : string) =
  let contributions =
    contributions
    |> Seq.map (fun ((phase, _) as it) ->
      if phase.XLength = 1 then
        it
      else
        invalidArg (nameof contributions) "Contributions with variable composition not supported")
    |> Seq.toArray

  let blurb =
    match blurb with
    | Some v ->
      v
    | None ->
      let buf = Text.StringBuilder("CombinedPhase[")
      contributions
      |> Seq.iteri (fun i (phase, n) ->
        if i > 0 then buf.Append("; ") |> ignore
        buf.Append(n).Append('*').Append(string phase) |> ignore)
      buf.Append(']').ToString()

  /// The number of contributions forming this phase.
  member this.ContributionCount =
    contributions.Length

  /// The delegate phases contributing to energy.
  member this.Contributions =
    contributions :> seq<IPhase * float>

  /// The constant term in the linear energy combination.
  member val EnergyOffset =
    defaultArg energyOffset 0.0<J/mol>

  interface IPhase with
    member this.AllowsNegativeComponents = false

    member this.XLength = 1

    member this.Mass(_) =
      contributions
      |> Array.fold (fun m (it, n) ->
        m + n * it.Mass()) 0.0<_>

    member this.Atoms(_) =
      contributions
      |> Array.fold (fun c (it, n) ->
        c + n * it.Atoms()) 0.0

    member this.Formula(_) =
      contributions
      |> Array.map (fun (it, n) ->
        it.Formula(), n)
      |> Formula.combineGrouped

    member this.Volume(p, T, x) =
      ThermoElastic.VolumeFromEnergy(this, p, T, x)

    member this.Density(p, T, x) =
      let phase = this :> IPhase
      phase.Mass(x) / phase.Volume(p, T, x)

    member this.Compressibility(p, T, x) =
      ThermoElastic.CompressibilityFromVolume(this, p, T, x)

    member this.Expansivity(p, T, x) =
      ThermoElastic.ExpansivityFromVolume(this, p, T, x)

    member this.Moduli(p, T, x) =
      ThermoElastic.CompressionModulusFromVABC(this, p, T, x),
      0.0<_>

    member this.Velocities(p, T, x) =
      ThermoElastic.VelocitiesFromModuli(this, p, T, x)

    member this.Energy(p, T, _) =
      contributions
      |> Array.fold (fun G (it, n) ->
        G + n * it.Energy(p, T)) this.EnergyOffset

    member this.Entropy(p, T, _) =
      contributions
      |> Array.fold (fun S (it, n) ->
        S + n * it.Entropy(p, T)) 0.0<_>

    member this.IsobaricHeatCapacity(p, T, x) =
      ThermoElastic.HeatCapacityFromEntropy(this, p, T, x)

    member this.IsochoricHeatCapacity(p, T, x) =
      ThermoElastic.HeatCapacityFromEntropy(this, p, T, x) +
      ThermoElastic.DeltaHeatCapacity(this, p, T, x)

    member this.Grueneisen(p, T, x) =
      ThermoElastic.GrueneisenFromVAKC(this, p, T, x)

  static member FromXElement(context : XFormatter, element : XElement) =
    let blurb = element.Element(XNamespace.EoS + "blurb").Value
    let energyOffset = context.GetQuantity<J/mol>("J/mol", element.Binding("energy-offset"))

    let contributions =
      seq {
        for it in element.Elements(XNamespace.EoS + "contribution") ->
          context.Deserialize<IPhase>(it.Element(XName.EoSPhase)),
          context.GetQuantity<1>("1", it)
      }

    CombinedPhase(contributions, energyOffset, blurb)

  interface IXSerializable with
    member this.XElementName = XName.EoSPhase

    member this.ToXElement(context : XFormatter) =
      let element =
        XElement(
          XName.EoSPhase,
          XFormatter.TypeAttribute(this.GetType()),
          XElement(XNamespace.EoS + "blurb", blurb),
          XFormatter.Quantity<J/mol>("J/mol", this.EnergyOffset, "energy-offset"))

      for it, n in contributions do
        let phase : IXSerializable = downcast box it
        let child = XFormatter.Quantity<1>("1", n, "contribution", true)
        child.Add(context.Serialize(phase))
        element.Add(child)

      element

  override this.Equals(that) =
    Object.ReferenceEquals(this, that) ||
    match that with
    | :? CombinedPhase as that ->
      (this.ContributionCount = that.ContributionCount) &&
      (Seq.forall2 ( = ) this.Contributions that.Contributions)
    | _ ->
      false

  override this.GetHashCode() =
    contributions
    |> Array.fold (fun acc (it, n) ->
      acc + int n * it.GetHashCode()) 0

  override this.ToString() =
    blurb
