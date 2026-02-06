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

/// Runtime unit conversion support.
namespace EoS.Units
open System
open System.Runtime.InteropServices

module internal C =
  [<Literal>]
  let UT_UTF8 = 2

  [<DllImport("udunits2")>]
  extern int ut_get_status()

  [<DllImport("udunits2")>]
  extern void* ut_new_system()

  [<DllImport("udunits2")>]
  extern void* ut_read_xml(
    [<In; MarshalAs(UnmanagedType.LPStr)>] string path)

  [<DllImport("udunits2")>]
  extern void ut_free_system(
    void* system)

  [<DllImport("udunits2", CharSet = CharSet.Unicode)>]
  extern void* ut_parse(
    void* system,
    [<In; MarshalAs(UnmanagedType.LPStr)>] string s,
    int encoding)

  [<DllImport("udunits2", CharSet = CharSet.Unicode)>]
  extern int ut_format(
    void* ut,
    [<Out; MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2s)>] byte[] s,
    unativeint length,
    [<MarshalAs(UnmanagedType.U4)>] int options)

  [<DllImport("udunits2")>]
  extern int ut_compare(
    void* ut1, void* ut2)

  [<DllImport("udunits2")>]
  extern void ut_free(
    void* ut)

  [<DllImport("udunits2")>]
  extern void* ut_get_converter(
    void* from_ut, void* to_ut)

  [<DllImport("udunits2", CharSet = CharSet.Unicode)>]
  extern int cv_get_expression(
    void* cv,
    [<Out; MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2s)>] byte[] s,
    unativeint length,
    [<In; MarshalAs(UnmanagedType.LPStr)>] string variable)

  [<DllImport("udunits2")>]
  extern float cv_convert_double(
    void* cv, float v)

  [<DllImport("udunits2")>]
  extern void cv_free(
    void* cv)

/// UDUnits2 operation error.
exception UnitsException of int with
  /// Exception representint the last status of UDUnits2 operations.
  static member LastException =
    UnitsException (C.ut_get_status())

/// Unit formatting options.
[<Flags>]
type UnitFormat =
  | Symbols = 0
  | Names = 4
  | Definition = 8

/// Handle for a unit of measure.
type Unit internal (ut : nativeint) =
  inherit Object()

  member internal this.Handle = ut

  /// Format the unit as a string.
  member this.ToString(options : UnitFormat) =
    let options = C.UT_UTF8 ||| int options
    let buf = Array.zeroCreate 4096
    let len = C.ut_format(ut, buf, unativeint buf.Length, options)
    if len < 0 then raise UnitsException.LastException
    Text.Encoding.UTF8.GetString(buf, 0, len)

  override this.ToString() =
    this.ToString(UnitFormat.Symbols)

  /// Compare two units.
  member this.CompareTo(that : Unit) =
    C.ut_compare(this.Handle, that.Handle)

  interface IComparable<Unit> with
    member this.CompareTo(that) = this.CompareTo(that)

  interface IComparable with
    member this.CompareTo(that) =
      match that with
      | :? Unit as that -> this.CompareTo(that)
      | _ -> invalidArg (nameof that) "Expected a unit of measure for comparison"

  override this.Equals(that) =
    match that with
    | :? Unit as that -> this.CompareTo(that) = 0
    | _ -> false

  override this.GetHashCode() =
    this.ToString(UnitFormat.Definition).GetHashCode()

  override this.Finalize() =
    C.ut_free(ut)
    base.Finalize()

/// Handle for a unit converter.
type UnitConverter private (cv : nativeint) =
  inherit Object()

  /// Construct a new converter from a source and target unit.
  new (source : Unit, target : Unit) =
    let cv = C.ut_get_converter(source.Handle, target.Handle)
    if cv = 0n then raise UnitsException.LastException
    UnitConverter(cv)

  /// Format the conversion function as an arithmetic expression with
  /// the given variable name.
  member this.ToString(variable) =
    let buf = Array.zeroCreate 4096
    let len = C.cv_get_expression(cv, buf, unativeint buf.Length, variable)
    if len < 0 then raise UnitsException.LastException
    Text.Encoding.UTF8.GetString(buf, 0, len)

  /// Convert a value.
  member this.Convert(v) =
    C.cv_convert_double(cv, v)

  override this.ToString() =
    this.ToString("x")

  override this.Finalize() =
    C.cv_free(cv)
    base.Finalize()

/// Handle for a unit system.
type UnitSystem private (system : nativeint) =
  inherit Object()

  /// Create a new empty unit system.
  new () =
    let system = C.ut_new_system()
    if system = 0n then raise UnitsException.LastException
    UnitSystem(system)

  /// Load a new unit system from a file.
  static member Load(?path) =
    let path = defaultArg path null
    let system = C.ut_read_xml(path)
    if system = 0n then raise UnitsException.LastException
    UnitSystem(system)

  /// Create a new unit object from a string representation.
  member this.Unit(s) =
    let ut = C.ut_parse(system, s, C.UT_UTF8)
    if ut = 0n then raise UnitsException.LastException
    Unit(ut)

  override this.Finalize() =
    C.ut_free_system(system)
    base.Finalize()
