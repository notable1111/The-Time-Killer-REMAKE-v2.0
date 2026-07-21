# Architecture

How the systems of The Time Killer Remake connect. Update this file whenever a system is added or changed.

## Principles

- **Heavy architecture, small pieces:** single-purpose components; every tunable number lives in a ScriptableObject config (`C#/<Feature>/Configs/`), never hardcoded.
- **Event-driven:** systems talk through the Core event bus, not direct references. A system should compile even if the systems it talks about don't exist yet.
- **Input ≠ logic:** input reading is a separate layer from character/system behavior, so a second player (future co-op) or an AI can drive the same character code.
- **State machines** for anything with modes (player movement, enemy AI, game flow).
- **Editor setup scripts** (menu `TimeKiller/Setup/...`) create and wire scene objects — scenes are never hand-authored YAML.

## Systems

*(none yet — Core foundation is the first system to be built)*

## Planned

- `Core` — game bootstrap, event bus, service locator, debug overlay (F1) + dev cheat hotkeys
- `Player` — movement (walk/run), then stamina / health / sanity
