using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PET.Benchmarks
{
    /// <summary>
    /// The result of one scenario, in the exact shape
    /// <c>Tools/check_thresholds.py</c> parses.
    ///
    /// WHY THIS SHAPE AND NOT THE PACKAGE'S
    /// ------------------------------------
    /// The Performance Testing package writes its own sample output, and that
    /// format is an implementation detail which has changed between major
    /// versions. A gate that breaks when a package updates is a gate that gets
    /// disabled, so the numbers the checker reads are written by this struct -
    /// by the same code that ran the measurement, which means they cannot
    /// disagree with what was measured.
    ///
    /// The package is still used: it drives the Test Runner integration and the
    /// editor's Performance Test Report. It is simply not the source of truth
    /// for the verdict.
    ///
    /// FIELD NAMES ARE A CONTRACT. <c>check_thresholds.py</c> reads these keys by
    /// name. Renaming one here without renaming it there silently turns a
    /// measured run into an INPUT ERROR, which is at least loud - but renaming
    /// it in both places to something the checker does not expect is how a gate
    /// starts passing vacuously.
    /// </summary>
    public struct ScenarioResult
    {
        public string Scenario;
        public string Tier;

        /// <summary>
        /// Which solver produced these numbers, from <c>IWaterSolver.Name</c>.
        ///
        /// WHY THIS FIELD EXISTS
        /// ---------------------
        /// The fallback ladder changes the solver, and the whole point of the
        /// ladder is to tell the rungs apart. Before this field the only record
        /// of which solver ran was a <c>Debug.Log</c> line in the Unity log,
        /// which the checker does not read and which is not uploaded as an
        /// artifact - so two runs against different solvers produced
        /// indistinguishable JSON, and a report could not be attributed.
        ///
        /// The checker reads this key and refuses a run whose summaries disagree
        /// about it: a directory of summaries from two different solvers is not
        /// a measurement of either.
        /// </summary>
        public string Solver;

        public int TileRes;
        public int TileCount;
        public float Dt;
        public int Samples;

        /// <summary>Milliseconds PER SOLVER STEP, not per frame.</summary>
        public double P50Ms;
        public double P95Ms;
        public double MaxMs;

        public int NanCount;

        /// <summary>Volume before the measured window, in cubic metres.</summary>
        public float MassBefore;

        /// <summary>Volume the sources actually added during the window. This is
        /// the realised volume after the head cap, not the nominal rate - a
        /// solver that reports its nominal rate fails a balance check it should
        /// have passed.</summary>
        public float MassInflow;

        public float MassAfter;

        public uint Hash;

        public string Device;
        public string Gpu;
        public string GpuDriver;

        /// <summary>
        /// Serialise to the summary JSON. Written with InvariantCulture
        /// throughout: a machine with a comma decimal separator would otherwise
        /// emit <c>"p50_ms":1,234</c>, which is not JSON and which the checker
        /// would reject as malformed input.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            sb.Append("\"scenario\":\"").Append(Escape(Scenario)).Append("\",");
            sb.Append("\"tier\":\"").Append(Escape(Tier)).Append("\",");
            sb.Append("\"solver\":\"").Append(Escape(Solver)).Append("\",");
            sb.Append("\"tile_res\":").Append(TileRes).Append(',');
            sb.Append("\"tile_count\":").Append(TileCount).Append(',');
            sb.Append("\"dt\":").Append(F(Dt)).Append(',');
            sb.Append("\"samples\":").Append(Samples).Append(',');
            sb.Append("\"p50_ms\":").Append(F4(P50Ms)).Append(',');
            sb.Append("\"p95_ms\":").Append(F4(P95Ms)).Append(',');
            sb.Append("\"max_ms\":").Append(F4(MaxMs)).Append(',');
            sb.Append("\"nan_count\":").Append(NanCount).Append(',');
            sb.Append("\"mass_before\":").Append(F(MassBefore)).Append(',');
            sb.Append("\"mass_inflow\":").Append(F(MassInflow)).Append(',');
            sb.Append("\"mass_after\":").Append(F(MassAfter)).Append(',');
            sb.Append("\"hash\":").Append(Hash).Append(',');
            sb.Append("\"device\":\"").Append(Escape(Device)).Append("\",");
            sb.Append("\"gpu\":\"").Append(Escape(Gpu)).Append("\",");
            sb.Append("\"gpu_driver\":\"").Append(Escape(GpuDriver)).Append('"');
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// The mass balance error as a fraction, or 0 when there is nothing to
        /// balance. This is a BALANCE, not a constancy check: a field with an
        /// open source is supposed to gain water, so the expected value is
        /// before + inflow.
        /// </summary>
        public float MassDrift()
        {
            float expected = MassBefore + MassInflow;
            if (expected <= 0f)
            {
                return 0f;
            }
            return Math.Abs(MassAfter - expected) / expected;
        }

        /// <summary>
        /// Nearest-rank percentile over an already-sorted array.
        ///
        /// Deliberately not <c>Utils.GetPercentile</c> from the Performance
        /// Testing package. That method exists and is correct, but it takes a
        /// <c>List&lt;double&gt;</c> and lives in a package whose API surface has
        /// moved between major versions. Six lines here removes that dependency
        /// from the verdict path entirely.
        /// </summary>
        public static double Percentile(double[] sorted, double p)
        {
            if (sorted == null || sorted.Length == 0)
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
        /// Build a result from a raw sample array.
        ///
        /// <paramref name="solver"/> is the solver's own <c>Name</c>, passed in
        /// rather than read from a static, so the value recorded is the one the
        /// scenario actually ran against.
        /// </summary>
        public static ScenarioResult FromSamples(
            string scenario,
            string tier,
            string solver,
            int tileRes,
            int tileCount,
            float dt,
            double[] samples,
            int nanCount,
            float massBefore,
            float massInflow,
            float massAfter,
            uint hash)
        {
            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);

            return new ScenarioResult
            {
                Scenario = scenario,
                Tier = tier,
                Solver = solver,
                TileRes = tileRes,
                TileCount = tileCount,
                Dt = dt,
                Samples = samples.Length,
                P50Ms = Percentile(sorted, 50.0),
                P95Ms = Percentile(sorted, 95.0),
                MaxMs = sorted.Length > 0 ? sorted[sorted.Length - 1] : 0.0,
                NanCount = nanCount,
                MassBefore = massBefore,
                MassInflow = massInflow,
                MassAfter = massAfter,
                Hash = hash,
                Device = SystemInfo.deviceModel,
                Gpu = SystemInfo.graphicsDeviceName,
                GpuDriver = SystemInfo.graphicsDeviceVersion,
            };
        }

        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static string F4(double v) => v.ToString("F4", CultureInfo.InvariantCulture);

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "unknown";
            }
            return s.Replace("\\", "/").Replace("\"", "'");
        }
    }
}
