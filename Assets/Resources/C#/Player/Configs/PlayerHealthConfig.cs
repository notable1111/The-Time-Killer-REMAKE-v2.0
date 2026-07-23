// Tunables for the 3-point health system (team decision 2026-07-22: 3 hit
// points, no stamina). Asset lives at C#/Player/Configs/PlayerHealthConfig.asset.
using UnityEngine;

namespace TimeKiller.Player
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Player Health", fileName = "PlayerHealthConfig")]
    public class PlayerHealthConfig : ScriptableObject
    {
        [Header("Health")]
        [Tooltip("Hit points. Team decision: 3.")]
        public int maxHealth = 3;

        [Header("Getting hit")]
        [Tooltip("Seconds of invulnerability after a hit — he can't re-hit you during the getaway. Kept close to the adrenaline window so the escape is real.")]
        public float invulnerabilitySeconds = 2.2f;
        [Tooltip("Impulse pushing the player away from the attacker on hit — the instant gap that starts the getaway.")]
        public float shoveImpulse = 11f;
        [Tooltip("Seconds the hit-flash tint stays on the sprite.")]
        public float hitFlashSeconds = 0.15f;

        [Header("Adrenaline (post-hit escape)")]
        [Tooltip("Run-speed factor after a hit. 1.55 × run 4.5 = 6.98 — clearly faster than the maniac's 5.2 chase, enough margin to round a corner and break sight.")]
        public float adrenalineMultiplier = 1.55f;
        [Tooltip("How long the burst lasts. He never stops chasing — this is your window to break away and reach a hiding spot.")]
        public float adrenalineSeconds = 4f;

        [Header("Death")]
        [Tooltip("OFF (design 2026-07-24): losing your last hit point ends the run — GameFlow shows the lose screen and R restarts. " +
                 "Turn ON to go back to the old forgiving rule (respawn at spawn with full health) while testing the map.")]
        public bool respawnOnDeath;
    }
}
