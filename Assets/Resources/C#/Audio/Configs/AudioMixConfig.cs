// The ONE place the balance lives.
//
// Before this, every volume in the game sat in a different asset — AudioConfig,
// HeartbeatConfig, BreathingConfig, ManiacVoiceConfig, PlayerVoiceConfig,
// PlayerFootstepConfig, HealthVfxConfig, EffectRecipe. Eight files, no way to
// see the mix as a whole, and no way to make one sound step back for another.
// Measured worst case during a chase: the linear amplitudes summed to **6.72x
// full scale**, and Unity hard-clips above 1.0. Everything was mashing.
//
// This config does NOT replace those volumes. It multiplies on top of them, so
// the tuning already done by ear survives — `chaseVolume` was hand-lowered to
// 0.42 and `stingVolume` to 0.70, and that work is kept. What this adds is
// **relative priority**: which sound wins when two want the same moment.
//
// Asset: C#/Audio/Configs/AudioMixConfig.asset (created by Setup/40).
using System;
using UnityEngine;

namespace TimeKiller.Audio
{
    /// Every distinct voice in the game. Adding one here is how it joins the mix.
    public enum MixChannel
    {
        Music, Ambience, Sting,
        ManiacBreath, ManiacVocal, ManiacFootsteps,
        Heartbeat, PlayerBreath, PlayerVoice, PlayerFootsteps,
        Effects
    }

    [Serializable]
    public class MixChannelSettings
    {
        public MixChannel channel;
        [Tooltip("Trim multiplied on top of whatever volume that system already computes. 1 = leave it alone.")]
        [Range(0f, 1f)] public float level = 1f;
        [Tooltip("Priority. LOWER wins. A channel ducks only for channels with a strictly lower tier than its own.")]
        [Range(0, 5)] public int tier = 3;
        [Tooltip("What this channel is multiplied by while something more important is speaking. 1 = never ducks (use for anything the player steers by).")]
        [Range(0f, 1f)] public float duckDepth = 0.45f;
    }

    [CreateAssetMenu(menuName = "TimeKiller/Configs/Audio Mix", fileName = "AudioMixConfig")]
    public class AudioMixConfig : ScriptableObject
    {
        [Header("Channels — tier 0 wins, tier 5 loses")]
        [Tooltip("Tune these live in Play Mode; ScriptableObject edits persist.")]
        public MixChannelSettings[] channels = DefaultChannels();

        [Header("Duck timing")]
        [Tooltip("Seconds to duck DOWN. Fast, because a duck that arrives late has already been masked by the thing it was making room for.")]
        public float duckAttackSeconds = 0.08f;
        [Tooltip("Seconds to come back UP. Slow, so the mix breathes instead of pumping.")]
        public float duckReleaseSeconds = 0.55f;

        [Header("The low-band rule")]
        [Tooltip("The heartbeat and the maniac's breath both live below ~300Hz and measured 2.75 combined — they cannot both own that band. When his breath is audible, the heart is multiplied by this. Information beats emotion: you need to hear WHERE HE IS to act, and the heart's RATE still tells you how close he is even when its level drops, so nothing is actually lost.")]
        [Range(0f, 1f)] public float heartUnderManiacBreath = 0.4f;
        [Tooltip("Maniac breath loudness at which that duck is at full strength.")]
        [Range(0f, 1f)] public float heartDuckAtBreath = 0.5f;

        [Header("Music EQ — carving room for everything else")]
        [Tooltip("Measured: the music holds 61% of its energy below 60Hz and 81% below 150Hz. Low frequencies mask higher ones far more than the reverse (upward spread of masking), so that sub-bass was smearing the whole game — and laptop speakers cannot reproduce it anyway, so most players never heard it in the first place. Applied live by MusicEq; turn this off to hear the difference instantly.")]
        public bool musicEqEnabled = true;
        [Tooltip("High-pass corner, applied as TWO cascaded biquads (24 dB/oct). 80Hz measured: the chase track's sub-60 energy drops 61% -> 28% and 5.7dB of headroom comes back, while everything from 80Hz up — where audible weight actually lives — is untouched. A single 12 dB/oct stage at 55Hz only reached 51% and was rejected.")]
        [Range(20f, 200f)] public float musicHighPassHz = 80f;
        [Tooltip("Centre of the 'mud zone' dip. 150-300Hz is where every element in this game was colliding and none of them were legible.")]
        [Range(80f, 600f)] public float musicDipHz = 220f;
        [Tooltip("Depth of that dip in dB (negative = a cut). Gentle on purpose — this is making room, not reshaping the music.")]
        [Range(-12f, 0f)] public float musicDipDb = -3f;
        [Tooltip("Width of the dip. Lower Q = wider. Around 0.9 covers 150-300 without touching the 300-800 band the maniac's growl lives in.")]
        [Range(0.2f, 4f)] public float musicDipQ = 0.9f;

        [Header("Master headroom")]
        [Tooltip("Master trim applied to every channel. Below 1 because a dozen sources that each peak under 1.0 still sum well over it.")]
        [Range(0f, 1f)] public float masterLevel = 0.85f;
        [Tooltip("Turn the whole mix system off without deleting anything — every channel returns 1 and the game sounds exactly as it did before.")]
        public bool enabled = true;

        /// Defaults chosen from the measured collision, not from taste:
        /// stings and vocals are the punctuation and must cut through; the
        /// maniac's breath is the one channel that is GAMEPLAY (it is how you
        /// locate him) so it barely ducks; footsteps never duck because muting
        /// the feedback you steer by reads as a bug, not as tension; the score
        /// is continuous and therefore the cheapest thing to take away.
        public static MixChannelSettings[] DefaultChannels() => new[]
        {
            new MixChannelSettings { channel = MixChannel.Sting,           level = 0.90f, tier = 0, duckDepth = 1.00f },
            new MixChannelSettings { channel = MixChannel.PlayerVoice,     level = 1.00f, tier = 1, duckDepth = 0.85f },
            new MixChannelSettings { channel = MixChannel.ManiacVocal,     level = 0.95f, tier = 1, duckDepth = 0.85f },
            new MixChannelSettings { channel = MixChannel.ManiacBreath,    level = 0.90f, tier = 2, duckDepth = 0.75f },
            new MixChannelSettings { channel = MixChannel.Heartbeat,       level = 0.85f, tier = 2, duckDepth = 0.60f },
            new MixChannelSettings { channel = MixChannel.ManiacFootsteps, level = 0.80f, tier = 3, duckDepth = 0.60f },
            // Your own lungs are NOT background. The first pass had this at
            // level 0.75 / duck 0.45, which put the recovery breath — the whole
            // reward for surviving a chase — at 0.638 normally and 0.287 while
            // anything else spoke, a 36-71% cut on a sound the user had already
            // approved. Rejected by ear 2026-07-28. It is trimmed now, barely,
            // and it ducks only slightly.
            new MixChannelSettings { channel = MixChannel.PlayerBreath,    level = 1.00f, tier = 3, duckDepth = 0.90f },
            new MixChannelSettings { channel = MixChannel.Effects,         level = 0.85f, tier = 3, duckDepth = 0.70f },
            new MixChannelSettings { channel = MixChannel.PlayerFootsteps, level = 0.85f, tier = 4, duckDepth = 1.00f },
            new MixChannelSettings { channel = MixChannel.Music,           level = 0.80f, tier = 5, duckDepth = 0.35f },
            new MixChannelSettings { channel = MixChannel.Ambience,        level = 0.70f, tier = 5, duckDepth = 0.40f },
        };
    }
}
