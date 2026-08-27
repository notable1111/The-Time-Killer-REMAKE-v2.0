// Menu: TimeKiller/Verify/Feature Install Audit.
//
// WHY THIS EXISTS. Twice in one day a feature turned out to be written,
// committed, described in ARCHITECTURE as shipped — and never actually present
// in the running game. ClockEffects was a scene object placed by a Setup script
// nobody had run, so no scene contained it, its recipes did not exist, and its
// art sat imported and unreferenced for three weeks. Both times it was found by
// accident. The project's own rule says that when you would eyeball the same
// question twice, build the measurement instead, so here it is.
//
// THE QUESTION IT ANSWERS: "for every gameplay component we have written, is
// there any path by which it ends up in a running game?" There are only three
// honest answers, and this reports which one applies:
//
//   IN SCENE   — a scene or prefab references the script by GUID.
//   SELF-INSTALL — the type creates itself (a RuntimeInitializeOnLoadMethod) or
//                  some other script does AddComponent<It>() at runtime.
//   DEAD       — neither. It compiles, it is committed, and nothing can ever
//                construct it. This is the state ClockEffects was in.
//
// It is READ-ONLY: it opens nothing, loads no scene, and dirties nothing. Scenes
// and prefabs are read as TEXT and searched for the script's GUID, which is
// exactly the check that found ClockEffects by hand — and it means the audit
// cannot itself disturb a hand-tuned scene, which matters while three sessions
// share one Editor.
//
// ⚠️ IT REPORTS, IT DOES NOT JUDGE. "DEAD" is a strong claim and the heuristics
// below can be fooled (a component added by a prefab variant, or constructed
// through reflection). Every verdict is printed with the evidence that produced
// it so a human can overrule it. Treat a DEAD line as "go and look at this one",
// not as "delete it".
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class FeatureInstallAudit
    {
        // Namespaces whose absence from a scene is not a defect:
        //   Testing  — the bot harness and probes attach themselves on demand and
        //              are deliberately absent from the shipped scenes.
        //   Tests    — EditMode fixtures; they are not components at all.
        static readonly string[] ExemptNamespaces = { "TimeKiller.Testing", "TimeKiller.Tests" };

        [MenuItem("TimeKiller/Verify/Feature Install Audit")]
        public static void Run()
        {
            if (SetupGuard.Blocked("Verify - Feature Install Audit")) return;

            var sceneText = ReadAll("*.unity");
            var prefabText = ReadAll("*.prefab");
            var sourceText = string.Join("\n", Directory
                .GetFiles("Assets/Resources/C#", "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

            // Which gameplay levels each component appears in, for the gap
            // report below. MainMenu and SampleScene are excluded: one is a menu
            // and one is the URP template, so neither owes the game a maniac.
            var perScene = new Dictionary<string, List<string>>();

            var inScene = new List<string>();
            var selfInstall = new List<string>();
            var dead = new List<string>();
            var noScript = new List<string>();

            foreach (var type in GameplayComponents())
            {
                string guid = GuidOf(type);
                if (guid == null) { noScript.Add($"{type.FullName} — no MonoScript asset found"); continue; }

                string needle = "guid: " + guid;
                var scenes = sceneText.Where(kv => kv.Value.Contains(needle)).Select(kv => kv.Key).ToList();
                var prefabs = prefabText.Where(kv => kv.Value.Contains(needle)).Select(kv => kv.Key).ToList();

                if (scenes.Count > 0 || prefabs.Count > 0)
                {
                    string where = string.Join(", ", scenes.Concat(prefabs));
                    inScene.Add($"{type.Name}  <- {where}");
                    perScene[type.Name] = scenes;
                    continue;
                }

                string how = InstallEvidence(type, sourceText);
                if (how != null) selfInstall.Add($"{type.Name}  <- {how}");
                else dead.Add(type.FullName);
            }

            var r = new System.Text.StringBuilder();
            r.AppendLine("=== TimeKiller Feature Install Audit ===");
            r.AppendLine($"scanned {sceneText.Count} scenes, {prefabText.Count} prefabs");
            r.AppendLine();

            if (dead.Count == 0)
            {
                r.AppendLine("NOTHING DEAD. Every gameplay component is either placed in a scene/prefab");
                r.AppendLine("or installs itself at runtime.");
            }
            else
            {
                r.AppendLine($"** {dead.Count} COMPONENT(S) WITH NO PATH INTO THE GAME **");
                r.AppendLine("   Written, compiled, committed - and nothing can construct them.");
                r.AppendLine("   This is the state ClockEffects was in for three weeks.");
                foreach (var d in dead) r.AppendLine("   - " + d);
            }

            if (noScript.Count > 0)
            {
                r.AppendLine();
                r.AppendLine("COULD NOT CHECK (no MonoScript asset - inner class, or renamed file):");
                foreach (var n in noScript) r.AppendLine("   - " + n);
            }

            // ---- the gap between the two playable levels ----
            // A component can be perfectly alive in CastleWing and absent from
            // Catacombs, and every check above still passes. That is not a dead
            // feature, it is a LEVEL missing one - and it stays invisible unless
            // something diffs the two, which is why this section exists.
            const string A = "CastleWingLDtk.unity", B = "Catacombs.unity";
            var onlyA = perScene.Where(kv => kv.Value.Contains(A) && !kv.Value.Contains(B))
                                .Select(kv => kv.Key).OrderBy(x => x).ToList();
            var onlyB = perScene.Where(kv => kv.Value.Contains(B) && !kv.Value.Contains(A))
                                .Select(kv => kv.Key).OrderBy(x => x).ToList();

            r.AppendLine();
            r.AppendLine("--- LEVEL GAP: present in one playable level, absent from the other ---");
            r.AppendLine("    Not automatically a bug. Some are level-specific by design (a hand-placed");
            r.AppendLine("    MusicZone, a castle-only prop light). Others are whole systems the second");
            r.AppendLine("    level never received. Read it, do not obey it.");
            r.AppendLine($"  in {A} only ({onlyA.Count}):");
            foreach (var n in onlyA) r.AppendLine("     - " + n);
            r.AppendLine($"  in {B} only ({onlyB.Count}):");
            if (onlyB.Count == 0) r.AppendLine("     (none)");
            foreach (var n in onlyB) r.AppendLine("     - " + n);

            r.AppendLine();
            r.AppendLine($"self-installing ({selfInstall.Count}):");
            foreach (var s in selfInstall.OrderBy(x => x)) r.AppendLine("   " + s);
            r.AppendLine();
            r.AppendLine($"placed in a scene or prefab ({inScene.Count}):");
            foreach (var s in inScene.OrderBy(x => x)) r.AppendLine("   " + s);

            // A report file as well as the console: read_console is unreliable
            // through the MCP bridge, and a long report is unreadable there anyway.
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/feature_install_audit.txt", r.ToString());
            Debug.Log(r.ToString() + "\n(also written to Temp/feature_install_audit.txt)");
        }

        /// Every concrete MonoBehaviour the GAME could use — the population whose
        /// absence from every scene is a real question.
        static IEnumerable<Type> GameplayComponents()
        {
            var asm = typeof(TimeKiller.Core.EventBus).Assembly;   // Assembly-CSharp
            return asm.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("TimeKiller"))
                .Where(t => !ExemptNamespaces.Any(n => t.Namespace.StartsWith(n)))
                .OrderBy(t => t.FullName);
        }

        /// The script's GUID, which is how a scene actually refers to it.
        static string GuidOf(Type type)
        {
            foreach (var g in AssetDatabase.FindAssets("t:MonoScript " + type.Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == type) return g;
            }
            return null;
        }

        /// Is there a runtime path that creates this without a scene? Two are
        /// legitimate in this codebase and both are used deliberately:
        /// a RuntimeInitializeOnLoadMethod that builds its own host object
        /// (EffectPlayer, ManiacThreatEffects, ClockEffects), and another
        /// component doing AddComponent&lt;It&gt;() in Awake (ManiacNavigator,
        /// ManiacEscalation, ManiacBloodTracker, PlayerNavDebug).
        static string InstallEvidence(Type type, string sourceText)
        {
            bool boots = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                             .Any(m => m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() != null);
            if (boots) return "RuntimeInitializeOnLoadMethod (installs itself)";

            if (sourceText.Contains("AddComponent<" + type.Name + ">"))
                return "AddComponent<" + type.Name + "> in code";

            // RequireComponent means Unity adds it whenever its host is added, so
            // it is live wherever the host is. Reported rather than treated as a
            // pass on its own, because the host may itself be dead.
            var requiredBy = GameplayComponents()
                .Where(t => t.GetCustomAttributes<RequireComponent>()
                             .Any(rc => rc.m_Type0 == type || rc.m_Type1 == type || rc.m_Type2 == type))
                .Select(t => t.Name).ToList();
            if (requiredBy.Count > 0)
                return "RequireComponent on " + string.Join("/", requiredBy);

            return null;
        }

        static Dictionary<string, string> ReadAll(string pattern)
        {
            var map = new Dictionary<string, string>();
            foreach (var p in Directory.GetFiles("Assets", pattern, SearchOption.AllDirectories))
            {
                // Vendor demo content is not our game and would only add noise.
                if (p.Replace('\\', '/').Contains("/Outsource/") || p.Replace('\\', '/').Contains("/_Recovery/")) continue;
                map[Path.GetFileName(p)] = File.ReadAllText(p);
            }
            return map;
        }
    }
}
