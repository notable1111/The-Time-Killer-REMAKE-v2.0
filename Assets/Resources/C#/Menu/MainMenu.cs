// The main menu. Knows nothing about the game — it loads scenes by name and
// quits. Everything visual is built by Setup/41 and lives in MainMenu.unity.
//
// The level buttons exist for a concrete reason: the Catacombs level is finished
// and audited but has, until now, only been reachable by opening its scene by
// hand in the editor. A menu is the first thing that makes the second level part
// of the game rather than part of the project.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimeKiller.Menu
{
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("Scene names, which must also be registered in Build Settings or LoadScene fails at runtime with a message players never see.")]
        [SerializeField] string castleWingScene = "CastleWingLDtk";
        [SerializeField] string catacombsScene = "Catacombs";

        public void PlayCastleWing() => Load(castleWingScene);
        public void PlayCatacombs() => Load(catacombsScene);

        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            // Application.Quit does nothing in the editor, so a Quit button would
            // look broken to whoever is testing the menu.
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void Load(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return;
            // Time.timeScale survives a scene load, and the run-end screen freezes
            // it at 0. Returning to the menu and starting again would otherwise
            // load a level that never ticks.
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
