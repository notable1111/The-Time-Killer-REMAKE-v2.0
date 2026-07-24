// Records what actually happened during an automated playtest, via the same
// EventBus the game itself uses — no special hooks in gameplay code.
// TestDriver starts/stops it; Summary() returns a JSON snapshot.
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Maniac;
using TimeKiller.Objectives;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    public class TestTelemetry : MonoBehaviour
    {
        public int Footsteps, Hits, Deaths, Hides, Unhides, Spotted, Heard;
        public string ManiacState = "?";
        public float MinManiacDistance = float.MaxValue;

        /// Run-clock second each clock was completed at. A timed-out run only
        /// tells us "did not finish"; these say WHERE the time went — three
        /// clocks by 0:40 then nothing means the bot could not find the fourth,
        /// while one clock at 3:10 means it could not survive long enough to
        /// work. Same scale as the row's runSeconds (GameFlow's run clock), so
        /// they are directly comparable.
        public readonly List<float> ClockFixTimes = new List<float>();

        float startedAt;

        Transform player, maniac;

        void OnEnable()
        {
            startedAt = Time.time;
            ClockFixTimes.Clear();   // a reused instance must not carry the last run's clocks
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
            EventBus.Subscribe<ClockFixedEvent>(OnClockFixed);
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
            EventBus.Unsubscribe<ClockFixedEvent>(OnClockFixed);
        }

        void OnFootstep(PlayerFootstepEvent e) => Footsteps++;
        void OnHit(PlayerHitEvent e) => Hits++;
        void OnDied(PlayerDiedEvent e) => Deaths++;
        void OnHid(PlayerHidEvent e) => Hides++;
        void OnUnhid(PlayerUnhidEvent e) => Unhides++;
        void OnSpotted(ManiacSpottedPlayerEvent e) => Spotted++;
        void OnHeard(ManiacHeardNoiseEvent e) => Heard++;
        void OnManiacState(ManiacStateChangedEvent e) => ManiacState = e.StateName;

        // GameFlow's clock, not Time.time: batch runs alter timeScale, and a
        // timestamp on a different scale than runSeconds is worse than none.
        void OnClockFixed(ClockFixedEvent e) =>
            ClockFixTimes.Add(GameFlow.Instance != null ? GameFlow.Instance.RunSeconds : Time.time - startedAt);

        /// Also written into the batch result row — see BatchRunner.WriteRow.
        public string ClockFixTimesJson()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("[");
            for (int i = 0; i < ClockFixTimes.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ClockFixTimes[i].ToString("0.0", ci));
            }
            return sb.Append(']').ToString();
        }

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
            sb.Append(",\"clockFixTimes\":").Append(ClockFixTimesJson());
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
