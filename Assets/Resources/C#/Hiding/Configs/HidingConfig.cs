// Tunables for the hiding system (interview 2026-07-23): E to enter/exit,
// the Outlast rule (seen entering = the spot is compromised), darkened view
// while hidden.
// The heartbeat knobs that used to live here moved to HeartbeatConfig when the
// audio got a single owner — hiding no longer has a heart of its own, it just
// raises the one PlayerHeartbeat already runs.
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
    }
}
