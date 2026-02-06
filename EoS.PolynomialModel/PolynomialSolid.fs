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

namespace EoS.PolynomialModel
open System.Xml.Linq
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.PhysicalConstants
open EoS.Math
open EoS.PolynomialModel.Math
open EoS.Chemistry
open EoS.Phases
open EoS.Xml

/// Possible representations of the isothermal bulk modulus.
type BulkModulusInfo =
  /// Polynomial describing the inverse bulk modulus dependency on
  /// thermodynamic temperature.
  | CompressibilityPolynomial of Polynomial<K, 1/Pa>
  /// Bulk modulus in the reference state and scaling factor for
  /// temperature difference to the reference state.
  | LinearizedBulkModulus of float<Pa> * float<Pa/K>

/// Representation of a phase using the Birch-Murnaghan+Polynomial model.
type PolynomialSolid =
  { /// Human readable descriptive name of the phase.
    Blurb : string
    /// Chemical formula of the phase.
    Formula : Formula
    /// Reference pressure.
    p0 : float<Pa>
    /// Reference temperature.
    T0 : float<K>
    /// Volume in the reference state.
    V0 : float<m^3/mol>
    /// Isothermal bulk modulus descriptor.
    K0 : BulkModulusInfo
    /// Pressure derivative of the isothermal bulk modulus in the reference state.
    K0_p : float<1>
    /// Pressure, temperature cross derivative of the isothermal bulk
    /// modulus in the reference state.
    K0_pT : float<1/K>
    /// Thermal expansivity dependency on temperature.
    α : Polynomial<K, 1/K>
    /// Heat capacity dependency on temperature.
    Cp : Polynomial<K, J/mol/K>
    /// Entropy of the reference state.
    S0 : float<J/mol/K>
    /// Enthalpy of the reference state.
    H0 : float<J/mol> }

  /// Convert finite strain parameter into volume.
  member this.StrainToVolume(f) =
    this.V0 / (2.0 * f + 1.0) ** 1.5

  /// Convert volume into finite strain parameter.
  member this.VolumeToStrain(V) =
    ((this.V0 / V) ** (2.0/3.0) - 1.0) / 2.0

  /// Compute the isothermal bulk modulus.
  member this.BulkModulus(T : float<K>) =
    match this.K0 with
    | CompressibilityPolynomial β ->
      1.0 / β.Eval(T)
    | LinearizedBulkModulus (K0, dK0dT) ->
      K0 + dK0dT * (T - this.T0)

  /// Compute the pressure derivative of the isothermal bulk modulus.
  member this.BulkModulusPerPressure(T : float<K>) =
    this.K0_p + this.K0_pT * (T - this.T0) * log(T / this.T0)

  /// Compute the temperature integral over thermal expansivity.
  member this.ExpansionCoefficient(T : float<K>) =
    let α_dT = this.α.Integral
    α_dT.Eval(T) - α_dT.Eval(this.T0)

  interface IPhase with
    member this.AllowsNegativeComponents = false
    member this.XLength = 1

    member this.Mass(x) = this.Formula.Mass
    member this.Atoms(x) = this.Formula.Atoms
    member this.Formula(x) = this.Formula

    member this.Volume(p, T, x) =
      let α_dT = this.ExpansionCoefficient(T)
      let K = this.BulkModulus(T)
      let K_p = this.BulkModulusPerPressure(T)
      this.V0 * exp(α_dT) * (1.0 + K_p * p / K)**(-1.0 / K_p)

    member this.Density(p, T, x) =
      this.Formula.Mass / this.Volume(p, T)

    member this.Compressibility(p, T, x) =
      1.0 / (this.BulkModulus(T) + this.BulkModulusPerPressure(T) * p)

    member this.Expansivity(p, T, x) =
      this.α.Eval(T)

    member this.Moduli(p, T, x) =
      ThermoElastic.CompressionModulusFromVABC(this, p, T, x), 0.0<_>

    member this.Velocities(p, T, x) =
      ThermoElastic.VelocitiesFromModuli(this, p, T, x)

    member this.Entropy(p, T, x) =
      let CpT_dT = this.Cp.DecrementedPower.Integral
      this.S0 + CpT_dT.Eval(T)- CpT_dT.Eval(this.T0)

    member this.Energy(p, T, x) =
      let Hel =
        integrate 1.0<J/mol> (fun p -> this.Volume(p, T)) this.p0 p
      let Hth =
        let Cp_dT = this.Cp.Integral
        Cp_dT.Eval(T) - Cp_dT.Eval(this.T0)
      this.H0 + Hel + Hth - T * this.Entropy(p, T)

    member this.IsobaricHeatCapacity(p, T, x) =
      this.Cp.Eval(T)

    member this.IsochoricHeatCapacity(p, T, x) =
      this.Cp.Eval(T) - ThermoElastic.DeltaHeatCapacity(this, p, T, x)

    member this.Grueneisen(p, T, x) =
      ThermoElastic.GrueneisenFromVAKC(this, p, T, x)

  static member FromXElement(context : XFormatter, element : XElement) =
    { Blurb   = element.Element(XNamespace.EoS + "blurb").Value.Trim()
      Formula = element.Element(XNamespace.EoS + "formula").Value |> Formula.ofString
      p0      = context.GetQuantity<Pa>("Pa", element.Binding("p0"))
      T0      = context.GetQuantity<K>("K", element.Binding("T0"))
      V0      = context.GetQuantity<m^3/mol>("m^3/mol", element.Binding("V0"))
      K0 =
        match element.TryGetBinding("β"), element.TryGetBinding("K0"), element.TryGetBinding("K0_T") with
        | Some β, None, None ->
          if β.Attribute(XNamespace.None + "unit").Value <> "K → 1/Pa" then failwith "Unknown compressibility unit"
          CompressibilityPolynomial (Polynomial.ofString β.Value)
        | None, Some K0, Some dK0dT ->
          LinearizedBulkModulus (context.GetQuantity<Pa>("Pa", K0), context.GetQuantity<Pa/K>("Pa/K", dK0dT))
        | _, _, _ ->
          failwith "Invalid combination of compressibility and bulk modulus parameters"
      K0_p    = context.GetQuantity<1>("1", element.Binding("K0_p"))
      K0_pT   = context.GetQuantity<1/K>("1/K", element.Binding("K0_pT"))
      α =
        let α = element.Binding("α")
        if α.Attribute(XNamespace.None + "unit").Value <> "K → 1/K" then failwith "Unknown expansivity unit"
        Polynomial.ofString α.Value
      Cp =
        let Cp = element.Binding("Cp")
        if Cp.Attribute(XNamespace.None + "unit").Value <> "K → J/mol/K" then failwith "Unknown heat capacity unit"
        Polynomial.ofString Cp.Value
      S0      = context.GetQuantity<J/mol/K>("J/mol/K", element.Binding("S0"))
      H0      = context.GetQuantity<J/mol>("J/mol", element.Binding("H0")) }

  interface IXSerializable with
    member this.XElementName = XName.EoSPhase

    member this.ToXElement(context : XFormatter) =
      let element =
        XElement(
          XName.EoSPhase,
          XFormatter.TypeAttribute(this.GetType()),
          XElement(XNamespace.EoS + "blurb", this.Blurb),
          XElement(XNamespace.EoS + "formula", this.Formula.ToString()),
          XFormatter.Quantity<Pa>("Pa", this.p0, "p0"),
          XFormatter.Quantity<K>("K", this.T0, "T0"),
          XFormatter.Quantity<m^3/mol>("m^3/mol", this.V0, "V0"))
      match this.K0 with
      | CompressibilityPolynomial β ->
        element.Add(
          XElement(
            XNamespace.EoS + "let",
            XAttribute(XNamespace.None + "name", "β"),
            XAttribute(XNamespace.None + "unit", "K → 1/Pa"),
            β.ToString()))
      | LinearizedBulkModulus (K0, dK0dT) ->
        element.Add(
          XFormatter.Quantity<Pa>("Pa", K0, "K0"),
          XFormatter.Quantity<Pa/K>("Pa/K", dK0dT, "K0_T"))
      element.Add(
        XFormatter.Quantity<1>("1", this.K0_p, "K0_p"),
        XFormatter.Quantity<1/K>("1/K", this.K0_pT, "K0_pT"),
        XElement(
          XNamespace.EoS + "let",
          XAttribute(XNamespace.None + "name", "α"),
          XAttribute(XNamespace.None + "unit", "K → 1/K"),
          this.α.ToString()),
        XElement(
          XNamespace.EoS + "let",
          XAttribute(XNamespace.None + "name", "Cp"),
          XAttribute(XNamespace.None + "unit", "K → J/mol/K"),
          this.Cp.ToString()),
        XFormatter.Quantity<J/mol/K>("J/mol/K", this.S0, "S0"),
        XFormatter.Quantity<J/mol>("J/mol", this.H0, "H0"))
      element

  override this.ToString() =
    this.Blurb
