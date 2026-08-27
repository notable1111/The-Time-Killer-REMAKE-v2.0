// Binds the maniac's two threat beats to their recipes: he sees you, and he
// swings at you.
//
// WHY THIS EXISTS. ARCHITECTURE's own effects audit names this as the biggest
// remaining gap: of the 17 gameplay moments published on the bus, the player
// being hit, the player dying and the two clock beats have visuals — and
// ManiacSpottedPlayerEvent and ManiacAttackEvent are audio-only. In a game about
// hiding and running, being SEEN is the emotional peak of the whole loop, and it
// was carried entirely by a growl and a music sting. A player with the sound
// down did not get told at all.
//
// SELF-INSTALLING, and that is a deliberate departure from PlayerHitEffects and
// ClockEffects, which are scene objects placed by a Setup script. Two reasons:
//   - Catacombs never received the Setup pass that placed ClockEffects, so a
//     scene-object binder silently does nothing in half the game's levels. This
//     one is in every scene, including any added later, with nothing to run.
//   - The scenes here are hand-tuned, protected, and currently shared by three
//     sessions. A feature that needs no scene edit cannot collide with anyone.
// The precedent is AudioMix, which self-loads from Resources for the same
// reason and documents deleting the asset as the uninstall.
//
// REMOVABLE: delete ManiacThreatEffectsConfig.asset and this never installs.
// Delete the script and nothing references it. Leave the recipes empty and it
// installs, counts the beats on the F1 overlay, and plays nothing — which is
// exactly the state it ships in until the art exists.
using TimeKiller.Core;
using TimeKiller.Maniac;
using UnityEngine;

namespace TimeKiller.Effects
{
    public class ManiacThreatEffects : MonoBehaviour
    {
        ManiacThreatEffectsConfig config;

        static ManiacThreatEffects instance;

        /// Counts for the F1 overlay, and the reason this can be verified without
        /// playing the game and trusting your eyes. `Played` vs `Heard` is the
        /// useful pair: the gap between them IS the anti-strobe cooldown doing
        /// its job, so a run where they are equal means the cooldown never
        /// mattered and a run where Played is far lower means it is eating beats.
        public int SpottedHeard { get; private set; }
        public int SpottedPlayed { get; private set; }
        public int AttacksHeard { get; private set; }
        public int AttacksPlayed { get; private set; }

        float nextSpottedAllowed;
        float nextAttackAllowed;

        // Statics survive play-mode restarts when Domain Reload is disabled —
        // the contract GameBootstrap documents and clears itself against.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;

            var config = Resources.Load<ManiacThreatEffectsConfig>(
                ManiacThreatEffectsConfig.ResourcesPath);
            if (config == null) return; // no asset = feature uninstalled, not broken

            var host = new GameObject("[ManiacThreatEffects]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ManiacThreatEffects>();
            instance.config = config;
        }

        void OnEnable()
        {
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Subscribe<ManiacAttackEvent>(OnAttack);
            DebugOverlay.Watch("ThreatVfx", () =>
                $"seen {SpottedPlayed}/{SpottedHeard}  swing {AttacksPlayed}/{AttacksHeard}" +
                (config == null || (config.spotted == null && config.attack == null)
                    ? "  (NO RECIPES — art pending)" : ""));
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Unsubscribe<ManiacAttackEvent>(OnAttack);
            DebugOverlay.Unwatch("ThreatVfx");
        }

        void OnSpotted(ManiacSpottedPlayerEvent evt)
        {
            SpottedHeard++;
            if (config == null || Time.time < nextSpottedAllowed) return;
            nextSpottedAllowed = Time.time + config.spottedCooldown;

            // At the PLAYER, not at him: this beat is about what just happened to
            // you. Where he is standing is information the sound already carries
            // (ManiacVoice attenuates by distance on purpose), and the event does
            // not carry his position anyway — if the art ever wants the flash on
            // HIM, that is a field on ManiacSpottedPlayerEvent, not a lookup here.
            EffectPlayer.Play(config.spotted, evt.PlayerPosition);
            SpottedPlayed++;
        }

        void OnAttack(ManiacAttackEvent evt)
        {
            AttacksHeard++;
            if (config == null || Time.time < nextAttackAllowed) return;
            nextAttackAllowed = Time.time + config.attackCooldown;

            EffectPlayer.Play(config.attack, evt.Position);
            AttacksPlayed++;
        }
    }
}
