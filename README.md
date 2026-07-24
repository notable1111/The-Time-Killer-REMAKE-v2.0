# The Time Killer REMAKE v2.0

A 2.5D survival-horror game built in **Unity 6000.4.10f1**.

**Genre:** survival / horror — hide & run (stealth, hiding spots, escape; combat is rare)
**View:** angled ¾ HD-2D — hand-drawn 2D sprites living in a lit 3D world
**Platform:** PC (Windows) · **Controls:** WASD move, Shift run
**Players:** single-player (2-player co-op planned later)

## Getting started (teammates)

> **Read [TEAM_SETUP.md](TEAM_SETUP.md) first.** It is a one-time setup and it is
> what prevents the two errors everyone hits: *"No 'git' executable was found"*
> and *"There are unresolved conflicts in the working directory"*. It also has
> the recovery steps if you are already stuck.

1. Install [Git for Windows](https://git-scm.com/download/win) — a GUI client alone is not enough, Unity needs `git` on PATH.
2. Install [Git LFS](https://git-lfs.com) and run `git lfs install` once (needed for art/audio files).
3. Enable Unity's YAML merge driver (see TEAM_SETUP.md step 3) — without it, pulled scenes merge into corrupt files.
4. Clone the repo, open the folder with Unity **6000.4.10f1** (exact version — Unity Hub will offer to install it).
5. Open a playable scene and press Play — `Assets/Scenes/CastleWingLDtk.unity` (castle wing) or `Assets/Scenes/Catacombs.unity` (catacombs). These two are the scenes in Build Settings; `SampleScene.unity` is an older hall kept for reference.

**Always close Unity before pulling.** Unity rewrites scene and `.meta` files
while it runs, which turns an otherwise clean pull into a conflict.

## Project rules

- All hand-written scripts live in `Assets/Resources/C#/<Feature>/` (namespace `TimeKiller.<Feature>`), with tunable values in ScriptableObject configs under `<Feature>/Configs/`.
- Downloaded third-party assets go to `Assets/Resources/Outsource/` (untouched) — modified copies go to `Assets/Resources/Assets/`.
- Scene objects are created by editor setup scripts (menu **TimeKiller → Setup**) — never wire scenes by hand if a setup script exists.
- `main` must always be in a working state: test in the Editor before pushing.
- See [ARCHITECTURE.md](ARCHITECTURE.md) for how the systems connect and [CHANGELOG.md](CHANGELOG.md) for what changed recently.

## Asset credits (dev assets)

- Player character: [New_Leaf — Top-Down 8-Direction Character](https://new-leaf.itch.io/top-down-8-direction-character-animated-sprite-pack) (free version, credit required)
- Maniac (killer enemy): [Maranza — TopDown Horror Pixel Art Characters Sprites](https://maranza.itch.io/assets) (free, credit required — commercial use author-approved)
- Previous dev character: [xzany — Adventurer 2D Top-Down](https://xzany.itch.io/top-down-adventurer-character) (kept in Outsource as fallback)
- Footstep sounds: [Kenney — RPG Audio](https://kenney.nl/assets/rpg-audio) (CC0)
- Horror SFX: [pabloalegria9 — Horror PSX Music & FX](https://pabloalegria9.itch.io/psxhorrorpack) (royalty-free)
- Horror music (chase/ambience/safe-room): [pabloalegria9 — PSX Horror Music Pack](https://pabloalegria9.itch.io/30freemusicpack) (royalty-free)
- Ambience (EVALUATION ONLY — license unverified, see LICENSE-UNVERIFIED.txt): [Echo Chambers — Free Horror Ambience & SFX](https://echochamberworks.itch.io/free-horror-ambience-sfx-sample-pack-echo-chambers)
