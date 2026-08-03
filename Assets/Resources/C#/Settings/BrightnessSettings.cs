// Player brightness, for a game whose floor measures 10 luminance out of 255.
//
// WHY THIS IS NOT OPTIONAL. The castle is deliberately near-black: the stealth
// system is built on shadow (exposureFloor 0.2, ambientExposure 0.12) and the
// art is tuned for a dark room. On a cheap laptop panel, or in a bright room,
// that is not atmosphere — it is a black screen, and the game is unplayable. A
// brightness control is the single most-requested setting in horror games and
// its absence locks people out entirely.
//
// THE FAIRNESS RULE, and the reason this applies POST-PROCESSING ONLY:
//
//   ManiacPerception.Exposure() decides how visible the player is by reading
//   Light2D intensities in the WORLD. Post exposure lives at the end of the
//   render, long after that. So brightening the screen changes what the PLAYER
//   sees and provably nothing about what the MANIAC knows.
//
// Do NOT ever implement this by raising the global light or the ambient value.
// That would brighten the player's view AND change nothing about his detection,
// which sounds identical but is not: the player could then read shadows the
// stealth system still believes are hiding them, and "I was clearly visible and
// he walked past" reads as a broken AI rather than as a setting they changed.
//
// Owns its own Volume at the highest priority. URP blends per-parameter, so
// overriding postExposure alone cannot disturb the saturation and vignette that
// HealthVfxDirector drives on its own volume.
//
// Removable: delete the component and the game renders exactly as authored.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Settings
{
    public class BrightnessSettings : MonoBehaviour
    {
        const string Pref = "TimeKiller.Brightness";

        [Tooltip("Post exposure (EV) at the DARKEST setting. Negative goes below the authored look, for players in a blacked-out room who want it darker still.")]
        [SerializeField] float minExposure = -0.6f;
        [Tooltip("Post exposure (EV) at the BRIGHTEST setting. +1.6 EV is roughly a tripling of perceived brightness — enough to rescue a bad panel without washing the art to grey.")]
        [SerializeField] float maxExposure = 1.6f;
        [Tooltip("Volume priority. Above HealthVfx's so the exposure override always wins, though URP blends per-parameter so nothing else is touched anyway.")]
        [SerializeField] float volumePriority = 100f;

        static BrightnessSettings instance;
        Volume volume;
        ColorAdjustments exposure;

        /// 0..1, where 0.5 is the look the art was authored for. Persisted the
        /// moment it changes: a settings value that survives only until the game
        /// closes is one the player has to set every session.
        public static float Brightness
        {
            get => brightness;
            set
            {
                brightness = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(Pref, brightness);
                if (instance != null) instance.Apply();
            }
        }
        static float brightness = 0.5f;

        /// The EV actually being applied, for the settings UI and for debug.
        public static float CurrentExposure =>
            instance == null ? 0f
            : Mathf.Lerp(instance.minExposure, instance.maxExposure, brightness);

        public static void Save() => PlayerPrefs.Save();

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            brightness = PlayerPrefs.GetFloat(Pref, 0.5f);

            // Its own volume and its own profile, built at runtime: nothing to add
            // to a scene, nothing to wire, and no shared profile another feature
            // could overwrite. HideAndDontSave so it can never be saved into a
            // scene by accident.
            var go = new GameObject("~Brightness") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = volumePriority;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            exposure = volume.profile.Add<ColorAdjustments>(true);
            // ONLY postExposure is overridden. Leaving saturation/contrast alone
            // is what lets HealthVfx keep driving them from its own volume.
            exposure.postExposure.overrideState = true;
            exposure.saturation.overrideState = false;
            exposure.contrast.overrideState = false;
            Apply();
        }

        void Start() => TimeKiller.Core.DebugOverlay.Watch("Brightness",
            () => $"{brightness:0.00}  ({CurrentExposure:+0.00;-0.00} EV)  [post-process only — his sight is unaffected]");

        void OnDestroy()
        {
            TimeKiller.Core.DebugOverlay.Unwatch("Brightness");
            if (instance == this) instance = null;
            if (volume != null && volume.profile != null) Destroy(volume.profile);
        }

        void Apply()
        {
            if (exposure == null) return;
            exposure.postExposure.value = Mathf.Lerp(minExposure, maxExposure, brightness);
        }
    }
}
