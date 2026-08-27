// Menu: TimeKiller/Verify/Escalation Ladder.
//
// WHAT ESCALATION LOOKS LIKE, clock by clock, printed against the player's own
// numbers — so "is he worse now?" is a table rather than an impression.
//
// It exists because escalation is a DIFFICULTY change and difficulty is the one
// thing this project has been burned tuning from a feeling (see CLAUDE.md §3,
// the fear-system retune). The ear test still belongs to the user and nothing
// here replaces it. What this removes is the other half of the problem: nobody
// should have to play four runs to find out what the numbers actually do, and
// the answer should be the same for whoever asks.
//
// Read-only: loads configs through the AssetDatabase, opens nothing, enters no
// play mode, dirties nothing.
using System.IO;
using TimeKiller.EditorTools;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class EscalationLadderProbe
    {
        [MenuItem("TimeKiller/Verify/Escalation Ladder")]
        public static void Run()
        {
            if (SetupGuard.Blocked("Verify - Escalation Ladder")) return;

            var esc = AssetDatabase.LoadAssetAtPath<ManiacEscalationConfig>(
                "Assets/Resources/C#/Maniac/Configs/ManiacEscalationConfig.asset");
            var man = AssetDatabase.LoadAssetAtPath<ManiacConfig>(
                "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset");
            var mov = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(
                "Assets/Resources/C#/Player/Configs/PlayerMovementConfig.asset");
            var steps = AssetDatabase.LoadAssetAtPath<PlayerFootstepConfig>(
                "Assets/Resources/C#/Player/Configs/PlayerFootstepConfig.asset");
            var ward = AssetDatabase.LoadAssetAtPath<WardrobeSearchConfig>(
                "Assets/Resources/C#/Maniac/Configs/WardrobeSearchConfig.asset");
            var dir = AssetDatabase.LoadAssetAtPath<TimeKiller.Director.DirectorConfig>(
                "Assets/Resources/C#/Director/Configs/DirectorConfig.asset");

            if (man == null || mov == null) { Debug.LogError("[Escalation Ladder] ManiacConfig or PlayerMovementConfig missing."); return; }

            var r = new System.Text.StringBuilder();
            r.AppendLine("=== Escalation Ladder ===");
            if (esc == null)
            {
                r.AppendLine("No ManiacEscalationConfig — run TimeKiller/Setup/51.");
                r.AppendLine("He is un-escalated: every row below would read the same.");
            }
            else if (!esc.enabled)
            {
                r.AppendLine("Escalation is DISABLED. The table shows what it WOULD do if switched on.");
            }
            r.AppendLine($"player: walk {mov.walkSpeed:0.00}, run {mov.runSpeed:0.00}");
            r.AppendLine();
            r.AppendLine("clocks |  patrol | search | earshot(run) | earshot(walk) | wardrobe | hint wait");
            r.AppendLine("-------|---------|--------|--------------|---------------|----------|----------");

            const int total = 3;   // the shipped objective count
            for (int step = 0; step <= total; step++)
            {
                float ramp = ManiacEscalation.Ramp(step, total);
                float move = ManiacEscalation.MoveMultiplier(esc, ramp);
                float hear = ManiacEscalation.HearingMultiplier(esc, ramp);
                float bonus = ManiacEscalation.WardrobeBonusFor(esc, ramp);
                float quiet = ManiacEscalation.HintQuietMultiplier(esc, ramp);

                float wardrobe = ward == null ? 0f
                    : ManiacEscalation.ChanceWithCeiling(esc, ward.checkChance, bonus);
                float hintWait = dir == null ? 0f : dir.hintAfterQuietSeconds * quiet;
                float runEar = man.hearingRadius * (steps == null ? 0.85f : steps.runLoudness) * hear;
                float walkEar = man.hearingRadius * (steps == null ? 0.30f : steps.walkLoudness) * hear;

                r.AppendLine($"  {step}/{total}  |  {man.patrolSpeed * move,6:0.00} | {man.searchSpeed * move,6:0.00} " +
                             $"| {runEar,12:0.00} | {walkEar,13:0.00} | {wardrobe,8:0.00} | {hintWait,8:0.0}s");
            }

            r.AppendLine();
            r.AppendLine("What each column has to stay true to:");
            float patrolFull = man.patrolSpeed * ManiacEscalation.MoveMultiplier(esc, 1f);
            r.AppendLine(patrolFull < mov.walkSpeed
                ? $"  OK   patrol tops out at {patrolFull:0.00} < your walk {mov.walkSpeed:0.00} — you can still" +
                  " quietly leave a patrolling maniac at the last clock."
                : $"  FAIL patrol reaches {patrolFull:0.00} >= your walk {mov.walkSpeed:0.00} — keeping distance now" +
                  " needs RUNNING, which is the loud choice. Escalation has deleted the endgame's stealth layer.");

            if (steps != null)
            {
                float walkFull = man.hearingRadius * steps.walkLoudness * ManiacEscalation.HearingMultiplier(esc, 1f);
                float runNow = man.hearingRadius * steps.runLoudness;
                r.AppendLine(walkFull < runNow
                    ? $"  OK   walking at the last clock ({walkFull:0.00}u) still carries less than running does" +
                      $" un-escalated ({runNow:0.00}u) — slowing down remains an answer."
                    : $"  FAIL walking ({walkFull:0.00}u) now carries as far as running used to ({runNow:0.00}u)." +
                      " The late game has no stealth answer left.");
            }

            r.AppendLine("  NOTE sight range is deliberately NOT escalated: " +
                         $"{man.sightRange:0.00}u on every row. How easily he NOTICES a careful player never changes.");
            r.AppendLine("  NOTE chase speed is deliberately NOT escalated: " +
                         $"{man.chaseSpeed:0.00}u/s against your run {mov.runSpeed:0.00} on every row.");
            r.AppendLine();
            r.AppendLine("The ear test is still yours. This says what changed, not whether it feels good.");

            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/escalation_ladder.txt", r.ToString());
            Debug.Log(r.ToString() + "\n(also written to Temp/escalation_ladder.txt)");
        }
    }
}
