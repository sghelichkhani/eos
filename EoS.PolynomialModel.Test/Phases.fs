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

module EoS.PolynomialModel.Test.Phases
open System
open System.Xml.Linq
open EoS.Phases
open EoS.Xml

let private phases = XFormatter()
do
  let root =
    XDocument.Load(
      IO.Path.Combine(
        IO.Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location),
        "HHP13.xml"))
      .Root
  for it in root.Elements(XName.EoSPhase) do
    phases.Deserialize<IPhase>(it) |> ignore

let ol = phases.TryGetObject<RegularSolution>("ol").Value
let fo = phases.TryGetObject<IPhase>("fo").Value
let fa = phases.TryGetObject<IPhase>("fa").Value

let mgwa = phases.TryGetObject<IPhase>("mwd").Value
let mgri = phases.TryGetObject<IPhase>("mrw").Value

let gr = phases.TryGetObject<IPhase>("gr").Value

let co = phases.TryGetObject<IPhase>("cor").Value
