// Menu: TimeKiller/Setup/43 - Build Pause Menu (Esc + audio sliders).
//
// Builds the pause canvas from the UI art already in the project — EndFrame for
// the panel, Bar for the slider track (which had been imported and never used by
// anything), KeyCap-era fonts for the type. Nothing new is generated: the art
// direction is locked ("carved stone & brass") and this screen has no business
// inventing a second look.
//
// Idempotent: find-or-create by name, re-wire in place, never destroy and rebuild.
using TimeKiller.Audio;
using TimeKiller.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimeKiller.MenuTools
{
    public static class PauseMenuSetup
    {
        const string UiRoot = "Assets/Resources/Assets/UI";
        const string CanvasName = "PauseCanvas";

        [MenuItem("TimeKiller/Setup/43 - Build Pause Menu (Esc + audio sliders)")]
        public static void Build()
        {
            // TMP font assets, built by Setup/44. Null is survivable — TMP falls
            // back to its own default rather than rendering nothing.
            var display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{UiRoot}/TimeKiller_Display SDF.asset");
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{UiRoot}/TimeKiller_Body SDF.asset");
            if (display == null || body == null)
                Debug.LogWarning("[TimeKiller Setup] 43 - TMP font assets missing; run Setup/44 first for the real type.");
            var frame = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/EndFrame.png");
            var bar = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/Bar.png");

            // uGUI is inert without one of these, and CastleWingLDtk may not have
            // one — the existing HUD canvases are all display-only.
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
                Debug.Log("[TimeKiller Setup] 43 - created an EventSystem (none in scene; UI would not have responded)");
            }

            var root = GameObject.Find(CanvasName);
            if (root == null)
            {
                root = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                Undo.RegisterCreatedObjectUndo(root, "Create PauseCanvas");
            }
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;              // above the HUD, the blood overlay and the slats
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var blackout = Panel(root.transform, "Blackout", null, new Color(0f, 0f, 0f, 0.72f));
            Stretch(blackout.rectTransform);

            var panel = Panel(root.transform, "Panel", frame, Color.white);
            Center(panel.rectTransform, new Vector2(760f, 620f));

            var title = Label(panel.transform, "Title", "PAUSED", display, 78, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(640f, 96f));

            var master = Row(panel.transform, "Master", "MASTER", body, bar, -40f);
            var music = Row(panel.transform, "Music", "MUSIC", body, bar, -140f);
            var sfx = Row(panel.transform, "Sfx", "SOUND", body, bar, -240f);

            var resume = Btn(panel.transform, "ResumeButton", "RESUME", body, new Vector2(-150f, 130f));
            var quit = Btn(panel.transform, "QuitButton", "QUIT", body, new Vector2(150f, 130f));

            var menu = root.GetComponent<PauseMenu>();
            if (menu == null) menu = Undo.AddComponent<PauseMenu>(root);
            var so = new SerializedObject(menu);
            so.FindProperty("group").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("masterSlider").objectReferenceValue = master;
            so.FindProperty("musicSlider").objectReferenceValue = music;
            so.FindProperty("sfxSlider").objectReferenceValue = sfx;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[TimeKiller Setup] 43 - Pause menu ready on '{EditorSceneManager.GetActiveScene().name}'. " +
                      $"Esc toggles it. Sliders write to PlayerPrefs, never to AudioMixConfig.\n" +
                      $"  art: frame={(frame != null)} bar={(bar != null)} display={(display != null)} body={(body != null)}");
            Selection.activeObject = root;
        }

        // ---- builders ----

        static Image Panel(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = Find(parent, name);
            var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
            img.sprite = sprite;
            img.color = color;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = true;                 // the panel eats clicks meant for the world
            return img;
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, int size, TextAlignmentOptions anchor)
        {
            var go = Find(parent, name);
            var t = go.GetComponent<TextMeshProUGUI>() ?? Undo.AddComponent<TextMeshProUGUI>(go);
            t.text = text; t.fontSize = size; t.alignment = anchor;
            if (font != null) t.font = font;
            t.color = new Color(0.92f, 0.88f, 0.80f);
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            // The outline lives on the font asset's material (applied by Setup/44),
            // NOT here: setting `outlineWidth` on a freshly added TMP component
            // throws inside SetOutlineThickness, because the material instance is
            // not created until the component first renders — which never happens
            // at edit time.
            return t;
        }

        static Slider Row(Transform parent, string name, string caption, TMP_FontAsset font, Sprite bar, float y)
        {
            var row = Find(parent, name);
            Anchor(Rect(row), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(600f, 70f));

            var cap = Label(row.transform, "Caption", caption, font, 30, TextAlignmentOptions.Left);
            Anchor(cap.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(210f, 44f));

            var sliderGo = Find(row.transform, "Slider");
            var slider = sliderGo.GetComponent<Slider>() ?? Undo.AddComponent<Slider>(sliderGo);
            Anchor(Rect(sliderGo), new Vector2(1f, 0.5f), new Vector2(-190f, 0f), new Vector2(360f, 26f));

            var bg = Panel(sliderGo.transform, "Background", bar, new Color(0.18f, 0.16f, 0.14f, 0.95f));
            Stretch(bg.rectTransform);

            var fillArea = Find(sliderGo.transform, "Fill Area");
            Stretch(Rect(fillArea));
            var fill = Panel(fillArea.transform, "Fill", bar, new Color(0.78f, 0.62f, 0.32f));   // brass
            Stretch(fill.rectTransform);

            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = bg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;

            // Readout so a player can see what they set, not just guess from a bar.
            var pct = Label(row.transform, "Percent", "100", font, 26, TextAlignmentOptions.Right);
            Anchor(pct.rectTransform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(70f, 40f));
            var echo = pct.gameObject.GetComponent<SliderPercentLabel>() ?? Undo.AddComponent<SliderPercentLabel>(pct.gameObject);
            var eso = new SerializedObject(echo);
            eso.FindProperty("slider").objectReferenceValue = slider;
            eso.FindProperty("label").objectReferenceValue = pct;
            eso.ApplyModifiedPropertiesWithoutUndo();
            return slider;
        }

        static Button Btn(Transform parent, string name, string caption, TMP_FontAsset font, Vector2 offsetFromBottom)
        {
            var go = Find(parent, name);
            var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
            img.color = new Color(0.22f, 0.19f, 0.16f, 0.95f);
            var btn = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.78f, 0.62f, 0.32f);   // brass on hover
            colors.pressedColor = new Color(0.55f, 0.42f, 0.2f);
            btn.colors = colors;
            Anchor(Rect(go), new Vector2(0.5f, 0f), offsetFromBottom, new Vector2(240f, 68f));
            var t = Label(go.transform, "Text", caption, font, 32, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            return btn;
        }

        // ---- rect helpers ----

        static GameObject Find(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static RectTransform Rect(GameObject go) =>
            go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }
    }
}
