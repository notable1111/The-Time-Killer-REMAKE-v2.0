// Blood pass 2: he can read the floor.
//
// The design was approved in July and deliberately left unwired — "the seam is
// already there", and this is that seam being used exactly as specified: a third
// NoiseCause plus a proximity query, with NO hard reference from Maniac to
// Blood. He listens for Core's WorldTraceEvent, which says "the world holds a
// physical trace here". Delete the Blood folder and he compiles and simply never
// finds anything; delete this component and blood goes back to being scenery.
//
// THE FAIRNESS RULE, and it is the whole design: a trail is a LEAD, never a
// detection. All this can do is call ManiacPerception.NoticeTrace, which writes
// the noise channel and nothing else — no awareness, no LastSeenPosition, no
// belief. He gets sent to a spot on the floor, and once there he still has to
// see or hear the player like anyone else. That is the same limit the Director
// operates under, and for the same reason: a hunter who is TOLD where you are
// reads as cheating however the code is written.
//
// Three more limits, each earning its place:
//   - He must nearly walk over it (noticeRadius 2u, with line of sight). The
//     trail therefore matters where he was already going, and bleeding is not a
//     homing beacon. This is the single most important number in the file.
//   - Traces expire, because blood dries and a permanent trail would turn the
//     map into a record of everywhere you have ever been.
//   - He follows the FRESHEST trace within followRadius, so he reads the
//     direction you went — but capped, so he gets the next few steps rather
//     than the destination.
//
// SHIPS DISABLED. See ManiacBloodTrackingConfig's header.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Maniac
{
    [RequireComponent(typeof(ManiacPerception))]
    public class ManiacBloodTracker : MonoBehaviour
    {
        [SerializeField] ManiacBloodTrackingConfig config;

        struct Trace
        {
            public Vector2 Position;
            public float At;       // Time.time it was left
            public bool Alive;
        }

        Trace[] traces;
        int writeIndex;
        ManiacPerception perception;
        float nextScanAt;
        float nextLeadAt;

        /// Live readout for the F1 overlay and for the bot telemetry. "Leads
        /// taken" is the number that answers whether the feature did anything at
        /// all in a run — a trail he never crossed and a feature that is broken
        /// look identical in a position track.
        public int LeadsTaken { get; private set; }
        public int TracesHeld { get; private set; }

        public bool Active => config != null && config.enabled;

        void Awake()
        {
            perception = GetComponent<ManiacPerception>();
            if (config == null)
                config = Resources.Load<ManiacBloodTrackingConfig>(ManiacBloodTrackingConfig.ResourcesPath);
            if (config != null)
                traces = new Trace[Mathf.Max(8, config.maxTraces)];
        }

        // Subscribed even while disabled would be a waste; but the flag is a
        // config value that can be flipped in the Inspector during play, so the
        // cheap thing to skip is the WORK, not the subscription.
        void OnEnable() => EventBus.Subscribe<WorldTraceEvent>(OnTrace);

        void OnDisable() => EventBus.Unsubscribe<WorldTraceEvent>(OnTrace);

        void Start() => DebugOverlay.Watch("BloodTrack", () => !Active
            ? (config == null ? "no config (off)" : "off")
            : $"{TracesHeld} traces  {LeadsTaken} leads  notice {config.noticeRadius:0.0}u");

        void OnDestroy() => DebugOverlay.Unwatch("BloodTrack");

        void OnTrace(WorldTraceEvent evt)
        {
            if (!Active || traces == null) return;
            traces[writeIndex] = new Trace { Position = evt.Position, At = Time.time, Alive = true };
            writeIndex = (writeIndex + 1) % traces.Length;
        }

        void Update()
        {
            if (!Active || traces == null || perception == null) return;
            if (Time.time < nextScanAt) return;
            nextScanAt = Time.time + config.scanInterval;

            Expire();

            // Never while he already has the player. Reading the floor is how he
            // FINDS you; during a chase he can see you, and a lead would only ever
            // drag his attention off the thing he is looking at.
            if (perception.Level != ManiacPerception.AwarenessLevel.Unaware) return;
            if (Time.time < nextLeadAt) return;

            if (!TryFindNoticed(out int noticed)) return;

            // Read the direction of travel: of the traces near the one he found,
            // take the freshest. That is the one closest to where the player was
            // going, which turns a puddle into a trail.
            Vector2 lead = Freshest(traces[noticed].Position, config.followRadius);

            Consume(traces[noticed].Position, config.consumeRadius);
            nextLeadAt = Time.time + config.leadCooldown;
            LeadsTaken++;
            perception.NoticeTrace(lead);
        }

        void Expire()
        {
            int alive = 0;
            float cutoff = Time.time - config.traceLifetime;
            for (int i = 0; i < traces.Length; i++)
            {
                if (!traces[i].Alive) continue;
                if (traces[i].At < cutoff) traces[i].Alive = false;
                else alive++;
            }
            TracesHeld = alive;
        }

        /// The nearest trace he can actually see from where he is standing.
        bool TryFindNoticed(out int index)
        {
            index = -1;
            Vector2 here = transform.position;
            float best = config.noticeRadius;

            for (int i = 0; i < traces.Length; i++)
            {
                if (!traces[i].Alive) continue;
                float d = Vector2.Distance(here, traces[i].Position);
                if (d > best) continue;
                if (config.requireLineOfSight && Blocked(here, traces[i].Position)) continue;
                best = d;
                index = i;
            }
            return index >= 0;
        }

        /// Reuses the maniac's OWN sight mask, so "can he see the floor there"
        /// answers to the same geometry his eyes do. A separate mask would drift
        /// out of sync with the level the first time a wall layer was added.
        bool Blocked(Vector2 from, Vector2 to)
        {
            var cfg = perception.Config;
            if (cfg == null) return false;
            var hit = Physics2D.Linecast(from, to, cfg.sightBlockers);
            return hit.collider != null
                   && !hit.collider.isTrigger
                   && hit.collider.transform.root != transform.root;
        }

        Vector2 Freshest(Vector2 around, float radius)
        {
            Vector2 best = around;
            float newest = float.NegativeInfinity;
            for (int i = 0; i < traces.Length; i++)
            {
                if (!traces[i].Alive) continue;
                if (Vector2.Distance(around, traces[i].Position) > radius) continue;
                if (traces[i].At <= newest) continue;
                newest = traces[i].At;
                best = traces[i].Position;
            }
            return best;
        }

        void Consume(Vector2 around, float radius)
        {
            for (int i = 0; i < traces.Length; i++)
                if (traces[i].Alive && Vector2.Distance(around, traces[i].Position) <= radius)
                    traces[i].Alive = false;
        }
    }
}
