// Player-facing HUD: the clocks-fixed counter and the repair skill-check gauge.
// The end-of-run screen is Core's RunEndScreen, not this.
//
// uGUI now, not IMGUI (2026-07-28). The old version drew everything with
// GUI.DrawTexture in OnGUI — fine as a prototype, but it could not use the
// "carved stone and brass" art set, and OnGUI runs several times per frame so
// every string and style had to be hand-cached to avoid churning garbage. This
// version owns no drawing at all: it moves RectTransforms and sets text on
// objects that Setup/38 builds, so the art is swappable without touching code.
//
// The gauge's inner channel is expressed as ANCHORS on a Channel rect, so the
// fill, the hit zone and the needle track the frame automatically at any screen
// size. The channel rectangle came from measuring Gauge.png — see
// Tools/UIArt/README.md, which is the only place that number is written down.
//
// Removable: delete the ObjectiveHUD object and the game runs unchanged.
using TimeKiller.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Objectives
{
    public class ObjectiveHUD : MonoBehaviour
    {
        [SerializeField] ClockRepair repair;

        [Header("Counter (built by Setup/38)")]
        [SerializeField] GameObject counterRoot;
        [SerializeField] TMP_Text counterText;

        [Header("Skill-check gauge (built by Setup/38)")]
        [SerializeField] GameObject gaugeRoot;
        [SerializeField] Image progressFill;
        [SerializeField] RectTransform fillEdge;
        [SerializeField] RectTransform zone;
        [SerializeField] RectTransform needle;

        int shownFixed = -1, shownTotal = -1;
        bool shownAllFixed;

        static readonly Color CounterDone = new Color(0.62f, 1f, 0.60f);
        static readonly Color CounterNormal = new Color(0.90f, 0.88f, 0.82f);

        void Update()
        {
            // The run is over — get out of the end screen's way.
            var flow = GameFlow.Instance;
            bool playing = flow == null || flow.Phase == RunPhase.Playing;

            if (counterRoot != null) counterRoot.SetActive(playing);
            UpdateCounter(playing);
            UpdateGauge(playing);
        }

        void UpdateCounter(bool playing)
        {
            if (!playing || counterText == null) return;
            var mgr = ObjectiveManager.Instance;
            if (mgr == null) return;

            // Only touch the Text when the count actually changes: assigning
            // Text.text rebuilds the mesh even when the string is identical.
            if (mgr.FixedCount == shownFixed && mgr.Total == shownTotal && mgr.AllFixed == shownAllFixed) return;
            shownFixed = mgr.FixedCount;
            shownTotal = mgr.Total;
            shownAllFixed = mgr.AllFixed;

            counterText.text = shownAllFixed ? "RUN" : $"{shownFixed} / {shownTotal}";
            counterText.color = shownAllFixed ? CounterDone : CounterNormal;
        }

        void UpdateGauge(bool playing)
        {
            bool active = playing && repair != null && repair.Repairing && repair.Active != null;
            if (gaugeRoot != null && gaugeRoot.activeSelf != active) gaugeRoot.SetActive(active);
            if (!active) return;

            float progress = repair.Active.Progress;
            if (progressFill != null) progressFill.fillAmount = progress;

            // The hot line rides the fill's boundary. A Filled image cannot do
            // this itself — its gradient is fixed to the texture, not to the cut,
            // so the brightest part would sit still while the fill moved past it.
            if (fillEdge != null)
            {
                fillEdge.anchorMin = new Vector2(progress, 0f);
                fillEdge.anchorMax = new Vector2(progress, 1f);
                fillEdge.anchoredPosition = Vector2.zero;
                // Nothing to lead when the bar is empty or already full.
                bool show = progress > 0.001f && progress < 0.999f;
                if (fillEdge.gameObject.activeSelf != show) fillEdge.gameObject.SetActive(show);
            }

            // Zone and needle live in normalised channel space, so the maths is
            // the same one the old bar used and stays correct at any resolution.
            if (zone != null)
            {
                float half = repair.ZoneWidth * 0.5f;
                zone.anchorMin = new Vector2(Mathf.Clamp01(repair.ZoneCenter - half), 0f);
                zone.anchorMax = new Vector2(Mathf.Clamp01(repair.ZoneCenter + half), 1f);
                zone.offsetMin = Vector2.zero;
                zone.offsetMax = Vector2.zero;
            }

            if (needle != null)
            {
                float t = Mathf.Clamp01(repair.Marker);
                needle.anchorMin = new Vector2(t, 0.5f);
                needle.anchorMax = new Vector2(t, 0.5f);
                needle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
