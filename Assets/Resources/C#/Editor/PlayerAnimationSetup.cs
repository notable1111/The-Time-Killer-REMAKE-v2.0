// Menu: TimeKiller/Setup/5 - Generate Player Animations.
// Slices the New_Leaf 8-direction sheets (32x48 cells, one row per file),
// builds 16 SpriteAnimationClips (idle + run x 8 directions), fills the
// PlayerAnimationSet arrays, and attaches everything to the Player.
// Timing: run fps scales with frame count so steps/second stays ~3.5 at full
// run speed regardless of whether a direction has 8 or 4 frames.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class PlayerAnimationSetup
    {
        const string PackRoot = "Assets/Resources/Outsource/NewLeafCharacter";
        const string ClipFolder = "Assets/Resources/C#/Player/Configs/Animations";
        const string SetPath = "Assets/Resources/C#/Player/Configs/PlayerAnimationSet.asset";
        const int CellWidth = 32, CellHeight = 48, PixelsPerUnit = 32;

        // Sheet file per direction, in FacingDirection order:
        // Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight.
        static readonly string[] IdleSheets =
        {
            "IDLE ANIMATIONS/CHARACTER 1-idle  front.png",
            "IDLE ANIMATIONS/CHARACTER 1-sw idle.png",
            "IDLE ANIMATIONS/CHARACTER 1-front left idle.png",
            "IDLE ANIMATIONS/CHARACTER 1-nw idle.png",
            "IDLE ANIMATIONS/CHARACTER 1-back idle.png",
            "IDLE ANIMATIONS/CHARACTER 1-ne idle.png",
            "IDLE ANIMATIONS/CHARACTER 1-front right idle.png",
            "IDLE ANIMATIONS/CHARACTER 1-se idle.png",
        };
        static readonly string[] RunSheets =
        {
            "RUN ANIMATIONS/CHARACTER 1-FRONT RUN.png",
            "RUN ANIMATIONS/CHARACTER 1-sw run.png",
            "RUN ANIMATIONS/CHARACTER 1-w run.png",
            "RUN ANIMATIONS/CHARACTER 1-nw run.png",
            "RUN ANIMATIONS/CHARACTER 1-BACK RUN.png",
            "RUN ANIMATIONS/CHARACTER 1-ne run.png",
            "RUN ANIMATIONS/CHARACTER 1-e  run.png",
            "RUN ANIMATIONS/CHARACTER 1-se run.png",
        };

        [MenuItem("TimeKiller/Setup/5 - Generate Player Animations")]
        public static void Generate()
        {
            SliceSheets(IdleSheets.Concat(RunSheets));
            Directory.CreateDirectory(ClipFolder);

            var set = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PlayerAnimationSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            var directions = System.Enum.GetNames(typeof(FacingDirection));
            set.idle = new SpriteAnimationClip[8];
            set.run = new SpriteAnimationClip[8];
            for (int i = 0; i < 8; i++)
            {
                set.idle[i] = BuildClip(IdleSheets[i], $"Idle_{directions[i]}", fpsForIdle: true);
                set.run[i] = BuildClip(RunSheets[i], $"Run_{directions[i]}", fpsForIdle: false);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AttachToPlayer(set);
            Debug.Log("[TimeKiller Setup] New_Leaf 8-direction animations generated (16 clips) and attached.");
        }

        static SpriteAnimationClip BuildClip(string sheetRelativePath, string clipName, bool fpsForIdle)
        {
            string sheetPath = $"{PackRoot}/{sheetRelativePath}";
            var frames = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(s => int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out var n) ? n : 0)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"[TimeKiller Setup] No sprites at {sheetPath}");
                return null;
            }

            string clipPath = $"{ClipFolder}/{clipName}.asset";
            var clip = AssetDatabase.LoadAssetAtPath<SpriteAnimationClip>(clipPath);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteAnimationClip>();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frames = frames;
            clip.loop = true;
            if (fpsForIdle)
            {
                clip.framesPerSecond = 5f;
                clip.eventFrames = new int[0];
            }
            else
            {
                // Keep ~3.5 steps/second at full run speed: 2 contacts per cycle.
                clip.framesPerSecond = frames.Length >= 8 ? 14f : 7f;
                clip.eventFrames = frames.Length >= 8 ? new[] { 1, 5 } : new[] { 1, 3 };
            }
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void SliceSheets(IEnumerable<string> sheets)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();

            foreach (var relative in sheets)
            {
                string path = $"{PackRoot}/{relative}";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[TimeKiller Setup] Missing sheet: {path}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int frames = Mathf.Max(1, texture.width / CellWidth);
                string baseName = Path.GetFileNameWithoutExtension(path);

                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();

                var rects = new List<SpriteRect>();
                for (int i = 0; i < frames; i++)
                {
                    rects.Add(new SpriteRect
                    {
                        name = $"{baseName}_{i}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(i * CellWidth, 0, CellWidth, CellHeight),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
                }
                provider.SetSpriteRects(rects.ToArray());

                var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (nameIds != null)
                    nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

                provider.Apply();
                importer.SaveAndReimport();
            }
        }

        static void AttachToPlayer(PlayerAnimationSet set)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[TimeKiller Setup] No Player in scene — run Setup/4 first, then re-run 5.");
                return;
            }

            if (player.GetComponent<SpriteAnimator>() == null)
                Undo.AddComponent<SpriteAnimator>(player.gameObject);

            var driver = player.GetComponent<PlayerAnimationDriver>();
            if (driver == null)
                driver = Undo.AddComponent<PlayerAnimationDriver>(player.gameObject);

            var so = new SerializedObject(driver);
            so.FindProperty("animations").objectReferenceValue = set;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Swap the visible sprite to the new character's front idle.
            var idleFront = set.idle[0];
            if (idleFront != null && idleFront.frames.Length > 0)
                player.GetComponent<SpriteRenderer>().sprite = idleFront.frames[0];

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
    }
}
