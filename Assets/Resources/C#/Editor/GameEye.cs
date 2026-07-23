// GameEye — Claude's programmatic eyes on the game (2026-07-23).
// Renders the game through a real URP camera to a PNG on demand, from any
// position, in EDIT mode (no Play required). Called by the unity-mcp bridge
// via execute_code, e.g.:
//   TimeKiller.EditorTools.GameEye.Photograph(32f, -6f, 5.5f, 1280, 720, "C:/tmp/kitchen.png")
// Copies the Main Camera (URP 2D renderer, ortho) so lighting/post match the
// player's actual view. Pure tooling: never touches the scene permanently.
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace TimeKiller.EditorTools
{
    public static class GameEye
    {
        public static string Photograph(float x, float y, float orthoSize, int width, int height, string path)
        {
            var main = Camera.main;
            if (main == null) return "ERROR: no Main Camera";

            var go = new GameObject("~GameEye") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);                       // URP renderer index, ortho, culling, bg
                cam.transform.position = new Vector3(x, y, main.transform.position.z);
                cam.orthographicSize = orthoSize;

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (!RenderPipeline.SupportsRenderRequest(cam, request))
                    return "ERROR: pipeline does not support StandardRequest";
                RenderPipeline.SubmitRenderRequest(cam, request);

                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                return $"OK: {path} ({width}x{height} at {x},{y} ortho {orthoSize})";
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Player's-eye photo: current Main Camera position and zoom, exactly
        // what the player sees right now (works in Play Mode too).
        public static string PhotographPlayerView(string path)
        {
            var main = Camera.main;
            if (main == null) return "ERROR: no Main Camera";
            var p = main.transform.position;
            return Photograph(p.x, p.y, main.orthographicSize, 1280, 720, path);
        }
    }
}
