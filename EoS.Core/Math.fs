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

module EoS.Math
open System

/// Numerical failure indicator.
type NumericalException(msg) =
  inherit System.Exception(msg)

let private Debye3Coefficients =
  [|-0.2e-19
    0.20e-18
    -0.244e-17
    0.3048e-16
    -0.38204e-15
    0.480759e-14
    -0.6077972e-13
    0.77250740e-12
    -0.987963459e-11
    0.12727961892e-9
    -0.165420999498e-8
    0.2173176139625e-7
    -0.28940328235386e-6
    0.392430195988049e-5
    -0.5463600095908238e-4
    0.79637553801738164e-3
    -0.1294515018444086863e-1
    0.34006813521109175100
    2.70773706832744094526|]

/// Compute the third order debye function
/// `D_3(x) = 3/x^3 int_0^x t^3/(e^t-1) dt`
let debye3 x =
  if x < 0.0 then
    invalidArg (nameof x) "non-negative argument required"
  elif x < 0.298023e-7 then
    (x - 7.5) * x / 20.0 + 1.0
  elif x <= 4.0 then
    let t = (x * x / 8.0) - 1.0
    let v = Array.zeroCreate<float> 3
    if abs x < 0.6 then
      let tt = 2.0 * t
      for c in Debye3Coefficients do
        v[2] <- v[1]
        v[1] <- v[0]
        v[0] <- tt * v[1] + c - v[2]
      (v[0] - v[2]) / 2.0 - 0.375 * x
    else
      let s = float (sign t)
      let tt = s * 2.0 * ((abs t) - 1.0)
      for c in Debye3Coefficients do
        v[2] <- v[0]
        v[0] <- tt * v[1] + c + s * v[2]
        v[1] <- v[0] + s * v[1]
      (v[0] + s * v[2]) / 2.0 - 0.375 * x
  elif x <= 0.9487163e103 then
    let v = 1.0 / (0.51329911273421675946e-1 * x * x * x)
    if x < 708.39642 then
      let emx = exp (-x)
      let mutable s = 0.0
      if x > 35.35051 then
        s <- (((x + 3.0) * x + 6.0) * x + 6.0) / (x * x * x)
      else
        let mutable rk = truncate (708.39642 / x)
        let mutable xk = rk * x
        while rk > 0.0 do
          let xki = 1.0 / xk
          s <- s * emx + (((6.0 * xki + 6.0) * xki + 3.0) * xki + 1.0) / rk
          rk <- rk - 1.0
          xk <- xk - x
      v - 3.0 * s * emx
    else
      v
  else
    0.0

/// Find a numerical root by bisection.
let bracket (accuracy : float<'Arg>) (f : float<'Arg> -> float<'Val>) (a : float<'Arg>) (b : float<'Arg>) : float<'Arg> =
  let rec search (a : float<'Arg>) (va : float<'Val>) (b : float<'Arg>) (vb : float<'Val>) =
    if va = 0.0<_> then
      a
    elif vb = 0.0<_> then
      b
    elif va * vb >= 0.0<_> then
      NumericalException(sprintf "[%A, %A] does not bracket a root" a b) |> raise
    else
      let c = (a + b) / 2.0
      if abs (a - b) < accuracy then
        c
      else
        let vc = f c
        if va * vc < 0.0<_> then
          search a va c vc
        elif vc * vb < 0.0<_> then
          search c vc b vb
        else
          c
  search a (f a) b (f b)

/// Find a numerical derivative 
let derive (accuracy : float<'Val / 'Arg>) (maxstep : float<'Arg>) (f : float<'Arg> -> float<'Val>) (x : float<'Arg>) (direction : int) : float<'Val / 'Arg> =
  let estimate =
    match sign direction with
    | +1 ->
      fun h ->
        let v0, vp1, vp2, vp3 =
          f x, f (x + h), f (x + 2.0*h), f (x + 3.0*h)
        (-1.5 * v0 + 2.0 * vp1 - 0.5 * vp2) / h,
        (-11.0/6.0 * v0 + 3.0 * vp1 - 1.5 * vp2 + 1.0/3.0 * vp3) / h
    | -1 ->
      fun h ->
        let v0, vp1, vp2, vp3 =
          f x, f (x - h), f (x - 2.0*h), f (x - 3.0*h)
        (1.5 * v0 - 2.0 * vp1 + 0.5 * vp2) / h,
        (11.0/6.0 * v0 - 3.0 * vp1 + 1.5 * vp2 - 1.0/3.0 * vp3) / h
    | _ ->
      fun h ->
        let vm1, vp1, vm2, vp2 =
          f (x - h), f (x + h), f (x - 2.0*h), f (x + 2.0*h)
        (-0.5 * vm1 + 0.5 * vp1) / h,
        (1.0/12.0 * vm2 - 2.0/3.0 * vm1 + 2.0/3.0 * vp1 - 1.0/12.0 * vp2) / h

  let minstep = (sqrt (float (accuracy * maxstep))) * maxstep
  let rec search step =
    let coarse, fine = estimate step
    if step < minstep || abs (coarse - fine) < accuracy then
      fine
    else
      search (step / 2.0)
  search maxstep

let private IntegratePoints =
  [|0.991455371120813, 0.0, 0.022935322010529
    0.949107912342759, 0.129484966168870, 0.063092092629979
    0.864864423359769, 0.0, 0.104790010322250
    0.741531185599394, 0.279705391489277, 0.140653259715525
    0.586087235467691, 0.0, 0.169004726639267
    0.405845151377397, 0.381830050505119, 0.190350578064785
    0.207784955007898, 0.0, 0.204432940075298
    0.0, 0.417959183673469, 0.209482141084728|]

/// Check whether something is actually a valid number.
let inline internal isValid (x : float<'U>) =
  not (Double.IsNaN(float x))

/// Find a numerical integral.
let integrate (accuracy : float<'Val * 'Arg>) (f : float<'Arg> -> float<'Val>) (a : float<'Arg>) (b : float<'Arg>) : float<'Val * 'Arg> =
  let maxstep = abs (b - a) / 2.0
  let minstep = (sqrt (float (accuracy / maxstep))) * maxstep
  let rec interval (a : float<'Arg>) (b : float<'Arg>) =
    let step = (b - a) / 2.0
    if abs step >= minstep then
      let c = (a + b) / 2.0
      let mutable v0, v1 = 0.0<_>, 0.0<_>
      for x, w0, w1 in IntegratePoints do
        do
          let v = f (c + x * step)
          v0 <- v0 + w0 * v
          v1 <- v1 + w1 * v
        if x > 0.0 then
          let v = f (c - x * step)
          v0 <- v0 + w0 * v
          v1 <- v1 + w1 * v
      if abs step < minstep || abs (v0 - v1) < accuracy / step then
        if isValid v1 then
          v1 * step
        elif isValid v0 then
          v0 * step
        else
          0.0<_>
      else
        (interval a c) + (interval c b)
    else
      0.0<_>
  interval a b
