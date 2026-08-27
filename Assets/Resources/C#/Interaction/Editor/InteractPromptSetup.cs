// Menu: TimeKiller/Setup/40 - Build Interact Prompt (E key cap).
//
// Adds the key cap + label to the HUD canvas Setup/38 already built, so there is
// one HUD canvas rather than two competing ones. Safe to re-run: found-or-created.
using TimeKiller.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Interaction.EditorTools
{
    public static class InteractPromptSetup
    {
        const string UiFolder = "Assets/Resources/Assets/UI";

        [MenuItem("TimeKiller/Setup/40 - Build Interact Prompt (E key cap)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("40 - Build Interact Prompt (E key cap)")) return;

            // GameObject.Find IGNORES INACTIVE OBJECTS, so a canvas that exists
            // but is switched off reads as absent and this script creates nothing
            // while blaming a missing Setup/38. That is a real latent bug and the
            // inactive-inclusive search below fixes it.
            //
            // ⚠️ IT WAS NOT, HOWEVER, THE CAUSE OF THE CATACOMBS CASE, and the
            // first version of this comment said it was. Measured 2026-08-27:
            // Catacombs contains ZERO ObjectiveHudCanvas, active or otherwise
            // (CastleWingLDtk has one). So the original error message was giving
            // CORRECT advice - that level genuinely never had Setup/38's HUD
            // canvas built on it, and no amount of searching will find one.
            // Recorded because a wrong diagnosis left in a comment outlives the
            // bug it was written about.
            var canvasGo = FindHudCanvas();
            if (canvasGo == null)
            {
                Debug.LogError("[TimeKiller Setup] 40 - no ObjectiveHudCanvas in the OPEN SCENE (" +
                               UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                               "), active or inactive. Run \"TimeKiller/Setup/38 - Build Objective HUD " +
                               "(carved stone & brass)\" on this scene first. Nothing was created.");
                return;
            }

            var cap = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/KeyCap_E.png");
            var body = AssetDatabase.LoadAssetAtPath<Font>($"{UiFolder}/TimeKiller_Body.ttf");
            if (cap == null)
            {
                Debug.LogError($"[TimeKiller Setup] KeyCap_E.png missing from {UiFolder} — run Setup/38 once (it imports the set).");
                return;
            }

            // Bottom-centre, ABOVE where the gauge sits. They never show at the
            // same time (the prompt hides while repairing), but keeping them out
            // of each other's space means neither has to know about the other.
            var promptGo = FindOrCreate("InteractPrompt", canvasGo.transform);
            var promptRt = promptGo.GetComponent<RectTransform>();
            promptRt.anchorMin = promptRt.anchorMax = new Vector2(0.5f, 0f);
            promptRt.pivot = new Vector2(0.5f, 0f);
            promptRt.sizeDelta = new Vector2(420f, 96f);
            promptRt.anchoredPosition = new Vector2(0f, 250f);

            var capGo = FindOrCreate("KeyCap", promptGo.transform);
            var capImg = Ensure<Image>(capGo);
            capImg.sprite = cap;
            capImg.raycastTarget = false;
            capImg.preserveAspect = true;
            var capRt = capGo.GetComponent<RectTransform>();
            capRt.anchorMin = capRt.anchorMax = new Vector2(0f, 0.5f);
            capRt.pivot = new Vector2(0f, 0.5f);
            capRt.sizeDelta = new Vector2(72f, 72f);
            capRt.anchoredPosition = new Vector2(70f, 0f);

            var labelGo = FindOrCreate("Label", promptGo.transform);
            var label = Ensure<Text>(labelGo);
            if (body != null) label.font = body;
            label.fontSize = 32;                       // native grid multiple
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(0.88f, 0.85f, 0.78f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "HIDE";
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.sizeDelta = new Vector2(260f, 44f);
            labelRt.anchoredPosition = new Vector2(154f, 0f);

            // The driver lives on the canvas, not on the prompt object — the
            // prompt object gets switched off, and a disabled object's Update
            // never runs, so it could never switch itself back on.
            var prompt = Ensure<InteractPrompt>(canvasGo);
            var so = new SerializedObject(prompt);
            so.FindProperty("root").objectReferenceValue = promptGo;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            promptGo.SetActive(false);
            EditorUtility.SetDirty(prompt);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Debug.Log("[TimeKiller Setup] Interact prompt ready: shows REPAIR near a broken clock, "
                      + "HIDE near a free wardrobe, GET OUT while hidden, and nothing while repairing.");
        }

        /// The HUD canvas, whether or not it happens to be enabled. Uses the
        /// inactive-inclusive search rather than GameObject.Find for the reason
        /// written at the call site.
        static GameObject FindHudCanvas()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.name == "ObjectiveHudCanvas") return c.gameObject;
            return null;
        }

        static GameObject FindOrCreate(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Interact Prompt");
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
