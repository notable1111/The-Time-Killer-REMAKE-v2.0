// Creates the URP pipeline assets and makes them active (Phase 0, step 3).
// Kept separate from ProjectSetup.cs because this file references URP types,
// which only compile after the URP package is installed by step 1.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.EditorTools
{
    public static class UrpSetup
    {
        const string FolderParent = "Assets/Resources/Assets";
        const string Folder = "Assets/Resources/Assets/Rendering";

        [MenuItem("TimeKiller/Setup/3 - Create + Activate URP Pipeline")]
        public static void CreateAndActivate()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("3 - Create + Activate URP Pipeline")) return;

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(FolderParent, "Rendering");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, Folder + "/URP_Renderer.asset");

            var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            // The renderer list is not settable through public API, so assign it serialized.
            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            so.ApplyModifiedProperties();
            pipeline.supportsHDR = true; // needed for bloom/glow in horror lighting
            AssetDatabase.CreateAsset(pipeline, Folder + "/URP_Pipeline.asset");

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[TimeKiller Setup] URP pipeline created at " + Folder + " and activated for Graphics + all quality levels.");
        }
    }
}
