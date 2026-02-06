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

/// Basic MPI parallelization support.
namespace EoS.MPI
open System
open System.Runtime.InteropServices
open Murphy.ByteString

module internal C =
  [<Literal>]
  let SUCCESS = 0

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Error_string")>]
  extern int Error_string(
    int errorcode,
    [<Out; MarshalAs(UnmanagedType.LPStr(*, SizeParamIndex = 2s*))>] Text.StringBuilder result,
    [<In; Out>] int& resultlen)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Comm_create_world")>]
  extern int Comm_create_world(
    [<Out>] nativeint& newcomm)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Comm_size")>]
  extern int Comm_size(
    nativeint comm, [<Out>] int& size)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Comm_rank")>]
  extern int Comm_rank(
    nativeint comm, [<Out>] int& rank)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Abort")>]
  extern int Abort(
    nativeint comm, int errorcode)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Barrier")>]
  extern int Barrier(
    nativeint comm)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Send")>]
  extern int Send(
    nativeint comm,
    [<In; MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 2s*))>] byte[] buffer, int length,
    int dest, int tag)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Recv_begin")>]
  extern int Recv_begin(
    nativeint comm, [<Out>] nativeint& message, [<Out>] int& length,
    [<In; Out>] int& source, [<In; Out>] int& tag)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Recv_end")>]
  extern int Recv_end(
    [<In; Out>] nativeint& message,
    [<Out; MarshalAs(UnmanagedType.LPArray(*, SizeParamIndex = 2s*))>] byte[] buffer, int length,
    int source, int tag)

  [<DllImport("mpiglue", EntryPoint = "MPIGlue_Comm_free")>]
  extern int Comm_free(
    [<In; Out>] nativeint& comm)

/// MPI operation error.
type MPIException(code : int) =
  inherit Exception(
    let message = Text.StringBuilder(512)
    let mutable messagelen = message.Capacity
    if C.Error_string(code, message, &messagelen) = C.SUCCESS then
      message.ToString(0, messagelen)
    else
      "MPI error")

  /// MPI error code.
  member this.Code = code

/// MPI communicator.
type Communicator private (comm : nativeint) =
  inherit MarshalByRefObject()

  static let check errorcode =
    if errorcode <> C.SUCCESS then raise (MPIException errorcode)

  static let hashtag (typ : Type) =
    use md5 = Security.Cryptography.MD5.Create()
    let buf = md5.ComputeHash(Text.Encoding.UTF8.GetBytes typ.AssemblyQualifiedName)
    ((int buf[0] &&& 0x7F) <<< 8) ||| (int buf[1])

  let mutable comm = comm

  /// Create an MPI world communicator or a dummy instance.
  static member Create() =
    let mutable comm = 0n
    try C.Comm_create_world(&comm) |> check with
    | :? DllNotFoundException as err ->
      eprintfn "# MPI support DLL not found: %s" err.Message
    | :? MPIException as err ->
      eprintfn "# MPI initialization failed: %s (%d)" err.Message err.Code
    new Communicator(comm)

  /// Serialization formatter for transmitted data.
  member val BareContext =
    Bare.Context(ByteString.DefaultBareContext)

  /// Number of processes connected through the communicator.
  member this.Size =
    if comm <> 0n then
      let mutable size = 0
      C.Comm_size(comm, &size) |> check
      size
    else
      1

  /// Rank of this process within the communicator.
  member this.Rank =
    if comm <> 0n then
      let mutable rank = 0
      C.Comm_rank(comm, &rank) |> check
      rank
    else
      0

  /// Abort all processes and return the error code.
  member this.Abort(errorcode) =
    if comm <> 0n then
      C.Abort(comm, errorcode) |> check
    else
      Environment.Exit(errorcode)
    errorcode

  /// Block until all processes have reached the barrier.
  member this.Barrier() =
    if comm <> 0n then
      C.Barrier(comm) |> check

  /// Send data to another process.
  member this.Send<'a>(obj:'a, dest:int, ?tag:int) : unit =
    if comm <> 0n then
      let tag = defaultArg tag (hashtag typeof<'a>)
      let buffer = ByteString.FromBareMessage<'a>(obj, context = this.BareContext)
      C.Send(comm, buffer.UnsafeBuffer, buffer.Length, dest, tag) |> check
    else
      invalidOp "MPI support is not available"

  /// Receive data from another process, return source and tag of
  /// the incoming message.
  member this.Receive<'T>(?source:int, ?tag:int) : 'T * int * int =
    if comm <> 0n then
      let mutable source = defaultArg source -1
      let mutable tag = defaultArg tag (hashtag typeof<'T>)
      let mutable message = 0n
      let mutable length = 0
      C.Recv_begin(comm, &message, &length, &source, &tag) |> check
      let buffer = ByteString.create length 0uy
      C.Recv_end(&message, buffer.UnsafeBuffer, buffer.Length, source, tag) |> check
      let obj = buffer.ToBareMessage<'T>(context = this.BareContext)
      obj, source, tag
    else
      invalidOp "MPI support is not available"

  abstract Dispose : disposing:bool -> unit
  default this.Dispose(disposing) =
    if comm <> 0n then
      C.Comm_free(&comm) |> check

  override this.Finalize() =
    this.Dispose(false)
    base.Finalize()

  interface IDisposable with
    member this.Dispose() =
      this.Dispose(true)
      GC.SuppressFinalize(this)
