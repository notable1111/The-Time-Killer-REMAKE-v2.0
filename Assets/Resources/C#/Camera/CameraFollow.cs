// Basic smooth-follow camera: keeps the target centered with SmoothDamp.
// Deliberately simple v1 — look-ahead, room confinement and shake come later.
// Removable: delete this component and the camera is static again.
using UnityEngine;

namespace TimeKiller.CameraSystem
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] CameraConfig config;

        Vector3 velocity;

        public void Init(Transform followTarget, CameraConfig cameraConfig)
        {
            target = followTarget;
            config = cameraConfig;
        }

        void LateUpdate() // after all movement, so the camera never lags a frame
        {
            if (target == null) return;
            float smooth = config != null ? config.smoothTime : 0.18f;
            var desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smooth);
        }
    }
}
