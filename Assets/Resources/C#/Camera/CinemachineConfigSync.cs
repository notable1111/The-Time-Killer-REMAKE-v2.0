// Pushes CameraConfig values into the Cinemachine camera every frame, so the
// user keeps tuning ONE asset (view size slider, follow damping) and both the
// legacy and Cinemachine rigs obey it. ExecuteAlways = live in edit mode too.
using Unity.Cinemachine;
using UnityEngine;

namespace TimeKiller.CameraSystem
{
    [ExecuteAlways]
    [RequireComponent(typeof(CinemachineCamera))]
    public class CinemachineConfigSync : MonoBehaviour
    {
        [SerializeField] CameraConfig config;

        CinemachineCamera cine;
        CinemachinePositionComposer composer;

        public void Init(CameraConfig cameraConfig) => config = cameraConfig;

        void LateUpdate()
        {
            if (config == null) return;
            if (cine == null) cine = GetComponent<CinemachineCamera>();
            if (composer == null) composer = GetComponent<CinemachinePositionComposer>();

            var lens = cine.Lens;
            lens.OrthographicSize = config.viewSize;
            cine.Lens = lens;

            if (composer != null)
                composer.Damping = new Vector3(config.smoothTime, config.smoothTime, 0f);
        }
    }
}
