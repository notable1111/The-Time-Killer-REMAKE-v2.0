// Dev tool: press P to peek the camera at the maniac, press P again to snap
// back to the player. Retargets the single CinemachineCamera's Follow — the
// Cinemachine brain blends across. Registered through CheatHotkeys, which is
// compiled out of release builds, so in a shipped build this component simply
// does nothing (no missing-script — the class stays compiled). Removable:
// delete the component; the camera keeps following the player.
using TimeKiller.Core;
using TimeKiller.Maniac;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeKiller.CameraSystem
{
    public class DebugManiacCam : MonoBehaviour
    {
        CinemachineCamera cine;
        Transform playerTarget;
        Transform maniac;
        bool peekingManiac;

        void Start()
        {
            cine = GetComponent<CinemachineCamera>();
            if (cine == null) cine = Object.FindAnyObjectByType<CinemachineCamera>();
            if (cine != null) playerTarget = cine.Follow;
            CheatHotkeys.RegisterCheat(Key.P, "Peek maniac cam", Toggle);
        }

        void OnDestroy()
        {
            // Never leave the camera stranded on the maniac if we're torn down mid-peek.
            if (peekingManiac && cine != null && playerTarget != null) cine.Follow = playerTarget;
        }

        void Toggle()
        {
            if (cine == null) return;
            if (!peekingManiac)
            {
                if (maniac == null)
                {
                    var m = Object.FindAnyObjectByType<ManiacController>();
                    if (m != null) maniac = m.transform;
                }
                if (maniac == null) { Debug.LogWarning("[DebugManiacCam] No maniac found to peek at."); return; }
                playerTarget = cine.Follow;   // remember where to return
                cine.Follow = maniac;
                peekingManiac = true;
            }
            else
            {
                cine.Follow = playerTarget;
                peekingManiac = false;
            }
        }
    }
}
