// Chase navigation without pathfinding: while the player is visible, his
// positions are recorded as a trail; the maniac steers to the oldest crumb,
// pops it when reached, and so threads through the same doorways the player
// used. Cleared when a chase starts or ends.
using System.Collections.Generic;
using UnityEngine;

namespace TimeKiller.Maniac
{
    public class ManiacBreadcrumbs : MonoBehaviour
    {
        readonly Queue<Vector2> trail = new Queue<Vector2>();
        ManiacConfig config;
        float nextRecordTime;

        public bool IsEmpty => trail.Count == 0;

        public void Init(ManiacConfig maniacConfig) => config = maniacConfig;

        public void Record(Vector2 position)
        {
            if (Time.time < nextRecordTime) return;
            nextRecordTime = Time.time + (config != null ? config.breadcrumbInterval : 0.15f);
            trail.Enqueue(position);
        }

        /// Oldest crumb not yet reached, or null when the trail is used up.
        public Vector2? NextTarget(Vector2 from, float tolerance)
        {
            while (trail.Count > 0 && Vector2.Distance(from, trail.Peek()) <= tolerance)
                trail.Dequeue();
            return trail.Count > 0 ? trail.Peek() : (Vector2?)null;
        }

        public void Clear() => trail.Clear();
    }
}
