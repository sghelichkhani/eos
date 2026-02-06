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
open NUnit.Framework
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.PhysicalConstants

[<TestFixture>]
type PhysicalConstantsTest() = 
  [<Test>]
  member this.MolarAndAtomicGasConstant() =
    let u = 1.0<J/mol/K>
    Assert.AreEqual(MolarGas / u, AtomicGas * MolarCount / u, 0.000050,
                    "R = k * Na not matched")
