// The inside of the wardrobe: a dark slat overlay closes in (world stays
// visible through the door crack) and your heartbeat gives away how close he
// is — BPM and volume scale with the maniac's distance. Presentation only,
// fully removable.
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
        [SerializeField] AudioSource heartAudio;
        [SerializeField] AudioClip heartbeatClip;

        bool hidden;
        Vector2 spotPosition;
        float pulsePhase;
        int lastBeatIndex = -1;
        ManiacController maniac;
        float liveBpm, liveVolume, liveDistance; // F1 readout for live tuning

        public void Init(HidingConfig hidingConfig) => config = hidingConfig;

        void Start()
        {
            overlay.alpha = 0f;
            EventBus.Subscribe<PlayerHidEvent>(OnHid);
            EventBus.Subscribe<PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Watch("HideVfx", () => hidden
                ? $"dist {liveDistance:0.0}/{(config != null ? config.heartbeatRange : 0f):0.0} bpm {liveBpm:0} vol {liveVolume:0.00} slat {overlay.alpha:0.00}"
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

            if (!hidden || heartAudio == null || heartbeatClip == null) return;

            if (maniac == null)
            {
                maniac = Object.FindAnyObjectByType<ManiacController>();
                if (maniac == null) return;
            }

            // Closer maniac -> faster, louder heart. One clock, thumps on the beat.
            float distance = Vector2.Distance(spotPosition, maniac.Motor.Position);
            float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, config.heartbeatRange));
            float bpm = Mathf.Lerp(config.farBpm, config.nearBpm, closeness);
            float volume = config.heartbeatMaxVolume * Mathf.Max(0.15f, closeness);
            liveBpm = bpm; liveVolume = volume; liveDistance = distance;

            pulsePhase += (bpm / 60f) * Time.deltaTime;
            int beatIndex = Mathf.FloorToInt(pulsePhase + 0.5f);
            if (beatIndex != lastBeatIndex)
            {
                if (lastBeatIndex >= 0) heartAudio.PlayOneShot(heartbeatClip, volume);
                lastBeatIndex = beatIndex;
            }
        }
    }
}
