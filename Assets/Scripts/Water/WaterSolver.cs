using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PET.Water
{
    /// <summary>
    /// Heightfield flow solver.
    ///
    /// The step is a four-pass sweep over the tile:
    ///   1. clear flux
    ///   2. compute outgoing flux per edge from the head difference
    ///   3. LIMIT: a cell may not give away more water than it holds
    ///   4. apply (subtract outgoing, add incoming), then add sources
    ///
    /// Pass 3 is the whole reason this solver is stable. Without it a cell can
    /// hand out more water than it contains, which drives depth negative, and
    /// the next step divides by a negative depth - that is where the first NaN
    /// comes from.
    /// </summary>
    public static class WaterSolver
    {
        /// <summary>
        /// The window the four-argument <see cref="Step(WaterField, NativeArray{WaterSource}, float, NativeArray{float})"/>
        /// overload steps under. Ladder rung 2.
        ///
        /// WHY THIS IS A STATIC, AND WHAT IT COSTS
        /// ---------------------------------------
        /// <see cref="IWaterSolver.Step"/> takes no window, and the three
        /// committed solver implementations are frozen, so there is no parameter
        /// to thread a window through. This static is how the rung is MEASURED
        /// today; it is an ambient default, not an architecture.
        ///
        /// The cost is real and worth naming: a static is not thread-safe. It is
        /// correct for this gate, which steps one tile at a time on one thread,
        /// and INCORRECT for a scheduler that steps several tiles in parallel.
        /// The shipping multi-tile stepper must call the five-argument overload
        /// and pass its own window, so no tile can ever observe another tile's
        /// setting. Any job reading this static across threads is a bug.
        ///
        /// It is mutable rather than readonly because the harness sets it per
        /// scenario, and it defaults to unwindowed so an untouched process runs
        /// exactly the solver rungs 0 and 1 measured.
        /// </summary>
        public static WaterWindow ActiveWindow = WaterWindow.Unwindowed;

        /// <summary>Below this depth a cell is treated as dry and neither gives
        /// nor receives flux. Prevents a film of water from jittering forever.</summary>
        public const float MinDepth = 1e-5f;

        public const float Gravity = 9.81f;

        /// <summary>
        /// Per-step energy loss, applied to the FLUX and not to the depth.
        ///
        /// This distinction is the difference between a solver that conserves
        /// mass and one that does not. Damping the depth multiplies every cell
        /// by 0.995 each step, which is a global mass sink: over a 500-step
        /// measurement window the field would lose 92% of its water, and the
        /// mass-conservation assertion would be measuring the damping constant
        /// rather than the solver.
        ///
        /// Damping the flux loses energy instead, which is what friction
        /// physically does. Mass is then conserved by construction, because
        /// every unit that leaves one cell arrives at its neighbour.
        /// </summary>
        public const float Damping = 0.995f;

        /// <summary>
        /// FloatMode.Strict is deliberate. Fast-math would let the compiler
        /// reassociate float operations, which breaks within-device determinism,
        /// which breaks the save system.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
        public struct StepJob : IJob
        {
            public WaterField Field;
            public NativeArray<WaterSource> Sources;
            public float Dt;

            /// <summary>
            /// Length-1 accumulator for the volume of water the sources actually
            /// added this step, in cubic metres.
            ///
            /// This exists because a mass-conservation assertion is only
            /// meaningful once the inflow is accounted for. A field with an open
            /// source is NOT a closed system: its mass is supposed to grow. An
            /// assertion that mass stayed constant would fail on a perfectly
            /// correct solver, and pass vacuously on a dry one.
            ///
            /// It is a caller-owned array rather than a field on the job because
            /// IJob.Run() takes the job by value, so a plain float written inside
            /// Execute() would never be visible to the caller.
            /// </summary>
            public NativeArray<float> Inflow;

            /// <summary>
            /// Which part of the tile this step may write. Ladder rung 2.
            ///
            /// Default(WaterWindow) has Scope = None, and IsActiveCell returns
            /// true for every cell under Scope.None, so a job that never sets
            /// this field runs the unwindowed solver - which is what keeps rungs
            /// 0 and 1 reproducible from this source tree without a flag.
            ///
            /// The window is a field on the job and not a parameter of Execute
            /// for the same reason Inflow is: IJob.Run() takes the job by value,
            /// and this is read-only during the step.
            /// </summary>
            public WaterWindow Window;

            public void Execute()
            {
                int w = Field.Width;
                int h = Field.Height;
                float area = Field.CellArea;
                float dt = Dt;

                // ---- Pass 1: clear flux -------------------------------------
                for (int i = 0; i < Field.Flux.Length; i++)
                {
                    Field.Flux[i] = 0f;
                }

                // ---- Pass 2: outgoing flux from head difference -------------
                //
                // A cell outside the window is skipped BEFORE its depth is read,
                // so it publishes no outgoing flux. Because pass 1 cleared the
                // whole flux array, the boundary carries zero flux for the whole
                // step and no water crosses it. That is what makes a windowed
                // step a closed system over the active set, and it is why the
                // mass-balance assertion stays meaningful rather than merely
                // covering fewer cells.
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!Window.IsActiveCell(x, y))
                        {
                            continue;
                        }

                        int c = Field.Index(x, y);
                        float surface = Field.Depth[c];
                        if (surface <= MinDepth)
                        {
                            continue;
                        }

                        if (x + 1 < w)
                        {
                            float dh = surface - Field.Depth[Field.Index(x + 1, y)];
                            if (dh > 0f)
                            {
                                Field.Flux[Field.FluxIndex(x, y, WaterField.East)] =
                                    dh * Gravity * dt * Damping;
                            }
                        }
                        if (y + 1 < h)
                        {
                            float dh = surface - Field.Depth[Field.Index(x, y + 1)];
                            if (dh > 0f)
                            {
                                Field.Flux[Field.FluxIndex(x, y, WaterField.South)] =
                                    dh * Gravity * dt * Damping;
                            }
                        }
                        if (x - 1 >= 0)
                        {
                            float dh = surface - Field.Depth[Field.Index(x - 1, y)];
                            if (dh > 0f)
                            {
                                Field.Flux[Field.FluxIndex(x, y, WaterField.West)] =
                                    dh * Gravity * dt * Damping;
                            }
                        }
                        if (y - 1 >= 0)
                        {
                            float dh = surface - Field.Depth[Field.Index(x, y - 1)];
                            if (dh > 0f)
                            {
                                Field.Flux[Field.FluxIndex(x, y, WaterField.North)] =
                                    dh * Gravity * dt * Damping;
                            }
                        }
                    }
                }

                // ---- Pass 3: the flux limiter -------------------------------
                //
                // The limiter is the only pass whose cost depends on the field's
                // STATE rather than its size, which is why skipping cells here
                // is worth more than skipping them in pass 1. Outside the window
                // every flux is already zero, so the limiter would find nothing
                // to scale - the guard removes the scan, not the work.
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!Window.IsActiveCell(x, y))
                        {
                            continue;
                        }

                        int c = Field.Index(x, y);
                        float held = Field.Depth[c];
                        if (held <= MinDepth)
                        {
                            continue;
                        }

                        float totalOut = 0f;
                        for (int e = 0; e < WaterField.EdgeCount; e++)
                        {
                            totalOut += Field.Flux[Field.FluxIndex(x, y, e)];
                        }

                        if (totalOut > held)
                        {
                            float scale = held / totalOut;
                            for (int e = 0; e < WaterField.EdgeCount; e++)
                            {
                                int fi = Field.FluxIndex(x, y, e);
                                Field.Flux[fi] *= scale;
                            }
                        }
                    }
                }

                // ---- Pass 4: apply, then add sources ------------------------
                //
                // Skipping a windowed-out cell here is what HOLDS it at its last
                // state: the cell is simply never assigned. Because no flux
                // crossed the boundary, the delta it would have computed is zero
                // anyway - the guard removes a write that would have been a
                // no-op read-modify-write of the same value.
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!Window.IsActiveCell(x, y))
                        {
                            continue;
                        }

                        int c = Field.Index(x, y);
                        float delta = 0f;

                        for (int e = 0; e < WaterField.EdgeCount; e++)
                        {
                            delta -= Field.Flux[Field.FluxIndex(x, y, e)];
                        }

                        if (x - 1 >= 0)
                        {
                            delta += Field.Flux[Field.FluxIndex(x - 1, y, WaterField.East)];
                        }
                        if (x + 1 < w)
                        {
                            delta += Field.Flux[Field.FluxIndex(x + 1, y, WaterField.West)];
                        }
                        if (y - 1 >= 0)
                        {
                            delta += Field.Flux[Field.FluxIndex(x, y - 1, WaterField.South)];
                        }
                        if (y + 1 < h)
                        {
                            delta += Field.Flux[Field.FluxIndex(x, y + 1, WaterField.North)];
                        }

                        float next = Field.Depth[c] + delta;
                        Field.Depth[c] = next < 0f ? 0f : next;
                    }
                }

                float addedVolume = 0f;

                for (int i = 0; i < Sources.Length; i++)
                {
                    WaterSource s = Sources[i];
                    if (s.X < 0 || s.X >= w || s.Y < 0 || s.Y >= h)
                    {
                        continue;
                    }

                    // A source outside the window adds nothing this step. Its
                    // water is not lost - the source is a boundary condition,
                    // not a reservoir - but it IS withheld, which is the rung's
                    // stated cost: distant water stops updating. The spring
                    // resumes contributing the moment the camera comes within
                    // the window again.
                    if (!Window.IsActiveCell(s.X, s.Y))
                    {
                        continue;
                    }

                    int c = Field.Index(s.X, s.Y);
                    float before = Field.Depth[c];
                    float added = s.Rate * dt / area;
                    float next = before + added;
                    if (next > s.Head)
                    {
                        next = s.Head;
                    }
                    Field.Depth[c] = next;

                    // The ACTUAL volume added, after the head cap. Once the cell
                    // reaches its head the source stops contributing, which is
                    // exactly the behaviour the mass balance must reflect.
                    addedVolume += (next - before) * area;
                }

                if (Inflow.IsCreated && Inflow.Length > 0)
                {
                    Inflow[0] = addedVolume;
                }
            }
        }

        /// <summary>
        /// Advance the field one step and report the volume the sources added.
        ///
        /// The inflow array is caller-owned and reused across steps so the hot
        /// loop allocates nothing. Pass a length-1 NativeArray allocated once.
        /// </summary>
        public static float Step(
            WaterField field,
            NativeArray<WaterSource> sources,
            float dt,
            NativeArray<float> inflow)
        {
            return Step(field, sources, dt, inflow, ActiveWindow);
        }

        /// <summary>
        /// Advance the field one step, writing only the cells the window admits.
        ///
        /// Ladder rung 2 enters here. Passing
        /// <see cref="WaterWindow.Unwindowed"/> is behaviourally identical to the
        /// four-argument overload, which is the property that lets rungs 0, 1
        /// and 2 be measured from one source tree.
        /// </summary>
        public static float Step(
            WaterField field,
            NativeArray<WaterSource> sources,
            float dt,
            NativeArray<float> inflow,
            WaterWindow window)
        {
            new StepJob
            {
                Field = field,
                Sources = sources,
                Dt = dt,
                Inflow = inflow,
                Window = window,
            }.Run();
            return inflow.IsCreated && inflow.Length > 0 ? inflow[0] : 0f;
        }

        /// <summary>
        /// Convenience overload for one-off calls. Allocates a temporary, so it
        /// is NOT for the benchmark's hot loop - use the overload above there.
        /// </summary>
        public static float Step(WaterField field, NativeArray<WaterSource> sources, float dt)
        {
            var inflow = new NativeArray<float>(1, Allocator.Temp);
            try
            {
                return Step(field, sources, dt, inflow);
            }
            finally
            {
                inflow.Dispose();
            }
        }
    }
}
