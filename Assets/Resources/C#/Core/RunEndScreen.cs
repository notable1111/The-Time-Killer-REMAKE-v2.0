// The win/lose screen. Watches GameFlow, fades a black sheet in on UNSCALED
// time (the game itself is frozen), then states how the run ended, how long it
// lasted, and how to start another. Built by Setup/29. Removable — without it
// the run still ends, you just don't see it.
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Core
{
    public class RunEndScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Text headline;
        [SerializeField] Text detail;
        [SerializeField] Text prompt;
        [SerializeField] float fadeSeconds = 1.1f;
        [SerializeField] Color wonColor = new Color(0.62f, 0.95f, 0.66f);
        [SerializeField] Color lostColor = new Color(0.85f, 0.25f, 0.25f);

        bool filled;

        void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        void Update()
        {
            var flow = GameFlow.Instance;
            if (flow == null || flow.Phase == RunPhase.Playing) return;

            if (!filled) Fill(flow);
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeSeconds));
        }

        void Fill(GameFlow flow)
        {
            filled = true;
            bool won = flow.Phase == RunPhase.Won;

            if (headline != null)
            {
                headline.text = flow.Headline;
                headline.color = won ? wonColor : lostColor;
            }

            if (detail != null)
            {
                string summary = flow.Summary;
                detail.text = string.IsNullOrEmpty(summary)
                    ? $"survived {GameFlow.FormatTime(flow.RunSeconds)}"
                    : $"survived {GameFlow.FormatTime(flow.RunSeconds)}   ·   {summary}";
            }

            if (prompt != null)
                prompt.text = "R — run it again        ESC — quit";
        }
    }
}
