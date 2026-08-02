// The heartbeat, published so anything can beat in time with it.
//
// PlayerHeartbeat exposed Bpm / Intensity / BeatThisFrame from the start, for
// exactly this — and for months nothing read them, because reading them meant a
// hard reference from a visual feature into the Heartbeat feature, which the
// architecture forbids. So HealthVfxDirector ran its OWN pulse clock at 74/118
// bpm off health, while the heart ran 56-160 off the maniac. The screen throbbed
// and the chest thumped, and they were never once in sync.
//
// An event fixes that: the heart announces its beat, and anyone who wants to
// throb subscribes. No feature references another, and deleting either side
// leaves the other working.
//
// Why this matters more than it sounds (research 2026-08-02): the James-Lange
// effect means a player unconsciously attributes a racing heart to fear — the
// heartbeat does not REPORT danger, it CAUSES the feeling of it. Two channels
// firing on the same instant is how that lands on a keyboard, with no haptics.
using UnityEngine;

namespace TimeKiller.Heartbeat
{
    /// Fired on the exact frame a beat sounds.
    public struct HeartbeatPulseEvent
    {
        /// Current rate, so a listener can shape its own decay to match.
        public float Bpm;
        /// 0..1 dread. Scale your effect by this or it will shout while calm.
        public float Intensity;
        /// True when this beat is the hard thud after a skipped one.
        public bool Palpitation;
    }
}
