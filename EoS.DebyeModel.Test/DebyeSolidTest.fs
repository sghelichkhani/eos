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

namespace EoS.DebyeModel.Test
open FSharp.Data.UnitSystems.SI.UnitSymbols
open NUnit.Framework
open EoS.Phases
open EoS.DebyeModel
open EoS.Test

[<TestFixture>]
type DebyeSolidTest() =
  [<Test(Description = "Check the plausibility and consistency of Forsterite properties")>]
  member this.Forsterite() =
    let phase = Phases.fo
    PhaseAssert.PlausibleVolume phase null
    PhaseAssert.PlausibleEntropy phase null
    PhaseAssert.ConsistentVolume phase null
    PhaseAssert.ConsistentExpansivity phase null
    PhaseAssert.ConsistentCompressibility phase null
    PhaseAssert.ConsistentEntropy phase null
    PhaseAssert.ConsistentGrueneisen phase null

  [<Test(Description = "Check the plausibility and consistency of Fayalite properties")>]
  member this.Fayalite() =
    let phase = Phases.fa
    PhaseAssert.PlausibleVolume phase null
    PhaseAssert.PlausibleEntropy phase null
    PhaseAssert.ConsistentVolume phase null
    PhaseAssert.ConsistentExpansivity phase null
    PhaseAssert.ConsistentCompressibility phase null
    PhaseAssert.ConsistentEntropy phase null
    PhaseAssert.ConsistentGrueneisen phase null

  [<Test(Description = "Check the plausibility and consistency of Olivine properties")>]
  member this.Olivine() =
    let phase, x = Phases.ol, [|0.9; 0.1|]
    PhaseAssert.PlausibleVolume phase x
    PhaseAssert.PlausibleEntropy phase x
    PhaseAssert.ConsistentVolume phase x
    PhaseAssert.ConsistentEntropy phase x
    PhaseAssert.ConsistentExpansivity phase x
    PhaseAssert.ConsistentCompressibility phase x
    PhaseAssert.DisorderEntropy phase x

  [<Test(Description = "Check the plausibility and consistency of Corundum properties")>]
  member this.Corundum() =
    let phase = Phases.co
    Assert.AreEqual(phase.Mass()/1.0e-3<kg/mol>, 101.96, 0.01, "Mass mismatch")
    PhaseAssert.PlausibleVolume phase null
    PhaseAssert.PlausibleEntropy phase null
    PhaseAssert.ConsistentVolume phase null
    PhaseAssert.ConsistentExpansivity phase null
    PhaseAssert.ConsistentCompressibility phase null
    PhaseAssert.ConsistentEntropy phase null

