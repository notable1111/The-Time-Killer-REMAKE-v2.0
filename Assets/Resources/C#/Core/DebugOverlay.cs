// On-screen developer overlay (toggle with F1): FPS plus any values features
// register via DebugOverlay.Watch("label", () => value). Compiled out of
// release builds — only exists in Editor and Development builds.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeKiller.Core
{
    public class DebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly Dictionary<string, Func<string>> watches = new Dictionary<string, Func<string>>();

        CoreConfig config;
        bool visible;
        float smoothedDelta;

        // Features call this to show live values, e.g. Watch("Player State", () => sm.Current.GetType().Name)
        public static void Watch(string label, Func<string> valueGetter) => watches[label] = valueGetter;
        public static void Unwatch(string label) => watches.Remove(label);

        public void Init(CoreConfig coreConfig)
        {
            config = coreConfig;
            visible = config == null || config.overlayVisibleOnStart;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;

            float smoothing = config != null ? config.fpsSmoothing : 0.95f;
            smoothedDelta = Mathf.Lerp(Time.unscaledDeltaTime, smoothedDelta, smoothing);
        }

        void OnGUI()
        {
            if (!visible) return;

            GUILayout.BeginArea(new Rect(8, 8, 320, 500), GUI.skin.box);
            GUILayout.Label($"FPS: {(smoothedDelta > 0f ? 1f / smoothedDelta : 0f):0}");
            foreach (var watch in watches)
            {
                string value;
                try { value = watch.Value?.Invoke() ?? "null"; }
                catch (Exception e) { value = $"<error: {e.GetType().Name}>"; }
                GUILayout.Label($"{watch.Key}: {value}");
            }
            GUILayout.EndArea();
        }

        void OnDestroy() => watches.Clear();
#else
        public void Init(CoreConfig coreConfig) { }
#endif
    }
}
