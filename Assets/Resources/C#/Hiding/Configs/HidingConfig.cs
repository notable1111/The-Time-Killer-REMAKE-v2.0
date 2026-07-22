// Tunables for the hiding system (interview 2026-07-23): E to enter/exit,
// the Outlast rule (seen entering = the spot is compromised), darkened view
// + proximity heartbeat while hidden.
// Asset: C#/Hiding/Configs/HidingConfig.asset.
using UnityEngine;

namespace TimeKiller.Hiding
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Hiding", fileName = "HidingConfig")]
    public class HidingConfig : ScriptableObject
    {
        // All fields below are read live every frame — drag them in Play Mode
        // (select HidingConfig.asset) and the change applies instantly. SO edits
        // made in Play Mode persist, so tuned values stick.

        [Header("Interaction")]
        [Range(0.5f, 4f), Tooltip("How close (units) the player must be to a spot to enter it with E. Measured to the sprite CENTER, so this must cover half the wardrobe height.")]
        public float interactRange = 1.8f;

        // (The seen-enter window lives in ManiacConfig — it's HIS perception rule.)

        [Header("Hidden view")]
        [Range(0f, 1f), Tooltip("How dark the slat overlay gets while hidden.")]
        public float overlayAlpha = 0.82f;
        [Range(0.05f, 1.5f), Tooltip("Seconds for the hide/unhide overlay fade.")]
        public float overlayFade = 0.35f;

        [Header("Proximity heartbeat (hidden only)")]
        [Range(3f, 25f), Tooltip("Distance at which the hidden heartbeat starts being audible.")]
        public float heartbeatRange = 10f;
        [Range(40f, 100f), Tooltip("BPM when he's at the edge of range.")]
        public float farBpm = 60f;
        [Range(80f, 200f), Tooltip("BPM when he's on top of the wardrobe.")]
        public float nearBpm = 140f;
        [Range(0f, 1f)] public float heartbeatMaxVolume = 0.85f;
    }
}
