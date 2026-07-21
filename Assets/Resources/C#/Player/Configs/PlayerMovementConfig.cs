// All tunable movement numbers in one Inspector-editable asset.
// Balance the game here, never by editing code.
using UnityEngine;

namespace TimeKiller.Player
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Player Movement", fileName = "PlayerMovementConfig")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Speeds (units/second)")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 4.5f;

        [Header("Responsiveness (units/second²)")]
        [Tooltip("How fast the player reaches target speed")]
        public float acceleration = 30f;
        [Tooltip("How fast the player stops when input is released")]
        public float deceleration = 40f;
    }
}
