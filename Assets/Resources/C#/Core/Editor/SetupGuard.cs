// One line of protection for every TimeKiller/Setup menu item.
//
// Editor setup scripts are written against edit-mode assumptions, and Unity does
// not stop you running them in play mode — it lets them get part-way and then
// throws on the first editor-only call. Measured 2026-08-03: Setup/43 ran in play
// mode, created and re-anchored its whole rig, and threw at `MarkSceneDirty` with
// "This cannot be used during play mode." Everything it did was then discarded when
// play stopped, so it *looked* like nothing happened while reporting an exception.
//
// The half-run is the dangerous part, not the exception. A script that creates
// objects, rewires references and then dies half way leaves the play-mode scene in
// a state nobody designed, and if the script had saved anything before throwing it
// would have persisted that.
//
// This is deliberately a plain static check rather than a MenuItem validate
// function: a validate function greys the item out with no explanation, and the
// person clicking it deserves to be told why.
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class SetupGuard
    {
        /// <summary>
        /// Returns true (and explains itself) when an editor setup script must not run.
        /// Call as the first line of any TimeKiller/Setup menu item:
        /// <code>if (SetupGuard.Blocked("43 - Build Pause Menu")) return;</code>
        /// </summary>
        public static bool Blocked(string label)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning($"[TimeKiller Setup] {label} - refusing to run in PLAY MODE. " +
                                 "Editor setup scripts half-complete there: they build and rewire, then throw " +
                                 "on the first editor-only call, and everything they did is discarded when play " +
                                 "stops. Exit play mode and run it again.");
                return true;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning($"[TimeKiller Setup] {label} - refusing to run while scripts are COMPILING. " +
                                 "The domain is about to reload, which drops statics and can abandon the run " +
                                 "part-way. Wait for the spinner to clear.");
                return true;
            }

            return false;
        }
    }
}
