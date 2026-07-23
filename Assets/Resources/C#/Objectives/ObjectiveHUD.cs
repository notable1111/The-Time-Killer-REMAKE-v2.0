// Player-facing HUD (IMGUI v1 — swap to UGUI later): the clocks-fixed counter
// and the repair skill-check bar while repairing. The end-of-run screen is
// Core's RunEndScreen, not this. Reads ObjectiveManager + ClockRepair. Removable.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Objectives
{
    public class ObjectiveHUD : MonoBehaviour
    {
        [SerializeField] ClockRepair repair;

        void OnGUI()
        {
            // The run is over — get out of the end screen's way.
            var flow = GameFlow.Instance;
            if (flow != null && flow.Phase != RunPhase.Playing) return;

            var mgr = ObjectiveManager.Instance;
            if (mgr != null)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    alignment = TextAnchor.UpperCenter,
                    fontStyle = FontStyle.Bold,
                };
                style.normal.textColor = mgr.AllFixed ? new Color(0.5f, 1f, 0.5f) : Color.white;
                string txt = mgr.AllFixed
                    ? $"CLOCKS {mgr.FixedCount}/{mgr.Total} — RUN TO THE EXIT"
                    : $"CLOCKS  {mgr.FixedCount} / {mgr.Total}";
                GUI.Label(new Rect(0, 12, Screen.width, 34), txt, style);
            }

            if (repair != null && repair.Repairing && repair.Active != null)
                DrawRepairBar(repair);
        }

        void DrawRepairBar(ClockRepair r)
        {
            float w = 360f, h = 26f;
            float x = (Screen.width - w) / 2f, y = Screen.height - 120f;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            // clock's overall progress (dim fill behind)
            GUI.color = new Color(0.25f, 0.6f, 0.3f, 0.5f);
            GUI.DrawTexture(new Rect(x, y, w * r.Active.Progress, h), Texture2D.whiteTexture);

            // target zone
            float zc = r.ZoneCenter, zw = r.ZoneWidth;
            GUI.color = new Color(0.4f, 1f, 0.4f, 0.9f);
            GUI.DrawTexture(new Rect(x + (zc - zw * 0.5f) * w, y, zw * w, h), Texture2D.whiteTexture);

            // sweeping marker
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x + r.Marker * w - 1.5f, y - 4, 3, h + 8), Texture2D.whiteTexture);

            var lab = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            lab.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + h + 2, w, 20), "SPACE when the marker hits green", lab);
        }
    }
}
