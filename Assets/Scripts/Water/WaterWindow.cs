namespace PET.Water
{
    /// <summary>
    /// Which part of the world a solver step is allowed to touch.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// Ladder rung 2 is "shrink the active water window to the tiles within 1 of
    /// the camera". That is a statement about WHICH TILES step, not about how
    /// expensive a single tile is, and the two must not be conflated:
    ///
    ///   * Scope = Tiles  - tile SELECTION. A tile steps only when it lies
    ///                      within Radius tiles of the camera's tile. Within a
    ///                      stepped tile every cell steps, so the per-tile step
    ///                      cost is UNCHANGED. What falls is the number of
    ///                      tiles stepped per frame. This is rung 2 as the
    ///                      ladder names it.
    ///
    ///   * Scope = Cells  - cell MASKING inside a tile. Only cells within
    ///                      Radius cells of the camera cell step; the rest hold
    ///                      their last depth. This DOES move the per-tile step
    ///                      cost, and it is what a per-tile benchmark can
    ///                      actually measure.
    ///
    /// Keeping both in one type is deliberate. Rung 2 needs the tile notion to
    /// be measured at all, and the cell notion to be measurable in the harness
    /// the project already has, which allocates ONE tile per scenario.
    ///
    /// WHAT "HELD AT ITS LAST STATE" MEANS
    /// -----------------------------------
    /// A cell outside the window is not written by any of the four passes and
    /// receives no source water. Its depth is therefore exactly what it was at
    /// the end of the previous step. Forward flux is cleared globally at the
    /// start of the step, and a windowed cell never re-populates its outgoing
    /// flux, so no flux crosses the window boundary: for the duration of the
    /// step the boundary is impermeable. A windowed step is therefore a closed
    /// system over the active set, which is what keeps the mass-balance
    /// assertion meaningful rather than merely smaller.
    ///
    /// DATA-DRIVEN BY CONSTRUCTION
    /// ---------------------------
    /// Nothing here is a compile-time constant chosen for one rung. The scope,
    /// the radius and the origin all come from configuration, and Scope = None
    /// is the identity: every predicate returns true and the solver takes a
    /// code path that is behaviourally identical to the unwindowed one. That is
    /// what keeps rungs 0 and 1 reproducible from the same source tree.
    /// </summary>
    public enum WaterWindowScope
    {
        /// <summary>No window. Every cell of every tile steps. Rungs 0 and 1.</summary>
        None = 0,

        /// <summary>A tile steps only when it is within Radius tiles of the camera's tile.</summary>
        Tiles = 1,

        /// <summary>Only cells within Radius cells of the camera's cell step, inside any tile.</summary>
        Cells = 2,
    }

    /// <summary>
    /// A Chebyshev-radius window. Chebyshev and not Euclidean because the world
    /// is a grid of square tiles: "within 1 tile" has to mean the eight
    /// neighbours, or a diagonal tile would be excluded while a tile twice as
    /// far away along an axis was included.
    /// </summary>
    public struct WaterWindow
    {
        public WaterWindowScope Scope;

        /// <summary>Radius in window units: tiles for <see cref="WaterWindowScope.Tiles"/>, cells for <see cref="WaterWindowScope.Cells"/>.</summary>
        public int Radius;

        /// <summary>Camera origin: a tile coordinate for tile scope, a cell coordinate for cell scope.</summary>
        public int OriginX;
        public int OriginY;

        public bool IsWindowed => Scope != WaterWindowScope.None;

        public static WaterWindow Unwindowed =>
            new WaterWindow { Scope = WaterWindowScope.None, Radius = 0, OriginX = 0, OriginY = 0 };

        public static WaterWindow ForTiles(int radius, int cameraTileX, int cameraTileY)
        {
            return new WaterWindow
            {
                Scope = WaterWindowScope.Tiles,
                Radius = radius,
                OriginX = cameraTileX,
                OriginY = cameraTileY,
            };
        }

        public static WaterWindow ForCells(int radius, int cameraCellX, int cameraCellY)
        {
            return new WaterWindow
            {
                Scope = WaterWindowScope.Cells,
                Radius = radius,
                OriginX = cameraCellX,
                OriginY = cameraCellY,
            };
        }

        /// <summary>
        /// Whether the cell at (x, y) inside its tile may be written this step.
        ///
        /// Called four times per cell per step, so it is written as a branch on
        /// an enum rather than anything polymorphic - and the unwindowed case is
        /// the first arm, so the rung-0/1 path costs one predictable compare and
        /// nothing else.
        /// </summary>
        public bool IsActiveCell(int x, int y)
        {
            switch (Scope)
            {
                case WaterWindowScope.Cells:
                    return Abs(x - OriginX) <= Radius && Abs(y - OriginY) <= Radius;

                // A selected tile is stepped in full. Tile selection is decided
                // one level up, by IsActiveTile, because a solver stepping one
                // tile cannot see the tile grid it belongs to.
                case WaterWindowScope.Tiles:
                case WaterWindowScope.None:
                default:
                    return true;
            }
        }

        /// <summary>
        /// Whether a whole tile is stepped this frame. Returns true unless the
        /// scope is <see cref="WaterWindowScope.Tiles"/>, so a caller that only
        /// wants cell masking does not have to special-case anything.
        /// </summary>
        public bool IsActiveTile(int tileX, int tileY)
        {
            if (Scope != WaterWindowScope.Tiles)
            {
                return true;
            }
            return Abs(tileX - OriginX) <= Radius && Abs(tileY - OriginY) <= Radius;
        }

        /// <summary>
        /// How many tiles step when the camera's tile is (cameraTileX,
        /// cameraTileY) in a world of tilesPerSide x tilesPerSide.
        ///
        /// The clamp is the whole reason this is a method and not the naive
        /// (2r+1)^2. A camera in a corner of the world has no neighbours outside
        /// it, so the active set is clipped by the world edge - and a model that
        /// ignored the clamp would overstate the saving. At r=1 the steady-state
        /// answer is 9 (all eight neighbours) and the corner answer is 4.
        /// </summary>
        public static int ActiveTileCount(int tilesPerSide, int radius, int cameraTileX, int cameraTileY)
        {
            int xs = Clamp(cameraTileX - radius, 0, tilesPerSide - 1);
            int xe = Clamp(cameraTileX + radius, 0, tilesPerSide - 1);
            int ys = Clamp(cameraTileY - radius, 0, tilesPerSide - 1);
            int ye = Clamp(cameraTileY + radius, 0, tilesPerSide - 1);
            return (xe - xs + 1) * (ye - ys + 1);
        }

        /// <summary>
        /// The largest active set any camera position can produce, which is the
        /// number a frame budget has to be met at. A tolerance that holds only
        /// in the middle of the map is not a budget.
        /// </summary>
        public static int MaxActiveTileCount(int tilesPerSide, int radius)
        {
            int best = 0;
            for (int y = 0; y < tilesPerSide; y++)
            {
                for (int x = 0; x < tilesPerSide; x++)
                {
                    int n = ActiveTileCount(tilesPerSide, radius, x, y);
                    if (n > best)
                    {
                        best = n;
                    }
                }
            }
            return best;
        }

        private static int Abs(int v) => v < 0 ? -v : v;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
