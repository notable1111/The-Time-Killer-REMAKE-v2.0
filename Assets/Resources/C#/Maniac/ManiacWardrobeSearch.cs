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

        // Live readout for tuning openBeliefThreshold against a real hunt
        // rather than a guess — see the config's comment.
        float lastMass;
        string lastSpotName = "-";

        public void Init(WardrobeSearchConfig searchConfig) => config = searchConfig;

        void Start()
        {
            spots = Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            DebugOverlay.Watch("Wardrobe", () => config == null
                ? "NO CONFIG"
                : $"{lastSpotName} mass {lastMass:0.000}/{config.openBeliefThreshold:0.000} ({spots?.Length ?? 0} spots)");
        }

        void OnDestroy() => DebugOverlay.Unwatch("Wardrobe");

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
        public bool ShouldOpen(HidingSpot spot, PlayerBeliefMap belief)
        {
            if (spot == null || belief == null || config == null) return false;
            lastSpotName = spot.name;
            lastMass = belief.MassNear(spot.transform.position, config.beliefSampleRadius);
            return lastMass >= config.openBeliefThreshold;
        }
    }
}
