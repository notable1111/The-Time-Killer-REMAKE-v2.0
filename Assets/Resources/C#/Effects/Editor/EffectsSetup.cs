// Menu: TimeKiller/Setup/23 - Setup Effect Recipes.
// Builds the effects toolkit: the BloodBurst particle prefab (droplet sprites
// cropped from the CC0 splats — real assets, not placeholders), the PlayerHit
// and PlayerDeath recipes, and the PlayerHitEffects binder in the scene.
// New effects later = new recipe asset + one EffectPlayer.Play call.
// CFXR Free prefabs (when imported) drop into recipes' particlePrefab slot.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Effects.EditorTools
{
    public static class EffectsSetup
    {
        const string DropletPath = "Assets/Resources/Assets/Effects/blood_droplet_1.png";
        const string MaterialPath = "Assets/Resources/Assets/Effects/BloodDroplet.mat";
        const string PrefabPath = "Assets/Resources/Assets/Effects/BloodBurst.prefab";
        const string RecipeFolder = "Assets/Resources/C#/Effects/Configs/Recipes";
        const string SplashClip1 = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/blood slpash 1.wav";
        const string SplashClip2 = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/blood splash 2.wav";
        const string ImpactClip = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/scary impact.wav";

        [MenuItem("TimeKiller/Setup/23 - Setup Effect Recipes")]
        public static void Build()
        {
            var prefab = BuildBloodBurstPrefab();
            if (prefab == null) return;
            Directory.CreateDirectory(RecipeFolder);

            // CFXR Free (imported to Outsource/JMO Assets) upgrades the particle
            // slots when present; our BloodBurst remains the fallback.
            var cfxrBlood = FindPrefab("CFXR2 Blood (Directional)");
            var cfxrBloodBig = FindPrefab("CFXR2 Blood Shape Splash");
            var cfxrSouls = FindPrefab("CFXR2 Souls Escape");

            var hit = Recipe("PlayerHit");
            hit.particlePrefab = cfxrBlood != null ? cfxrBlood : prefab;
            hit.particleScale = 1f;
            hit.clips = new[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>(SplashClip1),
                AssetDatabase.LoadAssetAtPath<AudioClip>(SplashClip2),
            };
            hit.volume = 0.85f;
            hit.pitchJitter = 0.08f;
            hit.shakeStrength = 0.35f;
            hit.shakeDuration = 0.25f;
            EditorUtility.SetDirty(hit);

            var death = Recipe("PlayerDeath");
            death.particlePrefab = cfxrBloodBig != null ? cfxrBloodBig : prefab;
            death.particleScale = 1.6f;
            death.clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(ImpactClip) };
            death.volume = 0.9f;
            death.pitchJitter = 0.03f;
            death.shakeStrength = 0.6f;
            death.shakeDuration = 0.4f;
            EditorUtility.SetDirty(death);

            // The soul leaves the body — layered on top of the death splash.
            var soul = Recipe("PlayerDeathSoul");
            soul.particlePrefab = cfxrSouls;
            soul.particleScale = 0.8f;
            soul.clips = new AudioClip[0];
            soul.shakeStrength = 0f;
            soul.particleLifetime = 4f;
            EditorUtility.SetDirty(soul);
            AssetDatabase.SaveAssets();

            var binderGo = GameObject.Find("PlayerHitEffects");
            if (binderGo == null)
            {
                binderGo = new GameObject("PlayerHitEffects");
                Undo.RegisterCreatedObjectUndo(binderGo, "Player Hit Effects");
            }
            var binder = binderGo.GetComponent<PlayerHitEffects>();
            if (binder == null) binder = binderGo.AddComponent<PlayerHitEffects>();
            var so = new SerializedObject(binder);
            so.FindProperty("hitRecipe").objectReferenceValue = hit;
            so.FindProperty("deathRecipe").objectReferenceValue = death;
            so.FindProperty("deathSoulRecipe").objectReferenceValue = soul;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(binderGo.scene);
            Debug.Log("[TimeKiller Setup] Effect recipes ready: PlayerHit (blood burst + splash + shake), PlayerDeath (big burst + impact + heavy shake). New effects = new recipe assets.");
        }

        static GameObject FindPrefab(string exactName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"\"{exactName}\" t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == exactName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Debug.LogWarning($"[TimeKiller Setup] CFXR prefab not found: {exactName} (recipe falls back to BloodBurst).");
            return null;
        }

        static EffectRecipe Recipe(string name)
        {
            string path = $"{RecipeFolder}/{name}.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<EffectRecipe>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<EffectRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }
            return recipe;
        }

        static GameObject BuildBloodBurstPrefab()
        {
            var importer = AssetImporter.GetAtPath(DropletPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[TimeKiller Setup] Droplet texture missing: {DropletPath}");
                return null;
            }
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default; // particles sample the raw texture
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DropletPath));
            material.SetFloat("_Surface", 1f);                     // transparent
            material.SetFloat("_Blend", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);

            var go = new GameObject("BloodBurst");
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 2.2f;                            // droplets arc and fall
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 5;                              // above floor, below overhead

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
