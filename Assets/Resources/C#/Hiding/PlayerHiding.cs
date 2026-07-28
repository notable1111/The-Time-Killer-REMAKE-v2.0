// Player side of hiding. Lives on the Player object: watches for E near a
// wardrobe, swaps the state machine into HidingState (player invisible,
// intangible, parked at the spot), E again exits. A landed hit while hidden
// (the compromised-spot drag-out) forces an exit.
using TimeKiller.Core;
using TimeKiller.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace TimeKiller.Hiding
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHiding : MonoBehaviour
    {
        [SerializeField] HidingConfig config;

        public bool IsHidden { get; private set; }
        public HidingSpot CurrentSpot { get; private set; }

        PlayerController controller;
        HidingState hidingState;
        HidingSpot[] spots;
        SpriteRenderer[] renderers;
        Collider2D bodyCollider;
        Vector3 exitPosition;

        public void Init(HidingConfig hidingConfig) => config = hidingConfig;

        /// The wardrobe E would enter right now, or null. Same reasoning as
        /// ClockRepair.NearbyClock: the prompt must ask the question the input
        /// actually asks, not a copy of it that can drift.
        public HidingSpot NearbySpot =>
            config != null && spots != null && !IsHidden ? NearestFreeSpot() : null;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            hidingState = new HidingState(this, controller);
        }

        void Start()
        {
            spots = Object.FindObjectsByType<HidingSpot>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            bodyCollider = GetComponent<Collider2D>();
            EventBus.Subscribe<PlayerHitEvent>(OnHit);
            DebugOverlay.Watch("Hide", () =>
            {
                if (config == null) return "NO CONFIG";
                if (spots == null || spots.Length == 0) return "no spots";
                float best = float.MaxValue;
                foreach (var s in spots)
                    if (s != null) best = Mathf.Min(best, Vector2.Distance(transform.position, s.transform.position));
                return $"{(IsHidden ? "HIDDEN" : "out")} nearest {best:0.00}/{config.interactRange:0.00}";
            });
        }

        void OnDestroy() => EventBus.Unsubscribe<PlayerHitEvent>(OnHit);

        void Update()
        {
            if (config == null || !controller.Input.InteractPressed) return;

            if (IsHidden) { Exit(); return; }

            var spot = NearestFreeSpot();
            if (spot != null) Enter(spot);
        }

        HidingSpot NearestFreeSpot()
        {
            HidingSpot best = null;
            float bestDist = config.interactRange;
            foreach (var spot in spots)
            {
                if (spot == null || spot.Occupied) continue;
                float d = Vector2.Distance(transform.position, spot.transform.position);
                if (d <= bestDist) { bestDist = d; best = spot; }
            }
            return best;
        }

        void Enter(HidingSpot spot)
        {
            CurrentSpot = spot;
            IsHidden = true;
            exitPosition = transform.position;
            spot.SetOccupied(true);
            controller.ChangeState(hidingState);
            EventBus.Publish(new PlayerHidEvent { SpotPosition = spot.transform.position });
        }

        public void Exit()
        {
            if (!IsHidden) return;
            IsHidden = false;
            var spotPosition = CurrentSpot != null ? (Vector2)CurrentSpot.transform.position : (Vector2)transform.position;
            if (CurrentSpot != null) CurrentSpot.SetOccupied(false);
            CurrentSpot = null;
            controller.ChangeState(controller.Idle);
            EventBus.Publish(new PlayerUnhidEvent { SpotPosition = spotPosition });
        }

        void OnHit(PlayerHitEvent evt)
        {
            if (IsHidden) Exit(); // dragged out — the compromised-spot rule
        }

        // ---- the state ----

        public class HidingState : IState
        {
            readonly PlayerHiding hiding;
            readonly PlayerController player;

            public HidingState(PlayerHiding hiding, PlayerController player)
            {
                this.hiding = hiding;
                this.player = player;
            }

            public void Enter()
            {
                player.Motor.SetTargetVelocity(Vector2.zero);
                if (hiding.bodyCollider != null) hiding.bodyCollider.enabled = false;
                foreach (var r in hiding.renderers) if (r != null) r.enabled = false;
                // Park inside the wardrobe (maniac range checks measure to here).
                if (hiding.CurrentSpot != null)
                    player.transform.position = hiding.CurrentSpot.transform.position;

                // Release the room confiner while hidden: near zone edges it
                // clamps the camera and shoves the wardrobe off-center — the
                // hidden view must always center on the wardrobe (user 2026-07-23).
                var confiner = Object.FindAnyObjectByType<CinemachineConfiner2D>();
                if (confiner != null) confiner.enabled = false;
            }

            public void Tick(float deltaTime) { }   // exit is handled by PlayerHiding.Update (E key)
            public void FixedTick(float fixedDelta) { }

            public void Exit()
            {
                if (hiding.bodyCollider != null) hiding.bodyCollider.enabled = true;
                foreach (var r in hiding.renderers) if (r != null) r.enabled = true;
                player.transform.position = hiding.exitPosition;
                var body = player.GetComponent<Rigidbody2D>();
                if (body != null) { body.position = hiding.exitPosition; body.linearVelocity = Vector2.zero; }
                Physics2D.SyncTransforms();

                var confiner = Object.FindAnyObjectByType<CinemachineConfiner2D>(FindObjectsInactive.Include);
                if (confiner != null)
                {
                    confiner.enabled = true;
                    confiner.InvalidateBoundingShapeCache();
                }
            }
        }
    }
}
