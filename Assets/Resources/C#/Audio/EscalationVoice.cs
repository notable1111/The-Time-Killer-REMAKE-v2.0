// The player HEARS the maniac get worse.
//
// WHY THIS EXISTS. ManiacEscalatedEvent fires every time an objective completes
// and he steps up — faster patrol, harder search. Measured 2026-08-27: nothing
// in the audio systems subscribed to it, and nothing subscribed to
// WorldProgressEvent either. So the difficulty rose and the only feedback was
// that you eventually died more often.
//
// That is the difference between a feature being INSTALLED and a feature being
// PERCEIVABLE, and it is the same trap that cost a play session earlier the same
// day: the threat effects were wired, counted on the F1 overlay, and reported
// firing correctly, while the answer to "can you hear it" was no. A difficulty
// change the player cannot perceive does not read as escalation. It reads as the
// game being unfair.
//
// WHY HIS OWN VOICE, HEARD FROM SOMEWHERE ELSE. The cue is built from his attack
// roar, pitched down, low-passed and given a stone-corridor tail. Distance eats
// the high end first, so a far sound is a dull sound: measured, the cue puts
// 67.6% of its energy below 460 Hz and 0.0% above 2 kHz, against the close roar's
// 60.1% and 0.1%. It reads as HIM, further away, rather than as a UI sting —
// which matters, because the thing that changed is him.
//
// PLAYED FLAT (2D) ON PURPOSE. He is not in the room when this fires; he is
// somewhere in the castle and you do not get to know where. A positional cue
// would hand the player a free bearing at exactly the moment the game is
// supposed to get harder.
//
// It announces on ManiacVocal rather than Sting: this IS his voice, so it takes
// his channel and his tier, and the music steps back for it the same way it does
// for a growl. Sting is tier 0 and reserved for the moment of being seen.
//
// SELF-INSTALLING, no scene edit — the scenes are hand-tuned, protected, and
// shared by three sessions. REMOVABLE: delete the config asset.
using TimeKiller.Core;
using TimeKiller.Maniac;
using UnityEngine;

namespace TimeKiller.Audio
{
    public class EscalationVoice : MonoBehaviour
    {
        static EscalationVoice instance;

        EscalationVoiceConfig config;
        AudioSource source;
        float nextAllowed;

        /// For the F1 overlay, and so "did it fire?" is a number rather than a
        /// memory of how the run felt.
        public int Heard { get; private set; }
        public int Played { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            var config = Resources.Load<EscalationVoiceConfig>(EscalationVoiceConfig.ResourcesPath);
            if (config == null || !config.enabled) return;

            var go = new GameObject("EscalationVoice");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<EscalationVoice>();
            instance.config = config;

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // somewhere in the castle, not in the room
            instance.source = source;
        }

        void OnEnable()
        {
            EventBus.Subscribe<ManiacEscalatedEvent>(OnEscalated);
            DebugOverlay.Watch("Escalation", () =>
                $"{Played}/{Heard}" +
                (config == null || config.cues == null || config.cues.Length == 0 ? "  (NO CUES ASSIGNED)" : ""));
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ManiacEscalatedEvent>(OnEscalated);
            DebugOverlay.Unwatch("Escalation");
        }

        void OnEscalated(ManiacEscalatedEvent evt)
        {
            Heard++;
            if (config.cues == null || config.cues.Length == 0) return;
            if (Time.time < nextAllowed) return;
            nextAllowed = Time.time + config.cooldownSeconds;

            var clip = config.cues[Random.Range(0, config.cues.Length)];
            if (clip == null || source == null) return;

            // Step is 1-based, so the first escalation gets no drop and each one
            // after goes lower. Clamped because a long run of objectives would
            // otherwise walk the pitch down into a rumble.
            float stepDrop = Mathf.Pow(config.pitchPerStep, Mathf.Max(0, evt.Step - 1));
            source.pitch = Mathf.Clamp(
                stepDrop + Random.Range(-config.pitchJitter, config.pitchJitter), 0.5f, 1.5f);

            AudioMix.Announce(MixChannel.ManiacVocal, clip.length);
            source.PlayOneShot(clip, config.volume * AudioMix.GainFor(MixChannel.ManiacVocal));
            Played++;
        }
    }
}
