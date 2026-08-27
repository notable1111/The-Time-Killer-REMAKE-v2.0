// Creates the escalation config, and prints the two comparisons that decide
// whether the feature is still fair.
//
// It touches NO scene. ManiacEscalation is auto-added by ManiacController and
// self-loads this asset from Resources, so there is nothing to place, nothing to
// wire, and nothing that can dirty a hand-tuned .unity file. Deleting the asset
// is the uninstall.
//
// NEVER OVERWRITES an existing config, for the reason Setup/40 does not: this is
// a difficulty dial meant to be tuned by ear in Play Mode, and a setup script
// that resets the numbers destroys the very work it exists to hold.
using System.IO;
using TimeKiller.EditorTools;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class ManiacEscalationSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacEscalationConfig.asset";
        const string ManiacConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";
        const string MovementConfigPath = "Assets/Resources/C#/Player/Configs/PlayerMovementConfig.asset";
        const string WardrobeConfigPath = "Assets/Resources/C#/Maniac/Configs/WardrobeSearchConfig.asset";

        [MenuItem("TimeKiller/Setup/51 - Create Maniac Escalation Config")]
        public static void Run()
        {
            if (SetupGuard.Blocked("51 - Create Maniac Escalation Config")) return;

            var config = AssetDatabase.LoadAssetAtPath<ManiacEscalationConfig>(ConfigPath);
            if (config != null)
            {
                Debug.Log($"[TimeKiller Setup] 51 - Escalation: config already exists at {ConfigPath} — " +
                          "left untouched. Delete the asset by hand to get the defaults back.");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<ManiacEscalationConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[TimeKiller Setup] 51 - Escalation: created {ConfigPath}.");
            }

            Selection.activeObject = config;
            Debug.Log(FairnessReport(config));
        }

        /// The numbers that say whether escalation is still a tension change
        /// rather than a difficulty spike. Printed on every run for the same
        /// reason Setup/45 prints hintError against sightRange: an invariant
        /// nobody re-checks is an invariant that quietly stops holding.
        static string FairnessReport(ManiacEscalationConfig config)
        {
            var maniac = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ManiacConfigPath);
            var movement = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(MovementConfigPath);
            var wardrobe = AssetDatabase.LoadAssetAtPath<WardrobeSearchConfig>(WardrobeConfigPath);

            var report = new System.Text.StringBuilder();
            report.AppendLine("[TimeKiller Setup] 51 - Escalation fairness check");
            report.AppendLine($"  enabled: {config.enabled}   blend: {config.blendSeconds:0.#}s");

            if (maniac == null || movement == null)
            {
                report.AppendLine("  ! ManiacConfig or PlayerMovementConfig not found — speed check skipped.");
            }
            else
            {
                float patrolAtFull = maniac.patrolSpeed * config.moveSpeedAtFull;
                report.AppendLine($"  patrol {maniac.patrolSpeed:0.00} -> {patrolAtFull:0.00} at the last clock, " +
                                  $"player walk {movement.walkSpeed:0.00}, run {movement.runSpeed:0.00}");
                report.AppendLine(patrolAtFull < movement.walkSpeed
                    ? "    OK - you can still WALK away from a patrolling maniac at full escalation."
                    : "    ** FAIL - a fully escalated patrol is faster than the player's walk. Sneaking past " +
                      "him stops being possible, which is a difficulty spike, not tension. Lower moveSpeedAtFull.");
                report.AppendLine($"  hearing {maniac.hearingRadius:0.00}u -> " +
                                  $"{maniac.hearingRadius * config.hearingAtFull:0.00}u   " +
                                  "(sight range is deliberately NOT escalated: " +
                                  $"{maniac.sightRange:0.00}u throughout)");
            }

            if (wardrobe == null)
            {
                report.AppendLine("  ! WardrobeSearchConfig not found — hiding check skipped.");
            }
            else
            {
                // 0.35 is DirectorConfig.maxWardrobeBonus, the cap on what the
                // player can teach him. The worst case is a player who has hidden
                // successfully many times, on the last clock.
                const float directorCap = 0.35f;
                float worst = ManiacEscalation.ChanceWithCeiling(
                    config, wardrobe.checkChance, directorCap + config.wardrobeBonusAtFull);
                report.AppendLine($"  wardrobe chance base {wardrobe.checkChance:0.00} " +
                                  $"+ taught {directorCap:0.00} + escalation {config.wardrobeBonusAtFull:0.00} " +
                                  $"-> {worst:0.00} (ceiling {config.maxWardrobeChance:0.00})");
                report.AppendLine(worst < 0.75f
                    ? "    OK - hiding early and away from his last contact is still worth doing."
                    : "    ** FAIL - he opens the wardrobe three times in four at the end of a run. " +
                      "Hiding stops being a plan and becomes a coin flip. Lower maxWardrobeChance.");
            }

            report.Append("  Set enabled = false, or moveSpeedAtFull/hearingAtFull to 1 and " +
                          "wardrobeBonusAtFull to 0, to get the pre-escalation maniac back exactly.");
            return report.ToString();
        }
    }
}
