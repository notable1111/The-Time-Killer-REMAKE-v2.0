// Tunables for the layered tension-radar music system (interview 2026-07-23,
// re-sorted by the user's ear 2026-07-23 with the new Horror Sounds pack).
// The music IS the threat detector. Layer priority (high→low):
//   Chase > Safe > Investigate > Mystery > Dread.
// "Dread" is the far-patrol exploration bed (the old "Calm"); "Investigate"
// is the old "Tense". "Mystery/Approach" is NEW — the "about to enter / act"
// dread the user described, driven by hand-placed mysteryZones.
// Asset: C#/Audio/Configs/AudioConfig.asset (clips wired by Setup/24).
using UnityEngine;
using UnityEngine.Serialization;

namespace TimeKiller.Audio
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Audio", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [Header("Music layers (one random track per layer entry)")]
        [Tooltip("Dread — maniac patrolling far. The exploration bed.")]
        [FormerlySerializedAs("calmTracks")] public AudioClip[] dreadTracks;
        [Tooltip("Mystery / Approach — about to enter a room or act (hand-placed mysteryZones).")]
        public AudioClip[] mysteryTracks;
        [Tooltip("Investigate — he heard a noise and is searching.")]
        [FormerlySerializedAs("tenseTracks")] public AudioClip[] investigateTracks;
        [Tooltip("Chase — he sees you. Run.")]
        public AudioClip[] chaseTracks;
        [Tooltip("Safe — inside a sanctuary (servant passage).")]
        public AudioClip[] safeTracks;
        [Tooltip("Menu — future title screen (not driven by the director yet).")]
        public AudioClip[] menuTracks;

        [Header("Layer volumes")]
        [FormerlySerializedAs("calmVolume")] [Range(0f, 1f)] public float dreadVolume = 0.35f;
        [Range(0f, 1f)] public float mysteryVolume = 0.42f;
        [FormerlySerializedAs("tenseVolume")] [Range(0f, 1f)] public float investigateVolume = 0.5f;
        [Range(0f, 1f)] public float chaseVolume = 0.75f;
        [Range(0f, 1f)] public float safeVolume = 0.28f; // quieter than dread — sanctuary whispers (user tuning 2026-07-23)

        [Header("Stings")]
        public AudioClip[] spottedStings;  // jumpscare the instant he sees you
        public AudioClip heardRiser;       // dark riser when your noise reaches him
        public AudioClip deathSting;
        [Range(0f, 1f)] public float stingVolume = 0.85f;
        [Tooltip("Seconds between spotted stings (re-acquiring sight mid-chase shouldn't restack them).")]
        public float spottedStingCooldown = 4f;
        [Tooltip("Seconds between heard-risers — only from an unaware layer (Dread/Mystery).")]
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

        [Header("Mystery zones (NEW — the 'about to enter / act' approach dread)")]
        [Tooltip("Player inside any rect (while not chased/investigated/in-safe) hears the Mystery layer. Hand-place these on thresholds, doorways, key rooms. Empty = Mystery never triggers.")]
        public Rect[] mysteryZones =
        {
            new Rect(0f, 0f, 3f, 3f),     // DEMO zone near spawn — MOVE/replace me in the editor
        };
    }
}
