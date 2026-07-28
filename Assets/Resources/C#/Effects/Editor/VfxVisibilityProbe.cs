// Menu: TimeKiller/Verify/VFX Visibility (measure, don't guess).
//
// Renders an effect prefab in the OPEN scene, at the real game camera's
// orthographic size, at the player's position, and reports how much of the
// screen it actually covers over time.
//
// This exists because of a mistake worth not repeating (2026-07-28): the blood
// burst was tuned against an isolated neutral backdrop and deliberately tinted
// DOWN to match the palette. In the actual castle -- floor luminance ~14 out of
// 255 -- that put the effect at 0.26% peak screen coverage, gone inside 0.75s.
// It looked fine in isolation and was invisible in the game. An effect's
// visibility is a property of the effect AND the scene behind it, so it has to
// be measured there.
//
// Read the numbers, not the screenshot: "coverage" is the share of pixels that
// differ from a clean plate of the same shot, so it counts what the effect
// actually adds rather than how bright it looks on its own.
//
// Pure read-only: renders offscreen to a RenderTexture, creates nothing that
// survives the call, never dirties the scene.
using System.Text;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Effects.EditorTools
{
    public static class VfxVisibilityProbe
    {
        const string BloodBurstPath = "Assets/Resources/Assets/Effects/BloodBurst.prefab";
        const int Width = 480, Height = 300;
        const int DiffThreshold = 18;      // ignore compression-level noise

        static readonly float[] SampleTimes = { 0.05f, 0.16f, 0.37f, 0.55f, 0.75f, 1.1f };

        [MenuItem("TimeKiller/Verify/VFX Visibility (measure, don't guess)")]
        public static void Run() => Debug.Log(Report(BloodBurstPath));

        public static string Report(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return $"[VfxProbe] Prefab not found: {prefabPath}";
            if (prefab.GetComponent<ParticleSystem>() == null)
                return $"[VfxProbe] {prefabPath} has no ParticleSystem — this probe only measures particle effects.";

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return "[VfxProbe] No player in the scene — nothing to measure against.";
            Vector3 at = player.transform.position;

            float orthoSize = 3.37f;
            var cineCam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (cineCam != null) orthoSize = cineCam.Lens.OrthographicSize;
            else if (Camera.main != null && Camera.main.orthographic) orthoSize = Camera.main.orthographicSize;

            var camGo = new GameObject("__vfxProbeCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.transform.position = new Vector3(at.x, at.y, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var clean = Grab(cam, rt);
            var sb = new StringBuilder("[VfxProbe] ").Append(System.IO.Path.GetFileNameWithoutExtension(prefabPath))
                      .Append("  camera ortho ").Append(orthoSize.ToString("0.00"))
                      .Append("  view ").Append((orthoSize * 2f).ToString("0.0")).Append("u tall\n");
            sb.Append("  floor reference luminance: ").Append(Luminance(AverageOf(clean)).ToString("0.0")).Append("/255\n");
            sb.Append("  time     coverage    meanRGB          luminance vs floor\n");

            float floorLum = Mathf.Max(0.01f, Luminance(AverageOf(clean)));
            float peak = 0f;
            foreach (float t in SampleTimes)
            {
                var inst = Object.Instantiate(prefab, at, Quaternion.identity);
                inst.hideFlags = HideFlags.HideAndDontSave;
                inst.GetComponent<ParticleSystem>().Simulate(t, true, true);
                var shot = Grab(cam, rt);
                Object.DestroyImmediate(inst);

                int changed = 0;
                Vector3 sum = Vector3.zero;
                var a = clean.GetPixels32();
                var b = shot.GetPixels32();
                for (int i = 0; i < a.Length; i++)
                {
                    int d = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                    if (d <= DiffThreshold) continue;
                    changed++;
                    sum += new Vector3(b[i].r, b[i].g, b[i].b);
                }
                Object.DestroyImmediate(shot);

                float coverage = 100f * changed / a.Length;
                peak = Mathf.Max(peak, coverage);
                Vector3 mean = changed > 0 ? sum / changed : Vector3.zero;
                sb.Append("  ").Append(t.ToString("0.00")).Append("s   ")
                  .Append(coverage.ToString("0.00")).Append("%      (")
                  .Append(Mathf.RoundToInt(mean.x)).Append(",").Append(Mathf.RoundToInt(mean.y)).Append(",").Append(Mathf.RoundToInt(mean.z))
                  .Append(")        ").Append((Luminance(mean) / floorLum).ToString("0.0")).Append("x\n");
            }

            Object.DestroyImmediate(clean);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);

            // Rules of thumb from the 2026-07-28 pass, not laws — but an effect
            // under ~1% peak coverage on a dark floor did not read in play.
            sb.Append("  VERDICT: peak ").Append(peak.ToString("0.00")).Append("% — ")
              .Append(peak < 0.5f ? "INVISIBLE, will be missed entirely"
                    : peak < 1.2f ? "faint, easy to miss"
                    : peak < 6f ? "reads clearly"
                    : "very large, check it does not overwhelm the scene");
            return sb.ToString();
        }

        static Texture2D Grab(Camera cam, RenderTexture rt)
        {
            cam.Render();
            var tex = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        static Vector3 AverageOf(Texture2D tex)
        {
            var p = tex.GetPixels32();
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < p.Length; i++) sum += new Vector3(p[i].r, p[i].g, p[i].b);
            return sum / p.Length;
        }

        static float Luminance(Vector3 rgb) => 0.299f * rgb.x + 0.587f * rgb.y + 0.114f * rgb.z;
    }
}
