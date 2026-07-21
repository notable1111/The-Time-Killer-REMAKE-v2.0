// Camera tuning values. The follow behavior will grow (look-ahead, room
// locking, shake) — every future knob lives here, never in code.
using UnityEngine;

namespace TimeKiller.CameraSystem
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Camera Config", fileName = "CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [Tooltip("Seconds the camera takes to catch up — higher feels heavier/slower")]
        [Range(0.02f, 1f)] public float smoothTime = 0.18f;
    }
}
