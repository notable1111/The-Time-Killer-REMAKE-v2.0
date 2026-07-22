# Architecture

How the systems of The Time Killer Remake connect. Update this file whenever a system is added or changed.

## Principles

- **Heavy architecture, small pieces:** single-purpose components; every tunable number lives in a ScriptableObject config (`C#/<Feature>/Configs/`), never hardcoded.
- **Event-driven:** systems talk through the Core event bus, not direct references. A system should compile even if the systems it talks about don't exist yet.
- **Input ≠ logic:** input reading is a separate layer from character/system behavior, so a second player (future co-op) or an AI can drive the same character code.
- **State machines** for anything with modes (player movement, enemy AI, game flow).
- **Editor setup scripts** (menu `TimeKiller/Setup/...`) create and wire scene objects — scenes are never hand-authored YAML.
- **Swappable content, removable features:** any asset (character, sounds, sprites) must be replaceable by editing configs only — never code; and any feature component (e.g. footsteps) must be removable from its object without breaking the others. Don't switch content until the full replacement asset set is ready.

## Systems

### Core (`C#/Core/`, namespace `TimeKiller.Core`)
- **GameBootstrap** — runs before the first scene loads (`RuntimeInitializeOnLoadMethod`), clears static state, spawns the persistent `[TimeKillerCore]` object. No scene setup needed — the game boots itself.
- **EventBus** — static typed publish/subscribe (`EventBus.Publish(new SomeEvent{...})`). The only sanctioned way for systems to talk across features.
- **ServiceLocator** — registry for long-lived services (`Register<T>` / `Get<T>`).
- **StateMachine + IState** — reusable state machine driven by `Tick`/`FixedTick`.
- **DebugOverlay** — F1 overlay: FPS + any values features register via `DebugOverlay.Watch(label, getter)`. Compiled out of release builds.
- **CoreConfig** (SO) — overlay tunables. Asset: `C#/Core/Configs/CoreConfig.asset`.

### Player (`C#/Player/`, namespace `TimeKiller.Player`)
Flow: `IInputSource` → states → `PlayerMotor` → `Rigidbody2D`.
- **IInputSource / KeyboardInputSource** — input abstraction (WASD + Shift via Input System). Co-op or AI drivers implement the same interface.
- **PlayerController** — composition root; owns the state machine (Idle/Walk/Run in `PlayerStates.cs`), publishes `PlayerStateChangedEvent`.
- **PlayerMotor** — accel/decel toward target velocity on `Rigidbody2D` (gravity 0, top-down).
- **PlayerFacing** — 4-direction facing, publishes `PlayerFacingChangedEvent` (animation hooks onto this).
- **PlayerMovementConfig** (SO) — speeds and responsiveness. Asset: `C#/Player/Configs/PlayerMovementConfig.asset`.
- **PlayerAnimationDriver** — listens to state/facing events, tells `SpriteAnimator` (Core) which clip to play. Walk = run clip slowed via `PlayerAnimationSet.walkAnimationSpeed` until real walk sheets exist.
- **PlayerAnimationSet** (SO) — maps state+facing → clips. Asset + generated clips: `C#/Player/Configs/`.
- Scene setup: menu `TimeKiller/Setup/4 - Create Player In Scene` (slices Adventurer sheets, creates configs, builds the Player object + top-down camera), then `5 - Generate Player Animations`.

### Sprite animation (in Core)
- **SpriteAnimationClip** (SO) — frames + fps + loop + `eventFrames` (foot contacts etc.), generated from sliced sheets.
- **SpriteAnimator** — plays clips on a SpriteRenderer; swap clips mid-play preserving phase; `SetSpeed` for live velocity-matched playback; fires `FrameReached` on event frames. Reusable for enemies/props.

### Footsteps & stealth noise (Player)
- **PlayerFootsteps** — on each foot-contact frame: plays a random Kenney step sound (pitch-jittered, quiet walk / loud run) and publishes `PlayerFootstepEvent{Position, IsRunning, Loudness}`. Enemy hearing will consume this event — audio volume and stealth noise are the same number by design.
- **PlayerFootstepConfig** (SO) — step clips + walk/run loudness + pitch jitter.
- Animation playback speed follows real velocity (`PlayerAnimationDriver.Update`), facing uses stickiness hysteresis (`PlayerFacing`) to prevent diagonal flicker. Camera: tight horror framing (ortho 2.4), follow deliberately deferred.
- Setup: menu `TimeKiller/Setup/6 - Setup Footsteps + Tight Camera` (after 4 and 5).

