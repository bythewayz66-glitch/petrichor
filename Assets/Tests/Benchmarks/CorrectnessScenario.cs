using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// Scenario D - within-device determinism.
    ///
    /// WHAT IT MEASURES
    /// ----------------
    /// Nothing about speed. It answers one question: given identical inputs and
    /// an identical starting field, does the solver produce a bit-identical
    /// field twice on the same device?
    ///
    /// WHY THE SAVE SYSTEM DEPENDS ON IT
    /// ---------------------------------
    /// The flow field is never saved. It is a pure function of terrain height
    /// plus source points, so it is re-derived on load. That design is what
    /// keeps the save small, and it is only valid if the derivation is
    /// deterministic - a device that produces a different field from identical
    /// inputs would load a different world than it saved, and the player would
    /// see their river in a different place.
    ///
    /// CROSS-DEVICE BIT-IDENTITY IS NOT ASSERTED
    /// -----------------------------------------
    /// Burst compiles to different SIMD widths on x86-64 and ARM64, so demanding
    /// cross-device bit-identity would be demanding something the toolchain does
    /// not offer. The claim is within-device, and the test is written to match
    /// the claim rather than to look stronger than it is.
    ///
    /// WHY IT IS A TEST AND NOT A HASH COMPARISON
    /// ------------------------------------------
    /// Asserting that the hash is non-zero proves nothing - a constant field
    /// hashes non-zero. The only way to actually test determinism is to run the
    /// same sequence twice from the same start and compare, which is what this
    /// does. The two fields are allocated separately so a shared buffer cannot
    /// make the comparison trivially true.
    ///
    /// REPORTS INTO
    /// ------------
    /// A red NUnit test case, which <c>check_thresholds.py</c> treats as a HARD
    /// FAIL regardless of the timing numbers. There is no summary JSON: this
    /// scenario has no budget, and writing one would invite someone to compare
    /// its numbers against a threshold that does not exist.
    /// </summary>
    public static class CorrectnessScenario
    {
        public const string Name = "D_Determinism";

        private const int Steps = 500;

        /// <summary>
        /// Run the determinism check. Throws on failure via NUnit assertions, so
        /// the failure is visible in the editor before the headless verdict is
        /// produced.
        /// </summary>
        public static void Run(IWaterSolver solver)
        {
            BenchmarkScenarios.BeginWindowed();
            try
            {
                RunCore(solver);
            }
            finally
            {
                BenchmarkScenarios.EndWindowed();
            }
        }

        private static void RunCore(IWaterSolver solver)
        {
            using var a = BenchmarkScenarios.MakeField();
            using var b = BenchmarkScenarios.MakeField();
            using var sources = BenchmarkScenarios.MakeSources();
            using var inflow = new NativeArray<float>(1, Allocator.Persistent);

            for (int i = 0; i < Steps; i++)
            {
                solver.Step(a, sources, inflow);
            }

            // Replay the identical sequence from an identical start. A fresh
            // solver instance is used so any state the implementation carries
            // between steps is reset - the reference heightfield solver is
            // stateless, but the rung-3 channel graph is not, and this test must
            // be meaningful for both.
            solver.Reset();

            for (int i = 0; i < Steps; i++)
            {
                solver.Step(b, sources, inflow);
            }

            uint hashA = a.Hash();
            uint hashB = b.Hash();

            Assert.AreNotEqual(
                0u, hashA,
                $"{Name}: field hash collapsed to zero. HARD FAIL.");

            Assert.AreEqual(
                hashA, hashB,
                $"{Name}: identical inputs produced different fields " +
                $"({hashA} vs {hashB}). Within-device determinism is broken, which " +
                $"breaks the save system. HARD FAIL.");

            // The two fields must also agree on mass, not just on hash. A hash
            // collision is unlikely but a hash over a different buffer would be
            // a silent pass, and comparing mass is a cheap second opinion.
            Assert.AreEqual(
                a.TotalMass(), b.TotalMass(), 1e-6f,
                $"{Name}: fields hash equal but differ in mass. HARD FAIL.");

            Debug.Log(
                $"[PET.Benchmarks] {Name} OK - hash {hashA} reproduced exactly " +
                $"over {Steps} steps, mass {a.TotalMass():R} m3.");
        }
    }
}
