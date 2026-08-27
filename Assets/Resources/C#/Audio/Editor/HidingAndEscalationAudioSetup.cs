// Creates and wires the two config assets behind the hiding muffle and the
// escalation cue. Both features load their config from Resources and DO NOT
// INSTALL AT ALL when it is missing, so this script IS the install.
//
// NO SCENE IS TOUCHED, deliberately. Both components self-install via
// RuntimeInitializeOnLoadMethod: the scenes here are hand-tuned and protected,
// three sessions share one Editor, and a feature that needs no scene edit cannot
// collide with anybody or miss a level. Catacombs spent today missing 23 systems
// precisely because they were scene-object features that its setup pass never
// received; these two cannot go the same way.
//
// Idempotent and non-destructive: an existing asset keeps every value already
// tuned by ear and only has genuinely empty fields filled in.
using System.Collections.Generic;
using TimeKiller.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Audio
{
    public static class HidingAndEscalationAudioSetup
    {
        const string ConfigFolder = "Assets/Resources/C#/Audio/Configs";
        const string MufflePath = ConfigFolder + "/WorldMuffleConfig.asset";
        const string EscalationPath = ConfigFolder + "/EscalationVoiceConfig.asset";
        const string EscalationClip = "Assets/Resources/Assets/Effects/Sfx/maniac_escalate_far.wav";

        [MenuItem("TimeKiller/Setup/57 - Hiding muffle + escalation cue (audio)")]
        public static void Run()
        {
            if (SetupGuard.Blocked("57 - Hiding muffle + escalation cue")) return;

            var report = new System.Text.StringBuilder("[TimeKiller Setup] 57 - hiding muffle + escalation cue\n");

            var muffle = FindOrCreate<WorldMuffleConfig>(MufflePath, report);
            report.AppendLine($"   muffle: cutoff {muffle.cutoffHz:0} Hz, close {muffle.closeSeconds:0.00}s, "
                            + $"open {muffle.openSeconds:0.00}s, spatial >= {muffle.spatialThreshold:0.00}");

            var escalation = FindOrCreate<EscalationVoiceConfig>(EscalationPath, report);

            // Only ever ADD the shipped cue, and only when the pool is empty.
            // A pool someone has already curated is theirs.
            if (escalation.cues == null || escalation.cues.Length == 0)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EscalationClip);
                if (clip == null)
                {
                    report.AppendLine($"   MISSING cue clip at {EscalationClip} — escalation stays silent.");
                }
                else
                {
                    var so = new SerializedObject(escalation);
                    var cues = so.FindProperty("cues");
                    cues.arraySize = 1;
                    cues.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(escalation);
                    report.AppendLine($"   escalation cue <- {clip.name} ({clip.length:0.00}s)");
                }
            }
            else report.AppendLine($"   escalation already has {escalation.cues.Length} cue(s) — left alone");

            ImportAsGameSfx(EscalationClip, report);

            AssetDatabase.SaveAssets();
            report.AppendLine("   No scene touched: both features self-install from Resources at runtime.");
            Debug.LogWarning(report.ToString());
        }

        static T FindOrCreate<T>(string path, System.Text.StringBuilder report) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { report.AppendLine($"   {System.IO.Path.GetFileName(path)} already exists — kept"); return asset; }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            report.AppendLine($"   created {path}");
            return asset;
        }

        /// Same import contract the voices use: decompressed on load, because
        /// these fire on the frame the beat happens and a streamed clip arrives
        /// late.
        static void ImportAsGameSfx(string path, System.Text.StringBuilder report)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            var settings = importer.defaultSampleSettings;
            bool dirty = false;
            if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
            { settings.loadType = AudioClipLoadType.DecompressOnLoad; dirty = true; }
            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            { settings.compressionFormat = AudioCompressionFormat.Vorbis; dirty = true; }
            if (settings.preloadAudioData) { settings.preloadAudioData = false; dirty = true; }
            if (dirty)
            {
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                report.AppendLine($"   imported {System.IO.Path.GetFileName(path)}");
            }
        }

        [MenuItem("TimeKiller/Setup/57b - Remove hiding muffle + escalation cue")]
        public static void Clear()
        {
            if (SetupGuard.Blocked("57b - Remove hiding muffle + escalation cue")) return;
            var removed = new List<string>();
            foreach (var path in new[] { MufflePath, EscalationPath })
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null
                    && AssetDatabase.DeleteAsset(path)) removed.Add(System.IO.Path.GetFileName(path));
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[TimeKiller Setup] 57b - deleted " + (removed.Count == 0 ? "nothing" : string.Join(", ", removed))
                + ". Both features check for their config at startup, so neither will install now.");
        }
    }
}
