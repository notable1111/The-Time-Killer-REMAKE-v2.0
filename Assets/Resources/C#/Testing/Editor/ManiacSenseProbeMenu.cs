// Menu: TimeKiller/Verify/Probe Maniac Senses.
// Enters play mode if needed, then spawns the probe once the scene is live.
// Results go to the console AND to Temp/maniac_sense_probe.txt.
using TimeKiller.Testing;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class ManiacSenseProbeMenu
    {
        [MenuItem("TimeKiller/Verify/Probe Maniac Senses")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                ManiacSenseProbe.Spawn();
                return;
            }

            // Same hand-off as AnimationProbe: entering play mode reloads the
            // domain and drops subscriptions, so the request travels in
            // SessionState and is picked up on the other side.
            SessionState.SetBool(ManiacSenseProbe.PendingKey, true);
            EditorApplication.isPlaying = true;
            Debug.Log("[ManiacSenseProbe] Entering play mode — probe starts automatically. "
                      + "Report: Temp/maniac_sense_probe.txt");
        }
    }
}
