// What a wardrobe does to the sound of the room.
//
// Asset: C#/Audio/Configs/WorldMuffleConfig.asset, loaded from Resources by
// WorldMuffle. DELETE THE ASSET AND THE FEATURE NEVER INSTALLS — that is the
// uninstall, same contract as AudioMix and ManiacThreatEffects.
using UnityEngine;

namespace TimeKiller.Audio
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/World Muffle", fileName = "WorldMuffleConfig")]
    public class WorldMuffleConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Audio/Configs/WorldMuffleConfig";

        [Tooltip("Master switch. Off = every source keeps its own cutoff and nothing is touched.")]
        public bool enabled = true;

        [Header("The muffle")]
        [Tooltip("Low-pass corner while hidden, in Hz. 900 puts a wardrobe door between you and the room: his boots keep their thud and lose their edge. Higher = thinner door.")]
        [Range(300f, 8000f)] public float cutoffHz = 900f;

        [Tooltip("Filter resonance. 1 is flat; above that the corner starts to ring, which reads as a metal box rather than wood.")]
        [Range(1f, 4f)] public float resonance = 1f;

        [Header("Timing")]
        [Tooltip("Seconds to close. Fast — the door shutting IS the beat, and a slow close reads as the game thinking rather than the door moving.")]
        [Range(0.02f, 1.5f)] public float closeSeconds = 0.22f;

        [Tooltip("Seconds to open. Slower than closing, so stepping out feels like the room rushing back rather than a switch being flipped.")]
        [Range(0.02f, 2f)] public float openSeconds = 0.45f;

        [Header("What counts as 'in the room'")]
        [Tooltip("Only AudioSources with spatialBlend at or above this are muffled. The project already draws this line in its own comments: the heartbeat, the breath, your voice and the fear drone are all 2D because they are INSIDE YOUR HEAD, and a wardrobe door cannot muffle those. The maniac's three sources, the exit-door beacon and the clock winds are positional, and those are the room.")]
        [Range(0.01f, 1f)] public float spatialThreshold = 0.5f;

        [Tooltip("Cutoff treated as 'no filtering' when opening back up. Unity's own default ceiling.")]
        public float openCutoffHz = 22000f;
    }
}
