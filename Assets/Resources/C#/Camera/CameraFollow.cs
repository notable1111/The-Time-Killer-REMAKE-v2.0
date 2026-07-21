// Basic smooth-follow camera: keeps the target centered with SmoothDamp and
// applies the distance (zoom) from CameraConfig — tunable live, also in edit
// mode thanks to ExecuteAlways. Deliberately simple v1; look-ahead, room
// confinement and shake come later. Removable: delete this component and the
// camera is static again.
using UnityEngine;

namespace TimeKiller.CameraSystem
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] CameraConfig config;

        Camera cam;
        Vector3 velocity;

        public void Init(Transform followTarget, CameraConfig cameraConfig)
        {
            target = followTarget;
            config = cameraConfig;
        }

        void LateUpdate() // after all movement, so the camera never lags a frame
        {
            if (cam == null) cam = GetComponent<Camera>();

            // Distance applies always — drag the slider in CameraConfig.asset
            // and the view reacts immediately, in edit mode and play mode.
            if (config != null && cam.orthographic)
                cam.orthographicSize = config.viewSize;

            // Position-follow only in play mode (never drag the camera while editing).
            if (!Application.isPlaying || target == null) return;
            float smooth = config != null ? config.smoothTime : 0.18f;
            var desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smooth);
        }
    }
}
