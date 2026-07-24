// Menu: TimeKiller/Setup/33 - Bot Playtest.
//
// Creates the bot profile assets (once), then lets you pick profiles + a run
// count and press Go. The window cannot start the batch itself: it has to enter
// Play Mode first and the runner only exists at runtime, so the request is
// parked in SessionState and picked up by the playModeStateChanged hook below.
//
// Profiles ship in two families, deliberately never mixed in one report:
//   novice / average / expert   — must FIND the clocks by looking (the honest run)
//   oracle_*                    — knowsEverything: the "already memorised the
//                                 castle" upper bound, for comparison only
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.Testing.EditorTools
{
    public class BotPlaytestWindow : EditorWindow
    {
        const string ConfigDir = "Assets/Resources/C#/Testing/Configs";
        const string PendingKey = "TimeKiller.BotPlaytest.Pending";
        const string DefaultScene = "Assets/Scenes/CastleWingLDtk.unity";

        static readonly string[] AllProfiles =
        {
            "Bot_novice", "Bot_average", "Bot_expert", "Bot_oracle_average", "Bot_oracle_expert",
        };

        readonly HashSet<string> selected = new HashSet<string> { "Bot_novice", "Bot_average", "Bot_expert" };
        int runs = 20;
        int baseSeed = 1000;
        float speed = 4f;
        bool alsoAtRealtime = true;
        int controlRuns = 15;
        string controlProfile = "Bot_average";   // "" = spread the control across every profile
        float maxRunSeconds = 480f;

        [MenuItem("TimeKiller/Setup/33 - Bot Playtest")]
        public static void Open()
        {
            EnsureProfiles();
            GetWindow<BotPlaytestWindow>("Bot Playtest").minSize = new Vector2(360f, 380f);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Profiles", EditorStyles.boldLabel);
            foreach (var p in AllProfiles)
            {
                bool on = selected.Contains(p);
                bool oracle = p.Contains("oracle");
                bool now = EditorGUILayout.ToggleLeft(
                    oracle ? p + "   (cheats: knows the map)" : p, on);
                if (now && !on) selected.Add(p);
                else if (!now && on) selected.Remove(p);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Batch", EditorStyles.boldLabel);
            runs = EditorGUILayout.IntSlider("Runs per profile", runs, 1, 200);
            baseSeed = EditorGUILayout.IntField("Base seed", baseSeed);
            speed = EditorGUILayout.Slider("Time scale", speed, 1f, 10f);
            alsoAtRealtime = EditorGUILayout.Toggle(
                new GUIContent("Also run at 1x", "Matched-seed control block. Without it you cannot tell whether the win rate measures the game or the accelerator."), alsoAtRealtime);
            using (new EditorGUI.DisabledScope(!alsoAtRealtime || speed <= 1f))
            {
                controlRuns = EditorGUILayout.IntSlider(
                    new GUIContent("  1x control runs", "A 1x run costs real minutes, so this is the expensive number in the whole window."), controlRuns, 2, 30);
                ControlProfileField();
            }
            maxRunSeconds = EditorGUILayout.Slider("Timeout (game seconds)", maxRunSeconds, 60f, 900f);

            bool control = alsoAtRealtime && speed > 1f;
            int controlCells = string.IsNullOrEmpty(controlProfile) ? selected.Count : 1;
            int fast = selected.Count * runs;
            int slow = control ? controlCells * Mathf.Min(runs, controlRuns) : 0;
            // Measured, not guessed: a full honest run on CastleWing takes ~400
            // in-game seconds, because the bot explores until it has seen every
            // clock. Deaths come in under that and timeouts sit at the cap, so
            // this is an estimate, not a promise — but 0.5x timeout was wishful.
            float typical = Mathf.Min(400f, maxRunSeconds * 0.85f);
            float minutes = (fast * typical / Mathf.Max(1f, speed) + slow * typical) / 60f;
            EditorGUILayout.HelpBox(
                $"{fast + slow} runs ({fast} at {speed:0.#}x, {slow} at 1x)\n" +
                $"~{minutes:0} min of wall clock — Unity must stay in Play Mode.\n" +
                "Results: Tools/Playtest/results/<timestamp>.jsonl\n" +
                "Then: python Tools/Playtest/analyze.py",
                MessageType.None);
            if (control && slow * typical > fast * typical / Mathf.Max(1f, speed))
                EditorGUILayout.HelpBox(
                    "The 1x control block is now the bulk of that time. It runs at real " +
                    "speed by definition — that is the cost of knowing the accelerator is honest.",
                    MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(selected.Count == 0 || EditorApplication.isPlaying))
                if (GUILayout.Button("Run batch", GUILayout.Height(32f)))
                    Launch();

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Exit Play Mode before starting a batch.", MessageType.Warning);

            EditorGUILayout.Space();
            if (GUILayout.Button("Recreate profile assets"))
            {
                EnsureProfiles(force: true);
                EditorGUILayout.HelpBox("Profiles rewritten.", MessageType.Info);
            }
        }

        // Win rate is binary, so the resolution of a control cell is set by its run
        // count: 5 runs resolve to roughly ±20 points and cannot see the 5-point
        // gap the speed check exists to catch. Spending the same runs on ONE
        // profile buys a single answer sharp enough to act on instead of three
        // that aren't. Spreading is still offered — it is the right shape once
        // you suspect a specific profile is the one the accelerator distorts.
        void ControlProfileField()
        {
            var options = new List<string> { "(spread across all)" };
            var values = new List<string> { "" };
            foreach (var p in AllProfiles)
                if (selected.Contains(p)) { options.Add(p); values.Add(p); }

            // A profile that got deselected falls back to spreading, visibly.
            int index = Mathf.Max(0, values.IndexOf(controlProfile));
            index = EditorGUILayout.Popup("  1x control profile", index, options.ToArray());
            controlProfile = values[index];
        }

        void Launch()
        {
            // THE run-11 BUG, fixed at its source. That batch was started 40
            // seconds after BatchRunner.cs was saved, but Unity had not noticed
            // the file yet — it only auto-refreshes when the editor regains
            // focus. Five minutes in, the refresh fired, the compile it had been
            // sitting on ran, and the domain reload took the batch with it.
            //
            // So: flush the AssetDatabase and let any pending compile finish
            // BEFORE entering Play Mode. Costs a few seconds once; the
            // alternative cost an hour of runs and a 190 MB log.
            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.Log("[BotPlaytest] Waiting for script compilation to finish — press Run batch again.");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != DefaultScene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(DefaultScene);
            }

            var list = new List<string>(selected);
            list.Sort();
            // profiles|runs|seed|speed|alsoAt1x|timeout|controlRuns|controlProfile
            // Invariant on purpose: this editor's locale writes "4,5" and the
            // parser on the other side is culture-invariant.
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string control = selected.Contains(controlProfile) ? controlProfile : "";
            SessionState.SetString(PendingKey,
                $"{string.Join(",", list)}|{runs}|{baseSeed}|{speed.ToString(ci)}|" +
                $"{(alsoAtRealtime ? 1 : 0)}|{maxRunSeconds.ToString(ci)}|{controlRuns}|{control}");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            string pending = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(pending)) return;
            SessionState.EraseString(PendingKey);

            var parts = pending.Split('|');
            if (parts.Length < 7) return;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            BatchRunner.Launch(new BatchRunner.Request
            {
                Profiles = parts[0].Split(','),
                Runs = int.Parse(parts[1]),
                BaseSeed = int.Parse(parts[2]),
                Speed = float.Parse(parts[3], ci),
                AlsoAtRealtime = parts[4] == "1",
                MaxRunSeconds = float.Parse(parts[5], ci),
                RealtimeControlRuns = int.Parse(parts[6]),
                ControlProfiles = parts.Length > 7 && !string.IsNullOrEmpty(parts[7])
                    ? new[] { parts[7] } : null,
            });
        }

        // ---- the profiles ----------------------------------------------------

        static void EnsureProfiles(bool force = false)
        {
            Directory.CreateDirectory(ConfigDir);

            // reaction, jitter, aim, panic, calm, hideBias, route, sprint, cheat
            Make("Bot_novice", "novice", 0.35f, 0.35f, 0.12f, 3.0f, 9f, 0.30f, 0.40f, 0.80f, false, force);
            Make("Bot_average", "average", 0.22f, 0.30f, 0.06f, 5.0f, 11f, 0.60f, 0.75f, 0.50f, false, force);
            Make("Bot_expert", "expert", 0.12f, 0.20f, 0.02f, 7.0f, 13f, 0.50f, 1.00f, 0.35f, false, force);
            Make("Bot_oracle_average", "oracle_average", 0.22f, 0.30f, 0.06f, 5.0f, 11f, 0.60f, 0.90f, 0.50f, true, force);
            Make("Bot_oracle_expert", "oracle_expert", 0.12f, 0.20f, 0.02f, 7.0f, 13f, 0.50f, 1.00f, 0.35f, true, force);

            AssetDatabase.SaveAssets();
        }

        static void Make(string asset, string name, float reaction, float jitter, float aim,
                         float panic, float calm, float hideBias, float route, float sprint,
                         bool cheat, bool force)
        {
            string path = $"{ConfigDir}/{asset}.asset";
            var config = AssetDatabase.LoadAssetAtPath<BotProfileConfig>(path);
            if (config != null && !force) return;
            bool create = config == null;
            if (create) config = ScriptableObject.CreateInstance<BotProfileConfig>();

            config.profileName = name;
            config.reactionTime = reaction;
            config.reactionJitter = jitter;
            config.aimError = aim;
            config.panicDistance = panic;
            config.calmDistance = calm;
            config.hideBias = hideBias;
            config.routeKnowledge = route;
            config.sprintTendency = sprint;
            config.knowsEverything = cheat;
            config.sightRange = 9f;
            config.minHideSeconds = cheat ? 3.5f : 4f;

            if (create) AssetDatabase.CreateAsset(config, path);
            else EditorUtility.SetDirty(config);
        }
    }
}
