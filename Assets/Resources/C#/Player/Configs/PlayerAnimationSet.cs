// Maps player state + 8-direction facing to sprite animation clips.
// Arrays are indexed by (int)FacingDirection (Down, DownLeft, Left, UpLeft,
// Up, UpRight, Right, DownRight). Generated and filled by the setup menu.
// Walk is optional: packs that ship no walk sheets (the New_Leaf placeholder)
// leave it empty and GetWalk falls back to the run clip played slowly, which is
// what Setup/5 relies on. Packs that do have one (the PixelLab Survivor, via
// Setup/33) get a real walk cycle instead — at walkSpeed 2.2 vs runSpeed 4.5 a
// halved run cycle reads as slow-motion arm pumping rather than walking.
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

        [Tooltip("Optional — leave empty to reuse the run clips slowed down.\nOrder: Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight")]
        public SpriteAnimationClip[] walk = new SpriteAnimationClip[8];

        [Tooltip("Optional — the standing work loop played while repairing a clock. Leave empty to reuse idle.\nOrder: Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight")]
        public SpriteAnimationClip[] repair = new SpriteAnimationClip[8];

        [Tooltip("Animation speed floor while moving — prevents comically slow leg cycles during acceleration")]
        [Range(0.1f, 1f)] public float minAnimationSpeed = 0.35f;

        public SpriteAnimationClip GetIdle(FacingDirection facing) => Get(idle, facing);
        public SpriteAnimationClip GetRun(FacingDirection facing) => Get(run, facing);

        /// True when this pack ships a real walk cycle rather than borrowing the
        /// run clips. Callers use it to pick which speed the playback rate is
        /// normalised against.
        public bool HasWalkClips => walk != null && walk.Length == 8 && walk[0] != null;

        public SpriteAnimationClip GetWalk(FacingDirection facing) =>
            HasWalkClips ? Get(walk, facing) : Get(run, facing);

        /// True when this pack ships a clock-repair work loop.
        public bool HasRepairClips => repair != null && repair.Length == 8 && repair[0] != null;

        /// Falls back to idle rather than null: a pack without repair art must
        /// still show a standing player, never an empty sprite, mid-objective.
        public SpriteAnimationClip GetRepair(FacingDirection facing) =>
            HasRepairClips ? Get(repair, facing) : Get(idle, facing);

        static SpriteAnimationClip Get(SpriteAnimationClip[] clips, FacingDirection facing)
        {
            int i = (int)facing;
            return clips != null && i < clips.Length ? clips[i] : null;
        }
    }
}
