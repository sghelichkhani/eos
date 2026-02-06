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
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open NUnit.Framework
open EoS.Chemistry
open EoS.Phases
open EoS.Optimization
open EoS.DebyeModel.Test.Phases

[<TestFixture>]
type OptimizationTest() = 
  let bulk0 = Formula.ofString"Mg2SiO4"
  let bulk1 = Formula.ofString"(MgO)1.9(FeO)0.1(SiO2)"

  [<Test(Description = "Test minimization for simple phases")>]
  member this.ForsteritePolymorphs() =
    let pc = PhaseCollection[fo; mgwa; mgri]

    Assert.AreEqual([|1.0; 0.0; 0.0|], pc.Optimize(1.0e5<Pa>, 300.0<K>, bulk0),
                    "Expected olivine at reference conditions and conforming composition")
    (*
    Assert.AreEqual([|0.95; 0.0; 0.0|], pc.Optimize(1.0e5<Pa>, 300.0<K>, bulk1),
                    "Expected olivine at reference conditions and non-conforming composition")
    *)

    let optimalPhaseX p T =
      let it = pc |> Seq.minBy (fun it -> it.Phase.Energy(p, T))
      it.Phase, Array.init pc.XLength (fun i -> if i = it.XOffset then 1.0 else 0.0)

    let p, T = 11.0e9<Pa>, 600.0<K>
    let phase, x = optimalPhaseX p T
    Assert.AreEqual(x, pc.Optimize(p, T, bulk0),
                    "Expected {0} at p = {1} Pa, T = {2} K", string phase, p/1.0<Pa>, T/1.0<K>)

    let phaseChange (phase0 : IPhase) (phase1 : IPhase) T p0 p1 =
      seq { p0 .. 0.01e9<Pa> .. p1 }
      |> Seq.find (fun p -> phase1.Energy(p, T) < phase0.Energy(p, T))

    let checkPhaseChange phase0 phase1 T p0 p1 =
      let p = phaseChange phase0 phase1 T p0 p1
      let phase, x = optimalPhaseX (p - 0.01e9<Pa>) T
      Assert.AreEqual(phase0, phase,
                      "Phase mismatch at p = ({0} - 0.01) GPa, T = {1} K", p/1.0e9<Pa>, T/1.0<K>)
      Assert.AreEqual(x, pc.Optimize(p - 0.01e9<Pa>, T, bulk0),
                      "Expected {0} at p = ({1} - 0.01) GPa, T = {2} K", phase, p/1.0e9<Pa>, T/1.0<K>)
      let phase, x = optimalPhaseX (p + 0.01e9<Pa>) T
      Assert.AreEqual(phase1, phase,
                      "Phase mismatch at p = ({0} - 0.01) GPa, T = {1} K", p/1.0e9<Pa>, T/1.0<K>)
      Assert.AreEqual(x, pc.Optimize(p + 0.01e9<Pa>, T, bulk0),
                      "Expected {0} at p = ({1} + 0.01) GPa, T = {2} K", phase, p/1.0e9<Pa>, T/1.0<K>)

    checkPhaseChange fo mgwa 600.0<K> 11.0e9<Pa> 13.0e9<Pa>
    checkPhaseChange mgwa mgri 600.0<K> 12.0e9<Pa> 14.0e9<Pa>
    checkPhaseChange fo mgwa 1200.0<K> 12.0e9<Pa> 14.0e9<Pa>
    checkPhaseChange mgwa mgri 1200.0<K> 15.0e9<Pa> 17.0e9<Pa>