[<TestFixture>]
type CorundumEntropyTest() =
  inherit NumericalTest(1.0)

  let phase = Phases.co

  member val Samples =
    [1.0e5<Pa>, 299.427<K>, 51.586<J/K/mol>
     1.0e5<Pa>, 400.287<K>, 77.6295<J/K/mol>
     1.0e5<Pa>, 497.994<K>, 98.9775<J/K/mol>
     1.0e5<Pa>, 501.146<K>, 99.666<J/K/mol>
     1.0e5<Pa>, 598.854<K>, 119.1985<J/K/mol>
     1.0e5<Pa>, 699.713<K>, 136.1965<J/K/mol>
     1.0e5<Pa>, 702.865<K>, 136.728<J/K/mol>
     1.0e5<Pa>, 797.421<K>, 153.2555<J/K/mol>
     1.0e5<Pa>, 800.573<K>, 153.6805<J/K/mol>
     1.0e5<Pa>, 901.433<K>, 167.279<J/K/mol>
     1.0e5<Pa>, 1002.29<K>, 179.7995<J/K/mol>
     1.0e5<Pa>, 1005.44<K>, 180.1875<J/K/mol>
     1.0e5<Pa>, 1100.0<K>, 191.8195<J/K/mol>
     1.0e5<Pa>, 1103.15<K>, 192.1955<J/K/mol>
     1.0e5<Pa>, 1200.86<K>, 203.8395<J/K/mol>
     1.0e5<Pa>, 1204.01<K>, 204.128<J/K/mol>
     1.0e5<Pa>, 1301.72<K>, 213.067<J/K/mol>
     1.0e5<Pa>, 1304.87<K>, 213.3555<J/K/mol>
     1.0e5<Pa>, 1399.43<K>, 222.8715<J/K/mol>
     1.0e5<Pa>, 1402.58<K>, 223.169<J/K/mol>
     1.0e5<Pa>, 1500.29<K>, 232.3875<J/K/mol>
     1.0e5<Pa>, 1503.44<K>, 232.669<J/K/mol>
     1.0e5<Pa>, 1601.15<K>, 241.4025<J/K/mol>
     1.0e5<Pa>, 1702.01<K>, 248.915<J/K/mol>
     1.0e5<Pa>, 1799.71<K>, 256.1925<J/K/mol>
     1.0e5<Pa>, 1802.87<K>, 256.4275<J/K/mol>
     1.0e5<Pa>, 1900.57<K>, 263.705<J/K/mol>
     1.0e5<Pa>, 1903.72<K>, 263.94<J/K/mol>
     1.0e5<Pa>, 1998.28<K>, 270.9515<J/K/mol>
     1.0e5<Pa>, 2004.58<K>, 271.346<J/K/mol>
     1.0e5<Pa>, 2102.29<K>, 277.4625<J/K/mol>
     1.0e5<Pa>, 2200.0<K>, 283.9735<J/K/mol>
     1.0e5<Pa>, 2203.15<K>, 284.1835<J/K/mol>]

  [<Test(Description = "Compares simulation results with tabulated values")>]
  member this.SampleValues() =
    for p, T, S in this.Samples do
      Assert.AreEqual(S/1.0<J/K/mol>, phase.Entropy(p, T)/1.0<J/K/mol>, this.Tolerance,
                      "Entropy mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

[<TestFixture>]
type CorundumHeatCapacityTest() =
  inherit NumericalTest(1.0)

  let phase = Phases.co

  member val Samples =
    [1.0e5<Pa>, 299.427<K>, 80.0501<J/K/mol>
     1.0e5<Pa>, 400.287<K>, 96.0768<J/K/mol>
     1.0e5<Pa>, 497.994<K>, 106.0935<J/K/mol>
     1.0e5<Pa>, 501.146<K>, 106.297<J/K/mol>
     1.0e5<Pa>, 598.854<K>, 112.6045<J/K/mol>
     1.0e5<Pa>, 699.713<K>, 117.112<J/K/mol>
     1.0e5<Pa>, 702.865<K>, 117.206<J/K/mol>
     1.0e5<Pa>, 797.421<K>, 120.023<J/K/mol>
     1.0e5<Pa>, 800.573<K>, 120.117<J/K/mol>
     1.0e5<Pa>, 901.433<K>, 123.122<J/K/mol>
     1.0e5<Pa>, 1002.29<K>, 125.0645<J/K/mol>
     1.0e5<Pa>, 1005.44<K>, 125.125<J/K/mol>
     1.0e5<Pa>, 1100.0<K>, 127.064<J/K/mol>
     1.0e5<Pa>, 1103.15<K>, 127.1285<J/K/mol>
     1.0e5<Pa>, 1200.86<K>, 128.584<J/K/mol>
     1.0e5<Pa>, 1204.01<K>, 128.631<J/K/mol>
     1.0e5<Pa>, 1301.72<K>, 130.1335<J/K/mol>
     1.0e5<Pa>, 1304.87<K>, 130.165<J/K/mol>
     1.0e5<Pa>, 1399.43<K>, 131.104<J/K/mol>
     1.0e5<Pa>, 1402.58<K>, 131.135<J/K/mol>
     1.0e5<Pa>, 1500.29<K>, 132.591<J/K/mol>
     1.0e5<Pa>, 1503.44<K>, 132.6375<J/K/mol>
     1.0e5<Pa>, 1601.15<K>, 133.1385<J/K/mol>
     1.0e5<Pa>, 1702.01<K>, 134.14<J/K/mol>
     1.0e5<Pa>, 1799.71<K>, 135.142<J/K/mol>
     1.0e5<Pa>, 1802.87<K>, 135.173<J/K/mol>
     1.0e5<Pa>, 1900.57<K>, 136.1435<J/K/mol>
     1.0e5<Pa>, 1903.72<K>, 136.1585<J/K/mol>
     1.0e5<Pa>, 1998.28<K>, 136.614<J/K/mol>
     1.0e5<Pa>, 2004.58<K>, 136.6445<J/K/mol>
     1.0e5<Pa>, 2102.29<K>, 137.646<J/K/mol>
     1.0e5<Pa>, 2200.0<K>, 137.646<J/K/mol>
     1.0e5<Pa>, 2203.15<K>, 137.646<J/K/mol>]

  [<Test(Description = "Compares simulation results with tabulated values")>]
  member this.SampleValues() =
    for p, T, Cp in this.Samples do
      Assert.AreEqual(Cp/1.0<J/K/mol>, phase.IsobaricHeatCapacity(p, T)/1.0<J/K/mol>, this.Tolerance,
                      "Heat capacity mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)
