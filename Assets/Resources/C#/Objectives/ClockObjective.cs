// One clock objective. Broken until repaired; ClockRepair (player-side) drives
// its progress via the skill-check mini-game. When full it swaps to the glowing
// FIXED sprite and lights up (green Light2D), and fires AnyFixed so the manager
// can tally. Registers itself in a static list — no spawn-order coupling.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Objectives
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ClockObjective : MonoBehaviour
    {
        [SerializeField] Sprite brokenSprite;
        [SerializeField] Sprite fixedSprite;
        [SerializeField] Light2D glow;   // optional — the green light, on when fixed
        [SerializeField] ClockConfig config;

        public static readonly List<ClockObjective> All = new List<ClockObjective>();
        public static event System.Action<ClockObjective> AnyFixed;

        SpriteRenderer sr;
        public float Progress { get; private set; }
        public bool IsFixed { get; private set; }

        /// Where an effect should play ON this clock.
        ///
        /// The sprite pivots at its BASE (0.5, 0.06) so the clock stands on the
        /// floor, which means transform.position is the floor under it — a burst
        /// played there goes off at the clock's feet, a world unit and a half
        /// below the mechanism it is supposed to be coming from. Renderer bounds
        /// rather than a hand-placed anchor child, so re-drawing the clock art
        /// cannot silently leave the effect pointing at the wrong spot.
        public Vector2 EffectPoint
        {
            get
            {
                if (sr == null) sr = GetComponent<SpriteRenderer>();
                return sr != null ? (Vector2)sr.bounds.center : (Vector2)transform.position;
            }
        }

        /// The glow's authored intensity — the value the rise climbs TO.
        float litIntensity = 1f;
        /// Time.time the rise began, or -1 when nothing is rising.
        float riseStartedAt = -1f;

        public void Init(ClockConfig cfg) => config = cfg;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            // Captured ONCE, before anything animates it. Reading the target at
            // the moment of the rise instead would sample whatever the light
            // happened to be sitting at, so a rise interrupted by a scene reload
            // would latch its own half-way value and the clock would come back
            // dimmer every time.
            if (glow != null) litIntensity = glow.intensity;
        }

        void OnEnable() { All.Add(this); ApplyVisual(); }
        void OnDisable() => All.Remove(this);

        /// Skill-check result feeds progress here. Negative = a miss penalty.
        public void AddProgress(float delta)
        {
            if (IsFixed) return;
            Progress = Mathf.Clamp01(Progress + delta);
            if (Progress >= 1f) Fix();
        }

        void Fix()
        {
            IsFixed = true;
            Progress = 1f;
            ApplyVisual();

            // The light RISES rather than snapping. Before this the whole
            // moment — sprite swap and full-strength green light — happened in
            // one frame, which is why finishing a clock read as a state change
            // rather than as an achievement. Zero seconds restores the snap
            // exactly, so this is reversible from the config alone.
            float rise = config != null ? config.lightRiseSeconds : 0f;
            if (glow != null && glow.enabled && rise > 0f)
            {
                riseStartedAt = Time.time;
                glow.intensity = 0f;
            }

            AnyFixed?.Invoke(this);
        }

        void Update()
        {
            if (riseStartedAt < 0f) return;

            float rise = config != null ? config.lightRiseSeconds : 0f;
            if (glow == null || rise <= 0f) { riseStartedAt = -1f; return; }

            float t = Mathf.Clamp01((Time.time - riseStartedAt) / rise);
            // Ease-out: the light surges as the mechanism catches, then settles.
            // A linear ramp reads as a dimmer being turned by hand.
            glow.intensity = litIntensity * (1f - Mathf.Pow(1f - t, 3f));
            if (t >= 1f)
            {
                glow.intensity = litIntensity;
                riseStartedAt = -1f;
            }
        }

        void ApplyVisual()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (IsFixed && fixedSprite != null) sr.sprite = fixedSprite;
            else if (!IsFixed && brokenSprite != null) sr.sprite = brokenSprite;
            if (glow != null)
            {
                glow.enabled = IsFixed && (config == null || config.lightWhenFixed);
                // A clock that was ALREADY fixed when this ran (a scene reload,
                // an object being re-enabled) is not waking up — it is simply
                // on, and must not be left sitting at whatever intensity an
                // interrupted rise had reached.
                if (glow.enabled && riseStartedAt < 0f) glow.intensity = litIntensity;
            }
        }
    }
}
