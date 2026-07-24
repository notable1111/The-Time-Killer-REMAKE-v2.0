// TestDriver — the static cockpit for automated playtests, called by the
// unity-mcp bridge (execute_code) while the game runs in Play Mode:
//   TestDriver.Possess();            // scripted controller takes over (keyboard suspended)
//   TestDriver.MoveTo(33f, -5f, run: true);
//   TestDriver.PressInteract();      // one E tap (hide/unhide)
//   TestDriver.Status();             // JSON: position, state, arrival, telemetry
//   TestDriver.Release();            // keyboard control returns
// All methods are safe to call in any order; each returns a short JSON string
// so the bridge gets structured results, never silence.
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    public static class TestDriver
    {
        static ScriptedInputSource scripted;
        static TestTelemetry telemetry;

        public static string Possess()
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc == null) return "{\"error\":\"no PlayerController (is Play Mode running?)\"}";

            scripted = pc.GetComponent<ScriptedInputSource>();
            if (scripted == null) scripted = pc.gameObject.AddComponent<ScriptedInputSource>();
            scripted.enabled = true;

            var keyboard = pc.GetComponent<KeyboardInputSource>();
            if (keyboard != null) keyboard.enabled = false; // suspend, not destroy

            pc.SetInputSource(scripted);

            telemetry = pc.GetComponent<TestTelemetry>();
            if (telemetry == null) telemetry = pc.gameObject.AddComponent<TestTelemetry>();
            telemetry.enabled = true;

            return "{\"possessed\":true}";
        }

        public static string Release()
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc == null) return "{\"error\":\"no PlayerController\"}";
            var keyboard = pc.GetComponent<KeyboardInputSource>();
            if (keyboard != null) keyboard.enabled = true;
            if (scripted != null) { scripted.Target = null; scripted.enabled = false; }
            pc.SetInputSource(keyboard);
            return "{\"released\":true}";
        }

        public static string MoveTo(float x, float y, bool run = false)
        {
            if (scripted == null || !scripted.enabled) return "{\"error\":\"not possessed\"}";
            scripted.Target = new Vector2(x, y);
            scripted.Run = run;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return $"{{\"movingTo\":[{x.ToString("0.00", ci)},{y.ToString("0.00", ci)}],\"run\":{(run ? "true" : "false")}}}";
        }

        public static string Stop()
        {
            if (scripted == null) return "{\"error\":\"not possessed\"}";
            scripted.Target = null;
            scripted.Run = false;
            return "{\"stopped\":true}";
        }

        public static string PressInteract()
        {
            if (scripted == null || !scripted.enabled) return "{\"error\":\"not possessed\"}";
            scripted.QueueInteract();
            return "{\"interactQueued\":true}";
        }

        /// One Space tap — the clock-repair skill check. Without this the driver
        /// could walk to a clock and start it but never finish one, so no
        /// automated run could ever reach the win condition.
        public static string PressSkillCheck()
        {
            if (scripted == null || !scripted.enabled) return "{\"error\":\"not possessed\"}";
            scripted.QueueSkillCheck();
            return "{\"skillCheckQueued\":true}";
        }

        /// Hand the possessed player over to the autonomous pilot. profile is a
        /// BotProfileConfig asset name under Resources (e.g. "Bot_average").
        public static string BotStart(string profile = "Bot_average", int seed = 1)
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc == null) return "{\"error\":\"no PlayerController (is Play Mode running?)\"}";
            if (scripted == null || !scripted.enabled) Possess();

            var config = Resources.Load<BotProfileConfig>("C#/Testing/Configs/" + profile);
            if (config == null) return $"{{\"error\":\"profile '{profile}' not found — run TimeKiller/Setup/33\"}}";

            var pilot = pc.GetComponent<BotPilot>();
            if (pilot == null) pilot = pc.gameObject.AddComponent<BotPilot>();
            pilot.enabled = true;
            pilot.Begin(config, seed);
            return $"{{\"bot\":\"{config.profileName}\",\"seed\":{seed},\"knowsEverything\":{(config.knowsEverything ? "true" : "false")}}}";
        }

        public static string BotStop()
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            var pilot = pc != null ? pc.GetComponent<BotPilot>() : null;
            if (pilot != null) Object.Destroy(pilot);
            if (scripted != null) { scripted.Target = null; scripted.Run = false; }
            return "{\"botStopped\":true}";
        }

        public static string Status()
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc == null) return "{\"error\":\"no PlayerController\"}";
            var pos = pc.transform.position;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string arrived = scripted != null && scripted.Arrived ? "true" : "false";
            string tele = telemetry != null ? telemetry.Summary() : "null";
            var pilot = pc.GetComponent<BotPilot>();
            string bot = pilot != null && pilot.Ready
                ? $"{{\"goal\":\"{pilot.CurrentGoal}\",\"clocksKnown\":{pilot.Memory.ClocksKnown},\"explored\":\"{pilot.Memory.ExploredCount}/{pilot.Memory.ExplorableCount}\",\"skillChecks\":\"{pilot.SkillChecksHit}/{pilot.SkillChecksAttempted}\"}}"
                : "null";
            return $"{{\"pos\":[{pos.x.ToString("0.00", ci)},{pos.y.ToString("0.00", ci)}],\"arrived\":{arrived},\"bot\":{bot},\"telemetry\":{tele}}}";
        }
    }
}