### Camera (`C#/Camera/`, namespace `TimeKiller.CameraSystem`)
- **v2 = Cinemachine rig** (menu `TimeKiller/Setup/12`): `CM_PlayerCamera` (CinemachineCamera + PositionComposer + Confiner2D) follows the **CameraTarget** child of the Player. `CameraTargetDriver` drifts that child toward the direction the character faces (look-ahead). `CameraBounds` polygon hard-confines the view to the hall — extend its path (or add per-room bounds) when the map grows. `CinemachineConfigSync` pushes `CameraConfig.asset` values (view size, damping) into Cinemachine every frame — tune the ONE asset, in edit or play mode.
- **CameraFollow** — legacy v1, kept on the Main Camera but disabled (fallback: disable CM objects, re-enable it). Setup: menu `TimeKiller/Setup/10`.
- All knobs in `CameraConfig.asset`: viewSize (zoom 1.5–8), smoothTime, lookAheadDistance/Speed.

### Lighting & depth (`C#/Lighting/`, namespace `TimeKiller.Lighting`)
- URP runs the **2D Renderer** (`URP_2DRenderer.asset`) with Y-axis transparency sorting: same-order sprites sort by world Y — lower on screen renders in front. Player and props share order 0 under SortingGroups, so walking behind/in front of objects just works.
- **Light2D setup** (menu `TimeKiller/Setup/11`): dark global ambient, warm flickering torch lights (`FlickerLight2D` — layered Perlin noise), soft player glow. All sprites use Sprite-Lit-Default; shadow/shade sprites stay unlit so light can't wash them out.
- **Contact shadows**: baked gradient strips at wall bases + blob shadows under props.

### Environment
- **Castle hall (active test space)** — fully dressed 2.5D hall from the RF Castle pack (user-imported, `Assets/RF Castle`): layered tilemaps (Floor -20, Rug -15, WallFace -10, WallDecor -9, Overhead +10 — the south band renders over the player), arched door, stained-glass window, banner, animated torches, box colliders. Built by `TimeKiller/Setup/9` (`CastleHallSetup.cs`) — pieces are sheet regions addressed as (col, row-from-top); rebuild = delete `CastleHall` + rerun menu.
- **Castle wing / map v1** (`Setup/14`, `CastleWingSetup.cs`) — LOOP: Hall → east corridor → Guardroom (B) → dark north corridor → Great Chamber (C) → corridor → Chapel (D) → dark south corridor → Hall west door. Floor plan = `NewAreas` rects; walls raise GENERICALLY from floor adjacency (north faces, overhead south bands, 2-thick side columns) so openings are automatic. Collision = per-cell boxes merged in `WingColliders` composite. Camera = per-zone boxes in `CameraBounds` composite (Confiner2D). Hall's protected colliders: east/west boxes split around the doorways (approved), values preserved. Vertical connector corridors are deliberately UNLIT.
- **Catacombs room builder** (`Setup/8`) — retired as test space, kept for future underground levels (floor-rect + auto-wall technique in `CatacombsRoomSetup.cs`).
- Tiles generated to `C#/Environment/Configs/`.

