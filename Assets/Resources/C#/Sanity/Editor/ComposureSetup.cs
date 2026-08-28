// Menu: TimeKiller/Setup/58 - Create Composure Config (sanity).
//
// 58 and not 57: the sound lane claimed 57 (and 57b) for its self-installing
// audio while this was being written. That is the THIRD Setup-number collision
// today - Unity accepts duplicates because the full menu strings differ, which
// is exactly what makes them dangerous, since two lanes then say "run Setup/57"
// and mean different scripts. Check the range before claiming a number.
//
// Touches no scene. PlayerComposure self-installs from Resources, so there is
// nothing to place and nothing that can dirty a hand-tuned .unity file. Deleting
// the asset is the uninstall.
//
// NEVER OVERWRITES an existing config: this is a difficulty dial meant to be
// tuned by ear in Play Mode, and a setup script that reset the numbers would
// destroy the work it exists to hold.
using System.IO;
using TimeKiller.EditorTools;
using TimeKiller.Sanity;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Sanity.EditorTools
{
    public static class ComposureSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Sanity/Configs/ComposureConfig.asset";
        const string FootstepPath = "Assets/Resources/C#/Player/Configs/PlayerFootstepConfig.asset";
        const string ManiacPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";

        [MenuItem("TimeKiller/Setup/58 - Create Composure Config (sanity)")]
        public static void Run()
        {
            if (SetupGuard.Blocked("58 - Create Composure Config (sanity)")) return;

            var config = AssetDatabase.LoadAssetAtPath<ComposureConfig>(ConfigPath);
            if (config != null)
            {
                Debug.Log($"[TimeKiller Setup] 58 - composure config already exists at {ConfigPath} — left untouched.");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<ComposureConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[TimeKiller Setup] 58 - created {ConfigPath}.");
            }

            Selection.activeObject = config;
            Debug.Log(Report(config));
        }

        /// What the dials actually mean in the units the player experiences,
        /// printed every run for the same reason Setup/51 prints the escalation
        /// ceilings: a difficulty number nobody re-checks stops being checked.
        static string Report(ComposureConfig cfg)
        {
            var steps = AssetDatabase.LoadAssetAtPath<TimeKiller.Player.PlayerFootstepConfig>(FootstepPath);
            var maniac = AssetDatabase.LoadAssetAtPath<TimeKiller.Maniac.ManiacConfig>(ManiacPath);

            var r = new System.Text.StringBuilder();
            r.AppendLine("[TimeKiller Setup] 58 - Composure (sanity)");
            r.AppendLine($"  enabled: {cfg.enabled}");
            r.AppendLine($"  dark at or below light {cfg.darkAtOrBelow:0.00}   " +
                         $"floor {cfg.floor:0.00}   resets every run");
            r.AppendLine($"  spend: {cfg.secondsToSpendInDark:0}s in the dark, " +
                         $"{cfg.secondsToSpendHiding:0}s hiding");
            r.AppendLine($"  restore: {cfg.secondsToRecoverInLight:0}s in light, " +
                         $"+{cfg.clockRestore:0.00} per clock (no items, ever)");

            if (steps != null && maniac != null)
            {
                float worst = PlayerComposure.LoudnessFor(cfg, cfg.floor);
                float walkNow = maniac.hearingRadius * steps.walkLoudness;
                float walkWorst = walkNow * worst;
                float runNow = maniac.hearingRadius * steps.runLoudness;
                r.AppendLine($"  AT THE FLOOR you are x{worst:0.00} louder: " +
                             $"walking carries {walkNow:0.0}u -> {walkWorst:0.0}u " +
                             $"(running today is {runNow:0.0}u)");
                r.AppendLine(walkWorst < runNow
                    ? "    OK - even at your worst, WALKING is still quieter than running is now, so" +
                      " slowing down remains the answer and the floor is survivable."
                    : "    ** FAIL - at the floor your walk is as loud as a run. There is no quiet left," +
                      " which is the death spiral the floor exists to prevent. Lower loudnessAtEmpty" +
                      " or raise floor.");
            }

            r.Append("  F1 line 'Composure' shows the value, the light level and the live multiplier.");
            return r.ToString();
        }
    }
}
