// Camera tuning values — the ONE place for all camera feel knobs.
// Used by the Cinemachine rig (v2) and by the legacy CameraFollow fallback.
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

        [Header("Look-ahead (toward facing)")]
        [Tooltip("How far the view drifts toward the direction the character faces (0 = off)")]
        [Range(0f, 3f)] public float lookAheadDistance = 1.1f;

        [Tooltip("How fast the drift settles — higher = snappier")]
        [Range(0.5f, 10f)] public float lookAheadSpeed = 2.5f;
    }
}
