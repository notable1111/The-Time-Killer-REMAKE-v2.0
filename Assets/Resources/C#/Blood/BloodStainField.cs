// Every stain the player has left, drawn as ONE mesh.
//
// Why a mesh and not GameObjects: a run can spill blood hundreds of times, and
// a pooled-GameObject decal system pays a transform and a renderer per mark.
// This keeps a fixed-size ring buffer and rebuilds a single mesh, so the cost is
// one draw call and one allocation whether there are 3 stains or 220 — and when
// the buffer is full the OLDEST stain is overwritten, which means a long run can
// never degrade into a slideshow.
//
// Presentation only. It listens for BloodSpilledEvent and knows nothing about
// the player, the maniac, or health. Delete this object and the game runs
// unchanged; blood simply stops being recorded.
//
// Two details that are load-bearing:
//   ROTATION IS QUANTISED to quarter turns (plus a mirror). Arbitrary angles
//   would resample the sprite off the pixel grid and reintroduce exactly the
//   mush this art was rewritten to avoid.
//   DRYING is refreshed at ~5Hz and only while something is still wet, then
//   stops touching the mesh entirely. Fading every stain every frame forever is
//   the obvious way to write this and the reason decal systems get blamed for
//   frame drops.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Blood
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BloodStainField : MonoBehaviour
    {
        [SerializeField] BloodConfig config;
        [Tooltip("Atlas layout of the droplet sheet — set by Setup/39 so a new sheet needs no code change.")]
        [SerializeField] int atlasColumns = 3;
        [SerializeField] int atlasRows = 2;

        struct Stain
        {
            public Vector2 Position;
            public float Size;
            public int QuarterTurns;   // 0..3 — keeps the sprite on the pixel grid
            public bool Mirror;
            public int Tile;
            public float BornAt;
        }

        Stain[] stains;
        int count;
        int nextSlot;

        Mesh mesh;
        Vector3[] vertices;
        Vector2[] uvs;
        Color[] colors;
        int[] triangles;

        float nextColorRefresh;
        bool geometryDirty;

        /// Stains currently recorded, for the F1 overlay and for tests.
        public int StainCount => count;

        void Awake()
        {
            int capacity = config != null ? Mathf.Max(8, config.capacity) : 220;
            stains = new Stain[capacity];
            vertices = new Vector3[capacity * 4];
            uvs = new Vector2[capacity * 4];
            colors = new Color[capacity * 4];
            triangles = new int[capacity * 6];
            for (int i = 0; i < capacity; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t + 0] = v + 0; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 0; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }

            mesh = new Mesh { name = "BloodStains" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            transform.position = Vector3.zero;   // stains are authored in world space
        }

        void Start()
        {
            EventBus.Subscribe<BloodSpilledEvent>(OnSpilled);
            DebugOverlay.Watch("Blood", () => $"{count} stains (cap {stains.Length})");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<BloodSpilledEvent>(OnSpilled);
            DebugOverlay.Unwatch("Blood");
            if (mesh != null) Destroy(mesh);
        }

        void OnSpilled(BloodSpilledEvent evt)
        {
            if (config == null) return;
            int n = Mathf.Max(1, evt.Count);
            for (int i = 0; i < n; i++)
            {
                Vector2 offset = Random.insideUnitCircle * evt.Spread;
                // A directed spill lands mostly downrange, so a hit paints the
                // floor the way the blood actually flew rather than as a disc.
                if (evt.Direction.sqrMagnitude > 0.0001f)
                    offset += evt.Direction.normalized * Random.Range(0f, evt.Spread);

                Add(evt.Position + offset,
                    Random.Range(config.sizeRange.x, config.sizeRange.y) * Mathf.Max(0.25f, evt.Amount));
            }
        }

        void Add(Vector2 position, float size)
        {
            stains[nextSlot] = new Stain
            {
                Position = position,
                Size = size,
                QuarterTurns = Random.Range(0, 4),
                Mirror = Random.value < 0.5f,
                Tile = Random.Range(0, Mathf.Max(1, atlasColumns * atlasRows)),
                BornAt = Time.time,
            };
            nextSlot = (nextSlot + 1) % stains.Length;
            if (count < stains.Length) count++;
            geometryDirty = true;
        }

        void LateUpdate()
        {
            if (config == null || count == 0) return;

            bool anythingWet = Time.time - stains[OldestWetIndex()].BornAt < config.dryingSeconds;
            bool refreshColours = anythingWet && Time.time >= nextColorRefresh;
            if (!geometryDirty && !refreshColours) return;
            if (refreshColours) nextColorRefresh = Time.time + 0.2f;

            Rebuild();
            geometryDirty = false;
        }

        /// The most recently added stain is the wettest; if IT is dry, they all are.
        int OldestWetIndex() => (nextSlot - 1 + stains.Length) % stains.Length;

        void Rebuild()
        {
            float uStep = 1f / Mathf.Max(1, atlasColumns);
            float vStep = 1f / Mathf.Max(1, atlasRows);

            for (int i = 0; i < count; i++)
            {
                var s = stains[i];
                int v = i * 4;
                float half = s.Size * 0.5f;

                // Quarter turns only — the sprite stays axis-aligned to the pixel grid.
                Vector2 right = s.QuarterTurns switch
                {
                    1 => new Vector2(0f, half),
                    2 => new Vector2(-half, 0f),
                    3 => new Vector2(0f, -half),
                    _ => new Vector2(half, 0f),
                };
                Vector2 up = new Vector2(-right.y, right.x);

                Vector2 p = s.Position;
                vertices[v + 0] = p - right - up;
                vertices[v + 1] = p - right + up;
                vertices[v + 2] = p + right + up;
                vertices[v + 3] = p + right - up;

                int col = s.Tile % Mathf.Max(1, atlasColumns);
                int row = s.Tile / Mathf.Max(1, atlasColumns);
                float u0 = col * uStep, u1 = u0 + uStep;
                // Atlas row 0 is the TOP row of the image, but v=1 is the top in
                // UV space, so rows are counted down from 1.
                float v1 = 1f - row * vStep, v0 = v1 - vStep;
                if (s.Mirror) (u0, u1) = (u1, u0);

                uvs[v + 0] = new Vector2(u0, v0);
                uvs[v + 1] = new Vector2(u0, v1);
                uvs[v + 2] = new Vector2(u1, v1);
                uvs[v + 3] = new Vector2(u1, v0);

                float dryness = config.dryingSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01((Time.time - s.BornAt) / config.dryingSeconds);
                Color tint = Color.Lerp(config.freshTint, config.driedTint, dryness);
                colors[v + 0] = colors[v + 1] = colors[v + 2] = colors[v + 3] = tint;
            }

            // Collapse unused slots to a degenerate point rather than resizing the
            // arrays — no per-spill allocation.
            for (int i = count * 4; i < vertices.Length; i++) vertices[i] = Vector3.zero;

            mesh.Clear(true);
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        /// Wipes every stain — used by a run restart so a new attempt starts clean.
        public void Clear()
        {
            count = 0;
            nextSlot = 0;
            geometryDirty = true;
            if (mesh != null) mesh.Clear();
        }
    }
}
