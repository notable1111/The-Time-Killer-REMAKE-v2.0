// The one place that decides how frightened the player is.
//
// WHY IT EXISTS. Tension used to be computed three times over: the heart ran a
// distance curve with awareness FLOORS clamped on top, breathing ran its own
// exertion model, and the world duck ran off the heart's intensity. Three
// opinions that could disagree, and — worse — floors, which snap. A maniac 30u
// away flipping to Detected slammed the heart from 88 to 150 bpm instantly:
// maximum panic for a threat that could not possibly reach you.
//
// THE MODEL. One continuous value:
//
//   raw   = distanceCurve(proximity) * awareness * closing
//   fear  = max(smoothed(raw), memoryFloor)
//
// Awareness and closing speed are MULTIPLIERS, and that is the whole trick:
// any multiple of near-zero proximity is still near-zero, so context can colour
// fear without ever manufacturing it. Being seen from across the castle raises
// your pulse a little. Being seen from six feet is panic.
//
// WHAT IT WILL NOT TELL YOU. The heartbeat is non-directional and deliberately
// incomplete: it says "something is wrong and getting worse", never where he is,
// which way to run, or whether this wall is safe. Directional information stays
// with his footsteps and voice, which are 3D and occluded. A heartbeat that
// answered "where" would be a radar, and a radar is not frightening.
//
// Removable: delete this component and every subscriber falls back to its own
// behaviour. It publishes; it is never referenced.
using System.Collections.Generic;
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Navigation;
using UnityEngine;

namespace TimeKiller.Fear
{
    public class FearConductor : MonoBehaviour
    {
        [SerializeField] FearConfig config;

        public FearConfig Config => config;

        /// THE number. 0..1, smoothed, what every channel answers to.
        public float Fear { get; private set; }
        /// Where fear is heading before smoothing — useful for reading the system.
        public float TargetFear { get; private set; }
        public FearStage Stage { get; private set; } = FearStage.Safe;
        /// True while fear is decaying rather than responding to a live threat.
        public bool Recovering { get; private set; }

        // ---- what the current threat looks like (debug + telemetry) ----------
        public string ThreatName { get; private set; } = "none";
        public float DirectDistance { get; private set; } = float.PositiveInfinity;
        public float ThreatDistance { get; private set; } = float.PositiveInfinity;
        public bool UsingPathDistance { get; private set; }
        public float DistanceProximity { get; private set; }
        public string AwarenessName { get; private set; } = "-";
        public float ClosingSpeed { get; private set; }
        public float RecoveryTimer { get; private set; }
        public string ConfigProblem { get; private set; } = "";

        readonly List<ManiacController> threats = new List<ManiacController>(4);
        ManiacController driver;          // the threat currently setting fear
        Transform player;

        float memoryPeak;
        float memoryUntil;
        float holdUntil;                  // recovery delay: fear may not fall yet
        float lastDistance = float.PositiveInfinity;
        float nextPathAt;
        float cachedPathDistance = float.PositiveInfinity;
        bool cachedPathValid;
        bool insideRadius;                // hysteresis latch
        bool hidden;
        bool wasDetected;
        float lastStingAt = float.NegativeInfinity;
        float rescanAt;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<FearConfig>("C#/Fear/Configs/FearConfig");
        }

        public void Init(FearConfig fearConfig) => config = fearConfig;

