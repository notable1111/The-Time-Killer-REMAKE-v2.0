// Turns foot-contact animation frames into sound + a stealth event.
// SpriteAnimator says "a foot touched the ground" → this plays a random step
// (louder when running) and publishes PlayerFootstepEvent for future enemy
// hearing. Movement and animation code know nothing about audio or stealth.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Player
{
    [RequireComponent(typeof(SpriteAnimator))]
    [RequireComponent(typeof(PlayerAnimationDriver))]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] PlayerFootstepConfig config;

        SpriteAnimator animator;
        PlayerAnimationDriver driver;
        AudioSource source;

        void Awake()
        {
            animator = GetComponent<SpriteAnimator>();
            driver = GetComponent<PlayerAnimationDriver>();
            source = GetComponent<AudioSource>();
        }

        void OnEnable() => animator.FrameReached += OnFrameReached;
        void OnDisable() => animator.FrameReached -= OnFrameReached;

        void OnFrameReached(int frame)
        {
            // Event frames only exist on run clips, but guard anyway: no steps while idle.
            if (config == null || !driver.IsMovingState) return;

            bool running = driver.IsRunningState;
            float loudness = running ? config.runLoudness : config.walkLoudness;

            if (config.stepClips != null && config.stepClips.Length > 0)
            {
                var clip = config.stepClips[Random.Range(0, config.stepClips.Length)];
                source.pitch = 1f + Random.Range(-config.pitchJitter, config.pitchJitter);
                source.PlayOneShot(clip, loudness);
            }

            EventBus.Publish(new PlayerFootstepEvent
            {
                Position = transform.position,
                IsRunning = running,
                Loudness = loudness
            });
        }
    }
}
