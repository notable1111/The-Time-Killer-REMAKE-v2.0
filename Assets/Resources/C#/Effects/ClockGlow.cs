// Makes each clock its own lamp, so a repair changes the room instead of puffing.
//
// WHY. The user, 2026-08-28, on the two clock sprite bursts: "they are not bad but
// looks cheap you should work on it". They were one-shot decorations — they played
// and vanished, and the world was identical afterwards.
//
// THE REFERENCE (per the standing rule: name the game before designing). Dead by
// Daylight's generators are the same beat as our clocks — the objective you repair
// while being hunted — and their feedback is never a burst. Pistons move faster as
// charge builds, so progress reads from a distance. On completion the generator's
// lights turn on and light the area. All of it is on the object, persistent, and
// changes the room.
//
// So: the clock glows faintly while unrepaired, brightens with each earned press,
// and when fixed it STAYS LIT and lights the floor around it. That also solves a
// real gameplay problem nobody had listed — until now, once you walked away from a
// clock, nothing on screen told you whether you had finished it.
//
// SELF-INSTALLING and scene-free, like ThreatVision and ManiacThreatEffects: it
// works in every level including any added later, needs no scene edit, and so
// cannot collide with the other sessions sharing this Editor. The lights it makes
// are runtime objects — nothing is written into a scene file, ever.
//
// REMOVABLE: delete ClockGlowConfig.asset or untick `enabled`.
using System.Collections.Generic;
using TimeKiller.Core;
using TimeKiller.Objectives;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Effects
{
    public class ClockGlow : MonoBehaviour
    {
        ClockGlowConfig config;
        static ClockGlow instance;

        class Lamp
        {
            public Light2D Light;
            public float Progress;      // 0..1, drives the resting brightness
            public bool Fixed;
            public float PressAge = 999f;
            public float Phase;         // so two clocks never breathe in lockstep
        }

        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Vector2> positions = new List<Vector2>();

        public int LampCount => lamps.Count;
        public int FixedCount { get { int n = 0; foreach (var l in lamps) if (l.Fixed) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            var config = Resources.Load<ClockGlowConfig>(ClockGlowConfig.ResourcesPath);
            if (config == null || !config.enabled) return;
            var host = new GameObject("[ClockGlow]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ClockGlow>();
            instance.config = config;
        }

        void OnEnable()
        {
            EventBus.Subscribe<ClockHitEvent>(OnHit);
            EventBus.Subscribe<ClockFixedEvent>(OnFixed);
            DebugOverlay.Watch("ClockGlow", () => $"lamps {LampCount}  lit {FixedCount}");
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ClockHitEvent>(OnHit);
            EventBus.Unsubscribe<ClockFixedEvent>(OnFixed);
            DebugOverlay.Unwatch("ClockGlow");
        }

        /// Clocks are identified by WHERE they are, because neither event carries an
        /// id. Half a world unit is far tighter than any two clocks are placed and
        /// far looser than the jitter between a press and its completion.
        Lamp LampAt(Vector2 pos)
        {
            for (int i = 0; i < positions.Count; i++)
                if ((positions[i] - pos).sqrMagnitude < 0.25f) return lamps[i];

            var go = new GameObject("[ClockLamp]");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = config.glowColour;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = config.workingRadiusMin;
            light.intensity = config.workingIntensityMin;
            // Deliberately NOT a shadow caster. A clock is a small object in a
            // corner; casting from it would throw hard shadows across a room the
            // level's torches already light, and the point here is to MARK the
            // clock, not to relight the map.
            light.shadowsEnabled = false;

            var lamp = new Lamp { Light = light, Phase = positions.Count * 1.7f };
            lamps.Add(lamp);
            positions.Add(pos);
            return lamp;
        }

        void OnHit(ClockHitEvent evt)
        {
            var lamp = LampAt(evt.Position);
            lamp.Progress = Mathf.Clamp01(evt.Progress);
            lamp.PressAge = 0f;
        }

        void OnFixed(ClockFixedEvent evt)
        {
            var lamp = LampAt(evt.Position);
            lamp.Progress = 1f;
            lamp.Fixed = true;
            lamp.PressAge = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < lamps.Count; i++)
            {
                var lamp = lamps[i];
                if (lamp.Light == null) continue;
                lamp.PressAge += dt;

                // Resting level: ramps with progress, but stops short of the fixed
                // brightness so FINISHING is still a visible jump.
                float t = lamp.Fixed ? 1f : lamp.Progress * config.workingCeiling;
                float radius = Mathf.Lerp(config.workingRadiusMin, config.fixedRadius, t);
                float intensity = Mathf.Lerp(config.workingIntensityMin, config.fixedIntensity, t);

                // Eased swell, exponential fall — the same envelope shape as the
                // heartbeat and the threat shocks. A body has no instant edges and
                // neither does a machine turning over.
                float punch = 0f;
                if (lamp.PressAge < config.pressAttack)
                {
                    float a = lamp.PressAge / Mathf.Max(0.001f, config.pressAttack);
                    punch = a * a * (3f - 2f * a);
                }
                else
                {
                    punch = Mathf.Exp(-(lamp.PressAge - config.pressAttack)
                                      / Mathf.Max(0.02f, config.pressRelease / 3f));
                }
                intensity += config.pressPunch * punch;

                // A fixed clock breathes. Dead-steady reads as a UI marker pasted on
                // the world; breathing reads as a mechanism that is running.
                if (lamp.Fixed && config.breathAmount > 0f)
                    intensity *= 1f + config.breathAmount *
                                 Mathf.Sin((Time.time + lamp.Phase) * config.breathHz * Mathf.PI * 2f);

                lamp.Light.pointLightOuterRadius = radius;
                lamp.Light.intensity = intensity;
            }
        }
    }
}
