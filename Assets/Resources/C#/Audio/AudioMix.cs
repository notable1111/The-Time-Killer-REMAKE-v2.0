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
        // Standing claims: "something at this tier is HERE", as opposed to
        // tierBusyUntil's "something at this tier is speaking until T".
        static readonly bool[] tierPresent = new bool[8];
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

        /// The live config, for the few systems that need more than a gain —
        /// MusicEq reads its filter settings from here so every audio number in
        /// the game still lives in exactly one asset.
        public static AudioMixConfig Config => config;

        /// Announce a one-shot that just started, so quieter tiers get out of its
        /// way for as long as it actually lasts. Duration comes from the clip, not
        /// from a guess — a fixed duck window is either too short for a death cry
        /// or too long for a footstep.
        public static void Announce(MixChannel channel, float seconds)
        {
            if (!Active || !settings.TryGetValue(channel, out var s)) return;
            int tier = Mathf.Clamp(s.tier, 0, tierBusyUntil.Length - 1);
            // UNSCALED, so a duck that was running when the player hit pause
            // expires normally instead of being frozen for the whole menu — they
            // would otherwise set the music slider against a ducked level.
            float until = Time.unscaledTime + Mathf.Max(0.05f, seconds);
            if (until > tierBusyUntil[tier]) tierBusyUntil[tier] = until;
        }

        /// A claim that LASTS, for something that is present rather than speaking.
        ///
        /// WHY THIS IS NOT Announce. Announce marks a tier busy for a duration,
        /// which is right for a growl or a sting — a thing with a beginning and an
        /// end. It is wrong for the maniac's footsteps, and the measurement is why:
        /// cadence is distance-driven at strideMeters 0.78, so at patrol speed he
        /// steps every 0.433s and at chase speed every 0.15s, against ~0.43s clips.
        /// Announcing per step would hold tier 3 busy CONTINUOUSLY, and with a
        /// 0.55s release the music could never climb back between steps. The result
        /// is not a duck, it is a permanent -9 dB on music for as long as he is
        /// within earshot, on top of a chase layer already tuned by ear.
        ///
        /// So presence is a separate, weaker kind of claim: it says "something more
        /// important is HERE", and the mix leans back by presenceDepthScale of the
        /// full duck rather than all of it. Set it false and the mix returns.
        ///
        /// Idempotent, so a caller can set it every frame from a distance test
        /// without thinking about edges.
        public static void SetPresence(MixChannel channel, bool present)
        {
            if (!Active || !settings.TryGetValue(channel, out var s)) return;
            int tier = Mathf.Clamp(s.tier, 0, tierPresent.Length - 1);
            tierPresent[tier] = present;
        }

        /// Drop every standing presence claim. Called when the thing holding one
        /// goes away — a scene reload destroys the maniac, and a claim with no
        /// claimant would duck the music forever.
        public static void ClearPresence()
        {
            for (int i = 0; i < tierPresent.Length; i++) tierPresent[i] = false;
        }

        // --- Player-facing volume, the settings menu's half of the mix ---
        //
        // Deliberately NOT stored in AudioMixConfig. Writing a player's slider
        // into the ScriptableObject would dirty the asset, overwrite the tuning
        // done by ear, and get committed to git the next time anyone pushed.
        // These are a separate runtime layer multiplied on top, persisted in
        // PlayerPrefs, and the config stays exactly as authored.
        const string PrefMaster = "tk.vol.master";
        const string PrefMusic  = "tk.vol.music";
        const string PrefSfx    = "tk.vol.sfx";

        static float userMaster = 1f, userMusic = 1f, userSfx = 1f;

        public static float UserMaster { get => userMaster; set => Set(ref userMaster, value, PrefMaster); }
        public static float UserMusic  { get => userMusic;  set => Set(ref userMusic,  value, PrefMusic);  }
        public static float UserSfx    { get => userSfx;    set => Set(ref userSfx,    value, PrefSfx);    }

        static void Set(ref float field, float value, string key)
        {
            field = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(key, field);
        }

        /// Call once the player is done dragging — writing to disk on every
        /// slider frame would hit the registry hundreds of times a second.
        public static void SaveUserVolumes() => PlayerPrefs.Save();

        static void LoadUserVolumes()
        {
            userMaster = PlayerPrefs.GetFloat(PrefMaster, 1f);
            userMusic  = PlayerPrefs.GetFloat(PrefMusic,  1f);
            userSfx    = PlayerPrefs.GetFloat(PrefSfx,    1f);
        }

        /// Music and ambience follow the music slider; everything else is SFX.
        /// There is no separate voice slider because nobody in this game speaks —
        /// a "Voice" control with no dialogue behind it only confuses players.
        static float UserGainFor(MixChannel channel) =>
            (channel == MixChannel.Music || channel == MixChannel.Ambience ? userMusic : userSfx) * userMaster;

        /// ManiacVoice reports how loud his breath currently is so the heartbeat
        /// can get out of the low band. Pushed rather than pulled: the mix must
        /// not reach into the Maniac feature to ask.
        public static void ReportManiacBreath(float loudness) => maniacBreath = Mathf.Clamp01(loudness);

        /// The multiplier for this channel right now.
        public static float GainFor(MixChannel channel)
        {
            // The player's sliders apply even when the mix system itself is off,
            // otherwise turning the mix off would silently ignore their settings.
            if (!Active || !settings.TryGetValue(channel, out var s)) return UserGainFor(channel);
            Tick();

            float gain = s.level * smoothed[(int)channel] * config.masterLevel * UserGainFor(channel);

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
                // Two kinds of claim, and a spoken one always wins over a merely
                // present one — so a growl still gets the full duck even while
                // the maniac is standing next to you holding a presence claim.
                bool spokenOver = false, presentOver = false;
                for (int tier = 0; tier < s.tier && tier < tierBusyUntil.Length; tier++)
                {
                    if (Time.unscaledTime < tierBusyUntil[tier]) { spokenOver = true; break; }
                    if (tierPresent[tier]) presentOver = true;
                }

                float depth = spokenOver
                    ? s.duckDepth
                    : Mathf.Lerp(1f, s.duckDepth, config.presenceDepthScale);
                float target = (spokenOver || presentOver) ? depth : 1f;
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
            ClearPresence();   // a claim whose claimant died with the domain
            smoothed = null;
            maniacBreath = 0f;
            lastFrame = -1;
            LoadUserVolumes();
            Use(Resources.Load<AudioMixConfig>(ConfigResourcePath));
        }

        /// Read-only view for the debug overlay and tests.
        public static string Describe(MixChannel channel) =>
            !Active ? "off" : $"{GainFor(channel):0.00}";
    }
}
