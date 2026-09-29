using Unity.Collections;

namespace PET.Water
{
    /// <summary>
    /// The reference implementation: the heightfield solver in
    /// <see cref="WaterSolver"/>, wrapped so the harness can hold it behind
    /// <see cref="IWaterSolver"/>.
    ///
    /// This is a thin adapter on purpose. It adds no behaviour, because any
    /// behaviour added here would be behaviour the gate does not measure - the
    /// gate times <see cref="Step"/>, and this class's Step is a direct call to
    /// the solver's.
    ///
    /// It is a class rather than a struct because the harness stores it in a
    /// field and calls through the interface; a struct would box on every call
    /// and the boxing would land in the measurement.
    /// </summary>
    public sealed class HeightfieldWaterSolver : IWaterSolver
    {
        /// <summary>
        /// The name written into every summary JSON. Kept as a const so the
        /// harness and the report can refer to it without a magic string.
        /// </summary>
        public const string SolverName = "heightfield";

        public string Name => SolverName;

        public int TileResolution { get; }

        public float CellSize { get; }

        public float Dt { get; }

        public HeightfieldWaterSolver(int tileResolution, float cellSize, float dt)
        {
            TileResolution = tileResolution;
            CellSize = cellSize;
            Dt = dt;
        }

        /// <summary>
        /// One step of the four-pass sweep: clear flux, compute outgoing flux
        /// from the head difference, limit, apply, then add sources.
        ///
        /// The limiter in pass 3 is the whole reason the solver is stable. It is
        /// also the reason this method is worth measuring separately from the
        /// other three passes: it is the only pass whose cost depends on the
        /// field's state rather than its size.
        /// </summary>
        public float Step(
            WaterField field,
            NativeArray<WaterSource> sources,
            NativeArray<float> inflow)
        {
            return WaterSolver.Step(field, sources, Dt, inflow);
        }

        /// <summary>
        /// No-op. The heightfield solver carries no state between steps: the
        /// depth buffer IS the state, and it is owned by the caller. Resetting
        /// is therefore the caller's job, and the harness does it by allocating
        /// a fresh field per scenario.
        /// </summary>
        public void Reset()
        {
        }
    }
}
