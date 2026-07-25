// A programmable "controller" for the player: implements the same IInputSource
// the keyboard does, but is steered by TestDriver (waypoints + interact
// presses) instead of keys. This is the co-op/AI-driver seam of the player
// architecture put to work for automated playtests.
// Fully removable: lives only while TestDriver possesses the player.
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    // Runs after BotPilot (-200) and before PlayerController (0), so the steering
    // below is always computed from the Target the pilot set THIS physics step and
    // is always read by the player's states in the same one. Script execution
    // order used to decide whether a decision cost one frame or two — a hidden
    // variable that changed with timeScale, which is exactly what this pass exists
    // to remove.
    [DefaultExecutionOrder(-150)]
    public class ScriptedInputSource : MonoBehaviour, IInputSource
    {
        public Vector2 MoveInput { get; private set; }
        public bool RunHeld { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool SkillCheckPressed { get; private set; }

        public Vector2? Target;              // world position to walk toward
        public bool Run;                     // sprint toward the target
        public float arriveTolerance = 0.15f;

        bool interactQueued;
        bool skillCheckQueued;

        /// Queue a single E press (consumed on the next frame, like a real tap).
        public void QueueInteract() => interactQueued = true;
        /// Queue a single Space press (clock-repair skill-check).
        public void QueueSkillCheck() => skillCheckQueued = true;

        public bool Arrived => Target == null;

        // The two halves of a controller tick on different clocks on purpose.
        //
        // PULSES stay on the RENDER frame, because that is where their consumers
        // live: ClockRepair and PlayerInteractor read InteractPressed /
        // SkillCheckPressed in Update. Published from FixedUpdate they would be
        // raised and cleared several times between two Updates at high timeScale
        // (3.3 physics steps per frame at 4x) and every press but the last would
        // vanish — the harness would then measure its own dropped inputs.
        void Update()
        {
            // One-frame interact pulse, exactly like a physical key tap.
            InteractPressed = interactQueued;
            interactQueued = false;
            SkillCheckPressed = skillCheckQueued;
            skillCheckQueued = false;
        }

        // STEERING runs on the physics clock. FixedUpdate is a fixed cadence in
        // GAME time, so the rate at which the bot corrects its course no longer
        // falls with timeScale — an Update-driven steer recomputed at 60 Hz of
        // game time at 1x but only 15 Hz at 4x, while ManiacMotor has always
        // re-solved its own heading every physics step. That asymmetry, not the
        // maniac's AI, is what an accelerated batch was measuring.
        void FixedUpdate()
        {
            RunHeld = Run;

            if (Target == null) { MoveInput = Vector2.zero; return; }
            Vector2 delta = Target.Value - (Vector2)transform.position;
            if (delta.magnitude <= arriveTolerance)
            {
                Target = null;
                MoveInput = Vector2.zero;
                return;
            }
            MoveInput = delta.normalized;
        }
    }
}
