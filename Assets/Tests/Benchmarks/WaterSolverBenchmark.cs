using NUnit.Framework;
using Unity.PerformanceTesting;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// The week-one water gate, as NUnit entry points.
    ///
    /// This file is deliberately thin. All of the measurement, the correctness
    /// assertions and the JSON writing live in the scenario classes; this is the
    /// surface the Test Runner and <c>Tools/run_bench.sh</c> call.
    ///
    /// WHY THE SCENARIOS ARE SEPARATE CLASSES
    /// --------------------------------------
    /// Each scenario is a self-contained unit with its own parameters, its own
    /// threshold and its own summary file. Keeping them in one 400-line test
    /// class made it easy to change a parameter in one scenario and not notice
    /// it was shared with another - which is exactly the kind of drift that
    /// makes a gate's numbers stop meaning what the report says they mean.
    ///
    /// THE FOUR SCENARIOS
    /// ------------------
    ///   A. Static soak   - steady state, mass balance
    ///   B. Step flood    - the transient the solver must survive without stuttering
    ///   C. Fast-forward  - stability and per-step cost under the 3600x time toggle
    ///   D. Determinism   - the same inputs must produce the same field, twice
    ///
    /// A solver can pass A and B and fail C. They are not three measurements of
    /// the same thing.
    ///
    /// HARD FAILS ARE ASSERTED HERE, IN NUNIT
    /// --------------------------------------
    /// A NaN, a mass imbalance or a zero hash fails the NUnit run, so the failure
    /// is visible in the editor before the headless verdict is produced. They are
    /// NOT budget failures: the fallback ladder buys performance and cannot buy
    /// correctness. Halving the resolution of a broken solver produces a broken
    /// solver that is twice as fast.
    ///
    /// MEASUREMENT NOTE
    /// ----------------
    /// The authoritative numbers the checker reads are computed by the scenario
    /// classes with a Stopwatch, not read back out of the Performance Testing
    /// package. The package is still used - it drives the Test Runner
    /// integration and the editor's Performance Test Report - but its result
    /// format is an implementation detail that has changed between major
    /// versions, and a gate that breaks when a package updates is a gate that
    /// gets disabled. The numbers written to disk are produced by the same code
    /// that ran the measurement, so they cannot disagree with it.
    /// </summary>
    public class WaterSolverBenchmark
    {
        /// <summary>
        /// The solver under test. Constructed per test so no state leaks between
        /// scenarios - the reference heightfield solver is stateless, but the
        /// rung-3 channel graph is not, and the harness must be correct for both.
        /// </summary>
        private static IWaterSolver MakeSolver()
        {
            return new HeightfieldWaterSolver(
                BenchmarkScenarios.TileRes,
                BenchmarkScenarios.CellSize,
                BenchmarkScenarios.Dt);
        }

        [Test, Performance]
        public void A_StaticSoak()
        {
            BenchmarkHarness.Write(StaticSoakScenario.Run(MakeSolver(), BenchmarkHarness.Tier));
        }

        [Test, Performance]
        public void B_StepFlood()
        {
            BenchmarkHarness.Write(StepFloodScenario.Run(MakeSolver(), BenchmarkHarness.Tier));
        }

        [Test, Performance]
        public void C_FastForward()
        {
            BenchmarkHarness.Write(FastForwardScenario.Run(MakeSolver(), BenchmarkHarness.Tier));
        }

        /// <summary>
        /// Not marked [Performance]: it has no budget, and marking it would put
        /// a meaningless timing in the editor's Performance Test Report next to
        /// three meaningful ones.
        /// </summary>
        [Test]
        public void D_Determinism()
        {
            CorrectnessScenario.Run(MakeSolver());
        }
    }
}
