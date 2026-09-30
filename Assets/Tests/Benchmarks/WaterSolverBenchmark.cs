using System;
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
    ///
    /// SOLVER LIFETIME
    /// ---------------
    /// Every test disposes its solver in a <c>finally</c>. This is not
    /// defensive habit: the rung-3 channel graph holds eight
    /// <c>Allocator.Persistent</c> arrays, and before this the harness never
    /// disposed a solver at all - four tests meant four leaked solver instances
    /// per run, which is a leak warning in the editor and a failure under a
    /// leak-detecting test run.
    ///
    /// The cast is required because <c>IWaterSolver</c> does not extend
    /// <c>IDisposable</c>, and the interface is deliberately not being changed:
    /// the reference heightfield solver allocates nothing and has nothing to
    /// dispose, so putting <c>IDisposable</c> on the interface would force every
    /// implementation to carry a method that is a no-op for the one that runs in
    /// the shipping game. <c>as IDisposable</c> disposes the solver that needs it
    /// and is a null-conditional no-op for the one that does not.
    /// </summary>
    public class WaterSolverBenchmark
    {
        /// <summary>
        /// The environment variable that selects the solver under test.
        ///
        /// Read once per test, in <see cref="MakeSolver"/>. Named alongside
        /// <c>PET_BENCH_TIER</c>, which the harness already reads, so the two
        /// knobs a run has are both environment variables and neither needs a
        /// code change to turn.
        /// </summary>
        public const string SolverEnvVar = "PET_BENCH_SOLVER";

        /// <summary>
        /// The solver under test. Constructed per test so no state leaks between
        /// scenarios - the reference heightfield solver is stateless, but the
        /// rung-3 channel graph is not, and the harness must be correct for both.
        ///
        /// SELECTION
        /// ---------
        /// The implementation is chosen by <see cref="SolverEnvVar"/> so the gate
        /// can measure a ladder rung without a code change. The default is the
        /// reference heightfield solver, so a run that does not set the variable
        /// constructs exactly the object it constructed before this method
        /// learned to choose - same type, same three arguments, same order.
        ///
        /// WHY AN UNKNOWN VALUE THROWS INSTEAD OF FALLING BACK
        /// ---------------------------------------------------
        /// A silent fallback would produce a report attributed to the wrong
        /// solver, and that is worse than no report at all. The entire purpose of
        /// the ladder is to tell the rungs apart; a run that quietly measured the
        /// reference solver while the operator believed it was measuring rung 3
        /// would send the project down a rung it never tested, and the numbers
        /// would look fine while doing it. A typo in an environment variable is
        /// cheap to fix and expensive to miss.
        ///
        /// The accepted values are the solvers' own <c>SolverName</c> constants
        /// rather than string literals, so a rename cannot leave this switch
        /// pointing at a name no solver answers to.
        /// </summary>
        private static IWaterSolver MakeSolver()
        {
            string requested = Environment.GetEnvironmentVariable(SolverEnvVar);

            if (string.IsNullOrEmpty(requested))
            {
                requested = HeightfieldWaterSolver.SolverName;
            }

            switch (requested)
            {
                case HeightfieldWaterSolver.SolverName:
                    return new HeightfieldWaterSolver(
                        BenchmarkScenarios.TileRes,
                        BenchmarkScenarios.CellSize,
                        BenchmarkScenarios.Dt);

                case ChannelGraphWaterSolver.SolverName:
                    return new ChannelGraphWaterSolver(
                        BenchmarkScenarios.TileRes,
                        BenchmarkScenarios.CellSize,
                        BenchmarkScenarios.Dt);

                default:
                    throw new ArgumentException(
                        $"{SolverEnvVar}='{requested}' is not a known solver. " +
                        $"Accepted values: '{HeightfieldWaterSolver.SolverName}', " +
                        $"'{ChannelGraphWaterSolver.SolverName}'. " +
                        "Refusing to fall back, because a report attributed to " +
                        "the wrong solver is worse than no report.");
            }
        }

        [Test, Performance]
        public void A_StaticSoak()
        {
            var solver = MakeSolver();
            try
            {
                BenchmarkHarness.Write(StaticSoakScenario.Run(solver, BenchmarkHarness.Tier));
            }
            finally
            {
                (solver as IDisposable)?.Dispose();
            }
        }

        [Test, Performance]
        public void B_StepFlood()
        {
            var solver = MakeSolver();
            try
            {
                BenchmarkHarness.Write(StepFloodScenario.Run(solver, BenchmarkHarness.Tier));
            }
            finally
            {
                (solver as IDisposable)?.Dispose();
            }
        }

        [Test, Performance]
        public void C_FastForward()
        {
            var solver = MakeSolver();
            try
            {
                BenchmarkHarness.Write(FastForwardScenario.Run(solver, BenchmarkHarness.Tier));
            }
            finally
            {
                (solver as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Not marked [Performance]: it has no budget, and marking it would put
        /// a meaningless timing in the editor's Performance Test Report next to
        /// three meaningful ones.
        /// </summary>
        [Test]
        public void D_Determinism()
        {
            var solver = MakeSolver();
            try
            {
                CorrectnessScenario.Run(solver);
            }
            finally
            {
                (solver as IDisposable)?.Dispose();
            }
        }
    }
}
