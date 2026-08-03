// Breathes a light shaft's alpha so it never sits perfectly still.
//
// Light through moving air is never steady, and a shaft held at a constant alpha
// reads instantly as a decal pasted on the floor. Two offset sine waves rather
// than one, so the period is not obvious, and a per-instance phase offset so the
// 18 torches never pulse together — that synchrony is the tell that gives away
// a room full of copies.
//
// Presentation only, and removable: delete the component and the shaft simply
// holds its authored alpha.
using UnityEngine;

namespace TimeKiller.Effects
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShaftFlicker : MonoBehaviour
    {
        [SerializeField] float amount = 0.35f;
        [SerializeField] float baseAlpha = 0.16f;
        [SerializeField] float speed = 0.6f;

        SpriteRenderer sprite;
        float phase;

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            // Seeded from the position, not Random: it must be stable across a
            // scene reload, or every restart re-rolls the whole room's timing.
            phase = Mathf.Abs(transform.position.x * 12.9898f + transform.position.y * 78.233f) % (Mathf.PI * 2f);
        }

        void Update()
        {
            if (sprite == null) return;
            float t = Time.time * speed + phase;
            float wobble = 0.65f * Mathf.Sin(t) + 0.35f * Mathf.Sin(t * 2.37f + 1.1f);
            var c = sprite.color;
            c.a = Mathf.Max(0f, baseAlpha * (1f + amount * wobble));
            sprite.color = c;
        }
    }
}
