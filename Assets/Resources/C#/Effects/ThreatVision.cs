// Turns the maniac's two threat beats into hits on the player's VISION.
//
// ManiacSpottedPlayerEvent  -> colour drains, the frame's edges clamp in, a brief
//                              lens tear. Long release: being seen is a state you
//                              are now in, not an event that happened.
// ManiacAttackEvent         -> a harder, faster, shorter version. Contact, not
//                              attention.
//
// WHY NOT A SPRITE. Both beats used to draw one, and the user rejected them on
// 2026-08-28: "looks like cheap, not feeling dangerous, not feels like the horror
// game effect". Outlast and Amnesia change what the player can SEE — obscuration
// and distortion — rather than adding a decal to the world; Dead by Daylight keeps
// threat feedback on the frame and on the survivor. A decoration on the floor is
// something a player can ignore; a frame that drains and closes in is not.
//
// IT DRAWS NOTHING ITSELF. It pushes into HealthVfxDirector, which is the one and
// only writer to the URP Volume. Two components driving one vignette is the exact
// bug that made the heartbeat read as mush the day before this was written.
//
// SELF-INSTALLING, for the same two reasons ManiacThreatEffects is: it then works
// in every scene including any added later, and it needs no scene edit, so it
// cannot collide with the other sessions sharing this Editor.
//
// REMOVABLE: delete ThreatVisionConfig.asset, or untick its `enabled`, and this
// never installs. Delete this file and nothing references it.
using TimeKiller.Core;
using TimeKiller.Maniac;
using UnityEngine;

namespace TimeKiller.Effects
{
    public class ThreatVision : MonoBehaviour
    {
        ThreatVisionConfig config;
        static ThreatVision instance;

        /// Counts for the F1 overlay, so this can be verified without playing the
        /// game and trusting your eyes — the same contract every other effect
        /// binder here follows.
        public int SpottedShocks { get; private set; }
        public int SwingShocks { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;

            var config = Resources.Load<ThreatVisionConfig>(ThreatVisionConfig.ResourcesPath);
            if (config == null || !config.enabled) return;   // absent or off = uninstalled, not broken

            var host = new GameObject("[ThreatVision]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ThreatVision>();
            instance.config = config;
        }

        void OnEnable()
        {
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Subscribe<ManiacAttackEvent>(OnAttack);
            DebugOverlay.Watch("ThreatVision", () => $"seen {SpottedShocks}  swing {SwingShocks}");
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Unsubscribe<ManiacAttackEvent>(OnAttack);
            DebugOverlay.Unwatch("ThreatVision");
        }

        // No cooldown here on purpose. ManiacThreatEffects already rate-limits
        // these two events (4s spotted, 0.3s attack) before they reach anything
        // visual, and PushVisionShock lets a louder shock override a quieter one
        // rather than summing — so a burst of beats cannot black the screen out.
        void OnSpotted(ManiacSpottedPlayerEvent evt)
        {
            SpottedShocks++;
            TimeKiller.HealthVfx.HealthVfxDirector.PushVisionShock(
                config.spottedVignette, config.spottedDesaturation, config.spottedChromatic,
                config.spottedAttack, config.spottedRelease);
        }

        void OnAttack(ManiacAttackEvent evt)
        {
            SwingShocks++;
            TimeKiller.HealthVfx.HealthVfxDirector.PushVisionShock(
                config.swingVignette, config.swingDesaturation, config.swingChromatic,
                config.swingAttack, config.swingRelease);
        }
    }
}
