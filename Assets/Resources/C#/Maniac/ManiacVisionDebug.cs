// Draws the maniac's ACTUAL vision geometry in the GAME view. Press F4 to toggle.
//
//   wide faint wedge  = peripheral vision — "corner of his eye". Reduced strength
//                       (peripheralWeight) and CLOSE ONLY (peripheralRange), which
//                       is why it is short and fat rather than long.
//   narrow solid wedge= central vision — full-strength detection, out to sightRange.
//   small ring        = proximityRange. Inside this he senses you regardless of
//                       facing; it is the reason walking into his back still kills.
//
// The wedge COLOUR is his awareness: grey Unaware -> amber Suspicious -> red
// Detected. So the fill tells you WHERE he can see and the colour tells you how
// much he currently knows — the two questions that the F1 numbers alone cannot
// place on the map.
//
// The geometry is read from ManiacConfig every frame and mirrors DetectionRate()
// exactly, including that it draws centralConeAngle and NOT the legacy
// sightConeAngle (140) — that one now only governs which belief cells SearchState
// collapses, not whether you are seen. A debug view that draws a cone the code
// does not use is worse than no debug view at all.
//
// NOT drawn: line of sight. Walls still block detection via linecast, so a wall
// inside the wedge is a hole in it. Colour is the check for that — stand behind
// cover inside the wedge and the meter must not climb.
//
// Hosts itself on first frame and is compiled out of release builds, exactly like
// DebugOverlay, CheatHotkeys and NavDebugView. Deleting this file removes the
// feature completely: nothing references it.
using TimeKiller.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeKiller.Maniac
{
    public class ManiacVisionDebug : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureHost()
        {
            if (FindAnyObjectByType<ManiacVisionDebug>() != null) return;
            new GameObject("[ManiacVisionDebug]").AddComponent<ManiacVisionDebug>();
        }

        const int Segments = 48;

        ManiacController maniac;
        MeshRenderer peripheral, central, proximity;
        Mesh peripheralMesh, centralMesh, proximityMesh;
        float builtCentral = -1f, builtPeripheral = -1f, builtProximity = -1f;
        bool visible;

        // Where he THINKS you are during a suspicion. Drawn in world space, so the
        // gap between this marker and your actual sprite IS the near-miss — the
        // one thing about the guess that cannot be judged from numbers.
        MeshRenderer guess;
        Mesh guessMesh;

        // Grey -> amber -> red, matching the Awareness line in the F1 overlay.
        static readonly Color Unaware = new Color(0.55f, 0.60f, 0.70f);
        static readonly Color Suspicious = new Color(1f, 0.72f, 0.15f);
        static readonly Color Detected = new Color(1f, 0.18f, 0.12f);

        void Start()
        {
            CheatHotkeys.RegisterCheat(Key.F4, "Vision cone", Toggle);
            DebugOverlay.Watch("Vision cone", () => !visible
                ? "off (F4)"
                : "wide faint = peripheral / narrow = central / ring = point-blank / amber dot = his GUESS");
        }

        void OnDestroy()
        {
            DebugOverlay.Unwatch("Vision cone");
            if (peripheralMesh != null) Destroy(peripheralMesh);
            if (centralMesh != null) Destroy(centralMesh);
            if (proximityMesh != null) Destroy(proximityMesh);
            if (guessMesh != null) Destroy(guessMesh);
            if (guess != null) Destroy(guess.gameObject);   // unparented, so it won't go with us
        }

        void Toggle()
        {
            visible = !visible;
            Apply();
        }

        void Apply()
        {
            if (peripheral == null) return;
            bool on = visible && maniac != null;
            peripheral.enabled = on;
            central.enabled = on;
            proximity.enabled = on;
            // Only ever turned OFF here: LateUpdate decides when it is on, and it
            // lives outside this transform so toggling would otherwise strand it.
            if (guess != null && !on) guess.enabled = false;
        }

        void LateUpdate()
        {
            if (!visible) return;
            if (maniac == null)
            {
                maniac = FindAnyObjectByType<ManiacController>();
                if (maniac == null) return;
            }
            var cfg = maniac.Config;
            var per = maniac.Perception;
            if (cfg == null || per == null) return;

            EnsureVisuals();

            // Rebuild only when a tunable actually changes, so the Inspector stays
            // live while tuning without regenerating three meshes every frame.
            if (!Mathf.Approximately(builtCentral, cfg.centralConeAngle + cfg.sightRange))
            {
                BuildWedge(centralMesh, cfg.sightRange, cfg.centralConeAngle * 0.5f);
                builtCentral = cfg.centralConeAngle + cfg.sightRange;
            }
            if (!Mathf.Approximately(builtPeripheral, cfg.peripheralConeAngle + cfg.peripheralRange))
            {
                BuildWedge(peripheralMesh, cfg.peripheralRange, cfg.peripheralConeAngle * 0.5f);
                builtPeripheral = cfg.peripheralConeAngle + cfg.peripheralRange;
            }
            if (!Mathf.Approximately(builtProximity, cfg.proximityRange))
            {
                BuildWedge(proximityMesh, cfg.proximityRange, 180f);
                builtProximity = cfg.proximityRange;
            }

            // Follow him, and aim along the SAME FacingDirection the perception
            // uses — so a frozen cone would visibly stop turning here.
            var facing = per.FacingDirection;
            if (facing.sqrMagnitude < 0.0001f) facing = Vector2.down;
            transform.position = maniac.Motor.Position;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);

            var tint = per.Level == ManiacPerception.AwarenessLevel.Detected ? Detected
                     : per.Level == ManiacPerception.AwarenessLevel.Suspicious ? Suspicious
                     : Unaware;
            central.material.color = new Color(tint.r, tint.g, tint.b, 0.22f);
            peripheral.material.color = new Color(tint.r, tint.g, tint.b, 0.09f);
            proximity.material.color = new Color(tint.r, tint.g, tint.b, 0.30f);

            // The guess marker sits OUTSIDE the rotating host, or it would spin
            // with his facing instead of staying on the spot he is walking to.
            bool suspecting = per.Level == ManiacPerception.AwarenessLevel.Suspicious
                              && per.LastNoiseCause == NoiseCause.Suspicion;
            guess.enabled = visible && suspecting;
            if (guess.enabled)
            {
                guess.transform.SetPositionAndRotation(per.LastNoisePosition, Quaternion.identity);
                guess.material.color = new Color(Suspicious.r, Suspicious.g, Suspicious.b, 0.55f);
            }

            Apply();
        }

        /// Triangle fan along +X, so the transform's rotation aims it.
        static void BuildWedge(Mesh mesh, float radius, float halfAngleDeg)
        {
            var verts = new Vector3[Segments + 2];
            var tris = new int[Segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.Lerp(-halfAngleDeg, halfAngleDeg, i / (float)Segments) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            for (int i = 0; i < Segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        // Unlit, for the same reason NavDebugView is: this is a horror game lit by
        // URP 2D lights, and a lit debug overlay goes black in exactly the dark
        // corners worth inspecting. Top sorting layer so furniture can't cover it.
        void EnsureVisuals()
        {
            if (central != null) return;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var layers = SortingLayer.layers;
            int topLayer = layers.Length > 0 ? layers[layers.Length - 1].id : 0;

            peripheralMesh = new Mesh { name = "PeripheralCone" };
            centralMesh = new Mesh { name = "CentralCone" };
            proximityMesh = new Mesh { name = "ProximityRing" };

            // Peripheral first / lowest order: the narrow central wedge reads on
            // top of it, so overlapping strength is visible as a brighter core.
            peripheral = MakeLayer("Peripheral", peripheralMesh, shader, topLayer, 31998);
            central = MakeLayer("Central", centralMesh, shader, topLayer, 31999);
            proximity = MakeLayer("Proximity", proximityMesh, shader, topLayer, 32000);

            guessMesh = new Mesh { name = "GuessMarker" };
            BuildWedge(guessMesh, 0.45f, 180f);
            guess = MakeLayer("Guess", guessMesh, shader, topLayer, 32001);
            guess.transform.SetParent(null, true);   // world space — must not inherit his rotation
        }

        MeshRenderer MakeLayer(string layerName, Mesh mesh, Shader shader, int sortingLayer, int order)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(shader);
            mr.sortingLayerID = sortingLayer;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;
            return mr;
        }
#endif
    }
}
