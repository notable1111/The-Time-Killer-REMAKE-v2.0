// Menu: TimeKiller/Setup/23 - Setup Effect Recipes.
// Builds the effects toolkit: the BloodBurst particle prefab (droplets drawn
// on-palette by Tools/VfxPipeline/draw_droplets.py), the PlayerHit and
// PlayerDeath recipes, and the PlayerHitEffects binder in the scene.
// New effects later = new recipe asset + one EffectPlayer.Play call.
//
// NOTE: this script is AUTHORITATIVE for its three recipes — re-running it
// resets them to the shipped values. That is deliberate (it is the "back to
// defaults" path), but it means Inspector tuning on PlayerHit/PlayerDeath/
// PlayerDeathSoul does not survive a re-run. Tune elsewhere, or edit the
// numbers here so the tuning is the shipped default. The scene object and the
// hand-drawn VFX clips (Setup/38) are NOT touched this way.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Effects.EditorTools
{
    public static class EffectsSetup
    {
        const string AtlasPath = "Assets/Resources/Assets/Effects/blood_droplets_atlas.png";
        const int AtlasColumns = 3, AtlasRows = 2;
        const string MaterialPath = "Assets/Resources/Assets/Effects/BloodDroplet.mat";
        const string PrefabPath = "Assets/Resources/Assets/Effects/BloodBurst.prefab";
        const string RecipeFolder = "Assets/Resources/C#/Effects/Configs/Recipes";
        const string SplashClip1 = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/blood slpash 1.wav";
        const string SplashClip2 = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/blood splash 2.wav";
        const string ImpactClip = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/scary impact.wav";

        [MenuItem("TimeKiller/Setup/23 - Setup Effect Recipes")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("23 - Setup Effect Recipes")) return;

            var prefab = BuildBloodBurstPrefab();
            if (prefab == null) return;
            Directory.CreateDirectory(RecipeFolder);

            // 2026-07-28: CFXR no longer wins by default. Those prefabs are
            // "Cartoon FX" — bright, saturated, round — and preferring them
            // silently replaced our own on-palette BloodBurst, which is what
            // made the hit read as cheap. CFXR is still in the project and can
            // be dragged into any recipe's particlePrefab by hand; it just no
            // longer overrides purpose-built art.
            var cfxrSouls = FindPrefab("CFXR2 Souls Escape");

            var hit = Recipe("PlayerHit");
            hit.particlePrefab = prefab;
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
            death.particlePrefab = prefab;
            death.particleScale = 1.9f;   // same burst, thrown harder and wider
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

        /// The part that actually reads. Flying droplets are thin and brief —
        /// measured at 0.75% peak screen coverage, which the visibility probe
        /// rates "easy to miss". What tells you you were hit is blood that
        /// STAYS: a few fat gouts that barely travel and linger on the floor
        /// while the spray is already gone.
        static void BuildSplatChild(GameObject parent, Material material)
        {
            var go = new GameObject("Splat");
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.6f);   // barely travels
            // Kept close to 1:1 like the droplets, capped at 1.35 so a single
            // gout can never be bigger than the 0.9u survivor — you must always
            // be able to see yourself in a game about running and hiding.
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 1.35f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 1f), new Color(0.82f, 0.82f, 0.82f, 1f));
            main.gravityModifier = 0.75f;
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.46f;     // ring the body, never sit on top of it
            shape.arc = 190f;         // wider than the spray: gouts fall broadly
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.radiusThickness = 0.2f;
            shape.randomDirectionAmount = 0.5f;

            // Settle almost immediately, then just sit there staining the floor.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.75f;
            limit.limit = new ParticleSystem.MinMaxCurve(0.35f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.72f, 0.72f, 0.72f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            // Gouts SPREAD as they land rather than shrink — a splat widens.
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var spread = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.18f, 1f), new Keyframe(1f, 1.08f));
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, spread);

            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = AtlasColumns;
            sheet.numTilesY = AtlasRows;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            // Top row ONLY (normalised 0..0.5 of a 3x2 sheet) — those are the
            // three fattest spatters. A gout that rolled a tiny speck would not
            // read as the wound it is meant to be.
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.49f);
            sheet.cycleCount = 1;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 4;      // under the flying droplets
            // Billboard, NOT stretched: these have landed. Stretching a settled
            // pool along a velocity it no longer has would look like it is still
            // sliding across the floor.
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
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
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[TimeKiller Setup] Droplet atlas missing: {AtlasPath}. Run Tools/VfxPipeline/draw_droplets.py.");
                return null;
            }
            if (importer.textureType != TextureImporterType.Default || importer.filterMode != FilterMode.Point)
            {
                importer.textureType = TextureImporterType.Default; // particles sample the raw texture
                // POINT, to match the rest of the world. The droplets are authored
                // at 32px per cell against a 32 px/unit world, so a particle sized
                // 1.0 draws them at 1:1 and bilinear would only blur pixel art
                // that is already the right resolution.
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            material.SetFloat("_Surface", 1f);                     // transparent
            material.SetFloat("_Blend", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);

            var go = new GameObject("BloodBurst");
            var ps = go.AddComponent<ParticleSystem>();

            // Tuning notes (2026-07-28 "looks cheap" pass). At 32 px/unit a
            // 0.12-unit droplet is under 4 screen pixels, so droplet DETAIL is
            // invisible and only these numbers matter:
            //   - fast out, hard drag, heavy gravity = blood is thrown, then
            //     falls. The old 2-4.5 speed with no drag drifted like ash.
            //   - a wide size range so a few fat gouts sell the wound while
            //     fine mist fills the gaps. One narrow range read as confetti.
            //   - alpha holds, then drops late: blood does not fade as it flies.
            var main = ps.main;
            main.duration = 0.7f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            // Wide on purpose. With a narrow speed range every droplet leaves at
            // once at nearly the same rate and the burst reads as an expanding
            // RING passing over the player. Spread the speeds and the ring
            // dissolves into spatter.
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 10f);
            // Near 1.0 on purpose: the cell is 32px and the world is 32 px/unit,
            // so size 1.0 is exactly 1:1 and the pixels stay square. Visual size
            // variety comes from how much INK each atlas tile holds (~8 to 26px
            // across), not from scaling particles — scaling pixel art is what
            // makes it look mushy.
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // DO NOT tint these down. Measured in the actual scene with
            // TimeKiller/Verify/VFX Visibility: the castle floor is ~(11,16,16),
            // luminance 10/255 — near black. An
            // earlier "palette match" pass that tinted particles to 0.88/0.48
            // put the blood at ~(70,6,4) — 1.7x the floor's luminance, covering
            // 0.26% of the screen at its peak and gone inside 0.75s. It was
            // invisible in play. The droplet art already carries the dark rim
            // that keeps it from looking garish; the CORE has to stay bright
            // enough to read against near-black.
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 1f), new Color(0.78f, 0.78f, 0.78f, 1f));
            main.gravityModifier = 3.4f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;  // spatter stays put when the victim moves

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            // 24, not 46. Each spatter tile is up to ~26px of ink and draws at
            // 1:1, so a particle is a real 0.25-0.8u mark — at 46 of them the
            // burst buried the player entirely. Count is the knob that decides
            // whether this reads as a wound or as a paint bomb.
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.26f;    // start off the body, not on top of it
            // Emitting purely from the rim (thickness 1) with an even speed makes
            // a textbook hollow RING — the single most artificial-looking thing
            // a radial burst can do. Filling most of the disc and jittering the
            // direction breaks the circle up into spatter.
            shape.radiusThickness = 0.55f;
            // A WEDGE, not a full circle. A symmetric burst reads as an
            // explosion centred on the victim; a wound sprays away from the
            // blow. EffectPlayer rotates the whole system to aim this arc using
            // the direction the hit came from, and falls back to unrotated
            // (which just looks like a lopsided splash) when no direction is
            // supplied. Less random scatter now that the arc provides spread.
            shape.arc = 150f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.randomDirectionAmount = 0.32f;

            // Air drag: the burst decelerates hard, which is what makes it read
            // as thrown liquid rather than as drifting particles.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.32f;
            limit.limit = new ParticleSystem.MinMaxCurve(1.2f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.66f, 0.66f, 0.66f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.86f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var shrink = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.62f));
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, shrink);

            // Each particle picks ONE random droplet from the 3x2 atlas and holds
            // it. Without this every droplet in the burst is the same stamp.
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = AtlasColumns;
            sheet.numTilesY = AtlasRows;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);           // hold the chosen tile
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, AtlasColumns * AtlasRows);
            sheet.cycleCount = 1;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 5;                              // above floor, below overhead
            // Stretched billboard = each droplet ORIENTS along its own flight
            // path instead of sitting at a random angle. Round billboards at
            // speed read as confetti; liquid points where it is going, and the
            // two streak tiles in the atlas only make sense aimed.
            // velocityScale is kept tiny ON PURPOSE: it stretches the quad
            // non-uniformly, which smears pixel art. Nearly all the benefit here
            // is the ORIENTATION, not the stretch.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0.035f;

            BuildSplatChild(go, material);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
