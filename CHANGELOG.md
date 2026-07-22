# Changelog

Newest entries on top. Updated with every push to `main`.

## 2026-07-23 — Audio tension radar: the music is the threat detector

- **AudioDirector** (`C#/Audio`, Setup/24): one crossfading music layer at a time, priority Safe > Chase > Tense > Calm, driven purely by bus events. Patrol→Calm (5 creepy ambiences), Investigate→Tense (4 tracks), Chase/Attack→Chase (3 tracks, random per chase). The servant passage plays the safe-room theme when not actively chased (safe zones = config rects).
- Stings: random jumpscare on spotted (4s cooldown), Dark Riser the first time he hears you from Calm (running is audibly punished), death sting.
- All 18 music tracks + stings wired from the PSX Horror Music pack (royalty-free, credited). F1 overlay shows the current layer + track.
- User tuning: safe-room volume 0.4 → 0.28 (sanctuary whispers).
- Playtested — full radar tour in one run: Calm → riser+ambush → Chase (Track 2) → Tense on escape → Calm → second chase picked Track 3 (variety confirmed) → Safe in the passage.

## 2026-07-23 — Maniac v1.1: relentless chase + fair escape (playtest-driven)

Three fixes from the user's playtest, one design: he never stops — but every hit hands you a real escape window.
- **Unpushable:** body mass 1 → 400 (`ManiacConfig.bodyMass`) — the player can't shove him around; his own movement is unaffected (motor drives velocity directly).
- **No more post-hit nap:** the old stand-still-through-cooldown is gone. Swing → 0.35s recovery (`attackRecoverySeconds`) → straight back to full-speed chase; the 1.6s swing cooldown now runs DURING the chase (gated in ChaseState).
- **Adrenaline escape:** on being hit the player gets a 1.3× speed burst for 2.5s (`PlayerHealthConfig.adrenaline*`) — run 5.85 vs his 5.2: you outrun him briefly, he never stops coming.
- **Ghost-through:** after his hit lands, player↔maniac collision is off for 2.5s (`phaseThroughSeconds`) — an unpushable body must never pin a cornered player; collision restores only once separated (no depenetration pop).
- Playtested in-editor: chased through a death+respawn without pausing, speed readout 8.11 post-hit (shove+adrenaline), walked clean through his body from a corner.

## 2026-07-23 — CFXR effects integrated + teammate-checkout fix (Windows path limit)

- **Cartoon FX Remaster Free integrated** (`Outsource/JMO Assets`, 66 prefabs, free): PlayerHit recipe now fires CFXR2 Blood (Directional), PlayerDeath uses CFXR2 Blood Shape Splash, and a NEW layered PlayerDeathSoul recipe releases a soul (CFXR2 Souls Escape) on death. Our hand-made BloodBurst remains the automatic fallback when CFXR is absent (Setup/23 logs a warning instead of breaking).
- **Fixed: teammates couldn't clone/pull on Windows.** The Echo Chambers pack extracted with a double-nested 208-character path — past `C:\Users\...\` that exceeds the Windows 260-char limit, so git checkout failed mid-way ("cannot install the project") and left broken working copies. Folder flattened to `EchoChambersAmbience/Ambience + OneShots`, duplicate copy of all 15 files deleted; repo's longest path is now 144 chars. Teammates with a broken clone: discard local changes → pull (or re-clone fresh); optional safety: `git config core.longpaths true`; open with Unity **6000.4.10f1** exactly.
- Playtested: CFXR blood spray on hit, death splash + respawn cleanup.

## 2026-07-22 (later) — Health VFX: the screen IS the health bar + audio stash + centered camera

- **HealthVfx** (`C#/HealthVfx`, Setup/22): diegetic health, no UI. Bands: 3 HP clean · 2 HP subtle edge blood + red vignette · 1 HP heavy blood + desaturation + breathing = "next hit kills". Every hit: blood splatter flash (slams in at 118% scale) + Cinemachine camera shake + blood-splash SFX (pitch-jittered); death: impact sting.
- **Audio-visual heartbeat, one clock:** synthesized lub-dub (S1/S2 physiological model, `Tools/VfxPipeline/gen_heartbeat.py` — license-free) fires on the exact frame the visual systole peaks. Real BPM: 74 at 2 HP, 118 (tachycardia) at 1 HP. Dual blood layers counter-pulse with a 3% scale-breath; chromatic aberration + film grain ramp with the bands.
- Blood textures: OpenGameArt CC0, reprocessed via `Tools/VfxPipeline/process_blood.py` — radial edge mask (center stays playable), two-tone wet crimson.
- **Audio packs stashed** (`Outsource/Audio`): PSX Horror SFX + PSX Horror Music (both royalty-free, credited) and Echo Chambers ambience (**evaluation only** — license unverified, warning file inside; do not ship its sounds).
- **Camera centered:** CameraConfig lookAheadDistance 1.1 → 0 per user decision — the player now sits dead-center; look-ahead can return via the slider anytime.
- Cheat: F7 = take 1 hit (respects i-frames) for stepping through bands.
- Playtested (Claude, in-editor): band escalation, hit flash + shake, death cleanup, centered framing in all directions.

