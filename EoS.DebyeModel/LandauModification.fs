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

namespace EoS.DebyeModel
open System
open System.Xml.Linq
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.Phases
open EoS.Xml

/// Representation of a modified phase using the Landau model.
type LandauModification =
  { /// Human-readable descriptive name of the phase.
    Blurb : string
    /// Underlying phase modified by the landau model.
    Basis : IPhase
    /// Whether the basis model is used transparently where the Landau modification is not applicable.
    TransparentFallback : bool
    /// Reference transition temperature.
    TC0 : float<K>
    /// Maximum excess volume.
    VD : float<m^3/mol>
    /// Maximum excess entropy.
    SD : float<J/mol/K> }

  /// Determine the pressure dependent transition temperature.
  member this.TransitionTemperature(p) =
    this.TC0 + p * this.VD / this.SD
    
  /// Check whether the Landau model is applicable at the given conditions.
  member this.IsApplicable(p, T) =
    let TC = this.TransitionTemperature(p)
    let x = T / TC
    0.0 <= x && x <= 1.0

  /// Compute auxiliary parameters for the Landau model.
  member private this.AuxiliaryParameters(p, T) =
    let TC = this.TransitionTemperature(p)
    let x = T / TC
    if 0.0 <= x && x <= 1.0 then
      let Q4 = 1.0 - x
      let Q2 = sqrt Q4
      let Q6 = Q4 * Q2
      TC, x, Q2, Q4, Q6
    else
      // Landau modification is infeasible in this case
      TC, x, nan, nan, nan

  interface IPhase with
    member this.AllowsNegativeComponents = this.Basis.AllowsNegativeComponents
    member this.XLength = this.Basis.XLength

    member this.Mass(x) = this.Basis.Mass(x)
    member this.Atoms(x) = this.Basis.Atoms(x)
    member this.Formula(x) = this.Basis.Formula(x)

    member this.Volume(p, T, xs) =
      let TC, x, Q2, _, _ = this.AuxiliaryParameters(p, T)

      if not (Double.IsNaN(Q2) && this.TransparentFallback) then
        let V0 = this.Basis.Volume(p, T, xs)
        let VL = this.VD * Q2 * (1.0 + 0.5 * x * (1.0 - this.TC0 / TC))
        V0 - VL
      else
        this.Basis.Volume(p, T, xs)

    member this.Density(p, T, x) =
      this.Basis.Mass(x) / (this :> IPhase).Volume(p, T, x)

    member this.Expansivity(p, T, x) =
      if not this.TransparentFallback || this.IsApplicable(p, T) then
        ThermoElastic.ExpansivityFromVolume(this, p, T, x)
      else
        this.Basis.Expansivity(p, T, x)

    member this.Compressibility(p, T, x) =
      if not this.TransparentFallback || this.IsApplicable(p, T) then
        ThermoElastic.CompressibilityFromVolume(this, p, T, x)
      else
        this.Basis.Compressibility(p, T, x)

    member this.Entropy(p, T, x) =
      let TC, _, Q2, _, _ = this.AuxiliaryParameters(p, T)

      if not (Double.IsNaN(Q2) && this.TransparentFallback) then
        let S0 = this.Basis.Entropy(p, T, x)
        let SL = this.SD * Q2 * (1.0 - 0.5 * (1.0 - this.TC0 / TC));
        S0 - SL
      else
        this.Basis.Entropy(p, T, x)

    member this.IsobaricHeatCapacity(p, T, xs) =
      let TC, x, Q2, _, _ = this.AuxiliaryParameters(p, T)

      if not (Double.IsNaN(Q2) && this.TransparentFallback) then
        let Cp0 = this.Basis.IsobaricHeatCapacity(p, T, xs)
        let CpL = 0.5 * this.SD / Q2 * x * (1.0 - 0.5 * (1.0 - this.TC0 / TC))
        Cp0 - CpL
      else
        this.Basis.IsobaricHeatCapacity(p, T, xs)

    member this.IsochoricHeatCapacity(p, T, x) =
      (this :> IThermoElastic).IsobaricHeatCapacity(p, T, x) -
      ThermoElastic.DeltaHeatCapacity(this, p, T, x)

    member this.Energy(p, T, x) =
      let TC, _, Q2, _, Q6 = this.AuxiliaryParameters(p, T)

      if not (Double.IsNaN(Q2) && this.TransparentFallback) then
        let G0 = this.Basis.Energy(p, T, x)
        let GL = this.SD * ((T - TC) * Q2 + this.TC0 * Q6 / 3.0)
        G0 + GL
      else
        this.Basis.Energy(p, T, x)

    member this.Moduli(p, T, x) =
      if not this.TransparentFallback || this.IsApplicable(p, T) then
        let κ = ThermoElastic.CompressionModulusFromVABC(this, p, T, x)
        let _, μ = this.Basis.Moduli(p, T, x)
        κ, μ
      else
        this.Basis.Moduli(p, T, x)

    member this.Velocities(p, T, x) =
      if not this.TransparentFallback || this.IsApplicable(p, T) then
        ThermoElastic.VelocitiesFromModuli(this, p, T, x)
      else
        this.Basis.Velocities(p, T, x)

    member this.Grueneisen(p, T, x) =
      if not this.TransparentFallback || this.IsApplicable(p, T) then
        ThermoElastic.GrueneisenFromVAKC(this, p, T, x)
      else
        this.Basis.Grueneisen(p, T, x)

  static member FromXElement(context : XFormatter, element : XElement) =
    { Blurb               = element.Element(XNamespace.EoS + "blurb").Value.Trim()
      Basis               = context.Deserialize<IPhase>(element.Element(XName.EoSPhase))
      TransparentFallback = Boolean.Parse(element.Binding("transparent-fallback").Value)
      TC0                 = context.GetQuantity<K>("K", element.Binding("TC0"))
      VD                  = context.GetQuantity<m^3/mol>("m^3/mol", element.Binding("VD"))
      SD                  = context.GetQuantity<J/mol/K>("J/mol/K", element.Binding("SD")) }

  interface IXSerializable with
    member this.XElementName = XName.EoSPhase

    member this.ToXElement(context : XFormatter) =
      XElement(
        XName.EoSPhase,
        XFormatter.TypeAttribute(this.GetType()),
        XElement(XNamespace.EoS + "blurb", this.Blurb),
        context.Serialize(downcast box this.Basis),
        XElement(
          XNamespace.EoS + "let",
          XAttribute(XNamespace.None + "name", "transparent-fallback"),
          string this.TransparentFallback),
        XFormatter.Quantity<K>("K", this.TC0, "TC0"),
        XFormatter.Quantity<m^3/mol>("m^3/mol", this.VD, "VD"),
        XFormatter.Quantity<J/mol/K>("J/mol/K", this.SD, "SD"))

  override this.ToString() =
    this.Blurb
