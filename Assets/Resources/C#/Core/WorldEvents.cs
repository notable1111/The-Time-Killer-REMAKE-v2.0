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
}
