// Menu: TimeKiller/Setup/29 - Setup Run Flow (win/lose screen + restart).
// Builds the GameFlow object and its end-of-run canvas: a black sheet, the
// headline, a run-summary line and the restart prompt. Also registers the open
// scene in Build Settings, because restarting is a scene reload and LoadScene
// can't find a scene that isn't listed. Safe to re-run — it rebuilds cleanly.
using System.Collections.Generic;
using System.Linq;
using TimeKiller.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Core.EditorTools
{
    public static class GameFlowSetup
    {
        [MenuItem("TimeKiller/Setup/29 - Setup Run Flow (win/lose screen + restart)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("29 - Setup Run Flow (win/lose screen + restart)")) return;

            var old = GameObject.Find("GameFlow");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("GameFlow");
            Undo.RegisterCreatedObjectUndo(root, "Run Flow");
            var flow = root.AddComponent<GameFlow>();

            var canvasGo = new GameObject("RunEndCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // above the hiding slats (600) and blood (500)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Split the difference between width and height. Left unset it defaults to
            // 0 (width only), which over-scales the run-end text on an ultrawide and
            // under-scales it on 4:3. Identical to what PauseMenuSetup chose.
            scaler.matchWidthOrHeight = 0.5f;

            var group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Sheet(canvasGo.transform);
            var headline = Label(canvasGo.transform, "Headline", 84, FontStyle.Bold, new Vector2(0f, 60f), 130f);
            var detail = Label(canvasGo.transform, "Detail", 30, FontStyle.Normal, new Vector2(0f, -40f), 50f);
            var prompt = Label(canvasGo.transform, "Prompt", 26, FontStyle.Normal, new Vector2(0f, -150f), 44f);
            detail.color = new Color(0.75f, 0.75f, 0.78f);
            prompt.color = new Color(0.55f, 0.55f, 0.6f);

            var screen = canvasGo.AddComponent<RunEndScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("headline").objectReferenceValue = headline;
            so.FindProperty("detail").objectReferenceValue = detail;
            so.FindProperty("prompt").objectReferenceValue = prompt;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool listed = RegisterSceneForReload();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[TimeKiller Setup] Run flow ready: the run ends on escape or on your last hit point — "
                + "screen fades in, R reloads the scene, Esc quits. "
                + (listed ? "Scene registered in Build Settings (restart needs it)." : "WARNING: scene is unsaved — save it, then re-run so restart can find it."));
        }

        static void Sheet(Transform parent)
        {
            var go = new GameObject("Blackout");
            go.transform.SetParent(parent, false);
            Stretch(go.AddComponent<RectTransform>());
            var image = go.AddComponent<Image>();
            image.color = new Color(0.02f, 0.02f, 0.03f, 0.94f);
            image.raycastTarget = false;
        }

        static Text Label(Transform parent, string name, int size, FontStyle style, Vector2 offset, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(0f, height);

            var text = go.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Font BuiltinFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // SceneManager.LoadScene only sees scenes listed in Build Settings.
        static bool RegisterSceneForReload()
        {
            string path = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(path)) return false;
            if (EditorBuildSettings.scenes.Any(s => s.path == path)) return true;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(path, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
            return true;
        }
    }
}
