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
- **CameraFollow** — smooth-follow v1 (SmoothDamp in LateUpdate). Look-ahead/room-lock/shake planned. Tuning: `CameraConfig.asset`. Setup: menu `TimeKiller/Setup/10`.

### Lighting & depth (`C#/Lighting/`, namespace `TimeKiller.Lighting`)
- URP runs the **2D Renderer** (`URP_2DRenderer.asset`) with Y-axis transparency sorting: same-order sprites sort by world Y — lower on screen renders in front. Player and props share order 0 under SortingGroups, so walking behind/in front of objects just works.
- **Light2D setup** (menu `TimeKiller/Setup/11`): dark global ambient, warm flickering torch lights (`FlickerLight2D` — layered Perlin noise), soft player glow. All sprites use Sprite-Lit-Default; shadow/shade sprites stay unlit so light can't wash them out.
- **Contact shadows**: baked gradient strips at wall bases + blob shadows under props.

### Environment
- **Castle hall (active test space)** — fully dressed 2.5D hall from the RF Castle pack (user-imported, `Assets/RF Castle`): layered tilemaps (Floor -20, Rug -15, WallFace -10, WallDecor -9, Overhead +10 — the south band renders over the player), arched door, stained-glass window, banner, animated torches, box colliders. Built by `TimeKiller/Setup/9` (`CastleHallSetup.cs`) — pieces are sheet regions addressed as (col, row-from-top); rebuild = delete `CastleHall` + rerun menu.
- **Catacombs room builder** (`Setup/8`) — retired as test space, kept for future underground levels (floor-rect + auto-wall technique in `CatacombsRoomSetup.cs`).
- Tiles generated to `C#/Environment/Configs/`.

## Planned

- `Player` — stamina / health / sanity
- Environment — darkness + player light (deferred by choice), side-wall trims, more hall variety
- `Core` — dev cheat hotkeys (god mode, refill stats) once stats exist
