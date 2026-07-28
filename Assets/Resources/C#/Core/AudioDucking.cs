// A single dial the atmospheric audio voluntarily respects.
//
// When the maniac has you, the design is that you hear your own heart and
// nothing else. Without a mixer there is no bus to pull down, and
// AudioListener.volume is no use because it would take the heartbeat with it.
// So this is a plain shared dial: whoever owns "how loud is the world" writes
// it, and the atmospheric sources multiply their own volume by it.
//
// Voluntary on purpose. Nothing is forced through it, so any system can opt out
// (footsteps deliberately do — see below), and if the writer is deleted the dial
// simply stays at 1 and every listener behaves exactly as it did before.
//
// NOT ducked: footsteps and effects. "Only the heartbeat" is about killing the
// score and the ambience — the bed you stop noticing until it vanishes. Muting
// your own footsteps would take away the feedback you steer by at the exact
// moment you most need it, which reads as a bug rather than as tension.
using UnityEngine;

namespace TimeKiller.Core
{
    public static class AudioDucking
    {
        /// 1 = the world at full volume, 0 = silence. Read every frame by the
        /// atmospheric sources; smoothed by whoever writes it, not here.
        public static float World { get; private set; } = 1f;

        public static void SetWorld(float value) => World = Mathf.Clamp01(value);

        // Statics survive a domain reload in the editor when "Reload Domain" is
        // off, so a run that ended mid-duck would start the next one muffled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => World = 1f;
    }
}
