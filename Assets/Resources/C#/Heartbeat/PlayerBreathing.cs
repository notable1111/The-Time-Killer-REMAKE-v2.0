// The player's breathing — EXERTION, not fear.
//
// The first version followed the heartbeat's fear level, which meant the player
// panted whenever the maniac was merely nearby. Constant breathing is wallpaper:
// the ear stops hearing it, and it stops meaning anything. Rejected by ear on
// 2026-07-28 and replaced with how a body actually works:
//
//   BEING CHASED winds you, over seconds, not instantly. Ordinary running does
//     NOT — that was tried and rejected, because the player runs almost
//     constantly and it turned breathing into a permanent bed nobody heard.
//   HIM LOSING YOU recovers you, slower than it took to get winded — catching
//     your breath always takes longer than losing it.
//   LOSING HIM triggers one long recovery breath: the moment you realise you got
//     away. That single sound is the whole reward for surviving a chase, and it
//     lands in the silence right after the music has ducked back.
//
// It reads the maniac and the player's own speed directly. It deliberately does
// NOT read the heartbeat's fear level — that coupling is exactly what made it
// constant. Heart and lungs answer different questions now: the heart says how
// dangerous this is, the lungs say how hard you have been working.
//
// NOT stamina. Nothing here affects movement and no meter is shown.
// Removable: delete it and the game is simply silent-lunged.
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Heartbeat
{
    [RequireComponent(typeof(AudioSource))]
    public class PlayerBreathing : MonoBehaviour
    {
        [SerializeField] BreathingConfig config;
        [SerializeField] AudioClip breathLoop;
        [Tooltip("The long recovery breath played once when a chase ends.")]
        [SerializeField] AudioClip gaspClip;

        AudioSource loopSource;
        AudioSource oneShotSource;
        ManiacController maniac;

        float exertion;      // 0..1, how winded you are
        float voiced;        // smoothed value actually driving the audio
        bool hidden;
        bool wasChased;

        /// 0..1 how out of breath the player is. Public for debug and tuning.
        public float Exertion => exertion;
        public bool Holding { get; private set; }
        /// How many recovery breaths have played this run. Exertion alone cannot
        /// evidence this — it decays to zero on its own, so a low reading is
        /// equally consistent with "the breath fired" and "nothing happened".
        public int RecoveryBreathCount { get; private set; }

        public void Init(BreathingConfig breathingConfig, AudioClip loop, AudioClip recovery)
        {
            config = breathingConfig;
            breathLoop = loop;
            gaspClip = recovery;
        }

        void Awake()
        {
            loopSource = GetComponent<AudioSource>();
            // A second source for the recovery breath: the loop is mid-cycle when
            // it fires, and a one-shot on the same source would fight its pitch.
            var go = new GameObject("RecoveryBreath");
            go.transform.SetParent(transform, false);
            oneShotSource = go.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.spatialBlend = 0f;
        }

        void Start()
        {
            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Watch("Breath", () => config == null
                ? "NO CONFIG"
                : Holding ? $"HELD (winded {exertion:0.00}) recoveries {RecoveryBreathCount}"
                          : voiced < config.silenceBelow ? $"calm (winded {exertion:0.00}) recoveries {RecoveryBreathCount}"
                          : $"winded {exertion:0.00} vol {loopSource.volume:0.00} pitch {loopSource.pitch:0.00} recoveries {RecoveryBreathCount}");

            if (loopSource != null && breathLoop != null)
            {
                loopSource.clip = breathLoop;
                loopSource.loop = true;
                loopSource.spatialBlend = 0f;   // your own lungs, not a world sound
                loopSource.volume = 0f;
                loopSource.Play();              // always running; volume is the gate
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Unwatch("Breath");
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent evt) => hidden = true;
        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent evt) => hidden = false;

        void Update()
        {
            if (config == null || loopSource == null) return;
            float dt = Time.deltaTime;

            if (maniac == null) maniac = Object.FindAnyObjectByType<ManiacController>();
            bool chased = maniac != null && maniac.Perception != null
                          && maniac.Perception.Level == ManiacPerception.AwarenessLevel.Detected;

            // ONLY being chased winds you. Ordinary running used to count too,
            // and since the player runs almost constantly that made breathing a
            // permanent bed you stopped hearing. Now it is silent until he is
            // actually after you, which is the only time it means anything.
            float rate = chased
                ? 1f / Mathf.Max(0.1f, config.secondsToWinded)
                : -1f / Mathf.Max(0.1f, config.secondsToRecover);
            exertion = Mathf.Clamp01(exertion + rate * dt);

            // He had you and now he does not: the one long breath of relief.
            if (wasChased && !chased) TryRecoveryBreath();
            wasChased = chased;

            Holding = config.holdWhileHiding && hidden && chased;

            voiced = Mathf.Lerp(voiced, exertion, 1f - Mathf.Exp(-config.smoothing * dt));

            float target;
            if (Holding) target = config.heldVolume;
            else if (voiced < config.silenceBelow) target = 0f;
            else target = Mathf.Lerp(0f, config.windedVolume,
                                     Mathf.InverseLerp(config.silenceBelow, 1f, voiced));

            loopSource.pitch = Mathf.Lerp(config.easyPitch, config.windedPitch, voiced);
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, target, dt * 1.5f);
        }

        /// The long exhale when you realise he has lost you. Gated on having
        /// actually been worked — otherwise a two-second scare would end in a
        /// dramatic recovery breath the player did not earn.
        void TryRecoveryBreath()
        {
            if (gaspClip == null || oneShotSource == null) return;
            if (exertion < config.recoveryNeedsExertion) return;
            oneShotSource.pitch = 1f;
            oneShotSource.PlayOneShot(gaspClip, config.recoveryVolume);
            RecoveryBreathCount++;

            // Ask the heart to step back while the exhale plays. Without this the
            // breath is buried: it is ~12dB below the heartbeat even normalised,
            // and the heart is still at chase rate for seconds after he gives up.
            var beat = Object.FindAnyObjectByType<PlayerHeartbeat>();
            if (beat != null)
                beat.Soften(gaspClip.length * config.heartSoftenShare, config.heartSoftenFactor);
            // The long breath IS the recovery — drop the panting under it so the
            // two do not talk over each other.
            exertion = Mathf.Min(exertion, config.exertionAfterRecovery);
        }
    }
}
