// The physics body of the player. States tell it a target velocity; it
// accelerates/decelerates the Rigidbody2D toward that target each physics step.
// Top-down game: gravity is zero, rotation is frozen (set up by PlayerSetup).
using UnityEngine;

namespace TimeKiller.Player
{
    // Last on the physics step, after whoever decided the target velocity. The
    // chain is BotPilot (-200) -> ScriptedInputSource (-150) -> PlayerController
    // (0) -> this (50); a human's input arrives on the render frame, so only the
    // scripted driver cares, and for it the whole decision now lands inside one
    // fixed step at any timeScale.
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMotor : MonoBehaviour
    {
        PlayerMovementConfig config;
        Rigidbody2D body;
        Vector2 targetVelocity;

        public Vector2 CurrentVelocity => body != null ? body.linearVelocity : Vector2.zero;

        public void Init(PlayerMovementConfig movementConfig)
        {
            config = movementConfig;
            body = GetComponent<Rigidbody2D>();
        }

        public void SetTargetVelocity(Vector2 velocity) => targetVelocity = velocity;

        void FixedUpdate()
        {
            if (config == null) return;

            // Accelerate toward the target; brake faster than we speed up so
            // stopping feels snappy (important for dodging in horror chases).
            bool braking = targetVelocity.sqrMagnitude < body.linearVelocity.sqrMagnitude;
            float rate = braking ? config.deceleration : config.acceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        }
    }
}
