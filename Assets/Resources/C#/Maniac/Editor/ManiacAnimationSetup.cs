// Menu: TimeKiller/Setup/21 - Generate Maniac Animations.
// Slices the killer sheets (maranza pack, Pacient) into a 32x32 grid and builds
// the clip set: 8 walk cycles (rows 0-7, counter-clockwise from Down — same
// order as FacingDirection), breathing idles, the death clip (row 8), and the
// attack lunge.
// Credit: character sprites by Maranza (maranza.itch.io) — see pack CREDITS.txt.
//
// TWO SHEETS, ON PURPOSE. The body comes from stage_two, as decided 2026-07-22.
// The lunge comes from stage_three row 8 — a blood-soaked rearing pose with red
// eyes that shipped in the pack and had never been referenced by anything, while
// AttackState ran with no visual at all. stage_three is NOT used wholesale: its
// sheet measures mean value 158.9 against stage_two's 153.4, so adopting it
// everywhere would have made the "he is too bright for this castle" problem
// slightly worse rather than better.
//
// SPRITE IDS ARE PRESERVED ACROSS RE-RUNS, and that is not a nicety. This script
// used to call GUID.Generate() for every rect on every run, so a second run
// invalidated every sprite reference in the project. It rebuilt the clips and
// re-pointed the OPEN scene, which hid the damage — but stage_two sprites are
// also referenced by Catacombs.unity, which it never touches. Re-running it
// would have silently left that scene's maniac with a missing sprite.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Core;
using TimeKiller.EditorTools;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class ManiacAnimationSetup
    {
        const string SheetPath = "Assets/Resources/Outsource/ManiacPack/Pacient/stage_two.png";
        const string AttackSheetPath = "Assets/Resources/Outsource/ManiacPack/Pacient/stage_three.png";
        const string ClipFolder = "Assets/Resources/C#/Maniac/Configs/Animations";
        const string SetPath = "Assets/Resources/C#/Maniac/Configs/ManiacAnimationSet.asset";
        const int Cell = 32, PixelsPerUnit = 32, WalkFrames = 4, DirectionRows = 8, DeathRow = 8;

        // stage_three row 8 is four frames: three of him rearing up, then one of
        // him crumpling. Only the first three are the lunge; the fourth belongs to
        // the death sequence that continues on row 9.
        const int AttackRow = 8, AttackFrames = 3;

        // The breath is one pixel. At 32 PPU with Point filtering there is no such
        // thing as half a pixel of motion — a sub-pixel bob is shimmer, not
        // breathing — so the "up" frame is the SAME art with its pivot moved down
        // one pixel, which draws it one pixel higher. No new texture, no transform
        // to animate (his SpriteRenderer shares a GameObject with the Rigidbody2D
        // and the collider, so moving it would move his physics).
        const float LiftPixels = 1f;

        [MenuItem("TimeKiller/Setup/21 - Generate Maniac Animations")]
        public static void Generate()
        {
            if (SetupGuard.Blocked("21 - Generate Maniac Animations")) return;

            if (AssetImporter.GetAtPath(SheetPath) == null)
            {
                Debug.LogError($"[TimeKiller Setup] Killer sheet missing: {SheetPath} — import the maranza pack first.");
                return;
            }

            SliceSheet(SheetPath, "maniac", withLiftFrames: true);

            bool hasAttackSheet = AssetImporter.GetAtPath(AttackSheetPath) != null;
            if (hasAttackSheet) SliceSheet(AttackSheetPath, "maniac3", withLiftFrames: false);
            else Debug.LogWarning($"[TimeKiller Setup] {AttackSheetPath} missing — the attack lunge will be skipped and AttackState stays visually silent.");

            Directory.CreateDirectory(ClipFolder);

            var frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                .ToDictionary(s => s.name);
            if (hasAttackSheet)
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(AttackSheetPath).OfType<Sprite>())
                    frames[sprite.name] = sprite;

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
                var rowFrames = RowFrames(frames, "maniac", row, WalkFrames);
                set.walk[row] = BuildClip($"ManiacWalk_{directions[row]}", rowFrames, fps: 9f, loop: true);

                // Standing used to be a single frame at 1 fps — he froze solid the
                // instant he stopped, which is the loudest "asset pack" tell there
                // is. Two frames: the pose, and the pose one pixel higher.
                var still = rowFrames[0];
                var lifted = frames.TryGetValue($"maniac_r{row}_lift", out var up) ? up : still;
                set.idle[row] = BuildClip($"ManiacIdle_{directions[row]}",
                    new[] { still, lifted }, fps: set.idleBreathFps, loop: true);
            }

            set.death = BuildClip("ManiacDeath", RowFrames(frames, "maniac", DeathRow, WalkFrames), fps: 6f, loop: false);

            if (hasAttackSheet)
            {
                var lunge = RowFrames(frames, "maniac3", AttackRow, AttackFrames);
                // Fast in, then hold. Anticipation and a held extreme is how a
                // 29-pixel sprite sells force — you cannot add detail, only timing.
                set.attack = BuildClip("ManiacAttack", lunge, fps: 12f, loop: false);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AttachToManiac(set, frames);
            Debug.Log($"[TimeKiller Setup] Maniac animations generated: 8 walk cycles + breathing idles + death" +
                      $"{(hasAttackSheet ? " + attack lunge (stage_three r8)" : " (NO attack — sheet missing)")}.");
        }

        static Sprite[] RowFrames(Dictionary<string, Sprite> frames, string prefix, int row, int count)
        {
            var result = new List<Sprite>();
            for (int col = 0; col < count; col++)
                if (frames.TryGetValue($"{prefix}_r{row}_{col}", out var sprite))
                    result.Add(sprite);
            if (result.Count == 0)
                Debug.LogError($"[TimeKiller Setup] No frames for {prefix} row {row} — was the sheet sliced?");
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

        /// Re-slices a sheet into a named grid. Existing sprite IDs are reused by
        /// NAME so every reference in every scene and clip survives a re-run.
        static void SliceSheet(string sheetPath, string prefix, bool withLiftFrames)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            int cols = texture.width / Cell, rows = texture.height / Cell;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Keep the IDs we already handed out; only mint one for a genuinely new name.
            var known = new Dictionary<string, GUID>();
            foreach (var existing in provider.GetSpriteRects())
                known[existing.name] = existing.spriteID;
            GUID IdFor(string name) => known.TryGetValue(name, out var id) ? id : GUID.Generate();

            var rects = new List<SpriteRect>();
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    string name = $"{prefix}_r{row}_{col}";
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = IdFor(name),
                        // texture y=0 is the BOTTOM; sheet rows count from the top
                        rect = new Rect(col * Cell, texture.height - (row + 1) * Cell, Cell, Cell),
                        // SpriteRect.alignment is a SpriteAlignment, not an int. The
                        // old (int) cast here compiled only because Center == 0 and C#
                        // implicitly converts the constant 0 to any enum; the same cast
                        // on Custom (9) does not compile.
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                    });
                }

            if (withLiftFrames)
            {
                // Same pixels, pivot one pixel lower => the art draws one pixel
                // higher. This is the whole breathing idle.
                for (int row = 0; row < System.Math.Min(DirectionRows, rows); row++)
                {
                    string name = $"{prefix}_r{row}_lift";
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = IdFor(name),
                        rect = new Rect(0, texture.height - (row + 1) * Cell, Cell, Cell),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, 0.5f - LiftPixels / Cell),
                    });
                }
            }

            // Replaces the whole list, so anything no longer named here is dropped.
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

            // The driver tints him at runtime; in the editor he should show the
            // tone he will actually be drawn with, or the scene view lies.
            var renderer = maniac.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = set.bodyTint;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(maniac.gameObject.scene);
        }
    }
}
