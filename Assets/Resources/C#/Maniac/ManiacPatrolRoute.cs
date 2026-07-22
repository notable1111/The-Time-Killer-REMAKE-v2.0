// Ordered loop of patrol waypoints (child transforms, created by the setup
// script — route through the wing's main loop; the servant passage is
// deliberately NOT on it: that's the player's blind-spot escape route).
using UnityEngine;

namespace TimeKiller.Maniac
{
    public class ManiacPatrolRoute : MonoBehaviour
    {
        public Vector2 Waypoint(int index) =>
            transform.childCount == 0
                ? (Vector2)transform.position
                : (Vector2)transform.GetChild(((index % transform.childCount) + transform.childCount) % transform.childCount).position;

        public int Count => transform.childCount;

        /// Index of the waypoint closest to a world position (used to resume
        /// patrol sensibly after a chase ends far from where it began).
        public int ClosestIndex(Vector2 position)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < transform.childCount; i++)
            {
                float d = Vector2.Distance(position, transform.GetChild(i).position);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.7f);
            for (int i = 0; i < transform.childCount; i++)
            {
                var a = transform.GetChild(i).position;
                var b = transform.GetChild((i + 1) % transform.childCount).position;
                Gizmos.DrawWireSphere(a, 0.25f);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
