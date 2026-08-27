// Menu: TimeKiller/Setup/44 - Migrate UI Text to TextMeshPro.
//
// Every label in the game was legacy `UnityEngine.UI.Text`, which renders from a
// bitmap atlas baked at one size. That is fine at 24pt and visibly soft at the
// sizes this game actually uses — the run-end Headline is **96pt** and the clock
// counter 64pt. Legacy Text also has no outline, and all of this type sits over a
// dark scene, the blood overlay and the hiding slats, where an unoutlined glyph
// loses its edge against whatever happens to be behind it.
//
// TextMeshPro renders from a signed-distance field: crisp at any size, with a
// real outline. It was already installed (it ships inside com.unity.ugui), just
// unused.
//
// Order matters and is the reason this is one script rather than three. Changing
// the runtime scripts' field types from Text to TMP_Text makes every serialised
// reference null, so this migration swaps the components AND re-wires the known
// references in the same pass. Run it once per scene.
//
// Idempotent: a label already migrated is skipped, so re-running is a no-op.
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.EditorTools
{
    public static class TextMeshProMigration
    {
        const string UiRoot = "Assets/Resources/Assets/UI";
        const float OutlineWidth = 0.18f;

        [MenuItem("TimeKiller/Setup/44 - Migrate UI Text to TextMeshPro")]
        public static void Migrate()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("44 - Migrate UI Text to TextMeshPro")) return;

            var log = new List<string>();
            var display = EnsureFontAsset("TimeKiller_Display", log);
            var body = EnsureFontAsset("TimeKiller_Body", log);
            if (display == null && body == null)
            {
                Debug.LogError("[TimeKiller Setup] 44 - no TMP font assets could be built; aborting before touching any label.");
                return;
            }

            // Collect first: swapping components while enumerating them is how you
            // silently miss half the list.
            var targets = new List<Text>(Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (targets.Count == 0) log.Add("no legacy Text left in this scene (already migrated?)");

            var swapped = new Dictionary<GameObject, TextMeshProUGUI>();
            foreach (var old in targets)
            {
                if (old == null) continue;
                var go = old.gameObject;

                // Capture everything worth keeping before the component dies.
                string content = old.text;
                int size = old.fontSize;
                Color colour = old.color;
                var align = Convert(old.alignment);
                bool wasDisplay = old.font != null && old.font.name.Contains("Display");
                bool raycast = old.raycastTarget;

                Undo.DestroyObjectImmediate(old);
                var tmp = Undo.AddComponent<TextMeshProUGUI>(go);
                tmp.text = content;
                tmp.fontSize = size;
                tmp.color = colour;
                tmp.alignment = align;
                tmp.raycastTarget = raycast;
                tmp.enableWordWrapping = false;
                var chosen = wasDisplay ? (display ?? body) : (body ?? display);
                if (chosen != null) tmp.font = chosen;

                // NOTE: the outline is NOT set here. `tmp.outlineWidth` on a
                // freshly added component throws a NullReferenceException inside
                // SetOutlineThickness — the material instance does not exist until
                // the component first renders, which never happens at edit time.
                // It lives on the font asset's material instead (see below), which
                // also avoids a material instance per label and keeps batching.

                swapped[go] = tmp;
                log.Add($"{go.name} -> TMP  {size}pt  {(wasDisplay ? "Display" : "Body")}");
            }

            // Any label that was already TMP (a previous partial run, or one built
            // by Setup/43) still needs a font — sweep them all, not just the ones
            // swapped just now.
            int adopted = 0;
            foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tmp == null || tmp.font != null) continue;
                tmp.font = body ?? display;
                adopted++;
            }
            if (adopted > 0) log.Add($"gave {adopted} pre-existing TMP label(s) a font");

            ApplyOutline(display, log);
            ApplyOutline(body, log);

            int rewired = Rewire(swapped, log);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log($"[TimeKiller Setup] 44 - TMP migration: {swapped.Count} label(s) swapped, {rewired} reference(s) re-wired.\n  "
                      + string.Join("\n  ", log));
        }

        /// Re-points the serialised fields that used to hold a Text.
        ///
        /// The obvious approach — read the dead reference's instance id and look up
        /// what replaced it — does NOT work: once the field's type changes to
        /// TMP_Text the stored id no longer resolves, so every field just reads
        /// null with nothing to trace it back to. Measured: 0 of 6 re-wired.
        ///
        /// So this matches by convention instead, and only ever fills fields that
        /// are already null, so nothing deliberately left empty gets clobbered:
        ///   1. a TMP_Text on the component's OWN object (SliderPercentLabel), then
        ///   2. a descendant of its canvas whose name matches the field name
        ///      (headline -> "Headline", counterText -> "CounterText").
        static int Rewire(Dictionary<GameObject, TextMeshProUGUI> swapped, List<string> log)
        {
            int count = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("TimeKiller")) continue;

                var so = new SerializedObject(mb);
                bool touched = false;
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue != null) continue;

                    var field = type.GetField(it.name, System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (field == null || !typeof(TMP_Text).IsAssignableFrom(field.FieldType)) continue;

                    var found = mb.GetComponent<TMP_Text>();
                    if (found == null) found = FindByName(mb.transform, it.name);
                    if (found == null) { log.Add($"** {type.Name}.{it.name} — no match, still null **"); continue; }

                    it.objectReferenceValue = found;
                    touched = true;
                    count++;
                    log.Add($"re-wired {type.Name}.{it.name} -> {found.gameObject.name}");
                }
                if (touched) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return count;
        }

        /// Finds a label whose object name matches the field name, ignoring case.
        ///
        /// Searches the component's own canvas first (nearest wins, so two canvases
        /// with a "Label" each do not swap), then falls back to EVERY canvas in the
        /// scene. The fallback is required, not defensive: `ObjectiveHUD` lives on
        /// `Clocks/ObjectiveManager`, entirely outside any canvas, and drives
        /// `ObjectiveHudCanvas/CounterPlaque/CounterText` from there. A controller
        /// sitting with the gameplay objects rather than with its own UI is a
        /// perfectly reasonable layout, and the first version of this silently
        /// failed on it.
        static TMP_Text FindByName(Transform from, string fieldName)
        {
            var root = from;
            while (root.parent != null && root.GetComponent<Canvas>() == null) root = root.parent;
            var near = Match(root, fieldName);
            if (near != null) return near;

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var hit = Match(canvas.transform, fieldName);
                if (hit != null) return hit;
            }
            return null;
        }

        static TMP_Text Match(Transform root, string name)
        {
            foreach (var candidate in root.GetComponentsInChildren<TMP_Text>(true))
                if (string.Equals(candidate.gameObject.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return candidate;
            return null;
        }

        /// Puts the outline on the font asset's own material, so every label using
        /// that font gets an edge without any of them owning a material instance.
        /// This is the readability fix the whole migration exists for: all of this
        /// type sits over a dark scene, the blood overlay and the hiding slats,
        /// and an unoutlined glyph loses its edge against whatever is behind it.
        static void ApplyOutline(TMP_FontAsset font, List<string> log)
        {
            if (font == null || font.material == null) return;
            font.material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            font.material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            font.material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            EditorUtility.SetDirty(font.material);
            log.Add($"outline {OutlineWidth:0.00} on {font.name}");
        }

        /// Builds (once) a TMP font asset beside the .ttf it came from.
        static TMP_FontAsset EnsureFontAsset(string fontName, List<string> log)
        {
            string assetPath = $"{UiRoot}/{fontName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) { log.Add($"{fontName} SDF already exists"); return existing; }

            var ttf = AssetDatabase.LoadAssetAtPath<Font>($"{UiRoot}/{fontName}.ttf");
            if (ttf == null) { log.Add($"** {fontName}.ttf NOT FOUND **"); return null; }

            var created = TMP_FontAsset.CreateFontAsset(ttf);
            if (created == null) { log.Add($"** could not build a TMP asset from {fontName}.ttf **"); return null; }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
            AssetDatabase.CreateAsset(created, assetPath);
            // Atlas and material must be nested inside the asset or they are lost
            // on the next import and the font renders as blank boxes.
            if (created.atlasTextures != null && created.atlasTextures.Length > 0)
            {
                created.atlasTextures[0].name = fontName + " Atlas";
                AssetDatabase.AddObjectToAsset(created.atlasTextures[0], created);
            }
            if (created.material != null)
            {
                created.material.name = fontName + " Material";
                AssetDatabase.AddObjectToAsset(created.material, created);
            }
            AssetDatabase.SaveAssets();
            log.Add($"built {fontName} SDF");
            return created;
        }

        static TextAlignmentOptions Convert(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                default: return TextAlignmentOptions.BottomRight;
            }
        }
    }
}
