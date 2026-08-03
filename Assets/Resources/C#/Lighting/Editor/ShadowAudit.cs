// Menu: TimeKiller/Verify/Shadow Audit (structural — judge contrast in Play).
//
// Reports what is RELIABLY knowable about 2D shadows from scene data, and
// deliberately does not pretend to measure how strongly they read.
//
// WHY THIS IS STRUCTURAL AND NOT A RENDER DIFF. The obvious probe — render the
// scene with shadows on, again with them off, diff the pixels — was written
// first and thrown away, because in this project it cannot work. Measured
// 2026-08-03, all through the real game camera and the real Game View path:
//
//   toggling every light's shadowsEnabled ....... 0.000% of pixels, peak 0/765
//   toggling every caster's castsShadows ........ 0.000% of pixels, peak 0/765
//   torch intensity 1.1 -> 20 ................... 0.000% of pixels, peak 0/765
//   every point light Multiply -> Additive ...... 0.000% of pixels, peak 0/765
//
// Changing a Light2D PROPERTY from a script in edit mode does not reach the
// rendered frame — not after cycling `enabled`, not after SceneView.RepaintAll,
// not after QueuePlayerLoopUpdate. Only adding or removing lights outright shows
// up (disabling all 25 flipped the frame to unlit albedo immediately). The
// player loop is not running, so the light data the 2D renderer holds is stale.
//
// The trap that makes this expensive: such a probe does not fail loudly. It
// returns a confident 0.00% and reads as "the shadows do nothing", which is a
// measurement that lies. Any visual judgement of lighting in this project has to
// be made in PLAY MODE. This audit covers the half that data can answer.
//
// It would have caught, in one call, both faults found the slow way: that the
// scene had zero ShadowCaster2D, and that all 25 lights sit on the Multiply
// blend style.
//
// Pure read-only: touches nothing, dirties nothing.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Lighting.EditorTools
{
    public static class ShadowAudit
    {
        const string ManiacConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";

        [MenuItem("TimeKiller/Verify/Shadow Audit (structural — judge contrast in Play)")]
        public static void Run() => Debug.Log(Report());

        public static string Report()
        {
            var casters = Object.FindObjectsByType<ShadowCaster2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var sb = new StringBuilder("[ShadowAudit] ")
                .Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name).Append('\n');

            // --- casters -----------------------------------------------------
            int walls = 0, props = 0, chars = 0, noMesh = 0;
            foreach (var c in casters)
            {
                // An overflow child is named __Shadow_N; it belongs to whatever
                // its parent is, or the counts misreport walls as props.
                var owner = c.transform;
                while (owner.parent != null && owner.name.StartsWith("__Shadow_")) owner = owner.parent;
                string n = owner.name;
                if (n == "IntGrid" || n == "HallColliders") walls++;
                else if (n == "Player" || n == "Maniac") chars++;
                else props++;
                if (c.mesh == null) noMesh++;
            }
            sb.Append("  casters .................. ").Append(casters.Length)
              .Append("   (walls ").Append(walls).Append(", props ").Append(props)
              .Append(", characters ").Append(chars).Append(")\n");
            if (casters.Length == 0)
                sb.Append("  *** nothing casts. Run TimeKiller/Setup/49.\n");

            // --- lights ------------------------------------------------------
            int points = 0, casting = 0;
            var blendStyles = new Dictionary<int, int>();
            foreach (var l in lights)
            {
                if (l.lightType != Light2D.LightType.Global) points++;
                if (l.shadowsEnabled) casting++;
                int bs = new SerializedObject(l).FindProperty("m_BlendStyleIndex").intValue;
                blendStyles[bs] = blendStyles.ContainsKey(bs) ? blendStyles[bs] + 1 : 1;
            }
            sb.Append("  point lights ............. ").Append(points)
              .Append(", of which casting shadows ").Append(casting).Append('\n');
            sb.Append("  blend styles ............. ");
            foreach (var kv in blendStyles)
                sb.Append(kv.Value).Append("x ").Append(BlendName(kv.Key)).Append("  ");
            sb.Append('\n');

            int multiply = blendStyles.ContainsKey(0) ? blendStyles[0] : 0;
            if (multiply == lights.Length && lights.Length > 0)
                sb.Append("  NOTE: every light is on Multiply. A Multiply light scales the sprite's own\n")
                  .Append("        colour toward full albedo rather than adding light of its own, so it\n")
                  .Append("        saturates and extra intensity stops doing anything. Whether that is\n")
                  .Append("        wrong for this art style is a judgement to make in Play, not here.\n");

            // --- can a shadow appear at all? ---------------------------------
            int reachable = 0;
            foreach (var c in casters)
            {
                foreach (var l in lights)
                {
                    if (!l.shadowsEnabled || l.lightType != Light2D.LightType.Point) continue;
                    if (Vector2.Distance(l.transform.position, c.transform.position) < l.pointLightOuterRadius)
                    { reachable++; break; }
                }
            }
            sb.Append("  casters inside a casting light's radius: ").Append(reachable)
              .Append(" of ").Append(casters.Length).Append('\n');
            if (casters.Length > 0 && reachable == 0)
                sb.Append("  *** no caster is inside any casting light — no shadow can appear anywhere.\n");
            if (casters.Length > 0 && noMesh == casters.Length)
                sb.Append("  NOTE: no caster has built a shadow mesh yet. Meshes build lazily on render,\n")
                  .Append("        so this is only meaningful after the scene has drawn at least once.\n");

            AppendStealthCost(sb);

            sb.Append("\n  Contrast is NOT measured here — see the header. Enter Play, stand beside a\n")
              .Append("  torch with a pillar or wardrobe between you and it, and judge it there.");
            return sb.ToString();
        }

        static string BlendName(int i)
        {
            switch (i)
            {
                case 0: return "Multiply";
                case 1: return "Additive";
                case 2: return "Multiply+Mask";
                case 3: return "Additive+Mask";
                default: return "style" + i;
            }
        }

        /// The gameplay side of torch strength, reported next to the visual side
        /// so raising one is never decided without seeing the other.
        static void AppendStealthCost(StringBuilder sb)
        {
            var maniacConfig = AssetDatabase.LoadAssetAtPath<Object>(ManiacConfigPath);
            if (maniacConfig == null) return;
            var ambientProp = new SerializedObject(maniacConfig).FindProperty("ambientExposure");
            if (ambientProp == null) return;

            Light2D torch = null;
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.gameObject.name == "TorchLight") { torch = l; break; }
            if (torch == null) return;

            // ManiacPerception.Exposure(): e = ambientExposure + intensity*(1 - d/outer),
            // clamped to 1. Solve for the distance at which the player reads as fully lit.
            var flicker = torch.GetComponent<FlickerLight2D>();
            float runtime = flicker != null
                ? new SerializedObject(flicker).FindProperty("baseIntensity").floatValue
                : torch.intensity;
            float outer = torch.pointLightOuterRadius;
            float radius = runtime <= 0f ? 0f : outer * (1f - (1f - ambientProp.floatValue) / runtime);

            sb.Append("  torch intensity in PLAY .. ").Append(runtime.ToString("0.00"))
              .Append("  (FlickerLight2D.baseIntensity — Light2D.intensity is ")
              .Append(torch.intensity.ToString("0.00")).Append(" and is overwritten every frame)\n");
            sb.Append("  fully-exposed radius ..... ").Append(Mathf.Max(0f, radius).ToString("0.00"))
              .Append("u of the ").Append(outer.ToString("0.0"))
              .Append("u torch radius — inside this, the maniac treats the player as maximally visible\n");
        }
    }
}
