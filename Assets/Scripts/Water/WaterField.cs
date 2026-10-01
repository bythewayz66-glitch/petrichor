using System;
using Unity.Collections;
using Unity.Mathematics;

namespace PET.Water
{
    /// <summary>
    /// A heightfield water surface over a single terrain tile.
    ///
    /// Layout is tile-major and contiguous:
    ///   Depth : Width * Height floats, row-major (y * Width + x)
    ///   Flux  : Width * Height * 4 floats, the four edge fluxes (E, S, W, N)
    ///           stored consecutively per cell.
    ///
    /// The layout is chosen for cache behaviour and Burst vectorisation. It has
    /// NOT been profiled. If the week-one gate comes back over budget, the
    /// layout is a legitimate thing to change before walking the fallback ladder.
    ///
    /// Depth is a datum offset, not a bed-relative depth: the terrain bed is
    /// folded in by the caller so that a single subtraction gives the head
    /// difference between two neighbouring cells.
    /// </summary>
    public struct WaterField : IDisposable
    {
        public const int EdgeCount = 4;
        public const int East = 0;
        public const int South = 1;
        public const int West = 2;
        public const int North = 3;

        public int Width;
        public int Height;
        public float CellSize;
        public float CellArea;

        public NativeArray<float> Depth;
        public NativeArray<float> Flux;

        public int CellCount => Width * Height;

        public static WaterField Allocate(int width, int height, float cellSize, Allocator allocator)
        {
            return new WaterField
            {
                Width = width,
                Height = height,
                CellSize = cellSize,
                CellArea = cellSize * cellSize,
                Depth = new NativeArray<float>(width * height, allocator, NativeArrayOptions.ClearMemory),
                Flux = new NativeArray<float>(width * height * EdgeCount, allocator, NativeArrayOptions.ClearMemory),
            };
        }

        public int Index(int x, int y) => y * Width + x;

        public int FluxIndex(int x, int y, int edge) => (y * Width + x) * EdgeCount + edge;

        /// <summary>Total water volume in cubic metres. Summed in double to keep
        /// the drift assertion meaningful over a long soak.</summary>
        public float TotalMass()
        {
            double sum = 0.0;
            for (int i = 0; i < Depth.Length; i++)
            {
                sum += Depth[i];
            }
            return (float)(sum * CellArea);
        }

        /// <summary>Count of non-finite depth samples. Any non-zero value is a
        /// hard fail, not a budget failure.</summary>
        public int NaNCount()
        {
            int n = 0;
            for (int i = 0; i < Depth.Length; i++)
            {
                float d = Depth[i];
                if (float.IsNaN(d) || float.IsInfinity(d))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// FNV-1a over the raw depth buffer.
        ///
        /// This asserts WITHIN-DEVICE determinism only, which is what the save
        /// system depends on: water is re-derived from terrain height and source
        /// points on load, so the same device must produce the same field from
        /// the same inputs. Cross-device bit-identity is explicitly NOT promised
        /// - Burst compiles to different SIMD widths on x86-64 and ARM64.
        /// </summary>
        public uint Hash()
        {
            const uint FnvOffset = 2166136261u;
            const uint FnvPrime = 16777619u;

            uint h = FnvOffset;
            for (int i = 0; i < Depth.Length; i++)
            {
                uint bits = math.asuint(Depth[i]);
                h = (h ^ (bits & 0xFFu)) * FnvPrime;
                h = (h ^ ((bits >> 8) & 0xFFu)) * FnvPrime;
                h = (h ^ ((bits >> 16) & 0xFFu)) * FnvPrime;
                h = (h ^ ((bits >> 24) & 0xFFu)) * FnvPrime;
            }
            return h;
        }

        /// <summary>
        /// Copy the depth buffer into another field of identical dimensions.
        ///
        /// Used by the harness to snapshot a state and replay from it, which is
        /// the only way to actually TEST within-device determinism rather than
        /// merely assert that the hash is non-zero.
        /// </summary>
        public void CopyTo(WaterField dst)
        {
            if (dst.Width != Width || dst.Height != Height)
            {
                throw new System.ArgumentException(
                    "WaterField.CopyTo: dimension mismatch (" + Width + "x" + Height +
                    " -> " + dst.Width + "x" + dst.Height + ")");
            }
            NativeArray<float>.Copy(Depth, dst.Depth, Depth.Length);
        }

        public void Dispose()
        {
            if (Depth.IsCreated)
            {
                Depth.Dispose();
            }
            if (Flux.IsCreated)
            {
                Flux.Dispose();
            }
        }
    }
}
