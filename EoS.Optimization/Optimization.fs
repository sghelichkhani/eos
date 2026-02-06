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

/// Gibbs energy minimization tools.
namespace EoS.Optimization
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols
open EoS.Chemistry
open EoS.Phases
open EoS.LPSolve

/// A representation of optimization parameters and a candidate
/// set.
type OptimizationContext(p : float<Pa>, T : float<K>,
                         bulk : seq<Formula * float>,
                         MinCompositionSteps : int,
                         MaxRefinementLevels : int,
                         KeepAllCandidates : bool,
                         RelaxBulkMatching : bool) =
  let bulk =
    let bulk' = Collections.Generic.SortedList(256)
    
    for formula, count in bulk do
      formula.FlattenTo(bulk', count)

    bulk'
    |> Seq.choose (fun (KeyValue (element, count)) ->
      if count <= 0.0 then Some element else None)
    |> List.ofSeq
    |> List.iter (fun element ->
      bulk'.Remove(element) |> ignore)

    bulk'

  let mutable phases : option<PhaseCollection> = None
  let candidates = Collections.Generic.LinkedList<Candidate>()

  /// Current context pressure.
  member this.Pressure = p

  /// Current context temperature.
  member this.Temperature = T

  /// Sequence view on the current context bulk composition.
  member this.BulkElements = Seq.map (|KeyValue|) bulk

  /// Number of elements in the bulk composition.
  member this.BulkElementCount = bulk.Count

  /// Bulk composition index lookup.
  member this.BulkElementIndex(element) =
    bulk.IndexOfKey(element)

  /// Bulk composition accessor.
  member this.BulkElementNumber(element) =
    match bulk.TryGetValue(element) with
    | false, _ -> 0.0
    | true, count -> count

  /// The steps parameter determines the minimum resolution of
  /// composition approximations.
  member this.MinCompositionSteps = MinCompositionSteps

  /// The steps at maximum composition grid resolution.
  member this.MaxCompositionSteps = MinCompositionSteps <<< MaxRefinementLevels

  /// The levels parameter determines the maximum resolution of
  /// composition approximations. The maximum resolution of the sparse
  /// composition grids is 1.0 / double(MinCompositionSteps <<<
  /// MaxRefinementLevels).
  member this.MaxRefinementLevels = MaxRefinementLevels

  /// Whether all pseudocompounds should be kept for later iterations
  /// of the linear problem solver, even if they are not picked as
  /// potentially stable phases.
  member this.KeepAllCandidates = KeepAllCandidates

  /// Whether the bulk composition is only used as an upper limit for
  /// used components rather than matched exactly.
  member this.RelaxBulkMatching = RelaxBulkMatching

  /// The backing phase collection.
  member this.Phases
    with get () =
      match phases with
      | None -> invalidOp "Phase collection has not been set"
      | Some v -> v
    and  set (v : PhaseCollection) =
      match phases with
      | Some _ -> invalidOp "Phase collection has already been set"
      | None ->
        candidates.Clear()
        for item in v do
          if item.XLength > 1 then
            let mesh = CandidateMesh(this, item)
            for i in 0 .. item.XLength-1 do
              let c : Candidate =
                fun j -> if i = j then 1.0 else 0.0
                |> Array.init item.XLength
                |> mesh.Candidate
              if c.HasBulkCompatibleComposition then
                c.Keep <- true
                candidates.AddLast(c) |> ignore
          else
            let c = Candidate(this, item)
            if c.HasBulkCompatibleComposition then
              c.Keep <- true
              candidates.AddLast(c) |> ignore
        phases <- Some v

  /// The length of composition vectors for the underlying phase
  /// collection.
  member this.XLength =
    match phases with
    | Some phases -> phases.XLength
    | None -> 0
  
  /// Candidates currently available for optimization. Manual
  /// modification of this list, while possible, is not advisable
  /// under normal circumstances. The implementation of
  /// OptimizationContext expects all candidates for a single phase to
  /// appear as a contiguous sequence; no candidate may appear more
  /// than once.
  member this.Candidates = candidates

  /// Create an LPsolve object to handle the problem represented by
  /// this context. The resulting object is only minimally
  /// initialized. Call InitializeConstraints and/or InitializeColumns
  /// to setup the problem completely.
  member this.CreateLinearProblem() =
    let problem =
      new Problem<1, J/mol>(
        this.BulkElementCount, candidates.Count,
        Minimize = true,
        Scaling = (Scaling.GEOMETRIC ||| Scaling.EQUILIBRATE),
        Improvement = (Improvement.DUALFEAS ||| Improvement.THETAGAP),
        Pivoting = (Pivoting.DEVEX ||| Pivoting.ADAPTIVE))

    problem.SetOutputFile()
    #if DEBUG
    problem.Verbosity <- Verbosity.DETAILED
    #else
    problem.Verbosity <- Verbosity.SEVERE
    #endif

    problem

  /// Transfer the bulk composition information from this context into
  /// the constraints on an LPsolve object.
  member this.InitializeConstraints(problem : Problem<1, J/mol>) =
    this.BulkElements
    |> Seq.iteri (fun row (element, count) ->
      #if DEBUG
      problem.RowName(row) <- element.Name
      #endif
      let op = if RelaxBulkMatching then ConstraintType.LE else ConstraintType.EQ
      problem.Constraint(row) <- (op, count))

  /// Transfer the composition and Gibbs energy information for the
  /// current candidates in this context into the constraints and
  /// objective function of an LPsolve object.
  member this.InitializeColumns(problem : Problem<1, J/mol>) =
    if problem.NRows <> this.BulkElementCount then
      invalidArg (nameof problem) "Problem has wrong number of rows"
    if problem.NColumns <> candidates.Count then
      problem.Resize(problem.NRows, candidates.Count)

    candidates
    |> Seq.iteri (fun col candidate ->
      #if DEBUG
      problem.ColumnName(col) <- candidate.ToString()
      #endif
      problem.SetColumn(
        col,
        candidate.Elements
        |> Seq.map (fun (element, count) ->
          this.BulkElementIndex(element), count)
        |> Seq.toArray)
      problem.Objective(col) <- candidate.Energy)

  /// Walk through the candidate set and mark every selected candidate
  /// with the keep flag.
  member this.KeepSelectedCandidates(solution : float[]) =
    candidates
    |> Seq.iteri (fun j candidate ->
      if solution[j] > 0.0 then
        candidate.Keep <- true)

  /// Walk through the candidate set and try to optimize compositions
  /// relative to the given reference plane (energies in bulk
  /// composition element order). Add optimized candidates unless they
  /// already exist in the set and remove suboptimal candidates unless
  /// they have their keep flag set. Return true iff the candidate set
  /// was changed.
  member this.AdjustCandidates(plane : float<J/mol>[]) =
    let rec descend (c0 : Candidate) level =
      let better =
        c0.Surrounding(level)
        |> List.filter (fun (c1 : Candidate) ->
          c1.AdjustedEnergy(plane) < c0.AdjustedEnergy(plane))
      if not better.IsEmpty then
        let c1 =  better |> List.minBy (fun c1 -> c1.AdjustedEnergy(plane))
        descend c1 level
      elif level <= this.MaxRefinementLevels then
        descend c0 (level + 1)
      else
        c0

    let isFresh (c0 : Candidate) =
      let rec walk (node : Collections.Generic.LinkedListNode<Candidate>) =
        if Object.ReferenceEquals(node, null) then
          true
        else
          let c1 = node.Value
          if c0.Phase <> c1.Phase then
            true
          elif c0 = c1 then
            false
          else
            walk node.Next
      walk

    let rec walk (anchor : Collections.Generic.LinkedListNode<Candidate>) (node : Collections.Generic.LinkedListNode<Candidate>) modified =
      if not(Object.ReferenceEquals(node, null)) then
        let c0 = node.Value
        if c0.Phase <> anchor.Value.Phase then
          walk node node modified
        else
          #if DEBUG
          eprintfn "Trying to optimize %O with G[adj] = %g J/mol" c0 <|
          c0.AdjustedEnergy(plane) / 1.0<J/mol>
          #endif
          let node, modified =
            let c1 = descend c0 0
            if isFresh c1 anchor then
              #if DEBUG
              eprintf "%O was optimized to %O with G[adj] = %g J/mol," c0 c1 <|
              c1.AdjustedEnergy(plane) / 1.0<J/mol>
              #endif
              if c0.Keep then
                #if DEBUG
                eprintfn " candidate will be added"
                #endif
                candidates.AddAfter(node, c1), true
              else
                #if DEBUG
                eprintfn " candidate will be replaced"
                #endif
                node.Value <- c1
                node, true
            else
              #if DEBUG
              eprintfn "%O was optimized to %O, no significant improvement" c0 c1
              #endif
              node, modified
              
          walk anchor node.Next modified
      else
        modified

    walk candidates.First candidates.First false

  /// Group the solution of a linear problem by candidates and collect
  /// the contributions in a phase collection composition vector.
  member this.CollectComposition(solution : float[]) =
    let x = Array.zeroCreate this.XLength
    candidates
    |> Seq.iteri (fun j candidate ->
      let xoffset = candidate.XOffset
      let sj = solution[j]
      if sj > 0.0 then
        #if DEBUG
        eprintfn "Solution component %d: %g * %O" j sj candidate
        #endif
        match candidate.Composition with
        | null ->
          x[xoffset] <- x[xoffset] + sj
        | xj ->
          Array.iteri (fun i xi ->
            x[xoffset+i] <- x[xoffset+i] + sj * xi) xj)
    x

/// Partially reified mesh of candidates with fast lazy lookup.
and internal CandidateMesh(context : OptimizationContext, item : PhaseCollectionItem) =
  let rec ilog l n =
    if n = 0 then l else ilog (l + 1) (n >>> 1)
  let keybits =
    ilog 1 context.MaxCompositionSteps

  do
    if context.XLength * keybits > 64 then
      raise(NotSupportedException "Implementation restriction: Candidate keys must fit within 64 bits")

  let cache = Collections.Generic.SortedList<int64, Candidate>(context.MinCompositionSteps)

  /// Optimization context.
  member this.Context = context

  /// Represented phase.
  member this.Item = item

  /// Retrieve a candidate by index, generate a new one if necessary.
  member this.Candidate(x : float[]) =
    let key, x =
      let norm = Array.sum x
      let scale = float(context.MaxCompositionSteps)
      let mask = (1L <<< keybits) - 1L
      let x = Array.map (fun xi -> scale * xi / norm) x
      let key = Array.fold (fun acc xi -> (acc <<< keybits) ||| (int64(xi) &&& mask)) 0L x
      for i in 0 .. x.Length-1 do x[i] <- x[i] / scale
      key, x

    match cache.TryGetValue(key) with
    | true, c ->
      c
    | false, _ ->
      let c = new Candidate(this, key, x)
      cache[key] <- c
      c

/// Representation of an optimization candidate for a phase.
and Candidate private (context : OptimizationContext, item : PhaseCollectionItem, mesh_key : option<CandidateMesh * int64>, x : float[]) =
  let formula = item.Phase.Formula(x)
  let elements = lazy formula.Flatten()

  let energy =
    lazy
      try item.Phase.Energy(context.Pressure, context.Temperature, x) with
      | _ -> infinity * 1.0<J/mol>

  /// Initialize a candidate that lives within a mesh.
  internal new (mesh : CandidateMesh, key : int64, x : float[]) =
    Candidate(mesh.Context, mesh.Item, Some (mesh, key), x)

  /// Initialize a free-standing candidate with null composition.
  new (context : OptimizationContext, item : PhaseCollectionItem) =
    Candidate(context, item, None, null)

  /// Initialize a free-standing candidate with specified composition.
  new (context : OptimizationContext, item : PhaseCollectionItem, x : float[]) =
    Candidate(context, item, None, x)

  /// Optimization context.
  member this.Context = context

  /// Represented phase.
  member this.Phase = item.Phase

  /// Composition vector slice offset of represented phase.
  member this.XOffset = item.XOffset

  /// Composition vector slice length of represented phase.
  member this.XLength = item.XLength

  /// Unique index of this candidate in the mesh.
  member internal this.MeshAndKey = mesh_key

  /// Composition for this candidate.
  member this.Composition = x

  /// Sequence view on a flat list of formula elements in the phase at
  /// the current composition.
  member this.Elements = Seq.map (|KeyValue|) elements.Value

  /// Number of elements in the phase at the current composition.
  member this.ElementCount = elements.Value.Count

  /// Element content accessor.
  member this.ElementNumber(element) =
    match elements.Value.TryGetValue(element) with
    | false, _ -> 0.0
    | true, count -> count

  /// Whether the element content of this candidate is compatible with
  /// the current context bulk composition.
  member this.HasBulkCompatibleComposition =
    formula.IsValid &&
    elements.Value
    |> Seq.forall (fun (KeyValue (element, count)) ->
      count <= 0.0 || this.Context.BulkElementNumber(element) > 0.0)

  /// Gibbs energy of the phase at the context pressure and
  /// temperature and at the current composition. Computed lazily and
  /// cached.
  member this.Energy = energy.Value

  /// Gibbs energy of the phase adjusted using a reference plane
  /// (energies in bulk composition energy order).
  member this.AdjustedEnergy(plane : float<J/mol>[]) =
    elements.Value
    |> Seq.fold (fun G (KeyValue (element, count)) ->
      if count > 0.0 then
        G - count * plane[this.Context.BulkElementIndex(element)]
      else
        G) energy.Value

  /// Candidates surrounding this one in the mesh at a given
  /// refinement level. The returned list is already filtered for such
  /// candidates that are compatible with the context bulk
  /// composition.
  member this.Surrounding(level) =
    match mesh_key with
    | Some (mesh, _) ->
      let s = item.XLength - 1
      let d = 1.0 / float(context.MinCompositionSteps <<< level)
      let inRange =
        if item.Phase.AllowsNegativeComponents then
          fun xi -> -1.0 <= xi && xi <= +1.0
        else
          fun xi -> 0.0 <= xi && xi <= +1.0
      [ for i in 0 .. s do
          for j in 0 .. s do
            if i <> j && inRange (x[i] + d) && inRange (x[j] - d) then
              let that =
                mesh.Candidate(Array.mapi (fun k it ->
                  if   k = i then it + d
                  elif k = j then it - d
                  else            it) x)
              if that.HasBulkCompatibleComposition then
                yield that ]
    | None ->
      []

  /// Flag indicating whether the candidate should be kept even if
  /// more optimal compositions are available for the represented
  /// phase.
  member val Keep = context.KeepAllCandidates with get, set

  override this.ToString() =
    if Object.ReferenceEquals(x, null) then
      this.Phase.ToString()
    else
      sprintf "%O[%s]" this.Phase (String.concat "; " (Seq.map string x))

/// Phase collection extensions for Gibbs energy minimization.
[<AutoOpen>]
module PhaseCollectionExtensions =
  type PhaseCollection with
    /// Determine the optimal composition of the collection given a
    /// chemical bulk composition. The minimum composition steps
    /// default to 64, which corresponds to about 1.6 mol% steps along
    /// every compositional axis. The maximum refinement levels
    /// default to 2 yielding a minimum composition step size of about
    /// 0.4 mol% along every axis. The keep all candidates flag
    /// defaults to false. The bulk composition matching relaxation
    /// flag defaults to false.
    member this.Optimize(p : float<Pa>, T : float<K>, bulk : seq<Formula * float>, ?steps, ?levels, ?keep, ?relax) =
      let context =
        OptimizationContext(
          p, T, bulk, Phases = this,
          MinCompositionSteps = defaultArg steps 64,
          MaxRefinementLevels = defaultArg levels 2,
          KeepAllCandidates = defaultArg keep false,
          RelaxBulkMatching = defaultArg relax false)

      use problem = context.CreateLinearProblem()
      context.InitializeConstraints(problem)
      
      let rec loop () =
        context.InitializeColumns(problem)
        let solution, G, plane = problem.Solve()

        #if DEBUG
        let G' =
          context.BulkElements
          |> Seq.mapi (fun i (_, count) ->
            count * plane[i])
          |> Seq.sum
        eprintfn "Energy delta: %g J/mol" ((G - G') / 1.0<J/mol>)
        #endif

        context.KeepSelectedCandidates(solution)

        if context.AdjustCandidates(plane) then
          loop ()
        else
          context.CollectComposition(solution)

      loop ()
