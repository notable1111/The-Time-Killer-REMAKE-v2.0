# Changelog

Newest entries on top. Updated with every push to `main`.

## 2026-07-24 — Team onboarding fixes: teammates could not open or pull the project

Three separate causes behind the errors teammates hit. All fixed in-repo; the
one-time machine setup each of them still has to do is in **TEAM_SETUP.md**.

- **"No 'git' executable was found"** — `Packages/manifest.json` depended on
  `com.coplaydev.unity-mcp` via a **git URL**. Unity resolves those by shelling
  out to `git` on PATH, and GUI clients (GitHub Desktop, Fork, Sourcetree) ship
  a private git that is not on PATH — so a teammate could clone but Unity could
  not resolve packages. The package was a solo dev bridge, not a game
  dependency, so it is **removed from the manifest**.
- **"There are unresolved conflicts in the working directory"** — `.gitattributes`
  had only `* text=auto`, so Unity's LF-written scenes were checked out as CRLF
  on Windows. Merely opening the project produced a whole-file diff on every
  scene, and two people with whole-file diffs conflict on pull even when nobody
  edited the same object. Unity YAML (`.unity/.prefab/.asset/.mat/.anim/...`),
  `.meta` and LDtk files are now `-text` (no normalization) and routed to
  `merge=unityyamlmerge`, so conflicts go through Unity's object-aware merge
  instead of a line-based one that silently produces corrupt scenes.
- **Unity template leftovers now ignored** — `Assets/Settings/`,
  `Assets/TextMesh Pro/`, `Assets/TutorialInfo/`, `Assets/Readme.asset`,
  `InputSystem_Actions`. Nothing references them (the URP assets the game uses
  live in `Assets/Resources/Assets/Rendering/`), every Unity install regenerates
  them differently, and that diff landed in the next pull as a conflict. Editor
  playtest screenshots and `*.bak` map backups are ignored too.

**Caught while reviewing the diff:** the same URP-template import had overwritten
`Assets/Scenes/SampleScene.unity` with the template's default scene — 114
GameObjects down to 3 — and given it a **new GUID**. Restored from HEAD, scene
and `.meta` together. Documented in TEAM_SETUP.md so it is recognised next time.

## 2026-07-24 (later 4) — Catacombs: a second, fully playable level, generated and proved

A whole new level built the way the castle *should* have been built: the map is generated from data, and every gameplay position is **derived from that same data** instead of hand-typed. Nothing can spawn inside a wall by construction.

**Map** — a new `Catacombs` level inside `CastleWing.ldtk` (42×41 cells, 709 walkable), drawn from the Rogue Fantasy Catacombs sheet. A crypt carved from solid rock: every non-floor cell is filled with rock tiles, so there are no black voids and the rooms read as excavated. 14 areas arranged as a **double loop** — stair_hall → nave → north_hall → loop_north → loop_east → loop_link → nave — never a dead-end tree, so a chase always has an exit.

**`Tools/MapPipeline/mapv3_catacombs.py`** — re-runnable generator (replaces its own level/tileset rather than appending duplicates). It emits three artifacts: the level itself, the `Catacombs.ldtkt` tileset export the Unity importer requires, and `CatacombsRooms.json` with **world-space** room rectangles + the full walkable-cell set. Tile picks were measured off a coordinate-labelled grid overlay of the sheet, not guessed — floor cols 19–22 × rows 21–23, wall face cols 17–21 × rows 17–19, rock mass cols 46–49 × rows 26–29.

**Correctness gates, in this order:**
- Generator refuses to write unless BFS from the spawn reaches all 709 floor cells.
- `validate_v2.py`: 0 collider gaps, 0 collision-on-floor (CastleWing unchanged at 1135 / 0 / 0).
- `Setup/32` in-engine audit: 0 leaks out of the walkable region, 0/709 unreachable, all 14 rooms still reachable with props solid.

**`Setup/30`** builds `Catacombs.unity` from the level (unpacks the prefab and drops the CastleWing level, so this scene holds the catacombs only — **re-run it after map edits; it does not auto-update**). Floor tilemap min cell is pinned to world (0,0), which is the same anchor `CatacombsRooms.json` exports against.

**`Setup/31`** places the escape loop by *calling* Setup/28/25/20/29 and then relocating what they produce onto derived positions — no fork, no parameterisation, castle scene untouched. 3 clocks (west_crypt / east_ossuary / loop_east), gate on north_hall's north wall, 6 wardrobes, 12-waypoint patrol ring with the maniac spawning at index 5 (far side from the player), 12 torches. Re-runnable.

