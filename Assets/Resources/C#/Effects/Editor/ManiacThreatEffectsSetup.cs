// Menu: TimeKiller/Setup/52 - Setup Maniac Threat Effects (seen + swing).
//
// Creates the config and the two empty recipes the binder loads, and prints the
// art brief for whoever is drawing the sheets. Touches NO scene: the binder
// installs itself from Resources at runtime, so there is nothing to place.
//
// NEVER OVERWRITES an existing recipe or config. This is the opposite choice
// from Setup/50, which is authoritative for its recipes and resets them on every
// run — and the reason is that these two recipes are going to be filled in by
// hand as the art arrives, one field at a time. A re-run that blanked
// `sheetClip` would delete exactly the work this script exists to enable.
using System.IO;
using TimeKiller.Core;          // SpriteAnimationClip
using TimeKiller.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Effects.EditorTools
{
    public static class ManiacThreatEffectsSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Effects/Configs/ManiacThreatEffectsConfig.asset";
        const string RecipeFolder = "Assets/Resources/C#/Effects/Configs/Recipes";
        const string SpottedRecipe = RecipeFolder + "/ManiacSpotted.asset";
        const string AttackRecipe = RecipeFolder + "/ManiacSwing.asset";
        const string ClipFolder = "Assets/Resources/C#/Effects/Configs/Clips";

        // Playback rates that land each burst on the LENGTH OF ITS OWN SOUND:
        // the sound lane measured spotted at 1.45s and swing at 0.62s, and the
        // sheets are 12 and 8 frames. 12/1.45 = 8.28, 8/0.62 = 12.9. A visual
        // that outlives its sound reads as two events rather than one.
        const float SpottedFps = 8.28f;
        const float SwingFps = 12.9f;

        /// The slicer's default rate. Anything else means a human tuned it, and
        /// a human's number wins over the arithmetic above.
        const float SlicerDefaultFps = 16f;

        [MenuItem("TimeKiller/Setup/52 - Setup Maniac Threat Effects (seen + swing)")]
        public static void Build()
        {
            if (SetupGuard.Blocked("52 - Setup Maniac Threat Effects")) return;

            Directory.CreateDirectory(RecipeFolder);

            var spotted = FindOrCreateRecipe(SpottedRecipe, sortingOrder: 8);
            var attack = FindOrCreateRecipe(AttackRecipe, sortingOrder: 8);

            var config = AssetDatabase.LoadAssetAtPath<ManiacThreatEffectsConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ManiacThreatEffectsConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log($"[TimeKiller Setup] 52 - created {ConfigPath}");
            }

            // Re-link only. Cooldowns and any tuning already in the asset survive.
            if (config.spotted == null) config.spotted = spotted;
            if (config.attack == null) config.attack = attack;
            EditorUtility.SetDirty(config);

            // Hang the sliced sheets on the recipes, if the art has arrived and
            // the slots are still empty. FILL-EMPTY-ONLY, like everything else in
            // this script: a re-run must never blank a clip someone chose, and
            // must never revert an fps tuned by eye.
            AttachSheet(spotted, "maniac_spotted", SpottedFps);
            AttachSheet(attack, "maniac_swing", SwingFps);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = config;

            Debug.Log(Brief(config));
        }

        static void AttachSheet(EffectRecipe recipe, string clipName, float fps)
        {
            if (recipe == null) return;
            var clip = AssetDatabase.LoadAssetAtPath<SpriteAnimationClip>($"{ClipFolder}/{clipName}.asset");
            if (clip == null)
            {
                Debug.Log($"[TimeKiller Setup] 52 - no sliced clip at {ClipFolder}/{clipName}.asset yet. " +
                          "Drop the strip in Assets/Resources/Assets/Effects/Vfx and run " +
                          "\"TimeKiller/Setup/38 - Build VFX Sheets (hand-drawn effects)\" first " +
                          "(the FULL menu string - a truncated path fails silently).");
                return;
            }

            if (recipe.sheetClip == null)
            {
                recipe.sheetClip = clip;
                Debug.Log($"[TimeKiller Setup] 52 - {recipe.name}.sheetClip <- {clipName} " +
                          $"({clip.frames?.Length ?? 0} frames)");
            }

            if (Mathf.Approximately(clip.framesPerSecond, SlicerDefaultFps))
            {
                clip.framesPerSecond = fps;
                EditorUtility.SetDirty(clip);
                Debug.Log($"[TimeKiller Setup] 52 - {clipName} fps {SlicerDefaultFps} -> {fps:0.00} " +
                          $"({(clip.frames?.Length ?? 0) / fps:0.00}s, matching its sound)");
            }
            EditorUtility.SetDirty(recipe);
        }

        static EffectRecipe FindOrCreateRecipe(string path, int sortingOrder)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<EffectRecipe>(path);
            if (recipe != null) return recipe;

            recipe = ScriptableObject.CreateInstance<EffectRecipe>();

            // UNLIT, for the reason Setup/50 spells out: being spotted happens in
            // a dark corridor, and a lit VFX sprite is invisible in exactly the
            // place the effect exists to be seen. Left as a default the art can
            // override, not as something it has to remember.
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(VfxSheetSetup.UnlitMaterialPath);
            if (unlit != null) recipe.sheetMaterial = unlit;
            else Debug.LogWarning("[TimeKiller Setup] 52 - URP Sprite-Unlit-Default not found; " +
                                  "the sheets will use the LIT material and may be invisible in the dark.");

            // Characters render at 0. Above them, because this beat is the whole
            // point of the frame it plays on.
            recipe.sheetSortingOrder = sortingOrder;
            // No random rotation and no flip: a threat tell must read the same
            // way every time or the player cannot learn it. Both are on by
            // default on a fresh recipe, which is right for blood and wrong here.
            recipe.randomRotation = false;
            recipe.randomFlip = false;

            AssetDatabase.CreateAsset(recipe, path);
            Debug.Log($"[TimeKiller Setup] 52 - created empty recipe {path}");
            return recipe;
        }

        /// The handoff. Printed every run so whoever draws the sheets does not
        /// have to read three docs to find the world's scale contract.
        static string Brief(ManiacThreatEffectsConfig config)
        {
            bool haveSpotted = config.spotted != null && config.spotted.sheetClip != null;
            bool haveAttack = config.attack != null && config.attack.sheetClip != null;

            var report = new System.Text.StringBuilder();
            report.AppendLine("[TimeKiller Setup] 52 - Maniac threat effects");
            report.AppendLine($"  spotted recipe: {(haveSpotted ? "HAS ART" : "empty (silent, no error)")}");
            report.AppendLine($"  swing recipe:   {(haveAttack ? "HAS ART" : "empty (silent, no error)")}");
            report.AppendLine($"  spotted cooldown {config.spottedCooldown:0.#}s, " +
                              $"swing cooldown {config.attackCooldown:0.##}s");
            report.AppendLine("  The binder installs itself at runtime — nothing to add to any scene.");
            report.AppendLine("  F1 line 'ThreatVfx' counts played/heard, so 'did it fire?' is a number.");

            if (haveSpotted && haveAttack) return report.ToString().TrimEnd();

            report.AppendLine();
            report.AppendLine("  ART BRIEF (what the empty recipes need):");
            report.AppendLine("    - One sprite-sheet strip per beat, in the locked hand-drawn style.");
            report.AppendLine("    - 32px square cells. The world is 32 px/unit, so a 32px cell at scale 1");
            report.AppendLine("      displays 1:1; anything else is resampled and reads as mush.");
            report.AppendLine("    - CENTRE-pivoted (a character pivots at the feet so it stands on the");
            report.AppendLine("      floor; a burst must sit on the point it was played at).");
            report.AppendLine("    - Import at 32 PPU, Point filter, no compression.");
            report.AppendLine("    - Drop the strip in Assets/Resources/Assets/Effects/Vfx/, slice it with");
            report.AppendLine("      Setup/38, then assign the clip to the recipe's sheetClip.");
            report.AppendLine("    - Judge it with TimeKiller/Verify/VFX Visibility in the REAL scene:");
            report.AppendLine("      under ~0.5% peak screen coverage on a dark floor is missed entirely,");
            report.AppendLine("      ~1.2%+ reads. That probe exists because this was got wrong before.");
            return report.ToString().TrimEnd();
        }
    }
}
