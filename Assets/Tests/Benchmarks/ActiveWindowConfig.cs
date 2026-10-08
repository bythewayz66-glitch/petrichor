using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using PET.Water;

namespace PET.Benchmarks
{
    /// <summary>
    /// Ladder rung 2 configuration, read from the environment like every other
    /// rung switch in this harness.
    ///
    /// WHY ENVIRONMENT VARIABLES AND NOT CONSTANTS
    /// -------------------------------------------
    /// A rung is a change to the BUILD UNDER TEST. If applying it means editing
    /// a source file, then measuring it and reverting it are two different
    /// states of the working tree, and the gate report cannot say which tree
    /// produced which number. Reading the rung from the environment means rungs
    /// 0, 1 and 2 are all measured from one commit, and the summary records the
    /// rung it actually ran at.
    ///
    /// STRICT VALIDATION, NO FALLBACK
    /// ------------------------------
    /// An unrecognised scope throws, exactly as an unrecognised solver and an
    /// invalid resolution do. A silent fallback would produce a report
    /// attributed to a rung that never ran - the same defect the solver switch
    /// already refuses, for the same reason.
    ///
    /// THE WINDOW IS SET ON THE SOLVER AS AN AMBIENT STATIC
    /// ---------------------------------------------------
    /// <see cref="IWaterSolver.Step"/> takes no window, and the three committed
    /// solver files are frozen, so there is no parameter to thread it through.
    /// <see cref="WaterSolver.ActiveWindow"/> is therefore a static that the
    /// harness sets once per scenario, and <see cref="WaterSolver"/>'s
    /// four-argument overload reads it.
    ///
    /// That is a deliberate trade and it has a real cost: a static is not
    /// thread-safe, so it is correct for a single-threaded gate that steps one
    /// tile at a time and incorrect for a scheduler that steps several tiles in
    /// parallel. The shipping multi-tile stepper must use
    /// <see cref="WaterSolver.Step(WaterField, Unity.Collections.NativeArray{WaterSource}, float, Unity.Collections.NativeArray{float}, WaterWindow)"/>,
    /// which takes the window explicitly and ignores the static. The static
    /// exists so the rung can be MEASURED today; the overload is the API the
    /// game will use.
    /// </summary>
    public static class ActiveWindowConfig
    {
        public const string ScopeEnvVar = "PET_BENCH_WINDOW_SCOPE";
        public const string RadiusEnvVar = "PET_BENCH_WINDOW_RADIUS";

        /// <summary>
        /// Tiles per side in the shipping slice, from the Deliverable 2 world
        /// concept: 8x8 tiles of 250 m is a 2 km x 2 km world. This is NOT the
        /// benchmark's own tile count - the harness allocates one tile and
        /// reports <see cref="BenchmarkScenarios.TileCount"/> = 4 as the
        /// shipping frame's tile count. It is here so the tile-grid arithmetic
        /// is computed from the real world size rather than from a number
        /// chosen to make the result look good.
        /// </summary>
        public const int SliceWorldTilesPerSide = 8;

        /// <summary>Default radius: the rung's own "within 1 of the camera".</summary>
        public const int DefaultRadius = 1;

        public static WaterWindowScope Scope
        {
            get
            {
                string requested = Environment.GetEnvironmentVariable(ScopeEnvVar);
                if (string.IsNullOrEmpty(requested))
                {
                    return WaterWindowScope.None;
                }

                switch (requested.Trim().ToLowerInvariant())
                {
                    case "none":  return WaterWindowScope.None;
                    case "tiles": return WaterWindowScope.Tiles;
                    case "cells": return WaterWindowScope.Cells;
                    default:
                        throw new ArgumentException(
                            $"{ScopeEnvVar}='{requested}' is not a known window scope. " +
                            "Accepted values: 'none', 'tiles', 'cells'. Refusing to fall " +
                            "back, because a report attributed to a rung that never ran " +
                            "is worse than no report.");
                }
            }
        }

