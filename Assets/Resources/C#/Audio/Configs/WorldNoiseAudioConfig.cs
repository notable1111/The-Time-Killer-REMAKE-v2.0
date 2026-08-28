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

        [Tooltip("Base level, multiplied by the event's own Loudness.\n\nMEASURED at 0.45: the clock lands about 3 dB above his footsteps in the two bands they share, when he is close enough for reach to be 1. That is coexistence rather than masking — he still has his breath and his voice on top. Push this toward 1 and repairing becomes genuinely deafening, which is a legitimate horror choice (you commit, and you go blind in the ears) but it is a DESIGN decision, not a mix fix. Ask before making it.")]
        [Range(0f, 1f)] public float volume = 0.45f;

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
