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
        [Tooltip("Seconds of invulnerability after a hit — the escape window.")]
        public float invulnerabilitySeconds = 1.5f;
        [Tooltip("Impulse pushing the player away from the attacker on hit.")]
        public float shoveImpulse = 8f;
        [Tooltip("Seconds the hit-flash tint stays on the sprite.")]
        public float hitFlashSeconds = 0.15f;

        [Header("Death (v1 placeholder rule)")]
        [Tooltip("Until the save/death design exists: respawn at the spawn point with full health.")]
        public bool respawnOnDeath = true;
    }
}
