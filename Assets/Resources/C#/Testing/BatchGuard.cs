// Keeps a batch alive across the one thing that reliably kills it: Unity
// recompiling scripts while the batch is running.
//
// WHAT HAPPENED ON 2026-07-25 (reconstructed from Editor.log, not guessed):
//   00:30       BatchRunner.cs was edited and saved to disk.
//   00:31:11    The batch launched — from the STALE already-loaded assembly,
//               because Unity only auto-refreshes when the editor regains focus.
//   runs 1-10   Clean. Zero exceptions in the whole block.
//   ~00:36      An AssetDatabase refresh fired, noticed the pending change, and
//               requested script compilation:
//                 "[ScriptCompilation] Requested script compilation because:
//                  AssetDatabase observed changes in script compilation related files"
//               Unity then did a SYNCHRONOUS DOMAIN RELOAD in Play Mode.
//   run 11      Never happened. The reload killed BatchRunner's coroutine and
//               reset its static Instance. No row was ever written and nothing
//               said so — the JSONL just stops at seed 1009.
//   after       Every non-serializable runtime field on the surviving scene
//               objects came back null (PlayerController.Input is an interface
//               field; BotPilot.memory is a plain C# object; Awake() does not
//               re-run on an object that already exists). Result: 498,704
//               NullReferenceExceptions and a 190 MB Editor.log, until Play Mode
//               was stopped by hand.
//
// So three separate defences, because they fail in different ways:
//   1. PREVENT — hold LockReloadAssemblies + DisallowAutoRefresh for the batch,
//      so the reload is deferred instead of executed. This is the actual fix.
//   2. RECORD — if a reload happens anyway, write an explicit aborted row before
//      the domain dies. A batch that stops must say WHY in its own results file,
//      not leave the next reader doing archaeology in a 190 MB log.
//   3. CONTAIN — after such a reload, stop Play Mode immediately. The scene is
//      unrecoverable at that point (null fields, no Awake), so every extra frame
//      is pure exception spam.
//
// Lives in the RUNTIME folder, not Editor/, and is entirely #if UNITY_EDITOR.
// It has to: BatchRunner is runtime code, and runtime assemblies cannot
// reference the Editor assembly in either direction that would help here.
// Gated like this it compiles to nothing in a build, same as the rest of
// C#/Testing — delete the folder and the game is untouched.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Testing
{
    [InitializeOnLoad]
    public static class BatchGuard
    {
        // Written by BatchRunner after every run. Survives a domain reload,
        // which is the entire point — it is the black box we read from the wreck.
        public const string LiveKey = "TimeKiller.BotPlaytest.Live";
        const string LockedKey = "TimeKiller.BotPlaytest.Locked";

        static BatchGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // ---- 1. prevent ------------------------------------------------------

        /// Called by BatchRunner when a batch starts. Unity defers any compile
        /// request until Release() — the batch finishes on the assembly it began
        /// on, which is the only way its coroutine can survive to the last run.
        public static void Hold()
        {
            if (SessionState.GetBool(LockedKey, false)) return;
            SessionState.SetBool(LockedKey, true);
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
        }

        /// Symmetrical release. Leaking this lock leaves the editor unable to
        /// compile anything, so it is also called on every play-mode exit and is
        /// safe to call when no lock is held.
        public static void Release()
        {
            if (!SessionState.GetBool(LockedKey, false)) return;
            SessionState.SetBool(LockedKey, false);
            EditorApplication.UnlockReloadAssemblies();
            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.Refresh();   // pick up whatever was deferred
        }

        // The escape hatch. If a crash ever leaves the lock held, the editor
        // silently stops compiling and that is a maddening thing to diagnose.
        [MenuItem("TimeKiller/Setup/34 - Unlock assemblies (batch escape hatch)")]
        static void ForceUnlock()
        {
            SessionState.SetBool(LockedKey, false);
            SessionState.EraseString(LiveKey);
            // Unlock is reference-counted; drain it rather than guess the depth.
            for (int i = 0; i < 8; i++) EditorApplication.UnlockReloadAssemblies();
            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.Refresh();
            Debug.Log("[BatchGuard] Assembly reload force-unlocked.");
        }

        // ---- 2. record -------------------------------------------------------

        static void OnBeforeReload()
        {
            var live = Live.Read();
            if (live == null) return;

            // The last thing this managed domain will ever do. After this the
            // coroutine, the statics and the scene wiring are all gone.
            live.WriteAbortRow("domain_reload",
                "Unity recompiled scripts mid-batch; the runner coroutine died with the domain. " +
                "Runs after this point were never attempted.");
            Debug.LogError($"[BatchGuard] BATCH ABORTED by a domain reload at run {live.Completed + 1}/{live.Total}. " +
                           $"Recorded in {Path.GetFileName(live.OutputPath)}.");
        }

        // ---- 3. contain ------------------------------------------------------

        static void OnAfterReload()
        {
            var live = Live.Read();
            if (live == null) return;
            SessionState.EraseString(LiveKey);
            if (!EditorApplication.isPlaying) return;

            // Scene objects survived the reload but their runtime-assigned
            // fields did not, and Awake() will not re-run to restore them.
            // Anything still ticking throws every frame — that is where the
            // 190 MB log came from. Get out now.
            Debug.LogError("[BatchGuard] Stopping Play Mode: the scene did not survive the reload " +
                           "(runtime references are null and Awake() will not re-run).");
            EditorApplication.isPlaying = false;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode ||
                change == PlayModeStateChange.ExitingPlayMode)
            {
                Release();
                SessionState.EraseString(LiveKey);
            }
        }

        // ---- the black box ---------------------------------------------------

        /// BatchRunner's live position, flattened so it survives a domain reload.
        /// Format: outputPath|completed|total|seed|profile|speed
        public class Live
        {
            public string OutputPath, Profile;
            public int Completed, Total, Seed;
            public float Speed;

            public static Live Read()
            {
                string s = SessionState.GetString(LiveKey, "");
                if (string.IsNullOrEmpty(s)) return null;
                var p = s.Split('|');
                if (p.Length < 6) return null;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                return new Live
                {
                    OutputPath = p[0],
                    Completed = int.Parse(p[1]),
                    Total = int.Parse(p[2]),
                    Seed = int.Parse(p[3]),
                    Profile = p[4],
                    Speed = float.Parse(p[5], ci),
                };
            }

            public void WriteAbortRow(string reason, string detail)
            {
                if (string.IsNullOrEmpty(OutputPath) || !File.Exists(OutputPath)) return;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                string row = "{\"aborted\":true"
                           + ",\"reason\":\"" + reason + "\""
                           + ",\"detail\":\"" + detail.Replace("\"", "'") + "\""
                           + ",\"atRun\":" + (Completed + 1)
                           + ",\"plannedTotal\":" + Total
                           + ",\"seed\":" + Seed
                           + ",\"profile\":\"" + Profile + "\""
                           + ",\"speed\":" + Speed.ToString("0.##", ci)
                           + "}";
                try { File.AppendAllText(OutputPath, row + "\n"); }
                catch (IOException e) { Debug.LogError("[BatchGuard] Could not write abort row: " + e.Message); }
            }
        }
    }
}
#endif
