using Unity.Collections;

namespace PET.Water
{
    /// <summary>
    /// The contract the week-one water gate measures against.
    ///
    /// WHY THIS LIVES IN PET.Water AND NOT IN PET.Benchmarks
    /// -----------------------------------------------------
    /// The assembly reference graph is strictly downward: PET.Benchmarks
    /// references PET.Water, never the reverse. An interface declared in the
    /// test assembly could not be implemented by production code without
    /// inverting that edge, which would let shipping code depend on test code.
    /// So the contract lives with the system it describes, and the harness
    /// consumes it.
    ///
    /// WHAT THE GATE ACTUALLY MEASURES
    /// -------------------------------
    /// The gate measures <see cref="Step"/> and nothing else. Everything the
    /// harness times is a call to this method, so a solver that is fast at
    /// something other than stepping has not been measured.
    ///
    /// IMPLEMENTATION REQUIREMENTS
    /// ---------------------------
    /// These are requirements, not suggestions. A solver that violates one of
    /// them is not a candidate for the gate, because the gate's numbers would
    /// not mean what the report says they mean.
    ///
    /// 1. BURST. <see cref="Step"/> must be Burst-compilable. The reference
    ///    implementation runs its work inside an IJob carrying
    ///    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision =
    ///    FloatPrecision.Standard)]. A managed implementation is permitted for
    ///    comparison, but it must be labelled as such in <see cref="Name"/> so
    ///    the report cannot present it as the shipping path.
    ///
    /// 2. FLOATMODE.STRICT. Fast-math is forbidden. It lets the compiler
    ///    reassociate float operations, which breaks within-device determinism,
    ///    which breaks the save system. This is the single most important
    ///    constraint in this file.
    ///
    /// 3. DETERMINISM. Given identical inputs and an identical starting field,
    ///    two runs on the same device must produce bit-identical depth buffers.
    ///    This is what the save system depends on: water is re-derived from
    ///    terrain height and source points on load, so a device that produces a
    ///    different field from identical inputs would load a different world
    ///    than it saved.
    ///
    ///    Cross-device bit-identity is explicitly NOT required and must not be
    ///    asserted. Burst compiles to different SIMD widths on x86-64 and
    ///    ARM64, so demanding it would be demanding something the toolchain
    ///    does not offer.
    ///
    /// 4. MASS BALANCE. The volume the solver reports through
    ///    <c>inflow[0]</c> must be the volume it actually added, after any head
    ///    cap. The gate compares mass_after against mass_before + mass_inflow,
    ///    so a solver that reports its nominal rate rather than its realised
    ///    rate will fail a balance check it should have passed.
    ///
    /// 5. NO ALLOCATION IN STEP. The hot loop allocates nothing. A solver that
    ///    allocates per step will show up as GC spikes in the p95 and max
    ///    columns, which is a real failure but a misleading one.
    ///
    /// 6. NO MANAGED STATE. The field, the sources and the inflow accumulator
    ///    are all passed in. A solver that keeps its own copy of the field is
    ///    not measuring the same thing the game will run.
    /// </summary>
    public interface IWaterSolver
    {
        /// <summary>
        /// Short identifier written into every summary JSON, so a result can be
        /// attributed to the implementation that produced it. Use a distinct
        /// name per implementation - "heightfield", "heightfield-129",
        /// "channel-graph" - because the ladder changes the solver and the
        /// report must be able to tell the rungs apart.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Simulation resolution this solver is configured for. Must match the
        /// field it is handed; the harness allocates the field from this value.
        /// </summary>
        int TileResolution { get; }

        /// <summary>Cell size in metres, derived as tile width / (res - 1).</summary>
        float CellSize { get; }

        /// <summary>
        /// Fixed simulation timestep in seconds. The gate runs at 1/30 s. The
        /// fast-forward toggle does NOT change this - it changes how many steps
        /// are taken per frame, which is why scenario C is normalised to
        /// per-step cost and is directly comparable with A and B.
        /// </summary>
        float Dt { get; }

        /// <summary>
        /// Advance the field one step.
        ///
        /// Returns the volume of water the sources actually added this step, in
        /// cubic metres, and writes the same value to <paramref name="inflow"/>[0].
        /// The return value is a convenience; the array is the contract, because
        /// it is what the harness threads through the measurement loop.
        ///
        /// <paramref name="inflow"/> is caller-owned and reused across steps so
        /// the hot loop allocates nothing. It is an array rather than a ref
        /// parameter because IJob.Run() takes the job by value, so a plain float
        /// written inside Execute() would never be visible to the caller.
        /// </summary>
        float Step(
            WaterField field,
            NativeArray<WaterSource> sources,
            NativeArray<float> inflow);

        /// <summary>
        /// Discard any internal state the solver carries between steps.
        ///
        /// The reference heightfield solver is stateless - all of its state is
        /// in the field - so this is a no-op for it. It exists because the
        /// rung-3 fallback (a channel graph on Android) DOES carry state, and
        /// the harness must be able to reset a solver between scenarios without
        /// knowing which implementation it holds.
        /// </summary>
        void Reset();
    }
}
