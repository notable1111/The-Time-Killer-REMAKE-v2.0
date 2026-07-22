# Changelog

Newest entries on top. Updated with every push to `main`.

## 2026-07-22 (later) — Wing playtest fixes: loop closed, 100% collider coverage

- Loop bug fixed: the C→D corridor was 2 tiles short of the Chapel (dead end) — extended, opening auto-raised; verified by running Chapel→Great Chamber in one pass.
- Walk-through-wall bug fixed: collider exclusion zones removed — EVERY wall cell adjacent to floor now gets a merged collider box (verified: capsule blocks flush at wall boundaries).
- Door-split guard hardened: a hall wall side with 2+ collider boxes is never re-split (protects hand-tuned values on rebuilds).
- New tool: Python collider-audit renderer (green/red coverage overlay on the map preview) — coverage is now proven visually before Unity runs.
- West-corridor torch moved onto the wall (dark connectors stay unlit by design). LDtk twin regenerated in sync.

## 2026-07-22 (later) — Map v1: castle wing loop + LDtk pipeline

- Castle wing (Setup/14): LOOP Hall -> east corridor -> Guardroom -> dark corridor -> Great Chamber -> corridor -> Chapel -> dark corridor -> Hall west door. Generic wall-raising from the floor plan, per-cell merged collision, per-zone camera bounds, room dressing + torches. Hall's protected side colliders split around the two new doorways (approved; tuned values preserved).
- LDtk map pipeline adopted: LDtkToUnity 6.12.3 (OpenUPM). Maps/CastleWing.ldtk = the full wing authored as JSON (self-render preview workflow) + exported .ldtkt tilesets; imports clean. LDtk editor (ldtk.io) opens it for visual editing. Scene still runs on script tilemaps — LDtk-driven scene is the next step.
- Recovery tooling after a meta-edit mishap: Setup/15 (RGBA32 via TextureImporter API), Setup/16 (floor repaint + sprite re-link). Rule: never hand-edit .meta files.

## 2026-07-22 (later) — Cinemachine camera v2, wall texture patch, packages

- Packages: Cinemachine 3.1.2 + PrimeTween 1.4.11 (via npmjs scoped registry) — resolve automatically on pull.
- Camera v2 (Setup/12): Cinemachine rig — facing look-ahead (CameraTarget child of Player + CameraTargetDriver), hard confinement to the hall (CameraBounds polygon + Confiner2D), CinemachineConfigSync keeps ALL tuning in CameraConfig.asset (viewSize/smoothTime/lookAhead). Legacy CameraFollow disabled on Main Camera as fallback.
- Hall wall patch (Setup/13, tilemap-only — colliders untouched): north wall's empty black band and the south band now textured dark brick.
- Map materials decision: candidate packs reviewed vs our style — rejected (visible palette clash); map v1 will use our two Szadi packs. Furniture gap noted.

## 2026-07-22 (later) — Camera distance slider

- CameraConfig gains "Distance (zoom)" slider (1.5 near … 8 far, default 2.4) — tunable live in edit AND play mode; value persists (config asset, not scene state).
- CameraFollow applies the config distance every frame (ExecuteAlways); position-follow stays play-mode-only.

## 2026-07-22 (later) — Hall wall & collider detail pass

- Side walls and south band rebuilt as real brick (matching the north treatment) with pillar accent edges — no more black voids.
- Wall colliders aligned to the visible brick edges.
- Props: per-item colliders — each pot/barrel is its own small round obstacle with walkable gaps (Setup/11 builds these automatically).
- Final collider tuning done by hand in the scene (do not re-run Setup/9/11 on the existing hall — it would overwrite the manual tuning).

## 2026-07-22 — Environment era: 2.5D castle hall, lighting, camera

- Castle hall showcase room (RF Castle pack): layered tilemaps (floor/rug/wall face/decor/overhead-over-player), arched doorway, stained-glass window, banner, pillar-colonnade side walls, animated torches. Menu: Setup/9. **Map is WIP — more rooms/layout to come.**
- Realism pass (Setup/11): URP switched to 2D Renderer, Y-axis depth sorting (hide behind props!), dark ambient + flickering torch lights (FlickerLight2D) + player glow, collidable Y-sorted props (pillars, barrels, pots) with contact shadows, gradient wall-base shading.
- Follow camera v1 (C#/Camera, Setup/10): smooth-follow, tunable in CameraConfig.
- Catacombs: free Rogue Fantasy Catacombs pack + room builder (Setup/8) — retired as test space, kept for future underground levels.
- Player concept art (watchman) archived in Assets/Characters; AI sprite pipeline paused — final character will be sourced manually by the team.

## 2026-07-22 — Core foundation + player movement

- `C#/Core`: GameBootstrap (auto-boots before first scene), EventBus, ServiceLocator, StateMachine, DebugOverlay (F1: FPS + watches), sprite animation engine (SpriteAnimationClip/SpriteAnimator with frame events + phase-preserving swaps).
- `C#/Player`: full movement — WASD walk + Shift run (Idle/Walk/Run state machine), input abstracted behind IInputSource (co-op ready), Rigidbody2D motor with accel/decel, 8-direction facing, velocity-matched animation speed, footsteps on foot-contact frames (Kenney CC0 sounds; publishes PlayerFootstepEvent with loudness for future enemy hearing), blob shadow.
- Character: New_Leaf 8-direction pack active (idle+run all 8 directions; walk = slowed run). Adventurer pack kept as fallback. Credits in README.
- Editor setup menus 4–7 (create player, generate animations, footsteps + tight camera, blob shadow).
- Concept art: approved "young night watchman with lantern" player design + 6 direction renders in `Assets/Resources/Assets/Characters/Watchman/` (AI pipeline paused — final character will be sourced manually).
- Camera: tight horror framing (ortho 2.4); follow deliberately deferred.

## 2026-07-21 — Phase 0: project foundation

- Git LFS enabled for all binary asset types (images, audio, 3D, video, fonts) — teammates: run `git lfs install` once.
- Rewrote README with project overview, setup steps, and team rules.
- Added ARCHITECTURE.md (system map) and this changelog.
- Added Claude project skills in `.claude/skills/`: `/status`, `/push`, `/new-feature`, `/art`.
- Installed packages: Universal Render Pipeline (URP), new Input System.
- Project configured: Linear color space, URP pipeline assets, Input System active, product identity.
