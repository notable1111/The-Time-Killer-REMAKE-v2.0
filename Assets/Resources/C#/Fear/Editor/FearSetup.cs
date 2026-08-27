// Menu: TimeKiller/Setup/45 - Fear System.
//
// Creates FearConfig.asset if missing, puts a FearConductor and a FearSting on
// the player, and points the heartbeat and breathing at the same config.
//
// IDEMPOTENT AND NON-DESTRUCTIVE, like every Setup script here: it finds or
// creates, and re-wires in place. It never destroys and rebuilds, because a
// setup script that rebuilt its rig on every run was quietly deleting a scene
// object nobody put back (Setup/22 and Setup/25, 2026-07-28). Anything already
// tuned by hand — volumes, curves, a config someone edited — survives a re-run.
using System.Collections.Generic;
using TimeKiller.Fear;
using TimeKiller.Heartbeat;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.FearTools
{
    public static class FearSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Fear/Configs/FearConfig.asset";
        const string StingDir = "Assets/Resources/Assets/AudioNormalized";
        // Chosen by measurement, not by name — see FearDrone's header. Lowpassing
        // this at 200Hz costs only 0.5 dB, so it is genuinely a sub-bass drone.
        const string DronePath =
            "Assets/Resources/Outsource/Audio/EchoChambersAmbience/Ambience/Monolith_1.wav";

        [MenuItem("TimeKiller/Setup/45 - Fear System")]
        public static void Run()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("45 - Fear System")) return;

            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[FearSetup] In play mode — setup scripts half-complete there. Stop play first.");
                return;
            }

            var config = EnsureConfig();

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[FearSetup] No PlayerController in the open scene.");
                return;
            }

            var report = new System.Text.StringBuilder("[FearSetup] ");

            // --- the conductor -------------------------------------------------
            var conductor = player.GetComponent<FearConductor>();
            if (conductor == null)
            {
                conductor = player.gameObject.AddComponent<FearConductor>();
                report.Append("added FearConductor; ");
            }
            else report.Append("FearConductor already present; ");
            conductor.Init(config);
            EditorUtility.SetDirty(conductor);

            // --- the sting, on its own child so its AudioSource is its own ------
            var stingGo = FindChild(player.transform, "FearSting");
            if (stingGo == null)
            {
                stingGo = new GameObject("FearSting");
                stingGo.transform.SetParent(player.transform, false);
                report.Append("created FearSting; ");
            }
            var stingSource = stingGo.GetComponent<AudioSource>();
            if (stingSource == null) stingSource = stingGo.AddComponent<AudioSource>();
            stingSource.playOnAwake = false;
            stingSource.spatialBlend = 0f;

            var sting = stingGo.GetComponent<FearSting>();
            if (sting == null) sting = stingGo.AddComponent<FearSting>();
            var clips = LoadStings();
            sting.Init(config, clips);
            report.Append(clips.Length + " sting clips; ");
            EditorUtility.SetDirty(sting);

            // --- the tension drone, on its own child ----------------------------
            var droneGo = FindChild(player.transform, "FearDrone");
            if (droneGo == null)
            {
                droneGo = new GameObject("FearDrone");
                droneGo.transform.SetParent(player.transform, false);
                report.Append("created FearDrone; ");
            }
            var droneSource = droneGo.GetComponent<AudioSource>();
            if (droneSource == null) droneSource = droneGo.AddComponent<AudioSource>();
            droneSource.playOnAwake = false;
            droneSource.loop = true;
            droneSource.spatialBlend = 0f;

            var drone = droneGo.GetComponent<FearDrone>();
            if (drone == null) drone = droneGo.AddComponent<FearDrone>();
            var droneClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DronePath);
            drone.Init(config, droneClip);
            report.Append(droneClip != null ? "drone clip OK; " : "DRONE CLIP MISSING; ");
            EditorUtility.SetDirty(drone);

            // --- point the existing tension channels at the same config ---------
            var heart = Object.FindAnyObjectByType<PlayerHeartbeat>();
            if (heart != null) { heart.InitFear(config); EditorUtility.SetDirty(heart); report.Append("heartbeat wired; "); }
            else report.Append("NO PlayerHeartbeat found; ");

            var lungs = Object.FindAnyObjectByType<PlayerBreathing>();
            if (lungs != null)
            {
                var so = new SerializedObject(lungs);
                var prop = so.FindProperty("fearConfig");
                if (prop != null) { prop.objectReferenceValue = config; so.ApplyModifiedProperties(); }
                report.Append("breathing wired; ");
            }
            else report.Append("NO PlayerBreathing found; ");

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log(report.ToString());
        }

        static FearConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<FearConfig>(ConfigPath);
            if (config != null) return config;   // never overwrite tuned values

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ConfigPath));
            config = ScriptableObject.CreateInstance<FearConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[FearSetup] Created " + ConfigPath);
            return config;
        }

        static AudioClip[] LoadStings()
        {
            var found = new List<AudioClip>();
            foreach (var name in new[] { "sting_spotted_1", "sting_spotted_2", "sting_spotted_3" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{StingDir}/{name}.wav");
                if (clip != null) found.Add(clip);
            }
            return found.ToArray();
        }

        static GameObject FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child.gameObject;
            return null;
        }
    }
}
