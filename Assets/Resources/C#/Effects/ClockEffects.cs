// Binds the objective loop's beats to their recipes: the earned press, the clock
// waking, and the gate unlocking.
//
// ⚠️ THE ESCAPE ITSELF IS DELIBERATELY NOT HERE, and the reason is measured, not
// stylistic. Winning publishes RunEndedEvent, and GameFlow sets Time.timeScale
// to 0 on that beat. SpriteAnimator advances on Time.deltaTime and EffectPlayer
// destroys a one-shot with a delayed Destroy, which is also scaled — so a world
// burst played on GameWonEvent freezes on its first frame and is never cleaned
// up. It would not be a flourish, it would be a sprite stuck under the end
// screen. The escape flourish belongs on RunEndScreen, which already fades on
// unscaledDeltaTime, and that is UI work rather than a recipe.
//
// WHY THIS EXISTS (2026-08-04). Audited on the day: EffectPlayer.Play had
// exactly two call sites in the entire game, the player being hit and the player
// dying. Fixing a clock — the core objective, the thing a run is about — fired
// an event that only TestTelemetry listened to, so the biggest progress moment
// in the game produced a one-frame sprite swap and nothing else. A correct
// skill-check press produced nothing whatsoever.
//
// WHY IT WAS STILL NOT LIVE (found 2026-08-27). The binder was written as a
// scene object placed by Setup/50 — and Setup/50 had never been run. No scene
// contained a ClockEffects, no recipe assets existed, and clock_hit.png and
// clock_wake.png sat on disk unused. The feature read as shipped in the docs and
// was inert in the game. It now INSTALLS ITSELF from Resources, which is the
// same fix ManiacThreatEffects uses and for the same two reasons: a scene-object
// binder is silently absent from every scene nobody ran the script on (Catacombs
// would have been missed even if CastleWing had not), and a feature that needs
// no scene edit cannot collide with the other sessions sharing this Editor.
//
// A hand-placed scene object still wins if one exists, so nothing that was
// wired by hand before this change breaks.
//
// Removable: delete the recipe assets and the beats fall silent; delete this
// file and nothing references it. Swapping the art or the sound is editing a
// recipe asset, never code.
using TimeKiller.Core;
using TimeKiller.Objectives;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Effects
{
    public class ClockEffects : MonoBehaviour
    {
        // Serialized for the legacy hand-placed path; filled from Resources when
        // this installs itself. Same asset either way.
        [SerializeField] EffectRecipe hitRecipe;
        [SerializeField] EffectRecipe fixedRecipe;
        [SerializeField] EffectRecipe gateRecipe;

        public const string RecipeFolder = "C#/Effects/Configs/Recipes/";

        static ClockEffects instance;

        /// Counts for the F1 overlay. The alternative to a counter is playing
        /// the game and trusting your eyes to tell you whether a burst fired,
        /// which is exactly the "play it and look" verification this project
        /// stopped accepting — and which is how this feature stayed dead for
        /// three weeks while the docs said it shipped.
        public int HitsPlayed { get; private set; }
        public int FixesPlayed { get; private set; }
        public int GatesPlayed { get; private set; }

        Transform player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            // A hand-placed binder is authoritative — it may carry recipes some-
            // one chose deliberately, and two binders would double every burst.
            if (FindAnyObjectByType<ClockEffects>() != null) return;

            var hit = Resources.Load<EffectRecipe>(RecipeFolder + "ClockHit");
            var fixd = Resources.Load<EffectRecipe>(RecipeFolder + "ClockFixed");
            var gate = Resources.Load<EffectRecipe>(RecipeFolder + "GateOpened");
            // No recipes at all = the feature is not installed. Run Setup/50.
            if (hit == null && fixd == null && gate == null) return;

            var host = new GameObject("[ClockEffects]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ClockEffects>();
            instance.hitRecipe = hit;
            instance.fixedRecipe = fixd;
            instance.gateRecipe = gate;
        }

        void OnEnable()
        {
            EventBus.Subscribe<ClockHitEvent>(OnHit);
            EventBus.Subscribe<ClockFixedEvent>(OnFixed);
            EventBus.Subscribe<AllClocksFixedEvent>(OnAllFixed);
            DebugOverlay.Watch("ClockVfx", () =>
                $"{HitsPlayed} hit / {FixesPlayed} fixed / {GatesPlayed} gate" +
                (hitRecipe == null || fixedRecipe == null ? "  (RECIPES MISSING — run Setup/50)" : ""));
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ClockHitEvent>(OnHit);
            EventBus.Unsubscribe<ClockFixedEvent>(OnFixed);
            EventBus.Unsubscribe<AllClocksFixedEvent>(OnAllFixed);
            DebugOverlay.Unwatch("ClockVfx");
        }

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

        /// The gate unlocking, played AT THE GATE rather than at the player.
        ///
        /// This one is information, not decoration. The moment the last clock is
        /// fixed the run changes shape — the question stops being "where are the
        /// clocks" and becomes "where is the door" — and the audio already
        /// answers map-wide with a deliberately non-positional sting. A burst on
        /// the door itself is the visual half of that answer. Falls back to the
        /// player if the level has no ExitDoor, so a scene without one still
        /// gets the beat rather than a burst at the world origin.
        void OnAllFixed(AllClocksFixedEvent evt)
        {
            var door = FindAnyObjectByType<ExitDoor>();
            EffectPlayer.Play(gateRecipe, door != null ? (Vector2)door.transform.position : PlayerPosition());
            GatesPlayed++;
        }

        Vector2 PlayerPosition()
        {
            if (player == null)
            {
                var controller = FindAnyObjectByType<PlayerController>();
                if (controller != null) player = controller.transform;
            }
            return player != null ? (Vector2)player.position : Vector2.zero;
        }
    }
}
