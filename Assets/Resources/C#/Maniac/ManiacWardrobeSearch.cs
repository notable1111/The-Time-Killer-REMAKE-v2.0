// Lets the maniac check wardrobes while hunting.
//
// REMOVABLE by design: SearchState asks for this component and behaves exactly
// as it always did when it is absent, so deleting it from the maniac restores
// the old "hiding unseen is perfect safety" rule with no other edit.
//
// It owns only the DECISIONS — which spot is worth a detour, whether the door
// gets opened, and what he has already checked. The walking, the pause at the
// door and the catch itself stay in SearchState and ManiacController, because
// those are state-machine concerns and this is not a state.
using System.Collections.Generic;
using TimeKiller.Core;
using TimeKiller.Hiding;
using UnityEngine;

namespace TimeKiller.Maniac
{
    public class ManiacWardrobeSearch : MonoBehaviour
    {
        [SerializeField] WardrobeSearchConfig config;

        public WardrobeSearchConfig Config => config;
        public bool Ready => config != null;

        HidingSpot[] spots;

        // Spot -> the time it becomes checkable again. Keyed by the component so
        // a spot that is destroyed (level reload) simply falls out of the map.
        readonly Dictionary<HidingSpot, float> checkedUntil = new Dictionary<HidingSpot, float>();

        // Live readout for tuning against a real hunt rather than a guess.
        float lastMass;
        float lastShare;
        float lastDistance;
        string lastSpotName = "-";

        public void Init(WardrobeSearchConfig searchConfig) => config = searchConfig;

        // Taught by the Director, which counts hides the player actually got away
        // with. Zero unless a ManiacDirector is in the scene, so this whole idea
        // is opt-in and the base chance is untouched without one.
        float learnedBonus;

        // Escalation, resolved lazily — see the same note in ManiacPerception:
        // ManiacController adds the component in its own Awake and component
        // order on one GameObject is undefined, so a cached null at startup
        // would stay null for the whole run.
        ManiacEscalation escalation;
        ManiacEscalation Escalation
        {
            get
            {
                if (escalation == null) escalation = GetComponent<ManiacEscalation>();
                return escalation;
            }
        }

        /// The chance he opens wardrobes on THIS hunt: the authored base, plus
        /// whatever the player has taught him, plus how far into the run he is.
        /// Isolation unlocks behaviours from player metrics for exactly this
        /// reason — a creature that starts checking lockers only after you have
        /// used them reads as having noticed, which is far more unsettling than
        /// one that always checked.
        ///
        /// TWO sources now stack here, which is why the sum goes through
        /// ChanceWithCeiling rather than a bare Clamp01. The Director's promise
        /// that hiding can never become useless was made about ITS bonus alone
        /// (capped at 0.35); with escalation added on top, that promise has to be
        /// re-made about the total or it quietly expires in the last third of the
        /// run — exactly where a player leans on a wardrobe most. With no
        /// escalation component present the ceiling is 1 and this is the same
        /// arithmetic it always was.
        public float EffectiveCheckChance
        {
            get
            {
                if (config == null) return 0f;
                var esc = Escalation;
                float bonus = learnedBonus + (esc != null ? esc.WardrobeBonus : 0f);
                return ManiacEscalation.ChanceWithCeiling(
                    esc != null ? esc.Config : null, config.checkChance, bonus);
            }
        }

        void OnLearned(ManiacLearnedEvent evt) => learnedBonus = evt.WardrobeBonus;

        void Start()
        {
            spots = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventBus.Subscribe<ManiacLearnedEvent>(OnLearned);
            DebugOverlay.Watch("Wardrobe", () => config == null
                ? "NO CONFIG"
                : $"chance {EffectiveCheckChance:0.00} (base {config.checkChance:0.00}) " +
                  $"{lastSpotName} dist {lastDistance:0.0}/{config.maxDistanceFromLastSeen:0.0} " +
                  $"share {lastShare:0.00}/{config.openBeliefShare:0.00} ({spots?.Length ?? 0} spots)");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ManiacLearnedEvent>(OnLearned);
            DebugOverlay.Unwatch("Wardrobe");
        }

        /// The wardrobe worth a detour on the way to `beliefTarget`, or none.
        /// Nearest wins, so he clears the closest doubt first rather than
        /// crossing the room to a spot that happens to be marginally likelier.
        public bool TryPickSpot(Vector2 beliefTarget, out HidingSpot spot)
        {
            spot = null;
            if (config == null || spots == null) return false;

            float best = config.considerRadius;
            foreach (var candidate in spots)
            {
                if (candidate == null) continue;
                if (checkedUntil.TryGetValue(candidate, out var until) && Time.time < until) continue;
                float d = Vector2.Distance(beliefTarget, candidate.transform.position);
                if (d <= best) { best = d; spot = candidate; }
            }
            return spot != null;
        }

        /// He has now looked here — don't come back for a while, whatever he found.
        public void MarkChecked(HidingSpot spot)
        {
            if (spot == null || config == null) return;
            checkedUntil[spot] = Time.time + config.recheckCooldown;
        }

        /// Does he believe in it enough to actually open the door?
        ///
        /// This is the whole fairness rule. He opens only where his belief map is
        /// genuinely concentrated — i.e. he heard you here, or watched you run
        /// into this room — so hiding EARLY and AWAY from his last contact stays
        /// reliably safe, and hiding in the first box he is already walking toward
        /// does not. A player who is never sensed is never found.
        public bool ShouldOpen(HidingSpot spot, PlayerBeliefMap belief, Vector2 lastSeen)
        {
            if (spot == null || belief == null || config == null) return false;
            lastSpotName = spot.name;
            lastMass = belief.MassNear(spot.transform.position, config.beliefSampleRadius);
            lastShare = 0f;

            // THE rule that decides whether hiding is safe. Hiding out of the
            // area he last had you in must reliably work, or wardrobes stop being
            // a plan and become a coin flip — which is exactly how this shipped.
            lastDistance = Vector2.Distance(spot.transform.position, lastSeen);
            if (lastDistance > config.maxDistanceFromLastSeen) return false;

            // RELATIVE, not absolute. Measured 2026-07-28: an absolute threshold
            // of 0.05 let him open any wardrobe within 9.5u of where he last saw
            // you, because a freshly seeded map concentrates its whole mass (it
            // sums to 1) into a short trail worth 0.13-0.44 per sample. Worse, the
            // same number meant 6.5u in one flee direction and 9.5u in another.
            // Comparing against the best guess he currently has removes all of
            // that: the question is "is this among the likeliest places?", which
            // does not care how big the map is or how long he has been looking.
            if (!belief.BestTarget(out var peakPosition)) return false;
            float peak = belief.MassNear(peakPosition, config.beliefSampleRadius);
            if (peak <= 0.0001f) return false;

            lastShare = lastMass / peak;
            return lastShare >= config.openBeliefShare
                && lastMass >= config.minAbsoluteMass;
        }
    }
}
