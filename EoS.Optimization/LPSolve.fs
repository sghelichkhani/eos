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

/// Bindings to the lp_solve API.
namespace EoS.LPSolve
open System
open System.Runtime.InteropServices

type ConstraintType =
  | LE = 1
  | EQ = 3
  | GE = 2
  | FR = 0

[<Flags>]
type Scaling =
  | NONE = 0
  | EXTREME = 1
  | RANGE = 2
  | MEAN = 3
  | GEOMETRIC = 4
  | CURTISREID = 7
  | QUADRATIC = 8
  | LOGARITHMIC = 16
  | USERWEIGHT = 31
  | POWER2 = 32
  | EQUILIBRATE = 64
  | INTEGERS = 128
  | DYNUPDATE = 256
  | ROWSONLY = 512
  | COLSONLY = 1024

[<Flags>]
type Improvement =
  | NONE = 0
  | SOLUTION = 1
  | DUALFEAS = 2
  | THETAGAP = 4
  | BBSIMPLEX = 8

[<Flags>]
type Pivoting =
  | FIRSTINDEX = 0
  | DANTZIG = 1
  | DEVEX = 2
  | STEEPESTEDGE = 3
  | PRIMALFALLBACK = 4
  | MULTIPLE = 8
  | PARTIAL = 16
  | ADAPTIVE = 32
  | RANDOMIZE = 128
  | AUTOPARTIAL = 512
  | LOOPLEFT = 1024
  | LOOPALTERNATE = 2048
  | HARRISTWOPASS = 4096
  | TRUENORMINIT = 16384

type Verbosity =
  | NEUTRAL = 0
  | CRITICAL = 1
  | SEVERE = 2
  | IMPORTANT = 3
  | NORMAL = 4
  | DETAILED = 5
  | FULL = 6

module internal C =
  [<DllImport("lpsolve55")>]
  extern void* eosmem_new()
  [<DllImport("lpsolve55")>]
  extern void eosmem_destroy(void* pool)
  [<DllImport("lpsolve55")>]
  extern void eosmem_use(void* pool)

  [<DllImport("lpsolve55")>]
  extern void* make_lp(int rows, int cols)
  [<DllImport("lpsolve55")>]
  extern void delete_lp(void* lp)

  [<DllImport("lpsolve55")>]
  extern int get_status(void* lp)
  [<DllImport("lpsolve55")>]
  extern void* get_statustext(void* lp, int status)

  [<DllImport("lpsolve55")>]
  extern int get_Nrows(void* lp)
  [<DllImport("lpsolve55")>]
  extern int get_Ncolumns(void* lp)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool resize_lp(void* lp, int rows, int cols)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool add_constraintex(void* lp, int count, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 1s*))>] float[] values, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 1s*))>] int[] cols, ConstraintType constr_type, float rh)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool add_columnex(void* lp, int count, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 1s*))>] float[] values, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 1s*))>] int[] rows)

  [<DllImport("lpsolve55")>]
  extern float get_mat(void* lp, int row, int col)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_mat(void* lp, int row, int col, float value)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_rowex(void* lp, int row, int count, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 2s*))>] float[] values, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 2s*))>] int[] cols)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_columnex(void* lp, int col, int count, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 2s*))>] float[] values, [<MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 2s*))>] int[] rows)

  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool is_unbounded(void* lp, int col)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_unbounded(void* lp, int col)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_bounds(void* lp, int col, float lower, float upper)

  [<DllImport("lpsolve55")>]
  extern ConstraintType get_constr_type(void* lp, int row)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_constr_type(void* lp, int row, ConstraintType con_type)

  [<DllImport("lpsolve55")>]
  extern float get_rh(void* lp, int row)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_rh(void* lp, int row, float value)

  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool is_maxim(void* lp)
  [<DllImport("lpsolve55")>]
  extern void set_maxim(void* lp)
  [<DllImport("lpsolve55")>]
  extern void set_minim(void* lp)

  [<DllImport("lpsolve55")>]
  extern Scaling get_scaling(void* lp);
  [<DllImport("lpsolve55")>]
  extern void set_scaling(void* lp, Scaling scaling);

  [<DllImport("lpsolve55")>]
  extern Improvement get_improve(void* lp);
  [<DllImport("lpsolve55")>]
  extern void set_improve(void* lp, Improvement improvement);

  [<DllImport("lpsolve55")>]
  extern Pivoting get_pivoting(void* lp);
  [<DllImport("lpsolve55")>]
  extern void set_pivoting(void* lp, Pivoting pivoting);

  [<DllImport("lpsolve55")>]
  extern Verbosity get_verbose(void* lp)
  [<DllImport("lpsolve55")>]
  extern void set_verbose(void* lp, Verbosity verbosity)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_outputfile(void* lp, [<MarshalAs(UnmanagedType.LPStr)>] string path)

  [<DllImport("lpsolve55")>]
  extern void* get_row_name(void* lp, int row)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_row_name(void* lp, int row, [<MarshalAs(UnmanagedType.LPStr)>] string name)

  [<DllImport("lpsolve55")>]
  extern void* get_col_name(void* lp, int col)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool set_col_name(void* lp, int col, [<MarshalAs(UnmanagedType.LPStr)>] string name)

  [<DllImport("lpsolve55")>]
  extern void default_basis(void* lp)
  [<DllImport("lpsolve55")>]
  extern int solve(void* lp)

  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool get_variables(void* lp, [<Out; MarshalAs(UnmanagedType.LPArray)>] float[] values)
  [<DllImport("lpsolve55")>]
  extern float get_objective(void* lp)
  [<DllImport("lpsolve55")>]
  extern [<MarshalAs(UnmanagedType.I1)>] bool get_dual_solution(void* lp, [<Out; MarshalAs(UnmanagedType.LPArray)>] float[] values)

