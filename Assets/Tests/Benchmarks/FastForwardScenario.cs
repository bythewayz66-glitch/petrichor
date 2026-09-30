using Unity.Collections;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// Scenario C - fast-forward.
    ///
    /// WHAT IT MEASURES
    /// ----------------
    /// The cost of a step when the time toggle is at its top speed, where a
    /// single frame takes 3600 simulation steps.
    ///
    /// THE TOGGLE DOES NOT CHANGE THE STEP SIZE
    /// ----------------------------------------
    /// This is the thing most easily got wrong. Fast-forward does not take a
    /// larger dt - it takes MORE steps of the same dt. A larger dt would change
    /// the solver's stability characteristics and the gate would be measuring a
    /// different solver. So the reported figure is normalised to PER STEP by
    /// dividing the 3600-step sample by 3600, which makes C directly comparable
    /// with A and B and with the thresholds in bench_thresholds.json.
    ///
    /// The frame-level question is answered by multiplying: at 30 Hz a frame is
    /// 33.33 ms, and the solver's share of it is the per-step figure times the
    /// number of steps that frame takes. At 3600 steps that is a number no
    /// device can meet - which is the point. Fast-forward is a stability test
    /// with a cost measurement attached, not a promise that 3600 steps fit in a
    /// frame.
    ///
    /// WHY IT IS A SEPARATE SCENARIO
    /// -----------------------------
    /// A solver can pass A and B and fail C. Sustained stepping at 3600x is
    /// where a slow accumulation shows up: a field that is very slightly
    /// unstable will look fine over 500 steps and produce NaNs over 18,000.
    ///
    /// PARAMETERS
    /// ----------
    ///   seed steps    200   so the field is not trivially dry
    ///   warmup          1   one 3600-step sample, discarded
    ///   measurements    5   five 3600-step samples = 18,000 steps measured
    ///   steps/sample 3600
    ///
    /// THRESHOLD (per step, ms)
    ///   linux    p50 1.5   p95 3.0   max 6.0
    ///   android  p50 2.5   p95 5.0   max 10.0
    ///
    /// REPORTS INTO
    /// ------------
    /// <c>BenchmarkResults/summary_C_FastForward.json</c>.
    /// </summary>
    public static class FastForwardScenario
    {
        public const string Name = "C_FastForward";

        /// <summary>3600x is the time toggle's top speed: the number of sim
        /// steps taken in a single frame at the fastest setting.</summary>
        public const int Multiplier = 3600;

        private const int SeedSteps = 200;
        private const int Warmup = 1;
        private const int Measurements = 5;

        public static ScenarioResult Run(IWaterSolver solver, string tier)
        {
            using var field = BenchmarkScenarios.MakeField();
            using var sources = BenchmarkScenarios.MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            // Seed some water so the field is not trivially dry. A dry field
            // would make every step a no-op and C would report a meaningless
            // number that looked excellent.
            for (int i = 0; i < SeedSteps; i++)
            {
                solver.Step(field, sources, inflow);
            }

            float massBefore = field.TotalMass();

            double[] samples = BenchmarkScenarios.MeasureSteps(
                solver, field, sources, inflow,
                warmup: Warmup,
                count: Measurements,
                stepsPerSample: Multiplier,
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
