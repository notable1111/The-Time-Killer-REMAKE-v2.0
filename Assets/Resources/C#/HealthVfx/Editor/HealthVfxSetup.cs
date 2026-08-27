// Menu: TimeKiller/Setup/22 - Add Health VFX (screen blood).
// Builds the diegetic health presentation: a global URP Volume with a red
// Vignette, a screen-space canvas with the CC0 blood overlays (band + hit
// flash layers), the breathing AudioSource, and the director wiring them to
// PlayerHealth events. Textures: OpenGameArt CC0 (see Assets/HealthVfx/CREDITS.txt).
// Safe to re-run: every object is found-or-created and re-wired in place, so it
// never destroys a hand-tuned rig. It creates no heartbeat AudioSource — the
// lub-dub belongs to PlayerHeartbeat (Setup/36); this director is visual only.
using System.IO;
using TimeKiller.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TimeKiller.HealthVfx.EditorTools
{
    public static class HealthVfxSetup
    {
        const string ConfigPath = "Assets/Resources/C#/HealthVfx/Configs/HealthVfxConfig.asset";
        const string ProfilePath = "Assets/Resources/C#/HealthVfx/Configs/HealthVfxVolumeProfile.asset";

        /// Write a freshly-added override into the profile ASSET, not just its
        /// in-memory list. Naming it keeps the sub-asset readable in the project
        /// window instead of showing up as an unnamed blob.
        static void Persist(VolumeComponent component, VolumeProfile profile, string name)
        {
            component.name = name;
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        /// Round-trip check: reload the profile from disk and complain loudly if
        /// the overrides did not survive. This exact failure was silent for days
        /// — the game simply had no post-processing and nothing said so.
        static void EnsureProfileIsLive(VolumeProfile profile)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProfilePath, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (reloaded == null || reloaded.components.Count == 0)
                Debug.LogError("[TimeKiller Setup] The HealthVfx volume profile reloaded EMPTY — " +
                               "post-processing (vignette/grain/desaturation) will silently do nothing.");
        }
        const string TextureFolder = "Assets/Resources/Assets/HealthVfx";
        const string BreathingClipPath = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/strong breathe person.wav";

        [MenuItem("TimeKiller/Setup/22 - Add Health VFX (screen blood)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("22 - Add Health VFX (screen blood)")) return;

            var subtle = ImportOverlay($"{TextureFolder}/band_subtle.png");
            var subtleB = ImportOverlay($"{TextureFolder}/band_subtle_b.png");
            var critical = ImportOverlay($"{TextureFolder}/band_critical.png");
            var criticalB = ImportOverlay($"{TextureFolder}/band_critical_b.png");
            var splatter = ImportOverlay($"{TextureFolder}/hit_splatter.png");
            if (subtle == null || critical == null || splatter == null)
            {
                Debug.LogError($"[TimeKiller Setup] Blood overlay textures missing in {TextureFolder}.");
                return;
            }
            var breathingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BreathingClipPath);
            if (breathingClip == null)
                Debug.LogWarning("[TimeKiller Setup] Breathing clip not found — critical band will be silent.");

            var config = AssetDatabase.LoadAssetAtPath<HealthVfxConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<HealthVfxConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            // Volume profile with the red vignette.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // Every override MUST be added to the asset as a sub-object.
            // VolumeProfile.Add<T>() only puts it in the in-memory components
            // list; without AddObjectToAsset it is never written to disk, so the
            // profile reloads EMPTY and every TryGet below resolves to null. That
            // is the second half of the bug found on 2026-07-28 — the first half
            // was `volume.profile` instead of `volume.sharedProfile`. Together
            // they meant the post stack had never once run in a loaded scene.
            if (!profile.TryGet<Vignette>(out _))
            {
                var vignette = profile.Add<Vignette>();
                vignette.intensity.overrideState = true;
                vignette.intensity.value = 0f;
                vignette.color.overrideState = true;
                vignette.color.value = config.vignetteColor;
                vignette.smoothness.overrideState = true;
                vignette.smoothness.value = 0.6f;
                Persist(vignette, profile, "Vignette");
            }
            if (!profile.TryGet<ChromaticAberration>(out _))
            {
                var chroma = profile.Add<ChromaticAberration>();
                chroma.intensity.overrideState = true;
                chroma.intensity.value = 0f;
                Persist(chroma, profile, "ChromaticAberration");
            }
            if (!profile.TryGet<FilmGrain>(out _))
            {
                var grain = profile.Add<FilmGrain>();
                grain.type.overrideState = true;
                grain.type.value = FilmGrainLookup.Medium1;
                grain.intensity.overrideState = true;
                grain.intensity.value = 0f;
                Persist(grain, profile, "FilmGrain");
            }
            if (!profile.TryGet<ColorAdjustments>(out _))
            {
                var color = profile.Add<ColorAdjustments>();
                color.saturation.overrideState = true;
                color.saturation.value = 0f;
                Persist(color, profile, "ColorAdjustments");
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Re-runnable: every object below is found-or-created, never destroyed
            // and rebuilt. A rebuild would silently discard anything hand-tuned on
            // the rig in the scene, which is exactly what the scene rule forbids.
            var root = GameObject.Find("HealthVfx");
            if (root == null)
            {
                root = new GameObject("HealthVfx");
                Undo.RegisterCreatedObjectUndo(root, "Health VFX");
            }

            EnsureProfileIsLive(profile);

            var volume = Ensure<Volume>(root);
            volume.isGlobal = true;
            volume.priority = 10f;
            // sharedProfile, NOT profile. `Volume.profile` writes a runtime-only
            // field that is never serialised, so assigning it from an editor
            // script looks correct, works until the scene reloads, and then
            // silently leaves sharedProfile null — which is exactly what had
            // happened here: every post effect below (vignette, chromatic, grain,
            // desaturation) resolved to null at runtime and had never once run.
            volume.sharedProfile = profile;

            var canvasGo = Child(root.transform, "BloodCanvas");
            var canvas = Ensure<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            Ensure<CanvasScaler>(canvasGo).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var (bandImage, bandGroup) = FullscreenLayer(canvasGo.transform, "BandOverlay", subtle);
            var (bandImageB, bandGroupB) = FullscreenLayer(canvasGo.transform, "BandOverlayB", subtleB);
            var (flashImage, flashGroup) = FullscreenLayer(canvasGo.transform, "HitFlash", splatter);

            // Shake listener on the CM camera (impulse SOURCES live in EffectPlayer).
            var cineCam = GameObject.Find("CM_PlayerCamera");
            if (cineCam != null && cineCam.GetComponent<Unity.Cinemachine.CinemachineImpulseListener>() == null)
                cineCam.AddComponent<Unity.Cinemachine.CinemachineImpulseListener>();

            var breathing = Ensure<AudioSource>(Child(root.transform, "Breathing"));
            breathing.clip = breathingClip;
            breathing.loop = true;
            breathing.playOnAwake = false;
            breathing.volume = 0f;
            breathing.spatialBlend = 0f; // in the player's head, not the world

            // No "Heartbeat" child here any more. The lub-dub has one owner —
            // PlayerHeartbeat on the Player (Setup/36) — and this director keeps
            // only the visual throb. Setup/37 removes the stale object from scenes
            // built before the split.

            var director = Ensure<HealthVfxDirector>(root);
            var so = new SerializedObject(director);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("volume").objectReferenceValue = volume;
            so.FindProperty("bandImage").objectReferenceValue = bandImage;
            so.FindProperty("bandGroup").objectReferenceValue = bandGroup;
            so.FindProperty("bandImageB").objectReferenceValue = bandImageB;
            so.FindProperty("bandGroupB").objectReferenceValue = bandGroupB;
            so.FindProperty("flashImage").objectReferenceValue = flashImage;
            so.FindProperty("flashGroup").objectReferenceValue = flashGroup;
            so.FindProperty("breathing").objectReferenceValue = breathing;
            so.FindProperty("subtleSprite").objectReferenceValue = subtle;
            so.FindProperty("subtleSpriteB").objectReferenceValue = subtleB;
            so.FindProperty("criticalSprite").objectReferenceValue = critical;
            so.FindProperty("criticalSpriteB").objectReferenceValue = criticalB;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[TimeKiller Setup] Health VFX ready: 3HP clean, 2HP subtle pulse, 1HP heavy blood + breathing, splatter on every hit. Test with F7 (take 1 hit).");
        }

        static (Image, CanvasGroup) FullscreenLayer(Transform parent, string name, Sprite sprite)
        {
            var go = Child(parent, name);
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var group = Ensure<CanvasGroup>(go);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var image = Ensure<Image>(go);
            image.sprite = sprite;
            image.raycastTarget = false;
            return (image, group);
        }

        /// Find a direct child by name, or create it. Never destroys.
        static GameObject Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Health VFX");
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static Sprite ImportOverlay(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
