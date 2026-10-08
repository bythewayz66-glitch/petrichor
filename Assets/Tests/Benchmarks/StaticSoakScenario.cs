using Unity.Collections;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// Scenario A - static soak.
    ///
    /// WHAT IT MEASURES
    /// ----------------
    /// The cost of a step on a field that has already reached steady state.
    /// This is the number that describes the game at rest: a river that has
    /// found its channel, a pool that has filled. It is the cheapest of the
    /// three budget scenarios and the one most likely to be quoted, which is
    /// exactly why it must not be the only one.
    ///
    /// WHY IT IS NOT ENOUGH ON ITS OWN
    /// -------------------------------
    /// A settled field has small head differences, so the flux limiter in pass 3
    /// rarely fires and the branch predictor is happy. A solver can look
    /// excellent here and fall over in scenario B, where every cell is trying to
    /// give away more water than it holds.
    ///
    /// PARAMETERS
    /// ----------
    ///   settle steps   3000   reach steady state before measuring
    ///   warmup           50   discard, so the JIT and Burst have settled
    ///   measurements    500   samples, one step each
    ///   steps/sample      1
    ///
    /// THRESHOLD (per step, ms)
    ///   linux    p50 1.5   p95 3.0   max 6.0
    ///   android  p50 2.5   p95 5.0   max 10.0
    ///
    /// REPORTS INTO
    /// ------------
    /// <c>BenchmarkResults/summary_A_StaticSoak.json</c>, plus a
    /// <c>PET_BENCH {...}</c> line in the NUnit results XML.
    /// </summary>
    public static class StaticSoakScenario
    {
        public const string Name = "A_StaticSoak";

        private const int SettleSteps = 3000;
        private const int Warmup = 50;
        private const int Measurements = 500;

        public static ScenarioResult Run(IWaterSolver solver, string tier)
        {
            // The window is set before the settle loop, not before the
            // measurement. Windowing only the measured window would measure a
            // field that never reached the state this run claims to describe.
            BenchmarkScenarios.BeginWindowed();
            try
            {
                return RunCore(solver, tier);
            }
            finally
            {
                BenchmarkScenarios.EndWindowed();
            }
        }

        private static ScenarioResult RunCore(IWaterSolver solver, string tier)
        {
            using var field = BenchmarkScenarios.MakeField();
            using var sources = BenchmarkScenarios.MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            // Settle first. The soak is the only scenario that measures a
            // settled field; B and C measure transients, and measuring a
            // transient here would make the three scenarios redundant.
            for (int i = 0; i < SettleSteps; i++)
            {
                solver.Step(field, sources, inflow);
            }

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
