// Menu: TimeKiller/Setup/11 - Hall Realism (Lighting + Depth + Shadows).
// Three upgrades in one pass:
//  1. Dynamic 2D lighting — switches URP to the 2D Renderer with Y-axis
//     transparency sorting, swaps sprite materials to lit, adds a dark global
//     light, flickering torch lights and a soft player glow.
//  2. Y-depth props — freestanding pillars built from sheet tiles under
//     SortingGroups: the player renders behind/in front by Y position.
//  3. Contact shadows — baked gradient strips at wall bases and blob shadows
//     under props, kept unlit so light never washes them out.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Lighting;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.EditorTools
{
    public static class HallRealismSetup
    {
        const string MainSheet = "Assets/RF Castle/Sliced/mainlevbuild.png";
        const string PipelinePath = "Assets/Resources/Assets/Rendering/URP_Pipeline.asset";
        const string Renderer2DPath = "Assets/Resources/Assets/Rendering/URP_2DRenderer.asset";
        const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        const string UnlitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string GradientPath = "Assets/Resources/Assets/Shadows/wall_shade.png";
        const string BlobPath = "Assets/Resources/Assets/Shadows/blob_shadow.png";

        [MenuItem("TimeKiller/Setup/11 - Hall Realism (Lighting + Depth + Shadows)")]
        public static void Build()
        {
            var hall = GameObject.Find("CastleHall");
            if (hall == null)
            {
                Debug.LogError("[TimeKiller Setup] No CastleHall in scene — run Setup/9 first.");
                return;
            }

            var litMat = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
            var unlitMat = AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath);
            if (litMat == null || unlitMat == null)
            {
                Debug.LogError("[TimeKiller Setup] URP sprite materials not found.");
                return;
            }

            SetupRenderer2D();
            SwapMaterialsToLit(hall, litMat);
            AddLights(hall);
            AddProps(hall, litMat, unlitMat);
            AddContactShadows(hall, unlitMat);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hall.scene);
            Debug.Log("[TimeKiller Setup] Realism pass done: 2D lights + Y-depth props + contact shadows. Press Play in the dark.");
        }

        // ---------- 1. Renderer + materials ----------

        static void SetupRenderer2D()
        {
            var renderer2d = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DPath);
            if (renderer2d == null)
            {
                renderer2d = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer2d, Renderer2DPath);
            }

            // Y-axis transparency sorting: lower on screen renders in front.
            var so = new SerializedObject(renderer2d);
            so.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
            so.FindProperty("m_TransparencySortAxis").vector3Value = Vector3.up;
            so.ApplyModifiedPropertiesWithoutUndo();

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var pso = new SerializedObject(pipeline);
            var list = pso.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer2d;
            pso.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static void SwapMaterialsToLit(GameObject hall, Material litMat)
        {
            foreach (var r in hall.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = litMat;
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                foreach (var r in player.GetComponentsInChildren<SpriteRenderer>(true))
                    r.sharedMaterial = litMat;
                // Same sorting order as props: the Y axis decides who's in front.
                if (player.GetComponent<UnityEngine.Rendering.SortingGroup>() == null)
                    Undo.AddComponent<UnityEngine.Rendering.SortingGroup>(player.gameObject);
            }
        }

        // ---------- 2. Lights ----------

        static void AddLights(GameObject hall)
        {
            foreach (var old in hall.GetComponentsInChildren<Light2D>(true))
                Object.DestroyImmediate(old.gameObject);

            var global = new GameObject("GlobalLight");
            global.transform.SetParent(hall.transform, false);
            var g = global.AddComponent<Light2D>();
            g.lightType = Light2D.LightType.Global;
            g.color = new Color(0.42f, 0.47f, 0.58f); // cold moonlit ambient
            g.intensity = 0.32f;

            // A flickering warm light on each torch.
            foreach (var animator in hall.GetComponentsInChildren<TimeKiller.Core.SpriteAnimator>())
            {
                if (!animator.name.StartsWith("Torch")) continue;
                var lightGo = new GameObject("TorchLight");
                lightGo.transform.SetParent(animator.transform, false);
                var l = lightGo.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = new Color(1f, 0.62f, 0.28f);
                l.pointLightInnerRadius = 0.4f;
                l.pointLightOuterRadius = 4.5f;
                l.falloffIntensity = 0.8f;
                lightGo.AddComponent<FlickerLight2D>();
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null && player.GetComponentInChildren<Light2D>() == null)
            {
                var glowGo = new GameObject("PlayerGlow");
                glowGo.transform.SetParent(player.transform, false);
                var glow = glowGo.AddComponent<Light2D>();
                glow.lightType = Light2D.LightType.Point;
                glow.color = new Color(1f, 0.82f, 0.6f);
                glow.intensity = 0.65f;
                glow.pointLightInnerRadius = 0.3f;
                glow.pointLightOuterRadius = 2.6f;
                glow.falloffIntensity = 0.9f;
            }
        }

        // ---------- 3. Y-sorted props ----------

        static void AddProps(GameObject hall, Material litMat, Material unlitMat)
        {
            var existing = hall.transform.Find("Props");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var props = new GameObject("Props");
            props.transform.SetParent(hall.transform, false);

            var lookup = BuildLookup(MainSheet);
            var deco = BuildLookup("Assets/RF Castle/Sliced/decorative.png");
            var blob = AssetDatabase.LoadAssetAtPath<Sprite>(BlobPath);

            // Two freestanding pillars mid-room: walk behind AND in front of them.
            MakeProp(props.transform, lookup, litMat, unlitMat, blob, "Pillar", 26, 27, 2, 4, new Vector2(3.5f, 5f));
            MakeProp(props.transform, lookup, litMat, unlitMat, blob, "Pillar", 29, 27, 2, 4, new Vector2(12.5f, 5f));

            // Barrels and pots as real objects: collidable, Y-sorted, shadowed.
            // perItemColliders: every occupied column gets its OWN small collider,
            // so you can pass through the gaps between individual pots/barrels.
            MakeProp(props.transform, deco, litMat, unlitMat, blob, "Barrels", 5, 8, 3, 3, new Vector2(13.8f, 7.4f), perItemColliders: true);
            MakeProp(props.transform, deco, litMat, unlitMat, blob, "Pots", 1, 8, 3, 3, new Vector2(1.4f, 0.7f), perItemColliders: true);
        }

        static void MakeProp(Transform parent, Dictionary<string, Sprite> lookup, Material litMat,
            Material unlitMat, Sprite blob, string name, int colTL, int rowTL, int w, int h, Vector2 basePos,
            bool perItemColliders = false)
        {
            var prop = new GameObject(name);
            prop.transform.SetParent(parent, false);
            prop.transform.position = basePos; // root sits at the base → Y-sorts by feet
            var group = prop.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingOrder = 0; // same order as the player → custom axis decides

            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                {
                    if (!lookup.TryGetValue($"{colTL + i},{rowTL + j}", out var sprite)) continue;
                    var cell = new GameObject($"t{i}_{j}");
                    cell.transform.SetParent(prop.transform, false);
                    cell.transform.localPosition = new Vector3(i - w / 2f + 0.5f, (h - 1 - j) + 0.5f, 0f);
                    var sr = cell.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sharedMaterial = litMat;
                }

            if (blob != null)
            {
                var shadow = new GameObject("Shadow");
                shadow.transform.SetParent(prop.transform, false);
                shadow.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                shadow.transform.localScale = new Vector3(w * 1.4f, 0.9f, 1f);
                var sr = shadow.AddComponent<SpriteRenderer>();
                sr.sprite = blob;
                sr.color = new Color(0f, 0f, 0f, 0.5f);
                sr.sortingOrder = -1; // under the prop, inside its sorting group
                sr.sharedMaterial = unlitMat;
            }

            if (perItemColliders)
            {
                // One small round collider per occupied column — matches each
                // individual pot/barrel and leaves the gaps walkable.
                for (int i = 0; i < w; i++)
                {
                    bool occupied = false;
                    for (int j = 0; j < h && !occupied; j++)
                        occupied = lookup.ContainsKey($"{colTL + i},{rowTL + j}");
                    if (!occupied) continue;
                    var circle = prop.AddComponent<CircleCollider2D>();
                    circle.radius = 0.3f;
                    circle.offset = new Vector2(i - w / 2f + 0.5f, 0.3f);
                }
            }
            else
            {
                var box = prop.AddComponent<BoxCollider2D>();
                box.size = new Vector2(w * 0.7f, 0.5f);
                box.offset = new Vector2(0f, 0.25f);
            }
        }

        // ---------- 4. Contact shadows ----------

        static void AddContactShadows(GameObject hall, Material unlitMat)
        {
            var existing = hall.transform.Find("ContactShadows");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var holder = new GameObject("ContactShadows");
            holder.transform.SetParent(hall.transform, false);

            BakeGradientIfMissing();
            var gradient = AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath);
            if (gradient == null) return;

            // Dark strip along the north wall base, fading down onto the floor.
            Strip(holder.transform, gradient, unlitMat, new Vector2(8f, 9.5f), new Vector2(16f, 1.2f), 0f);
            // Side strips fading inward from the black side voids.
            Strip(holder.transform, gradient, unlitMat, new Vector2(0.4f, 5f), new Vector2(10f, 0.9f), 90f);
            Strip(holder.transform, gradient, unlitMat, new Vector2(15.6f, 5f), new Vector2(10f, 0.9f), -90f);
            // South edge, fading up from the overhead band.
            Strip(holder.transform, gradient, unlitMat, new Vector2(8f, 0.35f), new Vector2(16f, 0.8f), 180f);
        }

        static void Strip(Transform parent, Sprite sprite, Material unlitMat, Vector2 pos, Vector2 scale, float zRot)
        {
            var go = new GameObject("Shade");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0, 0, zRot);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0f, 0f, 0f, 0.55f);
            sr.sortingOrder = -13; // above floor (-20) and rug (-15), below walls
            sr.sharedMaterial = unlitMat;
        }

        static void BakeGradientIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath) != null) return;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f - (y / (float)(size - 1)); // opaque at top, clear at bottom
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            tex.Apply();
            File.WriteAllBytes(GradientPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GradientPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GradientPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f; // sprite = exactly 1x1 unit, scaled per strip
            importer.SaveAndReimport();
        }

        static Dictionary<string, Sprite> BuildLookup(string sheetPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            var dict = new Dictionary<string, Sprite>();
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>())
            {
                int col = (int)sprite.rect.x / 16;
                int rowTop = (texture.height - (int)sprite.rect.y - (int)sprite.rect.height) / 16;
                dict[$"{col},{rowTop}"] = sprite;
            }
            return dict;
        }
    }
}
