// Entry point of the game. Runs automatically before the first scene loads
// (no scene object needed), resets static systems, and creates the persistent
// [TimeKillerCore] object that carries core components across scene changes.
using UnityEngine;

namespace TimeKiller.Core
{
    public static class GameBootstrap
    {
        public static bool IsBooted { get; private set; }

        // Statics survive play-mode restarts when Domain Reload is disabled,
        // so they are cleared explicitly at the very start of every session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EventBus.Clear();
            ServiceLocator.Clear();
            IsBooted = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (IsBooted) return;
            IsBooted = true;

            var config = Resources.Load<CoreConfig>(CoreConfig.ResourcesPath);
            if (config == null)
                Debug.LogWarning("[GameBootstrap] CoreConfig.asset not found — using defaults. Run TimeKiller/Setup/4 to create configs.");

            var core = new GameObject("[TimeKillerCore]");
            Object.DontDestroyOnLoad(core);
            core.AddComponent<DebugOverlay>().Init(config);

            Debug.Log("[GameBootstrap] Core booted.");
        }
    }
}
