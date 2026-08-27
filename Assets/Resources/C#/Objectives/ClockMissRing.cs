// The mistake you can SEE leave the clock.
//
// A fumbled skill check already made a noise the maniac could hear — but
// silently, as far as the player was concerned. The consequence arrived a minute
// later when he walked in, and by then nobody connects it to the press they got
// wrong. A rule the player cannot learn is not a mechanic, it is bad luck.
//
// THE RING IS HONEST. Its radius is the noise's REAL reach —
// loudness x the maniac's hearingRadius — so it is information, not decoration.
// When fear doubles the loudness, the player watches the ring get visibly bigger
// and learns the rule in one go: panicking is what gets you caught.
//
// What it deliberately does NOT show: where HE is. The ring is drawn around the
// PLAYER'S OWN noise, at the player's own position, so it leaks nothing about
// the maniac. Same contract as the heartbeat — it tells you about your situation,
// never his location.
//
// Procedural, so it needs no art asset: one LineRenderer circle, one pooled
// object, no per-miss allocation. Removable — delete the component and misses go
// back to being silent-but-costly.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Objectives
{
    public class ClockMissRing : MonoBehaviour
    {
        // SELF-INSTALLING (2026-08-27), because it had no installer at all and
        // that is exactly how it went missing. It was hand-placed in CastleWing
        // and no Setup script creates it, so Catacombs never got one: the Feature
        // Install Audit found it as one of 23 components living in one level and
        // absent from the other. Everything it needs it already loads itself (the
        // config comes from Resources in Awake) and it draws its own ring, so
        // there is nothing a scene has to provide.
        //
        // A hand-placed instance still wins - two rings would draw two circles on
        // one miss - so the castle's existing object keeps working untouched.
        static ClockMissRing instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            if (FindAnyObjectByType<ClockMissRing>() != null) return;

            var host = new GameObject("[ClockMissRing]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<ClockMissRing>();
        }

        [SerializeField] ClockConfig config;
        [Tooltip("Points around the circle. 48 is smooth at any size the ring reaches.")]
        [SerializeField] int segments = 48;
        [Tooltip("Line width in world units.")]
        [SerializeField] float width = 0.07f;
        [Tooltip("Sorting order. Above the floor and the blood (-6), below characters (0).")]
        [SerializeField] int sortingOrder = -3;

        LineRenderer ring;
        float startedAt = -1f;
        float targetRadius;
        Color colour;
        float hearingRadius = 9f;

        public int RingsShown { get; private set; }

        void Awake()
        {
            if (config == null)
                config = Resources.Load<ClockConfig>("C#/Objectives/Configs/ClockConfig");

            // The maniac's own hearing radius is the ONLY honest scale for this —
            // read once, so the ring cannot drift out of sync with the sense it
            // is illustrating. Falls back to 9 if the config is gone.
            var maniacConfig = Resources.Load<TimeKiller.Maniac.ManiacConfig>("C#/Maniac/Configs/ManiacConfig");
            if (maniacConfig != null) hearingRadius = maniacConfig.hearingRadius;

            BuildRing();
        }

        void Start()
        {
            EventBus.Subscribe<ClockMissEvent>(OnMiss);
            DebugOverlay.Watch("MissRing", () => config == null ? "NO CONFIG"
                : (config.showMissRing ? $"{RingsShown} shown  (reach x{hearingRadius:0.#})" : "off"));
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ClockMissEvent>(OnMiss);
            DebugOverlay.Unwatch("MissRing");
        }

        void BuildRing()
        {
            var go = new GameObject("~MissRing") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = segments;
            ring.widthMultiplier = width;
            ring.sortingOrder = sortingOrder;
            // Unlit: the castle is lit by URP 2D lights, and a lit ring would be
            // invisible in exactly the dark corners a repair usually happens in.
            ring.material = new Material(Shader.Find("Sprites/Default"));
            ring.enabled = false;

            // Unit circle, scaled at draw time — building the points once means a
            // miss costs no allocation at the moment the player is least able to
            // afford a hitch.
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }
        }

        void OnMiss(ClockMissEvent e)
        {
            if (config == null || !config.showMissRing || ring == null) return;
            ring.transform.position = e.Position;
            // The real reach of the noise, not an invented number.
            targetRadius = Mathf.Max(0.5f, e.Loudness * hearingRadius);
            colour = Color.Lerp(config.missRingCalmColor, config.missRingPanicColor, Mathf.Clamp01(e.Fear));
            startedAt = Time.time;
            ring.enabled = true;
            RingsShown++;
        }

        void Update()
        {
            if (startedAt < 0f || ring == null) return;
            float t = (Time.time - startedAt) / Mathf.Max(0.05f, config.missRingSeconds);
            if (t >= 1f) { ring.enabled = false; startedAt = -1f; return; }

            // Fast out, slow settle — a noise leaves quickly and the edge of it
            // dies away. A linear expansion reads as a UI wipe, not a sound.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            ring.transform.localScale = Vector3.one * (targetRadius * eased);
            // Width shrinks with the scale so the stroke stays a constant
            // thickness on screen instead of ballooning with the circle.
            ring.widthMultiplier = width / Mathf.Max(0.01f, targetRadius * eased);

            var c = colour;
            c.a = colour.a * (1f - t) * (1f - t);
            ring.startColor = c;
            ring.endColor = c;
        }
    }
}
