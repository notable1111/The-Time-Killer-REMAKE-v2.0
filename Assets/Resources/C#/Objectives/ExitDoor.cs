// The way out. A solid blocking collider seals it until every clock is fixed;
// on AllClocksFixedEvent it opens (doors swing wide, moonlight spills in), and
// the player walking into its trigger publishes GameWonEvent. Removable.
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Objectives
{
    public class ExitDoor : MonoBehaviour
    {
        [SerializeField] Collider2D block;    // NON-trigger: blocks passage while locked
        [SerializeField] SpriteRenderer sr;   // optional visual
        [SerializeField] Sprite lockedSprite; // gate shut
        [SerializeField] Sprite openSprite;   // gate swung open onto the night
        [SerializeField] Light2D glow;        // cold moonlight through the opening (off while locked)
        [SerializeField] Material lockedMat;  // lit: the shut gate obeys torchlight
        [SerializeField] Material openMat;    // unlit: the night beyond glows on its own
        [SerializeField] Color lockedTint = Color.white;
        [SerializeField] Color openTint = Color.white;

        [Header("Audio")]
        [SerializeField] AudioSource wind;   // 3D loop: the beacon you navigate back to
        [SerializeField] AudioClip creak;    // local one-shot as the doors swing
        [Tooltip("The gate crashing open is heard map-wide — the hunter learns where it is.")]
        [SerializeField] bool alertsHunters = true;

        bool open;

        void Start()
        {
            EventBus.Subscribe<AllClocksFixedEvent>(OnAllFixed);
            SetOpen(false);
        }

        void OnDestroy() => EventBus.Unsubscribe<AllClocksFixedEvent>(OnAllFixed);

        void OnAllFixed(AllClocksFixedEvent evt) => SetOpen(true, announce: true);

        // announce=false is the silent Start() setup; true is the actual moment
        // the gate gives way — that one creaks, howls, and carries across the map.
        void SetOpen(bool o, bool announce = false)
        {
            open = o;
            if (block != null) block.enabled = !o;
            if (glow != null) glow.enabled = o;

            if (sr != null)
            {
                sr.color = o ? openTint : lockedTint;
                var swap = o ? openSprite : lockedSprite;
                if (swap != null) sr.sprite = swap;
                var mat = o ? openMat : lockedMat;
                if (mat != null) sr.sharedMaterial = mat;
            }

            if (wind != null)
            {
                if (o && !wind.isPlaying) wind.Play();
                else if (!o && wind.isPlaying) wind.Stop();
            }

            if (!announce || !o) return;
            if (wind != null && creak != null) wind.PlayOneShot(creak);
            if (alertsHunters)
                EventBus.Publish(new WorldNoiseEvent
                {
                    Position = transform.position,
                    Loudness = 1f,
                    AlwaysHeard = true, // he always learns the way out is open
                });
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!open || other.GetComponentInParent<PlayerController>() == null) return;
            EventBus.Publish(new GameWonEvent());
        }
    }
}
