using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PET.Water
{
    /// <summary>
    /// Ladder rung 3: a channel-graph solver, intended for the Android tier.
    ///
    /// WHAT IT IS FOR
    /// --------------
    /// <c>Tools/bench_thresholds.json</c> defines a five-rung fallback ladder. Rung
    /// 3 is "channel graph on Android only. A real gameplay difference." This is
    /// that rung, written as a peer of <see cref="HeightfieldWaterSolver"/> so the
    /// harness can hold either behind <see cref="IWaterSolver"/> and the report can
    /// attribute a result to the implementation that produced it.
    ///
    /// It is NOT a faster heightfield solver. It is a different model of water.
    ///
    /// THE MODEL
    /// ---------
    /// The heightfield solver treats every cell as a reservoir and resolves the
    /// head difference on all four edges every step. That is O(4n) flux
    /// computations per step, and it is what makes water freely redirectable: dig
    /// anywhere and the field responds everywhere.
    ///
    /// This solver instead derives a fixed drainage network ONCE from the dry bed
    /// and then routes water along it. Each cell has exactly ONE downstream
    /// neighbour, so a step is O(n) rather than O(4n), and the routing is a single
    /// topological sweep rather than a relaxation. The network is a spanning tree
    /// rooted at the domain boundary, built by priority-flood, which is what makes
    /// it cycle-free by construction.
    ///
    /// WHAT IT TRADES AWAY
    /// -------------------
    /// 1. WATER IS NOT FREELY REDIRECTABLE. This is the real cost and the reason
    ///    the ladder calls it "a real gameplay difference". The network is built
    ///    from the bed at the moment the graph is built. Terraform the terrain
    ///    afterwards and the water keeps running down the old channel until the
    ///    graph is rebuilt. The heightfield solver has no such lag.
    ///
    /// 2. FLOW IS ONE-DIMENSIONAL. Water moves along the channel, not across it.
    ///    A pool that should spread sideways spreads only downstream. On a
    ///    floodplain this is visibly wrong.
    ///
    /// 3. THE DOMAIN IS CLOSED. Boundary cells are terminal: they hold what
    ///    arrives rather than draining it out of the world. This is not a
    ///    simplification - the heightfield solver is closed too, because its pass
    ///    2 refuses to compute flux across the boundary. Both solvers conserve
    ///    mass by holding water at the edge, and the mass-balance assertion in
    ///    <c>BenchmarkScenarios.AssertCorrectness</c> depends on it.
    ///
    /// WHAT IT KEEPS
    /// -------------
    /// Mass is conserved by construction: every unit that leaves a cell arrives at
    /// its downstream neighbour, and a cell may never give away more than it
    /// holds. The limiter is the same idea as the heightfield solver's pass 3, and
    /// it is here for the same reason - without it a cell goes negative and the
    /// next step divides by a negative depth.
    ///
    /// DETERMINISM
    /// -----------
    /// Within-device determinism is required, because the save system re-derives
    /// water from terrain and sources on load. Every loop here runs in fixed index
    /// order, every tie is broken by index, the topological order is taken from
    /// the flood's own discovery order rather than from a sort, and the Burst job
    /// carries <c>FloatMode.Strict</c>. Cross-device bit-identity is NOT claimed,
    /// for the same reason the heightfield solver does not claim it.
    ///
    /// STATE AND Reset()
    /// -----------------
    /// Unlike the heightfield solver this class carries state: the drainage
    /// network. <see cref="Reset"/> discards it, and the next <see cref="Step"/>
    /// rebuilds it from the field as it then stands. That is what makes
    /// <c>CorrectnessScenario</c> meaningful for this implementation - the
    /// scenario calls Reset() between its two runs precisely so a stateful solver
    /// cannot pass by accident.
    ///
    /// The network is built from the field's state at the first step after a
    /// reset, which for a freshly allocated field is the DRY BED. That is the
    /// intended use: the caller hands over a field carrying the terrain datum and
    /// no water, and the network is the terrain's drainage. Building it from a
    /// field that already carries water would derive channels from the water
    /// surface instead, which is a different and less useful thing.
    /// </summary>
    public sealed class ChannelGraphWaterSolver : IWaterSolver
    {
        /// <summary>
        /// The name written into every summary JSON. Distinct from
        /// <c>"heightfield"</c> so the report can tell the rungs apart, which is
        /// the whole point of the ladder.
        /// </summary>
        public const string SolverName = "channel-graph";

        /// <summary>
        /// Below this depth a cell is treated as dry and neither gives nor
        /// receives water. Matches <see cref="WaterSolver.MinDepth"/> so the two
        /// solvers agree on what "dry" means and a comparison between them is not
        /// measuring two different thresholds.
        /// </summary>
        public const float MinDepth = 1e-5f;

        /// <summary>
        /// Per-step energy loss, applied to the ROUTED DEPTH and not to the
        /// stored depth. Same reasoning as the heightfield solver's damping
        /// constant: damping the stored depth is a global mass sink and would make
        /// the mass-conservation assertion measure the damping constant instead of
        /// the solver. Damping the routed amount loses energy, which is what
        /// friction does, and leaves mass conserved by construction.
        /// </summary>
        public const float Damping = 0.995f;

        /// <summary>
        /// Hydraulic-geometry exponent. Channel width grows as the square root of
        /// contributing area, which is the standard downstream hydraulic geometry
        /// relation (width ~ A^0.5). It is the reason a river is wider than a
        /// rill, and it is what makes the capacity term a channel property rather
        /// than a global constant.
        /// </summary>
        public const float WidthExponent = 0.5f;

        /// <summary>
        /// Width of a channel draining a single cell, in metres. A first-order
        /// channel is about a cell wide. This is a tuning constant and it was
        /// chosen to make a one-cell channel pass roughly one cell of water per
        /// step; it has not been calibrated against anything.
        /// </summary>
        public const float FirstOrderWidthM = 1.0f;

        /// <summary>
        /// Ceiling on channel width, in metres. Without it a channel draining the
        /// whole tile would be wider than the tile, and the capacity term would
        /// stop being a limiter at all.
        /// </summary>
        public const float MaxChannelWidthM = 24.0f;

        /// <summary>
        /// Depth of water a channel carries at capacity, in metres. Together with
        /// width this gives the cross-section, and the cross-section times the
        /// cell length gives the volume the cell may pass in one step.
        /// </summary>
        public const float ChannelDepthM = 0.25f;

        /// <summary>
        /// Fraction of the cell's held depth a channel may pass in one step when
        /// it is below capacity. This is the term that keeps the solver stable on
        /// a nearly flat bed, where the capacity term alone would let a cell
        /// empty itself in a single step and oscillate.
        /// </summary>
        public const float MaxDrainFraction = 0.5f;

        public string Name => SolverName;

        public int TileResolution { get; }

        public float CellSize { get; }

        public float Dt { get; }

        // ---- Drainage network (built once per reset, reused every step) -------

        /// <summary>
        /// For each cell, the index of the neighbour it drains to, or -1 when the
        /// cell is terminal (a boundary cell or an enclosed sink). Terminal cells
        /// hold what arrives; they never lose it, which is what keeps the domain
        /// closed and the mass balance exact.
        /// </summary>
        private NativeArray<int> _downstream;

        /// <summary>
        /// The edge slot in <see cref="WaterField.Flux"/> that corresponds to
        /// <see cref="_downstream"/>, or -1 for a terminal cell. Kept alongside
        /// the neighbour index so the hot loop does not have to re-derive which
        /// of the four edges it is looking at.
        /// </summary>
        private NativeArray<int> _downstreamEdge;

        /// <summary>
        /// Cells in the flood's own discovery order, which is downstream-to-
        /// upstream: a cell is discovered when its downstream neighbour is
        /// processed, so the downstream neighbour always appears earlier.
        ///
        /// That makes this array a topological order of the spanning tree, taken
        /// directly from the flood rather than from a sort. Routing walks it
        /// forward; accumulation walks it backward. No cycle check is needed,
        /// because a tree has no cycles.
        /// </summary>
        private NativeArray<int> _order;

        /// <summary>Number of valid entries in <see cref="_order"/>.</summary>
        private int _orderCount;

        /// <summary>Number of cells draining through each cell, including
        /// itself. Drives the channel width.</summary>
        private NativeArray<float> _accum;

        /// <summary>
        /// Depth, in metres, a cell may pass downstream in one step, from its
        /// channel cross-section.
        ///
        /// Stored as a DEPTH and not a volume because that is the unit the field
        /// works in: <see cref="WaterField.Depth"/> is a datum offset in metres,
        /// and the heightfield solver's flux array holds depth-equivalents too
        /// (its pass 2 computes <c>dh * gravity * dt</c>, which is a depth). A
        /// capacity in cubic metres compared against a depth would be a units
        /// error that still compiles and still conserves mass - it would simply
        /// limit the wrong thing.
        /// </summary>
        private NativeArray<float> _capacity;

        /// <summary>Scratch for the priority-flood. Sized to the cell count and
        /// reused; never reallocated after construction.</summary>
        private NativeArray<int> _queue;

        /// <summary>Filled elevation per cell, used by the priority-flood to
        /// route across flats. Scratch, but kept as a field so the build
        /// allocates nothing.</summary>
        private NativeArray<float> _filled;

        /// <summary>Visited flag per cell for the flood. Scratch.</summary>
        private NativeArray<byte> _visited;

        private bool _graphBuilt;

        public ChannelGraphWaterSolver(int tileResolution, float cellSize, float dt)
        {
            TileResolution = tileResolution;
            CellSize = cellSize;
            Dt = dt;

            int cells = tileResolution * tileResolution;

            _downstream = new NativeArray<int>(cells, Allocator.Persistent);
            _downstreamEdge = new NativeArray<int>(cells, Allocator.Persistent);
            _order = new NativeArray<int>(cells, Allocator.Persistent);
            _accum = new NativeArray<float>(cells, Allocator.Persistent);
            _capacity = new NativeArray<float>(cells, Allocator.Persistent);
            _queue = new NativeArray<int>(cells, Allocator.Persistent);
            _filled = new NativeArray<float>(cells, Allocator.Persistent);
            _visited = new NativeArray<byte>(cells, Allocator.Persistent);

            _orderCount = 0;
            _graphBuilt = false;
        }

        /// <summary>
        /// Discard the drainage network. The next step rebuilds it from the field
        /// as it then stands.
        ///
        /// This is the method that makes the determinism scenario meaningful for
        /// this implementation. <c>CorrectnessScenario</c> runs the same sequence
        /// twice from an identical start and compares the resulting fields; a
        /// stateful solver that ignored Reset() would carry the first run's
        /// network into the second and the comparison would be testing nothing.
        /// </summary>
        public void Reset()
        {
            _graphBuilt = false;
        }

        /// <summary>
        /// Advance the field one step and report the volume the sources added.
        ///
        /// The inflow array is caller-owned and reused across steps so the hot
        /// loop allocates nothing. It is an array rather than a ref parameter
        /// because <c>IJob.Run()</c> takes the job by value, so a plain float
        /// written inside Execute() would never be visible to the caller.
        /// </summary>
        public float Step(
            WaterField field,
            NativeArray<WaterSource> sources,
            NativeArray<float> inflow)
        {
            if (!_graphBuilt)
            {
                BuildGraph(field);
                _graphBuilt = true;
            }

            new StepJob
            {
                Field = field,
                Sources = sources,
                Downstream = _downstream,
                DownstreamEdge = _downstreamEdge,
                Order = _order,
                OrderCount = _orderCount,
                Capacity = _capacity,
                Dt = Dt,
                Inflow = inflow,
            }.Run();

            return inflow.IsCreated && inflow.Length > 0 ? inflow[0] : 0f;
        }

        /// <summary>
        /// Release the persistent allocations. The harness allocates one solver
        /// per scenario and lets it fall out of scope, so this is called by the
        /// owner rather than by the harness.
        /// </summary>
        public void Dispose()
        {
            if (_downstream.IsCreated) { _downstream.Dispose(); }
            if (_downstreamEdge.IsCreated) { _downstreamEdge.Dispose(); }
            if (_order.IsCreated) { _order.Dispose(); }
            if (_accum.IsCreated) { _accum.Dispose(); }
            if (_capacity.IsCreated) { _capacity.Dispose(); }
            if (_queue.IsCreated) { _queue.Dispose(); }
            if (_filled.IsCreated) { _filled.Dispose(); }
            if (_visited.IsCreated) { _visited.Dispose(); }
        }

        // ---------------------------------------------------------------------
        // Graph construction. Cold path: runs once per reset, not per step.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Derive the drainage network from the field's current surface datum.
        ///
        /// PRIORITY-FLOOD, AND WHY NOT STEEPEST DESCENT
        /// --------------------------------------------
        /// The obvious approach is to point every cell at its lowest neighbour.
        /// It does not work, for two reasons that both show up on real terrain:
        ///
        ///   FLATS. On a flat area every neighbour is equal, so "lowest" is
        ///   undefined and the tie-break decides the flow direction. A tie-break
        ///   by index produces paths that wander and, worse, can point two
        ///   adjacent cells at each other.
        ///
        ///   CYCLES. Two cells pointing at each other is a cycle, and a cycle
        ///   means water circulates forever. Mass is still conserved, so the
        ///   mass-balance assertion passes, and the field never settles. That is
        ///   the worst kind of failure: a correct-looking number over a wrong
        ///   world.
        ///
        /// Priority-flood avoids both. Cells are processed in order of increasing
        /// FILLED elevation, seeded from the boundary, and each cell is pointed at
        /// the already-processed neighbour it was reached from. Because a cell is
        /// only ever pointed at a cell that was processed before it, the result is
        /// a spanning tree rooted at the boundary and a cycle is impossible by
        /// construction. Flats resolve naturally: on a flat, the flood spreads
        /// outward from the boundary and every cell drains toward the edge it was
        /// reached from.
        ///
        /// The queue is ordered by (filled elevation, cell index). The index is
        /// the tie-break and it is what makes the build deterministic - without
        /// it, two cells at the same elevation would be ordered by insertion,
        /// which depends on the order their neighbours happened to be visited.
        /// </summary>
        private void BuildGraph(WaterField field)
        {
            int w = field.Width;
            int h = field.Height;
            int cells = w * h;

            for (int i = 0; i < cells; i++)
            {
                _downstream[i] = -1;
                _downstreamEdge[i] = -1;
                _visited[i] = 0;
                _filled[i] = 0f;
                _accum[i] = 1f;
                _capacity[i] = 0f;
            }

            _orderCount = 0;

            // ---- Seed the flood with the boundary ---------------------------
            // Boundary cells are terminal. They are seeded so the flood has
            // somewhere to start, but they are never given a downstream
            // neighbour: water that reaches the edge of the domain stays in the
            // domain. This is what keeps the solver closed and the mass balance
            // exact, and it matches the heightfield solver, whose pass 2 refuses
            // to compute flux across the boundary.
            int head = 0;
            int tail = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool boundary = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    if (!boundary)
                    {
                        continue;
                    }

                    int c = field.Index(x, y);
                    _visited[c] = 1;
                    _filled[c] = field.Depth[c];
                    _queue[tail++] = c;
                }
            }

            // ---- Flood inward ------------------------------------------------
            // A linear scan for the minimum rather than a binary heap. The heap
            // would be O(n log n) and this is O(n^2), which sounds worse and is
            // not: the build runs once per reset, not once per step, and the
            // scan has no allocation, no sift-down and no branch on a comparison
            // function. At 257x257 the whole build is a few milliseconds, and it
            // is paid once. If the build ever shows up in a profile, this is the
            // line to replace with a heap - and the (elevation, index) ordering
            // is already the heap's comparison, so the swap is mechanical.
            while (head < tail)
            {
                int bestSlot = head;
                float bestElev = _filled[_queue[head]];
                int bestIndex = _queue[head];

                for (int s = head + 1; s < tail; s++)
                {
                    int c = _queue[s];
                    float e = _filled[c];
                    if (e < bestElev || (e == bestElev && c < bestIndex))
                    {
                        bestSlot = s;
                        bestElev = e;
                        bestIndex = c;
                    }
                }

                // Swap the winner to the head and consume it.
                int popped = _queue[bestSlot];
                _queue[bestSlot] = _queue[head];
                _queue[head] = popped;
                head++;

                // Record the discovery order. A cell is popped only after the
                // cell it was reached from, so this array is downstream-to-
                // upstream and is a topological order of the tree by
                // construction. No sort, and no cycle check.
                _order[_orderCount++] = popped;

                int px = popped % w;
                int py = popped / w;

                // Four neighbours, in the same E, S, W, N order the field's flux
                // layout uses, so the edge slot written below is the one the
                // heightfield solver would have used for the same direction.
                for (int e = 0; e < WaterField.EdgeCount; e++)
                {
                    int nx = px;
                    int ny = py;

                    if (e == WaterField.East) { nx = px + 1; }
                    else if (e == WaterField.South) { ny = py + 1; }
                    else if (e == WaterField.West) { nx = px - 1; }
                    else { ny = py - 1; }

                    if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                    {
                        continue;
                    }

                    int n = field.Index(nx, ny);
                    if (_visited[n] != 0)
                    {
                        continue;
                    }

                    _visited[n] = 1;

                    // The neighbour drains to the cell it was reached from. The
                    // edge slot is the one pointing back, which is the opposite
                    // of the direction we walked.
                    _downstream[n] = popped;
                    _downstreamEdge[n] = OppositeEdge(e);

                    // Filled elevation: a cell in a depression is raised to the
                    // level of the cell that flooded it, which is what lets the
                    // flood cross a flat without stalling.
                    float own = field.Depth[n];
                    _filled[n] = own > bestElev ? own : bestElev;

                    _queue[tail++] = n;
                }
            }

            // ---- Cells the flood never reached are enclosed sinks ------------
            // A depression with no outlet at all. They stay terminal, which means
            // they fill and hold. That is a legitimate feature - a pool is a
            // sink - and it conserves mass, because holding is not losing.
            //
            // They are appended to the order so the routing sweep still visits
            // them. Their position is irrelevant: they have no downstream, so
            // nothing depends on when they are processed.
            for (int i = 0; i < cells; i++)
            {
                if (_visited[i] == 0)
                {
                    _order[_orderCount++] = i;
                }
            }

            // ---- Contributing area -------------------------------------------
            // Walk upstream-to-downstream, which is the REVERSE of the discovery
            // order, pushing each cell's accumulated area into its downstream
            // neighbour. A cell's upstream contributors appear later in the
            // discovery order, so by the time a cell is visited in reverse every
            // contribution to it has already been added.
            for (int i = _orderCount - 1; i >= 0; i--)
            {
                int c = _order[i];
                int d = _downstream[c];
                if (d >= 0)
                {
                    _accum[d] += _accum[c];
                }
            }

            // ---- Channel capacity --------------------------------------------
            // Width from contributing area, cross-section from width and depth,
            // volume per step from cross-section and cell length, then converted
            // to a depth by dividing by the cell area - because the field works
            // in depths, not volumes.
            //
            //   volume per step = width * depth * cellLength * dt
            //   depth per step  = volume per step / cellArea
            //                   = width * depth * dt / cellSize
            //
            // A first-order channel passes about one cell of water per step; a
            // channel draining the whole tile is capped so the term stays a
            // limiter.
            for (int i = 0; i < cells; i++)
            {
                float width = FirstOrderWidthM * math.pow(_accum[i], WidthExponent);
                if (width > MaxChannelWidthM)
                {
                    width = MaxChannelWidthM;
                }

                _capacity[i] = width * ChannelDepthM * Dt / CellSize;
            }
        }

        /// <summary>
        /// The edge slot pointing the opposite way. Walking east to reach a
        /// neighbour means the neighbour drains west, and the flux slot must
        /// record the direction the water actually travels.
        /// </summary>
        private static int OppositeEdge(int edge)
        {
            if (edge == WaterField.East) { return WaterField.West; }
            if (edge == WaterField.West) { return WaterField.East; }
            if (edge == WaterField.South) { return WaterField.North; }
            return WaterField.South;
        }

        // ---------------------------------------------------------------------
        // The step. Hot path: allocation-free, no virtual dispatch, math.* only.
        // ---------------------------------------------------------------------

        /// <summary>
        /// One routing sweep: clear flux, route along the network in topological
        /// order, then add sources.
        ///
        /// FloatMode.Strict is deliberate, for the same reason it is on the
        /// heightfield solver's job: fast-math would let the compiler reassociate
        /// float operations, which breaks within-device determinism, which breaks
        /// the save system.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
        public struct StepJob : IJob
        {
            public WaterField Field;
            public NativeArray<WaterSource> Sources;

            [ReadOnly] public NativeArray<int> Downstream;
            [ReadOnly] public NativeArray<int> DownstreamEdge;
            [ReadOnly] public NativeArray<int> Order;
            [ReadOnly] public NativeArray<float> Capacity;

            /// <summary>Number of valid entries in <see cref="Order"/>. Passed
            /// explicitly because the array is sized to the cell count and may
            /// not be full.</summary>
            public int OrderCount;

            /// <summary>
            /// Fixed simulation timestep in seconds. Carried on the job because
            /// the source pass needs it: a source's rate is cubic metres per
            /// SECOND, so the volume it adds in one step is rate * dt. The
            /// routing pass does not use it - the channel capacity is already
            /// expressed as a per-step depth, computed once at graph build.
            /// </summary>
            public float Dt;

            /// <summary>
            /// Length-1 accumulator for the volume the sources actually added
            /// this step, in cubic metres. The realised volume after the head
            /// cap, not the nominal rate - the gate compares mass_after against
            /// mass_before + mass_inflow, so a solver that reports its nominal
            /// rate fails a balance check it should have passed.
            /// </summary>
            public NativeArray<float> Inflow;

            public void Execute()
            {
                int w = Field.Width;
                int h = Field.Height;
                float area = Field.CellArea;

                // ---- Pass 1: clear flux -------------------------------------
                for (int i = 0; i < Field.Flux.Length; i++)
                {
                    Field.Flux[i] = 0f;
                }

                // ---- Pass 2: route along the network ------------------------
                // REVERSE through the discovery order, which is upstream-to-
                // downstream. The direction is the whole performance argument
                // for this solver, so it is worth being precise about.
                //
                // The discovery order is downstream-to-upstream: a cell is
                // recorded when it is popped, and it is popped only after the
                // cell it drains to. Walking it backwards therefore visits the
                // most upstream cell first, and each cell moves its water on
                // BEFORE the cell above it tries to send more into it. A chain
                // of cells drains in a single sweep.
                //
                // Walking it forwards would do the opposite: each cell would
                // move only the water it held at the start of the step, and the
                // water arriving from upstream would sit until the next step.
                // The solver would still be correct and would still conserve
                // mass - it would simply take one step per cell of channel
                // length to drain, which is the heightfield solver's behaviour
                // and would throw away the reason this rung exists.
                for (int i = OrderCount - 1; i >= 0; i--)
                {
                    int c = Order[i];
                    int d = Downstream[c];
                    if (d < 0)
                    {
                        continue;
                    }

                    float held = Field.Depth[c];
                    if (held <= MinDepth)
                    {
                        continue;
                    }

                    // The limiter. A cell may not give away more than it holds,
                    // and it may not give away more than its channel can carry.
                    // The first bound is what keeps depth non-negative; the
                    // second is what makes this a channel rather than a pipe of
                    // infinite bore. Both are depths, so the comparison is
                    // meaningful.
                    float movable = held * MaxDrainFraction;
                    float cap = Capacity[c];
                    if (cap < movable)
                    {
                        movable = cap;
                    }

                    movable *= Damping;

                    if (movable <= 0f)
                    {
                        continue;
                    }

                    Field.Depth[c] = held - movable;
                    Field.Depth[d] = Field.Depth[d] + movable;

                    // Record the routed depth in the flux slot for the direction
                    // the water actually travelled, so the field's flux array
                    // means the same thing it means under the heightfield solver.
                    int edge = DownstreamEdge[c];
                    if (edge >= 0)
                    {
                        int cx = c % w;
                        int cy = c / w;
                        Field.Flux[Field.FluxIndex(cx, cy, edge)] = movable;
                    }
                }

                // ---- Pass 3: add sources ------------------------------------
                float addedVolume = 0f;

                for (int i = 0; i < Sources.Length; i++)
                {
                    WaterSource s = Sources[i];
                    if (s.X < 0 || s.X >= w || s.Y < 0 || s.Y >= h)
                    {
                        continue;
                    }

                    int c = Field.Index(s.X, s.Y);
                    float before = Field.Depth[c];
                    float added = s.Rate * Dt / area;
                    float next = before + added;
                    if (next > s.Head)
                    {
                        next = s.Head;
                    }
                    Field.Depth[c] = next;

                    // The ACTUAL volume added, after the head cap. Once the cell
                    // reaches its head the source stops contributing, which is
                    // exactly the behaviour the mass balance must reflect.
                    addedVolume += (next - before) * area;
                }

                if (Inflow.IsCreated && Inflow.Length > 0)
                {
                    Inflow[0] = addedVolume;
                }
            }
        }
    }
}
