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
#nowarn "1240"

namespace EoS.PolynomialModel.Math
open System

/// Representation of a generalized polynomial of one variable. To
/// close the set of representable functions under integration and
/// differentiation, logarithmic terms are allowed. The variable name
/// is used for presentation purposes only.
[<Sealed>]
type Polynomial<[<Measure>] 'A, [<Measure>] 'R> private (terms : Collections.Generic.SortedList<float * bool, float>, variable : string) =
  static let addTerm (terms : Collections.Generic.SortedList<float * bool, float>) power lnpow c =
    if c <> 0.0 then
      match terms.TryGetValue((power, lnpow)) with
      | true, c0 ->
        let c = c0 + c
        if c <> 0.0 then
          terms[(power, lnpow)] <- c
        else
          terms.Remove((power, lnpow)) |> ignore
      | false, _ ->
        terms[(power, lnpow)] <- c

  static let triplesToTerms (terms : seq<float * bool * float>) =
    let acc = Collections.Generic.SortedList()
    for power, lnpow, c in terms do addTerm acc power lnpow c
    acc

  static let pairsToTerms (terms : seq<float * float>) =
    let acc = Collections.Generic.SortedList()
    for power, c in terms do addTerm acc power false c
    acc

  let incpow : Lazy<Polynomial<'A, 'R * 'A>> =
    lazy
      let acc = Collections.Generic.SortedList()
      for KeyValue ((power, lnpow), c) in terms do
        addTerm acc (power + 1.0) lnpow c
      Polynomial(acc, variable)

  let decpow : Lazy<Polynomial<'A, 'R / 'A>> =
    lazy
      let acc = Collections.Generic.SortedList()
      for KeyValue ((power, lnpow), c) in terms do
        addTerm acc (power - 1.0) lnpow c
      Polynomial(acc, variable)

  let derivative : Lazy<Polynomial<'A, 'R / 'A>> =
    lazy
      let acc = Collections.Generic.SortedList()
      for KeyValue ((power, lnpow), c) in terms do
        let power' = power - 1.0
        addTerm acc power' lnpow (power * c)
        if lnpow then addTerm acc power' false c
      Polynomial(acc, variable)

  let integral : Lazy<Polynomial<'A, 'R * 'A>> =
    lazy
      let acc = Collections.Generic.SortedList()
      for KeyValue ((power, lnpow), c) in terms do
        if power = -1.0 then
          if lnpow then
            failwithf "Integral of log(%s) / %s is not representable" variable variable
          else
            addTerm acc 0.0 true c
        else
          let power' = power + 1.0
          addTerm acc power' lnpow (c / power')
          if lnpow then addTerm acc power' false (-c / power' / power')
      Polynomial(acc, variable)

  /// Default externally callable constructor. Takes a sequence of
  /// exponent, multiplied by logarithm flag and coefficient tuples.
  new (terms : seq<float * bool * float>, ?variable : string) =
    Polynomial(triplesToTerms terms, defaultArg variable "x")

  /// Pure polynomial externally callable constructor. Takes a
  /// sequence of exponent, coefficient pairs.
  new (terms : seq<float * float>, ?variable : string) =
    Polynomial(pairsToTerms terms, defaultArg variable "x")

  /// Name of the variable.
  member this.Variable =
    variable

  /// Sequence of power, logarithm flag, coefficient tuples.
  member this.Terms =
    Seq.map (fun (KeyValue ((power, lnpow), c)) -> power, lnpow, c) terms

  /// The generalized polynomial multiplied with its variable.
  member this.IncrementedPower =
    incpow.Value

  /// The generalized polynomial divided by its variable.
  member this.DecrementedPower =
    decpow.Value

  /// The derivative of the generalized polynomial.
  member this.Derivative =
    derivative.Value

  /// The integral of the generalized polynomial.
  member this.Integral =
    integral.Value

  /// Add a constant to all exponents in the polynomial.
  member this.Shift(shift : float) =
    Polynomial<'A, 'R>(
      Seq.map (fun (power, lnpow, c) -> power + shift, lnpow, c) this.Terms,
      variable)

  /// Evaluate the polynomial at a given point.
  member this.Eval(x : float<'A>) : float<'R> =
    let x = float x
    // NOTE a more efficient implementation may be possible
    terms
    |> Seq.fold (fun acc (KeyValue ((p, l), c)) ->
      acc +
      if l then
        c * x**p * log x
      else
        c * x**p) 0.0
    |> box |> unbox

  /// Evaluate the difference of the polynomial's values at two given points.
  member this.Eval(x0 : float<'A>, x1 : float<'A>) : float<'R> =
    let x0 = float x0
    let x1 = float x1
    // NOTE a more efficient implementation may be possible
    terms
    |> Seq.fold (fun acc (KeyValue ((p, l), c)) ->
      acc +
      if l then
        if sign p <> 0 then
          c * (x1**p * log x1 - x0**p * log x0)
        else
          c * log(x1 / x0)
      else
        c * (x1**p - x0**p)) 0.0
    |> box |> unbox

  override this.Equals(that) =
    Object.ReferenceEquals(this, that) ||
    match that with
    | :? Polynomial<'A, 'R> as that ->
      Seq.forall2 (=) this.Terms that.Terms
    | _ ->
      false

  override this.GetHashCode() =
    this.Terms
    |> Seq.fold (fun acc it ->
      acc + it.GetHashCode()) 0

  override this.ToString() =
    let acc = Text.StringBuilder(terms.Count * 4)
    for KeyValue ((power, lnpow), c) in terms do
      if acc.Length > 0 then acc.Append(' ') |> ignore
      let appendPower (acc : Text.StringBuilder) =
        acc.Append(if power < 0.0 then "/ " else "").Append(variable) |> ignore
        match abs power with
        | 1.0 -> acc
        | power -> acc.Append('^').Append(power)
      let appendLog (acc : Text.StringBuilder) =
        acc.Append("log ").Append(variable)
      let inline appendSpace (acc : Text.StringBuilder) =
        acc.Append(' ')
      let appendTerm (acc : Text.StringBuilder) =
        match abs c, sign power, lnpow with
        | 1.0, 0, false -> acc.Append('1')
        | 1.0, 0, true -> acc |> appendLog
        | 1.0, +1, false -> acc |> appendPower
        | 1.0, -1, false -> acc.Append("1 ") |> appendPower
        | 1.0, _, true -> acc |> appendLog |> appendSpace |> appendPower
        | c, 0, false -> acc.Append(c)
        | c, 0, true -> acc.Append(c) |> appendSpace |> appendLog
        | c, -1, true -> acc.Append(c) |> appendSpace |> appendLog |> appendSpace |> appendPower
        | c, _, false -> acc.Append(c) |> appendSpace |> appendPower
        | c, _, true -> acc.Append(c) |> appendSpace |> appendPower |> appendSpace |> appendLog
        |> ignore
      match sign c with
      | -1 -> acc.Append("- ") |> appendTerm
      | +1 -> (if acc.Length > 0 then acc.Append("+ ") else acc) |> appendTerm
      | _ -> ()
    if acc.Length = 0 then acc.Append('0') |> ignore
    acc.ToString()

  /// Prefix plus nop.
  static member inline (~+) (poly : Polynomial<'A, 'R>) =
    poly

  /// Add two polynomials.
  static member (+) (a : Polynomial<'A, 'R>, b : Polynomial<'A, 'R>) =
    Polynomial<'A, 'R>(
      Seq.append a.Terms b.Terms,
      a.Variable)

  /// Add a constant to a polynomial.
  static member (+) (a : Polynomial<'A, 'R>, b : float<'R>) =
    Polynomial<'A, 'R>(
      Seq.append a.Terms [0.0, false, float b],
      a.Variable)

  /// Add a constant to a polynomial.
  static member (+) (a : float<'R>, b : Polynomial<'A, 'R>) =
    Polynomial<'A, 'R>(
      Seq.append [0.0, false, float a] b.Terms,
      b.Variable)

  /// Additive inverse of a polynomial.
  static member (~-) (poly : Polynomial<'A, 'R>) =
    Polynomial<'A, 'R>(
      Seq.map (fun (power, lnpow, c) -> power, lnpow, -c) poly.Terms,
      poly.Variable)

  /// Subtract two polynomials.
  static member (-) (a : Polynomial<'A, 'R>, b : Polynomial<'A, 'R>) =
    Polynomial<'A, 'R>(
      Seq.append a.Terms (-b).Terms,
      a.Variable)

  /// Subtract a constant from a polynomial.
  static member (-) (a : Polynomial<'A, 'R>, b : float<'R>) =
    Polynomial<'A, 'R>(
      Seq.append a.Terms [0.0, false, float -b],
      a.Variable)

  /// Subtract a polynomial from a constant.
  static member (-) (a : float<'R>, b : Polynomial<'A, 'R>) =
    Polynomial<'A, 'R>(
      Seq.append [0.0, false, float a] (-b).Terms,
      b.Variable)

  /// Scale a polynomial by a constant.
  static member ( * ) (poly : Polynomial<'A, 'R>, scale : float<'S>) =
    Polynomial<'A, 'R * 'S>(
      Seq.map (fun (power, lnpow, c) -> power, lnpow, c * float scale) poly.Terms,
      poly.Variable)

  /// Scale a polynomial by a constant.
  static member inline ( * ) (scale : float<'S>, poly : Polynomial<'A, 'R>) =
    poly * scale

  /// Scale a polynomial by an inverse constant.
  static member (/) (poly : Polynomial<'A, 'R>, scale : float<'S>) =
    Polynomial<'A, 'R / 'S>(
      Seq.map (fun (power, lnpow, c) -> power, lnpow, c / float scale) poly.Terms,
      poly.Variable)
