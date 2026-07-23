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

        public void Init(ClockConfig cfg) => config = cfg;

        void Awake() => sr = GetComponent<SpriteRenderer>();

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
            AnyFixed?.Invoke(this);
        }

        void ApplyVisual()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (IsFixed && fixedSprite != null) sr.sprite = fixedSprite;
            else if (!IsFixed && brokenSprite != null) sr.sprite = brokenSprite;
            if (glow != null) glow.enabled = IsFixed && (config == null || config.lightWhenFixed);
        }
    }
}