**`CatacombsRooms.Anchor()`** finds a *real* wall segment: 3 cells of solid rock behind, clear floor on both flanks, checked against the actual floor set. It tries north → east → west and never south (south walls render an Overhead band that draws over anything standing there). This is what stopped a clock being placed in `west_crypt`'s north edge, which is actually the doorway into `cistern`.

**Bugs this pipeline caught before they shipped** — worth recording, because each one is invisible by eye:
1. Using the `Floor` layer as background fill broke the pipeline's "Floor layer == walkable" invariant and produced 494 phantom gaps. Rock mass moved to `Rug`.
2. LDtkToUnity needs an exported `.ldtkt` per tileset; a new tileset silently fails to import without one. Now emitted by the generator.
3. Wardrobes were being placed standing inside clocks — the reservation set was per-prop-type instead of shared.
4. Reserving only horizontal neighbours works for north walls but not east/west ones, where props stack vertically. Now a full 3×3 claim.
5. `validate_v2.py` never actually did the connectivity check `ARCHITECTURE.md` credited it with.

**Camera bounds bug (found by the user on first play, fixed same session)** — the level shipped unplayable: you spawned in `stair_hall` and the camera sat 25 units north in `north_hall`, so the player was off-screen with no way to tell where they were. Cause: camera zones were emitted **sized to the room rectangle** (stair_hall = 8×5), but `CinemachineConfiner2D` clamps the camera so the whole **viewport** (~16×7 world units) fits inside the shape. With every zone smaller than the viewport, the confiner parked the camera on `north_hall` — the only zone large enough — regardless of the player. Fixed by emitting a single map-wide zone (38×36, whole carved area + 2 cells). `Setup/32` now asserts camera bounds exceed the viewport at up to 2.4 aspect **and** that all 709 walkable cells fall inside them, so this cannot regress silently. Lesson: camera zones are sized against the **viewport**, never against the room.

**Playtest fixes (user feedback: "maniac is small, clock placed wrong on the wall, map looks empty")**
- **Maniac was knee-high.** `Setup/31` called `ManiacSetup` (20) but never `ManiacAnimationSetup` (21), so he kept the raw unsliced `stage_one.png` at Unity's default PPU 100 — 17x31 px became **0.17 x 0.31 world units**. Setup/21 slices `stage_two.png` into 32x32 frames at PPU 32 (1.0 units); a further 1.5x transform scale puts him at **1.50 x 1.50**, the player's height but broader. The capsule collider is divided by the same factor so his physical footprint stays 0.60 x 0.60 — scaling it too would have made him 0.90 wide, exactly the WalkabilityGrid clearance, and he would have snagged on every corner. Rule: **never call Setup/20 without Setup/21.**
- **Clock floated up the wall.** Props anchored at cell + 0.45, so a 2.33-unit clock ran from +0.31 to +2.64 — three quarters of it above the floor cell. Anchors now sit at cell + 0.12, giving a full unit on the floor.
- **Map was bare** (2 decorative tiles in the whole level). Added a decoration pass, all deterministic so regeneration is reproducible: a second cobble patch mixed into **30%** of floor cells to break up the repeating 4x3 tile, a chains-and-skulls wall variant on **22%** of wall columns, and **19 sarcophagus niches** set into the chamber walls (3 rows tall, only where there is genuinely 3 cells of rock behind, so a tomb can never be carved into a doorway). Niche cells are exported in `CatacombsRooms.json` and reserved before gameplay placement, so a clock never stands inside a tomb.

**Playtest** — 191 FPS, maniac patrols and pathfinds through the new geometry, nav grid 2498/7920 walkable (31.5%, flood-fill correctly discarding the rock interior), HUD `CLOCKS 0/3`, zero runtime errors.

## 2026-07-24 (later 3) — The escape loop: fix the clocks, open the gate, get out

The game now has a win condition *and* a lose condition — a run can be finished, and it can be ended.

**Objectives** (`C#/Objectives/`, namespace `TimeKiller.Objectives`) — the DbD-style core loop: three broken clocks scattered across the map, each repaired through a **SPACE skill-check** mini-game (a marker sweeps a bar; hit the green zone for progress, miss for a small penalty plus a quiet noise the maniac may hear). Fix all three and the exit gate unlocks; reach it and you escape. `ClockObjective` (broken/fixed sprites + green Light2D + a static registry so spawn order never matters), `ClockRepair` (player-side mini-game, reads the new `IInputSource.SkillCheckPressed`), `ObjectiveManager` (tally → `AllClocksFixedEvent` → win), `ExitDoor`, `ObjectiveHUD`, `ClockConfig` (SO). Menu `Setup/28`.

