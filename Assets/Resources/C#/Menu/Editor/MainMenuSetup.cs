// Menu: TimeKiller/Setup/41 - Build Main Menu Scene.
//
// Creates Assets/Scenes/MainMenu.unity from scratch, registers it FIRST in Build
// Settings (a built game starts at index 0), and reopens whatever scene you were
// working in so this never costs you your place.
//
// Layout follows Tools/UIArt/_menu_mock.png:
//   - TitleBackground fills the screen, point-upscaled.
//   - Two scrims make text legible over the art: a band behind the title and a
//     band behind the buttons. Without them the tagline disappears into the
//     castle towers — the art is busy exactly where the text sits.
//   - Buttons are Bar.png 9-sliced, so only the slate middle stretches and the
//     22px brass end caps keep their shape. Never scale the whole bar.
using TimeKiller.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TimeKiller.Menu.EditorTools
{
    public static class MainMenuSetup
    {
        const string UiFolder = "Assets/Resources/Assets/UI";
        const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("TimeKiller/Setup/41 - Build Main Menu Scene")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("41 - Build Main Menu Scene")) return;

            // Scene creation is illegal in play mode and throws part-way through,
            // which would leave a half-built menu scene open over your work.
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[TimeKiller Setup] Stop play mode first — building a scene during play throws halfway and would leave your work replaced by a half-made menu. Nothing was changed.");
                return;
            }

            var background = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/TitleBackground.png");
            var bar = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/Bar.png");
            var display = AssetDatabase.LoadAssetAtPath<Font>($"{UiFolder}/TimeKiller_Display.ttf");
            var body = AssetDatabase.LoadAssetAtPath<Font>($"{UiFolder}/TimeKiller_Body.ttf");
            if (background == null || bar == null || display == null || body == null)
            {
                Debug.LogError($"[TimeKiller Setup] Menu art missing from {UiFolder} — run Setup/38 once (it imports the set), "
                             + "and make sure TitleBackground.png was copied from Tools/UIArt/. Nothing was built.");
                return;
            }

            // Never lose the user's place: remember the open scene and come back.
            var previous = EditorSceneManager.GetActiveScene().path;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("MenuCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            camGo.tag = "MainCamera";

            var canvasGo = new GameObject("MenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // See GameFlowSetup: unset defaults to width-only matching, which breaks
            // on any non-16:9 monitor.
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // never crop the castle
            canvasGo.AddComponent<GraphicRaycaster>();

            // An EventSystem, or every button is inert and nothing says why.
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var bg = NewImage(canvasGo.transform, "Background", background);
            Stretch(bg.rectTransform);
            bg.preserveAspect = false;   // it is authored exactly 16:9

            var topScrim = NewImage(canvasGo.transform, "TitleScrim", null);
            topScrim.color = new Color(0f, 0f, 0f, 0.68f);
            var tsRt = topScrim.rectTransform;
            tsRt.anchorMin = new Vector2(0f, 0.72f);
            tsRt.anchorMax = new Vector2(1f, 1f);
            tsRt.offsetMin = tsRt.offsetMax = Vector2.zero;

            var leftScrim = NewImage(canvasGo.transform, "ButtonScrim", null);
            leftScrim.color = new Color(0f, 0f, 0f, 0.45f);
            var lsRt = leftScrim.rectTransform;
            lsRt.anchorMin = new Vector2(0f, 0f);
            lsRt.anchorMax = new Vector2(0.42f, 0.72f);
            lsRt.offsetMin = lsRt.offsetMax = Vector2.zero;

            var title = Label(canvasGo.transform, "Title", display, 96, TextAnchor.MiddleCenter);
            title.text = "THE TIME KILLER";
            title.color = new Color(0.94f, 0.90f, 0.80f);
            Anchor(title.rectTransform, new Vector2(0f, 0.80f), new Vector2(1f, 0.98f));

            var tagline = Label(canvasGo.transform, "Tagline", body, 32, TextAnchor.MiddleCenter);
            tagline.text = "fix the clocks  ·  reach the gate  ·  do not be found";
            tagline.color = new Color(0.90f, 0.86f, 0.78f);   // dim grey vanished into the towers
            Anchor(tagline.rectTransform, new Vector2(0f, 0.73f), new Vector2(1f, 0.80f));

            var menu = canvasGo.AddComponent<MainMenu>();

            MakeButton(canvasGo.transform, "PlayButton", bar, body, "THE CASTLE WING", 0.52f, menu, nameof(MainMenu.PlayCastleWing));
            MakeButton(canvasGo.transform, "CatacombsButton", bar, body, "THE CATACOMBS", 0.36f, menu, nameof(MainMenu.PlayCatacombs));
            MakeButton(canvasGo.transform, "QuitButton", bar, body, "QUIT", 0.20f, menu, nameof(MainMenu.Quit));

            EditorSceneManager.SaveScene(scene, ScenePath);
            bool listed = RegisterFirst();

            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            Debug.Log($"[TimeKiller Setup] Main menu built at {ScenePath}. "
                    + (listed ? "Registered FIRST in Build Settings — a built game now starts here. " : "WARNING: could not register it in Build Settings. ")
                    + "The Catacombs button is the first time that level is reachable without opening its scene by hand.");
        }

        /// The menu must be index 0 — that is the scene a built player loads.
        static bool RegisterFirst()
        {
            var guid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (string.IsNullOrEmpty(guid)) return false;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            return true;
        }

        static void MakeButton(Transform parent, string name, Sprite bar, Font font, string text,
                               float yAnchor, MainMenu menu, string method)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = bar;
            img.type = Image.Type.Sliced;   // stretch the slate, keep the brass caps
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, yAnchor);
            rt.anchorMax = new Vector2(0.37f, yAnchor + 0.10f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.10f, 0.95f, 1f);
            colors.pressedColor = new Color(0.75f, 0.70f, 0.60f, 1f);
            button.colors = colors;

            var label = Label(go.transform, "Label", font, 32, TextAnchor.MiddleCenter);
            label.text = text;
            label.color = new Color(0.93f, 0.89f, 0.80f);
            Stretch(label.rectTransform);

            // A persistent listener, so the wiring is saved in the scene rather
            // than added at runtime by code nobody can see in the inspector.
            var target = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), menu, method)
                         as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, target);
        }

        // NOT called Image: a helper with the same name as the type shadows it,
        // and `AddComponent<Image>()` then fails to resolve inside this class.
        static Image NewImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Text Label(Transform parent, string name, Font font, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;              // native grid multiples only
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // The mock's 1px dark shadow: without it the tagline vanishes into
            // the towers behind it.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
