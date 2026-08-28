// One shared dial: how loud the player currently is, over and above what they
// are doing. 1 = exactly as loud as their footstep config says.
//
// This is the AudioDucking pattern applied to a gameplay value rather than an
// audio one, and for the same reason. Composure needs to make you louder, and
// footsteps need to be made louder — but PlayerFootsteps must not reference the
// Sanity feature and Sanity must not reach into the Player feature, or neither
// could be deleted without the other. A static dial in Core is written by one
// and read by the other, and NEITHER knows the other exists.
//
// The failure mode is the good one: delete the writer and the dial simply stays
// at 1, so the game is exactly the game it was. Delete the reader and composure
// still computes, still shows on the overlay, and just changes nothing.
//
// Deliberately NOT an event. A footstep asks "how loud am I right now" at the
// instant it happens, which is a question about current state; delivering that
// as a stream of change notifications would mean every reader keeping its own
// copy of the answer, and a reader that subscribed late would have none.
namespace TimeKiller.Core
{
    public static class PlayerNoiseDial
    {
        /// Multiplier on the player's emitted noise. 1 = unmodified.
        /// Clamped on write so a broken writer cannot silence the player
        /// completely (0 would make them undetectable, which no design here
        /// intends) or make them absurdly loud.
        public static float Multiplier { get; private set; } = 1f;

        public static void Set(float value) =>
            Multiplier = UnityEngine.Mathf.Clamp(value, 0.25f, 4f);

        /// Statics survive play-mode restarts when Domain Reload is disabled —
        /// the contract GameBootstrap documents and clears itself against. A
        /// stale multiplier from a previous session would silently retune
        /// stealth for the next one.
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Multiplier = 1f;
    }
}
