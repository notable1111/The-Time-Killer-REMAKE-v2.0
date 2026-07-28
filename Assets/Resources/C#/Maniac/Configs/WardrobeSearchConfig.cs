// Tuning for the maniac checking wardrobes during a search.
//
// Hiding used to be BINARY: certain death if he watched you climb in (the
// compromised-spot march), and perfect safety if he did not — he never had a
// reason to walk near an occupied wardrobe, so the heartbeat in HidingVfx (which
// has always scaled with his distance) never had anything to climb toward.
// These numbers create the middle ground: he walks up, stands at the door for a
// beat, and opens it ONLY when his belief map says you really did come this way.
//
// openBeliefThreshold is the value worth tuning first. Belief is NORMALISED —
// the whole map sums to 1 — so the number is scale-free and does not need
// revisiting when a level gets bigger. Watch the live reading on the F1 overlay
// ("Wardrobe") while playing: it prints the mass actually sampled at the spot he
// is checking against the threshold, so the right value can be read off a real
// hunt rather than guessed.
using UnityEngine;

namespace TimeKiller.Maniac
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Wardrobe Search", fileName = "WardrobeSearchConfig")]
    public class WardrobeSearchConfig : ScriptableObject
    {
        [Header("Which spots he considers")]
        [Tooltip("A wardrobe is worth a detour only if it sits this close to where the belief map is already sending him. Larger = he wanders off to check wardrobes far from his actual suspicion.")]
        public float considerRadius = 4f;

        [Tooltip("Don't re-check the same wardrobe again for this long. Without it he can bounce between two spots for the whole hunt.")]
        public float recheckCooldown = 25f;

        [Header("Opening it")]
        [Tooltip("Belief is summed over this radius AROUND the wardrobe. The wardrobe's own cell is solid, so it never carries belief itself — the real question is how strongly he thinks you are in this area.")]
        public float beliefSampleRadius = 1.5f;

        [Tooltip("Normalised belief mass (whole map = 1) needed before he actually opens the door. Higher = hiding is safer and he bluffs more often. Read the live value off the F1 overlay to tune.")]
        [Range(0f, 0.5f)] public float openBeliefThreshold = 0.05f;

        [Header("The beat")]
        [Tooltip("How long he stands at the door before deciding. This IS the tension — the heartbeat is at its loudest here, because he is about a unit away. Too short and the moment does not land; too long and it reads as him being stuck.")]
        public float dreadPauseSeconds = 1.2f;
    }
}
