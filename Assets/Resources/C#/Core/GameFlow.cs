// The shape of a single run: how long it lasted, how it ended, and what happens
// next. Anything may end a run by publishing RunEndedEvent — the exit door, the
// player's last hit point, a future trap — so this knows nothing about clocks or
// maniacs. Freezes time on the ending beat; R reloads the scene, Esc quits.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimeKiller.Core
{
    public enum RunPhase { Playing, Won, Lost }

    /// Published by whichever system decides the run is over. The first one wins.
    public struct RunEndedEvent
    {
        public bool Won;
        public string Headline; // optional override for the end screen
    }

    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [SerializeField] KeyCode restartKey = KeyCode.R;
        [SerializeField] KeyCode quitKey = KeyCode.Escape;

        public RunPhase Phase { get; private set; } = RunPhase.Playing;
        public float RunSeconds { get; private set; }
        public string Headline { get; private set; }

        // One-line run summary owned by whatever feature defines the objective
        // (the clocks, today). Null when that feature isn't in the scene.
        static Func<string> summary;
        public static void ProvideSummary(Func<string> provider) => summary = provider;
        public string Summary => summary != null ? summary() : null;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f; // a reloaded scene always starts unfrozen
        }

        void OnEnable() => EventBus.Subscribe<RunEndedEvent>(OnRunEnded);

        void OnDisable() => EventBus.Unsubscribe<RunEndedEvent>(OnRunEnded);

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (Phase == RunPhase.Playing)
            {
                RunSeconds += Time.deltaTime;
                return;
            }
            if (Input.GetKeyDown(restartKey)) Restart();
            else if (Input.GetKeyDown(quitKey)) Quit();
        }

        void OnRunEnded(RunEndedEvent evt)
        {
            if (Phase != RunPhase.Playing) return; // dying as you escape shouldn't overwrite the win
            Phase = evt.Won ? RunPhase.Won : RunPhase.Lost;
            Headline = string.IsNullOrEmpty(evt.Headline) ? (evt.Won ? "YOU ESCAPED" : "HE CAUGHT YOU") : evt.Headline;
            Time.timeScale = 0f; // audio keeps playing; the end screen fades on unscaled time
        }

        /// Full scene reload — the only reset that can't leak stale state between runs.
        public void Restart()
        {
            Time.timeScale = 1f;
            summary = null; // the reloaded scene registers its own
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// m:ss for the end screen.
        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
