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

namespace EoS.Optimization
open System
open EoS.Chemistry
open EoS.Phases
open EoS.LPSolve

/// Utilities for dealing with formulas.
module Formula =
  /// Write a bulk composition by elements in terms of the given
  /// formulas. The result is composition vector and a number of unused
  /// atoms in the bulk composition.
  let regroup (bulk : Collections.Generic.SortedList<Element, float>) (groups : Formula[]) =
    let elements = Collections.Generic.SortedList<Element, float>(bulk)
    for group in groups do group.FlattenTo(elements, 1.0)

    use problem =
      new Problem<1, 1>(
        2 * elements.Count, groups.Length,
        Maximize = true,
        Scaling = (Scaling.GEOMETRIC ||| Scaling.EQUILIBRATE),
        Improvement = (Improvement.DUALFEAS ||| Improvement.THETAGAP),
        Pivoting = (Pivoting.DEVEX ||| Pivoting.ADAPTIVE))

    problem.SetOutputFile()
    #if DEBUG
    problem.Verbosity <- Verbosity.DETAILED
    #else
    problem.Verbosity <- Verbosity.SEVERE
    #endif

    elements.Keys |> Seq.iteri (fun row element ->
      let row = 2 * row
      #if DEBUG
      problem.RowName(row + 0) <- element.Name + ">"
      problem.RowName(row + 1) <- element.Name + "<"
      #endif
      let mutable n = 0.0
      bulk.TryGetValue(element, &n) |> ignore
      problem.Constraint(row + 0) <- (ConstraintType.GE, 0.0)
      problem.Constraint(row + 1) <- (ConstraintType.LE, n))

    groups |> Array.iteri (fun col group ->
      #if DEBUG
      problem.ColumnName(col) <- group.ToString()
      #endif
      problem.Objective(col) <- group.Atoms
      problem.SetUnbounded(col)
      for KeyValue (element, n) in group.Flatten() do
        let row = 2 * elements.IndexOfKey(element)
        problem[row + 0, col] <- n
        problem[row + 1, col] <- n)

    let x, n, _ = problem.Solve()
    x, Seq.sum bulk.Values - n

/// Phase extensions for formula to composition matching.
[<AutoOpen>]
module PhaseExtensions =
  type IPhase with
    /// Try to produce a composition vector for this phase that
    /// matches the given bulk composition. Returns a composition
    /// vector and a number of unused atoms in the bulk composition.
    member this.FindComposition(bulk : Collections.Generic.SortedList<Element, float>) =
      Array.init this.XLength (fun i ->
        let x = Array.zeroCreate this.XLength
        x[i] <- 1.0
        this.Formula(x))
      |> Formula.regroup bulk
