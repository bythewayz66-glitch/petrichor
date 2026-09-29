namespace PET.Water
{
    /// <summary>
    /// A point that adds water to the field at a fixed rate, up to a head limit.
    ///
    /// Sources are the ONLY water state that is persisted. The flow field itself
    /// is never saved: it is a pure function of terrain height plus source
    /// points, so it is re-derived on load. Storing it would add megabytes that
    /// must stay consistent with every terrain edit, forever.
    /// </summary>
    public struct WaterSource
    {
        public const byte KindSpring = 0;
        public const byte KindRain = 1;
        public const byte KindSeep = 2;

        /// <summary>Cell coordinate within the owning tile.</summary>
        public int X;
        public int Y;

        /// <summary>Inflow in cubic metres per second.</summary>
        public float Rate;

        /// <summary>Target depth the source will not push past, in metres.
        /// This is what makes a spring a spring rather than a hole in the world.</summary>
        public float Head;

        public byte Kind;

        public static WaterSource Spring(int x, int y, float rate, float head)
        {
            return new WaterSource { X = x, Y = y, Rate = rate, Head = head, Kind = KindSpring };
        }
    }
}
