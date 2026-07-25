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
        public int HeardSound, HeardSuspicion;   // Heard, split by NoiseCause
        public string ManiacState = "?";
        public float MinManiacDistance = float.MaxValue;

        /// Run-clock second each clock was completed at. A timed-out run only
        /// tells us "did not finish"; these say WHERE the time went — three
        /// clocks by 0:40 then nothing means the bot could not find the fourth,
        /// while one clock at 3:10 means it could not survive long enough to
        /// work. Same scale as the row's runSeconds (GameFlow's run clock), so
        /// they are directly comparable.
        public readonly List<float> ClockFixTimes = new List<float>();

        /// Where each hide happened. `hides` alone says a bot hid nine times; it
        /// cannot say whether that was one wardrobe nine times or nine wardrobes
        /// once, and those are opposite level-design verdicts. The position is
        /// what PlayerHidEvent already carries and what the old code threw away
        /// on the way to a counter.
        public readonly List<Vector2> HideSpots = new List<Vector2>();

        float startedAt;

        Transform player, maniac;
        ManiacPerception perception;

        // ---- tension trace ---------------------------------------------------
        // Win rate cannot tell a horror game from a chore: two builds with the
        // same escape rate can be one long quiet walk or five heart-stopping
        // near misses. These measure the SHAPE of a run rather than its outcome.
        //
        // Sampled in FixedUpdate, never Update. Physics is fixed-step in GAME
        // time, so a 6x batch still yields the same samples per game-second; an
        // Update-driven sampler would quietly coarsen at speed and the report
        // would blame the level for the accelerator's fault. The underlying
        // signal still degrades at speed (ManiacPerception ticks on Update), so
        // analyze.py gives these their OWN matched-seed speed verdict instead
        // of trusting them — they are more speed-sensitive than win rate, not
        // less.

        // These thresholds are NOT free parameters. Each is a claim about the
        // maniac's senses, and his senses live in ManiacConfig — so they are
        // DERIVED from it at run start instead of typed here. The first cut
        // hard-coded 4 / 12 / 0.25 beside a config reading sightRange 7,
        // hearingRadius 9, suspicionThreshold 0.4, and two of the three were
        // wrong in ways no amount of batch data could have revealed:
        //
        //   FeltRadius 12 sat THREE UNITS BEYOND his maximum sensory reach, so
        //   the 9-12 band was neither "felt" nor "dead air" — a limbo that
        //   undercounted the one boredom metric this class exists to expose.
        //
        //   AwareThreshold 0.25 sat BELOW the game's own suspicionThreshold, so
        //   "dread" spanned a band in which he does nothing observable at all.
        //
        // Deriving them also ends the silent drift the day he is retuned: a
        // copied constant does not follow, and nothing anywhere would fail.
        public float NearMissRadius { get; private set; } = 4f;
        public float FeltRadius { get; private set; } = 12f;
        float awareThreshold = 0.4f;

        /// Recorded only so the report can state fadeSeconds' structural
        /// ceiling — (1 - awareThreshold) / drainRate per lost contact. Without
        /// it, a reader has no way to tell a tuning artefact from a finding.
        float drainRate = 0.8f;

        /// "He is in the room with you" has to scale with how far he can SEE —
        /// an absolute metre count means a different thing on every tuning.
        /// 0.57 reproduces the original 4m at the current sightRange of 7.
        const float NearMissSightFraction = 0.57f;

        /// Deliberately NOT derived: this marks "the meter is empty", which is a
        /// property of a 0..1 meter, not a claim about his senses.
        const float CalmThreshold = 0.05f;

        const float SampleInterval = 0.25f;  // game seconds
        const int Buckets = 12;

        public int NearMissHidden, NearMissOpen, NearMissClipped, ChaseEpisodes, Samples;
        public float ChaseSeconds, LongestChase, DreadSeconds, LongestDeadAir;

        readonly List<float> threat = new List<float>();
        float lastSampleAt, nextSampleAt, currentChase, currentDeadAir;
        bool inNearMiss, nearMissEndedHidden;
        int nearMissStartHits;

        // Suspicion is accumulated by ManiacPerception itself (see StalkSeconds
        // there — the band is shorter than one sample at this rate). Baselined
        // on acquire so a maniac that survived the last run cannot donate it.
        float stalkBase, fadeBase;
        int suspicionEpisodeBase;
        bool suspicionBaselined;

        void OnEnable()
        {
            startedAt = Time.time;
            ClockFixTimes.Clear();   // a reused instance must not carry the last run's clocks
            HideSpots.Clear();
            // Was never reset. A reused instance carried the closest approach of
            // the PREVIOUS run forward, and since the field only ever moves down,
            // one terrifying run would quietly stamp its 0.17 on every run after
            // it — the field cannot recover from the carry-over by construction.
            MinManiacDistance = float.MaxValue;
            var pc = Object.FindAnyObjectByType<PlayerController>();
            var mc = Object.FindAnyObjectByType<ManiacController>();
            player = pc != null ? pc.transform : null;
            maniac = mc != null ? mc.transform : null;
            perception = mc != null ? mc.Perception : null;
            DeriveThresholds(mc != null ? mc.Config : null);
            ResetTension();   // a reused instance must not carry the last run's trace
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
        void OnHid(PlayerHidEvent e) { Hides++; HideSpots.Add(e.SpotPosition); }
        void OnUnhid(PlayerUnhidEvent e) => Unhides++;
        void OnSpotted(ManiacSpottedPlayerEvent e) => Spotted++;

        // Both causes still bump the legacy total so older comparisons hold,
        // but they are also kept apart: "heard 50 noises" was one number
        // covering the player being audible AND his own sight meter twitching,
        // which are different phenomena with different fixes.
        void OnHeard(ManiacHeardNoiseEvent e)
        {
            Heard++;
            if (e.Cause == NoiseCause.Suspicion) HeardSuspicion++;
            else HeardSound++;
        }
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

        /// The run's closest approach, in JSON-ready form.
        public string HideSpotsJson()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("[");
            for (int i = 0; i < HideSpots.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(HideSpots[i].x.ToString("0.0", ci))
                  .Append(',').Append(HideSpots[i].y.ToString("0.0", ci)).Append(']');
            }
            return sb.Append(']').ToString();
        }

        /// Read the three sensory thresholds off the maniac rather than assuming
        /// them. Silently keeps the previous defaults if he has no config yet —
        /// the alternative is dividing by zero and reporting a flat curve.
        void DeriveThresholds(ManiacConfig config)
        {
            if (config == null) return;
            // Beyond his own maximum reach he cannot sense the player by ANY
            // channel, so this is exactly where he stops existing for the run.
            FeltRadius = Mathf.Max(config.sightRange, config.hearingRadius);
            NearMissRadius = config.sightRange * NearMissSightFraction;
            awareThreshold = config.suspicionThreshold;
            drainRate = config.awarenessDrainRate;
        }

        void ResetTension()
        {
            NearMissHidden = NearMissOpen = NearMissClipped = ChaseEpisodes = Samples = 0;
            ChaseSeconds = LongestChase = DreadSeconds = LongestDeadAir = 0f;
            threat.Clear();
            lastSampleAt = nextSampleAt = currentChase = currentDeadAir = 0f;
            inNearMiss = nearMissEndedHidden = false;
            nearMissStartHits = 0;
            stalkBase = fadeBase = 0f;
            suspicionEpisodeBase = 0;
            suspicionBaselined = false;
        }

        void FixedUpdate()
        {
            if (player == null || maniac == null) return;

            // Perception is wired in ManiacController.Awake, which may land
            // AFTER this component's OnEnable. Re-acquire until found — the
            // alternative is a whole batch of runs reporting awareness 0 and
            // therefore zero near misses, which looks like a calm game rather
            // than a broken instrument.
            if (perception == null)
            {
                var mc = Object.FindAnyObjectByType<ManiacController>();
                perception = mc != null ? mc.Perception : null;
                if (perception == null) return;
                DeriveThresholds(mc.Config);   // OnEnable may have run before he existed
            }

            // Baseline on first sight of him, not in ResetTension: he may be
            // spawned after this component enables, and zeroing against a
            // counter we have not read yet would credit this run with his last.
            if (!suspicionBaselined)
            {
                stalkBase = perception.StalkSeconds;
                fadeBase = perception.FadeSeconds;
                suspicionEpisodeBase = perception.SuspicionEpisodes;
                suspicionBaselined = true;
            }

            // Closest approach is sampled EVERY fixed step, not on the render
            // frame and not at 4 Hz. On Update it was taken 4x less often per
            // game-second at 4x and so biased UPWARD exactly where the danger is
            // worst — a metric whose error grew with the accelerator it was
            // partly there to police. 50 Hz of game time costs one distance call.
            MinManiacDistance = Mathf.Min(MinManiacDistance,
                Vector2.Distance(player.position, maniac.position));

            float now = Time.time;
            if (now < nextSampleAt) return;

            // Elapsed is MEASURED, never assumed to be SampleInterval: at high
            // timeScale Unity drops fixed steps, and taking the nominal value
            // would inflate every duration this class reports.
            float dt = Samples == 0 ? SampleInterval : Mathf.Min(now - lastSampleAt, 1f);
            lastSampleAt = now;
            // Advance the SCHEDULE, not "now + interval". The old form re-based on
            // the overshoot every time, so the deadline drifted by half a fixed
            // step per sample and the achieved rate sat at a permanent ~96% at 4x
            // — indistinguishable, in the health check, from real dropped steps.
            // The clamp stops it chasing a backlog after a genuine stall.
            nextSampleAt += SampleInterval;
            if (nextSampleAt < now) nextSampleAt = now + SampleInterval;
            Samples++;

            float dist = Vector2.Distance(player.position, maniac.position);
            float awareness = perception.Awareness;
            bool chasing = ManiacState == nameof(ChaseState) || ManiacState == nameof(AttackState);

            // Chase episodes: contiguous stretches of pursuit. One 40-second
            // chase and eight 5-second ones are very different games.
            if (chasing)
            {
                ChaseSeconds += dt;
                currentChase += dt;
                if (currentChase > LongestChase) LongestChase = currentChase;
            }
            else if (currentChase > 0f)
            {
                ChaseEpisodes++;
                currentChase = 0f;
            }

            // Dread: he is onto something without having locked on. Kept for
            // continuity with earlier batches, but READ THE STALK/FADE SPLIT
            // INSTEAD — this number is sampled at 4 Hz and the suspicion band is
            // routinely shorter than one sample, so it aliases badly. Worse, it
            // is dominated by the post-spot drain tail, whose length is fixed by
            // awarenessDrainRate rather than by anything the level does.
            if (awareness >= awareThreshold && !perception.CanSeePlayer)
                DreadSeconds += dt;

            // Dead air: far away and calm. The boredom metric — a long stretch
            // here is the failure mode win rate structurally cannot see.
            if (awareness < CalmThreshold && dist > FeltRadius)
            {
                currentDeadAir += dt;
                if (currentDeadAir > LongestDeadAir) LongestDeadAir = currentDeadAir;
            }
            else currentDeadAir = 0f;

            // Near miss: close AND aware, resolved without a hit. Awareness is
            // required deliberately — him patrolling past your wardrobe unaware
            // is not a near miss, because you would never have known about it.
            if (dist <= NearMissRadius && awareness >= awareThreshold)
            {
                if (!inNearMiss)
                {
                    inNearMiss = true;
                    nearMissStartHits = Hits;
                }
                // How it ENDED, not how most of it looked. Diving into a
                // wardrobe as he rounds the corner is the whole beat, and the
                // old "hidden for >=50% of samples" rule scored exactly that as
                // OPEN, because the first half was spent in the open.
                nearMissEndedHidden = perception.PlayerHidden;
            }
            else if (inNearMiss) CloseNearMiss();

            // Unitless by construction. Only the SHAPE of the curve and
            // comparisons between builds are readable — the absolute number
            // means nothing and must never be tuned against directly.
            //
            // Awareness is normalised against the suspicion threshold: below it
            // he is doing nothing, so that meter movement is not "a little
            // tension", it is none. The weights then favour PROXIMITY, which is
            // the term that actually varies — the old 0.6 awareness share was
            // dead weight outside chases, capping a non-chase sample at 0.40 and
            // leaving the curve's whole top half unreachable.
            float alarm = awareness <= awareThreshold
                ? 0f
                : (awareness - awareThreshold) / (1f - awareThreshold);
            float proximity = Mathf.Clamp01((FeltRadius - dist) / FeltRadius);
            float t = Mathf.Clamp01(0.35f * alarm + 0.65f * proximity);
            threat.Add(chasing ? Mathf.Max(t, 0.75f) : t);
        }

        /// Three outcomes, counted apart because they are three different
        /// feelings: holding your breath in a wardrobe, being run down in the
        /// open and getting away, and being clipped and living anyway.
        ///
        /// Clipped used to be DISCARDED — any hit voided the whole episode. On
        /// the first traced batch that filter ate most of the encounters in the
        /// file (13 hits against 4 surviving near misses), which is the wrong
        /// call twice over: taking a hit and escaping is the classic horror
        /// beat, and throwing it away made a violent run look uneventful.
        void CloseNearMiss()
        {
            inNearMiss = false;
            if (Hits > nearMissStartHits) NearMissClipped++;
            else if (nearMissEndedHidden) NearMissHidden++;
            else NearMissOpen++;
        }

        /// Also written into the batch result row — see BatchRunner.WriteRow.
        public string TensionJson()
        {
            // A run that ended mid-encounter still resolves it — but WITHOUT
            // mutating. TestDriver calls Summary() repeatedly during a live
            // session, and closing the episode here would end it early and then
            // count its remainder again as a second, fictional near miss.
            int hidden = NearMissHidden, open = NearMissOpen, clipped = NearMissClipped;
            if (inNearMiss)
            {
                if (Hits > nearMissStartHits) clipped++;
                else if (nearMissEndedHidden) hidden++;
                else open++;
            }

            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{");
            sb.Append("\"nearMissHidden\":").Append(hidden);
            sb.Append(",\"nearMissOpen\":").Append(open);
            sb.Append(",\"nearMissClipped\":").Append(clipped);

            // The suspicion split, read straight off ManiacPerception's own
            // per-frame counters. stalkSeconds is the stalking beat and responds
            // to level design; fadeSeconds is the forgetting tail and responds
            // only to awarenessDrainRate. Reported apart so the second can never
            // be mistaken for the first, which is exactly what dreadSeconds did.
            if (perception != null && suspicionBaselined)
            {
                sb.Append(",\"stalkSeconds\":")
                  .Append((perception.StalkSeconds - stalkBase).ToString("0.0", ci));
                sb.Append(",\"fadeSeconds\":")
                  .Append((perception.FadeSeconds - fadeBase).ToString("0.0", ci));
                sb.Append(",\"suspicionEpisodes\":")
                  .Append(perception.SuspicionEpisodes - suspicionEpisodeBase);
            }

            // The thresholds this trace was measured with. Without them a file
            // cannot be compared against one recorded under a different maniac
            // tuning — and now that they are derived, that WILL happen silently.
            sb.Append(",\"nearMissRadius\":").Append(NearMissRadius.ToString("0.0", ci));
            sb.Append(",\"feltRadius\":").Append(FeltRadius.ToString("0.0", ci));
            sb.Append(",\"awareThreshold\":").Append(awareThreshold.ToString("0.00", ci));
            sb.Append(",\"drainRate\":").Append(drainRate.ToString("0.00", ci));
            sb.Append(",\"chaseSeconds\":").Append(ChaseSeconds.ToString("0.0", ci));
            sb.Append(",\"longestChase\":").Append(LongestChase.ToString("0.0", ci));
            sb.Append(",\"chaseEpisodes\":").Append(ChaseEpisodes + (currentChase > 0f ? 1 : 0));
            sb.Append(",\"dreadSeconds\":").Append(DreadSeconds.ToString("0.0", ci));
            sb.Append(",\"longestDeadAir\":").Append(LongestDeadAir.ToString("0.0", ci));
            // Sample count is the instrument's own health: analyze.py divides it
            // by run length and complains if the achieved rate fell short of
            // 1/SampleInterval, which is how dropped fixed steps announce
            // themselves instead of silently shortening every duration above.
            sb.Append(",\"samples\":").Append(Samples);
            sb.Append(",\"curve\":").Append(CurveJson());
            return sb.Append('}').ToString();
        }

        /// The run compressed to Buckets equal-length slices. Equal COUNT is
        /// equal TIME because sampling is uniform in game seconds, so curves
        /// from runs of different lengths stay directly comparable.
        string CurveJson()
        {
            if (threat.Count == 0) return "[]";
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("[");
            for (int b = 0; b < Buckets; b++)
            {
                int from = threat.Count * b / Buckets;
                int to = threat.Count * (b + 1) / Buckets;
                if (to <= from) to = Mathf.Min(from + 1, threat.Count);
                float sum = 0f;
                for (int i = from; i < to; i++) sum += threat[i];
                if (b > 0) sb.Append(',');
                sb.Append((sum / (to - from)).ToString("0.00", ci));
            }
            return sb.Append(']').ToString();
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
            sb.Append(",\"heardSound\":").Append(HeardSound);
            sb.Append(",\"heardSuspicion\":").Append(HeardSuspicion);
            sb.Append(",\"maniacState\":\"").Append(ManiacState).Append('"');
            sb.Append(",\"clockFixTimes\":").Append(ClockFixTimesJson());
            sb.Append(",\"hideSpots\":").Append(HideSpotsJson());
            sb.Append(",\"minManiacDistance\":").Append(
                MinManiacDistance == float.MaxValue ? "null" : MinManiacDistance.ToString("0.00", ci));
            sb.Append(",\"tension\":").Append(TensionJson());
            if (player != null)
                sb.Append(",\"playerPos\":[").Append(player.position.x.ToString("0.00", ci))
                  .Append(',').Append(player.position.y.ToString("0.00", ci)).Append(']');
            sb.Append('}');
            return sb.ToString();
        }
    }
}
