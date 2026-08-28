// Menu: TimeKiller/Verify/Darkness Reachable.
//
// Answers one question that cost a day to ask by hand: CAN the player ever be
// in darkness at all?
//
// It exists because on 2026-08-28 the answer was no, and nothing said so. The
// scene parents a Light2D to the player (PlayerGlow), LightSampler2D sums every
// active light including that one, and PlayerComposure samples at the player's
// own position — so the carried light contributed its full intensity forever.
// Against CastleWing's 0.32 global light the floor was 0.97 and the darkness
// threshold is 0.40, making "only genuine darkness drains" unreachable as
// arithmetic. It took 24 bot runs to notice, because a carried lamp and a
// brightly lit room produce the same number.
//
// CLAUDE.md's rule is that a question you would eyeball twice should become a
// measurement. This is that measurement, and it is deliberately about the
// INVARIANT (is the threshold reachable) rather than about any one spot.
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using TimeKiller.Sanity;

namespace TimeKiller.Sanity.EditorTools
{
    public static class DarknessReachableVerify
    {
        [MenuItem("TimeKiller/Verify/Darkness Reachable")]
        public static void Run() => Debug.Log(Report());

        public static string Report()
        {
            var cfg = Resources.Load<ComposureConfig>(ComposureConfig.ResourcesPath);
            var player = Object.FindAnyObjectByType<TimeKiller.Player.PlayerController>();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[TimeKiller Verify] Darkness Reachable");

            if (cfg == null) { sb.AppendLine("  FAIL - no ComposureConfig"); return sb.ToString(); }
            if (player == null) { sb.AppendLine("  SKIP - no player in the open scene"); return sb.ToString(); }

            float threshold = cfg.darkAtOrBelow;

            // Everything the player cannot walk away from: global lights, plus any
            // light parented to the player, measured at zero distance.
            float carried = 0f, globals = 0f;
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.lightType == Light2D.LightType.Global) { globals += Mathf.Max(0f, l.intensity); continue; }
                if (!l.transform.IsChildOf(player.transform)) continue;
                float d = Vector2.Distance(player.transform.position, l.transform.position);
                float outer = l.pointLightOuterRadius;
                if (outer <= 0.0001f || d >= outer) continue;
                float inner = Mathf.Min(l.pointLightInnerRadius, outer);
                float falloff = d <= inner ? 1f : 1f - Mathf.InverseLerp(inner, outer, d);
                carried += Mathf.Max(0f, l.intensity) * falloff;
            }

            var at = (Vector2)player.transform.position;
            float withGlow = LightSampler2D.LevelAt(at);
            float withoutGlow = LightSampler2D.LevelAt(at, player.transform);

            // Is the exclusion actually wired up? If the player carries light and
            // excluding it changes nothing, the caller is not passing the player
            // transform and the original bug is back — that is the regression this
            // probe exists to catch, and it is invisible in any single reading.
            bool exclusionWired = carried <= 0.0001f || !Mathf.Approximately(withGlow, withoutGlow);

            // What the mechanic can never get below. Carried light only belongs in
            // that floor when it is (wrongly) being counted.
            float effectiveFloor = exclusionWired ? globals : globals + carried;

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine(string.Format(ci, "  threshold (darkAtOrBelow)     {0:0.00}", threshold));
            sb.AppendLine(string.Format(ci, "  global light everywhere       {0:0.00}", globals));
            sb.AppendLine(string.Format(ci, "  carried by the player         {0:0.00}", carried));
            sb.AppendLine(string.Format(ci, "  at the player now:  with own light {0:0.00}   excluding it {1:0.00}",
                                        withGlow, withoutGlow));
            sb.AppendLine("  carried light excluded?       " + (exclusionWired ? "yes" : "NO"));
            sb.AppendLine(string.Format(ci, "  unavoidable floor             {0:0.00}", effectiveFloor));

            if (!exclusionWired)
                sb.AppendLine(string.Format(ci,
                    "  FAIL - the player carries {0:0.00} of light and it is being counted as room light. " +
                    "Floor {1:0.00} > threshold {2:0.00}: darkness can never be entered. " +
                    "PlayerComposure must pass the player transform to LevelAt.",
                    carried, effectiveFloor, threshold));
            else if (globals > threshold)
                sb.AppendLine(string.Format(ci,
                    "  FAIL - the GLOBAL light alone ({0:0.00}) exceeds the threshold ({1:0.00}). " +
                    "Darkness is unreachable anywhere on the map.", globals, threshold));
            else
                sb.AppendLine(string.Format(ci,
                    "  OK - darkness is reachable: a spot with under {0:0.00} of local light reads as dark.",
                    threshold - globals));

            return sb.ToString();
        }
    }
}
