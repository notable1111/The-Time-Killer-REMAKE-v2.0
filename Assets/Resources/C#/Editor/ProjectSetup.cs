// One-time project setup for The Time Killer Remake (Phase 0).
// Adds menu items under TimeKiller/Setup that install required packages
// and configure project settings. Safe to re-run; each step logs what it did.
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class ProjectSetup
    {
        static AddAndRemoveRequest packageRequest;

        [MenuItem("TimeKiller/Setup/1 - Install Packages (URP + Input System)")]
        public static void InstallPackages()
        {
            packageRequest = Client.AddAndRemove(new[]
            {
                "com.unity.render-pipelines.universal",
                "com.unity.inputsystem"
            }, null);
            EditorApplication.update += PackageProgress;
            Debug.Log("[TimeKiller Setup] Installing URP + Input System... (editor will recompile when done)");
        }

        static void PackageProgress()
        {
            if (packageRequest == null || !packageRequest.IsCompleted) return;
            EditorApplication.update -= PackageProgress;

            if (packageRequest.Status == StatusCode.Success)
                Debug.Log("[TimeKiller Setup] Packages installed OK: URP + Input System.");
            else
                Debug.LogError("[TimeKiller Setup] Package install FAILED: " + packageRequest.Error.message);
            packageRequest = null;
        }

        [MenuItem("TimeKiller/Setup/2 - Configure Project Settings")]
        public static void ConfigureProjectSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.productName = "The Time Killer";
            PlayerSettings.companyName = "The Time Killer Team";

            // Active input handler lives only in the serialized ProjectSettings asset.
            // 0 = legacy only, 1 = Input System only, 2 = Both.
            // "Both" keeps downloaded Outsource assets (which often use legacy Input) working.
            var settingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            var so = new SerializedObject(settingsAsset);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                Debug.Log("[TimeKiller Setup] Settings done: Linear color, identity, input handler = Both. RESTART THE EDITOR to apply the input change.");
            }
            else
            {
                Debug.LogError("[TimeKiller Setup] Could not find activeInputHandler property.");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
