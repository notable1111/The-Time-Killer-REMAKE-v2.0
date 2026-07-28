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
        [Header("Whether it even occurs to him")]
        [Tooltip("Chance, rolled ONCE when a hunt begins, that wardrobes are on his mind at all this time. Fail it and he will not open a single door for that entire hunt however many he walks past. Deliberately low: his default theory is that you kept running, and only occasionally does it occur to him that you stopped. Rolled per hunt rather than per wardrobe on purpose — per wardrobe, a room with three spots would reach ~40% and hiding would get riskier the more hiding places a level has, which is backwards.")]
        [Range(0f, 1f)] public float checkChance = 0.12f;

        [Header("Which spots he considers")]
        [Tooltip("A wardrobe is worth a detour only if it sits this close to where the belief map is already sending him. Larger = he wanders off to check wardrobes far from his actual suspicion.")]
        public float considerRadius = 4f;

        [Tooltip("Don't re-check the same wardrobe again for this long. Without it he can bounce between two spots for the whole hunt.")]
        public float recheckCooldown = 25f;

        [Header("Opening it")]
        [Tooltip("Belief is summed over this radius AROUND the wardrobe. The wardrobe's own cell is solid, so it never carries belief itself — the real question is how strongly he thinks you are in this area.")]
        public float beliefSampleRadius = 1.5f;

        [Tooltip("How strongly he must believe in THIS spot compared to the likeliest place he can currently think of, 0..1. A relative test on purpose: an absolute mass was tried and measured fragile — the same threshold meant 6.5u in one flee direction and 9.5u in another, and it drifts with map size and with how long he has been searching. A share asks the question that actually matters: is this among the best guesses he has?")]
        [Range(0f, 1f)] public float openBeliefShare = 0.75f;

        [Tooltip("Absolute floor so he cannot open a wardrobe for being the likeliest spot on an essentially empty map. Belief is normalised (whole map = 1), so this is a small number.")]
        [Range(0f, 0.5f)] public float minAbsoluteMass = 0.02f;

        [Tooltip("He will not open a wardrobe further than this from where he LAST SAW you. This is the rule that actually decides whether hiding is safe, and it is a plain distance on purpose: belief share alone was measured (2026-07-28) to allow 2.5u-6.5u depending only on which way you ran, because the seed lays a long ridge of near-equal mass along your flee direction. A share cannot tighten a belief that is genuinely flat; a distance says what we mean.")]
        public float maxDistanceFromLastSeen = 3.5f;

        [Header("The beat")]
        [Tooltip("How long he stands at the door before deciding. This IS the tension — the heartbeat is at its loudest here, because he is about a unit away. Too short and the moment does not land; too long and it reads as him being stuck.")]
        public float dreadPauseSeconds = 1.2f;
    }
}
