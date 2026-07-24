// Menu: TimeKiller/Setup/28 - Setup Clock Objectives (win loop).
// Creates ClockConfig, sets the clock sprite import (pixel-perfect, bottom
// pivot), seeds 3 clocks + an exit door in the scene, and wires the manager,
// HUD and the player's ClockRepair. PLACEMENT IS HAND-TUNED: if a "Clocks"
// object already exists this REFUSES to rebuild — drag the clocks/exit freely
// (same protection class as HidingSpots + Furniture).
using System.IO;
using TimeKiller.Objectives;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Objectives.EditorTools
{
    public static class ClocksSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Objectives/Configs/ClockConfig.asset";
        const string BrokenPath = "Assets/Resources/Assets/Objectives/Clock/Clock_Broken.png";
        const string FixedPath = "Assets/Resources/Assets/Objectives/Clock/Clock_Fixed.png";
        const string DoorLockedPath = "Assets/Resources/Assets/Objectives/Door/Door_Locked.png";
        const string DoorOpenPath = "Assets/Resources/Assets/Objectives/Door/Door_Open.png";
        const string WindLoopPath = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/Soundtracks/Ambience/AMBIENCE WIND.wav";
        const string CreakPath = "Assets/Resources/Outsource/Audio/EchoChambersAmbience/OneShots/Door Creak4_1.wav";

        // HAND-PLACED 2026-07-24 (three far corners, each beside a wardrobe):
        //   chapel NW · kitchen S · library NE. Gate sits on the west corridor's
        //   north wall — the only solid 2-cell segment there — visible from spawn.
        static readonly Vector2[] ClockSpots =
        {
            new Vector2(-7f, 29.45f),   // chapel, north wall, west of the wardrobe
            new Vector2(37f, -6.5f),    // kitchen, east wall, between firewood and block
            new Vector2(46.5f, 20.8f),  // library, south side, between bookstack and sidetable
        };
        static readonly Vector2 ExitSpot = new Vector2(-3f, 6.45f);

        [MenuItem("TimeKiller/Setup/28 - Setup Clock Objectives (win loop)")]
        public static void Build()
        {
            if (GameObject.Find("Clocks") != null)
            {
                Debug.LogWarning("[TimeKiller Setup] 'Clocks' already exists — placement is HAND-TUNED, refusing to rebuild. Delete it manually to reseed.");
                return;
            }

            var config = LoadOrCreateConfig();
            var broken = ImportClockSprite(BrokenPath);
            var fixedS = ImportClockSprite(FixedPath);
            if (broken == null || fixedS == null)
            {
                Debug.LogError("[TimeKiller Setup] Clock sprites not found — expected Clock_Broken.png / Clock_Fixed.png under Assets/Resources/Assets/Objectives/Clock/.");
                return;
            }

            var root = new GameObject("Clocks");
            Undo.RegisterCreatedObjectUndo(root, "Clock Objectives");

            for (int i = 0; i < ClockSpots.Length; i++)
                BuildClock(root.transform, i, ClockSpots[i], broken, fixedS, config);

            BuildExit(root.transform, ExitSpot);
            var manager = BuildManager(root.transform);
            WirePlayer(config, manager);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[TimeKiller Setup] Clock objectives ready: {ClockSpots.Length} clocks + exit + manager/HUD. Fix all clocks (E + Space skill-checks) to open the exit and escape. Drag the clocks/exit to taste.");
        }

        static void BuildClock(Transform parent, int i, Vector2 pos, Sprite broken, Sprite fixedS, ClockConfig config)
        {
            var go = new GameObject($"Clock_{i}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = broken;

            // blocking collider around the base so you can't walk through it
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.6f, 0.7f);
            col.offset = new Vector2(0f, 0.35f);

            // green glow light (off until fixed)
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.45f, 1f, 0.4f);
            light.intensity = 1.1f;
            light.pointLightOuterRadius = 4.5f;
            light.pointLightInnerRadius = 0.6f;
            light.enabled = false;
            NoShadows(light); // no ShadowCaster2D in this scene — shadows are pure overhead

            var clock = go.AddComponent<ClockObjective>();
            var so = new SerializedObject(clock);
            so.FindProperty("brokenSprite").objectReferenceValue = broken;
            so.FindProperty("fixedSprite").objectReferenceValue = fixedS;
            so.FindProperty("glow").objectReferenceValue = light;
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The gate is wall-mounted: bottom-pivot sprite standing on the last floor
        // row, trigger hugging the threshold, thin blocker so "locked" is felt.
        static void BuildExit(Transform parent, Vector2 pos)
        {
            var go = new GameObject("ExitDoor");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var locked = ImportDoorSprite(DoorLockedPath);
            var opened = ImportDoorSprite(DoorOpenPath);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = locked;
            var lit = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (lit != null) sr.sharedMaterial = lit;
            go.AddComponent<UnityEngine.Rendering.SortingGroup>(); // Y-sort by the threshold

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.8f, 1.2f);
            trigger.offset = new Vector2(0f, 0.6f);

            var block = go.AddComponent<BoxCollider2D>();
            block.isTrigger = false;
            block.size = new Vector2(1.8f, 0.35f);
            block.offset = new Vector2(0f, 0.18f);

            // moonlight pouring through the opening — the "you can leave now" beacon
            var lightGo = new GameObject("Moonlight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.68f, 0.82f, 1f);
            light.intensity = 1.4f;
            light.pointLightOuterRadius = 6f;
            light.pointLightInnerRadius = 0.8f;
            light.enabled = false;
            NoShadows(light);

            // 3D wind loop: once the gate is open this is how you find your way
            // back to it across a dark map. Silent until then.
            var windGo = new GameObject("Wind");
            windGo.transform.SetParent(go.transform, false);
            windGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            var wind = windGo.AddComponent<AudioSource>();
            wind.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WindLoopPath);
            wind.loop = true;
            wind.playOnAwake = false;
            wind.spatialBlend = 1f;                 // fully positional
            wind.rolloffMode = AudioRolloffMode.Linear;
            wind.minDistance = 3f;
            wind.maxDistance = 26f;                 // audible from most of the west half
            wind.volume = 0.55f;

            var exit = go.AddComponent<ExitDoor>();
            var so = new SerializedObject(exit);
            so.FindProperty("wind").objectReferenceValue = wind;
            so.FindProperty("creak").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(CreakPath);
            so.FindProperty("block").objectReferenceValue = block;
            so.FindProperty("sr").objectReferenceValue = sr;
            so.FindProperty("lockedSprite").objectReferenceValue = locked;
            so.FindProperty("openSprite").objectReferenceValue = opened;
            so.FindProperty("glow").objectReferenceValue = light;
            so.FindProperty("lockedMat").objectReferenceValue = lit;
            so.FindProperty("openMat").objectReferenceValue = unlit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 64 PPU -> the gate is 2 x 3 world units (exactly the 2-cell wall segment
        // it hangs on). Pivot sits on the art's last opaque row, not the canvas edge.
        static Sprite ImportDoorSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogError($"[TimeKiller Setup] Missing door sprite {path}"); return null; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 64f;
            var pivot = new Vector2(0.5f, 0.109f); // 21px of empty canvas below the art
            importer.spritePivot = pivot;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Light2D defaults 2D shadows ON; with no ShadowCaster2D in the scene that
        // is a per-light render pass drawing nothing. Turn it off at creation.
        static void NoShadows(Light2D light)
        {
            var so = new SerializedObject(light);
            var prop = so.FindProperty("m_ShadowsEnabled");
            if (prop != null) { prop.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static ObjectiveManager BuildManager(Transform parent)
        {
            var go = new GameObject("ObjectiveManager");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<ObjectiveManager>();
            go.AddComponent<ObjectiveHUD>(); // hud.repair wired in WirePlayer
            return manager;
        }

        static void WirePlayer(ClockConfig config, ObjectiveManager manager)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) { Debug.LogWarning("[TimeKiller Setup] No PlayerController — ClockRepair not attached."); return; }

            var repair = player.GetComponent<ClockRepair>();
            if (repair == null) repair = Undo.AddComponent<ClockRepair>(player.gameObject);
            var rso = new SerializedObject(repair);
            rso.FindProperty("config").objectReferenceValue = config;
            rso.ApplyModifiedPropertiesWithoutUndo();

            var hud = manager.GetComponent<ObjectiveHUD>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("repair").objectReferenceValue = repair;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }

        static ClockConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ClockConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<ClockConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        // Pixel-perfect import: point filter, ~48 PPU (clock ~2.3 units tall),
        // pivot at the base so it Y-sorts and sits on the floor.
        static Sprite ImportClockSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 48f;
            importer.spritePivot = new Vector2(0.5f, 0.06f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0.06f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
