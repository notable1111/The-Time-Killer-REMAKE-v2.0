// Menu: TimeKiller/Setup/33 - Generate Survivor Player Animations.
// Slices the PixelLab "Survivor" strips built by Tools/CharArt/build_sheets.py,
// builds SpriteAnimationClips (idle + run x 8 directions), fills the shared
// PlayerAnimationSet and attaches it -- the same contract Setup/5 fulfilled for
// the New_Leaf placeholder, so nothing downstream changes.
//
// Two things here are load-bearing and easy to get wrong:
//
// 1. PIVOT. PixelLab centres a small character inside a much larger padded
//    canvas (animations need headroom for raised limbs), so a plain centre
//    pivot would sink the character into the floor. We instead find the lowest
//    opaque pixel across every frame of a sheet -- the feet -- and place the
//    pivot GroundOffsetPixels above it. That is the exact offset the New_Leaf
//    placeholder used (its 48px cell had a 24px centre pivot and feet 7px off
//    the bottom => 17px), so the character stands where the collider already
//    expects and the swap is invisible to physics and pathfinding.
//
// 2. CELL SIZE is read from the texture rather than hardcoded, because
//    PixelLab's canvas grows with character size. Strips are one row of square
//    cells, so cell size == texture height.
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
    public static class SurvivorAnimationSetup
    {
        const string ArtRoot = "Assets/Resources/Assets/Characters/Survivor";
        const string ClipFolder = "Assets/Resources/C#/Player/Configs/Animations";
        const string SetPath = "Assets/Resources/C#/Player/Configs/PlayerAnimationSet.asset";
        const string Prefix = "Survivor";

        const int PixelsPerUnit = 32;

        // Feet sit this many pixels below the pivot. Matches the New_Leaf
        // placeholder exactly (24px centre pivot - 7px of empty cell = 17px),
        // which is what keeps the collider and nav grid valid after the swap.
        const int GroundOffsetPixels = 17;

        // Foot contacts per second at FULL speed, per gait.
        //
        // These are the numbers that decide how fast the legs turn over, and
        // they must DIFFER: the old formula (frames * 1.75, i.e. 3.5 steps/s)
        // was written when only run clips existed, and walking silently
        // inherited it -- so sneaking and sprinting turned their legs at
        // exactly the same rate while the body moved twice as fast.
        //
        // Chosen against the art rather than by taste. The side-view strips
        // depict a 0.41-unit walk stride and a 0.48-unit run stride, so
        // covering walkSpeed 2.2 / runSpeed 4.5 without sliding would need
        // 5.4 and 9.3 steps/s. That is a blur at run, so we take most of the
        // correction and stop short of it: sliding drops from ~35% to ~17%
        // (walk) and ~62% to ~35% (run), and the two gaits now read apart.
        const float WalkStepsPerSecond = 4.5f;
        const float RunStepsPerSecond = 6f;

        // Which cycle a clip depicts. Drives cadence, and nothing else.
        enum Gait { Idle, Walk, Run }

        [MenuItem("TimeKiller/Setup/33 - Generate Survivor Player Animations")]
        public static void Generate()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("33 - Generate Survivor Player Animations")) return;

            if (!Directory.Exists(ArtRoot))
            {
                Debug.LogError($"[TimeKiller Setup] No art at {ArtRoot}. Run Tools/CharArt/build_sheets.py first.");
                return;
            }

            var directions = System.Enum.GetNames(typeof(FacingDirection));
            Directory.CreateDirectory(ClipFolder);

            // Idle falls back to the single-frame rotations if the idle
            // animation is missing, so the player is never invisible.
            string idleSource = HasSheets("Idle", directions) ? "Idle" : "Rotation";
            string runSource = HasSheets("Run", directions) ? "Run" : idleSource;
            // Walk is optional: with no strips we leave the array empty and
            // PlayerAnimationSet.GetWalk falls back to the run clip.
            bool hasWalk = HasSheets("Walk", directions);
            // Repair likewise: without strips GetRepair falls back to idle, so a
            // clock repair still animates (as standing still) rather than break.
            bool hasRepair = HasSheets("Repair", directions);

            if (runSource != "Run")
                Debug.LogWarning("[TimeKiller Setup] No Run strips found -- running will reuse the idle pose.");
            if (!hasWalk)
                Debug.LogWarning("[TimeKiller Setup] No Walk strips found -- walking will reuse the run cycle slowed.");
            if (!hasRepair)
                Debug.LogWarning("[TimeKiller Setup] No Repair strips found -- clock repair will reuse the idle pose.");

            var sheets = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                sheets.Add(SheetPath(idleSource, directions[i]));
                sheets.Add(SheetPath(runSource, directions[i]));
                if (hasWalk) sheets.Add(SheetPath("Walk", directions[i]));
                if (hasRepair) sheets.Add(SheetPath("Repair", directions[i]));
            }
            SliceSheets(sheets.Distinct());

            var set = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PlayerAnimationSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            set.idle = new SpriteAnimationClip[8];
            set.run = new SpriteAnimationClip[8];
            set.walk = new SpriteAnimationClip[8];
            set.repair = new SpriteAnimationClip[8];
            for (int i = 0; i < 8; i++)
            {
                set.idle[i] = BuildClip(SheetPath(idleSource, directions[i]), $"Idle_{directions[i]}", Gait.Idle);
                set.run[i] = BuildClip(SheetPath(runSource, directions[i]), $"Run_{directions[i]}", Gait.Run);
                if (hasWalk)
                    set.walk[i] = BuildClip(SheetPath("Walk", directions[i]), $"Walk_{directions[i]}", Gait.Walk);
                // Repair is a stationary work loop: Gait.Idle keeps its feet
                // silent, so no phantom footsteps are broadcast to the maniac
                // while the player stands still fixing a clock.
                if (hasRepair)
                    set.repair[i] = BuildClip(SheetPath("Repair", directions[i]), $"Repair_{directions[i]}", Gait.Idle);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AttachToPlayer(set);
            Debug.Log($"[TimeKiller Setup] Survivor animations generated (idle='{idleSource}', run='{runSource}', "
                      + $"walk={(hasWalk ? "Walk" : "reuses run")}, repair={(hasRepair ? "Repair" : "reuses idle")}) and attached.");
        }

        static string SheetPath(string animation, string facing) =>
            $"{ArtRoot}/{Prefix}_{animation}_{facing}.png";

        static bool HasSheets(string animation, string[] directions) =>
            directions.All(d => File.Exists(SheetPath(animation, d)));

        static SpriteAnimationClip BuildClip(string sheetPath, string clipName, Gait gait)
        {
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
            if (gait == Gait.Idle)
            {
                clip.framesPerSecond = frames.Length > 1 ? 6f : 1f;
                clip.eventFrames = new int[0];
            }
            else
            {
                // One cycle = two foot contacts, so the cycle rate is half the
                // step rate. PlayerAnimationDriver then scales playback by actual
                // velocity against this gait's own reference speed, which is why
                // walk and run each need their own cadence here.
                float stepsPerSecond = gait == Gait.Run ? RunStepsPerSecond : WalkStepsPerSecond;
                clip.framesPerSecond = frames.Length * stepsPerSecond * 0.5f;
                clip.eventFrames = FootContactFrames(frames.Length);
            }
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // Two evenly spaced contacts, offset slightly into the cycle so the
        // sound lands on the down-step rather than the frame the clip starts on.
        static int[] FootContactFrames(int frameCount)
        {
            if (frameCount < 2) return new int[0];
            int quarter = Mathf.Max(1, frameCount / 4);
            return new[] { quarter, Mathf.Min(frameCount - 1, quarter + frameCount / 2) };
        }

        static void SliceSheets(IEnumerable<string> sheets)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();

            foreach (var path in sheets)
            {
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
                importer.isReadable = true; // needed to locate the feet
                importer.SaveAndReimport();

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int cell = texture.height;                       // one row of square cells
                int frames = Mathf.Max(1, texture.width / cell);
                float pivotY = PivotYFor(texture, cell);
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
                        rect = new Rect(i * cell, 0, cell, cell),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, pivotY)
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

        // Lowest opaque pixel across the whole strip = the feet at their lowest
        // point in the cycle. Using the whole strip (not per-frame) keeps every
        // frame on one consistent ground line, so the character never bobs
        // through the floor mid-animation.
        static float PivotYFor(Texture2D texture, int cell)
        {
            var pixels = texture.GetPixels32();
            int lowest = -1;
            for (int y = 0; y < texture.height && lowest < 0; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a > 8) { lowest = y; break; }
                }
            }

            if (lowest < 0)
            {
                Debug.LogWarning($"[TimeKiller Setup] {texture.name} is fully transparent; using centre pivot.");
                return 0.5f;
            }

            return Mathf.Clamp01((lowest + GroundOffsetPixels) / (float)cell);
        }

        static void AttachToPlayer(PlayerAnimationSet set)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[TimeKiller Setup] No Player in scene — open a level scene and re-run 33.");
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

            var idleFront = set.idle[0];
            if (idleFront != null && idleFront.frames.Length > 0)
                player.GetComponent<SpriteRenderer>().sprite = idleFront.frames[0];

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
    }
}
