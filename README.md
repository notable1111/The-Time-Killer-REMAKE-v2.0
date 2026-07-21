# The Time Killer REMAKE v2.0

A 2.5D survival-horror game built in **Unity 6000.4.10f1**.

**Genre:** survival / horror — hide & run (stealth, hiding spots, escape; combat is rare)
**View:** angled ¾ HD-2D — hand-drawn 2D sprites living in a lit 3D world
**Platform:** PC (Windows) · **Controls:** WASD move, Shift run
**Players:** single-player (2-player co-op planned later)

## Getting started (teammates)

1. Install [Git LFS](https://git-lfs.com) and run `git lfs install` once (needed for art/audio files).
2. Clone the repo, open the folder with Unity **6000.4.10f1** (exact version — Unity Hub will offer to install it).
3. Open `Assets/Scenes/SampleScene.unity` and press Play.

## Project rules

- All hand-written scripts live in `Assets/Resources/C#/<Feature>/` (namespace `TimeKiller.<Feature>`), with tunable values in ScriptableObject configs under `<Feature>/Configs/`.
- Downloaded third-party assets go to `Assets/Resources/Outsource/` (untouched) — modified copies go to `Assets/Resources/Assets/`.
- Scene objects are created by editor setup scripts (menu **TimeKiller → Setup**) — never wire scenes by hand if a setup script exists.
- `main` must always be in a working state: test in the Editor before pushing.
- See [ARCHITECTURE.md](ARCHITECTURE.md) for how the systems connect and [CHANGELOG.md](CHANGELOG.md) for what changed recently.
