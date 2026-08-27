// Menu: TimeKiller/Setup/50 - Setup Clock Effects (hit + wake).
//
// Builds the two recipes the clock-repair loop never had and wires the
// ClockEffects binder into the open scene. Runs the whole chain in one click:
// it slices its own strips through VfxSheetSetup.BuildClip rather than making
// you run Setup/38 first and remember which order.
//
// Like Setup/23, this script is AUTHORITATIVE for its two recipes — re-running
// resets them to the values below. That is the "back to defaults" path. Tune by
// editing the numbers here so the tuning IS the shipped default, or the next
// re-run silently reverts your Inspector work. The clips' framesPerSecond and
// loop are NOT touched (Setup/38 preserves eye-tuned values), and neither is
// anything else in the scene.
//
// THE SHEETS ARE UNLIT ON PURPOSE. A repair happens in an unlit corridor by
// definition — you stand still in the dark for several seconds. A lit VFX
// sprite is invisible in exactly the place this effect exists to be seen, which
// is the same trap the blood burst fell into when it was tinted to "match the
// palette" and measured 0.26% screen coverage.
using System.IO;
using TimeKiller.Core;
using TimeKiller.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Effects.EditorTools
{
    public static class ClockEffectsSetup
    {
        const string RecipeFolder = "Assets/Resources/C#/Effects/Configs/Recipes";
        const string SheetFolder = "Assets/Resources/Assets/Effects/Vfx";
        const string HitSheet = SheetFolder + "/clock_hit.png";
        const string WakeSheet = SheetFolder + "/clock_wake.png";

        // Chosen by category and measured duration, NOT by ear — nobody has
        // listened to these in the mix yet. metalClick is a single mechanical
        // click (0.45s) and metalLatch a mechanism catching (0.26s), which is
        // the right fiction for a clock. There is no bell or chime anywhere in
        // the project's audio packs; a synthesised clock chime is the obvious
        // upgrade for the wake, and swapping it is this one string.
        const string HitClip = "Assets/Resources/Outsource/KenneyRPGAudio/Audio/metalClick.ogg";
        const string WakeClip = "Assets/Resources/Outsource/KenneyRPGAudio/Audio/metalLatch.ogg";

        [MenuItem("TimeKiller/Setup/50 - Setup Clock Effects (hit + wake)")]
        public static void Build()
        {
            if (SetupGuard.Blocked("50 - Setup Clock Effects")) return;

            Directory.CreateDirectory(RecipeFolder);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(VfxSheetSetup.UnlitMaterialPath);
            if (unlit == null)
                Debug.LogWarning("[TimeKiller Setup] URP's Sprite-Unlit-Default material not found — " +
                                 "the clock bursts will use the LIT default and will be hard to see in a dark corridor.");

            var hitClip = BuildClipIfPresent(HitSheet);
            var wakeClip = BuildClipIfPresent(WakeSheet);

            // ---- the earned press ----
            var hit = Recipe("ClockHit");
            hit.sheetClip = hitClip;
            // 1.0 = exactly 1:1. The strip is authored at 32px cells against a
            // 32 px/unit world, so any other value resamples pixel art that is
            // already the right resolution. Size is changed by redrawing the
            // strip, not by scaling it — the same rule the droplets follow.
            hit.sheetScale = 1f;
            hit.sheetMaterial = unlit;
            hit.sheetSortingOrder = 5;          // clocks render at 0; this sits over the face
            hit.randomFlip = true;              // repeat presses must not be the same stamp
            hit.randomRotation = true;          // a spark burst has no readable "up"
            hit.particlePrefab = null;
            hit.clips = LoadClips(HitClip);
            hit.volume = 0.55f;                 // quiet: this fires up to five times per clock
            hit.pitchJitter = 0.10f;
            // NO SHAKE. A correct press is a reward, and shaking the camera on
            // every one would punish the player for succeeding — it also fights
            // the skill-check bar they are trying to read.
            hit.shakeStrength = 0f;
            EditorUtility.SetDirty(hit);

            // ---- the clock wakes ----
            var wake = Recipe("ClockFixed");
            wake.sheetClip = wakeClip;
            wake.sheetScale = 1f;
            wake.sheetMaterial = unlit;
            wake.sheetSortingOrder = 5;
            wake.randomFlip = true;
            // NOT rotated: the wake burst is a ring around the clock face, and a
            // random tilt would read as the ring being knocked askew.
            wake.randomRotation = false;
            wake.particlePrefab = null;
            wake.clips = LoadClips(WakeClip);
            wake.volume = 0.9f;
            wake.pitchJitter = 0.02f;           // a signature beat should sound the same every time
            wake.shakeStrength = 0.22f;         // felt, not thrown — this is good news, not a hit
            wake.shakeDuration = 0.35f;
            EditorUtility.SetDirty(wake);

            // ---- the gate unlocks ----
            // The beat where the run changes shape: the question stops being
            // "where are the clocks" and becomes "where is the door". Played at
            // the door itself, so it is a direction as well as a celebration.
            //
            // NO SHEET AND NO CLIP YET, on purpose. There is no gate art drawn,
            // and the SOUND is already taken: ExitDoor plays its own local creak
            // and AudioDirector fires a deliberately non-positional unlock sting
            // map-wide. A third sound on the same frame would be mud, so this
            // recipe carries the one channel nothing else is using — a heavy,
            // slow shake, which is what a stone gate grinding open feels like.
            // Drop a sheet into sheetClip when the art exists and it gains its
            // picture without a code change.
            var gate = Recipe("GateOpened");
            gate.sheetScale = 1.4f;             // a gate is bigger than a clock face
            gate.sheetMaterial = unlit;
            gate.sheetSortingOrder = 5;
            gate.randomFlip = false;            // a landmark reads the same way every time
            gate.randomRotation = false;
            gate.particlePrefab = null;
            gate.clips = null;
            gate.shakeStrength = 0.30f;         // heavier and longer than the clock wake
            gate.shakeDuration = 0.70f;
            EditorUtility.SetDirty(gate);

            AssetDatabase.SaveAssets();

            // ---- the binder ----
            // It used to be built here as a scene object, and that is exactly why
            // the feature was never live: this script was never run, so no scene
            // ever received it and the drawn art sat unused on disk. ClockEffects
            // now installs itself from Resources, so it is present in CastleWing,
            // Catacombs and any scene added later with nothing to remember.
            //
            // An existing scene object still wins (ClockEffects skips its own
            // installer when one is present), so a hand-placed binder from before
            // this change keeps working — it is just re-wired here rather than
            // created, and only if it already exists. Nothing dirties a scene.
            var existing = Object.FindAnyObjectByType<ClockEffects>();
            if (existing != null)
            {
                var so = new SerializedObject(existing);
                so.FindProperty("hitRecipe").objectReferenceValue = hit;
                so.FindProperty("fixedRecipe").objectReferenceValue = wake;
                so.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Debug.Log("[TimeKiller Setup] 50 - re-wired the ClockEffects object already in this scene.");
            }

            string art = (hitClip == null || wakeClip == null)
                ? $"  ART MISSING — drop clock_hit.png / clock_wake.png in {SheetFolder} and re-run; " +
                  "the recipes are wired and will play sound and shake without them."
                : $"  Sheets: {hitClip.frames.Length}f hit, {wakeClip.frames.Length}f wake.";
            Debug.Log("[TimeKiller Setup] Clock effects ready: ClockHit (earned press) + ClockFixed (the clock wakes)." + art);
        }

        /// Slices a strip into a clip, or returns null and says so. Missing art
        /// is NOT an error here: the recipes are still worth wiring, because
        /// sound and shake work without a sheet and the clip can be dropped in
        /// later by re-running this.
        static SpriteAnimationClip BuildClipIfPresent(string sheetPath)
        {
            if (!File.Exists(sheetPath))
            {
                Debug.LogWarning($"[TimeKiller Setup] No VFX strip at {sheetPath} — that recipe gets no drawn burst yet.");
                return null;
            }
            return VfxSheetSetup.BuildClip(sheetPath);
        }

        static AudioClip[] LoadClips(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[TimeKiller Setup] Audio clip missing: {path} — that beat will be silent.");
                return new AudioClip[0];
            }
            return new[] { clip };
        }

        static EffectRecipe Recipe(string name)
        {
            string path = $"{RecipeFolder}/{name}.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<EffectRecipe>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<EffectRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }
            return recipe;
        }
    }
}
