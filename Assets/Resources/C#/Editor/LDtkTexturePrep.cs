// Menu: TimeKiller/Setup/15 - Prep LDtk Textures (RGBA32).
// LDtkToUnity requires source tilesheets to import as uncompressed RGBA32.
// This sets that through the proper TextureImporter API (default platform),
// leaving every other artist setting untouched.
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class LDtkTexturePrep
    {
        static readonly string[] Sheets =
        {
            "Assets/Resources/Outsource/RF Castle/Sliced/mainlevbuild.png",
            "Assets/Resources/Outsource/RF Castle/Sliced/decorative.png",
            // Catacombs level (Setup/30) draws from this sheet.
            "Assets/Resources/Outsource/RogueFantasyCatacombs/mainlevbuild.png",
        };

        [MenuItem("TimeKiller/Setup/15 - Prep LDtk Textures (RGBA32)")]
        public static void Prep()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("15 - Prep LDtk Textures (RGBA32)")) return;

            foreach (var path in Sheets)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { Debug.LogError($"[TimeKiller Setup] Not found: {path}"); continue; }

                var settings = importer.GetDefaultPlatformTextureSettings();
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                Debug.Log($"[TimeKiller Setup] {path} -> RGBA32 uncompressed.");
            }
            AssetDatabase.ImportAsset("Assets/Resources/Assets/Maps/CastleWing.ldtk", ImportAssetOptions.ForceUpdate);
            Debug.Log("[TimeKiller Setup] LDtk texture prep done, CastleWing.ldtk reimported.");
        }
    }
}
