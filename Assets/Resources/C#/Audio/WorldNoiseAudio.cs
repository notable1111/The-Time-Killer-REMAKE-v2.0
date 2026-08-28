// You can hear the noise you are making.
//
// WHY THIS EXISTS. WorldNoiseEvent is how the game says "a sound happened HERE,
// this loud" — the maniac's perception subscribes to it, applies distance and
// wall muffling, and comes to investigate. Measured 2026-08-27: ManiacPerception
// was the ONLY listener. The maniac could hear it and the player could not.
//
// That costs a mechanic rather than a mood. ClockRepair publishes one on a timer
// the whole time you are working a clock — its own comment says "the clock is
// loud while you work it, hit or miss" — so repairing broadcasts your position
// on a repeating pulse. The player had no way to learn that rule: he simply
// started walking towards them for no perceivable reason. A rule the player
// cannot perceive is not a mechanic, it is bad luck.
//
// POSITIONAL, so it is the CLOCK you hear winding rather than a noise in your
// head. ClockRepair deliberately publishes from the clock's transform and not
// the player's, for exactly this reason, and playing it flat would throw that
// away.
//
// WHY IT SKIPS AlwaysHeard BY DEFAULT. The only publisher of a map-wide noise
// today is ExitDoor, and it plays its own creak on the line above the one that
// publishes the event. A generic listener would double it. That is a config flag
// rather than a hardcoded rule, because the reason is "the current publisher
// already voices itself", not "map-wide noises should be silent".
//
// WHERE IT SITS IN THE MIX. The clip is a heavy mechanical thunk, deliberately
// NOT a bright click: the two sources that were closest to hand measured 67% and
// 85% of their energy above 2 kHz, which is the one band the music leaves empty
// and therefore the band his footsteps live in. Masking his approach while you
// are locked in a repair — the moment you are least able to react — is the worst
// possible trade. Pitched down and low-passed, the shipped clip sits at 30.1% in
// 460-2k and 27.0% above 2 kHz, and the config volume keeps it near his steps
// rather than over them.
//
// SELF-INSTALLING, no scene edit. REMOVABLE: delete the config asset.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Audio
{
    public class WorldNoiseAudio : MonoBehaviour
    {
        static WorldNoiseAudio instance;

        WorldNoiseAudioConfig config;
        AudioSource source;
        float nextAllowed;

        public int Heard { get; private set; }
        public int Played { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            var config = Resources.Load<WorldNoiseAudioConfig>(WorldNoiseAudioConfig.ResourcesPath);
            if (config == null || !config.enabled) return;

            var go = new GameObject("WorldNoiseAudio");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<WorldNoiseAudio>();
            instance.config = config;

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;                  // it is a thing in the room
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 18f;
            instance.source = source;
        }

        void OnEnable()
        {
            EventBus.Subscribe<WorldNoiseEvent>(OnNoise);
            DebugOverlay.Watch("WorldNoise", () =>
                $"{Played}/{Heard}" +
                (config == null || config.clips == null || config.clips.Length == 0 ? "  (NO CLIPS ASSIGNED)" : ""));
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<WorldNoiseEvent>(OnNoise);
            DebugOverlay.Unwatch("WorldNoise");
        }

        void OnNoise(WorldNoiseEvent evt)
        {
            Heard++;
            if (config.clips == null || config.clips.Length == 0) return;
            if (evt.Loudness < config.minLoudness) return;
            if (evt.AlwaysHeard && !config.playAlwaysHeard) return;
            if (Time.time < nextAllowed) return;
            nextAllowed = Time.time + config.minGapSeconds;

            var clip = config.clips[Random.Range(0, config.clips.Length)];
            if (clip == null || source == null) return;

            // One source, moved to the noise. These are short and rate-limited, so
            // they do not overlap in practice — and a moved source beats
            // PlayClipAtPoint, which allocates a GameObject per call and would do
            // it on a repeating timer for the whole length of a repair.
            source.transform.position = evt.Position;
            source.pitch = 1f + Random.Range(-config.pitchJitter, config.pitchJitter);
            // Loudness scales HOW FAR the noise carries, not how loud it is to
            // someone standing in it — the struct says so and ManiacPerception
            // uses it as a range multiplier. So it only leans the level, floored,
            // instead of scaling it raw.
            float carry = Mathf.Lerp(config.loudnessFloor, 1f, Mathf.Clamp01(evt.Loudness));
            source.PlayOneShot(clip, config.volume * carry * AudioMix.GainFor(MixChannel.Effects));
            Played++;
        }
    }
}
