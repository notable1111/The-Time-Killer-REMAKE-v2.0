// Noises made by the WORLD rather than by the player's feet — a gate slamming
// open, a prop toppling, glass breaking. Anything that should be able to draw a
// hunter. Published on the Core bus; each listener decides whether it actually
// hears it, so nothing here depends on the Maniac feature existing.
namespace TimeKiller.Core
{
    public struct WorldNoiseEvent
    {
        public UnityEngine.Vector2 Position;
        public float Loudness;   // 0..1, scales the listener's hearing radius
        public bool AlwaysHeard; // map-wide: skip the radius check entirely
    }

    /// The run's objective moved forward — one more clock fixed, one more of
    /// whatever a future mode counts. Deliberately says NOTHING about clocks.
    ///
    /// This is a Core event rather than an Objectives one because the maniac
    /// escalates on it, and a system must compile even if the systems it talks
    /// about do not exist (CLAUDE.md section 4). Subscribing him straight to
    /// ClockFixedEvent would mean deleting the Objectives folder stops the
    /// Maniac feature compiling — exactly the coupling the removability rule
    /// exists to prevent, and the same reasoning that keeps ManiacHintEvent in
    /// the Maniac feature rather than in the Director that sends it.
    ///
    /// The other half of the trade: any future objective — a ritual, a fuse box,
    /// a generator — escalates him for free by publishing this, with no edit to
    /// the Maniac feature at all.
    /// The world now holds a physical TRACE at this point — a smear of blood, and
    /// later whatever else a hunter could read off the floor. Not a noise: nobody
    /// hears it, and it does not decay by distance. It simply sits there until
    /// something walks close enough to notice it.
    ///
    /// Core, not Blood, for the reason WorldProgressEvent is Core and not
    /// Objectives: the maniac must compile with the Blood folder deleted. This is
    /// the seam ARCHITECTURE promised when blood pass 1 shipped — "an additive
    /// third cause plus a proximity query, with NO hard reference from Maniac to
    /// Blood".
    public struct WorldTraceEvent
    {
        public UnityEngine.Vector2 Position;
        /// 0..1-ish. How much was spilled here; a wound leaves more than a drip.
        public float Strength;
    }

    public struct WorldProgressEvent
    {
        /// How many are now done. 1-based: the first completion sends Step 1.
        public int Step;
        /// How many there are in total. 0 means unknown — listeners must guard,
        /// because a scene with no objectives in it is a legitimate state.
        public int Total;
    }
}
