// Tunables for the blood trail (design 2026-07-28): you bleed when you are
// hurt, and the castle keeps the evidence.
//
// Only fields that are actually read live here. The maniac-tracking knobs are
// deliberately absent until tracking is built — this project has already paid
// once for config fields that outlived the code reading them.
// Asset: C#/Blood/Configs/BloodConfig.asset.
using UnityEngine;

namespace TimeKiller.Blood
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Blood", fileName = "BloodConfig")]
    public class BloodConfig : ScriptableObject
    {
        [Header("Field")]
        [Tooltip("How many stains exist at once. The oldest is recycled when full, so memory and draw cost are FIXED however long the run goes. One mesh, one draw call, whatever the count.")]
        [Range(32, 512)] public int capacity = 220;
        [Tooltip("Sorting order for the stain mesh. Measured against CastleWingLDtk: floor tilemaps sit at -9..-20 and characters at 0, so -6 puts blood on the floor and under everything that walks on it.")]
        public int sortingOrder = -6;

        [Header("Stain look")]
        [Tooltip("World size of a single stain, before the per-spill amount scales it.")]
        public Vector2 sizeRange = new Vector2(0.28f, 0.72f);
        [Tooltip("Seconds for fresh blood to darken to its dried colour. Drying is what stops an old trail reading as loud as a fresh one.")]
        public float dryingSeconds = 26f;
        [Tooltip("Tint multiplier when fresh (1 = the sprite's own colour).")]
        public Color freshTint = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Tint multiplier once dried — darker and browner, so old blood recedes into the floor.")]
        public Color driedTint = new Color(0.52f, 0.46f, 0.42f, 0.85f);

        [Header("Bleeding from a wound")]
        [Tooltip("At or below this HP you leave a trail. 2 of 3 means any real injury bleeds.")]
        public int bleedAtHp = 2;
        [Tooltip("Seconds between drips while wounded and moving.")]
        public float dripInterval = 0.75f;
        [Tooltip("Minimum world distance between drips, so standing still does not stack a puddle on one spot.")]
        public float dripMinDistance = 0.55f;
        [Tooltip("Stains dropped per drip.")]
        [Range(1, 4)] public int dripStains = 1;
        [Tooltip("Extra drips per second at the LOWEST health, added on top of the base rate — bleeding out gets worse.")]
        public float criticalBleedBonus = 0.9f;

        [Header("The wound itself")]
        [Tooltip("Stains splashed on the floor at the moment of a hit.")]
        [Range(0, 24)] public int stainsPerHit = 7;
        [Tooltip("How far the hit splash scatters from the impact.")]
        public float hitSpread = 0.85f;
        [Tooltip("Hit stains are scaled up by this — a wound marks the floor harder than a drip.")]
        public float hitSizeBoost = 1.35f;
    }
}
