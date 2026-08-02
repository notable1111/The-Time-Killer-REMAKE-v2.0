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
//   STAYING LOST earns one long recovery breath, ~8.5s AFTER the chase ends
//     (user ruling 2026-07-28). It used to fire the instant `Detected` dropped,
//     which was wrong twice over: relief is not instant, and `Detected` drops
//     every time line of sight breaks behind a pillar — so the "I got away"
//     breath was going off mid-chase, repeatedly, while he was still hunting.
//     Now the chase end only ARMS it; he must stay off you for the whole delay
//     or the wait resets. Panting is held up through that window on purpose, so
//     the exhale lands on top of audible breathing instead of out of silence.
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

        [Tooltip("Optional. Lets FEAR raise the breathing floor independently of exertion, so standing frozen while he searches nearby still breathes. Empty = exertion only, exactly as before.")]
        [SerializeField] TimeKiller.Fear.FearConfig fearConfig;

        float exertion;      // 0..1, how winded you are
        float voiced;        // smoothed value actually driving the audio
        float fear;          // from the conductor, if one is running
        bool haveFear;
        float lastFearAt = float.NegativeInfinity;

        bool FearDriven => fearConfig != null && haveFear && Time.time - lastFearAt < 0.5f;

        /// Fear's contribution to breathing, 0..1. Separate from exertion on
        /// purpose: the two are different facts about the body (how hard you have
        /// worked vs how frightened you are) and they must be able to disagree.
        /// Combined by MAX rather than sum, so a terrified player who has also
        /// been sprinting does not breathe at 200%.
        float FearBreath => !FearDriven ? 0f
            : Mathf.InverseLerp(fearConfig.breathStartsAt, 1f, fear) * fearConfig.breathMaxVolume;
        bool hidden;
        bool wasChased;
        bool recoveryPending;                              // chase over, relief breath owed but not yet due
        float recoveryArmedAt = float.NegativeInfinity;    // when the chase ended

        /// 0..1 how out of breath the player is. Public for debug and tuning.
        public float Exertion => exertion;
        public bool Holding { get; private set; }
        /// How many recovery breaths have played this run. Exertion alone cannot
        /// evidence this — it decays to zero on its own, so a low reading is
        /// equally consistent with "the breath fired" and "nothing happened".
        public int RecoveryBreathCount { get; private set; }
        /// Seconds until the relief breath is due, or -1 when none is owed.
        /// Exposed because a timer nobody can see is a timer nobody can test —
        /// this is what the overlay and the bot read to prove the delay ran.
        public float RecoveryDueIn => recoveryPending && config != null
            ? Mathf.Max(0f, config.recoveryDelaySeconds - (Time.time - recoveryArmedAt))
            : -1f;

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
            EventBus.Subscribe<TimeKiller.Fear.FearChangedEvent>(OnFearChanged);
            if (fearConfig == null)
                fearConfig = Resources.Load<TimeKiller.Fear.FearConfig>("C#/Fear/Configs/FearConfig");
            DebugOverlay.Watch("Breath", () =>
            {
                if (config == null) return "NO CONFIG";
                string relief = recoveryPending ? $" relief in {RecoveryDueIn:0.0}s" : "";
                string tail = $" recoveries {RecoveryBreathCount}{relief}";
                if (Holding) return $"HELD (winded {exertion:0.00}){tail}";
                if (voiced < config.silenceBelow) return $"calm (winded {exertion:0.00}){tail}";
                return $"winded {exertion:0.00} vol {loopSource.volume:0.00} pitch {loopSource.pitch:0.00}{tail}";
            });

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
            EventBus.Unsubscribe<TimeKiller.Fear.FearChangedEvent>(OnFearChanged);
            DebugOverlay.Unwatch("Breath");
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent evt) => hidden = true;
        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent evt) => hidden = false;

        void OnFearChanged(TimeKiller.Fear.FearChangedEvent e)
        {
            fear = e.Fear;
            haveFear = true;
            lastFearAt = Time.time;
        }

        void Update()
        {
            if (config == null || loopSource == null) return;
            float dt = Time.deltaTime;

            if (maniac == null) maniac = Object.FindAnyObjectByType<ManiacController>();
            bool chased = maniac != null && maniac.Perception != null
                          && maniac.Perception.Level == ManiacPerception.AwarenessLevel.Detected;

            // The chase ending only ARMS the relief breath — it does not play it.
            // Re-acquiring you takes it away again: that chase never ended, so no
            // relief is owed. Ordered ahead of the exertion update below because
            // the pending flag is what slows the decay.
            if (wasChased && !chased) ArmRecoveryBreath();
            else if (chased && recoveryPending) recoveryPending = false;
            wasChased = chased;

            // ONLY being chased winds you. Ordinary running used to count too,
            // and since the player runs almost constantly that made breathing a
            // permanent bed you stopped hearing. Now it is silent until he is
            // actually after you, which is the only time it means anything.
            float rate;
            if (chased) rate = 1f / Mathf.Max(0.1f, config.secondsToWinded);
            else
            {
                rate = -1f / Mathf.Max(0.1f, config.secondsToRecover);
                // Adrenaline does not stop the moment he turns away. At the full
                // rate you would be under silenceBelow by the time the exhale is
                // due, and it would land out of silence instead of out of panting.
                if (recoveryPending) rate *= config.settleDecayScale;
            }
            exertion = Mathf.Clamp01(exertion + rate * dt);

            // He has stayed off you long enough. Now you let it out.
            if (recoveryPending && Time.time - recoveryArmedAt >= config.recoveryDelaySeconds)
                FireRecoveryBreath();

            Holding = config.holdWhileHiding && hidden && chased;

            voiced = Mathf.Lerp(voiced, exertion, 1f - Mathf.Exp(-config.smoothing * dt));

            float target;
            if (Holding) target = config.heldVolume;
            else if (voiced < config.silenceBelow) target = 0f;
            else target = Mathf.Lerp(0f, config.windedVolume,
                                     Mathf.InverseLerp(config.silenceBelow, 1f, voiced));

            // Fear breathes too — being frozen in a corridor while he searches
            // three metres away is terrifying and involves no exertion at all.
            // MAX, never sum: one set of lungs, so the louder reason wins rather
            // than the two stacking into a hyperventilating cartoon. Holding your
            // breath in a wardrobe still overrides both.
            if (!Holding) target = Mathf.Max(target, FearBreath);

            loopSource.pitch = Mathf.Lerp(config.easyPitch, config.windedPitch, voiced);
            loopSource.volume = Mathf.MoveTowards(loopSource.volume,
                target * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.PlayerBreath),
                dt * 1.5f);
        }

        /// The chase just ended — start counting, do not breathe yet. Gated on
        /// having actually been worked, otherwise a two-second scare would end in
        /// a dramatic recovery breath the player did not earn. That test lives
        /// HERE rather than at fire time on purpose: exertion keeps decaying
        /// through the wait, so testing it late would let the waiting itself
        /// cancel a breath that was earned when the chase ended.
        void ArmRecoveryBreath()
        {
            if (gaspClip == null || oneShotSource == null) return;
            if (exertion < config.recoveryNeedsExertion) return;
            recoveryPending = true;
            recoveryArmedAt = Time.time;
        }

        /// The long exhale, once he has genuinely stayed lost. This is the whole
        /// reward for surviving a chase, and it only means anything because it
        /// waited: you spend the delay still panting, not yet sure he is gone.
        void FireRecoveryBreath()
        {
            recoveryPending = false;
            if (gaspClip == null || oneShotSource == null) return;
            oneShotSource.pitch = 1f;
            // This is the whole reward for surviving a chase, so it gets the room:
            // announcing it steps the score and the ambience back underneath it.
            // The Soften() call below was doing this by hand for the heartbeat
            // alone, before there was a bus that could do it for everything.
            TimeKiller.Audio.AudioMix.Announce(TimeKiller.Audio.MixChannel.PlayerBreath, gaspClip.length);
            oneShotSource.PlayOneShot(gaspClip, config.recoveryVolume
                * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.PlayerBreath));
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
