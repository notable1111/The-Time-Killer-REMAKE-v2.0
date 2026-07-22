// Menu: TimeKiller/Setup/21 - Generate Maniac Animations.
// Slices the chosen killer sheet (maranza pack, Pacient stage_two — decided
// 2026-07-22) into a 32x32 grid and builds the clip set: 8 walk cycles
// (rows 0-7, counter-clockwise from Down — same order as FacingDirection),
// single-frame idles, and the death clip (row 8). Attaches SpriteAnimator +
// ManiacAnimationDriver to the Maniac in the scene.
// Credit: character sprites by Maranza (maranza.itch.io) — see pack CREDITS.txt.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class ManiacAnimationSetup
    {
        const string SheetPath = "Assets/Resources/Outsource/ManiacPack/Pacient/stage_two.png";
        const string ClipFolder = "Assets/Resources/C#/Maniac/Configs/Animations";
        const string SetPath = "Assets/Resources/C#/Maniac/Configs/ManiacAnimationSet.asset";
        const int Cell = 32, PixelsPerUnit = 32, WalkFrames = 4, DirectionRows = 8, DeathRow = 8;

        [MenuItem("TimeKiller/Setup/21 - Generate Maniac Animations")]
        public static void Generate()
        {
            if (AssetImporter.GetAtPath(SheetPath) == null)
            {
                Debug.LogError($"[TimeKiller Setup] Killer sheet missing: {SheetPath} — import the maranza pack first.");
                return;
            }

            SliceSheet();
            Directory.CreateDirectory(ClipFolder);

            var frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                .ToDictionary(s => s.name);
            var set = AssetDatabase.LoadAssetAtPath<ManiacAnimationSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ManiacAnimationSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            var directions = System.Enum.GetNames(typeof(FacingDirection));
            set.walk = new SpriteAnimationClip[DirectionRows];
            set.idle = new SpriteAnimationClip[DirectionRows];
            for (int row = 0; row < DirectionRows; row++)
            {
                var rowFrames = RowFrames(frames, row, WalkFrames);
                set.walk[row] = BuildClip($"ManiacWalk_{directions[row]}", rowFrames, fps: 9f, loop: true);
                set.idle[row] = BuildClip($"ManiacIdle_{directions[row]}", new[] { rowFrames[0] }, fps: 1f, loop: true);
            }
            set.death = BuildClip("ManiacDeath", RowFrames(frames, DeathRow, WalkFrames), fps: 6f, loop: false);

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AttachToManiac(set, frames);
            Debug.Log("[TimeKiller Setup] Maniac animations generated: 8 walk cycles + idles + death (maranza Pacient stage_two).");
        }

        static Sprite[] RowFrames(Dictionary<string, Sprite> frames, int row, int count)
        {
            var result = new List<Sprite>();
            for (int col = 0; col < count; col++)
                if (frames.TryGetValue($"maniac_r{row}_{col}", out var sprite))
                    result.Add(sprite);
            if (result.Count == 0)
                Debug.LogError($"[TimeKiller Setup] No frames for row {row} — was the sheet sliced?");
            return result.ToArray();
        }

        static SpriteAnimationClip BuildClip(string clipName, Sprite[] frames, float fps, bool loop)
        {
            string path = $"{ClipFolder}/{clipName}.asset";
            var clip = AssetDatabase.LoadAssetAtPath<SpriteAnimationClip>(path);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteAnimationClip>();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frames = frames;
            clip.framesPerSecond = fps;
            clip.loop = loop;
            clip.eventFrames = new int[0];
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void SliceSheet()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            int cols = texture.width / Cell, rows = texture.height / Cell;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>();
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                    rects.Add(new SpriteRect
                    {
                        name = $"maniac_r{row}_{col}",
                        spriteID = GUID.Generate(),
                        // texture y=0 is the BOTTOM; sheet rows count from the top
                        rect = new Rect(col * Cell, texture.height - (row + 1) * Cell, Cell, Cell),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                    });
            provider.SetSpriteRects(rects.ToArray());

            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null)
                nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

            provider.Apply();
            importer.SaveAndReimport();
        }

        static void AttachToManiac(ManiacAnimationSet set, Dictionary<string, Sprite> frames)
        {
            var maniac = Object.FindAnyObjectByType<ManiacController>();
            if (maniac == null)
            {
                Debug.LogWarning("[TimeKiller Setup] No Maniac in scene — run Setup/20 first, then re-run 21.");
                return;
            }

            if (maniac.GetComponent<SpriteAnimator>() == null)
                Undo.AddComponent<SpriteAnimator>(maniac.gameObject);
            var driver = maniac.GetComponent<ManiacAnimationDriver>();
            if (driver == null) driver = Undo.AddComponent<ManiacAnimationDriver>(maniac.gameObject);

            var so = new SerializedObject(driver);
            so.FindProperty("animations").objectReferenceValue = set;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (frames.TryGetValue("maniac_r0_0", out var still))
                maniac.GetComponent<SpriteRenderer>().sprite = still;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(maniac.gameObject.scene);
        }
    }
}
