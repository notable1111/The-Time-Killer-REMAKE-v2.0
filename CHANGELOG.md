# Changelog

Newest entries on top. Updated with every push to `main`.

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
