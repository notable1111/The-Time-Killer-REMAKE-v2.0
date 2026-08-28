// Makes a clock's OWN light react while you are repairing it.
//
// WHY, AND WHAT THIS IS NOT. ClockObjective already owns a Light2D per clock and
// already raises it when the clock is FIXED. That is the Dead by Daylight
// "completed generator lights up" beat and it has been in this game for weeks —
// I wrote a second light system before checking, and deleted it (a0b74c0). This
// does not add a light. It drives the one that is already there.
//
// The gap that is actually real: DbD's generators show PROGRESS from a distance —
// the pistons move faster as charge builds — and ours show nothing until the
// moment they are done. Repair progress is invisible the second you look away
// from the skill-check bar. So the clock's light now fades up as the work goes in
// and pulses on each earned press.
//
// OWNERSHIP. ClockObjective is the gameplay lane's file and is not touched. This
// reaches the light the same way a designer would — the child object named
// "Glow" — rather than reflecting into a private field, so it breaks loudly and
// obviously if that rig ever changes, instead of silently doing nothing.
//
// THE HANDOVER MATTERS. While repairing, this drives the light. The moment the
// clock is fixed it stops touching it entirely and ClockObjective's own rise
// takes over, so the two never write the same value in the same frame — the bug
// that made the heartbeat read as mush and that a second clock light would have
// recreated.
//
// REMOVABLE: delete ClockProgressGlowConfig.asset, or untick its `enabled`.
using System.Collections.Generic;
using TimeKiller.Core;
using TimeKiller.Objectives;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Effects
{
    public class ClockProgressGlow : MonoBehaviour
    {
        ClockProgressGlowConfig config;
        static ClockProgressGlow instance;

        class Tracked
        {
            public ClockObjective Clock;
            public Light2D Glow;
            public float AuthoredIntensity;
            public float AuthoredRadius;
            public float PressAge = 999f;
            public bool HandedOver;     // fixed: ClockObjective owns it from here
        }

        readonly List<Tracked> tracked = new List<Tracked>();

        public int Tracking => tracked.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            var config = Resources.Load<ClockProgressGlowConfig>(ClockProgressGlowConfig.ResourcesPath);
            if (config == null || !config.enabled) return;
            var host = new GameObject("[ClockProgressGlow]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ClockProgressGlow>();
            instance.config = config;
        }

        void OnEnable()
        {
            EventBus.Subscribe<ClockHitEvent>(OnHit);
            DebugOverlay.Watch("ClockProgress", () =>
            {
                var sb = new System.Text.StringBuilder();
                foreach (var t in tracked)
                    sb.Append(t.HandedOver ? "FIXED " : (t.Clock != null ? t.Clock.Progress.ToString("0.00") + " " : "? "));
                return sb.Length == 0 ? "no clocks yet" : sb.ToString();
            });
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ClockHitEvent>(OnHit);
            DebugOverlay.Unwatch("ClockProgress");
        }

        void OnHit(ClockHitEvent evt)
        {
            var t = TrackNearest(evt.Position);
            if (t != null) t.PressAge = 0f;
        }

        /// Clocks are matched by position because ClockHitEvent carries no id.
        /// Half a unit is far tighter than any two clocks are placed.
        Tracked TrackNearest(Vector2 pos)
        {
            Sync();
            Tracked best = null;
            float bestD = 0.25f;
            foreach (var t in tracked)
            {
                if (t.Clock == null) continue;
                float d = ((Vector2)t.Clock.transform.position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        /// Picks up clocks as they register themselves. ClockObjective.All is a
        /// public static list maintained in OnEnable/OnDisable, so this needs no
        /// spawn-order coupling and survives a scene change.
        void Sync()
        {
            if (tracked.Count == ClockObjective.All.Count) return;
            tracked.Clear();
            foreach (var c in ClockObjective.All)
            {
                if (c == null) continue;
                Light2D glow = null;
                foreach (Transform child in c.transform)
                {
                    glow = child.GetComponent<Light2D>();
                    if (glow != null) break;
                }
                if (glow == null) continue;   // a clock with no light rig: leave it alone
                tracked.Add(new Tracked
                {
                    Clock = c,
                    Glow = glow,
                    AuthoredIntensity = glow.intensity,
                    AuthoredRadius = glow.pointLightOuterRadius,
                });
            }
        }

        void Update()
        {
            Sync();
            float dt = Time.deltaTime;
            for (int i = 0; i < tracked.Count; i++)
            {
                var t = tracked[i];
                if (t.Clock == null || t.Glow == null) continue;
                t.PressAge += dt;

                if (t.Clock.IsFixed)
                {
                    // Hand the light back, once. From here ClockObjective's own
                    // rise owns it and this must not write the same value.
                    if (!t.HandedOver) t.HandedOver = true;
                    continue;
                }
                t.HandedOver = false;

                float p = Mathf.Clamp01(t.Clock.Progress);
                if (p <= 0.001f && t.PressAge > 2f)
                {
                    // Untouched clock: leave it exactly as authored, dark.
                    if (t.Glow.enabled) t.Glow.enabled = false;
                    continue;
                }

                // Eased swell, exponential fall — the same envelope shape as the
                // heartbeat and the threat shocks, so every beat in this game
                // moves the same way.
                float punch;
                if (t.PressAge < config.pressAttack)
                {
                    float a = t.PressAge / Mathf.Max(0.001f, config.pressAttack);
                    punch = a * a * (3f - 2f * a);
                }
                else
                {
                    punch = Mathf.Exp(-(t.PressAge - config.pressAttack)
                                      / Mathf.Max(0.02f, config.pressRelease / 3f));
                }

                // Progress ramp stops short of the authored "fixed" brightness, so
                // FINISHING is still a jump rather than the last few percent.
                float level = p * config.workingCeiling;
                t.Glow.enabled = true;
                t.Glow.intensity = t.AuthoredIntensity * (level + config.pressPunch * punch);
                t.Glow.pointLightOuterRadius = t.AuthoredRadius *
                    Mathf.Lerp(config.workingRadiusScaleMin, 1f, level);
            }
        }
    }
}
