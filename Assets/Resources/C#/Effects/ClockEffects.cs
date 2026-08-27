// Binds the clock-repair beats to their recipes: ClockHitEvent -> a small burst
// at the mechanism, ClockFixedEvent -> the clock waking up.
//
// WHY THIS EXISTS (2026-08-04). Audited on the day: EffectPlayer.Play had
// exactly two call sites in the entire game, the player being hit and the player
// dying. Fixing a clock — the core objective, the thing a run is about — fired
// an event that only TestTelemetry listened to, so the biggest progress moment
// in the game produced a one-frame sprite swap and nothing else. A correct
// skill-check press produced nothing whatsoever.
//
// Same shape as PlayerHitEffects, deliberately: a binder holds the recipes and
// the feature holds none, so swapping the art or the sound is editing a recipe
// asset and never code. Removable — delete this object and the clocks go back to
// being silent; nothing else notices, because nobody references it.
using TimeKiller.Core;
using TimeKiller.Objectives;
using UnityEngine;

namespace TimeKiller.Effects
{
    public class ClockEffects : MonoBehaviour
    {
        [SerializeField] EffectRecipe hitRecipe;
        [SerializeField] EffectRecipe fixedRecipe;

        /// Counts for the F1 overlay. The alternative to a counter is playing
        /// the game and trusting your eyes to tell you whether a burst fired,
        /// which is exactly the "play it and look" verification this project
        /// stopped accepting.
        public int HitsPlayed { get; private set; }
        public int FixesPlayed { get; private set; }

        void OnEnable()
        {
            EventBus.Subscribe<ClockHitEvent>(OnHit);
            EventBus.Subscribe<ClockFixedEvent>(OnFixed);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ClockHitEvent>(OnHit);
            EventBus.Unsubscribe<ClockFixedEvent>(OnFixed);
        }

        void Start() => DebugOverlay.Watch("ClockVfx", () =>
            $"{HitsPlayed} hit / {FixesPlayed} fixed" +
            (hitRecipe == null || fixedRecipe == null ? "  (RECIPE MISSING — run Setup/50)" : ""));

        void OnDestroy() => DebugOverlay.Unwatch("ClockVfx");

        void OnHit(ClockHitEvent evt)
        {
            EffectPlayer.Play(hitRecipe, evt.Position);
            HitsPlayed++;
        }

        void OnFixed(ClockFixedEvent evt)
        {
            EffectPlayer.Play(fixedRecipe, evt.Position);
            FixesPlayed++;
        }
    }
}
