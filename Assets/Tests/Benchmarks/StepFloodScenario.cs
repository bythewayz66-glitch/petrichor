using Unity.Collections;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// Scenario B - step flood.
    ///
    /// WHAT IT MEASURES
    /// ----------------
    /// The cost of a step during the worst transient the solver will ever see:
    /// a dry field with a source switched on. Every cell adjacent to the source
    /// has the largest head difference it will ever have, so the flux limiter in
    /// pass 3 fires on a large fraction of cells at once.
    ///
    /// WHY THIS IS THE SCENARIO THAT MATTERS
    /// -------------------------------------
    /// The limiter is the only pass whose cost depends on the field's STATE
    /// rather than its size. In a settled field it is nearly free; in a flood it
    /// is the dominant cost. A solver tuned against scenario A alone will show a
    /// p95 here that is several times its p50, and that spread is the thing the
    /// player feels as a stutter when they open a channel.
    ///
    /// This is also the scenario that catches a solver which is fast because it
    /// is wrong - one that skips the limiter entirely will post excellent numbers
    /// here and produce NaNs, which is why correctness is asserted before the
    /// timing is even looked at.
    ///
    /// PARAMETERS
    /// ----------
    ///   warmup          10   discard; the flood is the measurement, so the
    ///                        warmup must be short or the transient is gone
    ///   measurements   600   samples, one step each
    ///   steps/sample     1
    ///
    /// THRESHOLD (per step, ms)
    ///   linux    p50 1.5   p95 3.0   max 6.0
    ///   android  p50 2.5   p95 5.0   max 10.0
    ///
    /// REPORTS INTO
    /// ------------
    /// <c>BenchmarkResults/summary_B_StepFlood.json</c>.
    /// </summary>
    public static class StepFloodScenario
    {
        public const string Name = "B_StepFlood";

        private const int Warmup = 10;
        private const int Measurements = 600;

        public static ScenarioResult Run(IWaterSolver solver, string tier)
        {
            using var field = BenchmarkScenarios.MakeField();
            using var sources = BenchmarkScenarios.MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            // No settle loop. The field is dry and the source is live, which is
            // the largest head difference the solver will ever be asked to
            // resolve. A long warmup here would let the flood subside and the
            // scenario would silently become a second static soak.
            float massBefore = field.TotalMass();

            double[] samples = BenchmarkScenarios.MeasureSteps(
                solver, field, sources, inflow,
                warmup: Warmup,
                count: Measurements,
                stepsPerSample: 1,
                out float inflowTotal);

            BenchmarkScenarios.AssertCorrectness(field, massBefore, inflowTotal, Name);

            return ScenarioResult.FromSamples(
                Name, tier, solver.Name,
                BenchmarkScenarios.TileRes, BenchmarkScenarios.TileCount, BenchmarkScenarios.Dt,
                samples,
                field.NaNCount(), massBefore, inflowTotal, field.TotalMass(), field.Hash());
        }
    }
}
