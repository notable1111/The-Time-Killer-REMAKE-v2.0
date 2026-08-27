// Menu: TimeKiller/Setup/39 - Style Run End Screen (carved stone & brass).
//
// Dresses the EXISTING run-end screen: adds the 9-sliced EndFrame border behind
// the text and swaps the labels off Unity's LegacyRuntime font onto the game's
// own pixel faces.
//
// Deliberately a SEPARATE menu item rather than a change to Setup/29, because
// Setup/29 destroys and rebuilds the whole GameFlow object. Re-running it to
// pick up a font swap would throw away anything hand-tuned on the rig. This one
// is found-or-created and touches nothing it did not create.
using TimeKiller.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.EditorTools
{
    public static class RunEndStyleSetup
    {
        const string UiFolder = "Assets/Resources/Assets/UI";

        [MenuItem("TimeKiller/Setup/39 - Style Run End Screen (carved stone & brass)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("39 - Style Run End Screen (carved stone & brass)")) return;

            var screen = Object.FindAnyObjectByType<RunEndScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogError("[TimeKiller Setup] No RunEndScreen in the scene — run Setup/29 first. Nothing was changed.");
                return;
            }

            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/EndFrame.png");
            var display = AssetDatabase.LoadAssetAtPath<Font>($"{UiFolder}/TimeKiller_Display.ttf");
            var body = AssetDatabase.LoadAssetAtPath<Font>($"{UiFolder}/TimeKiller_Body.ttf");
            if (frameSprite == null || display == null || body == null)
            {
                Debug.LogError($"[TimeKiller Setup] UI art missing from {UiFolder} — run Setup/38 once first (it imports the set). Nothing was changed.");
                return;
            }

            var canvas = screen.transform;

            // The frame sits behind the labels but in front of the blackout sheet.
            var frameGo = FindOrCreate("EndFrame", canvas);
            var frame = Ensure<Image>(frameGo);
            frame.sprite = frameSprite;
            frame.type = Image.Type.Sliced;   // the border keeps the brass corners intact
            frame.raycastTarget = false;
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.sizeDelta = new Vector2(1180f, 620f);
            frameRt.anchoredPosition = Vector2.zero;
            // Index 1: behind everything except the Blackout sheet at 0.
            frameGo.transform.SetSiblingIndex(Mathf.Min(1, canvas.childCount - 1));

            // The glyphs are deliberately neutral pale so RunEndScreen can tint the
            // headline green on a win and red on a loss — a baked-in colour would
            // fight that, which is why the font was authored this way.
            StyleLabel(canvas, "Headline", display, 96);
            StyleLabel(canvas, "Detail", body, 32);
            StyleLabel(canvas, "Prompt", body, 32);

            EditorUtility.SetDirty(screen);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
            Debug.Log("[TimeKiller Setup] Run end screen styled: EndFrame border (9-sliced) + Display/Body pixel fonts. "
                      + "Headline stays untinted in the asset so the win/lose colours still apply.");
        }

        static void StyleLabel(Transform canvas, string name, Font font, int size)
        {
            var t = canvas.Find(name);
            if (t == null) { Debug.LogWarning($"[TimeKiller Setup] No '{name}' label under the run-end canvas — skipped."); return; }
            var text = t.GetComponent<Text>();
            if (text == null) return;
            text.font = font;
            text.fontSize = size;                  // native grid multiples only, or glyphs blur
            text.fontStyle = FontStyle.Normal;     // the pixel faces have no bold variant
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            t.SetAsLastSibling();                  // always in front of the frame
        }

        static GameObject FindOrCreate(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Run End Style");
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
