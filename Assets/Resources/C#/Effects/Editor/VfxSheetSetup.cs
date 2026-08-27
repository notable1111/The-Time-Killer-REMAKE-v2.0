// Menu: TimeKiller/Setup/38 - Build VFX Sheets (hand-drawn effects).
//
// Turns every PixelLab VFX strip in Assets/Resources/Assets/Effects/Vfx/ into a
// SpriteAnimationClip that an EffectRecipe can play. Adding a new effect to the
// game is then: drop the PNG in that folder, run this, assign the clip to a
// recipe. No code per effect — the same contract EffectRecipe already promised
// for particles, now honoured for drawn art.
//
// Three things here are load-bearing:
//
// 1. CENTRE PIVOT. Characters pivot at the feet so they stand on the floor
//    (see SurvivorAnimationSetup). A VFX burst is the opposite: it must be
//    centred on the point it was played at, or every impact lands offset from
//    the thing it is supposed to be hitting.
//
// 2. 32 PIXELS PER UNIT — the world's scale contract. A 64px strip is therefore
//    2x2 world units, about two thirds of a character's height. Get this wrong
//    and effects are comically over- or undersized next to the survivor.
//
// 3. RE-RUN PRESERVES TUNING. Only the frame list is refreshed on an existing
//    clip; framesPerSecond and loop are left exactly as they were, because
//    those are the two values a human tunes by eye after seeing it play. New
//    clips get the defaults below. Same reasoning as the scene rule: a setup
//    script must never quietly discard hand-tuned work.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Core;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TimeKiller.Effects.EditorTools
{
    public static class VfxSheetSetup
    {
        const string SheetFolder = "Assets/Resources/Assets/Effects/Vfx";
        const string ClipFolder = "Assets/Resources/C#/Effects/Configs/Clips";
        const int PixelsPerUnit = 32;          // the world's scale contract
        const float DefaultFps = 16f;          // 8 frames -> a half-second burst

        /// URP's stock unlit sprite material — assign to a recipe's sheetMaterial
        /// for anything that must glow in an unlit corridor.
        public const string UnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        [MenuItem("TimeKiller/Setup/38 - Build VFX Sheets (hand-drawn effects)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("38 - Build VFX Sheets (hand-drawn effects)")) return;

            if (!Directory.Exists(SheetFolder))
            {
                Debug.LogError($"[TimeKiller Setup] No VFX folder at {SheetFolder}. Drop PixelLab strips there first.");
                return;
            }

            var sheets = Directory.GetFiles(SheetFolder, "*.png", SearchOption.TopDirectoryOnly)
                                  .Select(p => p.Replace('\\', '/'))
                                  .OrderBy(p => p)
                                  .ToArray();
            if (sheets.Length == 0)
            {
                Debug.LogWarning($"[TimeKiller Setup] {SheetFolder} has no .png strips — nothing built.");
                return;
            }

            Directory.CreateDirectory(ClipFolder);
            var built = new List<string>();
            foreach (var sheet in sheets)
            {
                var clip = BuildClip(sheet);
                if (clip != null) built.Add($"{clip.name} ({clip.frames.Length}f @ {clip.framesPerSecond:0.#}fps)");
            }
            AssetDatabase.SaveAssets();

            Debug.Log($"[TimeKiller Setup] {built.Count} VFX clip(s) ready in {ClipFolder}: {string.Join(", ", built)}. " +
                      "Assign one to an EffectRecipe's sheetClip. Re-running this keeps any fps/loop you tuned by hand.");
        }

        /// Slices one strip and builds/refreshes its clip. Public so other setup
        /// scripts can build a single effect without scanning the whole folder.
        public static SpriteAnimationClip BuildClip(string sheetPath)
        {
            if (!Slice(sheetPath)) return null;

            var frames = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out var n) ? n : 0)
                .ToArray();
            if (frames.Length == 0)
            {
                Debug.LogError($"[TimeKiller Setup] No sprites sliced out of {sheetPath}");
                return null;
            }

            string name = Path.GetFileNameWithoutExtension(sheetPath);
            string clipPath = $"{ClipFolder}/{name}.asset";
            var clip = AssetDatabase.LoadAssetAtPath<SpriteAnimationClip>(clipPath);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteAnimationClip>();
                clip.framesPerSecond = DefaultFps;
                clip.loop = false;             // a burst plays once and dies
                clip.eventFrames = new int[0];
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            // Existing clips keep framesPerSecond and loop — those are eye-tuned.
            clip.frames = frames;
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static bool Slice(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[TimeKiller Setup] Not an importable texture: {path}");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return false;

            int cell = texture.height;                      // one row of square cells
            int count = Mathf.Max(1, texture.width / cell);
            string baseName = Path.GetFileNameWithoutExtension(path);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                rects.Add(new SpriteRect
                {
                    name = $"{baseName}_{i}",
                    spriteID = GUID.Generate(),
                    rect = new Rect(i * cell, 0, cell, cell),
                    alignment = SpriteAlignment.Center,     // burst centres on the impact point
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
            provider.SetSpriteRects(rects.ToArray());

            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

            provider.Apply();
            importer.SaveAndReimport();
            return true;
        }
    }
}
