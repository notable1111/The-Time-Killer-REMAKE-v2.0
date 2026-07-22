// Menu: TimeKiller/Setup/12 - Switch Camera to Cinemachine.
// Builds the Cinemachine rig: brain on the Main Camera (legacy CameraFollow
// disabled as fallback), a CameraTarget child on the Player (facing
// look-ahead), a CinemachineCamera with position composer + 2D confiner, and
// a CameraBounds polygon hard-confining the view to the castle hall.
// Touches nothing built by Setup/9 or 11 (hall objects stay untouched).
using TimeKiller.CameraSystem;
using TimeKiller.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class CameraV2Setup
    {
        const string ConfigPath = "Assets/Resources/C#/Camera/Configs/CameraConfig.asset";

        // Hall visible area: floor 0..16 x 0..10, north wall up to y=17,
        // south band down to y=-3, side walls at x=-2 and x=18.
        static readonly Vector2[] HallBounds =
        {
            new Vector2(-2f, -3f), new Vector2(18f, -3f),
            new Vector2(18f, 17f), new Vector2(-2f, 17f),
        };

        [MenuItem("TimeKiller/Setup/12 - Switch Camera to Cinemachine")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<CameraConfig>(ConfigPath);
            var cam = Camera.main;
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (config == null || cam == null || player == null)
            {
                Debug.LogError("[TimeKiller Setup] Need CameraConfig, Main Camera and Player. Run Setup/10 first if CameraConfig.asset is missing.");
                return;
            }

            // 1. Legacy follow off (kept as fallback), brain on.
            var legacy = cam.GetComponent<CameraFollow>();
            if (legacy != null) legacy.enabled = false;
            if (cam.GetComponent<CinemachineBrain>() == null)
                Undo.AddComponent<CinemachineBrain>(cam.gameObject);

            // 2. Look-ahead target as a Player child.
            var targetTr = player.transform.Find("CameraTarget");
            if (targetTr == null)
            {
                var targetGo = new GameObject("CameraTarget");
                Undo.RegisterCreatedObjectUndo(targetGo, "Camera Target");
                targetGo.transform.SetParent(player.transform, false);
                targetTr = targetGo.transform;
            }
            var driver = targetTr.GetComponent<CameraTargetDriver>();
            if (driver == null) driver = Undo.AddComponent<CameraTargetDriver>(targetTr.gameObject);
            driver.Init(config);
            EditorUtility.SetDirty(driver);

            // 3. Confinement bounds around the hall (trigger — no physics impact).
            var boundsGo = GameObject.Find("CameraBounds");
            if (boundsGo == null)
            {
                boundsGo = new GameObject("CameraBounds");
                Undo.RegisterCreatedObjectUndo(boundsGo, "Camera Bounds");
            }
            var poly = boundsGo.GetComponent<PolygonCollider2D>();
            if (poly == null) poly = Undo.AddComponent<PolygonCollider2D>(boundsGo);
            poly.isTrigger = true;
            poly.SetPath(0, HallBounds);

            // 4. The Cinemachine camera itself.
            var cineGo = GameObject.Find("CM_PlayerCamera");
            if (cineGo == null)
            {
                cineGo = new GameObject("CM_PlayerCamera");
                Undo.RegisterCreatedObjectUndo(cineGo, "Cinemachine Camera");
            }
            var cine = cineGo.GetComponent<CinemachineCamera>();
            if (cine == null) cine = Undo.AddComponent<CinemachineCamera>(cineGo);
            cine.Follow = targetTr;
            var lens = cine.Lens;
            lens.OrthographicSize = config.viewSize;
            cine.Lens = lens;

            if (cineGo.GetComponent<CinemachinePositionComposer>() == null)
                Undo.AddComponent<CinemachinePositionComposer>(cineGo);

            var confiner = cineGo.GetComponent<CinemachineConfiner2D>();
            if (confiner == null) confiner = Undo.AddComponent<CinemachineConfiner2D>(cineGo);
            confiner.BoundingShape2D = poly;

            var sync = cineGo.GetComponent<CinemachineConfigSync>();
            if (sync == null) sync = Undo.AddComponent<CinemachineConfigSync>(cineGo);
            sync.Init(config);
            EditorUtility.SetDirty(sync);

            cineGo.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            Debug.Log("[TimeKiller Setup] Cinemachine rig ready: facing look-ahead + hard hall confinement. Legacy CameraFollow disabled (fallback). Press Play.");
        }
    }
}
