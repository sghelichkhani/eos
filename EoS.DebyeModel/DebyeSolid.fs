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
open System.Xml.Linq
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.PhysicalConstants
open EoS.Math
open EoS.Chemistry
open EoS.Phases
open EoS.Xml

/// Representation of a phase using the Birch-Murnaghan-Mie-Debye-Grüneisen model.
type DebyeSolid =
  { /// Human readable descriptive name of the phase.
    Blurb : string
    /// Chemical formula of the phase.
    Formula : Formula
    /// Reference temperature.
    T0 : float<K>
    /// Volume in the reference state.
    V0 : float<m^3/mol>
    /// Isothermal bulk modulus in the reference state.
    K0 : float<Pa>
    /// Pressure derivative of the isothermal bulk modulus in the reference state.
    K0_p : float<1>
    /// Shear modulus in the reference state.
    G0 : float<Pa>
    /// Pressure derivative of the shear modulus in the reference state.
    G0_p : float<1>
    /// Debye temperature at reference volume.
    θ0 : float<K>
    /// Grüneisen parameter at reference volume.
    γ0 : float<1>
    /// Logarithmic volume derivative of the Grüneisen parameter at reference volume.
    q0 : float<1>
    /// Shear strain derivative of the Grüneisen parameter at reference volume.
    η0 : float<1>
    /// Helmholtz energy in the reference state.
    F0 : float<J/mol> }

  /// Convert finite strain parameter into volume.
  member this.StrainToVolume(f) =
    this.V0 / (2.0 * f + 1.0) ** 1.5

  /// Convert volume into finite strain parameter.
  member this.VolumeToStrain(V) =
    ((this.V0 / V) ** (2.0/3.0) - 1.0) / 2.0

  /// Compute auxiliary parameters for the Grüneisen model. The method
  /// returns the Debye temperature, Grüneisen parameter, shear strain
  /// derivative of the Grüneisen parameter and logarithmic volume
  /// derivative of the Grüneisen parameter.
  member private this.AuxiliaryParameters(f) =
    let aii = 6.0 * this.γ0
    let aiikk = aii * (aii - 2.0 - 3.0 * this.q0)

    let apmf, apmf2, f2p1 =
      aii + aiikk * f,
      1.0 + (aii + aiikk * f / 2.0) * f,
      2.0 * f + 1.0

    let θ, γ =
      this.θ0 * sqrt apmf2,
      apmf * f2p1 / apmf2 / 6.0
    let η, q =
      γ * (6.0 * (this.η0 + this.γ0) * f2p1 / apmf - 1.0),
      2.0 * (γ - 1.0/3.0) - aiikk * f2p1 / (3.0 * apmf)

    θ, γ, η, q

  /// Compute the strain-dependent pressure.
  member this.Pressure(f, T) =
    let n = this.Formula.Atoms
    let V = this.StrainToVolume(f)
    let θ, γ, _, _ = this.AuxiliaryParameters(f)

    let x, x0 = θ / T, θ / this.T0
    let pel, Eth =
      3.0 * this.K0 * (2.0 * f + 1.0) ** 2.5 * (1.0 + 1.5 * (this.K0_p - 4.0) * f) * f,
      3.0 * n * MolarGas * (T * debye3 x - this.T0 * debye3 x0)
    
    pel + γ * Eth / V

  /// Compute the strain-dependent Helmholtz energy.
  member this.FreeEnergy(f, T) =
    let n = this.Formula.Atoms
    let θ, γ, _, _ = this.AuxiliaryParameters(f)

    let x, x0 = θ / T, θ / this.T0
    let Fel, Fth =
      -4.5 * this.K0 * this.V0 * (1.0 + (this.K0_p - 4.0) * f) * f * f,
      n * MolarGas * (
        this.T0 * (9.0/8.0 * x0 + 3.0 * log(1.0 - exp -x0) - debye3 x0) -
        T * (9.0/8.0 * x + 3.0 * log(1.0 - exp -x) - debye3 x))

    this.F0 - Fel - Fth

  /// Compute the pressure-dependent strain.
  member this.Strain(p, T) =
    let dp f = this.Pressure(f, T) - p
    // NOTE: We assume that the volume never expands by more than 17%
    // and never shrinks by more than 90% of its value in the
    // reference state. This allows us to fix the interval searched by
    // the root finding algorithm.
    let a, b =
      let next c f = if abs f > 0.001 then Some ((c, f), f / 2.0) else None
      Seq.unfold (next ()) -0.05
      |> Seq.collect (fun ((), a) -> Seq.unfold (next a) +2.0)
      |> Seq.find (fun (a, b) -> dp a * dp b < 0.0<Pa^2>)
    bracket 1.0e-12 dp a b

  interface IPhase with
    member this.AllowsNegativeComponents = false
    member this.XLength = 1

    member this.Mass(x) = this.Formula.Mass
    member this.Atoms(x) = this.Formula.Atoms
    member this.Formula(x) = this.Formula

    member this.Volume(p, T, x) =
      this.StrainToVolume(this.Strain(p, T))

    member this.Density(p, T, x) =
      this.Formula.Mass / this.Volume(p, T)

    member this.Compressibility(p, T, x) =
      let n, f = this.Formula.Atoms, this.Strain(p, T)
      let V = this.StrainToVolume(f)
      let θ, γ, _, q = this.AuxiliaryParameters(f)

      let x, x0 = θ / T, θ / this.T0

      let Eth =
        3.0 * n * MolarGas *
        (T * debye3 x - this.T0 * debye3 x0)
      let dCvT =
        3.0 * n * MolarGas * (
          T * (4.0 * debye3 x - (3.0 * x / (exp x - 1.0))) -
          this.T0 * (4.0 * debye3 x0 - (3.0 * x0 / (exp x0 - 1.0))))

      let K =
        (2.0 * f + 1.0) ** 2.5 *
        this.K0 * (1.0 + ((3.0 * this.K0_p - 5.0) + 13.5 * (this.K0_p - 4.0) * f) * f) +
        γ * ((γ + 1.0 - q) * Eth - γ * dCvT) / V

      1.0 / K

    member this.Expansivity(p, T, x) =
      let n, f = this.Formula.Atoms, this.Strain(p, T)
      let V = this.StrainToVolume(f)
      let θ, γ, _, q = this.AuxiliaryParameters(f)

      let x, x0 = θ / T, θ / this.T0

      let Eth =
        3.0 * n * MolarGas *
        (T * debye3 x - this.T0 * debye3 x0)
      let Cv =
        3.0 * n * MolarGas *
        (4.0 * debye3 x - (3.0 * x / (exp x - 1.0)))
      let dCvT =
        Cv * T -
        3.0 * n * MolarGas * this.T0 *
        (4.0 * debye3 x0 - (3.0 * x0 / (exp x0 - 1.0)))

      let K =
        (2.0 * f + 1.0) ** 2.5 *
        this.K0 * (1.0 + ((3.0 * this.K0_p - 5.0) + 13.5 * (this.K0_p - 4.0) * f) * f) +
        γ * ((γ + 1.0 - q) * Eth - γ * dCvT) / V

      γ * Cv / K / V

    member this.Moduli(p, T, x) =
      let n, f = this.Formula.Atoms, this.Strain(p, T)
      let V = this.StrainToVolume(f)
      let θ, γ, η, q = this.AuxiliaryParameters(f)

      let x, x0 = θ / T, θ / this.T0

      let Eth =
        3.0 * n * MolarGas *
        (T * debye3 x - this.T0 * debye3 x0)
      let CvT =
        3.0 * n * MolarGas * T *
        (4.0 * debye3 x - (3.0 * x / (exp x - 1.0)))
      let dCvT =
        CvT -
        3.0 * n * MolarGas * this.T0 *
        (4.0 * debye3 x0 - (3.0 * x0 / (exp x0 - 1.0)))

      let K, G =
        (2.0 * f + 1.0) ** 2.5 *
        this.K0 * (1.0 + ((3.0 * this.K0_p - 5.0) + 13.5 * (this.K0_p - 4.0) * f) * f) +
        γ * ((γ + 1.0 - q) * Eth - γ * dCvT) / V,
        (2.0 * f + 1.0) ** 2.5 *
        (this.G0 + ((3.0 * this.K0 * this.G0_p - 5.0 * this.G0) +
          (6.0 * this.K0 * this.G0_p - 24.0 * this.K0 - 14.0 * this.G0 + 4.5 * this.K0 * this.K0_p) * f) * f) -
        η * Eth / V;

      K + γ * γ * CvT / V, G

    member this.Velocities(p, T, x) =
      ThermoElastic.VelocitiesFromModuli(this, p, T, x)

    member this.Energy(p, T, x) =
      let f = this.Strain(p, T)
      let V = this.StrainToVolume(f)
      this.FreeEnergy(f, T) + p * V

    member this.Entropy(p, T, x) =
      ThermoElastic.EntropyFromEnergy(this, p, T, x)

    member this.IsobaricHeatCapacity(p, T, x) =
      let n, f = this.Formula.Atoms, this.Strain(p, T)
      let V = this.StrainToVolume(f)
      let θ, γ, _, q = this.AuxiliaryParameters(f)

      let x, x0 = θ / T, θ / this.T0

      let Eth =
        3.0 * n * MolarGas *
        (T * debye3 x - this.T0 * debye3 x0)
      let Cv =
        3.0 * n * MolarGas *
        (4.0 * debye3 x - (3.0 * x / (exp x - 1.0)))
      let dCvT =
        Cv * T -
        3.0 * n * MolarGas * this.T0 *
        (4.0 * debye3 x0 - (3.0 * x0 / (exp x0 - 1.0)))

      let K =
        (2.0 * f + 1.0) ** 2.5 *
        this.K0 * (1.0 + ((3.0 * this.K0_p - 5.0) + 13.5 * (this.K0_p - 4.0) * f) * f) +
        γ * ((γ + 1.0 - q) * Eth - γ * dCvT) / V
      let α =
        γ * Cv / K / V

      Cv + (V * T * α * α * K)

    member this.IsochoricHeatCapacity(p, T, x) =
      let n, f = this.Formula.Atoms, this.Strain(p, T)
      let θ, _, _, _ = this.AuxiliaryParameters(f)

      let x = θ / T

      3.0 * n * MolarGas *
      (4.0 * debye3 x - (3.0 * x / (exp x - 1.0)))

    member this.Grueneisen(p, T, x) =
      let f = this.Strain(p, T)
      let _, γ, _, _ = this.AuxiliaryParameters(f)
      γ

  static member FromXElement(context : XFormatter, element : XElement) =
    { Blurb   = element.Element(XNamespace.EoS + "blurb").Value.Trim()
      Formula = element.Element(XNamespace.EoS + "formula").Value |> Formula.ofString
      T0      = context.GetQuantity<K>("K", element.Binding("T0"))
      V0      = context.GetQuantity<m^3/mol>("m^3/mol", element.Binding("V0"))
      K0      = context.GetQuantity<Pa>("Pa", element.Binding("K0"))
      K0_p    = context.GetQuantity<1>("1", element.Binding("K0_p"))
      G0      = context.GetQuantity<Pa>("Pa", element.Binding("G0"))
      G0_p    = context.GetQuantity<1>("1", element.Binding("G0_p"))
      θ0      = context.GetQuantity<K>("K", element.Binding("θ0"))
      γ0      = context.GetQuantity<1>("1", element.Binding("γ0"))
      q0      = context.GetQuantity<1>("1", element.Binding("q0"))
      η0      = context.GetQuantity<1>("1", element.Binding("η0"))
      F0      = context.GetQuantity<J/mol>("J/mol", element.Binding("F0")) }

  interface IXSerializable with
    member this.XElementName = XName.EoSPhase

    member this.ToXElement(context : XFormatter) =
      XElement(
        XName.EoSPhase,
        XFormatter.TypeAttribute(this.GetType()),
        XElement(XNamespace.EoS + "blurb", this.Blurb),
        XElement(XNamespace.EoS + "formula", this.Formula.ToString()),
        XFormatter.Quantity<K>("K", this.T0, "T0"),
        XFormatter.Quantity<m^3/mol>("m^3/mol", this.V0, "V0"),
        XFormatter.Quantity<Pa>("Pa", this.K0, "K0"),
        XFormatter.Quantity<1>("1", this.K0_p, "K0_p"),
        XFormatter.Quantity<Pa>("Pa", this.G0, "G0"),
        XFormatter.Quantity<1>("1", this.G0_p, "G0_p"),
        XFormatter.Quantity<K>("K", this.θ0, "θ0"),
        XFormatter.Quantity<1>("1", this.γ0, "γ0"),
        XFormatter.Quantity<1>("1", this.q0, "q0"),
        XFormatter.Quantity<1>("1", this.η0, "η0"),
        XFormatter.Quantity<J/mol>("J/mol", this.F0, "F0"))

  override this.ToString() =
    this.Blurb
