// The bus this project never had.
//
// `AudioDucking` was the first attempt: one shared dial, written by the heart,
// read by the score. It works, and it stays — but it only ever governed ONE
// relationship. Measured across the whole game during a chase, nine sources
// wanted the same instant and their linear amplitudes summed to **6.72x full
// scale**, with three of them (heartbeat 1.00, maniac breath 0.85, growl 0.90)
// stacked below ~300Hz where they mask each other into mud.
//
// So this adds the missing idea: **priority**. Every voice declares a tier, and
// a voice ducks only for tiers strictly more important than its own. Stings and
// vocals are punctuation and cut through; the maniac's breath barely moves
// because it is not decoration, it is how you find him; your own footsteps never
// duck at all, because muting the feedback you steer by reads as a bug.
//
// Returns a MULTIPLIER, never an absolute level. Every system keeps computing
// its own volume exactly as before and multiplies by this, so all the balancing
// already done by ear survives, and a missing config means every call returns
// 1.0 and the game sounds precisely as it did.
using System.Collections.Generic;
using UnityEngine;

namespace TimeKiller.Audio
{
    public static class AudioMix
    {
        static AudioMixConfig config;
        static readonly Dictionary<MixChannel, MixChannelSettings> settings = new();
        static float[] tierBusyUntil = new float[8];
        static float[] smoothed;
        static float maniacBreath;      // 0..1, reported by ManiacVoice
        static int lastFrame = -1;

        /// Wired by Setup/40. Until then every channel returns 1.0.
        public static void Use(AudioMixConfig mixConfig)
        {
            config = mixConfig;
            settings.Clear();
            if (config?.channels == null) return;
            foreach (var c in config.channels) settings[c.channel] = c;
        }

        public static bool Active => config != null && config.enabled;

        /// Announce a one-shot that just started, so quieter tiers get out of its
        /// way for as long as it actually lasts. Duration comes from the clip, not
        /// from a guess — a fixed duck window is either too short for a death cry
        /// or too long for a footstep.
        public static void Announce(MixChannel channel, float seconds)
        {
            if (!Active || !settings.TryGetValue(channel, out var s)) return;
            int tier = Mathf.Clamp(s.tier, 0, tierBusyUntil.Length - 1);
            float until = Time.time + Mathf.Max(0.05f, seconds);
            if (until > tierBusyUntil[tier]) tierBusyUntil[tier] = until;
        }

        /// ManiacVoice reports how loud his breath currently is so the heartbeat
        /// can get out of the low band. Pushed rather than pulled: the mix must
        /// not reach into the Maniac feature to ask.
        public static void ReportManiacBreath(float loudness) => maniacBreath = Mathf.Clamp01(loudness);

        /// The multiplier for this channel right now.
        public static float GainFor(MixChannel channel)
        {
            if (!Active || !settings.TryGetValue(channel, out var s)) return 1f;
            Tick();

            float gain = s.level * smoothed[(int)channel] * config.masterLevel;

            // The low-band rule. Both of these are sub-300Hz and measured 2.75
            // together; whoever is carrying information wins. His breath tells you
            // where he is, which you can act on. The heart's LEVEL is redundant
            // while he is that close — its RATE already says the same thing and
            // keeps saying it at any volume.
            if (channel == MixChannel.Heartbeat && maniacBreath > 0f)
            {
                float t = Mathf.Clamp01(maniacBreath / Mathf.Max(0.01f, config.heartDuckAtBreath));
                gain *= Mathf.Lerp(1f, config.heartUnderManiacBreath, t);
            }
            return gain;
        }

        /// Advances every channel's duck once per frame. Frame-guarded because
        /// GainFor is called from several systems and integrating the smoothing
        /// more than once per frame would make the release rate depend on how
        /// many things happened to be listening.
        static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            EnsureBuffers();

            float dt = Time.unscaledDeltaTime;
            foreach (var pair in settings)
            {
                var s = pair.Value;
                int index = (int)pair.Key;

                // Ducked only by something strictly MORE important. Equal tiers
                // never duck each other — two things of the same rank fighting
                // would flip-flop the mix on whichever arrived last.
                bool outranked = false;
                for (int tier = 0; tier < s.tier && tier < tierBusyUntil.Length; tier++)
                    if (Time.time < tierBusyUntil[tier]) { outranked = true; break; }

                float target = outranked ? s.duckDepth : 1f;
                float seconds = target < smoothed[index] ? config.duckAttackSeconds : config.duckReleaseSeconds;
                smoothed[index] = Mathf.MoveTowards(smoothed[index], target, dt / Mathf.Max(0.01f, seconds));
            }
        }

        static void EnsureBuffers()
        {
            int count = System.Enum.GetValues(typeof(MixChannel)).Length;
            if (smoothed != null && smoothed.Length == count) return;
            smoothed = new float[count];
            for (int i = 0; i < count; i++) smoothed[i] = 1f;
        }

        /// Path inside the Resources root (the whole `Assets/Resources/` tree is
        /// one), so the mix loads itself in EVERY scene — Castle wing, Catacombs,
        /// and any future one — without a scene object to forget to add.
        /// Deleting the asset is the uninstall: every call then returns 1.0.
        const string ConfigResourcePath = "C#/Audio/Configs/AudioMixConfig";

        /// Statics survive a domain reload when "Reload Domain" is off, so a run
        /// that ended mid-duck would otherwise start the next one muffled — the
        /// same trap AudioDucking documents.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            tierBusyUntil = new float[8];
            smoothed = null;
            maniacBreath = 0f;
            lastFrame = -1;
            Use(Resources.Load<AudioMixConfig>(ConfigResourcePath));
        }

        /// Read-only view for the debug overlay and tests.
        public static string Describe(MixChannel channel) =>
            !Active ? "off" : $"{GainFor(channel):0.00}";
    }
}
