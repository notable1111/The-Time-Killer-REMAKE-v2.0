// Esc pauses. One panel, three sliders, two buttons — nothing else.
//
// Kept deliberately flat: no nested "Settings" screen, because a settings screen
// with three sliders in it is two extra clicks for no information. The design
// ruling for this game's UI is minimal and diegetic, and a pause menu is the one
// place a player is allowed to see plain controls, so it should be over quickly.
//
// AUDIO KEEPS PLAYING while paused, at full level. That is not an oversight —
// the sliders are unusable if you cannot hear what they are doing, and the end
// screen already behaves this way (GameFlow freezes time and lets audio run).
//
// Esc is free during play: GameFlow only reads its restart/quit keys once the run
// is over (Phase != Playing), so pausing here can never collide with quitting
// there. This component refuses to open at all once the run has ended.
//
// Removable — delete the object and the game runs, simply without a pause.
using TimeKiller.Audio;
using TimeKiller.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Menu
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] CanvasGroup group;
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Button resumeButton;
        [SerializeField] Button quitButton;
        [SerializeField] float fadeSeconds = 0.18f;

        public bool IsPaused { get; private set; }

        float restoreTimeScale = 1f;

        void Awake()
        {
            if (group != null) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
        }

        void Start()
        {
            // Push the stored values into the sliders BEFORE wiring the callbacks,
            // or SetValueWithoutNotify's absence would write the default straight
            // back over what the player saved last session.
            if (masterSlider != null) { masterSlider.SetValueWithoutNotify(AudioMix.UserMaster); masterSlider.onValueChanged.AddListener(OnMaster); }
            if (musicSlider != null)  { musicSlider.SetValueWithoutNotify(AudioMix.UserMusic);   musicSlider.onValueChanged.AddListener(OnMusic); }
            if (sfxSlider != null)    { sfxSlider.SetValueWithoutNotify(AudioMix.UserSfx);       sfxSlider.onValueChanged.AddListener(OnSfx); }
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (quitButton != null)   quitButton.onClick.AddListener(QuitToDesktop);

            DebugOverlay.Watch("Pause", () => IsPaused
                ? $"PAUSED  master {AudioMix.UserMaster:0.00} music {AudioMix.UserMusic:0.00} sfx {AudioMix.UserSfx:0.00}"
                : "running");
        }

        void OnDestroy() => DebugOverlay.Unwatch("Pause");

        void Update()
        {
            // Once the run is over the end screen owns the keyboard: R restarts,
            // Esc quits. Pausing a finished run would trap the player behind a
            // menu with nothing to go back to.
            var flow = GameFlow.Instance;
            bool runOver = flow != null && flow.Phase != RunPhase.Playing;

            if (Input.GetKeyDown(pauseKey) && !runOver)
            {
                if (IsPaused) Resume(); else Pause();
            }
            if (runOver && IsPaused) Resume();   // never leave the menu up over an ended run

            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, IsPaused ? 1f : 0f,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            // Remember rather than assume 1: something else may already have been
            // slowing time, and stomping it here would be a very hard bug to find.
            restoreTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (group != null) { group.interactable = true; group.blocksRaycasts = true; }
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = restoreTimeScale <= 0f ? 1f : restoreTimeScale;
            if (group != null) { group.interactable = false; group.blocksRaycasts = false; }
            AudioMix.SaveUserVolumes();   // one disk write when the menu closes, not per slider frame
        }

        void OnMaster(float v) => AudioMix.UserMaster = v;
        void OnMusic(float v)  => AudioMix.UserMusic = v;
        void OnSfx(float v)    => AudioMix.UserSfx = v;

        void QuitToDesktop()
        {
            AudioMix.SaveUserVolumes();
            Time.timeScale = 1f;
            var flow = GameFlow.Instance;
            if (flow != null) flow.Quit();
#if UNITY_EDITOR
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            else Application.Quit();
#endif
        }
    }
}
