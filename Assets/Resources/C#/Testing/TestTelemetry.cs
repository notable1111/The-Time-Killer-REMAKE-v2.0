// Records what actually happened during an automated playtest, via the same
// EventBus the game itself uses — no special hooks in gameplay code.
// TestDriver starts/stops it; Summary() returns a JSON snapshot.
using System.Globalization;
using System.Text;
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    public class TestTelemetry : MonoBehaviour
    {
        public int Footsteps, Hits, Deaths, Hides, Unhides, Spotted, Heard;
        public string ManiacState = "?";
        public float MinManiacDistance = float.MaxValue;
        float startedAt;

        Transform player, maniac;

        void OnEnable()
        {
            startedAt = Time.time;
            var pc = Object.FindAnyObjectByType<PlayerController>();
            var mc = Object.FindAnyObjectByType<ManiacController>();
            player = pc != null ? pc.transform : null;
            maniac = mc != null ? mc.transform : null;
            EventBus.Subscribe<PlayerFootstepEvent>(OnFootstep);
            EventBus.Subscribe<PlayerHitEvent>(OnHit);
            EventBus.Subscribe<PlayerDiedEvent>(OnDied);
            EventBus.Subscribe<PlayerHidEvent>(OnHid);
            EventBus.Subscribe<PlayerUnhidEvent>(OnUnhid);
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Subscribe<ManiacHeardNoiseEvent>(OnHeard);
            EventBus.Subscribe<ManiacStateChangedEvent>(OnManiacState);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlayerFootstepEvent>(OnFootstep);
            EventBus.Unsubscribe<PlayerHitEvent>(OnHit);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnDied);
            EventBus.Unsubscribe<PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<PlayerUnhidEvent>(OnUnhid);
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Unsubscribe<ManiacHeardNoiseEvent>(OnHeard);
            EventBus.Unsubscribe<ManiacStateChangedEvent>(OnManiacState);
        }

        void OnFootstep(PlayerFootstepEvent e) => Footsteps++;
        void OnHit(PlayerHitEvent e) => Hits++;
        void OnDied(PlayerDiedEvent e) => Deaths++;
        void OnHid(PlayerHidEvent e) => Hides++;
        void OnUnhid(PlayerUnhidEvent e) => Unhides++;
        void OnSpotted(ManiacSpottedPlayerEvent e) => Spotted++;
        void OnHeard(ManiacHeardNoiseEvent e) => Heard++;
        void OnManiacState(ManiacStateChangedEvent e) => ManiacState = e.StateName;

        void Update()
        {
            if (player != null && maniac != null)
                MinManiacDistance = Mathf.Min(MinManiacDistance,
                    Vector2.Distance(player.position, maniac.position));
        }

        public string Summary()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{");
            sb.Append("\"elapsed\":").Append((Time.time - startedAt).ToString("0.0", ci));
            sb.Append(",\"footsteps\":").Append(Footsteps);
            sb.Append(",\"hits\":").Append(Hits);
            sb.Append(",\"deaths\":").Append(Deaths);
            sb.Append(",\"hides\":").Append(Hides);
            sb.Append(",\"unhides\":").Append(Unhides);
            sb.Append(",\"spotted\":").Append(Spotted);
            sb.Append(",\"heardNoise\":").Append(Heard);
            sb.Append(",\"maniacState\":\"").Append(ManiacState).Append('"');
            sb.Append(",\"minManiacDistance\":").Append(
                MinManiacDistance == float.MaxValue ? "null" : MinManiacDistance.ToString("0.00", ci));
            if (player != null)
                sb.Append(",\"playerPos\":[").Append(player.position.x.ToString("0.00", ci))
                  .Append(',').Append(player.position.y.ToString("0.00", ci)).Append(']');
            sb.Append('}');
            return sb.ToString();
        }
    }
}
