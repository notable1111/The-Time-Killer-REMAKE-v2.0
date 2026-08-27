// What plays on his two threat beats, and how often it is allowed to.
//
// The recipes live here rather than on a scene component so the binder can
// install itself from Resources in every scene — including Catacombs, which
// never got the Setup pass that placed ClockEffects and would otherwise have
// been silently left out. Swapping the art or the sound is still editing a
// recipe asset and never code.
using UnityEngine;

namespace TimeKiller.Effects
{
    [CreateAssetMenu(fileName = "ManiacThreatEffectsConfig",
                     menuName = "TimeKiller/Configs/Maniac Threat Effects")]
    public class ManiacThreatEffectsConfig : ScriptableObject
    {
        /// Deleting this asset is the uninstall: the binder finds nothing and
        /// never installs itself. Same self-loading contract as AudioMixConfig.
        public const string ResourcesPath = "C#/Effects/Configs/ManiacThreatEffectsConfig";

        [Tooltip("Plays the moment he gets eyes on you — the emotional peak of a hide-and-run game, and until now the only beat in it with no picture at all (it was a voice growl and a music sting).\n\nEmpty recipe = silent, no error. That is the intended state until the art exists.")]
        public EffectRecipe spotted;

        [Tooltip("Plays on the swing itself, hit or miss.\n\nDeliberately separate from PlayerHitEvent's recipe: that one fires only when damage lands, so a swing you dodged — the near miss, the best moment in the game — produced nothing on screen.")]
        public EffectRecipe attack;

        [Header("Anti-strobe")]
        [Tooltip("Minimum seconds between two spotted effects.\n\nThis is not polish, it is the difference between a beat and a strobe: ManiacSpottedPlayerEvent fires every time line of sight is RE-established, and mid-chase that happens behind every pillar. The music sting has carried a 4s cooldown for exactly this reason since 2026-07-23; a visual without one would flash several times per chase and stop meaning 'he has seen you'.")]
        [Range(0f, 15f)] public float spottedCooldown = 4f;

        [Tooltip("Minimum seconds between two attack effects. His attackCooldown is 1.6s and his recovery is 0.35s, so this only ever catches a double-publish — keep it short or it will eat the second swing of a real flurry.")]
        [Range(0f, 2f)] public float attackCooldown = 0.3f;
    }
}
