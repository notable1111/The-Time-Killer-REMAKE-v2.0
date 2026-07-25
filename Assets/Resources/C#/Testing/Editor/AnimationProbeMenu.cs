// Menu: TimeKiller/Verify/Probe Player Animations.
// Enters play mode if needed, then spawns the probe once the scene is live.
// Results go to the console AND to Temp/animation_probe.txt.
using TimeKiller.Testing;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class AnimationProbeMenu
    {
        [MenuItem("TimeKiller/Verify/Probe Player Animations")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                AnimationProbe.Spawn();
                return;
            }

            // Hand the request to the next domain rather than to a callback:
            // entering play mode reloads the domain and drops subscriptions.
            // AnimationProbe.SpawnIfRequested picks this up on the other side.
            SessionState.SetBool(AnimationProbe.PendingKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("[AnimationProbe] Entering play mode — probe starts automatically. "
                      + "Report: Temp/animation_probe.txt");
        }
    }
}
