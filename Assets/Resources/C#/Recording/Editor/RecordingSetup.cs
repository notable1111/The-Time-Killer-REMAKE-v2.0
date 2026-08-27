// Menu: TimeKiller/Setup/46 - Session Recording.
//
// Puts SessionRecorder on the player and SessionAudioCapture on the AudioListener
// (it must live there — that component is handed the final mix only because it
// sits on the listener). Idempotent and non-destructive like every Setup script
// here: find-or-create, re-wire in place, never destroy and rebuild.
using TimeKiller.Player;
using TimeKiller.Recording;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.RecordingTools
{
    public static class RecordingSetup
    {
        [MenuItem("TimeKiller/Setup/46 - Session Recording")]
        public static void Run()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("46 - Session Recording")) return;

            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[RecordingSetup] In play mode — setup scripts half-complete there. Stop play first.");
                return;
            }

            var report = new System.Text.StringBuilder("[RecordingSetup] ");

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) { Debug.LogError("[RecordingSetup] No PlayerController in the open scene."); return; }

            var recorder = player.GetComponent<SessionRecorder>();
            if (recorder == null)
            {
                recorder = player.gameObject.AddComponent<SessionRecorder>();
                report.Append("added SessionRecorder; ");
            }
            else report.Append("SessionRecorder already present; ");
            EditorUtility.SetDirty(recorder);

            // The audio capture has exactly one valid home: OnAudioFilterRead is
            // only handed the final mix on the object carrying the AudioListener.
            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                report.Append("NO AudioListener — audio will not be captured; ");
            }
            else
            {
                var capture = listener.GetComponent<SessionAudioCapture>();
                if (capture == null)
                {
                    capture = listener.gameObject.AddComponent<SessionAudioCapture>();
                    report.Append("added SessionAudioCapture to '" + listener.gameObject.name + "'; ");
                }
                else report.Append("SessionAudioCapture already present; ");
                EditorUtility.SetDirty(capture);
            }

            report.Append("press F8 in play mode to start/stop, F9/F10/F11 to mark.");
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log(report.ToString());
        }
    }
}
