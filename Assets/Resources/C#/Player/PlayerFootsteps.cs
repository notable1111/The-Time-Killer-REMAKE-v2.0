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
                // Trimmed by the mix but never DUCKED by it (duckDepth 1.0):
                // muting the feedback you steer by reads as a bug, not as tension.
                source.PlayOneShot(clip, loudness
                    * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.PlayerFootsteps));
            }

            // Composure makes you louder TO HIM. The dial is a shared static in
            // Core with a default of 1, so this line is a no-op unless something
            // is writing it and PlayerFootsteps has never heard of the Sanity
            // feature — delete Sanity and the dial simply stays at 1.
            //
            // Applied to the PUBLISHED loudness only, deliberately NOT to the
            // PlayOneShot volume above. Making your own footsteps audibly louder
            // would be the honest feedback cue, but the mix is a finished,
            // measured balance (peak-sum 6.72 -> 3.01) and footsteps are the one
            // channel documented as trimmed-but-never-ducked because muting the
            // feedback you steer by reads as a bug. Reaching into that from a new
            // feature is the mistake CLAUDE.md §3 was written about. Composure
            // publishes ComposureChangedEvent instead, and the audio lane can
            // present it properly on their own terms.
            EventBus.Publish(new PlayerFootstepEvent
            {
                Position = transform.position,
                IsRunning = running,
                Loudness = loudness * PlayerNoiseDial.Multiplier
            });
        }
    }
}
