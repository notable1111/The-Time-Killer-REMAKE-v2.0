// Tunable values for the Core systems, editable in the Inspector.
// The asset lives at Resources/C#/Core/Configs/CoreConfig.asset and is loaded
// by GameBootstrap at startup via Resources.Load.
using UnityEngine;

namespace TimeKiller.Core
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Core Config", fileName = "CoreConfig")]
    public class CoreConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Core/Configs/CoreConfig";

        [Header("Debug Overlay")]
        [Tooltip("Overlay starts visible in Editor/Development builds")]
        public bool overlayVisibleOnStart = true;

        [Tooltip("Smoothing factor for the FPS display (higher = steadier number)")]
        [Range(0.8f, 0.99f)] public float fpsSmoothing = 0.95f;
    }
}
