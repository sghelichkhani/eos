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
open EoS.Math
open NUnit.Framework

type NumericalTest(accuracy : float) =
  member val Accuracy = accuracy
  member this.Tolerance = this.Accuracy * 5.0

[<TestFixture>]
type Debye3Test() =
  inherit NumericalTest(1.0e-16)

  member val Samples =
    [0.0019531250, 0.99926776885985461940
     0.0312500000, 0.98833007755734698212
     0.1250000000, 0.95390610472023510237
     0.5000000000, 0.82496296897623372315
     1.0000000000, 0.67441556407781468010
     1.5000000000, 0.54710665141286285468
     2.0000000000, 0.44112847372762418113
     2.5000000000, 0.35413603481042394211
     3.0000000000, 0.28357982814342246206
     4.0000000000, 0.18173691382177474795
     4.2500000000, 0.16277924385112436877
     5.0000000000, 0.11759741179993396450
     5.5000000000, 0.95240802723158889887e-01
     6.0000000000, 0.77581324733763020269e-01
     8.0000000000, 0.36560295673194845002e-01
     10.0000000000, 0.19295765690345489563e-01
     15.0000000000, 0.57712632276188798621e-02
     20.0000000000, 0.24352200674805479827e-02
     30.0000000000, 0.72154882216335666096e-03
     50.0000000000, 0.15585454565440389896e-03]

  [<Test(Description = "Triggers an invalid argument exception")>]
  member this.ErrorDetection() =
    try
      let r = debye3 -1.0
      Assert.Fail("Expected ArgumentException, got result {}", r)
    with
    | :? ArgumentException ->
      Assert.Pass()

  [<Test(Description = "Compares numerical results with tabulated values")>]
  member this.SampleValues() =
    for x, v in this.Samples do
      Assert.AreEqual(v, debye3 x, this.Tolerance,
                      "debye3({0}) not matched", x)

[<TestFixture>]
type BracketTest() =
  inherit NumericalTest(1.0e-12)

  [<Test(Description = "Triggers a numerical exception")>]
  member this.ErrorDetection() =
    try
      let bracket = bracket this.Accuracy
      let r = bracket sin (Math.PI/4.0) (Math.PI/2.0)
      Assert.Fail("Expected NumericalException, got result {}", r)
    with
    | :? NumericalException ->
      Assert.Pass()
    
  [<Test(Description = "Compares numerical results with well-known analytical values")>]
  member this.TheoreticalRoots() =
    let bracket = bracket this.Accuracy
    Assert.AreEqual(Math.PI/2.0, bracket cos 0.0 Math.PI, this.Tolerance,
                    "cos(?) = 0 not found")
    Assert.AreEqual(0.0, bracket (fun x -> (exp x) - 1.0) -10.0 10.0, this.Tolerance,
                    "exp(?) - 1 = 0 not found")

[<TestFixture>]
type DeriveTest() =
  inherit NumericalTest(1.0e-8)

  [<Test(Description = "Compares numerical results with well-known analytical values for trigonometric functions")>]
  member this.TheoreticalTrigFuncs() =
    let derive = derive this.Accuracy 1.0
    for direction in -1 .. 1 do
      for x in 0.0 .. Math.PI/8.0 .. 2.0*Math.PI do
        Assert.AreEqual(cos x, derive sin x direction, this.Tolerance,
                        "sin'({0}) = cos({0}) not matched for direction = {1}", x, direction)

  [<Test(Description = "Compares numerical results with well-known analytical values for the exponential function")>]
  member this.TheoreticalExpFunc() =
    let derive = derive this.Accuracy 1.0
    for x in 0.0 .. 10.0 do
      Assert.AreEqual(exp x, derive exp x 0, this.Tolerance,
                      "exp'({0}) = exp({0}) not matched", x)

[<TestFixture>]
type IntegrateTest() =
  inherit NumericalTest(1.0e-8)

  [<Test(Description = "Compares numerical results with well-known analytical values for trigonometric functions")>]
  member this.TheoreticalTrigFuncs() =
    let integrate = integrate this.Accuracy
    for x in 0.0 .. Math.PI/8.0 .. 2.0*Math.PI do
      Assert.AreEqual(sin x, integrate cos 0.0 x, this.Tolerance,
                      "Int[0..{0}] cos = sin({0}) not matched", x)

  [<Test(Description = "Compares numerical results with well-known analytical values for the exponential function")>]
  member this.TheoreticalExpFunc() =
    let integrate = integrate this.Accuracy
    for x in 0.0 .. 10.0 do
      Assert.AreEqual((exp x) - 1.0, integrate exp 0.0 x, this.Tolerance,
                      "Int[0..{0}] exp = exp({0}) - 1 not matched", x)
