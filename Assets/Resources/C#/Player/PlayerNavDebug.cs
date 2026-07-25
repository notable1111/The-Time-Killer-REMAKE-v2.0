// Lets F3 paint the map for the body YOU are driving.
//
// Why this exists: the maniac was the only registered nav source, so pressing
// F3 during a normal play session showed HIS map — sample box 0.85 — while you
// walked a 0.55 capsule. 4.9% of the floor you can actually stand on painted
// red, and every one of those cells is merely YELLOW (tight) on the player's
// own map. The overlay was right; it was answering a question about the wrong
// character. This registers the player so the question can be asked properly.
//
// The player has no path to draw — a human is steering — so NavPath is null and
// only the cells are painted. When a BotPilot drives this same body it registers
// itself separately, with its route.
//
// Dev-only, like DebugOverlay and CheatHotkeys; PlayerController attaches it at
// runtime so no scene or prefab needs editing (and none can drift out of sync).
using System.Collections.Generic;
using TimeKiller.Navigation;
using UnityEngine;

namespace TimeKiller.Player
{
    // The interface is implemented only in dev builds — declaring it while its
    // members are compiled out would not build. In release this is an empty
    // component that nothing ever attaches.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public class PlayerNavDebug : MonoBehaviour, INavDebugSource
    {
        WalkabilityGrid grid;
        GridPathfinder finder;
        bool tried;

        void Start() => NavDebugView.Register(this);
        void OnDestroy() => NavDebugView.Unregister(this);

        public string NavLabel => "Player";
        public IReadOnlyList<Vector2> NavPath => null;   // a human is steering
        public Vector2 NavPosition => transform.position;

        // Built on FIRST VIEW, not at startup: sampling the grid is ~33k physics
        // overlap queries, and nobody should pay that for a debug key they may
        // never press. NavDebugView only touches NavGrid for the source it is
        // currently showing, so the cost lands exactly when you ask for it.
        public WalkabilityGrid NavGrid
        {
            get { Build(); return grid; }
        }

        public GridPathfinder NavFinder
        {
            get { Build(); return finder; }
        }

        void Build()
        {
            if (tried) return;
            tried = true;

            // Read the real collider rather than hard-coding 0.55: PlayerSetup
            // owns that number, and a debug view that quietly disagrees with the
            // body it claims to describe is worse than no debug view.
            var col = GetComponent<CapsuleCollider2D>();
            if (col == null) { Debug.LogWarning("[PlayerNavDebug] no CapsuleCollider2D — cannot map this body."); return; }
            float width = col.bounds.size.x;

            var t0 = Time.realtimeSinceStartup;
            grid = new WalkabilityGrid(ComputeWorldBounds(), 0.5f, width, col.bounds.center);
            finder = new GridPathfinder(grid, width * 0.5f);
            Debug.Log($"[PlayerNavDebug] mapped a {width:0.00}-wide body in "
                    + $"{(Time.realtimeSinceStartup - t0) * 1000f:0} ms — F3 now shows YOUR map, not the maniac's.");
        }

        /// Re-sample after the layout opens (the gate). Next F3 paint picks it up.
        public void Rebuild() { tried = false; grid = null; finder = null; }

        // Same rule the maniac's navigator and BotPath use: the tilemaps define
        // the world. Kept local rather than shared because those two disagree on
        // which FindObjects overload to call, and unifying them is a separate job.
        static Bounds ComputeWorldBounds()
        {
            var maps = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None);
            bool any = false;
            Bounds b = new Bounds();
            foreach (var tm in maps)
            {
                var lb = tm.localBounds;
                Vector3 wc = tm.transform.TransformPoint(lb.center);
                var wb = new Bounds(wc, Vector3.Scale(lb.size, tm.transform.lossyScale));
                if (!any) { b = wb; any = true; } else b.Encapsulate(wb);
            }
            if (!any) b = new Bounds(Vector3.zero, new Vector3(80f, 80f, 0f));
            b.Expand(2f);
            return b;
        }
    }
#else
    public class PlayerNavDebug : MonoBehaviour { }
#endif
}
