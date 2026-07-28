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

        // OnGUI runs at least twice a frame (Layout + Repaint) and again for every
        // input event, so ANYTHING allocated in here is allocated several times per
        // frame. Styles and the counter string are therefore built once and reused;
        // they can only be created inside OnGUI because GUI.skin is null outside it.
        GUIStyle counterStyle;
        GUIStyle repairLabelStyle;
        string counterText;
        int counterFixed = -1, counterTotal = -1;
        bool counterAllFixed;

        static readonly Color CounterDone = new Color(0.5f, 1f, 0.5f);

        void EnsureStyles()
        {
            if (counterStyle != null) return;
            counterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
            };
            repairLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
            };
            repairLabelStyle.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            // The run is over — get out of the end screen's way.
            var flow = GameFlow.Instance;
            if (flow != null && flow.Phase != RunPhase.Playing) return;

            EnsureStyles();

            var mgr = ObjectiveManager.Instance;
            if (mgr != null)
            {
                // Rebuild the string only when the count actually changes — a few
                // times a run, instead of a few times a frame.
                if (mgr.FixedCount != counterFixed || mgr.Total != counterTotal || mgr.AllFixed != counterAllFixed)
                {
                    counterFixed = mgr.FixedCount;
                    counterTotal = mgr.Total;
                    counterAllFixed = mgr.AllFixed;
                    counterText = counterAllFixed
                        ? $"CLOCKS {counterFixed}/{counterTotal} — RUN TO THE EXIT"
                        : $"CLOCKS  {counterFixed} / {counterTotal}";
                }
                counterStyle.normal.textColor = counterAllFixed ? CounterDone : Color.white;
                GUI.Label(new Rect(0, 12, Screen.width, 34), counterText, counterStyle);
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

            GUI.Label(new Rect(x, y + h + 2, w, 20), "SPACE when the marker hits green", repairLabelStyle);
        }
    }
}
