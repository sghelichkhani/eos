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

/// Default implementation helpers for IThermoElastic methods.
module EoS.Phases.ThermoElastic
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.Math

/// Determine the volume by numerical differentiation of Gibbs' energy.
let VolumeFromEnergy(thel : IThermoElastic, p, T, x) : float<m^3/mol> =
  derive 1.0e-8<m^3/mol> 1.0e5<Pa> (fun p -> thel.Energy(p, T, x)) p 0

/// Determine the compressibility by numerical differentiation of volume.
let CompressibilityFromVolume(thel : IThermoElastic, p, T, x) : float<1/Pa> =
  let V, dV_dp =
    thel.Volume(p, T, x),
    derive 1.0e-3<m^3/mol/Pa> 1.0e5<Pa> (fun p -> thel.Volume(p, T, x)) p 0
  -dV_dp / V

/// Determine the thermal expansivity by numerical differentiation of volume.
let ExpansivityFromVolume(thel : IThermoElastic, p, T, x) : float<1/K> =
  let V, dV_dT =
    thel.Volume(p, T, x),
    derive 1.0e-8<m^3/mol/K> 1.0<K> (fun T -> thel.Volume(p, T, x)) T +1
  dV_dT / V

/// Determine the entropy by numerical differentiation of Gibbs' energy.
let EntropyFromEnergy(thel : IThermoElastic, p, T, x) : float<J/mol/K> =
  - derive 1.0e-8<J/mol/K> 1.0<K> (fun T -> thel.Energy(p, T, x)) T +1

/// Determine the isobaric heat capacity by numerical differentiation of entropy.
let HeatCapacityFromEntropy(thel : IThermoElastic, p, T, x) : float<J/mol/K> =
  let dS_dT =
    derive 1.0e-8<J/mol/K^2> 1.0<K> (fun T -> thel.Entropy(p, T, x)) T +1
  T * dS_dT

/// Compute the difference between isobaric and isochoric heat
/// capacity.
let DeltaHeatCapacity(thel : IThermoElastic, p, T, x) : float<J/mol/K> =
  let V = thel.Volume(p, T, x)
  let α = thel.Expansivity(p, T, x)
  let β = thel.Compressibility(p, T, x)
  V * T * α*α / β

/// Compute the thermodynamic Grüneisen parameter from volume,
/// isothermal expansivity, adiabatic bulk modulus and heat capacity
/// at constant pressure.
let GrueneisenFromVAKC(thel : IThermoElastic, p, T, x) : float =
  let α = thel.Expansivity(p, T, x)
  let κ, _ = thel.Moduli(p, T, x)
  let V = thel.Volume(p, T, x)
  let Cp = thel.IsobaricHeatCapacity(p, T, x)
  α * κ * V / Cp

/// Determine the compression modulus from volume, expansivity, compressibility and heat capacity.
let CompressionModulusFromVABC(thel : IThermoElastic, p, T, x) : float<Pa> =
  let V, α, β, Cp =
    thel.Volume(p, T, x),
    thel.Expansivity(p, T, x), thel.Compressibility(p, T, x),
    thel.IsobaricHeatCapacity(p, T, x)
  1.0 / (α*α * V * T / Cp + β)

/// Compute velocities from elastic moduli.
let ModuliFromVelocities(thel : IThermoElastic, p, T, x) : ĸ:float<Pa> * µ:float<Pa> =
  let ρ = thel.Density(p, T, x)
  let vp, vs = thel.Velocities(p, T, x)
  let µ = vs*vs * ρ
  let ĸ = vp*vp * ρ - 4.0/3.0 * µ
  ĸ, µ

/// Compute elastic moduli from velocities.
let VelocitiesFromModuli(thel : IThermoElastic, p, T, x) : vp:float<m/s> * vs:float<m/s> =
  let ρ = thel.Density(p, T, x)
  let ĸ, µ = thel.Moduli(p, T, x)
  sqrt ((ĸ + 4.0/3.0 * µ) / ρ), sqrt (µ / ρ)
