// He gets worse as the run goes on.
//
// THE MEASURED PROBLEM. Over 407 recorded seconds the tension curve came out
// Panic 44.3% / Safe 26.8% / Aftershock 20.5% / Unease 5.7% / Threat 2.7%: the
// run is either safe or a chase, and the BUILD — the part where you know he is
// somewhere and you do not yet know where — is 8% of it. Stretching the moment
// of being caught (awarenessCertaintyScale, 2026-08-04) made the caught-moment
// ~5x longer and did not fill that band in, which is what put per-clock
// escalation next in the plan rather than another pass on his senses.
//
// The horror-design research names the same thing as the genre's core back-half
// failure: "escalation through new elements is absent". This game had the purest
// form of it — the third clock was mechanically identical to the first. You were
// rewarded for progress with more of exactly what you had already beaten.
//
// WHAT IT DOES: as the objectives complete, he moves faster on PATROL (so you
// meet him more often), hears further (so the late run demands more care), and
// opens wardrobes more readily (so the endgame is not "sit in a box"). What it
// deliberately does NOT touch — chase speed, sight range, damage — and why, is
// written on ManiacEscalationConfig.
//
// REMOVABLE, three ways over. Delete this component and every consumer falls
// back to its authored config value (they all null-guard). Delete the config
// asset and the component itself becomes an identity. Delete the whole
// Objectives feature and nothing ever publishes progress, so he simply never
// escalates — this listens to Core's WorldProgressEvent and has never heard of a
// clock.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Maniac
{
    public class ManiacEscalation : MonoBehaviour
    {
        [SerializeField] ManiacEscalationConfig config;

        public ManiacEscalationConfig Config => config;

        /// How many objectives are done, and out of how many. Total 0 = nothing
        /// in the scene counts progress, which is a legitimate state (the bot's
        /// bare test scenes, a future mode) and must read as "not escalated".
        public int Step { get; private set; }
        public int Total { get; private set; }

        /// Where he is heading: 0 at the start of the run, 1 with every objective
        /// done. The instantaneous truth.
        public float Target => Ramp(Step, Total);

        /// Where he actually IS, blended toward Target over blendSeconds. Every
        /// multiplier below reads THIS, so the castle tightens over a few seconds
        /// instead of snapping the moment a clock lights up.
        public float Intensity { get; private set; }

        /// Multiplier on patrol / investigate / search speed. 1 = unchanged.
        public float MoveSpeed => MoveMultiplier(config, Intensity);

        /// Multiplier on how far a noise carries to him. 1 = unchanged.
        public float Hearing => HearingMultiplier(config, Intensity);

        /// Added to his wardrobe check chance. 0 = unchanged.
        public float WardrobeBonus => WardrobeBonusFor(config, Intensity);

        /// Multiplier on the Director's quiet thresholds — smaller = he is
        /// steered back at you sooner. 1 = unchanged.
        public float HintQuiet => HintQuietMultiplier(config, Intensity);

        public bool Active => config != null && config.enabled;

        void Awake()
        {
            // Self-loading, like AudioMix: the whole Assets/Resources tree is a
            // Resources root, so the feature works in CastleWing, Catacombs and
            // any scene added later with nothing to wire and nothing to forget.
            // No asset -> config stays null -> every multiplier is an identity
            // and the game is exactly the game it was.
            if (config == null)
                config = Resources.Load<ManiacEscalationConfig>(ManiacEscalationConfig.ResourcesPath);
        }

        void OnEnable() => EventBus.Subscribe<WorldProgressEvent>(OnProgress);

        void OnDisable() => EventBus.Unsubscribe<WorldProgressEvent>(OnProgress);

        void Start() => DebugOverlay.Watch("Escalate", () =>
            !Active ? (config == null ? "no config (off)" : "off")
                    : $"{Step}/{Total} lvl {Intensity:0.00}->{Target:0.00}  " +
                      $"spd x{MoveSpeed:0.00} ear x{Hearing:0.00} wardrobe +{WardrobeBonus:0.00}");

        void OnDestroy() => DebugOverlay.Unwatch("Escalate");

        void OnProgress(WorldProgressEvent evt)
        {
            Step = evt.Step;
            Total = evt.Total;

            // Said out loud so audio, telemetry and the recorder can mark the
            // beat without any of them polling this component — and so "did he
            // actually escalate?" is answerable from a session log rather than
            // from someone's impression of the run.
            EventBus.Publish(new ManiacEscalatedEvent
            {
                Step = Step,
                Total = Total,
                Intensity = Target,
            });
        }

        void Update()
        {
            if (!Active) { Intensity = 0f; return; }

            float target = Target;
            if (config.blendSeconds <= 0f) { Intensity = target; return; }

            // Rate-based rather than a smoothing constant: blendSeconds then
            // means what it says on the tooltip — the seconds a full 0->1 change
            // takes — instead of an exponential tail that never quite arrives.
            Intensity = Mathf.MoveTowards(Intensity, target, Time.deltaTime / config.blendSeconds);
        }

        // ---- Pure. Everything above is plumbing; these four are the feature,
        // and they are static so the EditMode suite can pin them without a
        // scene, a frame or a physics step (ARCHITECTURE: "only pure functions").

        /// 0 at the start of the run, 1 with everything done. Total 0 -> 0.
        public static float Ramp(int step, int total)
            => total <= 0 ? 0f : Mathf.Clamp01((float)step / total);

        public static float MoveMultiplier(ManiacEscalationConfig cfg, float ramp)
            => cfg == null || !cfg.enabled
                ? 1f
                : Mathf.Lerp(1f, Mathf.Max(0.01f, cfg.moveSpeedAtFull), Mathf.Clamp01(ramp));

        public static float HearingMultiplier(ManiacEscalationConfig cfg, float ramp)
            => cfg == null || !cfg.enabled
                ? 1f
                : Mathf.Lerp(1f, Mathf.Max(0.01f, cfg.hearingAtFull), Mathf.Clamp01(ramp));

        /// Multiplier on the Director's quiet thresholds. Note this one goes
        /// DOWN as he escalates — less waiting, not more — so the neutral value
        /// is 1 and the escalated value is smaller. Clamped above 0 because a
        /// zero here would make the Director fire every frame.
        public static float HintQuietMultiplier(ManiacEscalationConfig cfg, float ramp)
            => cfg == null || !cfg.enabled
                ? 1f
                : Mathf.Lerp(1f, Mathf.Clamp(cfg.hintQuietAtFull, 0.05f, 1f), Mathf.Clamp01(ramp));

        public static float WardrobeBonusFor(ManiacEscalationConfig cfg, float ramp)
            => cfg == null || !cfg.enabled
                ? 0f
                : Mathf.Lerp(0f, Mathf.Max(0f, cfg.wardrobeBonusAtFull), Mathf.Clamp01(ramp));

        /// The wardrobe chance from ALL sources, held under the ceiling.
        ///
        /// The Director caps what the player can TEACH him (maxWardrobeBonus
        /// 0.35) so that hiding never becomes useless. Escalation is a second
        /// source stacking on top of that, so the promise has to be re-made about
        /// the sum or it quietly stops being true in the last third of the run —
        /// which is precisely where a player is most reliant on a wardrobe.
        ///
        /// Never REDUCES an authored base: if someone deliberately sets
        /// checkChance above the ceiling, that is a decision, not an overflow.
        public static float ChanceWithCeiling(ManiacEscalationConfig cfg, float baseChance, float bonus)
        {
            float ceiling = cfg == null || !cfg.enabled ? 1f : Mathf.Clamp01(cfg.maxWardrobeChance);
            return Mathf.Clamp(baseChance + bonus, 0f, Mathf.Max(Mathf.Clamp01(baseChance), ceiling));
        }
    }
}
