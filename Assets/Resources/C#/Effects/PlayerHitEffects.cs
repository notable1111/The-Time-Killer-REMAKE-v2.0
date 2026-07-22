// Binds the hit/death moments to their recipes: PlayerHitEvent -> hit recipe
// at the player's position, PlayerDiedEvent -> death recipe. Impact juice
// (world blood burst + splash SFX + shake) lives HERE via EffectPlayer;
// HealthVfxDirector keeps only the persistent screen-state presentation.
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Effects
{
    public class PlayerHitEffects : MonoBehaviour
    {
        [SerializeField] EffectRecipe hitRecipe;
        [SerializeField] EffectRecipe deathRecipe;
        [SerializeField] EffectRecipe deathSoulRecipe;   // layered: the soul escapes the body

        Transform player;

        void OnEnable()
        {
            EventBus.Subscribe<PlayerHitEvent>(OnHit);
            EventBus.Subscribe<PlayerDiedEvent>(OnDied);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlayerHitEvent>(OnHit);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnDied);
        }

        Vector2 PlayerPosition()
        {
            if (player == null)
            {
                var controller = Object.FindAnyObjectByType<PlayerController>();
                if (controller != null) player = controller.transform;
            }
            return player != null ? (Vector2)player.position : Vector2.zero;
        }

        void OnHit(PlayerHitEvent evt) => EffectPlayer.Play(hitRecipe, PlayerPosition());
        void OnDied(PlayerDiedEvent evt) => EffectPlayer.Play(deathRecipe, evt.Position);
    }
}
