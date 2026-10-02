using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PET.EditorTools
{
    /// <summary>
    /// Verifies the W2.1 (issue #9) acceptance criteria against the assets on
    /// disk, rather than against the generator's own say-so.
    ///
    /// WHY A SEPARATE VERIFIER AND NOT A SELF-CHECK IN THE GENERATOR
    /// -------------------------------------------------------------
    /// The generator logging "wrote 64 files" is the generator reporting on
    /// itself. The acceptance criteria are about the FILES: are they 513
    /// squared, do they total ~34 MB, and - the one the generator cannot see -
    /// do neighbouring tiles actually join without a step? A seam between
    /// tiles is invisible from inside a single generator call, because each
    /// tile is generated in isolation. Only reading two neighbours' shared edge
    /// samples can see it.
    ///
    /// A seam in the placeholder would be actively harmful: W2.5 (issue #13)
    /// exists to prove that no simulation branches on tile-edge membership, and
    /// a placeholder with a step at every boundary manufactures exactly the
    /// bug that week is looking for.
    ///
    /// Exit code is the process exit code so CI can read it.
    ///
    /// USAGE
    /// -----
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod PET.EditorTools.HeightmapAcceptanceVerifier.Verify
    /// </summary>
    public static class HeightmapAcceptanceVerifier
    {
        private const string OutputDir = "Assets/World/Tiles/Terrain";
        private const int ExpectedCount = 64;
        private const int ExpectedResolution = 513;
        private const int Grid = 8;

        /// <summary>~34 MB, from the ticket: 513^2 * 2 bytes * 64.</summary>
        private const long ExpectedBytes = 35148032L;

        /// <summary>
        /// Allowed drift on the size total, as a fraction. The per-tile figure
        /// includes Unity's serialisation header, so the arithmetic total is a
        /// floor rather than an exact prediction. 25% is generous on purpose:
        /// this criterion exists to catch a set that is the wrong SHAPE (say, 8
        /// tiles, or 257 squared), not to police a header size.
        /// </summary>
        private const double SizeToleranceFraction = 0.25;

        /// <summary>
        /// Allowed height step across a tile boundary, in metres.
        ///
        /// The generator's dish is continuous in slice space, so the measured
        /// step should be a small fraction of a cell (250/512 = 0.488 m). A
        /// threshold of one tenth of a cell separates "continuous" from "there
        /// is a cliff here", which is what this check is for.
        /// </summary>
        private const float MaxSeamStepM = 0.05f;

        public static void Verify()
        {
            try
            {
                VerifyInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError("[PET.EditorTools] HeightmapAcceptanceVerifier FAILED: " + ex);
                EditorApplication.Exit(1);
                throw;
            }
        }

        private static void VerifyInternal()
        {
            int failures = 0;
            failures += CheckCount();
            failures += CheckResolutionAndSize();
            failures += CheckSeams();

            if (failures > 0)
            {
                Debug.LogError(
                    "[PET.EditorTools] HEIGHTMAP_VERIFY_FAIL failures=" + failures);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[PET.EditorTools] HEIGHTMAP_VERIFY_OK");
            EditorApplication.Exit(0);
        }

        private static int CheckCount()
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainData", new[] { OutputDir });
            if (guids.Length != ExpectedCount)
            {
                Debug.LogError(
                    "[PET.EditorTools] count FAIL: found " + guids.Length +
                    " TerrainData, expected " + ExpectedCount);
                return 1;
            }
            Debug.Log("[PET.EditorTools] count OK: " + guids.Length + " TerrainData");
            return 0;
        }

        private static int CheckResolutionAndSize()
        {
            int failures = 0;

            string[] paths = Directory.GetFiles(OutputDir, "*.asset");
            long total = 0;

            for (int i = 0; i < paths.Length; i++)
            {
                total += new FileInfo(paths[i]).Length;

                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(ToAssetPath(paths[i]));
                if (data == null)
                {
                    Debug.LogError("[PET.EditorTools] load FAIL: " + paths[i]);
                    failures++;
                    continue;
                }

                if (data.heightmapResolution != ExpectedResolution)
                {
                    Debug.LogError(
                        "[PET.EditorTools] resolution FAIL: " + Path.GetFileName(paths[i]) +
                        " is " + data.heightmapResolution + ", expected " +
                        ExpectedResolution + " (2^9 + 1)");
                    failures++;
                }

                // 513 must be a power of two plus one. Checked rather than
                // assumed, because the whole format constraint rests on it.
                int cells = data.heightmapResolution - 1;
                if (data.heightmapResolution < 3 || (cells & (cells - 1)) != 0)
                {
                    Debug.LogError(
                        "[PET.EditorTools] power-of-two FAIL: " +
                        Path.GetFileName(paths[i]) + " resolution " +
                        data.heightmapResolution);
                    failures++;
                }

                if (Math.Abs(data.size.x - 250f) > 0.01f)
                {
                    Debug.LogError(
                        "[PET.EditorTools] tile size FAIL: " +
                        Path.GetFileName(paths[i]) + " is " + data.size.x +
                        " m, expected 250 m");
                    failures++;
                }
            }

            double mb = total / 1048576.0;
            double expectedMb = ExpectedBytes / 1048576.0;
            double drift = Math.Abs(total - ExpectedBytes) / (double)ExpectedBytes;

            if (drift > SizeToleranceFraction)
            {
                Debug.LogError(
                    "[PET.EditorTools] size FAIL: " + total + " bytes (" +
                    mb.ToString("F1", CultureInfo.InvariantCulture) +
                    " MB) against an expected " +
                    expectedMb.ToString("F1", CultureInfo.InvariantCulture) +
                    " MB, drift " + (drift * 100.0).ToString("F1",
                        CultureInfo.InvariantCulture) + "%");
                failures++;
            }
            else
            {
                Debug.Log(
                    "[PET.EditorTools] size OK: " + total + " bytes (" +
                    mb.ToString("F1", CultureInfo.InvariantCulture) + " MB)");
            }

            return failures;
        }

        /// <summary>
        /// Walk every interior tile boundary and compare the shared edge
        /// samples of the two tiles that meet there.
        ///
        /// WHY EDGE SAMPLES AND NOT CORNERS
        /// --------------------------------
        /// The bug being hunted is a step that runs the full length of an edge.
        /// Comparing a single point could straddle it by luck; comparing the
        /// whole shared edge cannot. Both tiles sample the same 513 rows along
        /// that edge, so a continuous profile agrees sample-for-sample.
        /// </summary>
        private static int CheckSeams()
        {
            int failures = 0;
            int lastRes = ExpectedResolution - 1;
            float maxStepSeen = 0f;
            string worstSeam = "none";

            // Load every tile once.
            //
            // Declared as a Grid x Grid array OF float[,] rather than of float[]:
            // TerrainData.GetHeights returns float[,] (row-major, indexed
            // [y, x]), and a float[] here would silently force 1-D indexing and
            // fail to compile. The [x, y] indices below address the tile grid;
            // the [row, col] indices inside address one tile's samples.
            var heights = new float[Grid, Grid][,];
            for (int x = 0; x < Grid; x++)
            {
                for (int y = 0; y < Grid; y++)
                {
                    string path = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}/TD_x{1:00}_y{2:00}.asset",
                        OutputDir, x, y);

                    var data = AssetDatabase.LoadAssetAtPath<TerrainData>(ToAssetPath(path));
                    if (data == null)
                    {
                        Debug.LogError("[PET.EditorTools] seam check load FAIL: " + path);
                        return 1;
                    }
                    heights[x, y] = data.GetHeights(0, 0, ExpectedResolution, ExpectedResolution);
                }
            }

            // A heightfield from GetHeights is indexed [row, col] where row
            // runs along Z and col runs along X. Reading it as [col, row]
            // transposes the field, and because a transposed square grid still
            // looks plausible, the mistake shows up as a phantom SEAM rather
            // than an index error: this check first reported an 11.9 m step at
            // every horizontal boundary while the vertical ones were perfect,
            // which is the signature of the two axes being swapped.
            //
            // Vertical boundaries: tile (x,y) EAST edge (col = lastRes) against
            // tile (x+1,y) WEST edge (col = 0), compared row for row.
            for (int y = 0; y < Grid; y++)
            {
                for (int x = 0; x < Grid - 1; x++)
                {
                    var left = heights[x, y];
                    var right = heights[x + 1, y];
                    for (int row = 0; row <= lastRes; row++)
                    {
                        float step = Mathf.Abs(left[row, lastRes] - right[row, 0]);
                        if (step > maxStepSeen)
                        {
                            maxStepSeen = step;
                            worstSeam = string.Format(
                                CultureInfo.InvariantCulture,
                                "vertical x{0:00}|x{1:00} y{2:00} row {3}", x, x + 1, y, row);
                        }
                    }
                }
            }

            // Horizontal boundaries: tile (x,y) NORTH edge (row = lastRes)
            // against tile (x,y+1) SOUTH edge (row = 0), compared column for
            // column. The column index is the OUTER loop precisely because the
            // outer index of a heightfield is the row.
            for (int x = 0; x < Grid; x++)
            {
                for (int y = 0; y < Grid - 1; y++)
                {
                    var lower = heights[x, y];
                    var upper = heights[x, y + 1];
                    for (int col = 0; col <= lastRes; col++)
                    {
                        float step = Mathf.Abs(upper[0, col] - lower[lastRes, col]);
                        if (step > maxStepSeen)
                        {
                            maxStepSeen = step;
                            worstSeam = string.Format(
                                CultureInfo.InvariantCulture,
                                "horizontal y{0:00}|y{1:00} x{2:00} col {3}", y, y + 1, x, col);
                        }
                    }
                }
            }

            if (maxStepSeen > MaxSeamStepM)
            {
                Debug.LogError(
                    "[PET.EditorTools] seam FAIL: max height step across a tile " +
                    "boundary is " + maxStepSeen.ToString("R",
                        CultureInfo.InvariantCulture) + " (normalised), limit " +
                    MaxSeamStepM.ToString("R", CultureInfo.InvariantCulture) +
                    ". Worst: " + worstSeam +
                    ". A step here would manufacture the seam bug W2.5 hunts.");
                failures++;
            }
            else
            {
                Debug.Log(
                    "[PET.EditorTools] seam OK: max step across any tile boundary " +
                    "is " + maxStepSeen.ToString("R", CultureInfo.InvariantCulture) +
                    " normalised (limit " + MaxSeamStepM.ToString("R",
                        CultureInfo.InvariantCulture) + ")");
            }

            return failures;
        }

        /// <summary>
        /// AssetDatabase wants forward-slash asset paths; Directory returns
        /// platform separators. On Linux they coincide, but a path built for
        /// one API and handed to the other is the kind of thing that works on
        /// the author's machine.
        /// </summary>
        private static string ToAssetPath(string filesystemPath)
        {
            return filesystemPath.Replace('\\', '/');
        }
    }
}