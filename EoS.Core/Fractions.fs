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

/// Conversions between different types of chemical ingredient fractions.
module EoS.Chemistry.Fractions
open EoS.Chemistry

/// Convert molar counts or fractions to mass fractions.
let MolarToMass (x : seq<Formula * float>) =
  let weighted = x |> Seq.map (fun (f, xi) -> xi * f.Mass)
  let norm = ( * ) (1.0 / (Seq.sum weighted))
  weighted |> Seq.map norm |> Seq.zip (Seq.map fst x)

/// Convert mass fractions to molar fractions.
let MassToMolar (x : seq<Formula * float<_>>) =
  let unweighted = x |> Seq.map (fun (f, xi) -> xi / f.Mass)
  let norm = ( * ) (1.0 / (Seq.sum unweighted))
  unweighted |> Seq.map norm |> Seq.zip (Seq.map fst x)

/// Convert molar counts or fractions to atomic fractions.
let MolarToAtomic (x : seq<Formula * float>) =
  let weighted = x |> Seq.map (fun (f, xi) -> xi * f.Atoms)
  let norm = ( * ) (1.0 / (Seq.sum weighted))
  weighted |> Seq.map norm |> Seq.zip (Seq.map fst x)

/// Convert atomic fractions to molar fractions.
let AtomicToMolar (x : seq<Formula * float>) =
  let unweighted = x |> Seq.map (fun (f, xi) -> xi / f.Atoms)
  let norm = ( * ) (1.0 / (Seq.sum unweighted))
  unweighted |> Seq.map norm |> Seq.zip (Seq.map fst x)

/// Convert mass fractions to atomic fractions.
let MassToAtomic x =
  x |> MassToMolar |> MolarToAtomic

/// Convert atomic fractions to mass fractions.
let AtomicToMass x =
  x |> AtomicToMolar |> MolarToMass
