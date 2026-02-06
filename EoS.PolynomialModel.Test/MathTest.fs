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
open NUnit.Framework
open EoS.Parsing
open EoS.PolynomialModel.Math

[<TestFixture>]
type MathTest() =
  let zero = Polynomial[0.0, 0.0]

  let four = Polynomial[0.0, 4.0]

  let x2p2 = Polynomial[2.0, 1.0; 0.0, 2.0]

  let twox = Polynomial[1.0, 2.0]

  let invx = Polynomial[-1.0, 1.0]

  let xlnx = Polynomial[1.0, true, 1.0]

  let lnx = Polynomial[0.0, true, 1.0]

  [<Test(Description = "Triggers a format exception")>]
  member this.ErrorDetection() =
    match Polynomial.parse "   x * y   " with
    | Success (poly, _) -> Assert.Fail("Polynomial produced from garbage: {0}", poly)
    | Failure (pos, msg) -> Assert.AreEqual(5, pos, "Error position not matched: {0}", msg)

  [<Test(Description = "Verifies parsing of constants")>]
  member this.ParseConstant() =
    Assert.AreEqual(zero, Polynomial.ofString"0", "Constant zero not matched")
    Assert.AreEqual(four, Polynomial.ofString"4", "Constant four not matched")

  [<Test(Description = "Verifies parsing of single terms")>]
  member this.ParseSingleTerm() =
    Assert.AreEqual(twox, Polynomial.ofString"2 x", "Polynomial not matched")
    Assert.AreEqual(twox, Polynomial.ofString"2.0 y^1", "Polynomial not matched")
    Assert.AreEqual(twox, Polynomial.ofString"2 z^+1.0", "Polynomial not matched")
    Assert.AreEqual(twox, Polynomial.ofString"2 * u", "Polynomial not matched")
    Assert.AreEqual(twox, Polynomial.ofString"+2.0 / v^-1.0", "Polynomial not matched")

  [<Test(Description = "Verifies parsing of multiple terms")>]
  member this.ParseMultiTerm() =
    Assert.AreEqual(x2p2, Polynomial.ofString"x^2 + 2", "Polynomial not matched")
    Assert.AreEqual(x2p2, Polynomial.ofString"1 * x^2 + 2 * x^0", "Polynomial not matched")

  [<Test(Description = "Verifies the folding of terms")>]
  member this.FoldTerms() =
    Assert.AreEqual(twox, Polynomial[1.0, 1.5; 0.0, 2.0; 1.0, 0.5; 0.0, -2.0; 3.0, 0.0], "Redundant terms not eliminated")

  [<Test(Description = "Verifies the detection of constants")>]
  member this.DetectConstant() =
    Assert.AreEqual(Some 0.0, Polynomial.tryGetConstant zero, "Constant zero not matched")
    Assert.IsTrue(Polynomial.isConstant zero, "Constant not detected")
    Assert.IsTrue(Polynomial.isZero zero, "Zero not detected")
    Assert.AreEqual(Some 4.0, Polynomial.tryGetConstant four, "Constant four not matched")
    Assert.IsTrue(Polynomial.isConstant four, "Constant not detected")
    Assert.IsFalse(Polynomial.isZero four, "False positive zero detected")
    Assert.AreEqual(None, Polynomial.tryGetConstant twox, "Variable not detected")

  [<Test(Description = "Verify symbolic operations")>]
  member this.SymbolicOperations() =
    Assert.AreEqual(twox, x2p2.Derivative, "Derivative not matched")
    Assert.AreEqual(x2p2, twox.Integral + 2.0, "Integral not matched")
    Assert.AreEqual(zero, (twox * 2.0) - (2.0 * twox), "Redundant terms not eliminated")
    Assert.AreEqual(zero, x2p2 - x2p2, "Redundant terms not eliminated")
    Assert.AreEqual(four, (twox + twox).Derivative, "Constant not matched")

  [<Test(Description = "Verify symbolic operations involving logarithms")>]
  member this.LogOperations() =
    let hx2lxmqx2 = Polynomial.ofString"0.5 x^2 log x - 0.25 x^2"
    Assert.AreEqual(invx, lnx.Derivative, "Simple derivative not matched")
    Assert.AreEqual(lnx + 1.0, xlnx.Derivative, "Complex derivative not matched")
    Assert.AreEqual(lnx, invx.Integral, "Simple integral not matched")
    Assert.AreEqual(hx2lxmqx2, xlnx.Integral, "Complex integral not matched")

  [<Test(Description = "Verify simple evaluation results")>]
  member this.EvalSimple() =
    Assert.AreEqual(0.0, zero.Eval(42.0), "Zero not reproduced")
    Assert.AreEqual(4.0, four.Eval(42.0), "Constant not reproduced")
    Assert.AreEqual(46.0, twox.Eval(23.0), "Result not reproduced")
    Assert.AreEqual(4.0**2.0 + 2.0, x2p2.Eval(4.0), "Result not reproduced")

  [<Test(Description = "Verify complex evaluation results")>]
  member this.EvalComplex() =
    let poly = Polynomial.ofString"4 x^2 - x^0.5 + 3 x^3 - 2 / x"
    let func x = (4.0 + 3.0 * x) * x * x - 2.0 / x - (sqrt x)
    Assert.AreEqual(func 42.0, poly.Eval(42.0), 1.0e-12, "Computation paths don't match")
