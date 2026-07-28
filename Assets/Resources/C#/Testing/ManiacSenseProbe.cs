// Play-mode probe for the maniac's senses, written for the 2026-07-27 fix pass.
//
// Answers the three questions the fix pass raised that arithmetic alone cannot,
// because they depend on frames actually elapsing:
//   A. When he STOPS, does his sight cone move? ManiacController only writes
//      FacingDirection from velocity, so before this pass a stopped maniac aimed
//      wherever his last step pointed him — his patrol "look around" and his
//      whole Investigate were spent staring at one wall.
//   B. How long is he genuinely exposed to before he spots you? The distance
//      falloff used to reach zero at sightRange, which made the mid range a dead
//      zone. Measured here as real seconds of the Awareness meter filling.
//   C. Does awareness SURVIVE losing contact? It used to drain from the instant
//      cover broke, several times faster than it could ever fill, so one pillar
//      reset him to oblivious.
//
// For B and C the ManiacController is switched off so the states cannot walk him
// out of the test; ManiacPerception.Update runs on its own, which is exactly the
// unit under test. Everything touched is runtime-only state on a scene the user
// hand-fixed, so the probe restores what it changed and deletes itself.
using System.Collections;
using System.Text;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    public class ManiacSenseProbe : MonoBehaviour
    {
        public static string ReportPath =>
            System.IO.Path.Combine(Application.dataPath, "..", "Temp", "maniac_sense_probe.txt");

        ManiacController maniac;
        ManiacPerception perception;
        Transform player;
        Rigidbody2D playerBody;
        readonly StringBuilder report = new StringBuilder();

        public static void Spawn()
        {
            var probe = new GameObject("~ManiacSenseProbe") { hideFlags = HideFlags.DontSave };
            probe.AddComponent<ManiacSenseProbe>();
        }

#if UNITY_EDITOR
        public const string PendingKey = "TimeKiller.ManiacSenseProbe.Pending";

        // Entering play mode reloads the domain and drops any subscription made
        // beforehand, so the request has to be picked up from inside the new one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SpawnIfRequested()
        {
            if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
            UnityEditor.SessionState.SetBool(PendingKey, false);
            Spawn();
        }
#endif

        IEnumerator Start()
        {
            maniac = FindAnyObjectByType<ManiacController>();
            var playerController = FindAnyObjectByType<PlayerController>();
            if (maniac == null || playerController == null)
            {
                Debug.LogError("[ManiacSenseProbe] Need both a maniac and a player in the scene.");
                Destroy(gameObject);
                yield break;
            }
            perception = maniac.Perception;
            player = playerController.transform;
            playerBody = playerController.GetComponent<Rigidbody2D>();

            // Lethal damage freezes timeScale on the lose screen. Every loop below
            // is driven by Time.deltaTime, so a death mid-run does not fail the
            // probe — it hangs it forever. God mode prevents the death; restoring
            // timeScale recovers if one already happened before we spawned.
            var health = FindAnyObjectByType<PlayerHealth>();
            var god = health != null ? health.GetType().GetProperty("GodMode") : null;
            if (god != null) god.SetValue(health, true, null);
            if (Time.timeScale <= 0f) Time.timeScale = 1f;

            yield return new WaitForSecondsRealtime(0.5f);   // let Start/nav settle

            report.AppendLine("MANIAC SENSE PROBE");
            report.AppendLine("==================");
            yield return TestConeSweep();
            yield return TestDetectionAndMemory();
            yield return TestSuspicion();

            var text = report.ToString();
            Debug.Log("[ManiacSenseProbe]\n" + text);
            try { System.IO.File.WriteAllText(ReportPath, text); }
            catch (System.Exception e) { Debug.LogWarning("[ManiacSenseProbe] report write failed: " + e.Message); }
            Destroy(gameObject);
        }

        // --- A. does a STOPPED maniac still move his eyes? ---------------------
        IEnumerator TestConeSweep()
        {
            report.AppendLine("\nA. SIGHT CONE WHILE STOPPED");
            var setNoise = typeof(ManiacPerception).GetMethod("SetNoise",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (setNoise == null || setNoise.GetParameters().Length != 2)
            {
                // Reflection is not compile-checked, so a signature change here is
                // silent until it throws mid-run. Say so instead of dying.
                report.AppendLine("   SetNoise(Vector2, NoiseCause) not found — probe needs updating; skipped");
                yield break;
            }

            // Investigate, with the noise on his own feet: he is "arrived" at once,
            // so he stands still — the exact case whose cone used to freeze. Sound,
            // not Suspicion, so the new hesitation does not confound the sweep.
            setNoise.Invoke(perception, new object[] { maniac.Motor.Position, NoiseCause.Sound });
            maniac.ChangeState(maniac.Investigate);

            float min = 999f, max = -999f, maxSpeed = 0f;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                // Keep the noise fresh so the utility brain leaves him here.
                setNoise.Invoke(perception, new object[] { maniac.Motor.Position, NoiseCause.Sound });
                var f = perception.FacingDirection;
                float ang = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
                min = Mathf.Min(min, ang);
                max = Mathf.Max(max, ang);
                maxSpeed = Mathf.Max(maxSpeed, maniac.Motor.CurrentVelocity.magnitude);
                yield return null;
            }
            float span = max - min;
            report.AppendLine($"   investigating, stationary (peak speed {maxSpeed:F2})");
            report.AppendLine($"   cone swept {span:F0} deg over 2s   ->  {(span > 30f ? "PASS (he looks around)" : "FAIL (frozen cone)")}");
        }

        // --- B + C. time-to-spot, and whether awareness survives cover ---------
        IEnumerator TestDetectionAndMemory()
        {
            // Freeze the AI so nothing walks him out of the measurement; his
            // perception component keeps running, which is what we are measuring.
            maniac.enabled = false;
            maniac.Motor.Stop();
            var body = maniac.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;

            report.AppendLine("\nB. SECONDS OF EXPOSURE BEFORE FULLY SPOTTED");
            Vector2 eye = maniac.Motor.Position;
            Vector2 dir = FindOpenDirection(eye, 5f);
            if (dir == Vector2.zero)
            {
                report.AppendLine("   no 5u sightline free of walls near him — skipped");
                maniac.enabled = true;
                yield break;
            }
            report.AppendLine($"   clear sightline found along {dir}");

            // Torchlight varies from spot to spot, so raw time-to-spot is NOT
            // comparable across distances — it confounds the falloff curve with
            // wherever the level happens to be lit. Sample the exposure too, and
            // rescale each measured rate by the ratio of the old falloff term to
            // the new one. That isolates the single line this pass changed.
            var rateMethod = typeof(ManiacPerception).GetMethod("DetectionRate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var exposureMethod = typeof(ManiacPerception).GetMethod("Exposure",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float range = maniac.Config.sightRange, power = maniac.Config.sightFalloffPower;
            float fill = maniac.Config.awarenessFillRate;

            // TrackPlayerSpeed infers speed from position deltas, so a teleport can
            // register as a sprint and silently apply the RUNNING multiplier (1.6)
            // where "standing still" (0.4) was intended — a 4x error that looks
            // like a real reading. Settle for several frames and print the speed
            // the perception actually believes, so a bad row declares itself.
            var speedField = typeof(ManiacPerception).GetField("playerSpeed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            report.AppendLine("   dist | exposure | player spd |   rate | spotted in | old linear falloff");
            foreach (var dist in new[] { 3f, 4f, 5f, 6f })
            {
                yield return ResetAwareness();
                for (int i = 0; i < 5; i++)
                {
                    Place(eye + dist * dir);
                    perception.FacingDirection = dir;
                    yield return null;
                }

                float seenSpeed = speedField != null ? (float)speedField.GetValue(perception) : -1f;
                float rate = (float)rateMethod.Invoke(perception, null);
                float exposure = (float)exposureMethod.Invoke(perception, new object[] { (Vector2)player.position });

                float newFall = 1f - Mathf.Pow(dist / range, power);
                float oldFall = 1f - dist / range;
                float oldRate = newFall > 0f ? rate * (oldFall / newFall) : 0f;
                string now = rate > 0f ? $"{1f / (rate * fill),6:F1}s" : "  never";
                string then = oldRate > 0f ? $"{1f / (oldRate * fill),6:F1}s" : "  never";
                string flag = seenSpeed > 0.1f ? "  <- NOT STILL, ignore this row" : "";
                report.AppendLine($"   {dist,4:F0}u | {exposure,8:F2} | {seenSpeed,10:F2} | {rate,6:F3} | {now}     | {then}{flag}");
            }
            report.AppendLine("   (standing still, in shadow or torchlight as the level happens to be lit;");
            report.AppendLine("    the last column is the same instant with only the falloff line reverted)");

            report.AppendLine("\nC. AWARENESS AFTER CONTACT BREAKS");
            // Build the meter up, then yank him out of range and watch it.
            yield return ResetAwareness();
            float build = 0f;
            while (build < 15f && perception.Awareness < 1f)
            {
                Place(eye + dir * 3f);
                perception.FacingDirection = dir;
                build += Time.deltaTime;
                yield return null;
            }
            float atBreak = perception.Awareness;
            Place(eye + dir * 200f);          // far out of sightRange: contact lost
            float held = 0f, t2 = 0f;
            bool everDrained = false;
            while (t2 < 5f)
            {
                if (!everDrained && perception.Awareness >= atBreak - 0.001f) held = t2;
                else everDrained = true;
                t2 += Time.deltaTime;
                yield return null;
            }
            report.AppendLine($"   meter at {atBreak:F2} when cover broke");
            report.AppendLine($"   HELD flat for {held:F2}s, then drained to {perception.Awareness:F2} after 5s");
            report.AppendLine($"   -> {(held > 0.8f ? "PASS (he stays onto you)" : "FAIL (forgets instantly)")}");
            report.AppendLine($"   (old behaviour: drain began immediately, full meter gone in 1.25s)");

            maniac.enabled = true;
        }

        // --- D. is "suspicious" actually uncertain, and does he hesitate? ------
        IEnumerator TestSuspicion()
        {
            report.AppendLine("\nD. SUSPICION");
            var awareness = typeof(ManiacPerception).GetField("<Awareness>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (awareness == null) { report.AppendLine("   could not reach the awareness meter — skipped"); yield break; }

            maniac.enabled = false;
            Vector2 eye = maniac.Motor.Position;
            Vector2 dir = FindOpenDirection(eye, 5f);
            if (dir == Vector2.zero) { report.AppendLine("   no clear sightline — skipped"); maniac.enabled = true; yield break; }
            Vector2 stand = eye + dir * 4f;

            report.AppendLine("   his guess vs where the player really is:");
            report.AppendLine("   awareness | guess error | expected");
            foreach (var level in new[] { 0.45f, 0.6f, 0.8f, 0.95f })
            {
                // Drop to Unaware first so each row is a fresh episode.
                awareness.SetValue(perception, 0f);
                Place(eye + dir * 200f);
                yield return null;
                Place(stand);
                perception.FacingDirection = dir;
                awareness.SetValue(perception, level);
                yield return null;

                float err = Vector2.Distance(perception.LastNoisePosition, player.position);
                float expect = Mathf.Lerp(maniac.Config.suspicionGuessError, 0f,
                    Mathf.InverseLerp(maniac.Config.suspicionThreshold, 1f, level));
                report.AppendLine($"   {level,9:F2} | {err,11:F2}u | {expect,5:F2}u" +
                    (err < expect - 0.6f ? "   (clamped — a wall was in the way)" : ""));
            }

            // The hesitation, and the speed he closes at. AI back on; awareness is
            // pinned inside the band each frame so he cannot escalate to Chase.
            report.AppendLine("   the hesitation:");
            awareness.SetValue(perception, 0f);
            Place(eye + dir * 200f);

            // ManiacMotor keeps steering toward its last destination even while the
            // controller is disabled — leftover velocity would read as "he moved
            // immediately" and destroy the measurement. Bring him to a real stop
            // and let the body settle BEFORE the AI is switched back on.
            maniac.Motor.Stop();
            var maniacBody = maniac.GetComponent<Rigidbody2D>();
            if (maniacBody != null) maniacBody.linearVelocity = Vector2.zero;
            for (int i = 0; i < 5; i++) yield return null;
            if (maniac.Motor.CurrentVelocity.magnitude > 0.05f)
                report.AppendLine($"   (warning: still drifting at {maniac.Motor.CurrentVelocity.magnitude:F2} u/s before the test)");

            maniac.enabled = true;
            Place(stand);
            perception.FacingDirection = dir;

            // The clock must start when he BECOMES suspicious, not when the AI is
            // switched on: for the frame before the meter is pushed up he is still
            // patrolling, and that one step would be scored as "no hesitation".
            var smField = typeof(ManiacController).GetField("stateMachine",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float wait = 0f;
            while (wait < 3f && !CurrentStateIs(smField, "InvestigateState"))
            {
                Place(stand);
                awareness.SetValue(perception, 0.5f);
                perception.FacingDirection = dir;
                wait += Time.deltaTime;
                yield return null;
            }
            if (!CurrentStateIs(smField, "InvestigateState"))
            {
                report.AppendLine("   he never entered Investigate — cannot time the hesitation");
                awareness.SetValue(perception, 0f);
                yield break;
            }
            // Clear the momentum he carried in from patrolling, so what we measure
            // is him STARTING to move, not the tail of a previous step.
            maniac.Motor.Stop();
            if (maniacBody != null) maniacBody.linearVelocity = Vector2.zero;

            float firstMove = -1f, topSpeed = 0f, elapsed = 0f;
            while (elapsed < 4f)
            {
                Place(stand);
                awareness.SetValue(perception, 0.5f);      // hold him in the band
                elapsed += Time.deltaTime;
                yield return null;
                float speed = maniac.Motor.CurrentVelocity.magnitude;
                if (firstMove < 0f && speed > 0.25f) firstMove = elapsed;
                if (firstMove > 0f) topSpeed = Mathf.Max(topSpeed, speed);
            }
            report.AppendLine($"   stood still for {(firstMove < 0f ? elapsed : firstMove):F2}s before moving " +
                $"(configured hold {maniac.Config.suspicionHoldSeconds:F2}s)");
            report.AppendLine($"   then closed at {topSpeed:F2} u/s — player walks at 2.20, " +
                $"so retreating {(topSpeed < 2.2f ? "WORKS" : "does NOT work")}");
            report.AppendLine($"   (a plain heard noise still uses investigateSpeed {maniac.Config.investigateSpeed:F2})");

            awareness.SetValue(perception, 0f);
        }

        bool CurrentStateIs(System.Reflection.FieldInfo stateMachineField, string typeName)
        {
            if (stateMachineField == null) return false;
            var sm = stateMachineField.GetValue(maniac);
            var current = sm?.GetType().GetProperty("Current")?.GetValue(sm, null);
            return current != null && current.GetType().Name == typeName;
        }

        IEnumerator ResetAwareness()
        {
            Place(maniac.Motor.Position + Vector2.right * 500f);
            float t = 0f;
            while (t < 3f && perception.Awareness > 0.01f) { t += Time.deltaTime; yield return null; }
        }

        void Place(Vector2 world)
        {
            if (playerBody != null) { playerBody.position = world; playerBody.linearVelocity = Vector2.zero; }
            player.position = world;
        }

        // A direction with no wall between his eye and a point 'dist' away, so the
        // measurement is of the falloff curve rather than of level geometry.
        Vector2 FindOpenDirection(Vector2 eye, float dist)
        {
            var cfg = maniac.Config;
            for (int i = 0; i < 32; i++)
            {
                float a = i * (360f / 32f) * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool blocked = false;
                foreach (var h in Physics2D.LinecastAll(eye, eye + d * dist, cfg.sightBlockers))
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    if (h.collider.transform.root == maniac.transform.root) continue;
                    blocked = true; break;
                }
                if (!blocked) return d;
            }
            return Vector2.zero;
        }
    }
}
