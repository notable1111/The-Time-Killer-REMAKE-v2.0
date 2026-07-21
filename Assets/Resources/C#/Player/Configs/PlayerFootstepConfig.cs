// Footstep tuning: which sounds, how loud when walking vs running, and how
// much random variation. Loudness here is BOTH what you hear and what enemies
// will "hear" for stealth — one number drives both, so audio never lies.
using UnityEngine;

namespace TimeKiller.Player
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Player Footsteps", fileName = "PlayerFootstepConfig")]
    public class PlayerFootstepConfig : ScriptableObject
    {
        [Tooltip("Random pick per step — variation prevents machine-gun repetition")]
        public AudioClip[] stepClips;

        [Header("Loudness (audio volume AND stealth noise level)")]
        [Range(0f, 1f)] public float walkLoudness = 0.3f;
        [Range(0f, 1f)] public float runLoudness = 0.85f;

        [Header("Variation")]
        [Tooltip("Random pitch offset per step, e.g. 0.08 = ±8%")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.08f;
    }
}
