// Shows, in the GAME view, exactly where an AI character believes it can walk.
// Press F3 to cycle: off -> maniac -> bot -> ... -> off.
//
//   red    = blocked (wall, furniture, or the dropped outside-the-castle void)
//   yellow = walkable, but too tight for THIS body — a shortcut across it would
//            scrape, so the smoother refuses it (see GridPathfinder.ClearLine)
//   green  = comfortably open
//   cyan   = the route this agent is walking right now, waypoint to waypoint
//
// Yellow is the point of the whole thing: "walkable" and "walkable by a body
// this wide" are different questions, and every wall-scraping bug we have had
// lived in the gap between them. Each agent paints its OWN colours because each
// has its own body — the maniac's yellow is far wider than the player's.
//
// Registration is push-based (agents call Register on themselves) so Navigation
// never has to know which characters exist; a new AI shows up in the cycle for
// free. The host GameObject creates itself on first registration, so there is
// nothing to add to a scene and nothing to remove before shipping — the whole
// class is compiled out of release builds, like DebugOverlay and CheatHotkeys.
using System.Collections.Generic;
using TimeKiller.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeKiller.Navigation
{
    /// Implemented by anything that navigates, so it can be inspected on screen.
    public interface INavDebugSource
    {
        string NavLabel { get; }                 // shown in the legend
        WalkabilityGrid NavGrid { get; }         // null until the agent has built one
        GridPathfinder NavFinder { get; }        // supplies this body's clearance need
        IReadOnlyList<Vector2> NavPath { get; }  // current route, or null
        Vector2 NavPosition { get; }
    }

    public class NavDebugView : MonoBehaviour
    {
        static readonly List<INavDebugSource> sources = new List<INavDebugSource>();

        public static void Register(INavDebugSource source)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (source == null || sources.Contains(source)) return;
            sources.Add(source);
            EnsureHost();
#endif
        }

        public static void Unregister(INavDebugSource source)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            sources.Remove(source);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static NavDebugView host;

        static void EnsureHost()
        {
            if (host != null) return;
            var go = new GameObject("[NavDebugView]");
            host = go.AddComponent<NavDebugView>();
        }

        int shown = -1;              // -1 = off, otherwise an index into sources
        INavDebugSource painted;     // which source the texture currently describes
        WalkabilityGrid paintedGrid; // and which grid instance — rebuilds invalidate it

        SpriteRenderer cells;
        LineRenderer route;
        Texture2D texture;

        static readonly Color Blocked = new Color(0.85f, 0.15f, 0.15f, 0.38f);
        static readonly Color Tight = new Color(0.95f, 0.80f, 0.15f, 0.34f);
        static readonly Color Open = new Color(0.20f, 0.85f, 0.35f, 0.16f);

        void Start()
        {
            CheatHotkeys.RegisterCheat(Key.F3, "Walkable map", Cycle);
            DebugOverlay.Watch("Nav view", () => shown < 0 || shown >= sources.Count
                ? "off (F3)"
                : $"{sources[shown].NavLabel} — red blocked / yellow tight / green open");
        }

        void OnDestroy()
        {
            DebugOverlay.Unwatch("Nav view");
            if (texture != null) Destroy(texture);
        }

        void Cycle()
        {
            shown++;
            if (shown >= sources.Count) shown = -1;
            painted = null;             // force a repaint for the newly shown body
            Apply();
        }

        void LateUpdate()
        {
            if (shown < 0) return;
            if (shown >= sources.Count) { shown = -1; Apply(); return; }

            var src = sources[shown];
            // Repaint when the agent rebuilds its grid (a door opened, a level
            // reloaded) — otherwise we would keep showing a map of the old world.
            if (src != painted || src.NavGrid != paintedGrid) Apply();
            DrawRoute(src);
        }

        void Apply()
        {
            EnsureVisuals();
            bool on = shown >= 0 && shown < sources.Count;
            cells.enabled = on;
            route.enabled = on;
            if (!on) { painted = null; paintedGrid = null; return; }

            var src = sources[shown];
            var grid = src.NavGrid;
            if (grid == null) { cells.enabled = false; route.enabled = false; return; }

            Paint(src, grid);
            painted = src;
            paintedGrid = grid;
        }

        // One texel per grid cell. Point filtering keeps the cell edges crisp, so
        // you can count nodes on screen and match them to the numbers in code.
        void Paint(INavDebugSource src, WalkabilityGrid grid)
        {
            int w = grid.Cols, h = grid.Rows;
            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null) Destroy(texture);
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
            }

            int need = src.NavFinder != null ? src.NavFinder.RequiredClearanceCells : 1;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (!grid.Walkable(x, y)) c = Blocked;
                    else if (grid.ClearanceCells(x, y) < need) c = Tight;
                    else c = Open;
                    pixels[y * w + x] = c;
                }
            texture.SetPixels32(pixels);
            texture.Apply(false);

            float cell = grid.CellSize;
            // pivot (0,0) + pixelsPerUnit 1/cell makes texel (0,0) span [pos, pos+cell];
            // shifting back half a cell centres it on node (0,0)'s world position.
            if (cells.sprite != null) Destroy(cells.sprite);
            cells.sprite = Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.zero, 1f / cell,
                                         0, SpriteMeshType.FullRect);
            cells.transform.position = (Vector3)(grid.Origin - new Vector2(cell, cell) * 0.5f);
        }

        void DrawRoute(INavDebugSource src)
        {
            var path = src.NavPath;
            if (path == null || path.Count == 0) { route.positionCount = 0; return; }

            // Start the line at the agent so the first leg shows the direction it
            // is actually being steered, not just where the remaining path begins.
            route.positionCount = path.Count + 1;
            route.SetPosition(0, src.NavPosition);
            for (int i = 0; i < path.Count; i++) route.SetPosition(i + 1, path[i]);
        }

        // Unlit on purpose. The scene is lit by URP 2D lights and this is a horror
        // game — a lit debug overlay is black in exactly the dark corners we most
        // need to inspect. Top sorting layer so furniture cannot cover it.
        void EnsureVisuals()
        {
            if (cells != null) return;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);

            var layers = SortingLayer.layers;
            int topLayer = layers.Length > 0 ? layers[layers.Length - 1].id : 0;

            var cellsGo = new GameObject("Cells");
            cellsGo.transform.SetParent(transform, false);
            cells = cellsGo.AddComponent<SpriteRenderer>();
            cells.sharedMaterial = material;
            cells.sortingLayerID = topLayer;
            cells.sortingOrder = 32000;

            var routeGo = new GameObject("Route");
            routeGo.transform.SetParent(transform, false);
            route = routeGo.AddComponent<LineRenderer>();
            route.material = material;
            route.useWorldSpace = true;
            route.widthMultiplier = 0.09f;
            route.numCornerVertices = 2;
            route.startColor = route.endColor = new Color(0.2f, 0.95f, 1f, 0.95f);
            route.sortingLayerID = topLayer;
            route.sortingOrder = 32001;

            cells.enabled = false;
            route.enabled = false;
        }
#endif
    }
}
