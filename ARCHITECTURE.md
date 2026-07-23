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
The killer. Design (interview 2026-07-22, v1.1 playtest update 2026-07-23): hearing+sight hybrid, patrols the wing loop, chase FASTER than player run (5.2 vs 4.5) balanced by a generous lose-sight timer; catch = 1 HP hit + shove. **He never stops:** a swing costs him only 0.35s recovery — the cooldown runs while chasing. **The escape is the player's job:** post-hit adrenaline (1.3× for 2.5s, in PlayerHealthConfig) + ghost-through (player↔maniac collision off for 2.5s after a landed hit — his unpushable mass-400 body can't pin a cornered player; collision restores once separated). Flow: `ManiacPerception` (senses) → states → `ManiacMotor` → `Rigidbody2D`.
- **ManiacController** — composition root; owns the state machine (Patrol/Investigate/Chase/Attack in `ManiacStates.cs`), publishes `ManiacStateChangedEvent`, aims the sight cone along movement.
- **ManiacPerception** — hearing: subscribes `PlayerFootstepEvent`, heard when distance < loudness × hearingRadius (running is loud — the stealth choice). Sight: range + cone + LinecastAll that walls block (self/player/triggers filtered). Publishes `ManiacHeardNoiseEvent` / `ManiacSpottedPlayerEvent`.
- **ManiacMotor** — destination steering on Rigidbody2D, same accel/brake pattern as PlayerMotor. No pathfinding by design.
- **ManiacBreadcrumbs** — chase navigation: records the player's positions while visible; he follows the trail through the same doorways the player used.
- **ManiacPatrolRoute** — waypoint loop object (gizmo-drawn). The servant passage is deliberately NOT on the route — the player's blind-spot escape.
- **ManiacConfig** (SO) — every tunable: speeds, hearing radius, sight range/cone, lose-sight seconds (the balancing valve), attack damage/range/cooldown, patrol pacing. Asset: `C#/Maniac/Configs/ManiacConfig.asset`.
- Scene setup: menu `TimeKiller/Setup/20 - Create Maniac In Scene` — REFUSES to run without the Nightmare Slashers pack under `Outsource/NightmareSlashers` (no invisible enemies / no placeholder art). Animation slicing pass comes once the killer look is picked from the pack.

### Health VFX (`C#/HealthVfx/`, namespace `TimeKiller.HealthVfx`)
Diegetic health — no HP bar, the screen is the health bar (design 2026-07-22). Bands on 3 HP: **3 = clean · 2 = subtle red heartbeat vignette · 1 = heavy blood + panicked breathing = "next hit kills"**, plus a splatter flash on every hit.
- **HealthVfxDirector** — subscribes `PlayerHealthChangedEvent`/`PlayerHitEvent`/`PlayerDiedEvent` only (knows nothing about player/maniac; delete the object and the game runs unchanged). One BPM phase clock (74 at 2 HP, 118 at 1 HP) drives EVERYTHING in sync: URP Vignette + Chromatic Aberration (|sin|³ systole modulation), dual counter-pulsing blood layers with scale-breath, film grain + desaturation ramps, and the lub-dub SFX fired on the exact visual systole frame. Hits: splatter flash (scale punch) + Cinemachine impulse shake + pitch-jittered blood-splash SFX; death: impact sting; critical: breathing loop fade.
- **HealthVfxConfig** (SO) — band thresholds, per-band vignette/overlay/chromatic/grain/desat, BPM + pulse depths + heart volumes, hit/death SFX levels, shake/punch, fades. Asset: `C#/HealthVfx/Configs/`.
- Assets: OpenGameArt CC0 blood overlays reprocessed by `Tools/VfxPipeline/process_blood.py` (radial edge mask, two-tone crimson); heartbeat synthesized by `Tools/VfxPipeline/gen_heartbeat.py` (S1/S2 physiological model, license-free); breathing/splash/impact from the PSX SFX pack.
- Setup: menu `TimeKiller/Setup/22` (also run by Setup/18). Dev cheat: **F7 = take 1 hit** (respects i-frames).
- Camera note: `CameraConfig.lookAheadDistance` set to **0** (user decision 2026-07-22) — player stays dead-center; the look-ahead system remains available via the slider.

