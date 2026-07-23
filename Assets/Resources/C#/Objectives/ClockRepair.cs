// Player-side driver for repairing clocks. Press E near a broken clock to start;
// a marker sweeps a bar, press SPACE when it's in the green zone to add progress.
// A miss costs a little progress and makes a QUIET noise the maniac may hear
// (published as a low-loudness footstep — the hearing radius gates it to nearby).
// The maniac reaching you cancels the repair. The HUD reads the public state to
// draw the bar. Removable: no component -> clocks just sit broken.
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Objectives
{
    [RequireComponent(typeof(PlayerController))]
    public class ClockRepair : MonoBehaviour
    {
        [SerializeField] ClockConfig config;

        PlayerController player;
        TimeKiller.Maniac.ManiacController maniac;
        ClockObjective active;
        float sweepT;

        public bool Repairing => active != null;
        public ClockObjective Active => active;
        public float Marker { get; private set; }       // 0..1 sweep position
        public float ZoneCenter { get; private set; }   // 0..1 target-zone center
        public float ZoneWidth => config != null ? config.zoneWidth : 0.18f;

        public void Init(ClockConfig cfg) => config = cfg;

        void Awake() => player = GetComponent<PlayerController>();

        void Update()
        {
            if (config == null || player == null) return;

            if (active == null)
            {
                if (player.Input.InteractPressed) TryStart();
                return;
            }
            Repair();
        }

        void TryStart()
        {
            var clock = NearestBrokenClock();
            if (clock == null) return;
            active = clock;
            sweepT = 0f;
            NewZone();
        }

        void Repair()
        {
            if (active.IsFixed) { Stop(); return; }
            if (Vector2.Distance(transform.position, active.transform.position) > config.interactRange * 1.35f) { Stop(); return; }
            if (player.Input.InteractPressed) { Stop(); return; }   // E again = walk away
            if (ManiacTooClose()) { Stop(); return; }               // he caught you — run

            sweepT += Time.deltaTime * config.markerSpeed;
            Marker = Mathf.PingPong(sweepT, 1f);

            if (player.Input.SkillCheckPressed)
            {
                bool hit = Mathf.Abs(Marker - ZoneCenter) <= config.zoneWidth * 0.5f;
                if (hit)
                {
                    active.AddProgress(config.progressPerHit);
                    NewZone();
                }
                else
                {
                    active.AddProgress(-config.missPenalty);
                    EventBus.Publish(new PlayerFootstepEvent
                    {
                        Position = transform.position,
                        IsRunning = false,
                        Loudness = config.missNoiseLoudness,
                    });
                }
            }
        }

        void Stop() => active = null;

        void NewZone() => ZoneCenter = Random.Range(0.15f, 0.85f);

        ClockObjective NearestBrokenClock()
        {
            ClockObjective best = null;
            float bestD = config.interactRange;
            foreach (var c in ClockObjective.All)
            {
                if (c == null || c.IsFixed) continue;
                float d = Vector2.Distance(transform.position, c.transform.position);
                if (d <= bestD) { bestD = d; best = c; }
            }
            return best;
        }

        bool ManiacTooClose()
        {
            if (maniac == null) maniac = Object.FindAnyObjectByType<TimeKiller.Maniac.ManiacController>();
            return maniac != null &&
                Vector2.Distance(transform.position, maniac.transform.position) <= config.interruptRange;
        }
    }
}
