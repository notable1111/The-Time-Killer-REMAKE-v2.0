// Tunables for the layered tension-radar music system (interview 2026-07-23):
// the music IS the threat detector. Layer priority: Safe > Chase > Tense > Calm.
// Asset: C#/Audio/Configs/AudioConfig.asset (clips wired by Setup/24).
using UnityEngine;

namespace TimeKiller.Audio
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Audio", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [Header("Music layers (one random track per layer entry)")]
        public AudioClip[] calmTracks;    // maniac patrolling far away
        public AudioClip[] tenseTracks;   // he's investigating a noise
        public AudioClip[] chaseTracks;   // he sees you — run
        public AudioClip[] safeTracks;    // inside a safe zone (servant passage)

        [Header("Layer volumes")]
        [Range(0f, 1f)] public float calmVolume = 0.35f;
        [Range(0f, 1f)] public float tenseVolume = 0.5f;
        [Range(0f, 1f)] public float chaseVolume = 0.75f;
        [Range(0f, 1f)] public float safeVolume = 0.28f; // quieter than calm — sanctuary whispers (user tuning 2026-07-23)

        [Header("Stings")]
        public AudioClip[] spottedStings;  // jumpscare the instant he sees you
        public AudioClip heardRiser;       // dark riser when your noise reaches him
        public AudioClip deathSting;
        [Range(0f, 1f)] public float stingVolume = 0.85f;
        [Tooltip("Seconds between spotted stings (re-acquiring sight mid-chase shouldn't restack them).")]
        public float spottedStingCooldown = 4f;
        [Tooltip("Seconds between heard-risers — and it only plays from the Calm layer.")]
        public float riserCooldown = 8f;

        [Header("Transitions")]
        [Tooltip("Music crossfade time between layers.")]
        public float crossfadeSeconds = 1.6f;

        [Header("Safe zones (script-world rects — the servant passage)")]
        [Tooltip("Player inside any rect (while not actively chased) hears the safe layer.")]
        public Rect[] safeZones =
        {
            new Rect(8f, -3f, 2f, 3f),    // hidden door shaft
            new Rect(8f, -8f, 2f, 5f),    // vertical passage
            new Rect(10f, -8f, 16f, 2f),  // east run under the map
        };
    }
}