**Hand-placed on the real map** — clocks sit in the three far corners, each beside a wardrobe: chapel NW `(-7, 29.45)`, kitchen S `(37, -6.5)`, library NE `(46.5, 20.8)`. The gate is on the west corridor's north wall `(-3, 6.45)` — visible-but-locked from spawn, so you see the way out in the first seconds and then have to cross the map three times. Every spot verified clear of walls, furniture and corridor mouths. Placement is protected the same way as HidingSpots/Furniture: Setup/28 refuses to rebuild if `Clocks` exists.

**Real gate art** — PixelLab (`create_map_object`, side view, 128×192): an iron-banded oak double door in a weathered stone arch, replacing the grey placeholder quad. The open state was composited from the locked sprite's arch + the generated swung leaves + a baked moonlit void, so **both states share a pixel-identical frame** — opening reads as doors moving, not the gate morphing. 64 PPU → exactly the 2-cell wall segment it hangs on. The open gate swaps to an **unlit** material so the night beyond glows on its own.

**Endgame audio** — the biggest moment in the game is no longer silent:
- The unlock fires a deliberately **non-positional** deep impact: the whole castle hears the gate give way.
- The open gate runs a looping **3D wind beacon** (linear rolloff, ~26 units) so you can navigate back to it by ear across a dark map.
- New **Endgame music layer** in AudioDirector, ranked `Chase > Endgame > Safe > Investigate > Mystery > Dread`, latched by `AllClocksFixedEvent`. Plus an escape sting on `GameWonEvent`.
- **The maniac hears it too.** New Core event `WorldNoiseEvent{Position, Loudness, AlwaysHeard}` — noises made by the world rather than by the player's feet. `ManiacPerception` subscribes alongside footsteps; `AlwaysHeard` skips his hearing radius, so he learns where the exit is and starts heading over. Verified: he walked from (31, 24) all the way to the gate. The final stretch is now the tensest part of the run.

**Run flow** (`C#/Core/GameFlow.cs` + `RunEndScreen.cs`, menu `Setup/29`) — **death now ends the run** (`respawnOnDeath` off; flip it back on for forgiving map-testing). `RunPhase{Playing,Won,Lost}` + `RunEndedEvent`: Core owns the run timer, freezes `Time.timeScale`, and handles **R** (scene reload) / **Esc** (quit). Core stays ignorant of clocks and maniacs — features *publish* the ending (ObjectiveManager on the win, PlayerHealth on the last hit point), and the clock tally reaches the screen through an optional `GameFlow.ProvideSummary` hook. The uGUI end screen fades on *unscaled* time (the game is frozen) and shows the headline, `survived m:ss · X / N clocks fixed`, and the restart prompt. Setup/29 also registers the scene in Build Settings — `LoadScene` can't find an unlisted scene, so restart would silently fail without it.

Verified live: real killing blow → `Lost`, no respawn; escape → `Won`; restart → scene reloaded with clocks 0/3, full health, gate re-locked, timeScale 1, endgame music flag cleared. Console clean. Endgame track and escape sting are placeholder picks awaiting an ear-sort.

Shrank the committed audio ~12×. Re-encoded the 22 used tracks from WAV to OGG (Vorbis q5, ~134 kbit/s — transparent for game music) via ffmpeg, re-pointed `AudioSetup.cs` to `.ogg`, re-ran Setup/24 so AudioConfig references the OGGs (verified: all 22 clips resolve, 0 null), and removed the WAVs. Fresh clones now pull ~58MB instead of 687MB. (The original WAV objects still sit in LFS *history* from the earlier delivery commit — an optional history rewrite would reclaim that storage; the working tree and new clones are already small.)

## 2026-07-24 (later 2) — Maniac smart-search: belief map

Losing sight now triggers *reasoning*, not a rote sweep. `Maniac/PlayerBeliefMap.cs` is a probability field ("where did the player go?"): seeded at the last-seen spot and biased forward along the direction the player was fleeing (`ManiacPerception.LastSeenDirection`), it spreads along walkable corridors, and — the key move — **collapses to zero wherever he looks and doesn't find you**, so he never re-checks cleared ground. `SearchState` paths (via the A* navigator) to the highest-probability cell, scans, then moves to the next likeliest; the brain still decides *when* to give up. Pure (walkability + line-of-sight are delegates) → unit-tested (collapse-on-look, flee bias, never-into-walls); live lifecycle verified (seeded → hunted toward the flee spot → belief collapsed → gave up to Patrol).

