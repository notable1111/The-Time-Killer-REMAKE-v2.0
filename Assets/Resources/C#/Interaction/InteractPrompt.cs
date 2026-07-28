// "Press E" — the key cap and a one-word label, shown only when E would
// actually do something.
//
// It asks the interaction systems what E would do RIGHT NOW (ClockRepair
// .NearbyClock / PlayerHiding.NearbySpot) rather than re-deriving ranges of its
// own. A prompt that computes its own answer eventually disagrees with the input
// it is advertising, and then it is lying to the player at the worst moment.
//
// Both references are optional: with neither feature present this shows nothing
// and costs nothing. Removable — delete the object and the game plays the same,
// silently.
//
// KNOWN, and deliberately reflected rather than hidden: E is read by ClockRepair
// AND PlayerHiding in the same frame, so a clock placed within interact range of
// a wardrobe will trigger both. The prompt shows the clock in that case because
// the objective is what the player meant — but the underlying collision is a
// design bug in the placement, not something this component can fix.
using TimeKiller.Hiding;
using TimeKiller.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Interaction
{
    public class InteractPrompt : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Text label;

        ClockRepair repair;
        PlayerHiding hiding;
        string shown;

        void Start()
        {
            repair = Object.FindAnyObjectByType<ClockRepair>();
            hiding = Object.FindAnyObjectByType<PlayerHiding>();
            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            string want = Wanted();
            bool show = want != null;
            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (show && want != shown && label != null)
            {
                shown = want;
                label.text = want;
            }
        }

        /// What E does at this instant, or null for "nothing".
        string Wanted()
        {
            // Inside a wardrobe, E is the way out — and it is the only thing E
            // does, so it outranks everything else.
            if (hiding != null && hiding.IsHidden) return "GET OUT";

            // Mid-repair the gauge already owns the bottom of the screen and says
            // what to press; a second prompt there would just be clutter.
            if (repair != null && repair.Repairing) return null;

            if (repair != null && repair.NearbyClock != null) return "REPAIR";
            if (hiding != null && hiding.NearbySpot != null) return "HIDE";
            return null;
        }
    }
}