### Effects (`C#/Effects/`, namespace `TimeKiller.Effects`) — the "Feel-lite" juice system
One juice moment = ONE asset. `EffectPlayer.Play(recipe, worldPos)` fires a full impact package; adding a new effect to the game = creating a recipe asset, zero new code.
- **EffectRecipe** (SO) — particle prefab (+scale/lifetime), random SFX clips (+volume/pitch jitter), Cinemachine shake (strength/duration). CFXR Free prefabs drop straight into the particle slot when imported.
- **EffectPlayer** (static) — lazily builds its own runtime rig (audio source + impulse source), instantiates/auto-destroys particles. No scene setup needed.
- **PlayerHitEffects** — binds `PlayerHitEvent` → PlayerHit recipe (world blood burst + splash SFX + shake) and `PlayerDiedEvent` → PlayerDeath recipe (bigger burst + impact sting + heavy shake). Impact juice lives HERE; HealthVfxDirector is screen-state only (bands/heartbeat/breathing/flash).
- BloodBurst particle prefab: built by Setup/23 from droplet sprites cropped out of the CC0 splats (`Tools/VfxPipeline`) — real assets, not placeholders. URP Particles/Unlit material, gravity-arced droplets, sorting order 5.
- Setup: menu `TimeKiller/Setup/23` (also run by Setup/18).

### Audio (`C#/Audio/`, namespace `TimeKiller.Audio`) — the tension radar
The music IS the threat detector (interview 2026-07-23). One layer at a time, crossfaded (priority: Safe > Chase > Tense > Calm), driven ONLY by bus events — no reference to the maniac.
- **AudioDirector** — maps maniac state to layers: Patrol→Calm (5 creepy ambiences), Investigate→Tense (4 tense), Chase/Attack→Chase (3 tracks, random per chase). Safe zones (config rects = the servant passage) play the safe-room theme when not actively chased. Stings: random jumpscare on `ManiacSpottedPlayerEvent` (4s cooldown), Dark Riser on first `ManiacHeardNoiseEvent` out of Calm (8s cooldown — running is audibly punished), death sting on `PlayerDiedEvent`. Two crossfading music sources + one sting source. Debug overlay shows current layer + track.
- **AudioConfig** (SO) — all track arrays, per-layer volumes, sting cooldowns, crossfade time, safe-zone rects. Asset: `C#/Audio/Configs/`.
- Tracks: PSX Horror Music pack (royalty-free, credited). Setup: menu `TimeKiller/Setup/24` (also run by Setup/18).

