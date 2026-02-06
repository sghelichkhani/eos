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

namespace EoS.Optimization.Test
open EoS.LPSolve
open NUnit.Framework
open EoS.Test

[<TestFixture>]
type LPSolveTest() = 
  inherit NumericalTest(1.0e-12)

  member val Samples =
    [[300.0; 500.0], [[1.0; 2.0], 170.0; [1.0; 1.0], 150.0; [0.0; 3.0], 180.0], [130.0; 20.0], 49.0e3
     [2.0; 3.0; 4.0], [[3.0; 2.0; 1.0], 10.0; [2.0; 5.0; 3.0], 15.0], [0.0; 0.0; 5.0], 20.0
     [200.0; 400.0], [[1.0/40.0; 1.0/60.0], 1.0; [1.0/50.0; 1.0/50.0], 1.0], [0.0; 50.0], 2.0e4]

  [<Test(Description = "Compares numerical results with well-known analytical values")>]
  member this.SampleValues() =
    for objs, itms, x0, v0 in this.Samples do
      use problem = new Problem<1, 1>(itms.Length, objs.Length, Maximize = true)
      objs
      |> Seq.iteri (fun col v ->
        problem.Objective(col) <- v)
      itms
      |> Seq.iteri (fun row (cols, lim) ->
        cols
        |> Seq.iteri (fun col v ->
          problem[row, col] <- v)
        problem.Constraint(row) <- (ConstraintType.LE, lim))

      let x, v, _ = problem.Solve()
      Seq.zip x0 x
      |> Seq.iteri (fun i (x0, x) ->
        Assert.AreEqual(x0, x, this.Tolerance, "Solution vector mismatch at index {0}", i))
      Assert.AreEqual(v0, v, this.Tolerance, "Objective value mismatch")
