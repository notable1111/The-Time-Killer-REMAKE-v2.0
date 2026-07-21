// Plays SpriteAnimationClips on a SpriteRenderer. A tiny code-driven
// alternative to Unity's Animator — no controller graphs to maintain, and
// clips can be swapped mid-play while keeping the animation phase (so turning
// while running doesn't visibly restart the leg cycle).
// Fires FrameReached when playback enters a clip's eventFrames (foot contacts).
using System;
using UnityEngine;

namespace TimeKiller.Core
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        public event Action<int> FrameReached;

        [Tooltip("Optional clip that starts playing automatically (used by ambient props like torches)")]
        [SerializeField] SpriteAnimationClip playOnStart;

        SpriteRenderer spriteRenderer;
        SpriteAnimationClip clip;
        float speedMultiplier = 1f;
        float time;
        int lastFrame = -1;

        public SpriteAnimationClip CurrentClip => clip;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (playOnStart != null) Play(playOnStart);
        }

        /// preservePhase: keep the current animation time instead of restarting
        /// at frame 0 — used when only the direction of a clip changes.
        public void Play(SpriteAnimationClip newClip, float speed = 1f, bool preservePhase = false)
        {
            if (newClip == clip && Mathf.Approximately(speed, speedMultiplier)) return;
            clip = newClip;
            speedMultiplier = speed;
            if (!preservePhase) { time = 0f; lastFrame = -1; }
        }

        /// Live playback-speed control (velocity-matched animation): change speed
        /// without touching the clip or the phase.
        public void SetSpeed(float speed) => speedMultiplier = speed;

        void Update()
        {
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return;

            time += Time.deltaTime * clip.framesPerSecond * speedMultiplier;
            int frame = clip.loop
                ? Mathf.FloorToInt(time) % clip.frames.Length
                : Mathf.Min(Mathf.FloorToInt(time), clip.frames.Length - 1);
            spriteRenderer.sprite = clip.frames[frame];

            if (frame != lastFrame)
            {
                lastFrame = frame;
                if (clip.eventFrames != null && Array.IndexOf(clip.eventFrames, frame) >= 0)
                    FrameReached?.Invoke(frame);
            }
        }
    }
}
