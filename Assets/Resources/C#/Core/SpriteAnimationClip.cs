// A sprite-sheet animation as a data asset: which frames, how fast, looping.
// Clips are generated from sliced sheets by editor setup scripts and played
// by SpriteAnimator. Reusable for any animated sprite (player, enemies, props).
using UnityEngine;

namespace TimeKiller.Core
{
    [CreateAssetMenu(menuName = "TimeKiller/Sprite Animation Clip", fileName = "NewSpriteClip")]
    public class SpriteAnimationClip : ScriptableObject
    {
        public Sprite[] frames;
        public float framesPerSecond = 10f;
        public bool loop = true;

        [Tooltip("Frame indices that fire SpriteAnimator.FrameReached (e.g. foot-contact frames)")]
        public int[] eventFrames = new int[0];
    }
}