## 2026-07-22 (later) — THE MANIAC v1 + 3-point player health (first gameplay systems!)

- **Maniac** (`C#/Maniac`, Setup/20+21): the killer is in the game — patrol/investigate/chase/attack state machine, hearing (footstep loudness × 9-unit radius — walk quiet, run loud), sight (7 units, 140° cone aimed by movement, walls block via filtered linecast), breadcrumb-trail chase (follows the player's path through doorways, zero pathfinding), chase 5.2 vs player run 4.5 (design choice: faster than run; balanced by the generous 2.5s lose-sight valve). Patrols the whole wing loop — the servant passage is deliberately his blind spot. All tunables in `ManiacConfig.asset`.
- **Player health** (`C#/Player`, Setup/19): the 3-HP team decision is live — PlayerHitEvent (bus-decoupled) → −1 HP + red flash + shove away + 1.5s invulnerability; death v1 = respawn at spawn with full HP (placeholder rule until save/death design). Events: PlayerHealthChangedEvent / PlayerDiedEvent for future UI/audio.
- **CheatHotkeys** (Core, dev-only): F5 god mode, F6 refill — features register their own keys; shown in the F1 overlay.
- Maniac sprites: free maranza pack (TopDown Horror Characters — credited in README, commercial use author-approved), killer look = "Pacient stage_two" bandaged madman; 8-direction walk + idle stills + death clips generated from the sheet (rows CCW-from-Down = FacingDirection order).
- Fixed: respawn teleport silently overridden by Rigidbody2D interpolation (now body.position + SyncTransforms).
- Playtested (Claude, in-editor): full hunt loop — heard→investigate→spotted→chase→3 hits→death→respawn, plus cone blind-spot and lose-sight de-aggro all verified.

## 2026-07-22 (later) — Map v2 ground floor: kitchen, armory, library, servant passage + torches

- Map v2 authored ENTIRELY in CastleWing.ldtk (user-approved draft; gallery + undercroft levels drafted but parked by choice — `Tools/MapPipeline/mapv2_generate.py` re-adds them): **Kitchen** (south of Guardroom, corridor through its south wall), **Armory** (east of Guardroom), **Library** (east of Great Chamber), **Servant Passage** — hidden door in the Hall's south wall → long dark passage under the map → Kitchen. +412 floor cells, collision + camera zones regenerated (17 zones), all audited: 0 gaps, 0 collision-on-floor, BFS connectivity = every room reachable.
- Hall south collider split around the hidden servant door (approved; hand-tuned Y/height preserved, snapshot file untouched as pre-split record).
- Torch pass: kitchen/armory/library + their corridors lit (north-face placement matching v1 rooms); vertical connectors and the servant passage stay dark by design.
- Setup/18 hardened: camera-zone conversion now anchors on the Floor layer's min cell (same anchor as map alignment) — level growth in any direction can't desync the confiner; fixed a passage collision-gap bug found in Claude's playtest (connectivity check added to the pipeline).
- Tools/MapPipeline: map generator + validator + preview renderer committed (parked levels live here).
- Playtested (Claude, in-editor): Hall → servant passage → Kitchen → Guardroom → Armory, and Great Chamber → Library. New rooms are undressed — AI furniture props are the next step.

## 2026-07-22 (later) — LDtk-driven scene (side-by-side): map fully in CastleWing.ldtk

- CastleWing.ldtk is now the single source of truth: tiles + new **Collision IntGrid layer** (286 wall cells, 100% coverage rule — audited 0 gaps / 0 collision-on-floor before import) + **9 CameraZone entities** (the Cinemachine confiner zones, editable visually in the LDtk editor).
- New scene `Assets/Scenes/CastleWingLDtk.unity` (Setup/18): built entirely from the LDtk file — imported prefab auto-aligned to world coords, IntGrid colliders via WallIntGridTile (Grid type + CompositeCollider2D), full player pipeline (Setup 4→7: New_Leaf animations, footsteps, shadow), lighting, props/torches (shared with Setup/14 — no duplication), camera bounds parsed from the LDtk entities. Companion menu: "Restyle LDtk Map" for after reimports.
- Hall hand-tuning made durable (Setup/17): `HallColliderSnapshot.json` — export/re-apply the 6 hand-tuned hall collider boxes exactly; the snapshot is committed and re-applied automatically in the LDtk scene.
- Playtested (Claude, in-editor): full loop Hall→Guardroom→Great Chamber→Chapel→Hall — collision solid, doorways pass, camera confines per room, dark corridors unlit.
- SampleScene stays the main scene until the team switches; includes the user's manual prop tweaks (pillar collider + prop sorting orders) saved this session.

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
