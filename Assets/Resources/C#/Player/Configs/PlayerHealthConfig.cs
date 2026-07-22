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

        [Header("Adrenaline (post-hit escape)")]
        [Tooltip("Run-speed factor after a hit. 1.3 × run 4.5 = 5.85 — FASTER than the maniac's 5.2 chase, so you can break away while he keeps chasing.")]
        public float adrenalineMultiplier = 1.3f;
        [Tooltip("How long the burst lasts. He never stops chasing — this is your window.")]
        public float adrenalineSeconds = 2.5f;

        [Header("Death (v1 placeholder rule)")]
        [Tooltip("Until the save/death design exists: respawn at the spawn point with full health.")]
        public bool respawnOnDeath = true;
    }
}
