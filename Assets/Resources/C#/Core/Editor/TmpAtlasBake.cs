// Menu: TimeKiller/Setup/46 - Bake TMP fonts to a Static atlas.
//
// The two font assets ship in TMP's DYNAMIC atlas mode, which adds glyphs to the
// atlas the first time each character is rendered and writes them back into the
// .asset. That means the font files change whenever anyone plays: measured on
// 2026-08-03 as **826 deleted lines** of glyph entries in a single session's diff.
// On a shared repo that is a recurring conflict on a file nobody edited.
//
// Baking fixes it by doing the work once, up front. ORDER MATTERS: a Static atlas
// contains only what was baked into it, so the glyphs must be generated BEFORE the
// mode is switched. Flip the mode first and every character that was not already
// cached renders as nothing.
//
// The set baked is printable ASCII (32-126) plus the handful of typographic
// characters the UI actually uses. That is deliberate and sufficient — the project
// rule is English everywhere, so there is no case for a larger set, and every
// unused glyph is atlas space that pushes the real ones onto a second texture.
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class TmpAtlasBake
    {
        const string UiRoot = "Assets/Resources/Assets/UI";

        // Curly quotes and the dashes we use in UI copy; without these a baked
        // Static atlas silently drops them and the label shows a gap.
        // Public because Setup/47 rebakes the same fonts and the two character sets
        // must not drift apart.
        public const string Extras = "—–‘’“”…•©";

        [MenuItem("TimeKiller/Setup/46 - Bake TMP fonts to a Static atlas")]
        public static void Bake()
        {
            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { UiRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null) continue;

                // Must be Dynamic to add anything — TryAddCharacters is a no-op on
                // a Static asset, so a re-run over an already-baked font would
                // otherwise silently bake nothing.
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

                var wanted = new StringBuilder();
                for (char c = ' '; c <= '~'; c++) wanted.Append(c);
                wanted.Append(Extras);

                font.TryAddCharacters(wanted.ToString(), out string missing);
                int baked = font.characterTable != null ? font.characterTable.Count : 0;

                font.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(font);
                if (font.atlasTextures != null)
                    foreach (var tex in font.atlasTextures)
                        if (tex != null) EditorUtility.SetDirty(tex);

                log.Add($"{font.name}: {baked} glyphs baked, mode=Static" +
                        (string.IsNullOrEmpty(missing) ? "" : $"  ** could not add: {missing} **"));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TimeKiller Setup] 46 - TMP atlases baked:\n  " + string.Join("\n  ", log) +
                      "\n  These files should now stop changing when the game is played. " +
                      "Re-run this if you add a font or need a character outside printable ASCII.");
        }
    }
}