### Hiding (`C#/Hiding/`, namespace `TimeKiller.Hiding`) — the last verb of hide & run
Design (interview 2026-07-23): **E** to enter/exit a wardrobe; **the Outlast rule** — hiding only works if he didn't see you enter; darkened slat view + proximity heartbeat while hidden.
- **HidingSpot** — one wardrobe: occupied flag + closed/ajar sprite swap (ajar while empty — an invitation; shut while you're inside).
- **PlayerHiding** (+ its `HidingState` for the player state machine) — E near a free spot parks the player at it: invisible, intangible, motionless; E again (or a landed hit — the drag-out) exits at the entry position. Publishes `PlayerHidEvent` / `PlayerUnhidEvent`.
- **Maniac side:** `ManiacPerception.PlayerHidden` blinds sight while hidden; `ManiacController.CompromisedSpot` — if he had eyes on the player within `seenEnterWindow` (ManiacConfig, 1.25s) before they hid, he marches to the spot and drags a hit out (ChaseState/AttackState ignore sight rules for a compromised spot).
- **HidingVfx** — screen-space slat overlay (self-made texture, door-crack slit in the middle) + proximity heartbeat: BPM 60→140 and volume scale with the maniac's distance to the wardrobe. Presentation only. F1 overlay line `HideVfx` shows live dist/BPM/volume/slat for tuning.
- **HidingConfig** (SO) — interact range, overlay alpha/fade, heartbeat range/BPM/volume. All fields are `[Range]` sliders read live every frame — tune in Play Mode (SO edits persist). interactRange raised to 2.2 (2026-07-23): it measures to the sprite CENTER, and 1.8 was borderline at the wardrobe's foot.
- Art: AI-generated wardrobes (approved 2026-07-23) — style A flat-top (hall/guardroom/kitchen), style B gothic crown (chapel/great chamber/library), closed+ajar states each, in `Assets/Resources/Assets/Hiding/`. Seven spots: six original + armory (Setup/27, additive — refuses to run if the armory already has one).
- Fix that rode along: `PlayerAnimationDriver.IsMovingState` is now an explicit Walk/Run list — unknown states (Hiding) no longer play run-clip frame events that would publish phantom footstep noise.
- Setup: menu `TimeKiller/Setup/25` (also run by Setup/18).

### Furniture (map v2 props, `FurnitureSetup.cs` in `C#/Editor/`)
Lived-in dressing for the map-v2 rooms (interview 2026-07-23): kitchen 10 props, armory 9, library 10 (bookcase reused), 28 unique sprites in `Assets/Resources/Assets/Furniture/`.
- **Mixed colliders (team decision):** big furniture gets a base-only BoxCollider2D (you collide with its feet, not its painted height); small dressing (stool, sacks, firewood, shield, globe...) is walk-through. Every prop: bottom-center pivot → Y-sorts with the player under a SortingGroup, unlit blob shadow child, Sprite-Lit material. The library candelabra carries a `FlickerLight2D` candle light.
- **PROTECTED PLACEMENT** — same class as HidingSpots and the hall colliders: if a `Furniture` object exists, Setup/26 refuses to rebuild. The user drags props freely; full re-place = delete the object manually first.
- **Art pipeline** (`Tools/ArtPipeline/process_props.py`): AI prop sheets (Recraft V4.1, RF-Castle palette hint, flat magenta key background) → magenta key-out → connected-component slicing → per-prop downscale to 16 PPU targets → quantize to the full 57-color RF Castle tileset palette → labeled contact sheet for approval. Per-room shadow-lift (`BRIGHTEN` gamma/gain) — kitchen ships lifted (0.78/1.18) after the first pass came out too murky.
- Setup: menu `TimeKiller/Setup/26` (furniture) + `27` (armory wardrobe).

### Agent tooling (Claude's workflow, 2026-07-23)
Claude drives the editor programmatically instead of screen control:
- **unity-mcp bridge** (`com.coplaydev.unity-mcp` v10.1.0 pinned in manifest; project `.mcp.json` registers it for Claude Code at `http://127.0.0.1:8080/mcp`). 48 tools: execute menu items, run tests, read console, manage scenes/objects. Server runs inside the editor: Window → MCP for Unity.
- **PixelLab MCP** (`.mcp.json`, hosted at api.pixellab.ai/mcp) — purpose-built pixel-art generation (map objects, characters, 4/8-direction views, animations, tilesets). Auth via `PIXELLAB_API_TOKEN` env var (NOT in the repo — set your own from pixellab.ai). Output is quantized to the RF palette by `Tools/ArtPipeline` before it enters the game (first shipped assets: kitchen crates + sacks).
- **GameEye** (`C#/Editor/GameEye.cs`) — renders any world position through a URP camera to PNG (edit or play mode): programmatic screenshots for visual verification. Torch flicker only animates in play mode.
- **SmokeCheck** (`C#/Editor/SmokeCheck.cs`, menu TimeKiller/Test) — read-only scene integrity report (player rig, maniac, hiding spots + sprites, furniture, null sprites, camera rig) as JSON; the pre-push gate. Plain report instead of Unity Test Framework because the project has no asmdefs (UTF test assemblies can't reference Assembly-CSharp).
- **Automated playtests** (`C#/Testing/`, namespace `TimeKiller.Testing`) — `TestDriver` static cockpit (Possess/MoveTo/PressInteract/Status/Release) drives the player through **ScriptedInputSource**, a programmable `IInputSource` swapped in via the new `PlayerController.SetInputSource()` (the co-op/AI seam made official). **TestTelemetry** records bus events (hits, deaths, spotted, hides, min maniac distance) with zero hooks in gameplay code. Keyboard is suspended, never destroyed; `Release()` restores it.
- Editor prefs set for headless play: InteractionMode = NoThrottling, PlayerSettings.runInBackground — play mode must keep ticking while Claude works from the terminal.

## Planned

- `Player` — sanity
- `Maniac` — animation set from Nightmare Slashers pack (slicing setup), hiding-spot interaction, de-aggro polish
- Environment — darkness + player light (deferred by choice)
