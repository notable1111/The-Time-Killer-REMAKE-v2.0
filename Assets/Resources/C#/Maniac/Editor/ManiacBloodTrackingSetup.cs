// Menu: TimeKiller/Setup/56 - Create Maniac Blood Tracking Config.
//
// Numbered 56 and not 53: the sound lane also claimed 53 ("Assign maniac
// threat SFX"). Unity allows both, since the full menu strings differ, which
// is exactly what makes it dangerous - two lanes say "run Setup/53" and mean
// different scripts. Mine moved because it is the newer of the two and
// nothing outside this repo references it yet.
//
// Touches no scene — ManiacBloodTracker is auto-added by ManiacController and
// self-loads this asset from Resources. Never overwrites an existing config.
//
// The asset is created DISABLED. Turning it on is a deliberate act, and the
// report below is what you read before doing it: it prints the reach of the
// feature against his other senses, because "he can smell blood" and "he can
// find you anywhere" are separated by exactly one number.
using System.IO;
using TimeKiller.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class ManiacBloodTrackingSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacBloodTrackingConfig.asset";
        const string ManiacConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";
        const string BloodConfigPath = "Assets/Resources/C#/Blood/Configs/BloodConfig.asset";

        [MenuItem("TimeKiller/Setup/56 - Create Maniac Blood Tracking Config")]
        public static void Run()
        {
            if (SetupGuard.Blocked("56 - Create Maniac Blood Tracking Config")) return;

            var config = AssetDatabase.LoadAssetAtPath<ManiacBloodTrackingConfig>(ConfigPath);
            if (config != null)
            {
                Debug.Log($"[TimeKiller Setup] 56 - Blood tracking: config already exists at {ConfigPath} — " +
                          "left untouched.");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<ManiacBloodTrackingConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[TimeKiller Setup] 56 - Blood tracking: created {ConfigPath} (DISABLED).");
            }

            Selection.activeObject = config;
            Debug.Log(Report(config));
        }

        static string Report(ManiacBloodTrackingConfig config)
        {
            var maniac = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ManiacConfigPath);
            var blood = AssetDatabase.LoadAssetAtPath<TimeKiller.Blood.BloodConfig>(BloodConfigPath);

            var r = new System.Text.StringBuilder();
            r.AppendLine("[TimeKiller Setup] 56 - Blood tracking");
            r.AppendLine($"  enabled: {config.enabled}" + (config.enabled ? "" : "   <- OFF, as shipped"));
            r.AppendLine($"  notice {config.noticeRadius:0.0}u (LOS required: {config.requireLineOfSight})" +
                         $"  follow {config.followRadius:0.0}u  traces live {config.traceLifetime:0}s");

            if (maniac != null)
            {
                r.AppendLine($"  against his other senses: sight {maniac.sightRange:0.0}u, " +
                             $"hearing {maniac.hearingRadius:0.0}u");
                r.AppendLine(config.noticeRadius < maniac.sightRange * 0.5f
                    ? "    OK - he has to nearly walk over your blood, so a trail matters where he was " +
                      "already going rather than telling him where you are."
                    : "    ** WARNING - noticeRadius is approaching his sight range. At that reach bleeding " +
                      "stops being a trail he stumbles across and becomes a homing signal, which is what " +
                      "ARCHITECTURE warned could make the 1-HP state unsurvivable rather than tense.");
            }

            if (blood != null)
                r.AppendLine($"  you bleed at or below {blood.bleedAtHp} HP, a drip every " +
                             $"{blood.dripInterval:0.0}s and at least {blood.dripMinDistance:0.0}u apart");

            r.AppendLine();
            r.AppendLine("  BEFORE TURNING THIS ON:");
            r.AppendLine("    1. It applies pressure exactly when the player is weakest — bleeding starts at");
            r.AppendLine("       low HP by definition. ARCHITECTURE asked for a bot A/B before it goes live.");
            r.AppendLine("    2. Turn it on ALONE. Per-clock escalation is already in flight and unjudged;");
            r.AppendLine("       two difficulty changes at once cannot be attributed to either.");
            r.AppendLine("    3. DONE 2026-08-27: TestTelemetry now counts HeardBlood separately and the");
            r.AppendLine("       batch row emits it, so an A/B can tell 'he found the trail' from 'he heard");
            r.AppendLine("       you'. That was the last blocker; what is left is running the batch.");
            r.Append("  F1 line 'BloodTrack' shows traces held and leads taken.");
            return r.ToString();
        }
    }
}
