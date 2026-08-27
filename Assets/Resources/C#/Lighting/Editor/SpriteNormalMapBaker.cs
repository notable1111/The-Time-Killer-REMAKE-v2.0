// SpriteNormalMapBaker — bakes a tangent-space normal map out of a sprite's own
// pixels, so the 25 Light2Ds in CastleWingLDtk actually shape the art instead of
// washing it flat (2026-08-04).
//
// WHY this exists rather than a downloaded tool: the shadow pass controls where
// light DOESN'T land, but every lit sprite returns light perfectly flat because
// not one of the project's 111 sprite textures has a normal map. External tools
// (Laigter et al.) do this well, but this runs inside the Editor with no
// download, no new dependency, and re-runs over the real art whenever it changes.
//
// The tricky part is that this art is DARK: wardrobeA_ajar peaks at luminance
// 0.416 and averages 0.212. A textbook Sobel over raw luminance would yield
// near-zero gradients and a normal map that does nothing, so the height field is
// renormalised across the opaque region before any gradient is taken. That one
// step is the difference between this working and silently doing nothing.
//
// Usage (mirrors GameEye — callable from the unity-mcp bridge):
//   TimeKiller.Lighting.EditorTools.SpriteNormalMapBaker.Bake(
//       "Assets/Resources/Assets/Hiding/wardrobeA_ajar.png", 2.5f, 1, true)
//   TimeKiller.Lighting.EditorTools.SpriteNormalMapBaker.Unbake(
//       "Assets/Resources/Assets/Hiding/wardrobeA_ajar.png", true)
//
// Reversible on purpose: Unbake clears the _NormalMap slot and deletes the
// generated file, so a spike that reads badly costs nothing to undo.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Lighting.EditorTools
{
    public static class SpriteNormalMapBaker
    {
        const string SecondaryName = "_NormalMap";   // the name URP's 2D lit shader looks for
        const string Suffix = "_Normal";

        /// <summary>
        /// Bakes "<name>_Normal.png" beside the sprite and hangs it on the sprite's
        /// _NormalMap secondary-texture slot.
        /// </summary>
        /// <param name="strength">Bump depth. 1 = subtle, 3 = pronounced.</param>
        /// <param name="smoothing">Box-blur radius in pixels applied to the height
        /// field before the gradient. Pixel art stair-steps; 0 keeps every jagged
        /// edge, 1 is usually enough to read as surface rather than noise.</param>
        /// <param name="attach">False bakes the file but leaves the sprite untouched.</param>
        public static string Bake(string spritePath, float strength, int smoothing, bool attach)
        {
            if (!File.Exists(spritePath)) return $"ERROR: no such file {spritePath}";

            // Read the PNG off disk rather than through the importer: this sidesteps
            // having to flip isReadable on the source asset (an import-settings change
            // we'd then have to remember to revert).
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(src, File.ReadAllBytes(spritePath)))
            {
                Object.DestroyImmediate(src);
                return $"ERROR: could not decode {spritePath}";
            }

            int w = src.width, h = src.height;
            var px = src.GetPixels();
            Object.DestroyImmediate(src);

            // --- height field, renormalised over the opaque region only ---
            var height = new float[w * h];
            var alpha = new float[w * h];
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                alpha[i] = c.a;
                float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                height[i] = lum;
                if (c.a > 0.5f) { if (lum < min) min = lum; if (lum > max) max = lum; }
            }
            if (max <= min) return $"ERROR: {Path.GetFileName(spritePath)} has no luminance variation to bake from";

            float range = max - min;
            for (int i = 0; i < height.Length; i++)
                height[i] = Mathf.Clamp01((height[i] - min) / range);

            if (smoothing > 0) height = BoxBlur(height, alpha, w, h, smoothing);

            // --- Sobel -> tangent-space normal ---
            // GetPixels is bottom-to-top, so +y already points up: no flip needed to
            // land on Unity's OpenGL-style (G+ = up) convention.
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;

                    // Fully transparent pixels get a flat normal so the sprite's
                    // cutout edge doesn't read as a cliff face.
                    if (alpha[i] <= 0.01f) { outPx[i] = new Color32(128, 128, 255, 255); continue; }

                    float tl = At(height, w, h, x - 1, y + 1), t = At(height, w, h, x, y + 1), tr = At(height, w, h, x + 1, y + 1);
                    float l = At(height, w, h, x - 1, y), r = At(height, w, h, x + 1, y);
                    float bl = At(height, w, h, x - 1, y - 1), b = At(height, w, h, x, y - 1), br = At(height, w, h, x + 1, y - 1);

                    float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dy = (tl + 2f * t + tr) - (bl + 2f * b + br);

                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    outPx[i] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255),
                        255);
                }
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(outPx);
            outTex.Apply();
            string normalPath = Path.Combine(Path.GetDirectoryName(spritePath)!,
                                             Path.GetFileNameWithoutExtension(spritePath) + Suffix + ".png")
                                    .Replace('\\', '/');
            File.WriteAllBytes(normalPath, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            ConfigureNormalImporter(normalPath, SourcePixelsPerUnit(spritePath));

            if (!attach) return $"OK baked (not attached): {normalPath} ({w}x{h}) strength={strength} smoothing={smoothing}";

            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normalTex == null) return $"ERROR: baked {normalPath} but could not load it back";

            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null) return $"ERROR: {spritePath} has no TextureImporter";

            importer.secondarySpriteTextures = new[]
            {
                new SecondarySpriteTexture { name = SecondaryName, texture = normalTex }
            };
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            return $"OK: {normalPath} ({w}x{h}) attached as {SecondaryName} " +
                   $"strength={strength} smoothing={smoothing} lumRange={min:F3}-{max:F3}";
        }

        /// <summary>Clears the _NormalMap slot and deletes the generated file.</summary>
        public static string Unbake(string spritePath, bool deleteFile)
        {
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null) return $"ERROR: {spritePath} has no TextureImporter";

            importer.secondarySpriteTextures = new SecondarySpriteTexture[0];
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            string normalPath = Path.Combine(Path.GetDirectoryName(spritePath)!,
                                             Path.GetFileNameWithoutExtension(spritePath) + Suffix + ".png")
                                    .Replace('\\', '/');
            if (deleteFile && File.Exists(normalPath)) AssetDatabase.DeleteAsset(normalPath);

            return $"OK: cleared {SecondaryName} on {Path.GetFileName(spritePath)}" +
                   (deleteFile ? $" and deleted {Path.GetFileName(normalPath)}" : "");
        }

        // A normal map must not be colour-corrected or block-compressed: sRGB would
        // bend the vectors and DXT would swizzle the channels out from under the
        // sprite-lit shader. Point filtering matches the pixel-art source.
        static void ConfigureNormalImporter(string path, float pixelsPerUnit)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            ti.textureType = TextureImporterType.NormalMap;
            ti.sRGBTexture = false;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.spritePixelsPerUnit = pixelsPerUnit;
            ti.SaveAndReimport();
        }

        static float SourcePixelsPerUnit(string spritePath)
        {
            var ti = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            return ti != null ? ti.spritePixelsPerUnit : 16f;
        }

        // Alpha-weighted so transparent neighbours don't drag the silhouette edge
        // toward zero height and carve a false bevel all the way around the sprite.
        static float[] BoxBlur(float[] src, float[] alpha, int w, int h, int radius)
        {
            var dst = new float[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f, wsum = 0f;
                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int sx = Mathf.Clamp(x + ox, 0, w - 1);
                            int sy = Mathf.Clamp(y + oy, 0, h - 1);
                            int si = sy * w + sx;
                            float a = alpha[si];
                            sum += src[si] * a;
                            wsum += a;
                        }
                    }
                    dst[y * w + x] = wsum > 0f ? sum / wsum : src[y * w + x];
                }
            }
            return dst;
        }

        static float At(float[] a, int w, int h, int x, int y)
            => a[Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1)];
    }
}
