// The seam between "something bled" and "something draws blood".
//
// BloodTrail publishes, BloodStainField draws. Neither holds a reference to the
// other, so either can be deleted and the rest of the game is unchanged: with no
// field the events fall on the floor unheard, with no trail the field simply
// never fills. That is the removable-feature rule applied inside one feature.
//
// It also means anything else that bleeds later — the maniac taking a hit, a
// broken body prop, a trap — marks the floor for free by publishing this.
using UnityEngine;

namespace TimeKiller.Blood
{
    public struct BloodSpilledEvent
    {
        public Vector2 Position;
        /// 0..1-ish. Scales stain size and count; a wound spills more than a drip.
        public float Amount;
        /// Which way the blood was thrown. Zero means "straight down" — a drip.
        public Vector2 Direction;
        /// How far the spill scatters from Position.
        public float Spread;
        /// How many stains to lay down.
        public int Count;
    }
}
