// Menu: TimeKiller/Setup/39 - Setup Blood Trail.
//
// Creates BloodConfig.asset if missing, puts a BloodStainField object in the
// scene (mesh renderer + the droplet atlas material) and a BloodTrail on the
// Player. Nothing else in the game changes: BloodTrail publishes
// BloodSpilledEvent, BloodStainField listens, and neither knows about the other.
//
// Safe to re-run: every object is found-or-created and re-wired in place, never
// destroyed and rebuilt, so it cannot discard a hand-tuned scene.
// Removing the feature = delete the BloodStains object and the BloodTrail
// component; nothing else references either.
using System.IO;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Blood.EditorTools
{
    public static class BloodSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Blood/Configs/BloodConfig.asset";
        const string MaterialPath = "Assets/Resources/Assets/Effects/BloodDroplet.mat";
        const string AtlasPath = "Assets/Resources/Assets/Effects/blood_droplets_atlas.png";
        const int AtlasColumns = 3, AtlasRows = 2;

        [MenuItem("TimeKiller/Setup/39 - Setup Blood Trail")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("39 - Setup Blood Trail")) return;

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[TimeKiller Setup] No PlayerController in the scene — run Setup/4 first. Nothing was created.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<BloodConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<BloodConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log($"[TimeKiller Setup] Created {ConfigPath}");
            }

            // Shares the particle burst's material on purpose: same atlas, same
            // unlit transparent shader, and one less asset to keep in sync when
            // the droplet art is redrawn.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError($"[TimeKiller Setup] {MaterialPath} missing — run Setup/23 first (it builds the blood material).");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath) == null)
                Debug.LogWarning($"[TimeKiller Setup] {AtlasPath} missing — stains will draw untextured. Run Tools/VfxPipeline/draw_droplets.py.");

            var fieldGo = GameObject.Find("BloodStains");
            if (fieldGo == null)
            {
                fieldGo = new GameObject("BloodStains");
                Undo.RegisterCreatedObjectUndo(fieldGo, "Blood Stains");
            }
            fieldGo.transform.position = Vector3.zero;   // stains are authored in world space

            var filter = Ensure<MeshFilter>(fieldGo);
            var renderer = Ensure<MeshRenderer>(fieldGo);
            renderer.sharedMaterial = material;
            renderer.sortingOrder = config.sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var field = Ensure<BloodStainField>(fieldGo);
            var fso = new SerializedObject(field);
            fso.FindProperty("config").objectReferenceValue = config;
            fso.FindProperty("atlasColumns").intValue = AtlasColumns;
            fso.FindProperty("atlasRows").intValue = AtlasRows;
            fso.ApplyModifiedPropertiesWithoutUndo();

            var trail = player.GetComponent<BloodTrail>();
            if (trail == null) trail = Undo.AddComponent<BloodTrail>(player.gameObject);
            var tso = new SerializedObject(trail);
            tso.FindProperty("config").objectReferenceValue = config;
            tso.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(field);
            EditorUtility.SetDirty(trail);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(fieldGo.scene);
            Debug.Log($"[TimeKiller Setup] Blood trail ready. You bleed at {config.bleedAtHp} HP or less and faster near death; "
                    + $"up to {config.capacity} stains persist (oldest recycled) in ONE draw call. Watch the 'Blood' and 'Bleed' lines on F1. "
                    + "Maniac tracking is NOT wired yet — this pass is atmosphere only.");
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
