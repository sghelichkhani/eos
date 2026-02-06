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
open FSharp.Data.UnitSystems.SI.UnitSymbols
open NUnit.Framework
open EoS.Parsing
open EoS.Chemistry

[<TestFixture>]
type ChemistryTest() =
  let h2o = Composite[upcast Element.H, 2.0; upcast Element.O, 1.0]

  let fo = Composite[upcast Composite[upcast Element.Mg, 1.0
                                      upcast Element.O, 1.0], 2.0
                     upcast Composite[upcast Element.Si, 1.0
                                      upcast Element.O, 2.0], 1.0]

  [<Test(Description = "Triggers a format exception")>]
  member this.ErrorDetection() =
    match Formula.parse "   Foo_2   " with
    | Success (formula, _) -> Assert.Fail("Formula produced from garbage: {0}", formula)
    | Failure (pos, msg) -> Assert.AreEqual(3, pos, "Error position not matched: {0}", msg)

  [<Test(Description = "Verify the properties of carbon")>]
  member this.VerifyCarbon() =
    let f = Element.C
    Assert.AreEqual("C", f.Name, "Name mismatch")
    Assert.AreEqual(6, f.Ordinal, "Ordinal mismatch")
    Assert.AreEqual(1, f.Atoms, "Atom count mismatch")
    Assert.AreEqual(12.011e-3, f.Mass / 1.0<kg/mol>, 1.0e-6, "Mass mismatch")

  [<Test(Description = "Verify the properties of forsterite")>]
  member this.VerifyForsterite() =
    let f1 = Composite[upcast Element.O, 4.0
                       upcast Element.Mg, 2.0
                       upcast Element.Si, 1.0]
    Assert.AreEqual(7.0, fo.Atoms, "Atom count mismatch")
    Assert.AreEqual(fo.Atoms, f1.Atoms, "Atom count mismatch between flat and nested formulas")
    Assert.AreEqual(140.693e-3, fo.Mass / 1.0<kg/mol>, 1.0e-6, "Molar mass mismatch")
    Assert.AreEqual(fo.Mass, f1.Mass, "Molar mass mismatch between flat and nested formulas")
    Assert.AreEqual(fo.Flatten(), f1.Flatten(), "Mismatch between flattened formulas")

  [<Test(Description = "Verify that some single-atom formulas are parsed correctly")>]
  member this.ParseSingleAtom() =
    let f = Formula.ofString "C"
    Assert.AreSame(Element.C, f, "C not parsed as carbon")
    let f = Formula.ofString "He"
    Assert.AreSame(Element.He, f, "He not parsed as helium")

  [<Test(Description = "Verify that some multi-atom formula is parsed correctly")>]
  member this.ParseMultiAtom() =
    let f1 = Formula.ofString "H2O"
    Assert.AreEqual(h2o, f1, "Formula mismatch")

  [<Test(Description = "Verify that some grouped formula is parsed correctly")>]
  member this.ParseGrouped() =
    let f1 = Formula.ofString "(MgO)_2 (SiO2)"
    Assert.AreEqual(fo, f1, "Formula mismatch")
