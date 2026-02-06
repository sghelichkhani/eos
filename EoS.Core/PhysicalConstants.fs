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

module EoS.PhysicalConstants
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols

/// The Boltzmann atomic gas constant.
let AtomicGas = 1.3806504e-23<J/K>
/// The molar gas constant.
let MolarGas = 8.314472<J/K/mol>
/// The count of atoms in 1 mol of substance.
let MolarCount = 6.0221367e23<1/mol>
/// The mass of one atomic mass unit.
let AtomicMass = 1.6605402e-27<kg>

/// The Planck action quantum.
let Quantum = 6.6262e-34<J*s>
/// The Dirac constant, derived from the Planck action quantum.
let ReducedQuantum = Quantum / (2.0 * Math.PI)

/// The gravitational constant.
let Gravity = 6.673e-11<m^3/kg/s^2>
