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

/// Representation of a phase using the Birch-Murnaghan+Polynomial model.
type HollandPowellSolid =
  { /// Human readable descriptive name of the phase.
    Blurb : string
    /// Chemical formula of the phase.
    Formula : Formula
    /// Reference pressure.
    p0 : float<Pa>
    /// Reference temperature.
    T0 : float<K>
    /// Enthalpy of the reference state.
    H0 : float<J/mol>
    /// Entropy of the reference state.
    S0 : float<J/mol/K>
    /// Volume in the reference state.
    V0 : float<m^3/mol>
    /// Heat capacity dependency on temperature.
    Cp : Polynomial<K, J/mol/K>
    /// Thermal expansivity in the reference state.
    α0 : float<1/K>
    /// Isothermal bulk modulus in the reference state.
    K0 : float<Pa>
    /// Pressure derivative of the isothermal bulk modulus in the reference state.
    K0_p : float<1>
    /// Second pressure derivative of the isothermal bulk modulus in the reference state.
    K0_pp : float<1/Pa> }

  /// Convert finite strain parameter into volume.
  member this.StrainToVolume(f) =
    this.V0 / (2.0 * f + 1.0) ** 1.5

  /// Convert volume into finite strain parameter.
  member this.VolumeToStrain(V) =
    ((this.V0 / V) ** (2.0/3.0) - 1.0) / 2.0

  /// Compute auxiliary parameters for the equation of state.
  member private this.AuxiliaryParameters(T) =
    let θ =
      10636.0<K> / (this.S0/1.0<J/mol/K> / this.Formula.Atoms + 6.44)
    let u, u0 =
      θ / T,
      θ / this.T0
    let ξ, ξ0 =
      (u*u * exp u) / (exp u - 1.0)**2.0,
      (u0*u0 * exp u0) / (exp u0 - 1.0)**2.0
    let pth =
      this.α0 * this.K0 * θ / ξ0 *
      (1.0 / (exp u - 1.0) - 1.0 / (exp u0 - 1.0))
    let a, b, c =
      (1.0 + this.K0_p) / (1.0 + this.K0_p + this.K0 * this.K0_pp),
      (this.K0_p / this.K0) - (this.K0_pp / (1.0 + this.K0_p)),
      (1.0 + this.K0_p + this.K0 * this.K0_pp) / (this.K0_p*this.K0_p + this.K0_p - this.K0 * this.K0_pp)
    θ, u, u0, ξ, ξ0, pth, a, b, c

  /// Compute the strain-dependent pressure.
  member this.Pressure(f, T) =
    let _, _, _, _, _, pth, a, b, c = this.AuxiliaryParameters(T)
    ((1.0 - (1.0 - (2.0 * f + 1.0)**(-1.5)) / a)**c - 1.0) / b + pth

  interface IPhase with
    member this.AllowsNegativeComponents = false
    member this.XLength = 1

    member this.Mass(x) = this.Formula.Mass
    member this.Atoms(x) = this.Formula.Atoms
    member this.Formula(x) = this.Formula

    member this.Volume(p, T, x) =
      let _, _, _, _, _, pth, a, b, c = this.AuxiliaryParameters(T)
      this.V0 * (1.0 - a * (1.0 - (1.0 + b * (p - pth))**(-c)))

    member this.Density(p, T, x) =
      this.Formula.Mass / this.Volume(p, T)

    member this.Compressibility(p, T, x) =
      let _, _, _, _, _, pth, a, b, c = this.AuxiliaryParameters(T)
      1.0 / (this.K0 * (1.0 + b * (p - pth)) * (a + (1.0 - a) * (1.0 + b * (p - pth))**c))

    member this.Expansivity(p, T, x) =
      let θ, u, u0, _, ξ0, pth, a, b, c = this.AuxiliaryParameters(T)
      let u_T = θ / (T*T)
      let pth_T = this.α0 * this.K0 * θ * (exp u) * u_T / (ξ0 * (exp u - 1.0)**2.0)
      (a * b * c * (1.0 + b * (p - pth))**(-c-1.0) * pth_T) /
      (1.0 - a * (1.0 - (1.0 + b * (p - pth))**(-c)))

    member this.Moduli(p, T, x) =
      ThermoElastic.CompressionModulusFromVABC(this, p, T, x), 0.0<_>

    member this.Velocities(p, T, x) =
      ThermoElastic.VelocitiesFromModuli(this, p, T, x)

    member this.Entropy(p, T, x) =
      let thel = this :> IThermoElastic
      let Sth =
        this.Cp.DecrementedPower.Integral.Eval(this.T0, T)
      let Sel =
        integrate 1.0e-3<J/mol/K> (fun p ->
          thel.Volume(p, T, x) * thel.Expansivity(p, T, x)) this.p0 p
      this.S0 + Sth - Sel

    member this.Energy(p, T, x) =
      let _, _, _, _, _, pth, a, b, c = this.AuxiliaryParameters(T)
      let Gel =
        p * this.V0 *
        (1.0 - a + a * ((1.0 - b * pth)**(1.0-c) - (1.0 + b * (p - pth))**(1.0-c)) /
         (b * (c - 1.0) * p))
      let Hth =
        this.Cp.Integral.Eval(this.T0, T)
      let Sp0 =
        this.S0 + this.Cp.DecrementedPower.Integral.Eval(this.T0, T)
      this.H0 + Hth - T * Sp0 + Gel

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
      H0      = context.GetQuantity<J/mol>("J/mol", element.Binding("H0"))
      S0      = context.GetQuantity<J/mol/K>("J/mol/K", element.Binding("S0"))
      V0      = context.GetQuantity<m^3/mol>("m^3/mol", element.Binding("V0"))
      Cp =
        let Cp = element.Binding("Cp")
        if Cp.Attribute(XNamespace.None + "unit").Value <> "K → J/mol/K" then failwith "Unknown heat capacity unit"
        Polynomial.ofString Cp.Value
      α0      = context.GetQuantity<1/K>("1/K", element.Binding("α0"))
      K0      = context.GetQuantity<Pa>("Pa", element.Binding("K0"))
      K0_p    = context.GetQuantity<1>("1", element.Binding("K0_p"))
      K0_pp   = context.GetQuantity<1/Pa>("1/Pa", element.Binding("K0_pp")) }

  interface IXSerializable with
    member this.XElementName = XName.EoSPhase

    member this.ToXElement(context : XFormatter) =
      XElement(
        XName.EoSPhase,
        XFormatter.TypeAttribute(this.GetType()),
        XElement(XNamespace.EoS + "blurb", this.Blurb),
        XElement(XNamespace.EoS + "formula", this.Formula.ToString()),
        XFormatter.Quantity<Pa>("Pa", this.p0, "p0"),
        XFormatter.Quantity<K>("K", this.T0, "T0"),
        XFormatter.Quantity<J/mol>("J/mol", this.H0, "H0"),
        XFormatter.Quantity<J/mol/K>("J/mol/K", this.S0, "S0"),
        XFormatter.Quantity<m^3/mol>("m^3/mol", this.V0, "V0"),
        XElement(
          XNamespace.EoS + "let",
          XAttribute(XNamespace.None + "name", "Cp"),
          XAttribute(XNamespace.None + "unit", "K → J/mol/K"),
          this.Cp.ToString()),
        XFormatter.Quantity<1/K>("1/K", this.α0, "α0"),
        XFormatter.Quantity<Pa>("Pa", this.K0, "K0"),
        XFormatter.Quantity<1>("1", this.K0_p, "K0_p"),
        XFormatter.Quantity<1/Pa>("1/Pa", this.K0_pp, "K0_pp"))

  override this.ToString() =
    this.Blurb
