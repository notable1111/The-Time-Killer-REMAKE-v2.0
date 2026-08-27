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

        // EndFrame's 9-slice border, MEASURED off the sprite rather than taken from the
        // art README: border (60,48,60,58) at spritePPU 100 against the canvas's
        // referencePixelsPerUnit 100, so ppuScale is 1 and these are canvas units 1:1.
        static readonly Vector2 PanelSize = new Vector2(760f, 620f);
        const float BorderL = 60f, BorderB = 48f, BorderR = 60f, BorderT = 58f;

        // The frame's own deepest shadow tone, rgb(19,18,31) — the darkest colour that
        // occurs in EndFrame.png in any quantity (2964 px). Picked from the art's
        // palette instead of invented, so the fill reads as part of the frame rather
        // than a black hole punched behind it. Alpha 0.94 leaves a trace of the room.
        static readonly Color InteriorColor = new Color(0.075f, 0.071f, 0.122f, 0.94f);

        [MenuItem("TimeKiller/Setup/43 - Build Pause Menu (Esc + audio sliders)")]
        public static void Build()
        {
            // Without this the script builds its whole rig and then throws at
            // MarkSceneDirty below — a half-run that play mode silently discards.
            if (EditorTools.SetupGuard.Blocked("43 - Build Pause Menu")) return;

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

            // Fills the frame's transparent middle so the room (and the player standing
            // in it) stops showing through the menu. It sits BEHIND the frame, not
            // inside it: EndFrame's 9-slice CENTRE is only 79% transparent — sprite rows
            // y 56..72 are fully opaque and stretch into the stone ledge the buttons
            // rest on, at canvas y -232..-172. A fill parented under Panel would draw
            // over the frame and erase that ledge, so Interior is a sibling ordered
            // ahead of it and the art always wins.
            var interior = Panel(root.transform, "Interior", null, InteriorColor);
            Anchor(interior.rectTransform, new Vector2(0.5f, 0.5f),
                   new Vector2((BorderL - BorderR) * 0.5f, (BorderB - BorderT) * 0.5f),
                   new Vector2(PanelSize.x - BorderL - BorderR, PanelSize.y - BorderT - BorderB));

            var panel = Panel(root.transform, "Panel", frame, Color.white);
            Center(panel.rectTransform, PanelSize);

            // Explicit draw order, so a re-run cannot leave these stacked wrongly.
            blackout.transform.SetSiblingIndex(0);
            interior.transform.SetSiblingIndex(1);
            panel.transform.SetSiblingIndex(2);

            // 64, not 78: Display's native em is 32px, so it is only crisp at 32/64/96.
            var title = Label(panel.transform, "Title", "PAUSED", display, 64, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(640f, 96f));

            // VERTICAL BUDGET. Panel is 760x620 → panel-local y runs -310..310, and
            // EndFrame's 9-slice borders MEASURED off the sprite (top 58, bottom 48,
            // at ppuScale 1) leave a true inner area of y -262..252. Laid out as:
            //     title   156 .. 252     gap 31
            //     Master   55 .. 125     gap 20
            //     Music   -35 ..  35     gap 20
            //     Sfx    -125 .. -55     gap 31
            //     buttons-224 ..-156     then 38 clear above the bottom plinth
            // Symmetric 31/20/20/31, everything inside the frame.
            //
            // The shipped values (-40/-140/-240, buttons at +130) put the buttons at
            // -214..-146, overlapping BOTH the Music row (-175..-105) and the Sfx row
            // (-275..-205). That is the overlap in the bug report — it predates the
            // font-size change, since every rect here is an explicit literal.
            var master = Row(panel.transform, "Master", "MASTER", body, bar, 90f);
            var music = Row(panel.transform, "Music", "MUSIC", body, bar, 0f);
            var sfx = Row(panel.transform, "Sfx", "SOUND", body, bar, -90f);

            var resume = Btn(panel.transform, "ResumeButton", "RESUME", body, bar, new Vector2(-150f, 120f));
            var quit = Btn(panel.transform, "QuitButton", "QUIT", body, bar, new Vector2(150f, 120f));

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
            // HORIZONTAL BUDGET. Row is 560 wide, so row-local x runs -280..280 and
            // sits comfortably inside the frame's 60px side borders (panel -320..320).
            //     caption -272 .. -72
            //     slider   -60 .. 180
            //     percent  192 .. 272
            // NOTE the +108 below is not padding: `Anchor` forces pivot 0.5, so an
            // element anchored to the row's LEFT edge must be offset by half its own
            // width to line its left side up. The old +10 with a 210 width put the
            // caption's left edge at -395 — 15px outside the panel entirely, and well
            // into the decorative border. Same trap on the right for the readout.
            var row = Find(parent, name);
            Anchor(Rect(row), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(560f, 70f));

            // 32, not 30: Body's native em is 16px (crisp at 16/32/48).
            var cap = Label(row.transform, "Caption", caption, font, 32, TextAlignmentOptions.Left);
            Anchor(cap.rectTransform, new Vector2(0f, 0.5f), new Vector2(108f, 0f), new Vector2(200f, 44f));

            var sliderGo = Find(row.transform, "Slider");
            var slider = sliderGo.GetComponent<Slider>() ?? Undo.AddComponent<Slider>(sliderGo);
            Anchor(Rect(sliderGo), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(240f, 26f));

            var bg = Panel(sliderGo.transform, "Background", bar, new Color(0.18f, 0.16f, 0.14f, 0.95f));
            Stretch(bg.rectTransform);

            // Inset, NOT stretched. Stretched, the fill covers the track exactly, so
            // at 100% the brass plaque completely hides the dark one behind it and
            // the two read as a single flat shape. Insetting leaves the track visible
            // as a channel around the fill, which is what gives the widget any depth
            // at all without new art.
            var fillArea = Find(sliderGo.transform, "Fill Area");
            Inset(Rect(fillArea), 7f, 5f, 7f, 5f);
            var fill = Panel(fillArea.transform, "Fill", bar, new Color(0.78f, 0.62f, 0.32f));   // brass
            Stretch(fill.rectTransform);

            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = bg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;

            // Readout so a player can see what they set, not just guess from a bar.
            // 32, not 26: nearest on-grid size for Body. This makes the value the same
            // size as its caption; 16 is the other on-grid option if it should read as
            // secondary instead.
            var pct = Label(row.transform, "Percent", "100", font, 32, TextAlignmentOptions.Right);
            Anchor(pct.rectTransform, new Vector2(1f, 0.5f), new Vector2(-48f, 0f), new Vector2(80f, 40f));
            var echo = pct.gameObject.GetComponent<SliderPercentLabel>() ?? Undo.AddComponent<SliderPercentLabel>(pct.gameObject);
            var eso = new SerializedObject(echo);
            eso.FindProperty("slider").objectReferenceValue = slider;
            eso.FindProperty("label").objectReferenceValue = pct;
            eso.ApplyModifiedPropertiesWithoutUndo();
            return slider;
        }

        static Button Btn(Transform parent, string name, string caption, TMP_FontAsset font, Sprite bar, Vector2 offsetFromBottom)
        {
            var go = Find(parent, name);
            var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
            // These were untextured flat rectangles — the loudest "programmer UI"
            // element on a screen otherwise made of painted stone. Bar.png was
            // imported for exactly this ("9-slice for buttons later" per the art
            // README) and its border is already authored at (22,4,22,4); MainMenu's
            // buttons have been using it all along. Tint stays white so the art
            // reads rather than a colour painted over it.
            img.sprite = bar;
            img.type = bar != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = bar != null ? Color.white : new Color(0.22f, 0.19f, 0.16f, 0.95f);
            var btn = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
            btn.targetGraphic = img;
            var colors = btn.colors;
            // Deliberately the same values MainMenuSetup uses: >1 brightens the art
            // instead of washing a flat brass over it, so both menus respond alike.
            colors.highlightedColor = new Color(1.15f, 1.10f, 0.95f, 1f);
            colors.pressedColor = new Color(0.75f, 0.70f, 0.60f, 1f);
            colors.fadeDuration = 0.08f;
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

        /// Stretch to the parent but hold a margin on each edge.
        static void Inset(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
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
