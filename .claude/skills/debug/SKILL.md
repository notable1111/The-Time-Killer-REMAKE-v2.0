---
name: debug
description: Debug a Time Killer bug by checking the known traps before theorizing. Use for any report that the maniac, player, navigation, or interaction is behaving wrong.
---

# Debug — Known Traps First

This project has a catalogue of bugs that have already been paid for once. Most
new "the AI is being stupid" reports are one of them wearing a new hat. Check
the catalogue before building a theory — a theory costs the user a round-trip,
the catalogue costs nothing.

## Steps

1. **Get evidence before hypothesising.** `read_console` for exceptions. For
   anything navigation-shaped, press **F3** in play mode. A wrong route and a
   wrong *map* look identical from outside and have completely different fixes.

   F3 **cycles** through registered bodies: off -> maniac -> bot -> player ->
   off. It does not show "the" map — it shows whichever body is currently
   selected, so cycle to the one you are actually debugging. Colours:
   - **red** — a real collider: wall or furniture
   - **grey** — cut off (unreachable)
   - **yellow** — walkable, but too tight for THIS body
   - **green** — comfortably open

   First press costs ~33k physics overlap queries (the grid is built lazily on
   first view), so expect a hitch. `PlayerNavDebug.Rebuild()` re-samples after
   the layout changes — the gate opening.
2. **Walk the catalogue below** and rule each entry in or out explicitly.
3. Only then form a hypothesis, and change one thing at a time.
4. **Append any new trap to this file** when a bug turns out to have a
   non-obvious cause. That is what makes this skill compound.

## Trap catalogue

### Navigation / pathfinding
- **Sampling only one tilemap layer.** Walkability must be sampled from ALL
  layers. Getting this wrong once produced a grid that was 71% void; a
  flood-fill check brought it to 0.
- **Grid alignment.** The node grid is 0.5 spacing and must be integer-aligned —
  thin walls sit on integer coordinates and are missed by a misaligned grid.
- **Null path freezes the agent.** Every pathfinding consumer needs an explicit
  no-path fallback. A frozen maniac usually means a null path, not bad AI.
- **Floor layer is walkable** is an invariant of the Catacombs map pipeline. If
  the floor tilemap is not marked walkable, the whole level reads as solid.
- **`bodyRadius` is not sampling `clearance`.** The agent's physical half-width
  and the clearance used when sampling walkability are different numbers.
  Conflating them lets wall-scraping survive a fix that looks correct.
- **The overlay may be answering about the wrong character.** Before
  `PlayerNavDebug` existed, the maniac was the only registered nav source, so
  F3 during a normal session painted HIS map — sample box 0.85 — while you
  walked a 0.55 capsule. 4.9% of floor the player can genuinely stand on
  showed red, and every one of those cells is merely *yellow* (tight) on the
  player's own map. The overlay was correct and the conclusion drawn from it
  was wrong. Always confirm which body is selected before believing a colour.

### Interaction
- **E is read by both `ClockRepair` and `PlayerHiding` at 2.2 range.** Clocks
  placed beside wardrobes produce accidental wardrobe entry while repairing.
  The bot hits this; a human hits it too. It is a design bug, not a bot bug.

### Test harness
- **A truncated batch is biased, not small.** A 75-run batch once died silently
  at run 11 (mid-run domain reload) and left a file that simply stopped. Read
  the HARNESS STATUS block `analyze.py` prints first, before any number.
- **Never average oracle profiles with honest ones.** `knowsEverything` runs
  answer a different question and `analyze.py` splits them deliberately.
- **Wedged runs are not losses.** Timeouts clustered at the same coordinate
  across different seeds are the harness failing, not the game being hard.

## The rule

"I think the problem is..." is worth less than one F3 screenshot or one console
read. Look first.
