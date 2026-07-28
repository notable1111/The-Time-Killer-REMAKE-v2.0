// The inside of the wardrobe: a dark slat overlay closes in and the world stays
// visible through the door crack. The heartbeat you hear in here belongs to
// PlayerHeartbeat, not to this script — it listens to the same hide events and
// boosts itself while you are hidden. Presentation only, fully removable.
using TimeKiller.Core;
using TimeKiller.Maniac;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Hiding
{
    public class HidingVfx : MonoBehaviour
    {
        [SerializeField] HidingConfig config;
        [SerializeField] CanvasGroup overlay;

        bool hidden;
        Vector2 spotPosition;
        ManiacController maniac;
        float liveDistance; // F1 readout for live tuning

        public void Init(HidingConfig hidingConfig) => config = hidingConfig;

        void Start()
        {
            overlay.alpha = 0f;
            EventBus.Subscribe<PlayerHidEvent>(OnHid);
            EventBus.Subscribe<PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Watch("HideVfx", () => hidden
                ? $"hidden — maniac {liveDistance:0.0}u, slat {overlay.alpha:0.00} (heart: see Heart)"
                : "not hidden");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<PlayerUnhidEvent>(OnUnhid);
        }

        void OnHid(PlayerHidEvent evt) { hidden = true; spotPosition = evt.SpotPosition; }
        void OnUnhid(PlayerUnhidEvent evt) => hidden = false;

        void Update()
        {
            if (config == null || overlay == null) return;

            float step = Time.deltaTime / Mathf.Max(0.05f, config.overlayFade);
            overlay.alpha = Mathf.MoveTowards(overlay.alpha, hidden ? config.overlayAlpha : 0f, step * config.overlayAlpha);

            // The heartbeat used to live here, on its own clock and its own
            // AudioSource. PlayerHeartbeat owns it now — it hears the same hide
            // events and already boosts volume while hidden — so this keeps only
            // the slat overlay. Two hearts beating out of phase was the bug.
            if (!hidden || maniac == null)
            {
                if (maniac == null) maniac = Object.FindAnyObjectByType<ManiacController>();
                return;
            }
            liveDistance = Vector2.Distance(spotPosition, maniac.Motor.Position);
        }
    }
}
