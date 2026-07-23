// A programmable "controller" for the player: implements the same IInputSource
// the keyboard does, but is steered by TestDriver (waypoints + interact
// presses) instead of keys. This is the co-op/AI-driver seam of the player
// architecture put to work for automated playtests.
// Fully removable: lives only while TestDriver possesses the player.
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    public class ScriptedInputSource : MonoBehaviour, IInputSource
    {
        public Vector2 MoveInput { get; private set; }
        public bool RunHeld { get; private set; }
        public bool InteractPressed { get; private set; }

        public Vector2? Target;              // world position to walk toward
        public bool Run;                     // sprint toward the target
        public float arriveTolerance = 0.15f;

        bool interactQueued;

        /// Queue a single E press (consumed on the next frame, like a real tap).
        public void QueueInteract() => interactQueued = true;

        public bool Arrived => Target == null;

        void Update()
        {
            // One-frame interact pulse, exactly like a physical key tap.
            InteractPressed = interactQueued;
            interactQueued = false;

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
