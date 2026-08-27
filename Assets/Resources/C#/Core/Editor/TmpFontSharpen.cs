// Menu: TimeKiller/Setup/47 - Sharpen the TMP fonts (on-grid bake, desktop shader).
//
// Three measured defects, fixed together because they all live in the font asset:
//
// 1. THE ATLAS WAS BAKED OFF-GRID. Both fonts are pixel fonts: Display is drawn on
//    a 32-units-per-pixel grid, Body on 64, against a 1024 em. So one pixel of the
//    original artwork is 32px (Display) or 16px (Body) of em. The atlas shipped at
//    samplingPointSize 90 — 90/32 = 2.8125 and 90/16 = 5.625, both fractional. Every
//    glyph edge was therefore resampled onto a half-texel and the blur was baked in
//    permanently, where no runtime setting could recover it. Baking at 96 (Display,
//    3x native) and 64 (Body, 4x native) puts every pixel edge back on a texel edge.
//
// 2. THE SHADER WAS THE MOBILE ONE. `TextMeshPro/Mobile/Distance Field` is the
//    reduced-instruction variant and does not expose Sharpness at all. This is a
//    PC-only game (CLAUDE.md 10), so the cost saving buys nothing and the control
//    is worth having.
//
// 3. SHARPNESS SAT AT 0. With the desktop shader available, this is the one dial
//    that trades edge crispness against ringing. See the constant below.
//
// WHAT THIS DELIBERATELY DOES NOT CHANGE: padding stays 9 so gradientScale stays 10,
// which means the existing outline (width 0.18, black) keeps the exact weight it has
// today — text legibility over the dark floor is a tuned, working thing and this pass
// is about focus, not look. Population mode stays Static, so the git-churn fix from
// Setup/46 survives; re-linking the source TTF does not undo it, because a Static
// atlas never adds glyphs at runtime regardless.
//
// The rebake is IN PLACE. The material and the atlas texture are sub-assets of the
// .asset file and the scenes reference them by fileID, so creating fresh assets would
// silently break every TMP_Text in the project. Instead the new glyph data is copied
// onto the existing objects and the references are pointed back at them.
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TimeKiller.EditorTools
{
    public static class TmpFontSharpen
    {
        // -1..1 on TMP's desktop SDF shader. 0 is "no correction" and is what the
        // fonts shipped with. Positive tightens the alpha ramp across the distance
        // field; too far and thin stems grow a bright halo. 0.4 was chosen as a
        // conservative starting point for 2px stems — this is the number to trust
        // your eye on, not a measurement.
        const float Sharpness = 0.4f;

        // Held constant so the tuned outline keeps its exact weight (see header).
        const int Padding = 9;
        const int AtlasDim = 1024;

        struct Target
        {
            public string Asset, Ttf;
            public int PointSize;   // must be an integer multiple of the face's native em
            public Target(string a, string t, int p) { Asset = a; Ttf = t; PointSize = p; }
        }

        static readonly Target[] Targets =
        {
            new Target("Assets/Resources/Assets/UI/TimeKiller_Display SDF.asset",
                       "Assets/Resources/Assets/UI/TimeKiller_Display.ttf", 96),   // native em 32px -> 3x
            new Target("Assets/Resources/Assets/UI/TimeKiller_Body SDF.asset",
                       "Assets/Resources/Assets/UI/TimeKiller_Body.ttf",    64),   // native em 16px -> 4x
        };

        [MenuItem("TimeKiller/Setup/47 - Sharpen the TMP fonts (on-grid bake, desktop shader)")]
        public static void Sharpen()
        {
            // This one writes ASSETS, not scene objects, so a play-mode half-run would
            // persist rather than be discarded — worth refusing outright.
            if (SetupGuard.Blocked("47 - Sharpen the TMP fonts")) return;

            var log = new List<string>();
            var warnings = new List<string>();

            foreach (var target in Targets)
            {
                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target.Asset);
                var ttf = AssetDatabase.LoadAssetAtPath<Font>(target.Ttf);
                if (existing == null) { warnings.Add("font asset missing: " + target.Asset); continue; }
                if (ttf == null) { warnings.Add("source TTF missing: " + target.Ttf); continue; }

                // The sub-assets the scenes point at. These objects must survive.
                var keepMaterial = existing.material;
                var keepTexture = existing.atlasTextures != null && existing.atlasTextures.Length > 0
                    ? existing.atlasTextures[0] : null;
                var keepName = existing.name;
                if (keepMaterial == null || keepTexture == null)
                {
                    warnings.Add(keepName + ": missing material or atlas sub-asset, skipped");
                    continue;
                }

                // Rasterise fresh at the on-grid size. Dynamic while building, because
                // TryAddCharacters is a no-op on a Static asset.
                var fresh = TMP_FontAsset.CreateFontAsset(
                    ttf, target.PointSize, Padding, GlyphRenderMode.SDFAA,
                    AtlasDim, AtlasDim, AtlasPopulationMode.Dynamic, true);
                if (fresh == null) { warnings.Add(keepName + ": CreateFontAsset returned null"); continue; }

                var wanted = new StringBuilder();
                for (char c = ' '; c <= '~'; c++) wanted.Append(c);
                wanted.Append(TmpAtlasBake.Extras);

                string missing;
                fresh.TryAddCharacters(wanted.ToString(), out missing, true);

                if (fresh.atlasTextures.Length > 1)
                    warnings.Add(keepName + ": spilled onto " + fresh.atlasTextures.Length +
                                 " atlas textures — only the first is kept, glyphs will be missing. " +
                                 "Raise AtlasDim or lower PointSize.");

                // Move the pixels onto the sub-asset texture, byte-for-byte.
                var src = fresh.atlasTextures[0];
                if (keepTexture.width != src.width || keepTexture.height != src.height ||
                    keepTexture.format != src.format)
                    keepTexture.Reinitialize(src.width, src.height, src.format, false);
                keepTexture.LoadRawTextureData(src.GetRawTextureData());
                keepTexture.Apply(false, false);
                // Bilinear is correct here and is NOT an oversight: an SDF is sampled
                // and thresholded, so point filtering would produce stair-stepped
                // distance values and ruin the very edges this pass is sharpening.
                keepTexture.filterMode = FilterMode.Bilinear;

                // Copy every serialised field, then put the sub-asset references back.
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = keepName;

                var so = new SerializedObject(existing);
                so.FindProperty("m_Material").objectReferenceValue = keepMaterial;
                var textures = so.FindProperty("m_AtlasTextures");
                textures.arraySize = 1;
                textures.GetArrayElementAtIndex(0).objectReferenceValue = keepTexture;
                so.FindProperty("m_AtlasPopulationMode").enumValueIndex = (int)AtlasPopulationMode.Static;
                so.ApplyModifiedPropertiesWithoutUndo();

                // The fresh objects were scratch; without this they leak into the file.
                Object.DestroyImmediate(fresh.material);
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(fresh);

                // Desktop shader + the dial it unlocks. Outline settings are read back
                // and re-applied so the tuned weight survives the shader swap.
                float outlineWidth = keepMaterial.GetFloat("_OutlineWidth");
                float outlineSoftness = keepMaterial.GetFloat("_OutlineSoftness");
                Color outlineColor = keepMaterial.GetColor("_OutlineColor");
                bool hadOutline = keepMaterial.IsKeywordEnabled("OUTLINE_ON");

                keepMaterial.shader = Shader.Find("TextMeshPro/Distance Field");
                keepMaterial.SetTexture("_MainTex", keepTexture);
                keepMaterial.SetFloat("_GradientScale", Padding + 1);
                keepMaterial.SetFloat("_TextureWidth", keepTexture.width);
                keepMaterial.SetFloat("_TextureHeight", keepTexture.height);
                keepMaterial.SetFloat("_Sharpness", Sharpness);
                keepMaterial.SetFloat("_OutlineWidth", outlineWidth);
                keepMaterial.SetFloat("_OutlineSoftness", outlineSoftness);
                keepMaterial.SetColor("_OutlineColor", outlineColor);
                if (hadOutline) keepMaterial.EnableKeyword("OUTLINE_ON");

                EditorUtility.SetDirty(existing);
                EditorUtility.SetDirty(keepMaterial);
                EditorUtility.SetDirty(keepTexture);

                log.Add($"{keepName}: baked at {target.PointSize}pt (was 90), " +
                        $"{existing.characterTable.Count} glyphs, atlas {keepTexture.width}², " +
                        $"shader=desktop, sharpness={Sharpness}, outline={outlineWidth} preserved");

                if (!string.IsNullOrEmpty(missing))
                    warnings.Add(keepName + ": the source TTF has no glyph for [" + missing + "] — " +
                                 "these cannot be baked because they were never drawn. " +
                                 "Author them in Tools/UIArt/add_glyphs.py to fix.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var message = "[TimeKiller Setup] 47 - fonts sharpened:\n  " + string.Join("\n  ", log);
            if (warnings.Count > 0)
                Debug.LogWarning(message + "\n\n  !! " + string.Join("\n  !! ", warnings));
            else
                Debug.Log(message);
        }
    }
}
