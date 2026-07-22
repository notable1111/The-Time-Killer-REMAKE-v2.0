// Menu: TimeKiller/Setup/22 - Add Health VFX (screen blood).
// Builds the diegetic health presentation: a global URP Volume with a red
// Vignette, a screen-space canvas with the CC0 blood overlays (band + hit
// flash layers), the breathing AudioSource, and the director wiring them to
// PlayerHealth events. Textures: OpenGameArt CC0 (see Assets/HealthVfx/CREDITS.txt).
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
        const string TextureFolder = "Assets/Resources/Assets/HealthVfx";
        const string BreathingClipPath = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/strong breathe person.wav";
        const string HeartbeatClipPath = "Assets/Resources/Assets/HealthVfx/heartbeat.wav"; // synthesized in-house, license-free

        [MenuItem("TimeKiller/Setup/22 - Add Health VFX (screen blood)")]
        public static void Build()
        {
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
            if (!profile.TryGet<Vignette>(out _))
            {
                var vignette = profile.Add<Vignette>();
                vignette.intensity.overrideState = true;
                vignette.intensity.value = 0f;
                vignette.color.overrideState = true;
                vignette.color.value = config.vignetteColor;
                vignette.smoothness.overrideState = true;
                vignette.smoothness.value = 0.6f;
            }
            if (!profile.TryGet<ChromaticAberration>(out _))
            {
                var chroma = profile.Add<ChromaticAberration>();
                chroma.intensity.overrideState = true;
                chroma.intensity.value = 0f;
            }
            if (!profile.TryGet<FilmGrain>(out _))
            {
                var grain = profile.Add<FilmGrain>();
                grain.type.overrideState = true;
                grain.type.value = FilmGrainLookup.Medium1;
                grain.intensity.overrideState = true;
                grain.intensity.value = 0f;
            }
            if (!profile.TryGet<ColorAdjustments>(out _))
            {
                var color = profile.Add<ColorAdjustments>();
                color.saturation.overrideState = true;
                color.saturation.value = 0f;
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var old = GameObject.Find("HealthVfx");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject("HealthVfx");
            Undo.RegisterCreatedObjectUndo(root, "Health VFX");

            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.profile = profile;

            var canvasGo = new GameObject("BloodCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var (bandImage, bandGroup) = FullscreenLayer(canvasGo.transform, "BandOverlay", subtle);
            var (bandImageB, bandGroupB) = FullscreenLayer(canvasGo.transform, "BandOverlayB", subtleB);
            var (flashImage, flashGroup) = FullscreenLayer(canvasGo.transform, "HitFlash", splatter);

            // Shake listener on the CM camera (impulse SOURCES live in EffectPlayer).
            var cineCam = GameObject.Find("CM_PlayerCamera");
            if (cineCam != null && cineCam.GetComponent<Unity.Cinemachine.CinemachineImpulseListener>() == null)
                cineCam.AddComponent<Unity.Cinemachine.CinemachineImpulseListener>();

            var audioGo = new GameObject("Breathing");
            audioGo.transform.SetParent(root.transform, false);
            var breathing = audioGo.AddComponent<AudioSource>();
            breathing.clip = breathingClip;
            breathing.loop = true;
            breathing.playOnAwake = false;
            breathing.volume = 0f;
            breathing.spatialBlend = 0f; // in the player's head, not the world

            var heartGo = new GameObject("Heartbeat");
            heartGo.transform.SetParent(root.transform, false);
            var heartAudio = heartGo.AddComponent<AudioSource>();
            heartAudio.playOnAwake = false;
            heartAudio.spatialBlend = 0f;

            var heartbeatClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeartbeatClipPath);
            if (heartbeatClip == null) Debug.LogWarning("[TimeKiller Setup] heartbeat.wav missing — heart will be silent.");

            var director = root.AddComponent<HealthVfxDirector>();
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
            so.FindProperty("heartAudio").objectReferenceValue = heartAudio;
            so.FindProperty("heartbeatClip").objectReferenceValue = heartbeatClip;
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
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return (image, group);
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
