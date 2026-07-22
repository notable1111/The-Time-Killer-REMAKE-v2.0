// Menu: TimeKiller/Setup/14 - Build Castle Wing (map v1).
// Expands the map into a LOOP: Hall -> east corridor -> Guardroom (B) ->
// north corridor -> Great Chamber (C) -> west corridor -> Chapel (D) ->
// south corridor -> back into the Hall's west door.
//
// Walls are raised GENERICALLY from the floor plan: every new floor cell
// checks its neighbors — north faces (brick+ledge+dark+trim), south overhead
// bands and side columns appear wherever floor meets non-floor, so doorway
// openings handle themselves. Collision = one 1x1 box per wall cell adjacent
// to new floor, merged by CompositeCollider2D.
//
// The ONLY touch to the user's protected hall colliders (approved): the east
// and west wall boxes are each SPLIT into two (above/below the new doorways),
// preserving their hand-tuned X offsets and sizes.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Core;
using TimeKiller.Lighting;
using TimeKiller.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TimeKiller.EditorTools
{
    public static class CastleWingSetup
    {
        const string MainSheet = "Assets/Resources/Outsource/RF Castle/Sliced/mainlevbuild.png";
        const string DecoSheet = "Assets/Resources/Outsource/RF Castle/Sliced/decorative.png";
        const string TileFolder = "Assets/Resources/C#/Environment/Configs/CastleTiles";
        const string LightClipPath = "Assets/Resources/C#/Environment/Configs/TorchLight.asset";
        const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        const string UnlitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string BlobPath = "Assets/Resources/Assets/Shadows/blob_shadow.png";

        // Floor plan (x, y, w, h) — hall floor itself is NOT rebuilt.
        static readonly RectInt Hall = new RectInt(0, 0, 16, 10);
        static readonly RectInt[] NewAreas =
        {
            new RectInt(16, 4, 2, 3),    // east doorway (cut through hall wall)
            new RectInt(18, 4, 8, 3),    // east corridor
            new RectInt(26, 0, 12, 10),  // Room B — guardroom
            new RectInt(30, 10, 3, 10),  // B -> C corridor (north, unlit)
            new RectInt(20, 20, 16, 10), // Room C — great chamber
            new RectInt(0, 22, 20, 3),   // C -> D corridor (x0..1 included: reaches the chapel!)
            new RectInt(-10, 20, 10, 10),// Room D — chapel
            new RectInt(-7, 6, 3, 14),   // D -> hall corridor (south, unlit)
            new RectInt(-8, 4, 6, 3),    // west corridor
            new RectInt(-2, 4, 2, 3),    // west doorway (cut through hall wall)
        };

        static Dictionary<string, Sprite> mainLk, decoLk;
        static readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();

        [MenuItem("TimeKiller/Setup/14 - Build Castle Wing (map v1)")]
        public static void Build()
        {
            var hallGo = GameObject.Find("CastleHall");
            if (hallGo == null) { Debug.LogError("[TimeKiller Setup] CastleHall not found."); return; }
            var floor = Map(hallGo, "Floor"); var rug = Map(hallGo, "Rug");
            var wallFace = Map(hallGo, "WallFace"); var wallDecor = Map(hallGo, "WallDecor");
            var overhead = Map(hallGo, "Overhead");
            if (floor == null || wallFace == null || overhead == null || rug == null || wallDecor == null) return;

            tileCache.Clear();
            mainLk = Lookup(MainSheet); decoLk = Lookup(DecoSheet);
            if (mainLk == null || decoLk == null) return;

            var old = GameObject.Find("CastleWing");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var wing = new GameObject("CastleWing");
            Undo.RegisterCreatedObjectUndo(wing, "Build Castle Wing");

            // --- Floor sets.
            var newFloor = new HashSet<Vector2Int>();
            foreach (var r in NewAreas)
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int y = r.yMin; y < r.yMax; y++)
                        newFloor.Add(new Vector2Int(x, y));
            var allFloor = new HashSet<Vector2Int>(newFloor);
            for (int x = Hall.xMin; x < Hall.xMax; x++)
                for (int y = Hall.yMin; y < Hall.yMax; y++)
                    allFloor.Add(new Vector2Int(x, y));

            // --- Cut the two hall doorways (tiles) + split protected colliders.
            foreach (var cell in newFloor.Where(c => (c.x >= 16 && c.x <= 17) || (c.x >= -2 && c.x <= -1)))
            {
                wallFace.SetTile((Vector3Int)cell, null);
                wallDecor.SetTile((Vector3Int)cell, null);
                overhead.SetTile((Vector3Int)cell, null);
            }
            SplitHallSideColliders(hallGo);

            // --- Paint new floors.
            foreach (var c in newFloor)
                Set(floor, mainLk, Mod(c.x, 8), 27 + Mod(c.y, 8), c.x, c.y);

            // --- Generic walls from the plan.
            foreach (var c in newFloor)
            {
                // North face: anchored where the cell above is not floor.
                if (!allFloor.Contains(c + Vector2Int.up))
                    for (int i = 0; i < 8; i++)
                    {
                        var cell = new Vector2Int(c.x, c.y + 1 + i);
                        if (allFloor.Contains(cell)) break;
                        if (i < 4) Set(wallFace, mainLk, 6 + Mod(c.x, 10), 11 - i, cell.x, cell.y);
                        else if (i == 4) Set(wallFace, mainLk, 6 + Mod(c.x, 10), 7, cell.x, cell.y);
                        else if (i < 7) Set(wallFace, mainLk, 8 + Mod(c.x, 4), i == 5 ? 24 : 25, cell.x, cell.y);
                        else Set(wallFace, mainLk, 6 + Mod(c.x, 10), 0, cell.x, cell.y);
                    }
                // South band (overhead, renders over the player).
                if (!allFloor.Contains(c + Vector2Int.down))
                    for (int i = 0; i < 3; i++)
                    {
                        var cell = new Vector2Int(c.x, c.y - 1 - i);
                        if (allFloor.Contains(cell)) break;
                        if (i == 0) Set(overhead, mainLk, 6 + Mod(c.x, 10), 0, cell.x, cell.y);
                        else Set(overhead, mainLk, 8 + Mod(c.x, 4), i == 1 ? 24 : 25, cell.x, cell.y);
                    }
                // Side columns, 2 thick.
                foreach (int dir in new[] { -1, 1 })
                    if (!allFloor.Contains(new Vector2Int(c.x + dir, c.y)))
                        for (int t = 1; t <= 2; t++)
                        {
                            var cell = new Vector2Int(c.x + dir * t, c.y);
                            if (allFloor.Contains(cell)) break;
                            if (wallFace.GetTile((Vector3Int)cell) == null)
                                Set(wallFace, mainLk, 7 + Mod(cell.x, 2), 8 + Mod(cell.y, 4), cell.x, cell.y);
                        }
            }

            // --- Collision: 1x1 boxes on every wall cell adjacent to new floor.
            BuildWingColliders(wing.transform, newFloor, allFloor);

            // --- Camera bounds -> composite of per-zone boxes.
            RebuildCameraBounds();

            // --- Dressing: rugs/banners/window + props + torches.
            DressRooms(wing.transform, rug, wallDecor);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hallGo.scene);
            Debug.Log("[TimeKiller Setup] Castle wing built: loop Hall->B->C->D->Hall, walls auto-raised, colliders + camera zones + dressing done.");
        }

        // ---------- protected-collider surgery (approved) ----------

        static void SplitHallSideColliders(GameObject hallGo)
        {
            var holder = hallGo.transform.Find("Colliders");
            if (holder == null) { Debug.LogWarning("[TimeKiller Setup] Hall Colliders holder not found — door gaps rely on wing boxes only."); return; }

            // Count boxes per side: 2+ on a side means the doorway split already
            // happened (possibly hand-tuned since) — NEVER split that side again.
            var all = holder.GetComponents<BoxCollider2D>();
            int eastCount = all.Count(b => b.offset.x > 10f);
            int westCount = all.Count(b => b.offset.x < 0f);

            foreach (var box in all)
            {
                bool east = box.offset.x > 10f, west = box.offset.x < 0f;
                if (!east && !west) continue;
                if (east && eastCount >= 2) continue; // already split (protected)
                if (west && westCount >= 2) continue; // already split (protected)

                float top = box.offset.y + box.size.y / 2f, bottom = box.offset.y - box.size.y / 2f;
                const float doorBottom = 4f, doorTop = 7f;

                var upper = holder.gameObject.AddComponent<BoxCollider2D>();
                upper.offset = new Vector2(box.offset.x, (doorTop + top) / 2f);
                upper.size = new Vector2(box.size.x, top - doorTop);
                var lower = holder.gameObject.AddComponent<BoxCollider2D>();
                lower.offset = new Vector2(box.offset.x, (bottom + doorBottom) / 2f);
                lower.size = new Vector2(box.size.x, doorBottom - bottom);
                Undo.DestroyObjectImmediate(box);
            }
        }

        // ---------- collision ----------

        static void BuildWingColliders(Transform wing, HashSet<Vector2Int> newFloor, HashSet<Vector2Int> allFloor)
        {
            var go = new GameObject("WingColliders");
            go.transform.SetParent(wing, false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            // 100% coverage rule: EVERY non-floor cell touching ANY floor gets a
            // box — no exclusion zones (overlap with the hall's hand-tuned boxes
            // is harmless; gaps are walk-through-wall bugs).
            var cells = new HashSet<Vector2Int>();
            foreach (var f in allFloor)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = new Vector2Int(f.x + dx, f.y + dy);
                        if (!allFloor.Contains(n)) cells.Add(n);
                    }
            foreach (var c in cells)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.offset = new Vector2(c.x + 0.5f, c.y + 0.5f);
                box.size = Vector2.one;
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            go.AddComponent<CompositeCollider2D>();
        }

        // ---------- camera ----------

        static void RebuildCameraBounds()
        {
            var boundsGo = GameObject.Find("CameraBounds");
            if (boundsGo == null) { boundsGo = new GameObject("CameraBounds"); Undo.RegisterCreatedObjectUndo(boundsGo, "Camera Bounds"); }
            foreach (var col in boundsGo.GetComponents<Collider2D>()) Object.DestroyImmediate(col);

            var body = boundsGo.GetComponent<Rigidbody2D>();
            if (body == null) body = boundsGo.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            // (center, size) view zones per area — margins show the walls.
            var zones = new (Vector2 c, Vector2 s)[]
            {
                (new Vector2(8f, 7f), new Vector2(20f, 20f)),      // hall
                (new Vector2(21f, 5.5f), new Vector2(14f, 9f)),    // east corridor
                (new Vector2(32f, 7f), new Vector2(16f, 20f)),     // room B
                (new Vector2(31.5f, 15f), new Vector2(7f, 14f)),   // B-C corridor
                (new Vector2(28f, 27f), new Vector2(20f, 20f)),    // room C
                (new Vector2(11f, 23.5f), new Vector2(22f, 9f)),   // C-D corridor
                (new Vector2(-5f, 27f), new Vector2(14f, 20f)),    // room D
                (new Vector2(-5.5f, 13f), new Vector2(7f, 16f)),   // D-hall corridor
                (new Vector2(-5f, 5.5f), new Vector2(12f, 9f)),    // west corridor
            };
            foreach (var z in zones)
            {
                var box = boundsGo.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.offset = z.c;
                box.size = z.s;
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            var composite = boundsGo.AddComponent<CompositeCollider2D>();
            composite.isTrigger = true;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var confiner = Object.FindAnyObjectByType<CinemachineConfiner2D>();
            if (confiner != null)
            {
                confiner.BoundingShape2D = composite;
                EditorUtility.SetDirty(confiner);
            }
        }

        // ---------- dressing ----------

        static void DressRooms(Transform wing, Tilemap rug, Tilemap wallDecor)
        {
            // Room C: rug center + banners on the north face.
            Region(rug, decoLk, 25, 15, 6, 6, 25, 23);
            Region(wallDecor, decoLk, 0, 0, 3, 4, 23, 31);
            Region(wallDecor, decoLk, 0, 0, 3, 4, 30, 31);
            // Room D: stained-glass window on its north face.
            Region(wallDecor, mainLk, 25, 6, 6, 6, -8, 31);
            // Room B: banner accent.
            Region(wallDecor, decoLk, 0, 0, 3, 4, 30, 11);

            BuildWingDressing(wing, includeHallDressing: false);
        }

        // Props + torches only, no tile painting — also used by Setup/18, where
        // tiles come from CastleWing.ldtk. includeHallDressing adds the hall's
        // pillars/barrels/pots/torches (in the main scene those exist already
        // from Setup/9+11; a fresh LDtk scene needs them built here).
        public static void BuildWingDressing(Transform wing, bool includeHallDressing)
        {
            if (mainLk == null) mainLk = Lookup(MainSheet);
            if (decoLk == null) decoLk = Lookup(DecoSheet);
            if (mainLk == null || decoLk == null) return;
            var litMat = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
            var unlitMat = AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath);
            var blob = AssetDatabase.LoadAssetAtPath<Sprite>(BlobPath);

            var props = new GameObject("WingProps");
            props.transform.SetParent(wing, false);
            Prop(props.transform, mainLk, litMat, unlitMat, blob, "Pillar", 26, 27, 2, 4, new Vector2(29.5f, 5f), false);
            Prop(props.transform, mainLk, litMat, unlitMat, blob, "Pillar", 29, 27, 2, 4, new Vector2(34.5f, 5f), false);
            Prop(props.transform, decoLk, litMat, unlitMat, blob, "Barrels", 5, 8, 3, 3, new Vector2(36f, 8f), true);
            Prop(props.transform, mainLk, litMat, unlitMat, blob, "Pillar", 26, 27, 2, 4, new Vector2(23.5f, 24f), false);
            Prop(props.transform, mainLk, litMat, unlitMat, blob, "Pillar", 29, 27, 2, 4, new Vector2(32.5f, 24f), false);
            Prop(props.transform, decoLk, litMat, unlitMat, blob, "Pots", 1, 8, 3, 3, new Vector2(-8.5f, 21f), true);

            var torches = new GameObject("WingTorches");
            torches.transform.SetParent(wing, false);
            foreach (var p in new[]
            {
                new Vector2(21f, 9.5f), new Vector2(-4f, 9.5f),          // east + west corridors (west torch ON the wall, not in the dark connector)
                new Vector2(28.5f, 12.5f), new Vector2(35.5f, 12.5f),    // room B
                new Vector2(24f, 32.5f), new Vector2(31f, 32.5f),        // room C
                new Vector2(-7.5f, 32.5f), new Vector2(-3.5f, 32.5f),    // room D
            })
                Torch(torches.transform, p);
            // The two vertical connector corridors stay UNLIT on purpose — dark passages.

            if (!includeHallDressing) return;

            // Hall dressing, mirroring Setup/11's props and Setup/9's torches.
            Prop(props.transform, mainLk, litMat, unlitMat, blob, "Pillar", 26, 27, 2, 4, new Vector2(3.5f, 5f), false);
            Prop(props.transform, mainLk, litMat, unlitMat, blob, "Pillar", 29, 27, 2, 4, new Vector2(12.5f, 5f), false);
            Prop(props.transform, decoLk, litMat, unlitMat, blob, "Barrels", 5, 8, 3, 3, new Vector2(13.8f, 7.4f), true);
            Prop(props.transform, decoLk, litMat, unlitMat, blob, "Pots", 1, 8, 3, 3, new Vector2(1.4f, 0.7f), true);
            Torch(torches.transform, new Vector2(4.5f, 13f));
            Torch(torches.transform, new Vector2(11.5f, 13f));
        }

        static void Torch(Transform parent, Vector2 pos)
        {
            var clip = AssetDatabase.LoadAssetAtPath<SpriteAnimationClip>(LightClipPath);
            var go = new GameObject("Torch");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            if (clip != null && clip.frames.Length > 0) sr.sprite = clip.frames[0];
            sr.sortingOrder = -8;
            var anim = go.AddComponent<SpriteAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("playOnStart").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            var lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(go.transform, false);
            var l = lightGo.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(1f, 0.62f, 0.28f);
            l.pointLightInnerRadius = 0.4f;
            l.pointLightOuterRadius = 4.5f;
            l.falloffIntensity = 0.8f;
            lightGo.AddComponent<FlickerLight2D>();
        }

        static void Prop(Transform parent, Dictionary<string, Sprite> lookup, Material lit, Material unlit,
            Sprite blob, string name, int colTL, int rowTL, int w, int h, Vector2 basePos, bool perItem)
        {
            var prop = new GameObject(name);
            prop.transform.SetParent(parent, false);
            prop.transform.position = basePos;
            var group = prop.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingOrder = 0;

            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                {
                    if (!lookup.TryGetValue($"{colTL + i},{rowTL + j}", out var sprite)) continue;
                    var cell = new GameObject($"t{i}_{j}");
                    cell.transform.SetParent(prop.transform, false);
                    cell.transform.localPosition = new Vector3(i - w / 2f + 0.5f, (h - 1 - j) + 0.5f, 0f);
                    var sr = cell.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sharedMaterial = lit;
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
                sr.sortingOrder = -1;
                sr.sharedMaterial = unlit;
            }

            if (perItem)
            {
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

        // ---------- shared helpers ----------

        static int Mod(int a, int m) => ((a % m) + m) % m;
        static Tilemap Map(GameObject hall, string name) => hall.transform.Find(name)?.GetComponent<Tilemap>();

        static void Region(Tilemap map, Dictionary<string, Sprite> lookup, int colTL, int rowTL, int w, int h, int worldX, int worldY)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    Set(map, lookup, colTL + i, rowTL + j, worldX + i, worldY + (h - 1 - j));
        }

        static void Set(Tilemap map, Dictionary<string, Sprite> lookup, int col, int rowTop, int worldX, int worldY)
        {
            string prefix = lookup == mainLk ? "m" : "d";
            string key = $"{prefix}_{col}_{rowTop}";
            if (!tileCache.TryGetValue(key, out var tile) || tile == null)
            {
                if (!lookup.TryGetValue($"{col},{rowTop}", out var sprite)) return;
                Directory.CreateDirectory(TileFolder);
                string path = $"{TileFolder}/{key}.asset";
                tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, path);
                }
                tileCache[key] = tile;
            }
            map.SetTile(new Vector3Int(worldX, worldY, 0), tile);
        }

        static Dictionary<string, Sprite> Lookup(string sheetPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (texture == null) { Debug.LogError($"[TimeKiller Setup] Sheet not found: {sheetPath}"); return null; }
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