        public static int Radius
        {
            get
            {
                string requested = Environment.GetEnvironmentVariable(RadiusEnvVar);
                if (string.IsNullOrEmpty(requested))
                {
                    return DefaultRadius;
                }

                int value;
                if (!int.TryParse(requested, out value))
                {
                    throw new ArgumentException(
                        $"{RadiusEnvVar}='{requested}' is not an integer. Refusing to " +
                        "fall back, because a run reported at a radius it was not asked " +
                        "to measure at describes nothing.");
                }

                if (value < 1)
                {
                    throw new ArgumentException(
                        $"{RadiusEnvVar}={value} is not a usable radius. The window is a " +
                        "Chebyshev neighbourhood, so a radius below 1 is an empty world " +
                        "and not a narrower window - use " +
                        $"{ScopeEnvVar}=none to disable the window entirely.");
                }

                return value;
            }
        }

        /// <summary>
        /// The window this run measures at.
        ///
        /// The origin defaults to the centre of a single tile (tile scope) or the
        /// centre of the tile buffer (cell scope), which is the position that
        /// MAXIMISES the active set for radius 1 - a budget met only when the
        /// camera is in the middle of the world is not a budget, so the default
        /// origin is the pessimistic one, not the flattering one.
        /// </summary>
        public static WaterWindow Current
        {
            get
            {
                WaterWindowScope scope = Scope;
                int radius = Radius;
                int res = BenchmarkScenarios.TileRes;

                switch (scope)
                {
                    case WaterWindowScope.Tiles:
                        // One benchmark tile in a 2x2 world: the centre tile has
                        // all three neighbours inside the world, so radius 1
                        // activates all four. Stated explicitly because it means
                        // the benchmark world CANNOT show rung 2's tile saving -
                        // see ActiveTilesMax and the note in the report.
                        return WaterWindow.ForTiles(radius, 0, 0);

                    case WaterWindowScope.Cells:
                        int mid = res / 2;
                        return WaterWindow.ForCells(radius, mid, mid);

                    case WaterWindowScope.None:
                    default:
                        return WaterWindow.Unwindowed;
                }
            }
        }

        /// <summary>
        /// Cells the window admits inside one tile, or -1 when the run is not
        /// cell-windowed. Reported so the fraction of the tile that actually
        /// stepped is a recorded fact rather than an inference.
        /// </summary>
        public static int ActiveCellCount
        {
            get
            {
                if (Scope != WaterWindowScope.Cells)
                {
                    return -1;
                }

                int res = BenchmarkScenarios.TileRes;
                int radius = Radius;
                int mid = res / 2;

                int xs = Clamp(mid - radius, 0, res - 1);
                int xe = Clamp(mid + radius, 0, res - 1);
                return (xe - xs + 1) * (xe - xs + 1);
            }
        }

        /// <summary>
        /// The largest active-tile set any camera position in the shipping slice
        /// can produce. This is the number a frame budget must be met at.
        /// </summary>
        public static int ActiveTilesMax =>
            WaterWindow.MaxActiveTileCount(SliceWorldTilesPerSide, Radius);

        /// <summary>The active-tile set at the world corner: the best case.</summary>
        public static int ActiveTilesCorner =>
            WaterWindow.ActiveTileCount(SliceWorldTilesPerSide, Radius, 0, 0);

        public static int SliceWorldTiles => SliceWorldTilesPerSide * SliceWorldTilesPerSide;

        /// <summary>
        /// Push the configured window into the solver's ambient static and
        /// return it, so a scenario can set and report in one call.
        ///
        /// Called once at the top of each scenario's Run, BEFORE the settle loop
        /// - windowing only the measured window and not the settle would measure
        /// a field that was never in the state the run claims to measure.
        /// </summary>
        public static WaterWindow Apply()
        {
            WaterWindow window = Current;
            WaterSolver.ActiveWindow = window;
            return window;
        }

        /// <summary>Restore the unwindowed solver. Called in each scenario's
        /// finally, so one scenario's rung cannot leak into the next one's.</summary>
        public static void Restore()
        {
            WaterSolver.ActiveWindow = WaterWindow.Unwindowed;
        }

