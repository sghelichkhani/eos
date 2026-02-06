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

namespace EoS.Test
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open NUnit.Framework
open EoS.PhysicalConstants
open EoS.Chemistry
open EoS.Phases

module PhaseAssert =
  let plo, phi = 1.0e5<Pa>, 2.0e9<Pa>
  let Tlo, Thi = 298.15<K>, 1000.0<K>

  let PlausibleVolume (phase : IPhase) x =
    let v0, v1 = phase.Volume(plo, Tlo, x), phase.Volume(phi, Tlo, x)
    Assert.Greater(v0, v1, "Compression should decrease volume")
    let v0, v1 = phase.Volume(plo, Tlo, x), phase.Volume(plo, Thi, x)
    Assert.Less(v0, v1, "Heating should increase volume")
    let v0, v1 = phase.Volume(phi, Thi, x), phase.Volume(phi, Tlo, x)
    Assert.Greater(v0, v1, "Cooling should decrease volume")
    let v0, v1 = phase.Volume(phi, Thi, x), phase.Volume(plo, Thi, x)
    Assert.Less(v0, v1, "Decompression should increase volume")

  let PlausibleEntropy (phase : IPhase) x =
    let S0, S1 = phase.Entropy(plo, Tlo, x), phase.Entropy(plo, Thi, x)
    Assert.Less(S0, S1, "Heating should increase entropy")
    let S0, S1 = phase.Entropy(phi, Thi, x), phase.Entropy(phi, Tlo, x)
    Assert.Greater(S0, S1, "Cooling should decrease entropy")

  let ConsistentVolume (phase : IPhase) x =
    for p in [plo; phi] do
      for T in [Tlo; Thi] do
        Assert.AreEqual(
          ThermoElastic.VolumeFromEnergy(phase, p, T, x)/1.0<m^3/mol>,
          phase.Volume(p, T, x)/1.0<m^3/mol>, 1.0e-10,
          "Volume mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

  let ConsistentExpansivity (phase : IPhase) x =
    for p in [plo; phi] do
      for T in [Tlo; Thi] do
        Assert.AreEqual(
          ThermoElastic.ExpansivityFromVolume(phase, p, T, x)*1.0<K>,
          phase.Expansivity(p, T, x)*1.0<K>, 1.0e-10,
          "Expansivity mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

  let ConsistentCompressibility (phase : IPhase) x =
    for p in [plo; phi] do
      for T in [Tlo; Thi] do
        Assert.AreEqual(
          ThermoElastic.CompressibilityFromVolume(phase, p, T, x)*1.0<Pa>,
          phase.Compressibility(p, T, x)*1.0<Pa>, 1.0e-10,
          "Compressibility mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

  let ConsistentEntropy (phase : IPhase) x =
    for p in [plo; phi] do
      for T in [Tlo; Thi] do
        Assert.AreEqual(
          ThermoElastic.EntropyFromEnergy(phase, p, T, x)/1.0<J/mol/K>,
          phase.Entropy(p, T, x)/1.0<J/mol/K>, 1.0e-2,
          "Entropy mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

  let DisorderEntropy (phase : RegularSolution) x =
    for p in [plo; phi] do
      for T in [Tlo; Thi] do
        Assert.Greater(
          (phase :> IPhase).Entropy(p, T, x),
          phase.XFold (fun S it f ->
            S + f * it.Entropy(p, T)) 0.0<J/mol/K> x,
          "Disorder should increase entropy at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

  let ConsistentGrueneisen (phase : IPhase) x =
    for p in [plo; phi] do
      for T in [Tlo; Thi] do
        Assert.AreEqual(
          ThermoElastic.GrueneisenFromVAKC(phase, p, T, x),
          phase.Grueneisen(p, T, x), 1.0e-8,
          "Grüneisen parameter mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

type IdealGas(formula : Formula) =
  interface IPhase with
    member this.AllowsNegativeComponents = false
    member this.XLength = 1

    member this.Mass(x) = formula.Mass
    member this.Atoms(x) = formula.Atoms
    member this.Formula(x) = formula

    member this.Volume(p, T, x) = formula.Atoms * MolarGas * T / p
    member this.Density(p, T, x) = formula.Mass / this.Volume(p, T)
    member this.Compressibility(p, T, x) = 1.0 / p
    member this.Expansivity(p, T, x) = 1.0 / T
    member this.Moduli(p, T, x) = ThermoElastic.CompressionModulusFromVABC(this, p, T, x), 0.0<_>
    member this.Velocities(p, T, x) = ThermoElastic.VelocitiesFromModuli(this, p, T, x)

    member this.Energy(p, T, x) =
      let λ = Quantum / sqrt(2.0<mol> * Math.PI * formula.Mass * AtomicGas * T)
      let V = this.Volume(p, T)
      - formula.Atoms * MolarGas * T * log(V * Math.E / λ/λ/λ / MolarCount) + p * V

    member this.Entropy(p, T, x) =
      let λ = Quantum / sqrt(2.0<mol> * Math.PI * formula.Mass * AtomicGas * T)
      let V = this.Volume(p, T)
      - formula.Atoms * MolarGas * (2.5 + log(V / λ/λ/λ / MolarCount))

    member this.IsobaricHeatCapacity(p, T, x) =
      ThermoElastic.HeatCapacityFromEntropy(this, p, T, x)

    member this.IsochoricHeatCapacity(p, T, x) =
      ThermoElastic.HeatCapacityFromEntropy(this, p, T, x) -
      ThermoElastic.DeltaHeatCapacity(this, p, T, x)

    member this.Grueneisen(p, T, x) = ThermoElastic.GrueneisenFromVAKC(this, p, T, x)

[<TestFixture>]
type PhasesTest() =
  let he = IdealGas(Formula.ofString"(He)")
  let ne = IdealGas(Formula.ofString"(Ne)")

  let mix =
    RegularSolution(
      [he; ne],
      interactions = [upcast he, upcast ne, 1.0e3<J/mol>])

  [<Test(Description = "Basic volume checks")>]
  member this.SolutionVolume() =
    let p, T, x = 2.0e5<Pa>, 500.0<K>, [|0.5; 0.5|]
    let V0, V1 = he.Volume(p, T), ne.Volume(p, T)
    Assert.AreEqual(V0, V1, "Ideal molar gas volume mismatch")
    let Vmix = (mix :> IPhase).Volume(p, T, x)
    Assert.AreEqual(V0, Vmix, "Mixture volume mismatch")

  [<Test(Description = "Basic entropy checks")>]
  member this.SolutionEntropy() =
    Assert.AreEqual(1, mix.Sites, "One solution site expected")

    let p, T, x = 2.0e5<Pa>, 1000.0<K>, [|0.6; 0.4|]
    let S0, S1 = he.Entropy(p, T), ne.Entropy(p, T)
    Assert.AreNotEqual(S0, S1, "Entropy should be mass dependent")

    PhaseAssert.DisorderEntropy mix x

  [<Test(Description = "Symmetry check for configuration entropy")>]
  member this.SymmetricConfigurationEntropy() =
    Assert.AreEqual(
      mix.ConfigurationEntropy[|0.8; 0.2|] / 1.0<J/mol/K>,
      mix.ConfigurationEntropy[|0.2; 0.8|] / 1.0<J/mol/K>,
      1.0e-6,
      "Simple configuration entropy should be symmetric")

  [<Test(Description = "Symmetry check for interaction energy")>]
  member this.SymmetricInteractionEnergy() =
    Assert.AreEqual(
      mix.InteractionEnergy[|0.8; 0.2|] / 1.0<J/mol>,
      mix.InteractionEnergy[|0.2; 0.8|] / 1.0<J/mol>,
      1.0e-6,
      "Interaction energy should be symmetric")