## 2026-07-24 (later) — Audio pack delivered via Git LFS

The horror music the tension radar uses is now actually in the repo. Trimmed the 1.6GB Horror Sounds pack: removed macOS junk + the unused Fantasy pack (~73MB), and committed the **22 tracks the game references** (687MB) via **Git LFS** (`*.wav` already LFS-tracked). The ~817MB of unauditioned tracks stay gitignored/local for future layer sorting. AudioConfig's clip references now resolve on a fresh clone. Follow-up: re-encode the committed WAVs to OGG (~10× smaller) once an audio encoder is available — 687MB brushes the free LFS tier.

## 2026-07-24 — Maniac AI overhaul + escape balance + audio wiring

**Maniac AI** — he stops being predictable and stops getting stuck:
- **Utility-AI brain** (`Maniac/ManiacBrain.cs`): replaces hard-coded state transitions with weighted scoring — every frame each behavior (Patrol/Investigate/Search/Chase) gets a utility from considerations (line of sight, time-since-seen, noise recency × proximity) and the highest wins, with a stickiness bonus against flip-flop. Decisions now *emerge* from competing scores (e.g. a fresh noise pulls him off a search). States are pure behaviors; the brain owns transitions. Attack swing + the Outlast wardrobe-march stay reactive. Pure `Score()` is unit-tested (7 scenarios). F1 overlay shows live P/I/S/C scores. Tunables in `ManiacConfig` "Brain".
- **Custom grid A\* pathfinding** (`C#/Navigation/`: WalkabilityGrid + GridPathfinder + ManiacNavigator): he now routes *around* walls/pillars instead of grinding into them. Patrol/Investigate/Search/wardrobe-march path around obstacles (chase keeps the breadcrumb trail). Grid samples the physics world at 0.5-unit **integer-aligned** nodes (this map's wall colliders are thin strips on integer coords — a coarse grid missed them), ALL layers (walls live on several), binary-heap A* + line-of-sight string-pull. ~0.2ms/query, clean routes to all 12 patrol waypoints.
- **Search behavior** (`SearchState`): losing sight no longer snaps him back to patrol — he sweeps your last-seen spot + nearest waypoints, scanning his sight cone at each, before giving up. The servant passage stays off the search route (still a real blind-spot escape).

**Post-hit escape balance** (`PlayerHealthConfig`): you can actually get away now. Invulnerability 1.5→2.2s (was *shorter* than the burst — he re-hit you), adrenaline 1.3×/2.5s→1.55×/4s (run 6.98 vs his chase 5.2 = +1.78/s for 4s ≈ break-sight-and-corner distance), shove 8→11.

**Dev tool** (`Camera/DebugManiacCam.cs`): press **P** to snap the camera to the maniac, P again to return — editor/dev-only, for watching his behavior.

**Audio** — the tension radar upgraded to the new Horror Sounds pack, re-sorted by the user's ear: new **Mystery/Approach** layer added between Dread and Investigate (driven by hand-placed `mysteryZones`); layers renamed Dread/Investigate (was Calm/Tense); tracks wired via Setup/24 from the pack, stings still on the PSX pack. NOTE: the 1.6GB Horror Sounds pack is **gitignored** (wired in code, kept local, delivered separately — trim + Git LFS follow-up); AudioConfig references resolve once the pack is present.

## 2026-07-23 (later) — Workflow upgrade: Claude fully integrated with the editor

Research-driven tooling pass (verified deep-research report): Claude now works in the editor programmatically instead of screenshot-clicking.
- **unity-mcp bridge** (CoplayDev v10.1.0, MIT, pinned): 48 editor tools over HTTP — menu execution, console reading, scene/object management, tests. Registered for Claude Code in the project `.mcp.json`.
- **GameEye** (Editor): render any world position to PNG through the real URP pipeline — programmatic screenshots, edit or play mode, plus objective image metrics (luminance).
- **Automated playtests** (`C#/Testing` + `PlayerController.SetInputSource()`): TestDriver possesses the player through ScriptedInputSource (the co-op/AI input seam), walks waypoints, presses E, reports JSON status; TestTelemetry records bus events. First live run: walked spawn→hall wardrobe, got spotted 18×, hit 6×, died 2×, then hid successfully in the user's hand-placed wardrobe — all hands-free.
- **SmokeCheck** (menu TimeKiller/Test): read-only scene integrity gate (rig, spots, sprites, camera) — run before every push.
- **PixelLab MCP** (hosted): pixel-art generation service wired into `.mcp.json`; auth via `PIXELLAB_API_TOKEN` env var — teammates set their own, no secrets in the repo. Acceptance test shipped: kitchen crates + sacks regenerated (the old palette-quantize red tint is gone), processed through the RF-palette pipeline, verified in-game via GameEye.
- Editor settings for headless play: InteractionMode NoThrottling + runInBackground (play mode keeps ticking while Claude drives from the terminal).
- Housekeeping: `__pycache__/` gitignored.

## 2026-07-23 — Map v2 furnished (100%) + hiding feel pass

- **Furniture** (Setup/26): 28 AI-generated props dress the map-v2 rooms — kitchen (table, hearth w/ cauldron, shelf, chopping block, bench, barrel, firewood, sacks, crates, stool), armory (sword/spear racks, armor stand, shield, chest, anvil, training dummy, arrow barrel, grindstone), library (2 bookcase styles ×3, candle-lit desk, lectern, globe, tattered armchair, book stack, flickering candelabra, scroll table). Mixed colliders per team decision: big furniture blocks at its feet, small dressing is walk-through; all props Y-sort with the player + blob shadows.
- **⚠️ Furniture placement is HAND-TUNED territory** (same rule as HidingSpots): Setup/26 refuses to rebuild an existing `Furniture` object — drag props freely.
- **New art pipeline** `Tools/ArtPipeline/process_props.py`: room prop sheets (Recraft V4.1, magenta key bg, RF-palette hint) → key-out → component slicing → 16 PPU downscale → quantize to the RF Castle 57-color palette → approval contact sheet. Kitchen ships with a shadow-lift (gamma 0.78 / gain 1.18) after the first pass proved too murky. All 28 props user-approved before integration.
- **Armory wardrobe** (Setup/27): the one room without a hiding spot got a WardrobeA, added WITHOUT touching the six hand-placed spots (menu is additive + idempotent). Hide & run now works across the whole map.
- **Hiding feel pass:** every HidingConfig value is a real `[Range]` slider, read live each frame (tune in Play Mode, values persist); new F1 line `HideVfx: dist/bpm/vol/slat` for tuning sessions; interactRange 1.8 → 2.2 (measured to sprite center — 1.8 was borderline standing at the wardrobe's foot). Heartbeat/slat values await the user's ear-tuning session.
- Playtested (Claude, in-editor): kitchen table blocks movement, armory wardrobe hide w/ live readout, library candelabra flickers, spawn restored, console clean. Known nit: kitchen crates/sacks lean reddish after palette quantize; kitchen wardrobe (hand-placed) is reachable only from the corridor side.

## 2026-07-23 — HIDING: wardrobes complete the hide & run loop

- **Hiding** (`C#/Hiding`, Setup/25): press **E** near a wardrobe to slip in (invisible, intangible, doors shut behind you), E to step out. While hidden: dark slat overlay with a door-crack view + your heartbeat scales with his distance (60→140 BPM as he approaches — you HEAR how close he is).
- **The Outlast rule:** while hidden his sight can't find you — but if he had eyes on you within 1.25s of entering (`ManiacConfig.seenEnterWindow`), the spot is compromised: he marches to the wardrobe and drags a hit out of it (auto-eject into your shove/adrenaline/ghost-through escape kit).
- **Hidden camera:** the room confiner releases while hidden so the wardrobe is ALWAYS dead-center (fixes the off-center view the user caught); restores seamlessly on exit.
- Art: AI-generated wardrobes per the approved pipeline — style A flat-top + style B gothic crown, closed/ajar states, processed to 32×48 px in the RF Castle palette. Six spots seeded across the wing.
- **⚠️ Placement is HAND-TUNED territory:** the user drags wardrobes where he wants them — Setup/25 PRESERVES an existing HidingSpots object (rewires player/VFX only). Same protection class as the hall colliders. (One user position restored at 1.67/9.6; the rest re-placed by hand after an overwrite mistake — rule now enforced in code.)
- Interact input added to IInputSource (E) — the seed of the future doors/items interaction system. Fix: unknown player states no longer emit phantom footstep noise (explicit Walk/Run animation gate).
- New F1 overlay line: "Hide" (state + nearest wardrobe distance). Playtested: hide/exit, centered view, spot occupancy, maniac blindness while hidden.

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
