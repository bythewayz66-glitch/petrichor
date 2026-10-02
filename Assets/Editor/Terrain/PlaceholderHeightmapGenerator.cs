using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PET.EditorTools
{
    /// <summary>
    /// Generates the 64 placeholder terrain data assets for the 4 km2 slice.
    ///
    /// W2.1 (issue #9). The point is NOT the shape of the terrain. It is that
    /// the streamer has 64 real things to stream by the end of the week. The
    /// real basin arrives in W6.1 (issue #48), which replaces every file this
    /// writes.
    ///
    /// WHY THIS LIVES OUTSIDE THE TWELVE ASSEMBLIES
    /// -------------------------------------------
    /// The assembly rule in CONTRIBUTING fixes twelve assemblies and says an
    /// assembly may only reference assemblies below it. An asset generator is
    /// not one of the twelve and must not become a thirteenth: it is
    /// editor-only tooling, it references no PET assembly, and it is excluded
    /// from every player build. Putting it under Assets/Scripts/ would put it
    /// inside one of the twelve and make it part of that assembly's contract.
    /// An Editor-folder script with no .asmdef compiles into Unity's own
    /// Assembly-CSharp-Editor, which keeps the twelve exactly twelve.
    ///
    /// WHY THE FILE EXTENSION IS .asset
    /// --------------------------------
    /// Unity only recognises a TerrainData by the `.asset` extension. Writing
    /// one as `.terrain` or `.asset.terrain` produces a file the AssetDatabase
    /// imports with DefaultImporter - not a TerrainData - so nothing can
    /// reference it and 34 MB sits in the repository as a dead blob. This was
    /// measured, not assumed: a first pass wrote `.asset.terrain` and the
    /// acceptance verifier reported `found 0 TerrainData` plus 64 load
    /// failures. The extension is therefore load-bearing in TWO directions -
    /// Unity must recognise it, and git must LFS-track it - and
    /// VerifyLfsPatternIsMatched guards the second.
    ///
    /// The `TD_` prefix is added to the CONTRIBUTING section 5 prefix table by
    /// the same commit that introduces these assets, so the automated naming
    /// test has a documented prefix to check against.
    ///
    /// WHY THE PROFILE IS DETERMINISTIC
    /// --------------------------------
    /// Nothing here calls UnityEngine.Random. The same commit regenerates
    /// byte-comparable heightmaps, so a re-run of this generator that produces
    /// a diff is a real change rather than a new dice roll. Determinism is not
    /// a nicety in this project: PET.Water re-derives its flow field from
    /// terrain height on load, and the save system depends on the terrain being
    /// reproducible.
    ///
    /// USAGE
    /// -----
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod PET.EditorTools.PlaceholderHeightmapGenerator.GenerateAll
    /// </summary>
    public static class PlaceholderHeightmapGenerator
    {
        /// <summary>Grid is 8x8. 8 * 250 m = 2000 m = 4 km2, which is the
        /// slice's stated area.</summary>
        public const int Grid = 8;

        /// <summary>250 m per tile edge. 8 tiles * 250 m = 2 km across.</summary>
        public const float TileSizeM = 250f;

        /// <summary>
        /// 513 = 2^9 + 1. Unity terrain heightmap resolution must be a power of
        /// two plus one; this is a format constraint, not a preference.
        /// </summary>
        public const int HeightmapResolution = 513;

        /// <summary>
        /// Height range in metres. Deliberately small and generous: the
        /// placeholder is flat enough that no simulation can fall off a cliff,
        /// and tall enough that the whole range is inside the terrain's
        /// precision rather than at the bottom of it.
        /// </summary>
        public const float TerrainHeightM = 120f;

        /// <summary>
        /// Where the generated assets go. A separate asset per tile is the
        /// whole point of the W2.2 scene split: Unity cannot serialise
        /// TerrainData as text, so a tile edit has to touch one small binary
        /// file rather than a monolithic scene. See CONTRIBUTING section 6.
        /// </summary>
        public const string OutputDir = "Assets/World/Tiles/Terrain";

        /// <summary>
        /// Filename suffix. Must satisfy the path-scoped `*.asset` rule in
        /// .gitattributes - see VerifyLfsPatternIsMatched for why that is
        /// checked rather than assumed.
        /// </summary>
        public const string AssetFileSuffix = ".asset";

        /// <summary>
        /// Basin depth in metres at the centre of the slice, relative to the
        /// rim. "Gentle": a shallow dish, so the water solver has a direction
        /// to flow on the placeholder without a channel being cut for it.
        /// </summary>
        public const float BasinDepthM = 18f;

        /// <summary>
        /// Generate all 64 heightmaps.
        ///
        /// Exit code is the process exit code, so a CI job can read it: a
        /// non-zero code means the generator did not produce a full set, and a
        /// partial set of tiles is a broken world rather than a smaller one.
        /// </summary>
        public static void GenerateAll()
        {
            try
            {
                GenerateAllInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[PET.EditorTools] PlaceholderHeightmapGenerator FAILED: " + ex);
                // -1 is a failure the caller cannot confuse with Unity's own
                // exit codes.
                EditorApplication.Exit(1);
                throw;
            }
        }

        private static void GenerateAllInternal()
        {
            // Checked BEFORE anything is written, so a repository with the
            // wrong LFS rule fails without having produced 33 MB of
            // untrackable data.
            if (!VerifyLfsPatternIsMatched())
            {
                EditorApplication.Exit(1);
                return;
            }

            EnsureDirectory(OutputDir);

            int written = 0;
            for (int x = 0; x < Grid; x++)
            {
                for (int y = 0; y < Grid; y++)
                {
                    GenerateTile(x, y);
                    written++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (written != Grid * Grid)
            {
                Debug.LogError(
                    "[PET.EditorTools] wrote " + written + " of " + (Grid * Grid) +
                    " heightmaps");
                EditorApplication.Exit(1);
                return;
            }

            long bytes = MeasureOutputBytes();
            Debug.Log(
                "[PET.EditorTools] PLACEHOLDER_HEIGHTMAPS_OK count=" + written +
                " resolution=" + HeightmapResolution +
                " tile_size_m=" + TileSizeM.ToString("R", CultureInfo.InvariantCulture) +
                " total_bytes=" + bytes +
                " total_mb=" + (bytes / 1048576.0).ToString("F1", CultureInfo.InvariantCulture));
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// One tile's heightmap. The profile is a smooth dish centred on the
        /// middle of the whole 64-tile slice, not on this tile, so the 64 tiles
        /// join into one continuous basin instead of 64 identical dishes with a
        /// step at every seam. That matters even for a placeholder: a visible
        /// step at each tile boundary is the seam bug W2.5 exists to prevent,
        /// and generating one here would hide the bug the week is looking for.
        /// </summary>
        private static void GenerateTile(int tileX, int tileY)
        {
            var data = new TerrainData
            {
                heightmapResolution = HeightmapResolution,
                size = new Vector3(TileSizeM, TerrainHeightM, TileSizeM),
                baseMapResolution = 1024,
                alphamapResolution = 1024
            };

            var heights = new float[HeightmapResolution, HeightmapResolution];
            FillBasinProfile(tileX, tileY, heights);
            data.SetHeights(0, 0, heights);

            string assetPath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/TD_x{1:00}_y{2:00}" + AssetFileSuffix,
                OutputDir, tileX, tileY);

            AssetDatabase.CreateAsset(data, assetPath);
        }

        /// <summary>
        /// A radial dish over the full 2 km slice.
        ///
        /// Normalised radius is measured from the centre of the SLICE, not the
        /// centre of the tile, and is clamped to 1 at the corners so the rim is
        /// continuous all the way round. Norm is sqrt(dx^2+dz^2) where dx and dz
        /// are the tile's offset from the slice centre in tile units.
        ///
        /// The height is the dish plus a gentle rim rise, which gives the water
        /// somewhere to collect at the edges instead of letting it run off the
        /// edge of the world.
        /// </summary>
        private static void FillBasinProfile(int tileX, int tileY, float[,] heights)
        {
            int last = HeightmapResolution - 1;
            float half = (Grid - 1) / 2f;

            // Offset of this tile's centre from the slice centre, in tile units.
            float dx = tileX - half;
            float dz = tileY - half;

            for (int j = 0; j <= last; j++)
            {
                // Normalised position within the tile, 0..1.
                float v = (float)j / last;

                for (int i = 0; i <= last; i++)
                {
                    float u = (float)i / last;

                    // Position within the tile in tile units, then the slice
                    // radius at that point.
                    float tx = dx + u;
                    float tz = dz + v;
                    float r = Mathf.Sqrt(tx * tx + tz * tz);

                    // Clamp so the rim is flat rather than continuing to fall
                    // past the corners.
                    float rn = Mathf.Clamp01(r / (half * 1.41421356f));

                    // Smoothstep the dish so the centre is flat-bottomed and it
                    // meets the rim without a crease. A crease would be a
                    // standing wave in the placeholder.
                    float t = rn * rn * (3f - 2f * rn);

                    float dish = t * BasinDepthM;
                    float rim = Mathf.Pow(rn, 6f) * (TerrainHeightM * 0.08f);

                    heights[j, i] = Mathf.Clamp01(
                        (dish + rim) / TerrainHeightM);
                }
            }
        }

        /// <summary>
        /// Assert that the generated filenames are actually routed through Git
        /// LFS by the repository's own .gitattributes.
        ///
        /// WHY THIS IS A RUNTIME ASSERT AND NOT A REVIEW NOTE
        /// ---------------------------------------------------
        /// The failure it guards against is silent and permanent. A TerrainData
        /// committed as a raw binary blob cannot be diffed, cannot be merged,
        /// and cannot be LFS-pruned later without rewriting history. Nothing
        /// errors at generation time; the file is written, the generator logs
        /// success, and the damage only appears once the file is in the object
        /// database. A reviewer reading a 33 MB diff is not going to notice.
        ///
        /// The check parses the literal rule out of .gitattributes rather than
        /// consulting git, so it runs identically inside a batch-mode editor
        /// with no git available. It fails LOUDLY, because a heightmap set that
        /// is not LFS-tracked is a repository that is already broken.
        /// </summary>
        private static bool VerifyLfsPatternIsMatched()
        {
            const string attributesPath = ".gitattributes";
            const string requiredRule = "/Assets/World/Tiles/Terrain/*.asset";

            if (!File.Exists(attributesPath))
            {
                Debug.LogError(
                    "[PET.EditorTools] " + attributesPath + " is missing; cannot " +
                    "verify that terrain data is LFS-tracked. Refusing to " +
                    "generate assets that may be committed as raw binary.");
                return false;
            }

            string[] lines = File.ReadAllLines(attributesPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith(requiredRule, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            Debug.LogError(
                "[PET.EditorTools] " + attributesPath + " has no '" +
                requiredRule + "' rule. Terrain data written as " +
                "*.asset terrain data would NOT be LFS-tracked. Add the rule, or " +
                "change AssetFileSuffix to match an existing rule.");
            return false;
        }

        private static void EnsureDirectory(string assetDir)
        {
            if (!Directory.Exists(assetDir))
            {
                Directory.CreateDirectory(assetDir);
            }
        }

        /// <summary>
        /// Sum the on-disk size of the generated assets, so the log records the
        /// figure the ticket's acceptance criterion names (~34 MB) as a
        /// measurement rather than an arithmetic claim.
        /// </summary>
        private static long MeasureOutputBytes()
        {
            long total = 0;
            if (!Directory.Exists(OutputDir))
            {
                return 0;
            }

            var files = Directory.GetFiles(OutputDir, "*" + AssetFileSuffix);
            for (int i = 0; i < files.Length; i++)
            {
                total += new FileInfo(files[i]).Length;
            }
            return total;
        }
    }
}