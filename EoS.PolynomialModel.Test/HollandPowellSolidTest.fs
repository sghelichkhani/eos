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

namespace EoS.PolynomialModel.Test
open FSharp.Data.UnitSystems.SI.UnitSymbols
open NUnit.Framework
open EoS.Phases
open EoS.PolynomialModel
open EoS.Test

[<TestFixture>]
type HollandPowellSolidTest() =
  [<Test(Description = "Check the plausibility and consistency of Forsterite properties")>]
  member this.Forsterite() =
    let phase = Phases.fo
    PhaseAssert.PlausibleVolume phase null
    PhaseAssert.PlausibleEntropy phase null
    PhaseAssert.ConsistentVolume phase null
    PhaseAssert.ConsistentExpansivity phase null
    PhaseAssert.ConsistentCompressibility phase null
    PhaseAssert.ConsistentEntropy phase null

  [<Test(Description = "Check the plausibility and consistency of Fayalite properties")>]
  member this.Fayalite() =
    let phase = Phases.fa
    PhaseAssert.PlausibleVolume phase null
    PhaseAssert.PlausibleEntropy phase null
    PhaseAssert.ConsistentVolume phase null
    PhaseAssert.ConsistentExpansivity phase null
    PhaseAssert.ConsistentCompressibility phase null
    PhaseAssert.ConsistentEntropy phase null

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
type ForsteriteExpansivityTest() =
  inherit NumericalTest(0.05e-5)

  let phase = Phases.fo

  member val Samples =
    [1.0e5<Pa>, 300.429<K>, 2.71812e-5<1/K>
     1.0e5<Pa>, 394.85<K>, 3.02013e-5<1/K>
     1.0e5<Pa>, 497.854<K>, 3.22148e-5<1/K>
     1.0e5<Pa>, 600.858<K>, 3.3557e-5<1/K>
     1.0e5<Pa>, 695.279<K>, 3.48993e-5<1/K>
     1.0e5<Pa>, 798.283<K>, 3.5906e-5<1/K>
     1.0e5<Pa>, 901.288<K>, 3.69128e-5<1/K>
     1.0e5<Pa>, 995.708<K>, 3.79195e-5<1/K>
     1.0e5<Pa>, 1098.71<K>, 3.92617e-5<1/K>
     1.0e5<Pa>, 1193.13<K>, 4.0604e-5<1/K>
     1.0e5<Pa>, 1296.14<K>, 4.16107e-5<1/K>
     1.0e5<Pa>, 1399.14<K>, 4.26174e-5<1/K>
     1.0e5<Pa>, 1493.56<K>, 4.39597e-5<1/K>
     1.0e5<Pa>, 1596.57<K>, 4.49664e-5<1/K>
     1.0e5<Pa>, 1699.57<K>, 4.63087e-5<1/K>]

  [<Test(Description = "Compares simulation results with tabulated values")>]
  member this.SampleValues() =
    for p, T, α in this.Samples do
      Assert.AreEqual(α/1.0<1/K>, phase.Expansivity(p, T)/1.0<1/K>, this.Tolerance,
                      "Expansivity mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

[<TestFixture>]
type ForsteriteBulkModulusTest() =
  inherit NumericalTest(0.1e9)

  let phase = Phases.fo

  member val Samples =
    [1.0e5<Pa>, 294.372<K>, 127.143e9<Pa>
     1.0e5<Pa>, 398.268<K>, 125.238e9<Pa>
     1.0e5<Pa>, 493.506<K>, 123.016e9<Pa>
     1.0e5<Pa>, 597.403<K>, 120.476e9<Pa>
     1.0e5<Pa>, 701.299<K>, 118.571e9<Pa>
     1.0e5<Pa>, 796.537<K>, 116.349e9<Pa>
     1.0e5<Pa>, 900.433<K>, 113.81e9<Pa>
     1.0e5<Pa>, 995.671<K>, 111.587e9<Pa>
     1.0e5<Pa>, 1090.91<K>, 109.365e9<Pa>
     1.0e5<Pa>, 1194.81<K>, 106.508e9<Pa>
     1.0e5<Pa>, 1298.7<K>, 104.603e9<Pa>
     1.0e5<Pa>, 1393.94<K>, 102.063e9<Pa>
     1.0e5<Pa>, 1497.84<K>, 99.8413e9<Pa>
     1.0e5<Pa>, 1593.07<K>, 97.3016e9<Pa>
     1.0e5<Pa>, 1696.97<K>, 94.7619e9<Pa>]

  [<Test(Description = "Compares simulation results with tabulated values")>]
  member this.SampleValues() =
    for p, T, κ in this.Samples do
      Assert.AreEqual(κ/1.0<Pa>, fst(phase.Moduli(p, T))/1.0<Pa>, this.Tolerance,
                      "Bulk modulus mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

[<TestFixture>]
type GrossularVolumeTest() =
  inherit NumericalTest(0.01e-5)

  let phase = Phases.gr

  member val Samples =
    [300.0<K>, 0.296296e9<Pa>, 12.5281e-5<m^3/mol>
     300.0<K>, 0.395062e9<Pa>, 12.5151e-5<m^3/mol>
     300.0<K>, 0.691358e9<Pa>, 12.4935e-5<m^3/mol>
     300.0<K>, 1.11934e9<Pa>, 12.4633e-5<m^3/mol>
     300.0<K>, 1.20165e9<Pa>, 12.4547e-5<m^3/mol>
     300.0<K>, 1.61317e9<Pa>, 12.4245e-5<m^3/mol>
     300.0<K>, 1.8107e9<Pa>, 12.4158e-5<m^3/mol>
     300.0<K>, 2.09053e9<Pa>, 12.3813e-5<m^3/mol>
     300.0<K>, 2.13992e9<Pa>, 12.3942e-5<m^3/mol>
     300.0<K>, 2.50206e9<Pa>, 12.364e-5<m^3/mol>
     300.0<K>, 2.69959e9<Pa>, 12.3554e-5<m^3/mol>
     300.0<K>, 2.91358e9<Pa>, 12.3381e-5<m^3/mol>
     300.0<K>, 3.22634e9<Pa>, 12.3165e-5<m^3/mol>
     300.0<K>, 3.30864e9<Pa>, 12.3036e-5<m^3/mol>
     300.0<K>, 3.50617e9<Pa>, 12.2863e-5<m^3/mol>
     500.0<K>, 0.0987654e9<Pa>, 12.636e-5<m^3/mol>
     500.0<K>, 0.296296e9<Pa>, 12.6014e-5<m^3/mol>
     500.0<K>, 0.806584e9<Pa>, 12.5669e-5<m^3/mol>
     500.0<K>, 1.30041e9<Pa>, 12.541e-5<m^3/mol>
     500.0<K>, 1.71193e9<Pa>, 12.4978e-5<m^3/mol>
     500.0<K>, 2.20576e9<Pa>, 12.4763e-5<m^3/mol>
     500.0<K>, 2.60082e9<Pa>, 12.4504e-5<m^3/mol>
     500.0<K>, 2.79835e9<Pa>, 12.4288e-5<m^3/mol>
     500.0<K>, 3.11111e9<Pa>, 12.4072e-5<m^3/mol>
     500.0<K>, 3.29218e9<Pa>, 12.3986e-5<m^3/mol>
     500.0<K>, 3.50617e9<Pa>, 12.3813e-5<m^3/mol>
     500.0<K>, 3.90123e9<Pa>, 12.3554e-5<m^3/mol>
     800.0<K>, 0.312757e9<Pa>, 12.7007e-5<m^3/mol>
     800.0<K>, 0.592593e9<Pa>, 12.6705e-5<m^3/mol>
     800.0<K>, 0.987654e9<Pa>, 12.6446e-5<m^3/mol>
     800.0<K>, 1.30041e9<Pa>, 12.6187e-5<m^3/mol>
     800.0<K>, 1.79424e9<Pa>, 12.5928e-5<m^3/mol>
     800.0<K>, 2.107e9<Pa>, 12.5626e-5<m^3/mol>
     800.0<K>, 2.40329e9<Pa>, 12.554e-5<m^3/mol>
     800.0<K>, 2.89712e9<Pa>, 12.5151e-5<m^3/mol>
     800.0<K>, 3.09465e9<Pa>, 12.4935e-5<m^3/mol>
     800.0<K>, 3.40741e9<Pa>, 12.4547e-5<m^3/mol>
     1000.0<K>, 0.395062e9<Pa>, 12.7612e-5<m^3/mol>
     1000.0<K>, 0.691358e9<Pa>, 12.7482e-5<m^3/mol>
     1000.0<K>, 1.08642e9<Pa>, 12.7094e-5<m^3/mol>
     1000.0<K>, 1.59671e9<Pa>, 12.6662e-5<m^3/mol>
     1000.0<K>, 2.09053e9<Pa>, 12.6317e-5<m^3/mol>
     1000.0<K>, 2.60082e9<Pa>, 12.6101e-5<m^3/mol>
     1000.0<K>, 2.79835e9<Pa>, 12.5885e-5<m^3/mol>
     1000.0<K>, 3.11111e9<Pa>, 12.5583e-5<m^3/mol>
     1000.0<K>, 3.40741e9<Pa>, 12.5281e-5<m^3/mol>
     1000.0<K>, 3.80247e9<Pa>, 12.4935e-5<m^3/mol>]

  [<Test(Description = "Compares simulation results with tabulated values")>]
  member this.SampleValues() =
    for T, p, V in this.Samples do
      Assert.AreEqual(V/1.0<m^3/mol>, phase.Volume(p, T)/1.0<m^3/mol>, this.Tolerance,
                      "Volume mismatch at p = {0} Pa, T = {1} K", p/1.0<Pa>, T/1.0<K>)

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
