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

namespace EoS.Phases
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS

/// Common interface of all thermoelastic objects.
type IThermoElastic =
  /// The number of components in composition vectors used by this object.
  abstract XLength : int

  /// Compute the molar volume of a thermoelastic object.
  abstract Volume : p:float<Pa> * T:float<K> * x:float[] -> float<m^3/mol>

  /// Compute the density of a thermoelastic object.
  abstract Density : p:float<Pa> * T:float<K> * x:float[] -> float<kg/m^3>

  /// Compute the compressibility of a thermoelastic object at constant temperature.
  abstract Compressibility : p:float<Pa> * T:float<K> * x:float[] -> float<1/Pa>

  /// Compute the thermal expansivity of a thermoelastic object at constant pressure.
  abstract Expansivity : p:float<Pa> * T:float<K> * x:float[] -> float<1/K>

  /// Compute the adiabatic elastic moduli of a thermoelastic object.
  abstract Moduli : p:float<Pa> * T:float<K> * x:float[] -> ĸ:float<Pa> * µ:float<Pa>

  /// Compute the elastic wave velocities of a thermoelastic object.
  abstract Velocities : p:float<Pa> * T:float<K> * x:float[] -> vp:float<m/s> * vs:float<m/s>

  /// Compute the molar Gibbs energy of a thermoelastic object.
  abstract Energy : p:float<Pa> * T:float<K> * x:float[] -> float<J/mol>

  /// Compute the molar entropy of a thermoelastic object.
  abstract Entropy : p:float<Pa> * T:float<K> * x:float[] -> float<J/mol/K>

  /// Compute the molar heat capacity of a thermoelastic object at constant pressure.
  abstract IsobaricHeatCapacity : p:float<Pa> * T:float<K> * x:float[] -> float<J/mol/K>

  /// Compute the molar heat capacity of a thermoelastic object at constant volume.
  abstract IsochoricHeatCapacity : p:float<Pa> * T:float<K> * x:float[] -> float<J/mol/K>

  /// Compute the Grüneisen parameter of a thermoelastic object.
  abstract Grueneisen : p:float<Pa> * T:float<K> * x:float[] -> float

/// Common interface of phases.
type IPhase =
  inherit IThermoElastic

  /// Whether this phase may contain negative molar fractions of some
  /// components. The flag is used by clients such as the optimization
  /// library to decide whether some entries of the composition vector
  /// may become negative as long as the sum of composition vector
  /// entries remains one and the resulting formula for the phase
  /// remains valid.
  abstract AllowsNegativeComponents : bool

  /// Determine the molar mass of a phase.
  abstract Mass : x:float[] -> float<kg/mol>

  /// Determine the number of atoms per formula unit of the phase.
  abstract Atoms : x:float[] -> float

  /// Determine a chemical formula for the phase.
  abstract Formula : x:float[] -> Chemistry.Formula

/// Information about a phase in a collection.
type [<Struct>] PhaseCollectionItem =
  /// The phase in the collection.
  val Phase : IPhase

  /// The position of the composition vector slice for this phase in
  /// composition vectors for the entire collection.
  val XOffset : int

  /// The number of components in composition vectors used by the phase.
  val XLength : int

  /// Extract the correct composition vector slice for the phase from
  /// a collection composition vector.
  member this.XSlice(x : double[]) =
    x[this.XOffset .. this.XOffset + this.XLength - 1]

  new (phase, xoffset) =
    { Phase = phase
      XOffset = xoffset
      XLength = phase.XLength }

/// Extension methods to call IThermoElastic and IPhase methods without
/// a composition parameter and formatted output helpers.
[<AutoOpen>]
module CompositionExtensions =
  type IThermoElastic with
    member inline this.Volume(p, T) = this.Volume(p, T, null)
    member inline this.Density(p, T) = this.Density(p, T, null)
    member inline this.Compressibility(p, T) = this.Compressibility(p, T, null)
    member inline this.Expansivity(p, T) = this.Expansivity(p, T, null)
    member inline this.Moduli(p, T) = this.Moduli(p, T, null)
    member inline this.Velocities(p, T) = this.Velocities(p, T, null)
    member inline this.Energy(p, T) = this.Energy(p, T, null)
    member inline this.Entropy(p, T) = this.Entropy(p, T, null)
    member inline this.IsobaricHeatCapacity(p, T) = this.IsobaricHeatCapacity(p, T, null)
    member inline this.IsochoricHeatCapacity(p, T) = this.IsochoricHeatCapacity(p, T, null)

    /// Print a formatted description of a composition vector.
    member this.PrintX(x, ?out, ?indent, ?prefix) =
      let out = defaultArg out Console.Out
      let indent = defaultArg indent "\t"
      let prefix = defaultArg prefix String.Empty

      let rec walk prefix (thel:IThermoElastic) x =
        let f = if Object.ReferenceEquals(x, null) then 1.0 else Array.sum x
        if f <> 0.0 then
          fprintfn out "%s%g * %O" prefix f thel

          let prefix = prefix + indent
          match thel with
          | :? seq<PhaseCollectionItem> as items ->
            for it in items do
              walk prefix it.Phase (it.XSlice x)
          | _ when thel.XLength > 1 ->
            for i in 0 .. thel.XLength-1 do
              let f = x[i]
              if f <> 0.0 then
                fprintfn out "%s%g * %O[%d]" prefix f thel i
          | _ ->
            ()

      walk prefix this x

  type IPhase with
    member inline this.Mass() = this.Mass(null)
    member inline this.Atoms() = this.Atoms(null)
    member inline this.Formula() = this.Formula(null)
