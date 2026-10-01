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
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
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
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
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
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
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
            new StepJob { Field = field, Sources = sources, Dt = dt, Inflow = inflow }.Run();
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
