// Menu: TimeKiller/Setup/7 - Add Blob Shadow.
// Bakes a soft radial-gradient ellipse texture (once) and parents it under the
// Player at the feet. Pure visual child object — deleting "Shadow" removes the
// feature with zero side effects, and any future character reuses it as-is.
using System.IO;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class BlobShadowSetup
    {
        const string TexturePath = "Assets/Resources/Assets/Shadows/blob_shadow.png";
        const int Width = 64, Height = 32;

        [MenuItem("TimeKiller/Setup/7 - Add Blob Shadow")]
        public static void AddShadow()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("7 - Add Blob Shadow")) return;

            BakeTextureIfMissing();

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[TimeKiller Setup] No Player in scene — run Setup/4 first.");
                return;
            }

            var existing = player.transform.Find("Shadow");
            if (existing != null)
            {
                Debug.LogWarning("[TimeKiller Setup] Player already has a Shadow child — nothing added.");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            var shadow = new GameObject("Shadow");
            Undo.RegisterCreatedObjectUndo(shadow, "Add Blob Shadow");
            shadow.transform.SetParent(player.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.5f, 0f); // under the feet

            var renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0f, 0f, 0f, 0.45f);
            renderer.sortingOrder = -1; // behind the character, above the floor

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("[TimeKiller Setup] Blob shadow added under the Player's feet.");
        }

        static void BakeTextureIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath) != null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath)!);
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                // Normalized distance from the ellipse center; smooth alpha falloff.
                float dx = (x + 0.5f - Width / 2f) / (Width / 2f);
                float dy = (y + 0.5f - Height / 2f) / (Height / 2f);
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha)); // squared = softer edge
            }
            tex.Apply();
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(TexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f; // 64px → 0.64 units wide, fits the character
            importer.SaveAndReimport();
            Debug.Log("[TimeKiller Setup] Baked blob shadow texture: " + TexturePath);
        }
    }
}
