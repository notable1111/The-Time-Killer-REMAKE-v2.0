// Menu: TimeKiller/Setup/20 - Create Maniac In Scene.
// Creates ManiacConfig.asset if missing, builds the Maniac object (physics +
// perception + AI) and the patrol route through the wing's main loop. The
// servant passage is deliberately NOT on the route — the player's blind spot.
//
// SPRITES: refuses to run without the maranza pack imported (no invisible
// enemies, no placeholder art — project rule). The killer look (decided
// 2026-07-22): Pacient stage_two — bandaged bloody madman. Setup/21 slices
// the sheet and wires the full animation set after this runs.
// Credit: character sprites by Maranza (maranza.itch.io).
using System.IO;
using System.Linq;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class ManiacSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";
        const string PackFolder = "Assets/Resources/Outsource/ManiacPack";
        const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // Patrol loop through the wing (script-world coords): hall -> east
        // corridor -> guardroom -> B-C corridor -> great chamber -> C-D
        // corridor -> chapel -> D-hall corridor -> west corridor -> hall.
        static readonly Vector2[] RouteWaypoints =
        {
            new Vector2(8f, 5f),      // hall center
            new Vector2(14f, 5.5f),   // hall east side
            new Vector2(21f, 5.5f),   // east corridor
            new Vector2(30f, 5f),     // guardroom
            new Vector2(31.5f, 15f),  // B-C corridor (dark)
            new Vector2(31f, 24f),    // great chamber east
            new Vector2(24f, 25f),    // great chamber west
            new Vector2(12f, 23.5f),  // C-D corridor
            new Vector2(-5f, 24f),    // chapel
            new Vector2(-5.5f, 13f),  // D-hall corridor (dark)
            new Vector2(-5f, 5.5f),   // west corridor
            new Vector2(2f, 5f),      // hall west side
        };

        [MenuItem("TimeKiller/Setup/20 - Create Maniac In Scene")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("20 - Create Maniac In Scene")) return;

            var standIn = FindPackSprite();
            if (standIn == null)
            {
                Debug.LogError($"[TimeKiller Setup] Maniac pack not found under {PackFolder} — import the maranza pack first (no invisible enemies, no placeholder art). Nothing was created.");
                return;
            }
            if (Object.FindAnyObjectByType<PlayerController>() == null)
            {
                Debug.LogError("[TimeKiller Setup] No Player in scene — the maniac needs prey. Run Setup/4 (or 18) first.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<ManiacConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var existing = Object.FindAnyObjectByType<ManiacController>();
            if (existing != null)
            {
                Debug.LogWarning("[TimeKiller Setup] A Maniac already exists in the scene — nothing created.");
                return;
            }

            // Patrol route with waypoint children.
            var routeGo = new GameObject("ManiacPatrolRoute");
            Undo.RegisterCreatedObjectUndo(routeGo, "Create Maniac");
            var route = routeGo.AddComponent<ManiacPatrolRoute>();
            for (int i = 0; i < RouteWaypoints.Length; i++)
            {
                var wp = new GameObject($"WP{i}");
                wp.transform.SetParent(routeGo.transform, false);
                wp.transform.position = RouteWaypoints[i];
            }

            // The maniac himself.
            var maniac = new GameObject("Maniac");
            Undo.RegisterCreatedObjectUndo(maniac, "Create Maniac");
            maniac.transform.position = RouteWaypoints[5]; // spawn far from the player (great chamber)

            var body = maniac.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = maniac.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.6f, 0.45f);   // feet-only, like the player
            collider.offset = new Vector2(0f, -0.35f);

            var renderer = maniac.AddComponent<SpriteRenderer>();
            renderer.sprite = standIn;
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
            if (lit != null) renderer.sharedMaterial = lit;
            maniac.AddComponent<UnityEngine.Rendering.SortingGroup>(); // Y-depth sorts vs player/props

            maniac.AddComponent<ManiacMotor>();
            maniac.AddComponent<ManiacPerception>();
            maniac.AddComponent<ManiacBreadcrumbs>();
            var controller = maniac.AddComponent<ManiacController>();
            controller.Init(config, route);
            var so = new SerializedObject(controller);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("route").objectReferenceValue = route;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(maniac.scene);
            Debug.Log($"[TimeKiller Setup] Maniac created (stand-in sprite: {standIn.name}). Patrols the wing loop; servant passage is his blind spot. Next: the slicing/animation pass once you pick his final look from the pack.");
        }

        static Sprite FindPackSprite()
        {
            if (!AssetDatabase.IsValidFolder(PackFolder)) return null;
            return AssetDatabase.FindAssets("t:Texture2D", new[] { PackFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Sprite>()
                .FirstOrDefault();
        }
    }
}
