// Maps player state + 8-direction facing to sprite animation clips.
// Arrays are indexed by (int)FacingDirection (Down, DownLeft, Left, UpLeft,
// Up, UpRight, Right, DownRight). Generated and filled by the setup menu.
// The New_Leaf pack has no separate walk sheets, so Walk plays Run slowed
// (velocity-matched by PlayerAnimationDriver).
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Player
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Player Animation Set", fileName = "PlayerAnimationSet")]
    public class PlayerAnimationSet : ScriptableObject
    {
        [Tooltip("Order: Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight")]
        public SpriteAnimationClip[] idle = new SpriteAnimationClip[8];

        [Tooltip("Order: Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight")]
        public SpriteAnimationClip[] run = new SpriteAnimationClip[8];

        [Tooltip("Animation speed floor while moving — prevents comically slow leg cycles during acceleration")]
        [Range(0.1f, 1f)] public float minAnimationSpeed = 0.35f;

        public SpriteAnimationClip GetIdle(FacingDirection facing) => Get(idle, facing);
        public SpriteAnimationClip GetRun(FacingDirection facing) => Get(run, facing);

        static SpriteAnimationClip Get(SpriteAnimationClip[] clips, FacingDirection facing)
        {
            int i = (int)facing;
            return clips != null && i < clips.Length ? clips[i] : null;
        }
    }
}
