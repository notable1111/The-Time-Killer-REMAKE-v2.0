// The sound of him. Breath, throat and boots — never words.
//
// Before this existed the maniac was completely silent: no AudioSource lived
// anywhere under C#/Maniac. The consequence was a gameplay one, not an aesthetic
// one — the first information the game ever gave you about him was SEEING him,
// which is exactly why hiding measured as inert in the bot playtests. You cannot
// hide from something you have no warning about. The breathing bed is the
// warning, and it is the reason hiding can become a decision instead of a
// reaction.
//
// Deliberately NOT occluded by walls. Real stone would muffle him, but muffling
// is precisely the information the player needs, and a horror game that hides
// its tells from an attentive player is just an unfair one.
//
// Presentation only, like ManiacAnimationDriver: it reads his components, it
// never drives them. Delete the component and he simply goes silent again.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Maniac
{
    [RequireComponent(typeof(ManiacMotor))]
    public class ManiacVoice : MonoBehaviour
    {
        [SerializeField] ManiacVoiceConfig config;

        AudioSource breath;      // the loop — always running, volume is the gate
        AudioSource vocal;       // growls, roars, mutters
        AudioSource feet;        // separate so a step never cuts a growl short
        ManiacMotor motor;
        ManiacPerception perception;
        Transform listener;

        float stride;            // metres walked since the last footstep
        float nextMutter;
        float nextSpotted;
        float nextLostYou;
        string previousState = "";
        float voicedVolume;      // smoothed breath volume, before distance
        float voicedPitch;

        /// Audible level of the breath bed after distance, 0..1. Public because a
        /// warning the player cannot measure is a warning nobody can tune — the
        /// overlay and the bot playtester both read this.
        public float BreathLoudness => breath != null ? breath.volume : 0f;
        /// Metres to the listener, or -1 if there is nobody to hear him.
        public float DistanceToListener => listener != null
            ? Vector2.Distance(transform.position, listener.position) : -1f;

        public void Init(ManiacVoiceConfig voiceConfig) => config = voiceConfig;

        void Awake()
        {
            motor = GetComponent<ManiacMotor>();
            perception = GetComponent<ManiacPerception>();
            breath = MakeSource("Breath", loop: true);
            vocal = MakeSource("Vocal", loop: false);
            feet = MakeSource("Footsteps", loop: false);
        }

        /// All three sources share one important piece of setup: a FLAT rolloff
        /// curve. Unity's own 3D attenuation is unusable here — the AudioListener
        /// rides the camera, and CameraFollow keeps the camera at its authored Z
        /// (conventionally -10 in a 2D setup), so Unity would measure the distance
        /// to him as sqrt(d^2 + 100). Standing on top of him would read as 10
        /// units away and an 11-unit hearing radius would be silent everywhere.
        /// So Unity does the panning and this component does the distance.
        AudioSource MakeSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;                 // keep the panning: "he is to my left"
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
            source.minDistance = 1f;
            source.maxDistance = 500f;                // never Unity-attenuated; see above
            source.volume = 0f;
            return source;
        }

        void Start()
        {
            EventBus.Subscribe<ManiacStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Subscribe<ManiacAttackEvent>(OnAttack);

            if (config != null && config.breathLoop != null)
            {
                breath.clip = config.breathLoop;
                breath.Play();                        // always running; volume is the gate
            }
            ScheduleMutter();

            DebugOverlay.Watch("ManiacVoice", () => config == null || !config.enabled
                ? "OFF"
                : $"{DistanceToListener:0.0}u vol {breath.volume:0.00} pitch {breath.pitch:0.00}");
        }

        void OnDestroy()
        {
            // A scene reload destroys him. A presence claim with no claimant left
            // would duck the music for the rest of the session.
            TimeKiller.Audio.AudioMix.SetPresence(TimeKiller.Audio.MixChannel.ManiacFootsteps, false);
            OnDestroyInner();
        }

        void OnDestroyInner()
        {
            EventBus.Unsubscribe<ManiacStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Unsubscribe<ManiacAttackEvent>(OnAttack);
            DebugOverlay.Unwatch("ManiacVoice");
        }

        void Update()
        {
            if (config == null || !config.enabled) { Silence(); return; }
            float dt = Time.deltaTime;

            if (listener == null)
            {
                var found = Object.FindAnyObjectByType<AudioListener>();
                if (found != null) listener = found.transform;
            }

            float distance = DistanceToListener;
            float reach = distance < 0f ? 0f : Attenuation(distance);

            DriveBreath(dt, reach);
            DriveFootsteps(dt, reach);
            DriveMutters(reach);

            // "He is HERE" — a standing claim on the mix, not a per-step one.
            // His footsteps are the primary where-is-he signal in a hide-and-run
            // game, and until now they never claimed the mix at all: the channel
            // outranks Music and Ambience on paper, but a channel only makes
            // others step back if it announces, and announcing every step would
            // park the music permanently (he steps every 0.433s at patrol against
            // ~0.43s clips). Presence is the shape that fits: it holds while he is
            // close and releases when he leaves. reach is already the linear
            // distance curve the rest of this component runs on, so "near" costs
            // nothing extra to compute and moves with the same radii.
            TimeKiller.Audio.AudioMix.SetPresence(
                TimeKiller.Audio.MixChannel.ManiacFootsteps, reach >= config.presenceReach);
        }

        /// Linear in amplitude between the two radii. Real sound falls off with
        /// the inverse square, but inverse-square is unreadable as a distance cue
        /// — nearly all of the change happens in the first metre and the rest is
        /// an undifferentiated whisper. Linear trades physics for the thing the
        /// system exists to provide: knowing how far away he is.
        float Attenuation(float distance)
        {
            if (distance <= config.fullVolumeRadius) return 1f;
            if (distance >= config.hearingRadius) return 0f;
            return 1f - Mathf.InverseLerp(config.fullVolumeRadius, config.hearingRadius, distance);
        }

        void DriveBreath(float dt, float reach)
        {
            float targetVolume = config.breathVolumeUnaware;
            float targetPitch = config.breathPitchCalm;
            if (perception != null)
            {
                switch (perception.Level)
                {
                    case ManiacPerception.AwarenessLevel.Detected:
                        targetVolume = config.breathVolumeDetected;
                        targetPitch = config.breathPitchHunting;
                        break;
                    case ManiacPerception.AwarenessLevel.Suspicious:
                        targetVolume = config.breathVolumeSuspicious;
                        // Halfway: he is working, but not yet certain.
                        targetPitch = Mathf.Lerp(config.breathPitchCalm, config.breathPitchHunting, 0.5f);
                        break;
                }
            }

            // Blended rather than snapped, so the change reads as him getting
            // interested rather than as a switch being thrown.
            float t = 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, config.breathBlendSeconds));
            voicedVolume = Mathf.Lerp(voicedVolume, targetVolume, t);
            voicedPitch = Mathf.Lerp(voicedPitch <= 0f ? targetPitch : voicedPitch, targetPitch, t);

            // Report the INTENDED loudness (before the mix trims it), so the
            // heartbeat's low-band duck follows how close he actually is rather
            // than chasing the mix's own output — that would be a loop.
            TimeKiller.Audio.AudioMix.ReportManiacBreath(voicedVolume * reach);

            breath.volume = voicedVolume * reach
                          * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.ManiacBreath);
            breath.pitch = voicedPitch;
        }

        void DriveFootsteps(float dt, float reach)
        {
            if (config.footstepClips == null || config.footstepClips.Length == 0) return;
            float speed = motor != null ? motor.CurrentVelocity.magnitude : 0f;
            if (speed < 0.15f) { stride = 0f; return; }   // standing: no phantom steps

            // Cadence from DISTANCE TRAVELLED, not animation event frames. His
            // clips are not guaranteed to carry foot-contact frames the way the
            // player's run clips do, and distance stays correct at every speed
            // including the slow investigate walk.
            stride += speed * dt;
            if (stride < config.strideMeters) return;
            stride -= config.strideMeters;

            if (reach <= 0f) return;                      // too far to bother playing
            var clip = config.footstepClips[Random.Range(0, config.footstepClips.Length)];
            feet.pitch = 1f + Random.Range(-config.pitchJitter, config.pitchJitter);
            feet.PlayOneShot(clip, config.footstepVolume * reach
                * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.ManiacFootsteps));
        }

        void DriveMutters(float reach)
        {
            if (Time.time < nextMutter) return;
            ScheduleMutter();
            // Only while he has no idea. Once he is hunting, the growls carry the
            // moment and an idle throat noise on top would undercut them.
            if (perception != null && perception.Level != ManiacPerception.AwarenessLevel.Unaware) return;
            PlayVocal(config.idleMutters, reach);
        }

        void ScheduleMutter() =>
            nextMutter = Time.time + (config == null ? 20f
                : Random.Range(config.mutterMinInterval, config.mutterMaxInterval));

        void OnSpotted(ManiacSpottedPlayerEvent evt)
        {
            if (config == null || !config.enabled || Time.time < nextSpotted) return;
            nextSpotted = Time.time + config.spottedCooldown;
            PlayVocal(config.spottedGrowls, ReachNow());
        }

        void OnAttack(ManiacAttackEvent evt)
        {
            if (config == null || !config.enabled) return;
            PlayVocal(config.attackRoars, ReachNow());
        }

        /// He had you and now he does not. This is the mirror of the player's
        /// recovery breath: their relief and his frustration are the same moment
        /// heard from the two ends of it.
        void OnStateChanged(ManiacStateChangedEvent evt)
        {
            string was = previousState;
            previousState = evt.StateName;
            if (config == null || !config.enabled) return;
            if (was != nameof(ChaseState) || evt.StateName != nameof(SearchState)) return;
            if (Time.time < nextLostYou) return;
            nextLostYou = Time.time + config.lostYouCooldown;
            PlayVocal(config.lostYouGrowls, ReachNow());
        }

        float ReachNow()
        {
            float distance = DistanceToListener;
            return distance < 0f ? 0f : Attenuation(distance);
        }

        void PlayVocal(AudioClip[] pool, float reach)
        {
            if (pool == null || pool.Length == 0 || reach <= 0f || vocal == null) return;
            var clip = pool[Random.Range(0, pool.Length)];
            vocal.pitch = 1f;
            // Announce first: the duck has to be moving before the growl lands,
            // not after it has already been buried by the score.
            TimeKiller.Audio.AudioMix.Announce(TimeKiller.Audio.MixChannel.ManiacVocal, clip.length);
            vocal.PlayOneShot(clip, config.vocalVolume * reach
                * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.ManiacVocal));
        }

        void Silence()
        {
            if (breath != null) breath.volume = 0f;
        }
    }
}
