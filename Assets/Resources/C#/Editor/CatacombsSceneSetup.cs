// Menu: TimeKiller/Setup/30 - Build Catacombs Scene.
// Builds Catacombs.unity entirely from the "Catacombs" level inside
// CastleWing.ldtk — the single source of truth for tiles, the Collision IntGrid
// (100% coverage rule) and the CameraZone entities.
//
// The .ldtk project holds TWO levels, so the imported prefab contains both.
// This scene unpacks the instance and deletes the CastleWing level, leaving a
// plain hierarchy that cannot be silently reverted by a reimport. Consequence:
// after editing the map, RE-RUN this menu item — the scene does not auto-update.
//
// Map authoring lives in Tools/MapPipeline/mapv3_catacombs.py, which also emits
// CatacombsRooms.json with WORLD-space room rectangles. Gameplay placement
// (Setup/31) reads that file instead of hard-coded coordinates, so nothing can
// spawn inside a wall.
using System.IO;
using System.Linq;
using LDtkUnity;
using TimeKiller.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TimeKiller.EditorTools
{
    public static class CatacombsSceneSetup
    {
        public const string LdtkPath = "Assets/Resources/Assets/Maps/CastleWing.ldtk";
        public const string ScenePath = "Assets/Scenes/Catacombs.unity";
        public const string LevelName = "Catacombs";
        public const string MapRootName = "CatacombsMap";
        const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // The Floor tilemap's min cell corner is pinned here. CatacombsRooms.json
        // exports its world rects against exactly this anchor — keep them in sync.
        public const float FloorWorldMinX = 0f, FloorWorldMinY = 0f;

        [MenuItem("TimeKiller/Setup/30 - Build Catacombs Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var stray = GameObject.Find("Directional Light");
            if (stray != null) Object.DestroyImmediate(stray); // 3D leftover, useless for URP 2D

            var mapRoot = InstantiateCatacombsOnly();
            if (mapRoot == null) return;
            Restyle(mapRoot);
            AddGlobalLight();

            // Same player pipeline as the castle scene, so the two levels play
            // identically: base object (4), animations (5), footsteps (6),
            // shadow (7), health (19), vfx (22), effects (23), audio (24).
            PlayerSetup.CreatePlayer();
            PlayerAnimationSetup.Generate();
            PlayerFootstepSetup.Setup();
            BlobShadowSetup.AddShadow();
            TimeKiller.Player.EditorTools.PlayerHealthSetup.Setup();
            TimeKiller.HealthVfx.EditorTools.HealthVfxSetup.Build();
            TimeKiller.Effects.EditorTools.EffectsSetup.Build();
            TimeKiller.Audio.EditorTools.AudioSetup.Build();

            var rooms = CatacombsRooms.Load();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = rooms.Center("stair_hall");
                StylePlayer(player);
            }

            CameraV2Setup.Build();          // brain + look-ahead target + confiner
            BuildCameraBoundsFromLdtk();    // then swap its bounds for the LDtk zones

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[TimeKiller Setup] Catacombs scene built at {ScenePath}. Run Setup/31 to place the escape loop.");
        }

        // Re-run if tilemap materials come back unlit after a reimport.
        [MenuItem("TimeKiller/Setup/30 - Restyle Catacombs Map")]
        public static void RestyleMenu()
        {
            var root = GameObject.Find(MapRootName);
            if (root == null) { Debug.LogError($"[TimeKiller Setup] {MapRootName} not found — is the Catacombs scene open?"); return; }
            Restyle(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[TimeKiller Setup] Catacombs map restyled.");
        }

        // ---------- map ----------

        static GameObject InstantiateCatacombsOnly()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LdtkPath);
            if (prefab == null)
            {
                Debug.LogError($"[TimeKiller Setup] Import produced no prefab for {LdtkPath} — check the console for LDtk import errors.");
                return null;
            }
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.name = MapRootName;

            // Unpack so the other level can be removed; a prefab instance would
            // not allow deleting a child, and a reimport could restore it.
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var level = FindDeep(root.transform, LevelName);
            if (level == null)
            {
                Debug.LogError($"[TimeKiller Setup] No '{LevelName}' level inside the LDtk prefab — did mapv3_catacombs.py run and the .ldtk reimport succeed?");
                return null;
            }
            // Drop every other level so this scene contains the catacombs only.
            foreach (var other in root.GetComponentsInChildren<LDtkComponentLevel>(true)
                         .Where(l => l.gameObject != level.gameObject).ToArray())
                Object.DestroyImmediate(other.gameObject);

            var floor = root.GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(t => LayerNameOf(t) == "Floor");
            if (floor == null)
            {
                Debug.LogError("[TimeKiller Setup] No Floor tilemap in the Catacombs level — aborting alignment.");
                return root;
            }
            floor.CompressBounds();
            var currentMin = floor.CellToWorld(floor.cellBounds.min);
            root.transform.position += new Vector3(FloorWorldMinX, FloorWorldMinY, 0f) - currentMin;
            return root;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var hit = FindDeep(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        static void Restyle(GameObject mapRoot)
        {
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
            foreach (var r in mapRoot.GetComponentsInChildren<TilemapRenderer>(true))
            {
                if (LayerNameOf(r) == "Collision")
                {
                    r.enabled = false; // physics only, never rendered
                    continue;
                }
                if (lit != null) r.sharedMaterial = lit;
            }
        }

        // LDtkToUnity puts the Tilemap on a child named "Tiles"/"IntGrid" under
        // each layer object, so the LDtk layer name is the parent's name.
        static string LayerNameOf(Component c) =>
            (c.name == "Tiles" || c.name == "IntGrid") && c.transform.parent != null
                ? c.transform.parent.name : c.name;

        // ---------- lighting / player ----------

        static void AddGlobalLight()
        {
            var global = new GameObject("GlobalLight");
            var light = global.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            // Colder and dimmer than the castle wing (0.32): underground, no moon.
            light.color = new Color(0.34f, 0.40f, 0.52f);
            light.intensity = 0.20f;
        }

        static void StylePlayer(PlayerController player)
        {
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
            foreach (var r in player.GetComponentsInChildren<SpriteRenderer>(true))
                if (lit != null) r.sharedMaterial = lit;
            if (player.GetComponent<UnityEngine.Rendering.SortingGroup>() == null)
                player.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();

            if (player.GetComponentInChildren<Light2D>() == null)
            {
                var glowGo = new GameObject("PlayerGlow");
                glowGo.transform.SetParent(player.transform, false);
                var glow = glowGo.AddComponent<Light2D>();
                glow.lightType = Light2D.LightType.Point;
                glow.color = new Color(1f, 0.82f, 0.6f);
                glow.intensity = 0.75f;   // the catacombs are darker than the wing
                glow.pointLightInnerRadius = 0.3f;
                glow.pointLightOuterRadius = 3.0f;
                glow.falloffIntensity = 0.9f;
            }
        }

        // ---------- camera bounds ----------

        static void BuildCameraBoundsFromLdtk()
        {
            var json = LdtkJson.FromJson(File.ReadAllBytes(LdtkPath));
            var level = json.Levels.FirstOrDefault(l => l.Identifier == LevelName);
            if (level == null) { Debug.LogError($"[TimeKiller Setup] '{LevelName}' level missing from the .ldtk."); return; }

            var entities = level.LayerInstances.First(l => l.Identifier == "Entities").EntityInstances
                .Where(e => e.Identifier == "CameraZone").ToArray();
            if (entities.Length == 0)
            {
                Debug.LogError("[TimeKiller Setup] No CameraZone entities in the Catacombs level — confiner keeps its default bounds.");
                return;
            }

            var boundsGo = GameObject.Find("CameraBounds");
            if (boundsGo == null) boundsGo = new GameObject("CameraBounds");
            foreach (var col in boundsGo.GetComponents<Collider2D>()) Object.DestroyImmediate(col);
            var body = boundsGo.GetComponent<Rigidbody2D>();
            if (body == null) body = boundsGo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            // Anchored on the Floor layer's min/max cells — the same reference
            // the map alignment uses — so map growth can't desync the two.
            var floorTiles = level.LayerInstances.First(l => l.Identifier == "Floor").GridTiles;
            int floorCxMin = floorTiles.Min(t => t.Px[0]) / 16;
            int floorCyMax = floorTiles.Max(t => t.Px[1]) / 16;

            foreach (var e in entities)
            {
                float w = e.Width / 16f, h = e.Height / 16f;
                float xMin = FloorWorldMinX + (e.Px[0] / 16f - floorCxMin);
                float yMax = FloorWorldMinY + (floorCyMax + 1 - e.Px[1] / 16f);
                var box = boundsGo.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.offset = new Vector2(xMin + w / 2f, yMax - h / 2f);
                box.size = new Vector2(w, h);
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            var composite = boundsGo.AddComponent<CompositeCollider2D>();
            composite.isTrigger = true;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var confiner = Object.FindAnyObjectByType<CinemachineConfiner2D>();
            if (confiner != null)
            {
                confiner.BoundingShape2D = composite;
                confiner.InvalidateBoundingShapeCache();
                EditorUtility.SetDirty(confiner);
            }
            Debug.Log($"[TimeKiller Setup] Camera bounds rebuilt from {entities.Length} Catacombs CameraZone entities.");
        }
    }
}
