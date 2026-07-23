// SmokeCheck — scene integrity report before a push, runnable two ways:
//   menu  TimeKiller/Test/Smoke Check (humans, logs to console)
//   TimeKiller.EditorTools.SmokeCheck.Report()  (Claude via unity-mcp execute_code)
// Checks the CURRENTLY OPEN scene. Pure read-only: changes nothing.
// (Written as a plain report instead of Unity Test Framework because the
// project uses no asmdefs — UTF test assemblies can't reference Assembly-CSharp.)
using System.Collections.Generic;
using System.Text;
using TimeKiller.Hiding;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class SmokeCheck
    {
        [MenuItem("TimeKiller/Test/Smoke Check (scene integrity)")]
        public static void RunFromMenu() => Debug.Log("[SmokeCheck]\n" + Report());

        public static string Report()
        {
            var problems = new List<string>();
            var info = new List<string>();

            // --- Player rig ---
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) problems.Add("no PlayerController in scene");
            else
            {
                if (player.GetComponent<KeyboardInputSource>() == null) problems.Add("Player has no KeyboardInputSource");
                if (player.GetComponent<PlayerHealth>() == null) problems.Add("Player has no PlayerHealth");
                var hiding = player.GetComponent<PlayerHiding>();
                if (hiding == null) problems.Add("Player has no PlayerHiding");
                else if (new SerializedObject(hiding).FindProperty("config").objectReferenceValue == null)
                    problems.Add("PlayerHiding.config not assigned");
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                info.Add($"\"playerPos\":[{player.transform.position.x.ToString("0.0", ci)},{player.transform.position.y.ToString("0.0", ci)}]");
            }

            // --- Maniac ---
            var maniac = Object.FindAnyObjectByType<ManiacController>();
            if (maniac == null) problems.Add("no ManiacController in scene");

            // --- Hiding spots ---
            var spots = Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
            if (spots.Length < 7) problems.Add($"expected >=7 HidingSpots, found {spots.Length}");
            foreach (var s in spots)
            {
                var so = new SerializedObject(s);
                if (so.FindProperty("closedSprite").objectReferenceValue == null ||
                    so.FindProperty("ajarSprite").objectReferenceValue == null)
                    problems.Add($"HidingSpot '{s.name}' missing sprites");
            }
            info.Add($"\"hidingSpots\":{spots.Length}");

            // --- Furniture ---
            var furniture = GameObject.Find("Furniture");
            if (furniture == null) problems.Add("no Furniture root in scene");
            else info.Add($"\"furnitureProps\":{furniture.transform.childCount}");

            // --- Missing sprites anywhere ---
            int missingSprites = 0;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (sr.sprite == null) missingSprites++;
            if (missingSprites > 0) problems.Add($"{missingSprites} SpriteRenderers with NULL sprite");

            // --- Camera rig ---
            if (Camera.main == null) problems.Add("no Main Camera");
            if (Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>() == null)
                problems.Add("no CinemachineCamera");

            var sb = new StringBuilder("{");
            sb.Append("\"ok\":").Append(problems.Count == 0 ? "true" : "false");
            sb.Append(",\"problems\":[");
            for (int i = 0; i < problems.Count; i++)
                sb.Append(i > 0 ? "," : "").Append('"').Append(problems[i].Replace("\"", "'")).Append('"');
            sb.Append(']');
            foreach (var kv in info) sb.Append(',').Append(kv);
            sb.Append('}');
            return sb.ToString();
        }
    }
}
