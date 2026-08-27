// Gives the maniac's two threat beats their SOUND layer.
//
// WHY A SEPARATE SCRIPT FROM Setup/52. Setup/52 belongs to the visual pass: it
// creates the recipes and hangs the sprite sheets on them. This one only ever
// touches `clips`, so the two can be run in either order, any number of times,
// without either undoing the other's work. It is also the lane seam — sound is
// delivered as an asset and pointed at by a config field, so neither side edits
// the other's files.
//
// WHY THESE TWO CLIPS, AND NOT A SECOND STING. Neither moment was silent to
// begin with: being seen already fires a music sting plus one of three growls,
// and the swing already fires one of three roars. Dropping another loud sound on
// top of either would just add mud. So the band each existing sound LEAVES EMPTY
// was measured first, with Tools/AudioPipeline/measure_audio.py, and each new
// clip was processed to live there:
//
//   moment    existing              free band          this clip lands
//   ------    --------------------  -----------------  ---------------------
//   seen      sting  (460-2k: 31%)  2k-20k             70.6% in 2k-20k
//             growl  (<460:  52%)   (sting 11%, growl 0%)   0.0% below 460
//   swing     roar   (<460:  60%)   460-2k AND 2k-20k  54.6% in 460-2k
//                    (460-2k: 6%)   (roar has 0.1% up   19.0% in 2k-20k
//                    (2k-20k: 0.1%)  there)              0.4% below 460
//
// THE SWING WAS RE-CUT ONCE, and what the second pass taught is worth keeping.
// Version 1 put 32.7% in 460-2k but ALSO left 26.9% sitting at 150-460 — inside
// the roar's dominant band. Mixed at real gains and played in a real session it
// could not be heard, and the user confirmed it. The fix was not more level (a
// limiter could only buy ~4 dB before the sound squashed); it was getting OUT of
// the roar's band. High-passed at 800 Hz in three stages, the same clip now
// lifts the 2-20 kHz band of that moment by 14.9 dB, because the roar puts
// essentially nothing up there. Masking was the problem, not loudness.
//
// A second lesson, about measuring: the first "+0.6 dB, inaudible" verdict was
// partly an artifact of averaging over the WHOLE audition file when the clip
// only occupies its first 0.62s. Measure the window a sound occupies, not the
// file it sits in.
//
// Both are peak-normalised to -3 dBFS, the project's layering headroom ruling of
// 2026-08-02 — these play ON TOP of sounds that are already going, which is the
// exact case that ruling exists for.
//
// SOURCE. Both are processed from EchoChambers vendor one-shots rather than
// used raw: the "Hush" was high-passed off the sting's mid-band, and the whoosh
// was pitch-shifted up 3.5x and then high-passed at 800 Hz, because measurement
// showed the raw file was bottom-heavy and would have fought the roar it is
// supposed to sit beside. Each re-cut starts from the VENDOR file, never from a
// previously processed one — reprocessing compounds the limiter artifacts. The
// vendor originals themselves are untouched, per add_headroom.py's scope rule.
using TimeKiller.EditorTools;
using TimeKiller.Effects;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Audio
{
    public static class ManiacThreatAudioSetup
    {
        const string SfxFolder = "Assets/Resources/Assets/Effects/Sfx";
        const string SpottedClip = SfxFolder + "/maniac_spotted_hiss.wav";
        const string SwingClip = SfxFolder + "/maniac_swing_air.wav";

        const string RecipeFolder = "Assets/Resources/C#/Effects/Configs/Recipes";
        const string SpottedRecipe = RecipeFolder + "/ManiacSpotted.asset";
        const string SwingRecipe = RecipeFolder + "/ManiacSwing.asset";

        [MenuItem("TimeKiller/Setup/53 - Assign maniac threat SFX (seen + swing)")]
        public static void Run()
        {
            if (SetupGuard.Blocked("53 - Assign maniac threat SFX")) return;

            var report = new System.Text.StringBuilder("[TimeKiller Setup] 53 - maniac threat SFX\n");

            Import(SpottedClip, report);
            Import(SwingClip, report);

            Assign(SpottedRecipe, SpottedClip, report);
            Assign(SwingRecipe, SwingClip, report);

            AssetDatabase.SaveAssets();
            Debug.LogWarning(report.ToString());
        }

        /// Match the import settings the voices already use: decompressed on load
        /// (these are short and fire on the frame the beat happens — a streamed
        /// clip would arrive late), Vorbis, and no forced mono, since the files
        /// are already single-channel.
        static void Import(string path, System.Text.StringBuilder report)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) { report.AppendLine($"   MISSING clip: {path}"); return; }

            var settings = importer.defaultSampleSettings;
            bool dirty = false;
            if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
            { settings.loadType = AudioClipLoadType.DecompressOnLoad; dirty = true; }
            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            { settings.compressionFormat = AudioCompressionFormat.Vorbis; dirty = true; }
            if (!Mathf.Approximately(settings.quality, 1f))
            { settings.quality = 1f; dirty = true; }
            // Per-platform since Unity 2022; the importer-level property of the
            // same name is obsolete-as-error here, not merely deprecated.
            if (settings.preloadAudioData)
            { settings.preloadAudioData = false; dirty = true; }

            if (dirty)
            {
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                report.AppendLine($"   imported {System.IO.Path.GetFileName(path)} (DecompressOnLoad, Vorbis)");
            }
            else report.AppendLine($"   {System.IO.Path.GetFileName(path)} already imported correctly");
        }

        /// The undo. These two layers are additions to moments that already had
        /// sound — a sting plus a growl when he sees you, a roar when he swings —
        /// so "remove them again" has to be one click, not an archaeology
        /// exercise. Clears only `clips`; the visual half of each recipe stays.
        [MenuItem("TimeKiller/Setup/53b - Remove maniac threat SFX")]
        public static void Clear()
        {
            if (SetupGuard.Blocked("53b - Remove maniac threat SFX")) return;
            foreach (var path in new[] { SpottedRecipe, SwingRecipe })
            {
                var recipe = AssetDatabase.LoadAssetAtPath<EffectRecipe>(path);
                if (recipe == null) continue;
                var so = new SerializedObject(recipe);
                so.FindProperty("clips").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(recipe);
            }
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[TimeKiller Setup] 53b - threat SFX removed; both moments are back to voice and sting only.");
        }

        /// Fill ONLY the clips array, and only when it does not already hold this
        /// clip. Everything else on the recipe — sheet, particles, shake, volume,
        /// the visual lane's work — is read and written back untouched, so
        /// re-running this can never cost anyone their tuning.
        static void Assign(string recipePath, string clipPath, System.Text.StringBuilder report)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<EffectRecipe>(recipePath);
            if (recipe == null)
            {
                report.AppendLine($"   NO RECIPE at {recipePath} — run Setup/52 first, then this again.");
                return;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null) { report.AppendLine($"   MISSING clip: {clipPath}"); return; }

            string name = System.IO.Path.GetFileNameWithoutExtension(recipePath);

            if (recipe.clips != null && System.Array.IndexOf(recipe.clips, clip) >= 0)
            {
                report.AppendLine($"   {name} already carries {clip.name} — left alone");
                return;
            }

            var so = new SerializedObject(recipe);
            var clips = so.FindProperty("clips");
            int index = clips.arraySize;
            clips.arraySize = index + 1;
            clips.GetArrayElementAtIndex(index).objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recipe);

            report.AppendLine($"   {name} <- {clip.name}  ({clips.arraySize} clip(s) on the recipe)");
        }
    }
}
