using System;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// Shared fixtures, parameters and measurement for the four scenarios.
    ///
    /// Every constant here mirrors <c>Tools/bench_thresholds.json</c>. The two
    /// are kept in step by hand, and the harness writes the values it actually
    /// used into every summary so a mismatch is visible in the results rather
    /// than inferred from a threshold that quietly stopped applying.
    /// </summary>
    public static class BenchmarkScenarios
    {
        // ---- Geometry ------------------------------------------------------

        /// <summary>
        /// 257 = 2^8 + 1. Unity terrain heightmap resolution must be a power of
        /// two plus one; this is a constraint of the heightmap format, not a
        /// preference. The gate runs at the shipping resolution deliberately -
        /// measuring at a convenient resolution and extrapolating is how a gate
        /// passes and the game does not.
        /// </summary>
        public const int TileRes = 257;

        /// <summary>250 m tile across 256 cells between 257 samples.</summary>
        public const float TileSizeM = 250f;

        /// <summary>Four tiles, laid out as a 2x2 block.</summary>
        public const int TileCount = 4;

        /// <summary>Fixed simulation timestep, 30 Hz.</summary>
        public const float Dt = 1f / 30f;

        /// <summary>Cell size in metres: 250 / 256 = 0.9766 m. Getting this
        /// wrong by one cell changes the mass-balance arithmetic by 0.8%.</summary>
        public const float CellSize = TileSizeM / (TileRes - 1);

        // ---- Source --------------------------------------------------------

        public const float SourceRate = 2.0f;   // m^3/s
        public const float SourceHead = 0.35f;  // m

        // ---- Hard-fail tolerance -------------------------------------------

        /// <summary>
        /// Mass may not drift more than 0.1% once the source inflow is
        /// accounted for. Water is re-derived from terrain height and source
        /// points on load, so a solver that loses or invents water is a
        /// save-corruption bug rather than a visual one.
        /// </summary>
        public const float MassDriftTolerance = 0.001f;

        // ---- Fixtures ------------------------------------------------------

        public static WaterField MakeField()
        {
            return WaterField.Allocate(TileRes, TileRes, CellSize, Allocator.Persistent);
        }

        public static NativeArray<WaterSource> MakeSources()
        {
            var arr = new NativeArray<WaterSource>(1, Allocator.Persistent);
            arr[0] = WaterSource.Spring(TileRes / 2, TileRes / 2, SourceRate, SourceHead);
            return arr;
        }

        // ---- Measurement ---------------------------------------------------

        /// <summary>
        /// Time <paramref name="count"/> samples of
        /// <paramref name="stepsPerSample"/> solver steps each, and return the
        /// PER-STEP milliseconds.
        ///
        /// Normalising to per-step is what makes scenario C comparable with A
        /// and B. The fast-forward toggle takes 3600 steps in a frame, but the
        /// budget question is still "what does one step cost" - so C divides by
        /// 3600 and lands in the same units as the other two.
        ///
        /// The inflow accumulator is threaded through so the mass balance can be
        /// checked against the water the sources actually added, rather than
        /// against a nominal rate.
        /// </summary>
        public static double[] MeasureSteps(
            IWaterSolver solver,
            WaterField field,
            NativeArray<WaterSource> sources,
            NativeArray<float> inflow,
            int warmup,
            int count,
            int stepsPerSample,
            out float inflowTotal)
        {
            inflowTotal = 0f;

            for (int i = 0; i < warmup; i++)
            {
                for (int s = 0; s < stepsPerSample; s++)
                {
                    inflowTotal += solver.Step(field, sources, inflow);
                }
            }

            var samples = new double[count];
            var sw = new Stopwatch();

            for (int i = 0; i < count; i++)
            {
                sw.Restart();
                for (int s = 0; s < stepsPerSample; s++)
                {
                    inflowTotal += solver.Step(field, sources, inflow);
                }
                sw.Stop();
                samples[i] = sw.Elapsed.TotalMilliseconds / stepsPerSample;
            }

            return samples;
        }

        /// <summary>
        /// Correctness first, budget second. This ordering is the point: a fast
        /// wrong solver is the most dangerous outcome the gate can produce, and
        /// it must be impossible to mistake for a pass.
        ///
        /// The mass assertion is a BALANCE, not a constancy check. A field with
        /// an open source is not a closed system - its mass is supposed to grow.
        /// Asserting that mass stayed constant would fail on a perfectly correct
        /// solver and pass vacuously on a dry one.
        /// </summary>
        public static void AssertCorrectness(
            WaterField field, float massBefore, float inflowTotal, string scenario)
        {
            int nans = field.NaNCount();
            Assert.AreEqual(
                0, nans,
                $"{scenario}: {nans} non-finite depth samples. HARD FAIL - the ladder " +
                $"buys performance and cannot buy correctness.");

            float massAfter = field.TotalMass();
            float expected = massBefore + inflowTotal;
            float drift = expected > 0f
                ? Math.Abs(massAfter - expected) / expected
                : 0f;

            Assert.LessOrEqual(
                drift, MassDriftTolerance,
                $"{scenario}: mass balance off by {drift:P3} " +
                $"(expected {expected:R} m3, got {massAfter:R} m3). HARD FAIL.");

            Assert.AreNotEqual(
                0u, field.Hash(),
                $"{scenario}: field hash collapsed to zero. HARD FAIL.");
        }
    }
}