/// lp_solve exception
exception LPSolveException of int * string with
  override this.Message = this.Data1

/// Wrapper for an lp_solve problem descriptor
type Problem<[<Measure>] 'C, [<Measure>] 'O>(rows, cols) =
  inherit MarshalByRefObject()

  let mutable pool =
    C.eosmem_new()
  let mutable lp =
    C.eosmem_use(pool)
    C.make_lp(rows, cols)

  let ensure ok =
    if not ok then
      let status = C.get_status(lp)
      let message = Marshal.PtrToStringAnsi(C.get_statustext(lp, status))
      LPSolveException (status, message) |> raise

  let check status =
    if status <> 0 then
      let message = Marshal.PtrToStringAnsi(C.get_statustext(lp, status))
      LPSolveException (status, message) |> raise

  /// Number of rows in the problem.
  member this.NRows =
    C.get_Nrows(lp)

  /// Number of columns in the problem.
  member this.NColumns =
    C.get_Ncolumns(lp)

  /// Change the size of the problem.
  member this.Resize(rows, cols) =
    let rows0, cols0 = C.get_Nrows(lp), C.get_Ncolumns(lp)
    C.eosmem_use(pool)
    ensure (C.resize_lp(lp, rows, cols))
    if rows > rows0 then
      for row in rows0 .. rows-1 do
        ensure (C.add_constraintex(lp, 0, null, null, ConstraintType.FR, 0.0))
    if cols > cols0 then
      for col in cols0 .. cols-1 do
        ensure (C.add_columnex(lp, 0, null, null))

  /// Accessor for problem matrix entries.
  member this.Item
    with get(row, col) : float<'C> =
      unbox(box(C.get_mat(lp, row+1, col+1)))
    and  set(row, col) (v:float<'C>) =
      C.eosmem_use(pool)
      ensure (C.set_mat(lp, row+1, col+1, float v))

  /// Bulk row setter.
  member this.SetRow(row, values:(int * float<'C>)[]) =
    let count, cols, values =
      Array.length values,
      Array.map (fun (col, _) -> col + 1) values,
      Array.map (fun (_, v) -> float v) values
    C.eosmem_use(pool)
    ensure (C.set_rowex(lp, row+1, count, values, cols))

  /// Bulk column setter.
  member this.SetColumn(col, values) =
    let count, rows, values =
      Array.length values,
      Array.map (fun (row, _) -> row + 1) values,
      Array.map snd values
    C.eosmem_use(pool)
    ensure (C.set_columnex(lp, col+1, count, values, rows))

  /// Check whether a variable is unbounded.
  member this.IsUnbounded(col) =
    C.is_unbounded(lp, col+1)

  /// Make a variable unbounded.
  member this.SetUnbounded(col) =
    C.eosmem_use(pool)
    ensure (C.set_unbounded(lp, col+1))

  /// Set bounds for a variable.
  member this.SetBounds(col, lower, upper) =
    C.eosmem_use(pool)
    ensure (C.set_bounds(lp, col+1, lower, upper))

  /// Accessor for variable constraints.
  member this.Constraint
    with get(row) =
      C.get_constr_type(lp, row+1),
      C.get_rh(lp, row+1)
    and  set(row) (t, v) =
      C.eosmem_use(pool)
      ensure (C.set_constr_type(lp, row+1, t))
      ensure (C.set_rh(lp, row+1, v))

  /// Accessor for objective coefficients.
  member this.Objective
    with get(col) : float<'O> =
      unbox(box(C.get_mat(lp, 0, col+1)))
    and  set(col) (v:float<'O>) =
      C.eosmem_use(pool)
      ensure (C.set_mat(lp, 0, col+1, float v))

  /// Whether the objective function should be maximized.
  member this.Maximize
    with get() =
      C.is_maxim(lp)
    and  set v =
      C.eosmem_use(pool)
      if v then
        C.set_maxim(lp)
      else
        C.set_minim(lp)

  /// Whether the objective function should be minimized.
  member this.Minimize
    with get() =
      not (C.is_maxim(lp))
    and  set v =
      C.eosmem_use(pool)
      if v then
        C.set_minim(lp)
      else
        C.set_maxim(lp)

  /// Scaling algorithm flags.
  member this.Scaling
    with get() =
      C.get_scaling(lp)
    and  set v =
      C.eosmem_use(pool)
      C.set_scaling(lp, v)

  /// Iterative improvement level flags.
  member this.Improvement
    with get() =
      C.get_improve(lp)
    and  set v =
      C.eosmem_use(pool)
      C.set_improve(lp, v)

  /// Pivot rule and mode flags.
  member this.Pivoting
    with get() =
      C.get_pivoting(lp)
    and  set v =
      C.eosmem_use(pool)
      C.set_pivoting(lp, v)

  /// Verbosity level of messages.
  member this.Verbosity
    with get() =
      C.get_verbose(lp)
    and  set v =
      C.eosmem_use(pool)
      C.set_verbose(lp, v)

  /// Set output file for messages. If no path is specified, direct
  /// output to the console or standard error device.
  member this.SetOutputFile(?path) =
    let path =
      match path with
      | Some path ->
        path
      | None ->
        match Environment.OSVersion.Platform with
        | PlatformID.Unix | PlatformID.MacOSX -> "/dev/stderr"
        | _ -> "CON:"
    C.eosmem_use(pool)
    ensure (C.set_outputfile(lp, path))

  /// Optional row labels.
  member this.RowName
    with get(row) =
      C.eosmem_use(pool)
      Marshal.PtrToStringAnsi(C.get_row_name(lp, row+1))
    and  set(row) v =
      C.eosmem_use(pool)
      ensure (C.set_row_name(lp, row+1, v))

  /// Optional column labels.
  member this.ColumnName
    with get(col) =
      C.eosmem_use(pool)
      Marshal.PtrToStringAnsi(C.get_col_name(lp, col+1))
    and  set(col) v =
      C.eosmem_use(pool)
      ensure (C.set_col_name(lp, col+1, v))

  /// Solve the linear programming problem, return the variables,
  /// objective value and dual constraint variables.
  member this.Solve() : float[] * float<'O> * float<'O>[] =
    C.eosmem_use(pool)
    C.default_basis(lp)
    check (C.solve(lp))
    let solution = Array.zeroCreate this.NColumns
    ensure (C.get_variables(lp, solution))
    let duals = Array.zeroCreate (1 + this.NRows + this.NColumns)
    ensure (C.get_dual_solution(lp, duals))
    // Dual result variables are omitted: duals[this.NRows + 1 .. this.NRows + this.NColumns]
    solution, unbox(box(C.get_objective(lp))), unbox(box(duals[1 .. this.NRows]))

  abstract Dispose : disposing:bool -> unit
  default this.Dispose(disposing) =
    if lp <> 0n then
      C.eosmem_use(pool)
      C.delete_lp(lp)
      lp <- 0n
    if pool <> 0n then
      C.eosmem_destroy(pool)
      pool <- 0n
      C.eosmem_use(0n)

  override this.Finalize() =
    this.Dispose(false)
    base.Finalize()

  interface IDisposable with
    member this.Dispose() =
      this.Dispose(true)
      GC.SuppressFinalize(this)