### LDtk map pipeline (side-by-side, awaiting switch approval)
- **Source of truth: `Assets/Resources/Assets/Maps/CastleWing.ldtk`** — tile layers + `Collision` IntGrid layer (1=Wall; rule: every non-floor cell adjacent to floor, 100% coverage) + `CameraZone` entities (the Cinemachine confiner rectangles). All three edit visually in the LDtk editor (ldtk.io); a reimport happens automatically on save.
- **Scene builder** (`Setup/18`, `LDtkWingSceneSetup.cs`) — builds `Assets/Scenes/CastleWingLDtk.unity` from the LDtk file: configures the LDtkToUnity importer (PPU 16, composite colliders, `WallIntGridTile` with Grid collider type, per-layer sorting orders matching Setup/9), instantiates + aligns the prefab, runs the full player pipeline (Setup 4→7), re-applies the hall collider snapshot, builds props/torches via `CastleWingSetup.BuildWingDressing` (shared code), and parses CameraZone entities into the `CameraBounds` composite. "Restyle LDtk Map" re-applies lit materials after reimports.
- **Hall collider snapshot** (`Setup/17`, `HallColliderGuard.cs`) — `C#/Environment/Configs/HallColliderSnapshot.json` stores the hand-tuned hall boxes; Export reads the scene, Re-apply restores them exactly (also called by Setup/18). This is the durable form of the "never overwrite hall tuning" rule.
- **Switch status:** SampleScene (script tilemaps) is still the main scene; CastleWingLDtk becomes primary only after team playtest approval. After the switch, map editing = LDtk editor only (Setup/9/14 tile painting retires).
- **Map v2 (ground floor, LDtk-only — NOT in SampleScene):** Kitchen (south of Guardroom), Armory (east of Guardroom), Library (east of Great Chamber), Servant Passage (hidden door in the hall's south wall → dark under-map passage → Kitchen; hall south collider auto-split around the door by Setup/18). Torches in the new rooms + corridors; vertical connectors and the passage stay dark. UpperGallery/Undercroft levels parked in `Tools/MapPipeline/mapv2_generate.py` (generator + validator + preview renderer — the offline map pipeline; run from repo root with Python 3, then reimport + Setup/18).

### Player health (`C#/Player/`, part of the Player feature)
- **PlayerHealth** — the 3-point health system (team decision; no stamina). Listens for `PlayerHitEvent` on the bus (attackers never reference the player), applies damage + invulnerability window + shove impulse away from the source + red hit-flash, publishes `PlayerHealthChangedEvent` / `PlayerDiedEvent`. v1 death rule (placeholder until save/death design): respawn at spawn with full HP.
- **PlayerHealthConfig** (SO) — maxHealth(3), i-frame seconds, shove impulse, death rule. Asset: `C#/Player/Configs/PlayerHealthConfig.asset`.
- **CheatHotkeys** (Core) — dev-only hotkey registry (compiled out of release). Features register their own cheats: F5 god mode, F6 refill health. Setup: menu `TimeKiller/Setup/19` (also run by Setup/18).

### Maniac (`C#/Maniac/`, namespace `TimeKiller.Maniac`)
The killer. Design (interview 2026-07-22): hearing+sight hybrid, patrols the wing loop, chase FASTER than player run (5.2 vs 4.5) balanced by a generous lose-sight timer; catch = 1 HP hit + shove. Flow: `ManiacPerception` (senses) → states → `ManiacMotor` → `Rigidbody2D`.
- **ManiacController** — composition root; owns the state machine (Patrol/Investigate/Chase/Attack in `ManiacStates.cs`), publishes `ManiacStateChangedEvent`, aims the sight cone along movement.
- **ManiacPerception** — hearing: subscribes `PlayerFootstepEvent`, heard when distance < loudness × hearingRadius (running is loud — the stealth choice). Sight: range + cone + LinecastAll that walls block (self/player/triggers filtered). Publishes `ManiacHeardNoiseEvent` / `ManiacSpottedPlayerEvent`.
- **ManiacMotor** — destination steering on Rigidbody2D, same accel/brake pattern as PlayerMotor. No pathfinding by design.
- **ManiacBreadcrumbs** — chase navigation: records the player's positions while visible; he follows the trail through the same doorways the player used.
- **ManiacPatrolRoute** — waypoint loop object (gizmo-drawn). The servant passage is deliberately NOT on the route — the player's blind-spot escape.
- **ManiacConfig** (SO) — every tunable: speeds, hearing radius, sight range/cone, lose-sight seconds (the balancing valve), attack damage/range/cooldown, patrol pacing. Asset: `C#/Maniac/Configs/ManiacConfig.asset`.
- Scene setup: menu `TimeKiller/Setup/20 - Create Maniac In Scene` — REFUSES to run without the Nightmare Slashers pack under `Outsource/NightmareSlashers` (no invisible enemies / no placeholder art). Animation slicing pass comes once the killer look is picked from the pack.

## Planned

- `Player` — sanity
- `Maniac` — animation set from Nightmare Slashers pack (slicing setup), hiding-spot interaction, de-aggro polish
- Environment — darkness + player light (deferred by choice), AI furniture props for map v2 rooms
