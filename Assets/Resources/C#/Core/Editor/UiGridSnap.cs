// Menu: TimeKiller/Setup/48 - Snap UI text to the font grid.
//
// The two UI fonts are pixel fonts, so they only render cleanly at whole multiples
// of their native em. Display is drawn 32 units-per-pixel against a 1024 em, so its
// native em is 1024/32 = 32px and it is crisp at 32/64/96. Body is drawn at 64
// units-per-pixel, so its em is 16px and it is crisp at 16/32/48. At any other size
// a stem that is 2px in the artwork lands across a fraction of a screen pixel and
// the glyph goes soft — which is why Tools/UIArt/README.md says "integer font sizes
// that are multiples of the native grid ... or the glyphs will blur".
//
// Most of the UI already obeys this (ObjectiveHudSetup and RunEndStyleSetup both
// carry a "native grid multiple" comment). The pause menu did not: it shipped
// Title 78, Caption 30 and Percent 26, all off-grid.
//
// This encodes the RULE rather than those three numbers, so text added later is
// snapped too instead of quietly drifting off-grid again. It is idempotent: text
// already on-grid is left exactly as it is.
//
// It also makes the canvas scale mode explicit where it was only ever a default.
// ObjectiveHudCanvas is deliberately left alone — ObjectiveHudSetup sets match=1
// with the reason "the HUD hugs top and bottom", and that is a decision, not an
// oversight.
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TimeKiller.EditorTools
{
    public static class UiGridSnap
    {
        // Native em in screen pixels, derived above. A font not listed here is left
        // alone — snapping a font whose grid we do not know would be a guess.
        static readonly Dictionary<string, int> NativeEm = new Dictionary<string, int>
        {
            { "TimeKiller_Display SDF", 32 },
            { "TimeKiller_Body SDF",    16 },
        };

        // Canvases whose match was never set and so fell to 0 (match width only),
        // which over- or under-scales the UI on any non-16:9 monitor. 0.5 is what
        // PauseMenuSetup already chose explicitly.
        static readonly Dictionary<string, float> CanvasMatch = new Dictionary<string, float>
        {
            { "RunEndCanvas", 0.5f },
            { "MenuCanvas",   0.5f },
        };

        // Catacombs is deliberately ABSENT. Its UI was never migrated to TextMeshPro
        // (Setup/44 only ever ran on CastleWing), so its RunEndCanvas holds legacy
        // UnityEngine.UI.Text while RunEndScreen's headline/detail/prompt fields are
        // now TMP_Text. Those references are therefore already dangling in memory the
        // moment the scene loads, and merely opening and SAVING the scene writes the
        // nulls into the file — a 9-line destructive diff for a canvas that contains
        // no text to snap in the first place. Measured 2026-08-03 and reverted.
        // Put it back here once Catacombs has been through Setup/44.
        static readonly string[] Scenes =
        {
            "Assets/Scenes/CastleWingLDtk.unity",
            "Assets/Scenes/MainMenu.unity",
        };

        [MenuItem("TimeKiller/Setup/48 - Snap UI text to the font grid")]
        public static void Snap()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TimeKiller Setup] 48 - refusing to run in play mode: " +
                                 "scene edits made now are thrown away when play stops.");
                return;
            }

            var log = new List<string>();

            foreach (var scenePath in Scenes)
            {
                var scene = SceneManager.GetSceneByPath(scenePath);
                bool weOpenedIt = false;
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    weOpenedIt = true;
                }

                int changes = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (text.font == null) continue;
                        int em;
                        if (!NativeEm.TryGetValue(text.font.name, out em)) continue;

                        int current = Mathf.RoundToInt(text.fontSize);
                        int snapped = Mathf.Max(em, Mathf.RoundToInt(current / (float)em) * em);
                        if (snapped == current) continue;

                        Undo.RecordObject(text, "Snap UI text to font grid");
                        text.fontSize = snapped;
                        EditorUtility.SetDirty(text);
                        changes++;
                        log.Add($"{scene.name}/{text.gameObject.name}: {current} -> {snapped} " +
                                $"({text.font.name}, em {em}px)");
                    }

                    foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                    {
                        float match;
                        if (!CanvasMatch.TryGetValue(scaler.gameObject.name, out match)) continue;
                        if (Mathf.Approximately(scaler.matchWidthOrHeight, match)) continue;

                        Undo.RecordObject(scaler, "Set canvas match");
                        scaler.matchWidthOrHeight = match;
                        EditorUtility.SetDirty(scaler);
                        changes++;
                        log.Add($"{scene.name}/{scaler.gameObject.name}: match -> {match}");
                    }
                }

                // Only ever save a scene this pass actually changed (CLAUDE.md 5).
                if (changes > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                if (weOpenedIt) EditorSceneManager.CloseScene(scene, true);
            }

            if (log.Count == 0)
                Debug.Log("[TimeKiller Setup] 48 - everything already on-grid, nothing changed.");
            else
                Debug.Log("[TimeKiller Setup] 48 - UI snapped to the font grid:\n  " +
                          string.Join("\n  ", log));
        }
    }
}
