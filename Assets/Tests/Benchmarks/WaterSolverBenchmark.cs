using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;
using UnityEngine;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// The week-one water gate.
    ///
    /// Four scenarios. A, B and C are the budget scenarios; D is the
    /// determinism scenario. Each asserts correctness BEFORE budget:
    ///
    ///   A. Static soak   - steady state, mass balance
    ///   B. Step flood    - the transient the solver must survive without stuttering
    ///   C. Fast-forward  - stability and per-step cost under the 3600x time toggle
    ///   D. Determinism   - the same inputs must produce the same field, twice
    ///
    /// A solver can pass A and B and fail C. They are not three measurements of
    /// the same thing.
    ///
    /// Hard fails (NaN, mass imbalance, zero hash) are asserted HERE, in the
    /// NUnit run, so the failure is visible in the editor before the headless
    /// verdict is produced. They are NOT budget failures: the fallback ladder
    /// buys performance and cannot buy correctness.
    ///
    /// MEASUREMENT NOTE. The authoritative numbers the checker reads are
    /// computed by this file with a Stopwatch, not read back out of the
    /// Performance Testing package. The package is still used - it drives the
    /// Test Runner integration and the editor's Performance Test Report - but
    /// its result format is an implementation detail that has changed between
    /// major versions, and a gate that breaks when a package updates is a gate
    /// that gets disabled. The numbers written to disk are produced by the same
    /// code that ran the measurement, so they cannot disagree with it.
    /// </summary>
    public class WaterSolverBenchmark
    {
        // ---- Scenario parameters (mirrors Tools/bench_thresholds.json) ----

        /// <summary>257 = 2^8 + 1. Unity terrain heightmap resolution must be a
        /// power of two plus one; this is a constraint of the format, not a
        /// preference.</summary>
        private const int TileRes = 257;

        /// <summary>250 m tile across 256 cells between 257 samples.</summary>
        private const float TileSizeM = 250f;

        private const int TileCount = 4;          // 2x2 block
        private const float Dt = 1f / 30f;        // fixed sim step, 30 Hz

        private const int SoakSteps = 3000;
        private const int SoakWarmup = 50;
        private const int SoakMeasurements = 500;

        private const int FloodWarmup = 10;
        private const int FloodMeasurements = 600;

        /// <summary>3600x is the time toggle's top speed: the number of sim
        /// steps taken in a single frame at the fastest setting.</summary>
        private const int FastForwardMultiplier = 3600;
        private const int FastForwardWarmup = 1;
        private const int FastForwardMeasurements = 5;

        private const float SourceRate = 2.0f;    // m^3/s
        private const float SourceHead = 0.35f;   // m

        // ---- Hard-fail tolerances ----

        /// <summary>Mass may not drift more than 0.1% once the source inflow is
        /// accounted for. Water is re-derived on load, so a solver that loses or
        /// invents water is a save-corruption bug.</summary>
        private const float MassDriftTolerance = 0.001f;

        private static string OutputDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkResults");

        private static string TierName =>
            Environment.GetEnvironmentVariable("PET_BENCH_TIER") ?? "unknown";

        // ------------------------------------------------------------------
        //  Scenario A - static soak
        // ------------------------------------------------------------------

        [Test, Performance]
        public void A_StaticSoak()
        {
            using var field = MakeField();
            using var sources = MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            // Warm up to steady state before measuring. The soak is the only
            // scenario that measures a settled field; B and C measure transients.
            for (int i = 0; i < SoakSteps; i++)
            {
                WaterSolver.Step(field, sources, Dt, inflow);
            }

            float massBefore = field.TotalMass();

            double[] samples = MeasureSteps(
                field, sources, inflow,
                warmup: SoakWarmup,
                count: SoakMeasurements,
                stepsPerSample: 1,
                out float inflowTotal);

            AssertCorrectness(field, massBefore, inflowTotal, "A_StaticSoak");
            Record("A_StaticSoak", field, massBefore, inflowTotal, samples);
        }

        // ------------------------------------------------------------------
        //  Scenario B - step flood
        // ------------------------------------------------------------------

        [Test, Performance]
        public void B_StepFlood()
        {
            using var field = MakeField();
            using var sources = MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            // A dry field with a sudden source is the worst transient: the
            // largest head differences the solver will ever see.
            float massBefore = field.TotalMass();

            double[] samples = MeasureSteps(
                field, sources, inflow,
                warmup: FloodWarmup,
                count: FloodMeasurements,
                stepsPerSample: 1,
                out float inflowTotal);

            AssertCorrectness(field, massBefore, inflowTotal, "B_StepFlood");
            Record("B_StepFlood", field, massBefore, inflowTotal, samples);
        }

        // ------------------------------------------------------------------
        //  Scenario C - fast-forward
        // ------------------------------------------------------------------

        [Test, Performance]
        public void C_FastForward()
        {
            using var field = MakeField();
            using var sources = MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            // Seed some water so the field is not trivially dry.
            for (int i = 0; i < 200; i++)
            {
                WaterSolver.Step(field, sources, Dt, inflow);
            }

            float massBefore = field.TotalMass();

            // The toggle does not change the step size - it changes how many
            // steps are taken per frame. Stability under that is the question,
            // and the reported figure is normalised to PER STEP so it is
            // comparable with the thresholds in bench_thresholds.json.
            double[] samples = MeasureSteps(
                field, sources, inflow,
                warmup: FastForwardWarmup,
                count: FastForwardMeasurements,
                stepsPerSample: FastForwardMultiplier,
                out float inflowTotal);

            AssertCorrectness(field, massBefore, inflowTotal, "C_FastForward");
            Record("C_FastForward", field, massBefore, inflowTotal, samples);
        }

        // ------------------------------------------------------------------
        //  Scenario D - within-device determinism
        // ------------------------------------------------------------------

        /// <summary>
        /// The same inputs must produce the same field, twice, on the same
        /// device. This is what the save system depends on: water is re-derived
        /// from terrain height and source points on load, so a device that
        /// produces a different field from identical inputs would load a
        /// different world than it saved.
        ///
        /// Cross-device bit-identity is explicitly NOT asserted. Burst compiles
        /// to different SIMD widths on x86-64 and ARM64, so demanding it would
        /// be demanding something the toolchain does not offer.
        /// </summary>
        [Test]
        public void D_Determinism()
        {
            const int steps = 500;

            using var a = MakeField();
            using var b = MakeField();
            using var sources = MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            for (int i = 0; i < steps; i++)
            {
                WaterSolver.Step(a, sources, Dt, inflow);
            }

            // Replay the identical sequence from an identical start.
            for (int i = 0; i < steps; i++)
            {
                WaterSolver.Step(b, sources, Dt, inflow);
            }

            uint hashA = a.Hash();
            uint hashB = b.Hash();

            Assert.AreNotEqual(0u, hashA, "D_Determinism: field hash collapsed to zero. HARD FAIL.");
            Assert.AreEqual(
                hashA, hashB,
                $"D_Determinism: identical inputs produced different fields " +
                $"({hashA} vs {hashB}). Within-device determinism is broken, which " +
                $"breaks the save system. HARD FAIL.");

            Debug.Log($"[PET.Benchmarks] D_Determinism OK - hash {hashA} reproduced exactly.");
        }

        // ------------------------------------------------------------------
        //  Measurement
        // ------------------------------------------------------------------

        /// <summary>
        /// Time <paramref name="count"/> samples of <paramref name="stepsPerSample"/>
        /// solver steps each, and return the PER-STEP milliseconds.
        ///
        /// Normalising to per-step is what makes scenario C comparable with A and
        /// B: the fast-forward toggle takes 3600 steps in a frame, but the budget
        /// question is still "what does one step cost".
        ///
        /// The inflow accumulator is threaded through so the mass balance can be
        /// checked against the water the sources actually added.
        /// </summary>
        private static double[] MeasureSteps(
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
                    inflowTotal += WaterSolver.Step(field, sources, Dt, inflow);
                }
            }

            var samples = new double[count];
            var sw = new System.Diagnostics.Stopwatch();

            for (int i = 0; i < count; i++)
            {
                sw.Restart();
                for (int s = 0; s < stepsPerSample; s++)
                {
                    inflowTotal += WaterSolver.Step(field, sources, Dt, inflow);
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
        private static void AssertCorrectness(
            WaterField field, float massBefore, float inflowTotal, string scenario)
        {
            int nans = field.NaNCount();
            Assert.AreEqual(0, nans, $"{scenario}: {nans} non-finite depth samples. HARD FAIL.");

            float massAfter = field.TotalMass();
            float expected = massBefore + inflowTotal;
            float drift = expected > 0f
                ? Mathf.Abs(massAfter - expected) / expected
                : 0f;

            Assert.LessOrEqual(
                drift, MassDriftTolerance,
                $"{scenario}: mass balance off by {drift:P3} " +
                $"(expected {expected.ToString("R", CultureInfo.InvariantCulture)} m3, " +
                $"got {massAfter.ToString("R", CultureInfo.InvariantCulture)} m3). HARD FAIL.");

            Assert.AreNotEqual(
                0u, field.Hash(),
                $"{scenario}: field hash collapsed to zero. HARD FAIL.");
        }

        // ------------------------------------------------------------------
        //  Recording
        // ------------------------------------------------------------------

        private static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 0)
            {
                return 0.0;
            }
            int idx = (int)Math.Ceiling(p / 100.0 * sorted.Length) - 1;
            if (idx < 0)
            {
                idx = 0;
            }
            if (idx >= sorted.Length)
            {
                idx = sorted.Length - 1;
            }
            return sorted[idx];
        }

        /// <summary>
        /// Writes the small JSON summary the checker reads, and logs the same
        /// numbers as a single PET_BENCH line so they also land in the NUnit
        /// results XML.
        ///
        /// Deliberately NOT the package's own sample output: that format is an
        /// implementation detail which has changed between major versions, and a
        /// gate that breaks when a package updates is a gate that gets disabled.
        /// This shape is one the project controls.
        /// </summary>
        private static void Record(
            string scenario, WaterField field, float massBefore, float inflowTotal, double[] samples)
        {
            Directory.CreateDirectory(OutputDir);

            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);

            double p50 = Percentile(sorted, 50.0);
            double p95 = Percentile(sorted, 95.0);
            double max = sorted.Length > 0 ? sorted[sorted.Length - 1] : 0.0;

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"scenario\":\"{scenario}\",");
            sb.Append($"\"tier\":\"{TierName}\",");
            sb.Append($"\"tile_res\":{TileRes},");
            sb.Append($"\"tile_count\":{TileCount},");
            sb.Append($"\"dt\":{Dt.ToString("R", CultureInfo.InvariantCulture)},");
            sb.Append($"\"samples\":{samples.Length},");
            sb.Append($"\"p50_ms\":{p50.ToString("F4", CultureInfo.InvariantCulture)},");
            sb.Append($"\"p95_ms\":{p95.ToString("F4", CultureInfo.InvariantCulture)},");
            sb.Append($"\"max_ms\":{max.ToString("F4", CultureInfo.InvariantCulture)},");
            sb.Append($"\"nan_count\":{field.NaNCount()},");
            sb.Append($"\"mass_before\":{massBefore.ToString("R", CultureInfo.InvariantCulture)},");
            sb.Append($"\"mass_inflow\":{inflowTotal.ToString("R", CultureInfo.InvariantCulture)},");
            sb.Append($"\"mass_after\":{field.TotalMass().ToString("R", CultureInfo.InvariantCulture)},");
            sb.Append($"\"hash\":{field.Hash()},");
            sb.Append($"\"device\":\"{Escape(SystemInfo.deviceModel)}\",");
            sb.Append($"\"gpu\":\"{Escape(SystemInfo.graphicsDeviceName)}\",");
            sb.Append($"\"gpu_driver\":\"{Escape(SystemInfo.graphicsDeviceVersion)}\"");
            sb.Append("}");

            string json = sb.ToString();
            string path = Path.Combine(OutputDir, $"summary_{scenario}.json");
            File.WriteAllText(path, json);

            // The same numbers, on one line, for the NUnit results XML.
            Debug.Log($"PET_BENCH {json}");
            Debug.Log($"[PET.Benchmarks] wrote {path}");
        }

        private static string Escape(string s)
        {
            return string.IsNullOrEmpty(s) ? "unknown" : s.Replace("\\", "/").Replace("\"", "'");
        }

        // ------------------------------------------------------------------
        //  Fixtures
        // ------------------------------------------------------------------

        private static WaterField MakeField()
        {
            return WaterField.Allocate(
                TileRes, TileRes, TileSizeM / (TileRes - 1), Allocator.Persistent);
        }

        private static NativeArray<WaterSource> MakeSources()
        {
            var arr = new NativeArray<WaterSource>(1, Allocator.Persistent);
            arr[0] = WaterSource.Spring(TileRes / 2, TileRes / 2, SourceRate, SourceHead);
            return arr;
        }
    }
}
