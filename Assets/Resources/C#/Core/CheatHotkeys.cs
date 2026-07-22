// Dev cheat hotkeys (editor + dev builds only — compiled out of release like
// the DebugOverlay). Planned since Phase 0, activated now that stats exist:
//   F5 = toggle god mode, F6 = refill health.
// Uses reflection-free direct references? No — Core must not depend on
// features, so cheats are registered BY features via RegisterCheat.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeKiller.Core
{
    public class CheatHotkeys : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        struct Cheat
        {
            public Key Key;
            public string Name;
            public Action Action;
        }

        static readonly List<Cheat> cheats = new List<Cheat>();

        /// Features call this (e.g. PlayerHealth setup) to add a hotkey.
        public static void RegisterCheat(Key key, string name, Action action)
        {
            cheats.RemoveAll(c => c.Key == key);
            cheats.Add(new Cheat { Key = key, Name = name, Action = action });
            DebugOverlay.Watch($"[{key}]", () => name);
        }

        public static void ClearCheats()
        {
            foreach (var cheat in cheats) DebugOverlay.Unwatch($"[{cheat.Key}]");
            cheats.Clear();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            foreach (var cheat in cheats)
                if (kb[cheat.Key].wasPressedThisFrame)
                {
                    cheat.Action?.Invoke();
                    Debug.Log($"[Cheat] {cheat.Name}");
                }
        }
#else
        public static void RegisterCheat(UnityEngine.InputSystem.Key key, string name, System.Action action) { }
        public static void ClearCheats() { }
#endif
    }
}