        /// <summary>
        /// The window block of the run summary, as JSON.
        ///
        /// WHY A SEPARATE SUMMARY
        /// ----------------------
        /// <c>Tools/check_thresholds.py</c> reads exactly four scenario files, by
        /// name, and refuses a directory whose summaries disagree about which
        /// solver ran. Adding window keys to a scenario summary would put
        /// fields into a file the checker parses field-by-field. A separate file
        /// the checker never opens carries the run's configuration without
        /// touching the verdict path at all.
        /// </summary>
        public static string ToJson(string tier, string solver)
        {
            var sb = new StringBuilder(768);
            var inv = CultureInfo.InvariantCulture;
            WaterWindowScope scope = Scope;
            int radius = Radius;
            int res = BenchmarkScenarios.TileRes;

            sb.Append('{');
            sb.Append("\"scenario\":\"RunSummary\",");
            sb.Append("\"tier\":\"").Append(tier).Append("\",");
            // Present so a future checker that scans every summary can still
            // attribute this file to the solver that produced it.
            sb.Append("\"solver\":\"").Append(solver).Append("\",");
            sb.Append("\"tile_res\":").Append(res).Append(',');
            sb.Append("\"tile_count\":").Append(BenchmarkScenarios.TileCount).Append(',');

            sb.Append("\"window\":{");
            sb.Append("\"scope\":\"").Append(scope.ToString().ToLowerInvariant()).Append("\",");
            sb.Append("\"radius\":").Append(radius).Append(',');
            sb.Append("\"units\":\"").Append(scope == WaterWindowScope.Cells ? "cells" : "tiles").Append("\",");
            sb.Append("\"origin_x\":").Append(Current.OriginX).Append(',');
            sb.Append("\"origin_y\":").Append(Current.OriginY).Append(',');

            if (scope == WaterWindowScope.Cells)
            {
                int active = ActiveCellCount;
                int total = res * res;
                sb.Append("\"active_cells\":").Append(active).Append(',');
                sb.Append("\"cells_per_tile\":").Append(total).Append(',');
                sb.Append("\"active_cell_fraction\":")
                  .Append((total > 0 ? (active / (double)total) : 0.0).ToString("R", inv));
            }
            else
            {
                sb.Append("\"active_cells\":null,");
                sb.Append("\"cells_per_tile\":").Append(res * res).Append(',');
                sb.Append("\"active_cell_fraction\":1");
            }

            sb.Append('}').Append(',');

            sb.Append("\"tile_grid\":{");
            sb.Append("\"tiles_per_side\":").Append(SliceWorldTilesPerSide).Append(',');
            sb.Append("\"world_tiles\":").Append(SliceWorldTiles).Append(',');
            sb.Append("\"radius_tiles\":").Append(radius).Append(',');
            sb.Append("\"active_tiles_max\":").Append(ActiveTilesMax).Append(',');
            sb.Append("\"active_tiles_corner\":").Append(ActiveTilesCorner).Append(',');
            sb.Append("\"active_tile_fraction_max\":")
              .Append((ActiveTilesMax / (double)SliceWorldTiles).ToString("R", inv)).Append(',');
            sb.Append("\"tile_reduction_factor_max\":")
              .Append((SliceWorldTiles / (double)ActiveTilesMax).ToString("R", inv)).Append(',');
            sb.Append("\"benchmark_tiles_per_side\":2,");
            sb.Append("\"benchmark_active_tiles\":")
              .Append(WaterWindow.ActiveTileCount(2, radius, 0, 0)).Append(',');
            sb.Append("\"benchmark_tile_reduction_factor\":1");
            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
        }

        public static void WriteSummary(string tier, string solver)
        {
            string dir = BenchmarkHarness.OutputDir;
            Directory.CreateDirectory(dir);
            string json = ToJson(tier, solver);
            File.WriteAllText(Path.Combine(dir, "summary_RunSummary.json"), json);
            Debug.Log($"PET_BENCH {json}");
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
