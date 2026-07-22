// One wardrobe. Holds the closed/ajar sprites and the occupied flag; the
// interaction logic lives in PlayerHiding, the visibility rule in the maniac.
using UnityEngine;

namespace TimeKiller.Hiding
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class HidingSpot : MonoBehaviour
    {
        [SerializeField] Sprite closedSprite;
        [SerializeField] Sprite ajarSprite;

        public bool Occupied { get; private set; }

        SpriteRenderer spriteRenderer;

        void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        public void SetOccupied(bool occupied)
        {
            Occupied = occupied;
            // Ajar while empty (an invitation); shut tight while the player is inside.
            if (spriteRenderer != null && closedSprite != null && ajarSprite != null)
                spriteRenderer.sprite = occupied ? closedSprite : ajarSprite;
        }

        void Start() => SetOccupied(false);
    }
}