        void Start()
        {
            var pc = Object.FindAnyObjectByType<TimeKiller.Player.PlayerController>();
            player = pc != null ? pc.transform : transform;

            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            RegisterOverlay();
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Unwatch("Fear");
            DebugOverlay.Unwatch("Fear src");
            DebugOverlay.Unwatch("Fear out");
            // Never leave the world ducked behind us — the dial is shared and a
            // deleted writer must not mute the game for whatever remains.
            AudioDucking.SetWorld(1f);
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent e) => hidden = true;
        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent e) => hidden = false;

        void Update()
        {
            if (config == null) { ConfigProblem = "NO FearConfig"; return; }
            ConfigProblem = "";
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            TargetFear = config.debugOverrideFear ? Mathf.Clamp01(config.debugFear) : ComputeTargetFear(dt);

            Smooth(dt);
            ApplyMemory();
            Stage = StageFor(Fear);

            // Visuals are scaled HERE so subscribers never have to know the rules.
            // Deliberately restrained: a light vignette and a small throb. No red
            // flash, no blur, no shake, no FOV pumping — those read as damage
            // feedback or as a bug, and they make people feel ill.
            bool visuals = config.visualFearEnabled;
            float scale = config.fearEffectMultiplier;
            EventBus.Publish(new FearChangedEvent
            {
                Fear = Fear,
                Stage = Stage,
                Recovering = Recovering,
                VisualVignette = visuals ? config.visualFearVignette * Fear * scale : 0f,
                VisualPulse = visuals ? config.visualPulseIntensity * Fear * scale : 0f,
                VisualDesaturation = visuals ? config.visualDesaturation * Fear * scale : 0f,
            });
            UpdateDuck(dt);
        }

        // ---------------------------------------------------------------- input

        float ComputeTargetFear(float dt)
        {
            var threat = SelectThreat();
            if (threat == null || threat.Perception == null)
            {
                driver = null;
                ThreatName = "none";
                DirectDistance = ThreatDistance = float.PositiveInfinity;
                DistanceProximity = 0f;
                AwarenessName = "-";
                ClosingSpeed = 0f;
                insideRadius = false;
                lastDistance = float.PositiveInfinity;
                return 0f;
            }

            driver = threat;
            ThreatName = threat.name;
            Vector2 here = player != null ? (Vector2)player.position : (Vector2)transform.position;
            DirectDistance = Vector2.Distance(here, threat.Motor.Position);
            ThreatDistance = ResolveThreatDistance(threat, here, DirectDistance);

            // Hysteresis: once afraid, the threat has to leave a WIDER circle
            // before it stops counting, so a maniac loitering on the boundary
            // cannot start and stop the heartbeat over and over.
            float outer = config.fearStartDistance + (insideRadius ? config.exitHysteresis : 0f);
            insideRadius = ThreatDistance <= outer;
            if (!insideRadius)
            {
                DistanceProximity = 0f;
                AwarenessName = Describe(threat);
                ClosingSpeed = 0f;
                lastDistance = ThreatDistance;
                return 0f;
            }

            float t = Mathf.InverseLerp(config.closeDangerDistance, outer, ThreatDistance);
            DistanceProximity = Mathf.Clamp01(1f - t);
            float shaped = Mathf.Clamp01(config.distanceCurve.Evaluate(DistanceProximity));

            // Closing speed, from the threat distance actually in use so a maniac
            // rounding a corner does not read as a teleport.
            float closing = 0f;
            if (!float.IsInfinity(lastDistance))
                closing = (lastDistance - ThreatDistance) / dt;
            lastDistance = ThreatDistance;
            ClosingSpeed = closing;
            float closingMul = 1f + config.closingBoost *
                Mathf.Clamp01(closing / Mathf.Max(0.01f, config.closingSpeedForFull));

            AwarenessName = Describe(threat);
            float awarenessMul = AwarenessMultiplier(threat);

            FireStingIfDetected(threat, shaped * awarenessMul * closingMul);

            return Mathf.Clamp01(shaped * awarenessMul * closingMul);
        }

        /// The most threatening LIVE maniac. Destroyed, disabled and dead
        /// references are filtered every rescan, so a killed enemy cannot keep
        /// driving fear from beyond the grave.
        ManiacController SelectThreat()
        {
            if (Time.time >= rescanAt)
            {
                rescanAt = Time.time + 1f;
                threats.Clear();
                threats.AddRange(Object.FindObjectsByType<ManiacController>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            }

            Vector2 here = player != null ? (Vector2)player.position : (Vector2)transform.position;
            ManiacController best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < threats.Count; i++)
            {
                var m = threats[i];
                // A destroyed MonoBehaviour compares == null through Unity's
                // overload; isActiveAndEnabled catches disabled-but-alive.
                if (m == null || !m.isActiveAndEnabled || m.Perception == null) continue;

                float distance = Vector2.Distance(here, m.Motor.Position);
                // Rank by closeness, nudged by how switched-on he is — the nearer
                // maniac usually matters more, but one actively chasing at 12u
                // outranks one idly patrolling at 10u. Never SUM threats: two
                // distant enemies must not add up to panic.
                float score = -distance + AwarenessMultiplier(m) * 3f;
                if (score > bestScore) { bestScore = score; best = m; }
            }
            return best;
        }

        float AwarenessMultiplier(ManiacController m)
        {
            var perception = m.Perception;
            switch (perception.Level)
            {
                case ManiacPerception.AwarenessLevel.Detected:
                    return config.detectedMultiplier;
                case ManiacPerception.AwarenessLevel.Suspicious:
                    return config.suspiciousMultiplier;
                default:
                    // Unaware splits in two. His meter climbs long before it
                    // crosses into Suspicious and you have no way to see it — so
                    // the body reacts before the mind knows why, which is the
                    // engine of anticipatory panic. Blended by the meter itself
                    // so there is no step at the boundary either.
                    float noticing = Mathf.Clamp01(perception.Awareness /
                        Mathf.Max(0.01f, m.Config != null ? m.Config.suspicionThreshold : 0.4f));
                    float baseMul = Mathf.Lerp(config.unawareMultiplier, config.noticingMultiplier, noticing);
                    // Searching is a distinct, nameable dread: he lost you and is
                    // hunting. Only counts when he is actually in that state.
                    if (IsSearching(m)) baseMul = Mathf.Max(baseMul, config.searchingMultiplier);
                    return baseMul;
            }
        }

        static bool IsSearching(ManiacController m) =>
            m.Perception != null && !float.IsNegativeInfinity(m.Perception.LastSeenTime)
            && m.Config != null
            && Time.time - m.Perception.LastSeenTime < m.Config.brainSearchMemory;

        string Describe(ManiacController m)
        {
            switch (m.Perception.Level)
            {
                case ManiacPerception.AwarenessLevel.Detected: return "detected";
                case ManiacPerception.AwarenessLevel.Suspicious: return "suspicious";
                default: return IsSearching(m) ? "searching" : "unaware";
            }
        }

        /// Path distance where we can get it, straight line where we cannot.
        ///
        /// A maniac on the far side of a wall is not as dangerous as one the same
        /// number of metres away down an open corridor, and straight-line distance
        /// cannot tell those apart. The navigator can. Guarded two ways: the query
        /// is rate-limited, and an implausibly long route (the router going round
        /// the whole castle) is discarded rather than trusted, because reporting a
        /// maniac in the next doorway as "far" is the worse failure.
        float ResolveThreatDistance(ManiacController threat, Vector2 here, float direct)
        {
            UsingPathDistance = false;
            if (!config.usePathDistance) return direct;

            var nav = threat.Nav;
            if (nav == null || !nav.Ready) return direct;

            if (Time.time >= nextPathAt)
            {
                nextPathAt = Time.time + Mathf.Max(0.05f, config.pathInterval);
                cachedPathValid = false;
                var finder = nav.NavFinder;
                if (finder != null)
                {
                    var route = finder.FindPath(threat.Motor.Position, here);
                    if (route != null && route.Count > 0)
                    {
                        float length = Vector2.Distance(threat.Motor.Position, route[0]);
                        for (int i = 1; i < route.Count; i++)
                            length += Vector2.Distance(route[i - 1], route[i]);
                        cachedPathDistance = length;
                        cachedPathValid = true;
                    }
                }
            }

            if (!cachedPathValid) return direct;
            if (cachedPathDistance > direct * config.maxPathDetour) return direct;
            UsingPathDistance = true;
            return Mathf.Max(direct, cachedPathDistance);
        }

        // ------------------------------------------------------------- dynamics

        void Smooth(float dt)
        {
            float target = TargetFear;

            if (target > Fear)
            {
                // Rising. A sudden jump gets the adrenaline response; an ordinary
                // approach gets the slow build. Chosen by how fast the target is
                // pulling away, not by which state flag changed — so a fast
                // approach is as startling as a detection, which is correct.
                float gap = target - Fear;
                bool spike = gap / Mathf.Max(0.0001f, dt) > config.spikeThreshold;
                float seconds = spike ? config.detectionRiseSeconds : config.riseSeconds;
                Fear = Mathf.Lerp(Fear, target, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, seconds)));
                holdUntil = Time.time + config.recoveryDelaySeconds;
                Recovering = false;
                RecoveryTimer = 0f;
                RememberPeak();
                return;
            }

            // Falling. HOLD first: fear does not begin to drop the instant he
            // turns away, or breaking line of sight for one frame would collapse
            // the whole system and corners would read as safety.
            RecoveryTimer = Mathf.Max(0f, holdUntil - Time.time);
            if (Time.time < holdUntil) { Recovering = true; return; }

            Recovering = Fear > 0.001f;
            Fear = Mathf.Lerp(Fear, target, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, config.recoverySeconds)));
            if (Fear < 0.002f) { Fear = 0f; Recovering = false; }
        }

        void RememberPeak()
        {
            if (Fear <= memoryPeak && Time.time < memoryUntil) return;
            memoryPeak = Fear;
            memoryUntil = Time.time + config.memorySeconds;
        }

        /// Recently-bad keeps fear propped up. Without this the player learns that
        /// one corner resets the game's opinion of the situation.
        void ApplyMemory()
        {
            if (Time.time >= memoryUntil) { memoryPeak = 0f; return; }
            float remaining = Mathf.InverseLerp(memoryUntil - config.memorySeconds, memoryUntil, Time.time);
            float floor = memoryPeak * config.memoryFloorShare * (1f - remaining);
            if (floor > Fear) { Fear = floor; Recovering = true; }
        }

        FearStage StageFor(float fear)
        {
            if (fear >= config.panicAt) return FearStage.Panic;
            if (fear >= config.threatAt) return Recovering ? FearStage.Aftershock : FearStage.Threat;
            if (fear >= config.uneaseAt) return Recovering ? FearStage.Aftershock : FearStage.Unease;
            return FearStage.Safe;
        }

        // --------------------------------------------------------------- output

        void UpdateDuck(float dt)
        {
            // The world recedes SLIGHTLY — 0.75 is about -2.5 dB. The old system
            // pulled it to 0.08, which mutes the level and takes the maniac's own
            // footsteps with it, removing the directional information the player
            // needs exactly when they need it most.
            float pull = Fear * config.fearEffectMultiplier;
            float want = Mathf.Lerp(1f, config.ambienceAtMaxFear, Mathf.Clamp01(pull));
            float seconds = want < AudioDucking.World ? config.duckInSeconds : config.duckOutSeconds;
            AudioDucking.SetWorld(Mathf.MoveTowards(AudioDucking.World, want, dt / Mathf.Max(0.01f, seconds)));
        }

        void FireStingIfDetected(ManiacController threat, float rawFear)
        {
            bool detected = threat.Perception.Level == ManiacPerception.AwarenessLevel.Detected;
            bool rising = detected && !wasDetected;
            wasDetected = detected;
            if (!rising || !config.stingEnabled) return;
            // Two gates, both needed. The cooldown stops the machine-gun (his
            // Detected flag flickers every time a pillar breaks line of sight),
            // and the fear gate stops a sting for a sighting too far away to
            // matter — a jump scare that is not scary spends the effect for free.
            if (Time.time - lastStingAt < config.stingCooldown) return;
            if (rawFear < config.stingNeedsFear) return;

            lastStingAt = Time.time;
            EventBus.Publish(new FearDetectionEvent { Fear = rawFear, Distance = DirectDistance });
        }

        // ---------------------------------------------------------------- debug

        public bool Hidden => hidden;

        void RegisterOverlay()
        {
            DebugOverlay.Watch("Fear", () =>
            {
                if (config == null) return "NO CONFIG";
                string over = config.debugOverrideFear ? " [OVERRIDE]" : "";
                int bars = Mathf.RoundToInt(Fear * 10f);
                return $"[{new string('#', bars)}{new string('.', 10 - bars)}] {Fear:0.00} " +
                       $"{Stage}{(Recovering ? " (recovering)" : "")} tgt {TargetFear:0.00}" +
                       $" hold {RecoveryTimer:0.0}s{over}";
            });
            DebugOverlay.Watch("Fear src", () =>
            {
                if (config == null) return "NO CONFIG";
                if (driver == null) return "no threat in range";
                string kind = UsingPathDistance ? "path" : "direct";
                return $"{ThreatName} {AwarenessName}  {kind} {ThreatDistance:0.0}u " +
                       $"(direct {DirectDistance:0.0}u)  prox {DistanceProximity:0.00}  " +
                       $"closing {ClosingSpeed:+0.0;-0.0}u/s";
            });
        }

        void OnDrawGizmosSelected()
        {
            if (config == null || !config.drawGizmos) return;
            Vector3 at = transform.position;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(at, config.fearStartDistance);
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(at, config.closeDangerDistance);
        }
    }
}
