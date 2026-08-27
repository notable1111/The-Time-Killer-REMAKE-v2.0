// The maniac's senses.
//
// SIGHT is a GRADUAL AWARENESS model (2026-07-24), not a binary see/unsee:
// each frame a detection RATE is computed from how exposed the player is —
// distance, how centered they are in his vision (central cone strong, peripheral
// weak and close-only), how LIT they are (torchlight exposes, shadow hides), and
// whether they're MOVING (running spots fast, standing still is a real hiding
// tool). That rate fills an Awareness meter (0..1). When contact breaks the meter
// HOLDS for awarenessHoldSeconds before draining, so he stays onto you across a
// pillar instead of forgetting far faster than he could ever learn.
//   Awareness >= suspicionThreshold -> he INVESTIGATES a GUESS at your position.
//     Deliberately not your real one: the guess is offset by suspicionGuessError
//     in a direction drawn once per episode, and that error shrinks to nothing as
//     awareness climbs. Handing him your exact live coordinates (as this used to)
//     made being half-noticed identical to being seen, so nothing was ever at
//     stake in the doubt — he could not check the wrong side of a pillar.
//   Awareness >= 1 AND sensing NOW    -> fully SPOTTED: CanSeePlayer -> Chase.
//     CanSeePlayer requires LIVE contact, not merely a full meter: ChaseState
//     reads it as "line of sight is clear" and beelines while it is true.
// Walls block sight (linecast); a wardrobe hides you outright.
//
// HEARING: footstep/world noises within loudness*hearingRadius set the noise
// fields that drive Investigate — and since 2026-08-02 WALLS MUFFLE THEM. It had
// been the one sense that ignored geometry, so a footstep two rooms away through
// solid stone arrived exactly as loud as one taken beside him. Note what the fix
// does NOT need to do: distance already handles the far case, so muffling only
// has to fix the SAME distance heard THROUGH a wall. States read the flags; this
// component never drives movement itself.
using System.Collections.Generic;
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Maniac
{
    public class ManiacPerception : MonoBehaviour
    {
        public enum AwarenessLevel { Unaware, Suspicious, Detected }

        ManiacConfig config;
        Transform player;
        Light2D[] lights;          // cached torches/glows for exposure sampling
        Vector2 prevPlayerPos;
        float playerSpeed;
        bool havePrev;
        float senseLostTime = float.NegativeInfinity;  // when contact broke, for the awareness hold
        Vector2 guessDirection = Vector2.right;        // this episode's error direction
        TimeKiller.Navigation.ManiacNavigator nav;     // optional — only to keep guesses standable

        // Sight is tested every frame for the whole run, so the hit buffer and the
        // filter are built once. Physics2D.LinecastAll would hand back a freshly
        // allocated array on each of those calls.
        readonly List<RaycastHit2D> sightHits = new List<RaycastHit2D>(8);
        ContactFilter2D sightFilter;

        // Hearing casts against the same solid geometry as sight, but from an
        // EventBus callback rather than from Update. A second buffer means the two
        // can never end up sharing one list mid-iteration if that ever changes.
        readonly List<RaycastHit2D> hearingHits = new List<RaycastHit2D>(8);

        /// True while the player is inside a hiding spot — sight can't find them.
        public bool PlayerHidden { get; private set; }

        /// 0..1 stealth meter. Fills while exposed, drains when he loses you.
        public float Awareness { get; private set; }
        public AwarenessLevel Level { get; private set; } = AwarenessLevel.Unaware;

        public bool CanSeePlayer { get; private set; }   // == fully Detected (Awareness hit 1)
        public Vector2 LastSeenPosition { get; private set; }
        public Vector2 LastSeenDirection { get; private set; } = Vector2.zero; // flee bias for the search belief map
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;
        // ---- suspicion accounting (for playtest telemetry) -------------------
        // Measured HERE, per frame, rather than sampled by TestTelemetry at
        // 4 Hz. The suspicion band is routinely shorter than one sample: at
        // awarenessFillRate 2.6 the climb from suspicionThreshold to spotted
        // takes under 0.25s under good exposure, so a sampler misses most
        // crossings outright and reports a calm game that isn't one.

        /// Seconds Suspicious while still actively sensing something — the
        /// stalking beat, and the only part of this band that reflects LEVEL
        /// design rather than tuning.
        public float StalkSeconds { get; private set; }

        /// Seconds Suspicious while sensing nothing — the forgetting tail after
        /// he loses you. Structurally capped at
        /// (1 - suspicionThreshold) / awarenessDrainRate per lost contact, so it
        /// is a property of the CONFIG, not the level. Kept apart from
        /// StalkSeconds precisely so it can never be read as tension.
        public float FadeSeconds { get; private set; }

        /// Times suspicion first flickered up from Unaware — the "did he see
        /// me?" beat. Counted as events because durations here are short enough
        /// to be lost to any sampling rate.
        public int SuspicionEpisodes { get; private set; }

        public bool HasUnhandledNoise { get; private set; }
        public Vector2 LastNoisePosition { get; private set; }
        public float LastNoiseTime { get; private set; } = float.NegativeInfinity;
        /// Why the current noise exists. InvestigateState reads this to tell a
        /// heard footstep (walk straight over) from a suspicion (stop, stare,
        /// then close slowly on a spot he is not sure about).
        public NoiseCause LastNoiseCause { get; private set; } = NoiseCause.Sound;

        /// When the current suspicion episode began. The stare belongs to the
        /// EPISODE, not to a state entry: component update order is undefined, so
        /// a state that latched "is this a suspicion?" in Enter could read the
        /// previous frame's cause and skip the hesitation entirely.
        public float SuspicionStartedTime { get; private set; } = float.NegativeInfinity;
        public Vector2 FacingDirection { get; set; } = Vector2.down; // set by controller from velocity

        public void Init(ManiacConfig maniacConfig)
        {
            config = maniacConfig;
            // useTriggers false does what the old per-hit isTrigger skip did, in the
            // physics query itself; SetLayerMask also turns useLayerMask on.
            sightFilter = new ContactFilter2D { useTriggers = false };
            sightFilter.SetLayerMask(config.sightBlockers);
            nav = GetComponent<TimeKiller.Navigation.ManiacNavigator>();
            lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventBus.Subscribe<PlayerFootstepEvent>(OnFootstep);
            EventBus.Subscribe<PlayerHidEvent>(OnPlayerHid);
            EventBus.Subscribe<PlayerUnhidEvent>(OnPlayerUnhid);
            EventBus.Subscribe<WorldNoiseEvent>(OnWorldNoise);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerFootstepEvent>(OnFootstep);
            EventBus.Unsubscribe<PlayerHidEvent>(OnPlayerHid);
            EventBus.Unsubscribe<PlayerUnhidEvent>(OnPlayerUnhid);
            EventBus.Unsubscribe<WorldNoiseEvent>(OnWorldNoise);
        }

        void OnPlayerHid(PlayerHidEvent evt) => PlayerHidden = true;
        void OnPlayerUnhid(PlayerUnhidEvent evt) => PlayerHidden = false;

        /// Called by states when they start responding to the current noise.
        public void ConsumeNoise() => HasUnhandledNoise = false;

        void OnFootstep(PlayerFootstepEvent step)
        {
            if (config == null) return;
            if (!Reaches(step.Position, step.Loudness)) return;
            HearNoise(step.Position);
        }

        void OnWorldNoise(WorldNoiseEvent noise)
        {
            if (config == null) return;
            // AlwaysHeard is the LEVEL telling him something — the exit gate
            // grinding open. It is not a sound competing with the building, so
            // neither distance nor walls apply to it.
            if (!noise.AlwaysHeard && !Reaches(noise.Position, noise.Loudness)) return;
            HearNoise(noise.Position);
        }

        // Escalation, resolved lazily rather than in Awake: ManiacController adds
        // the component in ITS Awake and the order between two components on one
        // object is undefined, so caching here at startup could cache a null for
        // the whole run. Null stays null forever if the feature was removed, and
        // the scale is then a constant 1 — the hearing this shipped with.
        ManiacEscalation escalation;
        float HearingScale
        {
            get
            {
                if (escalation == null) escalation = GetComponent<ManiacEscalation>();
                return escalation != null ? escalation.Hearing : 1f;
            }
        }

        /// Does a noise of this loudness, made here, actually get to him?
        ///
        /// The escalation scale is applied HERE and not inside HeardRadiusFor on
        /// purpose: that static is pure, unit-tested and reads only the config, and
        /// a component-dependent multiplier inside it would make the same call
        /// return different answers in the same config — untestable by
        /// construction. Both comparisons take the scale so the cheap early-out
        /// can never reject a noise the real test would have accepted.
        bool Reaches(Vector2 position, float loudness)
        {
            float distance = Vector2.Distance(transform.position, position);
            float scale = HearingScale;
            // Cheapest test first: even with nothing in the way, is it in range at
            // all? Walls can only ever shrink the radius, so this rejects the vast
            // majority of footsteps without paying for a physics query.
            if (distance > config.hearingRadius * Mathf.Clamp01(loudness) * scale) return false;
            return distance <= HeardRadiusFor(config, loudness,
                                              WallsBetween(transform.position, position)) * scale;
        }

        /// How many solid bodies stand between him and a point.
        ///
        /// Counts DISTINCT colliders, which grades honestly across this castle's
        /// separate wall boxes — but a tilemap merged into one CompositeCollider2D
        /// reports a single hit however many of its walls the line crosses. So this
        /// is a FLOOR on the real count, never a measure of thickness, which is why
        /// the tuning knob is "how much survives one wall" rather than metres of
        /// stone. Under-counting only ever makes him hear better, i.e. it fails
        /// toward the old behaviour rather than toward a deaf maniac.
        int WallsBetween(Vector2 from, Vector2 to)
        {
            int count = Physics2D.Linecast(from, to, sightFilter, hearingHits);
            int walls = 0;
            for (int i = 0; i < count; i++)
            {
                var col = hearingHits[i].collider;
                if (col == null || col.isTrigger) continue;
                var root = col.transform.root;
                // His own body, and the player's — the noise is made AT the player's
                // feet, so their collider sits on the far end of every cast.
                if (root == transform.root) continue;
                if (player != null && root == player.root) continue;
                walls++;
            }
            return walls;
        }

        /// Pure: the radius within which a noise of this loudness still reaches him
        /// after `walls` solid bodies have muffled it. Each wall multiplies what is
        /// left, so depth falls off fast without needing a second cutoff knob.
        /// hearingWallMuffle = 1 reproduces the old geometry-blind hearing exactly.
        public static float HeardRadiusFor(ManiacConfig cfg, float loudness, int walls)
        {
            float radius = cfg.hearingRadius * Mathf.Clamp01(loudness);
            if (walls <= 0) return radius;
            return radius * Mathf.Pow(Mathf.Clamp01(cfg.hearingWallMuffle), walls);
        }

        // A discrete noise (footstep, gate) — sets the noise fields AND announces it.
        void HearNoise(Vector2 position)
        {
            SetNoise(position, NoiseCause.Sound);
            EventBus.Publish(new ManiacHeardNoiseEvent
            {
                NoisePosition = position,
                Cause = NoiseCause.Sound
            });
        }

        void SetNoise(Vector2 position, NoiseCause cause)
        {
            LastNoisePosition = position;
            LastNoiseTime = Time.time;
            LastNoiseCause = cause;
            HasUnhandledNoise = true;
        }

        /// Where he THINKS the movement was — never exactly where it was.
        ///
        /// The offset direction is drawn once per suspicion episode and then held,
        /// so his estimate slides smoothly toward the truth as awareness climbs
        /// instead of jittering around it. Re-rolling every frame would average
        /// out to the player's exact position, which is the bug this replaces.
        Vector2 GuessedPosition()
        {
            float error = GuessErrorFor(config, Awareness);
            return ToStandableSpot((Vector2)player.position + guessDirection * error, player.position);
        }

        // A guess inside a wall is worse than no guess: the navigator would find no
        // route, so he would press against the geometry and never "arrive" to look
        // around. Walk the guess back toward the truth until he can stand on it.
        Vector2 ToStandableSpot(Vector2 guess, Vector2 truth)
        {
            if (nav == null || !nav.Ready) return guess;
            for (int i = 0; i < 5; i++)
            {
                if (nav.IsWalkable(guess)) return guess;
                guess = Vector2.Lerp(guess, truth, 0.4f);
            }
            return truth;
        }

        void Update()
        {
            if (config == null) return;
            if (player == null)
            {
                var controller = Object.FindAnyObjectByType<PlayerController>();
                if (controller == null) return;
                player = controller.transform;
                prevPlayerPos = player.position;
            }

            TrackPlayerSpeed();

            float dt = Time.deltaTime;
            float rate = DetectionRate();
            bool sensing = rate > 0f;

            // He HOLDS what he'd built up for a moment before it decays — see
            // StepAwareness. Draining from the instant cover breaks made him forget
            // several times faster than he could ever learn.
            if (sensing) senseLostTime = float.NegativeInfinity;
            else if (float.IsNegativeInfinity(senseLostTime)) senseLostTime = Time.time;
            float sinceLost = float.IsNegativeInfinity(senseLostTime) ? 0f : Time.time - senseLostTime;
            Awareness = StepAwareness(config, Awareness, rate, dt, sinceLost);

            var newLevel = Awareness >= 1f ? AwarenessLevel.Detected
                         : Awareness >= config.suspicionThreshold ? AwarenessLevel.Suspicious
                         : AwarenessLevel.Unaware;

            // Split by whether he is still sensing (rate > 0 = closing in) or
            // merely forgetting (rate == 0 = draining). Counted before the
            // branch below so a rise straight through the band is not lost.
            if (newLevel == AwarenessLevel.Suspicious)
            {
                if (rate > 0f) StalkSeconds += dt;
                else FadeSeconds += dt;
                if (Level == AwarenessLevel.Unaware) SuspicionEpisodes++;
            }

            // EYES ON RIGHT NOW, not merely "the meter is full": the hold above
            // deliberately keeps Level at Detected through a sight break, and
            // ChaseState depends on CanSeePlayer meaning line of sight is clear
            // (true -> beeline, false -> A* around the wall). Letting the held
            // meter report sight would send him charging into geometry again.
            if (newLevel == AwarenessLevel.Detected && sensing)
            {
                if (Level != AwarenessLevel.Detected)
                    EventBus.Publish(new ManiacSpottedPlayerEvent { PlayerPosition = player.position });
                Vector2 now = player.position;
                Vector2 delta = now - LastSeenPosition;
                if (delta.sqrMagnitude > 0.0004f) LastSeenDirection = delta.normalized;
                LastSeenPosition = now;
                LastSeenTime = Time.time;
                CanSeePlayer = true;
            }
            else
            {
                CanSeePlayer = false;
                // Suspicious + still actively sensing something -> he comes to CHECK
                // your rough spot (drives Investigate; his movement turns him to look).
                if (newLevel == AwarenessLevel.Suspicious && rate > 0f)
                {
                    // One direction per episode, held while the episode lasts.
                    if (Level == AwarenessLevel.Unaware)
                    {
                        var roll = Random.insideUnitCircle;
                        guessDirection = roll.sqrMagnitude > 0.0001f ? roll.normalized : Vector2.right;
                        SuspicionStartedTime = Time.time;
                    }
                    var guess = GuessedPosition();
                    SetNoise(guess, NoiseCause.Suspicion);
                    if (Level == AwarenessLevel.Unaware) // first flicker of suspicion — a dark riser
                        EventBus.Publish(new ManiacHeardNoiseEvent
                        {
                            NoisePosition = guess,
                            Cause = NoiseCause.Suspicion
                        });
                }
            }
            Level = newLevel;
        }

        void TrackPlayerSpeed()
        {
            Vector2 now = player.position;
            float dt = Time.deltaTime;
            if (havePrev && dt > 0f)
            {
                float raw = (now - prevPlayerPos).magnitude / dt;
                if (raw < 20f) playerSpeed = raw; // ignore teleport spikes (tests, respawns)
            }
            prevPlayerPos = now;
            havePrev = true;
        }

        // 0 (undetectable this frame) .. 1 (ideal exposure). Drives the meter.
        // Only the scene-dependent parts live here — the wall check, how lit the
        // spot is, how fast the player is going. The arithmetic that decides how
        // hard the game is sits in the pure statics below, where it can be tested
        // without a scene (same reasoning as ManiacBrain.Score).
        float DetectionRate()
        {
            if (PlayerHidden) return 0f;
            Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
            float dist = toPlayer.magnitude;
            if (dist > config.sightRange) return 0f;
            if (!HasLineOfSight()) return 0f;               // a wall/prop is in the way
            return RateFor(config, dist, Vector2.Angle(FacingDirection, toPlayer),
                           Exposure(player.position), playerSpeed);
        }

        /// Pure detection rate. `angleFromFacing` in degrees, `exposure` 0..1.
        public static float RateFor(ManiacConfig cfg, float distance, float angleFromFacing,
                                    float exposure, float playerSpeed)
        {
            if (distance > cfg.sightRange) return 0f;
            if (distance <= cfg.proximityRange) return 1f;   // point-blank — he feels you

            float coneWeight;
            if (angleFromFacing <= cfg.centralConeAngle * 0.5f)
                coneWeight = 1f;                             // central vision
            else if (angleFromFacing <= cfg.peripheralConeAngle * 0.5f && distance <= cfg.peripheralRange)
                coneWeight = cfg.peripheralWeight;           // corner of the eye
            else
                return 0f;                                   // behind him

            // Closer = stronger, but NOT linearly to zero: at power 1 the mid range
            // was a dead zone (5u in shadow needed 4.5s of unbroken exposure, 6u
            // needed 9.1s), which is what made him feel blind past arm's reach.
            float distFactor = 1f - Mathf.Pow(distance / cfg.sightRange, cfg.sightFalloffPower);
            float lightFactor = Mathf.Lerp(cfg.exposureFloor, 1f, exposure);   // shadow hides
            float moveFactor = playerSpeed < 0.1f ? cfg.stillDetectionMultiplier
                             : playerSpeed >= cfg.runSpeedThreshold ? cfg.runningDetectionMultiplier
                             : 1f;
            return coneWeight * distFactor * lightFactor * moveFactor;
        }

        /// Pure awareness integration for one frame. `secondsSinceContactLost` is
        /// ignored while `rate > 0`. The HOLD is the whole point: draining from the
        /// instant cover breaks made him forget faster than he could ever learn.
        public static float StepAwareness(ManiacConfig cfg, float awareness, float rate,
                                          float deltaTime, float secondsSinceContactLost)
        {
            if (rate > 0f)
            {
                // HESITATION. Past the suspicion threshold the climb slows, so
                // becoming CERTAIN takes seconds rather than the measured 0.25s.
                // Noticing you is unchanged — everything below the threshold still
                // fills at the full rate — so this stretches the moment of being
                // caught without making the game easier to sneak through.
                float fill = cfg.awarenessFillRate;
                if (awareness >= cfg.suspicionThreshold) fill *= cfg.awarenessCertaintyScale;
                return Mathf.Clamp01(awareness + rate * fill * deltaTime);
            }
            if (secondsSinceContactLost < cfg.awarenessHoldSeconds) return awareness;
            return Mathf.Clamp01(awareness - cfg.awarenessDrainRate * deltaTime);
        }

        /// Pure: how far off his guess is at a given awareness. Shrinks to nothing
        /// as he grows certain.
        public static float GuessErrorFor(ManiacConfig cfg, float awareness) =>
            Mathf.Lerp(cfg.suspicionGuessError, 0f,
                       Mathf.InverseLerp(cfg.suspicionThreshold, 1f, awareness));

        bool HasLineOfSight()
        {
            int count = Physics2D.Linecast(transform.position, player.position, sightFilter, sightHits);
            for (int i = 0; i < count; i++)
            {
                var col = sightHits[i].collider;
                if (col == null || col.isTrigger) continue;
                var root = col.transform.root;
                if (root == transform.root || root == player.root) continue;
                return false;
            }
            return true;
        }

        // How lit the point is: ambient + nearby enabled point lights (torches,
        // clock glows, the open gate). 0 = deep shadow, 1 = full torchlight.
        float Exposure(Vector2 pos)
        {
            float e = config.ambientExposure;
            if (lights != null)
            {
                foreach (var l in lights)
                {
                    if (l == null || !l.enabled || !l.isActiveAndEnabled) continue;
                    if (l.lightType != Light2D.LightType.Point) continue;
                    float outer = l.pointLightOuterRadius;
                    if (outer <= 0f) continue;
                    float d = Vector2.Distance(pos, l.transform.position);
                    if (d >= outer) continue;
                    e += l.intensity * (1f - d / outer);
                }
            }
            return Mathf.Clamp01(e);
        }

        /// Seconds since he last had eyes on the player.
        public float TimeSinceSeen => Time.time - LastSeenTime;
    }
}
