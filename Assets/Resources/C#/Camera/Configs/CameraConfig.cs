// Camera tuning values. The follow behavior will grow (look-ahead, room
// locking, shake) — every future knob lives here, never in code.
using UnityEngine;

namespace TimeKiller.CameraSystem
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Camera Config", fileName = "CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [Header("Distance (zoom)")]
        [Tooltip("Camera distance: NEAR 1.5 (claustrophobic close-up) … FAR 8 (wide overview). 2.4 = the tight horror framing.")]
        [Range(1.5f, 8f)] public float viewSize = 2.4f;

        [Header("Follow")]
        [Tooltip("Seconds the camera takes to catch up — higher feels heavier/slower")]
        [Range(0.02f, 1f)] public float smoothTime = 0.18f;
    }
}
