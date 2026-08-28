// What a noise in the world SOUNDS like to the player who made it.
//
// Asset: C#/Audio/Configs/WorldNoiseAudioConfig.asset, loaded from Resources by
// WorldNoiseAudio. Delete the asset and the feature never installs.
using UnityEngine;

namespace TimeKiller.Audio
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/World Noise Audio", fileName = "WorldNoiseAudioConfig")]
    public class WorldNoiseAudioConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Audio/Configs/WorldNoiseAudioConfig";

        public bool enabled = true;

        [Tooltip("One is chosen at random. Empty = world noises stay silent, which is the state this shipped in for months.")]
        public AudioClip[] clips;

        [Tooltip("Base level for a noise at full carry. RAISED from 0.45 to 0.85 on 2026-08-28 after a real session measured the pulse firing on schedule but landing 15-20 dB under the player own footsteps; the cause was misusing the event Loudness field, see loudnessFloor. THE LEVEL IS A REFERENCE DECISION, not a taste one: Dead by Daylight, whose loop this game copies, makes a generator plainly audible to the survivor working it, because it is the constant reminder that you are broadcasting - and it does NOT deafen them, the terror radius still cuts through. Prominent, never masking.")]
        [Range(0f, 1f)] public float volume = 0.85f;

        [Tooltip("What a MINIMUM-carry noise is scaled to, as a fraction of volume. WorldNoiseEvent.Loudness is documented on the struct as scaling the listener HEARING RADIUS, and ManiacPerception uses it exactly that way, as a range multiplier in Reaches(). It is a carry field, not a player-volume field. Multiplying playback by it raw was a mistake: ClockRepair publishes 0.3, so a clock the player was STANDING AT played at 30% while the number only ever meant that it does not travel far. A short-carry noise right next to you is still loud. 1.0 ignores carry entirely; 0.0 restores the old raw multiply.")]
        [Range(0f, 1f)] public float loudnessFloor = 0.6f;

        [Tooltip("Random pitch spread, so a repeating repair noise never sounds like a loop.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.07f;

        [Tooltip("Shortest gap between two played noises, whatever the publishers do. ClockRepair already rate-limits itself with repairNoiseInterval; this is the backstop for anything that does not.")]
        [Range(0f, 2f)] public float minGapSeconds = 0.12f;

        [Header("What to skip")]
        [Tooltip("Play a sound for map-wide noises too (WorldNoiseEvent.AlwaysHeard).\n\nOFF by default and that is deliberate: the only AlwaysHeard publisher today is ExitDoor, which ALREADY plays its own creak on the same line it publishes the event. Turning this on would double that sound. Turn it on only after checking that whatever publishes AlwaysHeard does not voice itself.")]
        public bool playAlwaysHeard = false;

        [Tooltip("Ignore events quieter than this. A noise too faint for the maniac to care about does not need a voice either.")]
        [Range(0f, 1f)] public float minLoudness = 0.05f;
    }
}
