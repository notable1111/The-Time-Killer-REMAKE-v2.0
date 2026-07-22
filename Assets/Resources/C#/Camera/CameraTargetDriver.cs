// Sits on the "CameraTarget" child of the Player. Cinemachine follows THIS
// transform, not the player directly: the target drifts toward the direction
// the character faces, so the view reveals what's ahead (horror look-ahead).
// Removable: delete the CameraTarget child and point Cinemachine back at the
// Player — everything else keeps working.
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.CameraSystem
{
    public class CameraTargetDriver : MonoBehaviour
    {
        [SerializeField] CameraConfig config;

        PlayerFacing facing;

        // Indexed by (int)FacingDirection: Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight.
        static readonly Vector2[] Directions =
        {
            new Vector2(0f, -1f), new Vector2(-0.707f, -0.707f), new Vector2(-1f, 0f),
            new Vector2(-0.707f, 0.707f), new Vector2(0f, 1f), new Vector2(0.707f, 0.707f),
            new Vector2(1f, 0f), new Vector2(0.707f, -0.707f),
        };

        public void Init(CameraConfig cameraConfig) => config = cameraConfig;

        void Awake() => facing = GetComponentInParent<PlayerFacing>();

        void LateUpdate()
        {
            if (facing == null || config == null) return;
            Vector2 desired = Directions[(int)facing.Current] * config.lookAheadDistance;
            // Exponential smoothing: frame-rate independent, no overshoot.
            float t = 1f - Mathf.Exp(-config.lookAheadSpeed * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, desired, t);
        }
    }
}
