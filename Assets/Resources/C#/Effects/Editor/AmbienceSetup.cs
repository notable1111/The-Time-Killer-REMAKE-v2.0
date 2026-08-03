// Menu: TimeKiller/Setup/40 - Ambient Atmosphere (dust + torch embers).
//
// Phase 5 of the VFX pass. Two systems, both particles, both deliberately so:
// dust and embers are continuous dispersal with no readable shape, which is the
// exact case EffectRecipe reserves for particles rather than drawn sheets.
//
// DUST follows the CAMERA, not the world. The map is 66x116 units; filling that
// with motes would cost thousands of particles to make a handful visible. A
// camera-parented emitter with WORLD simulation space gives a field that is
// always on screen but whose motes still hang in the world as you move past
// them — move the camera and the dust stays put, which is what sells it as air
// rather than as an overlay stuck to the lens.
//
// EMBERS are placed on every TorchLight found in the scene (18 of them here),
// as children, so they inherit the torch's position and vanish with it.
//
// Art: Kenney Particle Pack, CC0. Those sprites measured value 223 / softness
// 0.75 against this game's 33 / 0.00 and were correctly flagged as too bright
// and too soft for anything with a readable shape. They are WHITE MASKS, so the
// tint in AmbienceConfig sets the on-screen colour — which is why a pack that
// fails the style check is still the right choice for light.
//
// Safe to re-run: every object is found-or-created and re-tuned in place, never
// destroyed and rebuilt. Removing the feature = delete the "Ambience" object and
// the "Embers" children; nothing references either.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Effects.EditorTools
{
    public static class AmbienceSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Effects/Configs/AmbienceConfig.asset";
        const string KenneyFolder = "Assets/Resources/Outsource/KenneyParticles";
        const string DustSprite = "circle_05.png";     // soft round mote
        const string EmberSprite = "spark_05.png";     // small bright fleck
        const string HazeSprite = "smoke_09.png";      // big ragged cloud, for the medium
        const string MaterialFolder = "Assets/Resources/Assets/Effects";

        [MenuItem("TimeKiller/Setup/40 - Ambient Atmosphere (dust + torch embers)")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<AmbienceConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<AmbienceConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log($"[TimeKiller Setup] Created {ConfigPath}");
            }

            var dustMat = ParticleMaterial("AmbientDust", DustSprite);
            var emberMat = ParticleMaterial("AmbientEmber", EmberSprite);
            if (dustMat == null || emberMat == null) return;

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[TimeKiller Setup] No Main Camera — dust needs one to follow. Nothing built.");
                return;
            }

            BuildDust(camera, config, dustMat);
            int torches = BuildEmbers(config, emberMat);

            var hazeMat = ParticleMaterial("AmbientHaze", HazeSprite);
            if (hazeMat != null) BuildHaze(camera, config, hazeMat);
            int shafts = config.shafts ? BuildShafts(config) : 0;
            Debug.Log($"[TimeKiller Setup] Atmosphere: {config.hazeCount} haze puffs, {shafts} light shaft(s).");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            Debug.Log($"[TimeKiller Setup] Ambience ready: {config.dustCount} dust motes following the camera, " +
                      $"embers on {torches} torch(es). Tunables live on {ConfigPath} — drag them in Play Mode. " +
                      "Kenney Particle Pack (CC0); see Outsource/KenneyParticles/License.txt.");
        }

        static void BuildDust(Camera camera, AmbienceConfig config, Material material)
        {
            var go = Child(camera.transform, "AmbientDust");
            go.transform.localPosition = new Vector3(0f, 0f, 10f);   // in front of an ortho camera
            var ps = Ensure<ParticleSystem>(go);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(config.dustLifetime.x, config.dustLifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(config.dustDrift * 0.4f, config.dustDrift);
            main.startSize = new ParticleSystem.MinMaxCurve(config.dustSizeMin, config.dustSizeMax);
            main.startColor = config.dustTint;
            main.maxParticles = Mathf.Max(1, config.dustCount);
            main.gravityModifier = -0.005f;      // the faintest lift: dust hangs and rises, it does not fall
            // WORLD space is the whole trick. The emitter rides the camera so the
            // field is always on screen, but each mote is left behind in the
            // world — walk past one and it stays where it was.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = config.dustCount / Mathf.Max(1f, (config.dustLifetime.x + config.dustLifetime.y) * 0.5f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(config.dustArea.x, config.dustArea.y, 0.1f);

            // Fade in and out at the ends of life, so motes never pop.
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                        new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            // Slow drift, and a little noise so the field is not a uniform crawl.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.18f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = config.dustSortingOrder;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// The medium. A handful of ENORMOUS, almost invisible puffs drifting
        /// across the view. Any single one being noticeable means it is too
        /// strong — haze works by several overlapping at once, which is why the
        /// alpha is a twentieth of the dust's.
        static void BuildHaze(Camera camera, AmbienceConfig config, Material material)
        {
            var go = Child(camera.transform, "AmbientHaze");
            go.transform.localPosition = new Vector3(0f, 0f, 9.5f);
            var ps = Ensure<ParticleSystem>(go);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(config.hazeLifetime.x, config.hazeLifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(config.hazeDrift * 0.3f, config.hazeDrift);
            main.startSize = new ParticleSystem.MinMaxCurve(config.hazeSize.x, config.hazeSize.y);
            main.startColor = config.hazeTint;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = Mathf.Max(1, config.hazeCount);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = config.hazeCount / Mathf.Max(1f, (config.hazeLifetime.x + config.hazeLifetime.y) * 0.5f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 13f, 0.1f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f),
                        new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            // Very slow rotation so the cloud shapes never sit still enough to
            // be recognised as the same sprite twice.
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = config.hazeSortingOrder;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// A cone of lit air under each torch. Static quads, not particles: a
        /// shaft is a shape that belongs to its light, and it should not drift.
        static int BuildShafts(AmbienceConfig config)
        {
            var sprite = ShaftSprite();
            if (sprite == null) return 0;

            var torches = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .Where(l => l.gameObject.name.Contains("Torch"))
                                .ToArray();
            foreach (var torch in torches)
            {
                var go = Child(torch.transform, "Shaft");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;

                var sr = Ensure<SpriteRenderer>(go);
                sr.sprite = sprite;
                sr.color = config.shaftTint;
                sr.sortingOrder = config.shaftSortingOrder;
                sr.drawMode = SpriteDrawMode.Simple;

                // The texture is authored source-at-top, so pivot the quad at its
                // top edge and hang it downward from the torch.
                float unitsPerPixel = 1f / 32f;
                float nativeW = sprite.texture.width * unitsPerPixel;
                float nativeH = sprite.texture.height * unitsPerPixel;
                go.transform.localScale = new Vector3(
                    config.shaftWidth / Mathf.Max(0.001f, nativeW),
                    config.shaftLength / Mathf.Max(0.001f, nativeH), 1f);
                go.transform.localPosition = new Vector3(0f, -config.shaftLength * 0.5f, 0f);

                var flicker = Ensure<ShaftFlicker>(go);
                var so = new SerializedObject(flicker);
                so.FindProperty("amount").floatValue = config.shaftFlicker;
                so.FindProperty("baseAlpha").floatValue = config.shaftTint.a;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return torches.Length;
        }

        static Sprite ShaftSprite()
        {
            const string path = "Assets/Resources/Assets/Effects/light_shaft.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[TimeKiller Setup] Missing {path}. Run Tools/VfxPipeline/gen_light_shaft.py.");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 32f)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                // Pivot at the TOP so the shaft hangs from its source.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.TopCenter;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static int BuildEmbers(AmbienceConfig config, Material material)
        {
            var torches = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .Where(l => l.gameObject.name.Contains("Torch") || l.gameObject.name.Contains("Candle"))
                                .ToArray();

            foreach (var torch in torches)
            {
                var go = Child(torch.transform, "Embers");
                go.transform.localPosition = Vector3.zero;
                var ps = Ensure<ParticleSystem>(go);

                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(config.emberLifetime.x, config.emberLifetime.y);
                main.startSpeed = new ParticleSystem.MinMaxCurve(config.emberRise * 0.5f, config.emberRise);
                main.startSize = new ParticleSystem.MinMaxCurve(config.emberSizeMin, config.emberSizeMax);
                main.startColor = config.emberTint;
                main.maxParticles = 40;
                main.gravityModifier = -0.15f;   // heat lifts them
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = ps.emission;
                emission.rateOverTime = config.emberRate;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = config.emberRadius;

                // Wander sideways so they do not rise in a straight column.
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = config.emberWander;
                noise.frequency = 0.6f;

                var color = ps.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.25f, 0.1f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                color.color = gradient;

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = config.emberSortingOrder;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
            return torches.Length;
        }

        /// Unlit additive-ish sprite material. Unlit ON PURPOSE: dust and embers
        /// ARE light. A lit particle would be black in exactly the dark rooms
        /// where atmosphere matters most — the same trap the nav overlay hit.
        static Material ParticleMaterial(string name, string spriteFile)
        {
            string spritePath = $"{KenneyFolder}/{spriteFile}";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
            if (texture == null)
            {
                Debug.LogError($"[TimeKiller Setup] Missing {spritePath}. Import the Kenney Particle Pack first.");
                return null;
            }
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;   // particles sample the raw texture
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Ambience");
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
