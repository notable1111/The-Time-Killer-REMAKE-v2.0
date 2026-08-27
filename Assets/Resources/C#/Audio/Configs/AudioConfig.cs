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
        [Tooltip("Endgame — every clock is fixed and the gate is open. Outranks all but Chase.")]
        public AudioClip[] endgameTracks;
        [Tooltip("Menu — future title screen (not driven by the director yet).")]
        public AudioClip[] menuTracks;

        [Header("Per-track level match")]
        [Tooltip("Trim in dB applied to one track on top of its layer volume. A track not listed here plays at 0 dB, so an empty array is exactly the old behaviour. Written by Setup/54 from measurement; safe to nudge by ear afterwards.")]
        public TrackTrim[] trackTrims;

        /// Linear gain for one track, 1.0 when it has no entry.
        ///
        /// WHY THIS EXISTS. A layer plays ONE RANDOM track from its pool, and the
        /// pools are not internally level-matched: measured 2026-08-27, the
        /// investigate pool spans 5.3 dB and dread 3.7 dB. So entering the same
        /// threat state could sound distinctly louder or quieter depending on the
        /// roll — level was carrying information the dice were setting, in a
        /// system whose whole premise is that the music IS the threat detector.
        ///
        /// A trim rather than re-rendered audio, for two reasons: the tracks are
        /// third-party files and rewriting a vendor's assets makes the next pack
        /// update a merge conflict (the scope rule add_headroom.py already
        /// follows), and a number in a config can be turned by ear afterwards
        /// while a baked file cannot.
        ///
        /// Deliberately does NOT touch the volume differences BETWEEN layers —
        /// menu loudest, safe quietest — because those are the user's tuning and
        /// they carry real meaning.
        public float TrimFor(AudioClip clip)
        {
            if (clip == null || trackTrims == null) return 1f;
            for (int i = 0; i < trackTrims.Length; i++)
                if (trackTrims[i] != null && trackTrims[i].clip == clip)
                    return Mathf.Pow(10f, trackTrims[i].trimDb / 20f);
            return 1f;
        }

        [Header("Layer volumes")]
        [FormerlySerializedAs("calmVolume")] [Range(0f, 1f)] public float dreadVolume = 0.35f;
        [Range(0f, 1f)] public float mysteryVolume = 0.42f;
        [FormerlySerializedAs("tenseVolume")] [Range(0f, 1f)] public float investigateVolume = 0.5f;
        [Range(0f, 1f)] public float chaseVolume = 0.75f;
        [Range(0f, 1f)] public float safeVolume = 0.28f; // quieter than dread — sanctuary whispers (user tuning 2026-07-23)
        [Range(0f, 1f)] public float endgameVolume = 0.6f;

        [Header("Stings")]
        public AudioClip[] spottedStings;  // jumpscare the instant he sees you
        public AudioClip heardRiser;       // dark riser when your noise reaches him
        public AudioClip deathSting;
        [Tooltip("The last clock lands and the gate gives way — heard map-wide, wherever you are.")]
        public AudioClip gateUnlockSting;
        [Tooltip("You step through the gate. Win.")]
        public AudioClip escapeSting;
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

    /// One track's level trim. A class rather than a struct so an unfilled entry
    /// in the inspector is null and gets skipped, instead of silently meaning
    /// "0 dB on a null clip".
    [System.Serializable]
    public class TrackTrim
    {
        public AudioClip clip;
        [Tooltip("Decibels added to this track's layer volume. Negative pulls a loud track back into line with its pool.")]
        [Range(-12f, 12f)] public float trimDb;
    }
}
