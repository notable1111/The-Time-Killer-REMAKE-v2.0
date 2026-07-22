// Menu: TimeKiller/Setup/18 - Build LDtk Wing Scene (side-by-side).
// Builds a SEPARATE scene (CastleWingLDtk.unity) entirely from CastleWing.ldtk
// — the single source of truth for tiles, the Collision IntGrid (100% coverage
// rule) and the CameraZone entities (Cinemachine confiner bounds). The main
// scene with its protected hand-tuned hall colliders is NOT touched; we only
// switch over after this scene passes a 1:1 parity playtest.
//
// The hall's hand tuning still applies here: the exported snapshot
// (HallColliderSnapshot.json) is re-applied on a HallColliders object, layered
// on top of the IntGrid boxes (overlap is harmless; gaps are bugs).
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
    public static class LDtkWingSceneSetup
    {
        const string LdtkPath = "Assets/Resources/Assets/Maps/CastleWing.ldtk";
        const string WallTilePath = "Assets/Resources/C#/Environment/Configs/WallIntGridTile.asset";
        const string ScenePath = "Assets/Scenes/CastleWingLDtk.unity";
        const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // LDtk cell space is y-down with origin at the level's top-left. The
        // verified mapping to script-world coords: worldX = cx - 12,
        // worldY = 37 - cy — the map root is shifted so both agree (the exact
        // offset is computed from the imported Floor tilemap, not hardcoded).
        const float FloorWorldMinX = -10f, FloorWorldMinY = 0f;

        // Sorting orders copied from the script-built scene (Setup/9).
        static readonly (string name, int order)[] LayerOrders =
        {
            ("Floor", -20), ("Rug", -15), ("WallFace", -10),
            ("DecorMain", -9), ("DecorDeco", -9), ("Overhead", 10),
            ("Collision", -30),
        };

        [MenuItem("TimeKiller/Setup/18 - Build LDtk Wing Scene (side-by-side)")]
        public static void Build()
        {
            if (!ConfigureImporter()) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var stray = GameObject.Find("Directional Light");
            if (stray != null) Object.DestroyImmediate(stray); // 3D leftover, useless for URP 2D

            var mapRoot = InstantiateAndAlignMap();
            if (mapRoot == null) return;
            Restyle(mapRoot);

            // Hand-tuned hall boxes on top of the IntGrid colliders.
            var hallColliders = new GameObject("HallColliders");
            HallColliderGuard.ApplyTo(hallColliders);

            AddGlobalLight();

            // Full player pipeline, same as the main scene: base object (4),
            // New_Leaf 8-direction animations (5), footsteps (6), shadow (7).
            PlayerSetup.CreatePlayer();
            PlayerAnimationSetup.Generate();
            PlayerFootstepSetup.Setup();
            BlobShadowSetup.AddShadow();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = new Vector3(8f, 4f, 0f); // hall center, same as main scene
                StylePlayer(player);
            }

            var dressing = new GameObject("CastleWingDressing");
            CastleWingSetup.BuildWingDressing(dressing.transform, includeHallDressing: true);

            CameraV2Setup.Build();                 // brain + look-ahead target + confiner
            BuildCameraBoundsFromLdtk();           // then swap its bounds for the LDtk zones

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[TimeKiller Setup] LDtk wing scene built at {ScenePath}: tiles + collision + camera zones all driven by CastleWing.ldtk. Press Play and run the loop.");
        }

        // Re-run after any .ldtk reimport if tilemap materials come back unlit.
        [MenuItem("TimeKiller/Setup/18 - Restyle LDtk Map (after reimport)")]
        public static void RestyleMenu()
        {
            var root = GameObject.Find("CastleWingLDtkMap");
            if (root == null) { Debug.LogError("[TimeKiller Setup] CastleWingLDtkMap not found — is the LDtk scene open?"); return; }
            Restyle(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[TimeKiller Setup] LDtk map restyled (lit materials + hidden collision layer).");
        }

        // ---------- importer ----------

        static bool ConfigureImporter()
        {
            // The IntGrid tile asset: Grid collider = a full-cell physics box on
            // every Wall cell; the importer merges them via CompositeCollider2D.
            var wallTile = AssetDatabase.LoadAssetAtPath<LDtkIntGridTile>(WallTilePath);
            if (wallTile == null)
            {
                wallTile = ScriptableObject.CreateInstance<LDtkIntGridTile>();
                AssetDatabase.CreateAsset(wallTile, WallTilePath);
            }
            var tileSo = new SerializedObject(wallTile);
            tileSo.FindProperty("_colliderType").enumValueIndex = (int)Tile.ColliderType.Grid;
            tileSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            var importer = AssetImporter.GetAtPath(LdtkPath);
            if (importer == null)
            {
                Debug.LogError($"[TimeKiller Setup] No importer for {LdtkPath} — is LDtkToUnity installed?");
                return false;
            }

            var so = new SerializedObject(importer);
            so.FindProperty("_pixelsPerUnit").intValue = 16;
            so.FindProperty("_useCompositeCollider").boolValue = true;
            so.FindProperty("_intGridValueColorsVisible").boolValue = false;

            var values = so.FindProperty("_intGridValues");
            values.arraySize = 1;
            var element = values.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("_key").stringValue = "Collision_1";
            element.FindPropertyRelative("_asset").objectReferenceValue = wallTile;

            // Sorting orders live in the importer so they survive reimports.
            so.FindProperty("_useLayerCustomSortingOrders").boolValue = true;
            var orders = so.FindProperty("_layerCustomSortingOrders");
            orders.arraySize = LayerOrders.Length;
            for (int i = 0; i < LayerOrders.Length; i++)
            {
                var entry = orders.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_ldtkLayerName").stringValue = LayerOrders[i].name;
                entry.FindPropertyRelative("_ldtkLayerOrder").intValue = LayerOrders[i].order;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            return true;
        }

        // ---------- map ----------

        static GameObject InstantiateAndAlignMap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LdtkPath);
            if (prefab == null)
            {
                Debug.LogError($"[TimeKiller Setup] Import produced no prefab for {LdtkPath} — check the console for LDtk import errors.");
                return null;
            }
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.name = "CastleWingLDtkMap";

            // Align: the Floor tilemap's min corner must land at the same world
            // position as the script-built wing's floor min (chapel corner).
            // LDtkToUnity puts the Tilemap on a child named "Tiles" under each
            // layer object, so we match by the LAYER (parent) name.
            var floor = root.GetComponentsInChildren<Tilemap>(true)
                .FirstOrDefault(t => LayerNameOf(t) == "Floor");
            if (floor == null)
            {
                Debug.LogError("[TimeKiller Setup] No Floor tilemap inside the LDtk prefab — aborting alignment.");
                return root;
            }
            floor.CompressBounds();
            var currentMin = floor.CellToWorld(floor.cellBounds.min);
            root.transform.position += new Vector3(FloorWorldMinX, FloorWorldMinY, 0f) - currentMin;
            return root;
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

        // LDtk layer name for an imported tilemap: the "Tiles" object's parent
        // ("Floor/Tiles" -> "Floor"); falls back to the component's own name.
        static string LayerNameOf(Component c) =>
            c.name == "Tiles" && c.transform.parent != null ? c.transform.parent.name : c.name;

        // ---------- lighting / player ----------

        static void AddGlobalLight()
        {
            var global = new GameObject("GlobalLight");
            var light = global.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = new Color(0.42f, 0.47f, 0.58f); // cold moonlit ambient (matches Setup/11)
            light.intensity = 0.32f;
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
                glow.intensity = 0.65f;
                glow.pointLightInnerRadius = 0.3f;
                glow.pointLightOuterRadius = 2.6f;
                glow.falloffIntensity = 0.9f;
            }
        }

        // ---------- camera bounds ----------

        static void BuildCameraBoundsFromLdtk()
        {
            var json = LdtkJson.FromJson(File.ReadAllBytes(LdtkPath));
            var level = json.Levels[0];
            var entities = level.LayerInstances.First(l => l.Identifier == "Entities").EntityInstances
                .Where(e => e.Identifier == "CameraZone").ToArray();
            if (entities.Length == 0)
            {
                Debug.LogError("[TimeKiller Setup] No CameraZone entities in the LDtk file — confiner keeps CameraV2Setup's hall-only bounds.");
                return;
            }

            var boundsGo = GameObject.Find("CameraBounds");
            if (boundsGo == null) boundsGo = new GameObject("CameraBounds");
            foreach (var col in boundsGo.GetComponents<Collider2D>()) Object.DestroyImmediate(col);
            var body = boundsGo.GetComponent<Rigidbody2D>();
            if (body == null) body = boundsGo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            float levelCellHeight = level.PxHei / 16f;
            foreach (var e in entities)
            {
                // px is the zone's top-left in LDtk space; convert to script-world.
                float w = e.Width / 16f, h = e.Height / 16f;
                float xMin = e.Px[0] / 16f - 12f;
                float yMax = levelCellHeight - e.Px[1] / 16f - 3f;
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
            Debug.Log($"[TimeKiller Setup] Camera bounds rebuilt from {entities.Length} LDtk CameraZone entities.");
        }
    }
}
