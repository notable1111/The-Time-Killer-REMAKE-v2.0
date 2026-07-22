// The maniac's physics body. States hand it a destination + speed; it steers
// the Rigidbody2D toward the point each physics step (same accel/brake pattern
// as PlayerMotor). No pathfinding — corridor geometry + the chase breadcrumb
// trail do the navigating; walls simply slide him along their surface.
using UnityEngine;

namespace TimeKiller.Maniac
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ManiacMotor : MonoBehaviour
    {
        ManiacConfig config;
        Rigidbody2D body;
        Vector2? destination;
        float speed;

        public Vector2 CurrentVelocity => body != null ? body.linearVelocity : Vector2.zero;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        public void Init(ManiacConfig maniacConfig)
        {
            config = maniacConfig;
            body = GetComponent<Rigidbody2D>();
        }

        public void MoveTo(Vector2 target, float moveSpeed)
        {
            destination = target;
            speed = moveSpeed;
        }

        public void Stop() => destination = null;

        public bool ReachedDestination(float tolerance) =>
            destination == null || Vector2.Distance(Position, destination.Value) <= tolerance;

        void FixedUpdate()
        {
            if (config == null) return;

            Vector2 targetVelocity = Vector2.zero;
            if (destination != null)
            {
                var toTarget = destination.Value - Position;
                if (toTarget.magnitude > 0.05f)
                    targetVelocity = toTarget.normalized * speed;
            }

            bool braking = targetVelocity.sqrMagnitude < body.linearVelocity.sqrMagnitude;
            float rate = braking ? config.deceleration : config.acceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        }
    }
}
