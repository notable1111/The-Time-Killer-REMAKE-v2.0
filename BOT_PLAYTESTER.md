# Bot playtester — design + build log

**Status:** BUILT and verified on CastleWing (2026-07-25). One full run watched
end to end: the bot explored the castle, found all three clocks by looking at
them, repaired them, and walked out of the gate.

**Your two answers (2026-07-25):**

1. **Scene: CastleWing.** So there is no room-rectangle JSON to attribute deaths
   with. Instead each result row carries the raw `endPos` plus
   `nearestLandmark` — the closest clock / wardrobe / gate by name. `analyze.py`
   prints both the landmark ranking and a 4-unit grid bucket for heatmaps.
2. **No cheating — it must feel realistic.** So the "start with known clock
   positions" recommendation below was **dropped**. The bot now discovers clocks,
   wardrobes and the gate by line of sight (`BotMemory`), and explores the map
   to find them. Two profiles keep the cheat as a labelled control group:
   `oracle_average` and `oracle_expert` set `knowsEverything`, and `analyze.py`
   refuses to average them together with the honest ones.

## The problem it solves

Every balance number in this project is currently unverified. The maniac AI is
"needs playtest", the escape loop is "unplaytested", the catacombs are "awaiting
playtest". Not because anyone was lazy — because verifying one balance change
costs a human evening, so nobody runs the experiment twice.

The goal is to make a full playthrough cost **seconds**, so questions like *"is
4 clocks better than 3?"* or *"does +0.3 maniac speed break the level?"* get
answered by running it 200 times instead of arguing about it.

## What already exists (most of the seam is built)

This is cheaper than it looks, because the architecture already anticipated it:

| Piece | Where | What it gives us |
|---|---|---|
| `IInputSource` | `C#/Player/IInputSource.cs` | The player reads input through an interface. A bot is just another implementation — **gameplay code needs zero changes.** |
| `ScriptedInputSource` | `C#/Testing/` | Programmable controller: `Target`, `Run`, `QueueInteract()`, `QueueSkillCheck()` |
| `TestDriver` | `C#/Testing/` | Static cockpit: `Possess()` / `MoveTo()` / `Release()` |
| `TestTelemetry` | `C#/Testing/` | EventBus recorder — footsteps, hits, deaths, hides, spotted, heard, `MinManiacDistance` |
| `GridPathfinder` + `WalkabilityGrid` | `C#/Navigation/` | A* the maniac already uses. The bot can reuse it as-is. |
| `RunEndedEvent { Won }` | `C#/Core/GameFlow.cs` | The exact ground-truth signal a batch run needs |
| `GameFlow.Restart()` | `C#/Core/GameFlow.cs` | Full scene reload — "the only reset that can't leak stale state" |
| `ObjectiveManager` | `C#/Objectives/` | `Total` / `FixedCount` / `AllFixed` |
| `CatacombsRooms.json` | `Assets/.../Maps/` | Room rectangles — lets us attribute a death to a **named room** |

## The five gaps

1. **`ScriptedInputSource` walks into walls.** `Update()` steers straight at
   `Target` via `delta.normalized`. On any non-convex route it grinds along a
   wall forever. It needs a waypoint queue fed by `GridPathfinder`.
2. **The bot physically cannot repair a clock today.**
   `ScriptedInputSource.QueueSkillCheck()` exists but `TestDriver` never exposes
   it — there is no `PressSkillCheck()`. One missing method blocks the entire
   win condition.
3. **No goal logic.** Nothing decides *pick a clock → walk → repair → next →
   exit*, or *the maniac is close, break off and hide*.
4. **No batch loop.** One run at a time, driven by hand.
5. **`Time.timeScale = 0` on run end.** `GameFlow.OnRunEnded` freezes time. A
   batch runner that waits on scaled time deadlocks there forever.

## Architecture

Four new files, all under `C#/Testing/`, all removable — delete the folder and
the game is untouched, because nothing in gameplay code references them.

```
Testing/
  Configs/BotProfileConfig.cs   (SO)  the skill model — see below
  BotPath.cs                          A* waypoint follower; feeds ScriptedInputSource.Target
  BotPilot.cs                         the goal logic (a StateMachine, like the maniac's)
  BatchRunner.cs                      run N times, record, reload, aggregate
  Editor/BotPlaytestWindow.cs         Setup/33 — pick profile + run count, press Go
```

`BotPilot` is a small state machine mirroring `ManiacStates`:

- **Repair** — path to the nearest unfixed `ClockObjective`, `PressInteract()` in
  range, then time `SkillCheck` presses against `ClockRepair.Marker`.
- **Escape** — once `ObjectiveManager.AllFixed`, path to the `ExitDoor`.
- **Flee** — maniac within `panicDistance`: abandon the clock, path *away* from
  him, biased toward the nearest `HidingSpot`.
- **Hide** — `PressInteract()` at the spot; stay until `MinManiacDistance` grows.

## The most important design decision: the bot must be allowed to be bad

The skill check is deterministic and readable from code:

```csharp
Marker = Mathf.PingPong(sweepT, 1f);                              // ClockRepair.cs:62
bool hit = Mathf.Abs(Marker - ZoneCenter) <= config.zoneWidth * 0.5f;
```

So a naive bot hits **100% of skill checks, forever**. That bot would report the
game is easy and every tuning decision made from its numbers would be wrong. It
would be a broken instrument that looks like it works.

`BotProfileConfig` therefore models a *player*, not an oracle:

| Field | Meaning | Novice | Average | Expert |
|---|---|---|---|---|
| `reactionTime` | delay before reacting to what's on screen | 0.35s | 0.22s | 0.12s |
| `aimError` | random offset added to the perceived marker | 0.12 | 0.06 | 0.02 |
| `panicDistance` | how close the maniac gets before fleeing | 3.0 | 5.0 | 7.0 |
| `hideBias` | how strongly they prefer hiding over running | 0.3 | 0.6 | 0.5 |
| `routeKnowledge` | 0 = wanders toward the goal, 1 = optimal A* | 0.4 | 0.75 | 1.0 |

`routeKnowledge` is what stops the bot from being clairvoyant: below 1.0 it
paths to a random reachable cell *in the general direction* of its goal before
re-planning, which is what a person who doesn't have the map memorized does.

**The number that matters is not "does the bot win" — it's the spread between
Novice and Expert.** If both win 95% the level is trivial; if both lose the
level is unfair; a healthy horror level is roughly Novice 20–35%, Expert 65–80%.

## Determinism

Every run gets an explicit seed: `Random.InitState(seed)` at run start. This
matters because `ClockRepair.NewZone()` calls `Random.Range` unseeded today, so
two "identical" runs currently diverge. With a seed, **any interesting failure
can be replayed exactly** — a run that died in `west_crypt` at 0:42 can be
watched at 1x speed instead of guessed at.

## Time acceleration, and why it needs a guard

Runs get accelerated with `Time.timeScale`, but that is not free: physics runs
on a fixed timestep, and perception/pathfinding tick on `Update`. Above some
factor the maniac effectively gets dumber, and the win rate becomes a measure of
the accelerator rather than the game.

**Rule: the batch runner refuses to trust a speed it has not validated.** It
emits a matched-seed 1x control block alongside the fast one, and `analyze.py`
compares them. I expect ~4-6x to be safe and ~10x not to be — but that is a
guess, and the guard is there precisely because it is a guess.

**The first version of that guard was itself broken** (fixed 2026-07-25, before
any real batch ran). It compared win rates and flagged a gap of more than 5
points. Win/loss is binary, so a 15-run control carries a ~13-point standard
error: a *perfectly honest* accelerator produces gaps well past 5 points from
sampling noise alone, and the check would have condemned it nearly every time.
Verified against a synthetic honest accelerator — a 13-point gap that the old
rule called UNTRUSTWORTHY.

The check now leans on **matched-seed pairs of continuous metrics**, which carry
far more information than a binary outcome and move long before a win flips. On
the same seed, an accelerator that is quietly making the maniac dumber shows up
as: `minManiacDistance` up, `spotted` down, `hits` down, `clocksFixed` up. A
sign test asks whether those move together; win rate is still printed, but with
a Wilson interval and an explicit note that overlapping intervals are *absence
of evidence, not evidence of agreement*. `runSeconds` is printed unscored on
purpose — a dead bot and a winning bot both produce a short run.

## Output

One JSON object per run, appended to
`Tools/Playtest/results/<timestamp>.jsonl` (outside `Assets/`, so Unity does not
import it):

```json
{"seed":1041,"profile":"average","scene":"Catacombs","won":false,
 "runSeconds":97.4,"clocksFixed":2,"clocksTotal":3,
 "deathPos":[12.5,8.1],"deathRoom":"west_crypt",
 "spotted":4,"heardNoise":11,"hides":2,"minManiacDistance":0.71,
 "skillChecks":{"attempted":38,"hit":29}}
```

`deathRoom` comes from intersecting `deathPos` with `CatacombsRooms.json` — the
same room rectangles that carved the floor. That gives the report every level
designer actually wants:

```
west_crypt      41% of deaths   ← the corridor mouth is a trap
east_ossuary    22%
north_hall      18%
```

A Python analyzer (`Tools/Playtest/analyze.py`) does the aggregation, matching
the existing `Tools/MapPipeline/` style — offline, re-runnable, no Unity needed.

## Pre-registration: the reading, committed before the numbers landed

Written 2026-07-25 while the first batch was still running, on purpose. Once
results are on screen it is nearly impossible not to read a story into them —
"novice 25%, expert 40%" *looks* like skill being rewarded, and at 20 runs per
profile it is equally consistent with the two being identical. `power.py`
computes what this design can actually resolve, scoring the decision rules
`analyze.py` really applies rather than a textbook test we never run:

| true novice → expert | spread | P(verdict = "healthy") | P("flat") | P("too hard") |
|---|---|---|---|---|
| 25% → 70% (the target) | 45 | **97%** | 3% | 0% |
| 30% → 50% | 20 | 57% | 37% | 6% |
| 35% → 45% | 10 | 31% | 55% | 13% |
| 40% → 40% (skill irrelevant) | 0 | **13%** | 61% | 24% |
| 5% → 15% (brutal) | 10 | 2% | 0% | **98%** |

**Minimum reliable spread: ~30 points.** Below that a real difference is more
likely to be missed than found.

So, decided in advance:

1. **A "too hard" verdict is trustworthy** and we act on it. It keys off the
   expert rate alone — one proportion, not a difference — and it is 98%
   reliable in exactly the regime the first ten runs point at (10–30% wins).
2. **A "flat" verdict is NOT evidence that skill goes unrewarded.** It is the
   single most likely output whenever the true spread is under 30 points,
   including when skill matters a great deal. It means *run more*, nothing else.
3. **A "healthy" verdict gets a 13% asterisk.** That is how often these rules
   say "healthy" when skill is genuinely irrelevant. Do not celebrate it without
   a second batch.
4. **Any spread under 30 points is not actionable** at this n, in either
   direction. Not a tuning signal. Not a regression. Noise.
5. **Instrument failures bias us toward 2 and 3.** Every wedged run replaces a
   coin flip with a guaranteed loss, which shrinks the observed spread toward
   zero. The first batch ran at ~20% wedged, so its spread is an *underestimate*
   and a "flat" reading from it is close to meaningless.

The good news in that table: the design is well powered for the two questions
actually on the table — *is this level in the healthy band* (97%) and *is it
brutal* (98%). It is weak only in the middle, and the middle is not where the
early data sits.

## What this will NOT tell us

Worth being explicit, so the numbers don't get over-trusted:

- **Whether the game is scary.** It measures difficulty, not dread. Idea #3
  (clocks setting the tempo) would still need your ears.
- **Whether the maniac reads as intelligent.** A bot dying to a dumb-but-fast
  maniac produces the same row as dying to a clever one.
- **Anything about readability** — the bot's eyes are a distance check plus a
  linecast. A clock that is hard to *notice* but easy to *see* still gets found.
  (It does now measure whether a clock is findable at all, which the original
  known-positions design could not.)

It answers *"is this beatable, how often, and where does it kill people"*. That
is the question currently blocking every other decision.

## What was actually built

All under `C#/Testing/`, all removable — delete the folder and the game is
untouched, because no gameplay file references any of it. **Zero gameplay files
were modified.**

| File | Role |
|---|---|
| `Configs/BotProfileConfig.cs` | the skill model (+ `knowsEverything`, the labelled cheat) |
| `BotPath.cs` | A* waypoint follower over the maniac's own `WalkabilityGrid`, with a stuck detector |
| `BotMemory.cs` | **the honesty layer** — what the bot has SEEN; coarse exploration grid |
| `BotPilot.cs` | Explore / Repair / Flee / Hide / Escape state machine + the fallible skill check |
| `BatchRunner.cs` | N runs unattended, seeded, JSONL out, survives the scene reload |
| `Editor/BotPlaytestWindow.cs` | `TimeKiller/Setup/33`, creates the 5 profiles, runs the batch |
| `TestDriver.PressSkillCheck/BotStart/BotStop` | the missing methods + a manual cockpit |
| `Tools/Playtest/analyze.py` | the report |

### Two things the first live run found

Both were the harness doing its job before a single batch had run.

1. **The bot reported 0 hits out of 16 skill checks while the clock was visibly
   being repaired.** `QueueSkillCheck()` only sets a flag; `ScriptedInputSource`
   turns it into a press next frame and `ClockRepair` reads it the frame after
   that, depending on script execution order. Scoring on the next tick measured
   nothing. The scorer now waits for the clock's progress to actually move.
   *A broken instrument that looked like it worked — exactly the failure this
   design was written to avoid, in the place nobody looked.*
2. **The bot fixed all three clocks, walked to the gate, and did not win.** The
   win trigger starts at y 6.45; the bot's feet collider topped out at 6.41 —
   four centimetres short. It stops on the gate's coordinate; a human keeps
   holding W and walks *through* the threshold. `EscapeState` now does the same.
   Worth knowing this is only 0.15 units of margin for a real player too.

### Known quirk it will keep reporting

`accidentalHides` counts times the bot pressed E at a clock and climbed into a
wardrobe instead. `ClockRepair` and `PlayerHiding` both read the same
`InteractPressed` flag at the same 2.2 range, and every CastleWing clock was
hand-placed beside a wardrobe. A human hits this too. Flagged separately.

## The run-11 death — post-mortem (2026-07-25)

The first 75-run batch stopped after 10 runs. The results file simply ended;
nothing in it said it had not finished. This is the reconstruction from
`Editor.log`, not a guess — every claim below has a line number behind it.

**What happened**

| time | event |
|---|---|
| 00:30 | `BatchRunner.cs` was edited and saved to disk |
| 00:31:11 | the batch launched — from the **stale, already-loaded assembly**, because Unity only auto-refreshes when the editor regains focus |
| runs 1–10 | clean: **zero exceptions** in the entire block |
| ~00:36 | an AssetDatabase refresh fired, noticed the pending change, and requested compilation: `[ScriptCompilation] Requested script compilation because: AssetDatabase observed changes in script compilation related files` |
| — | Unity performed a **synchronous domain reload while in Play Mode** |
| run 11 | never happened |

**Why that is fatal.** A domain reload destroys all managed state. It killed
`BatchRunner`'s coroutine and reset its static `Instance`, so no further row was
ever written and nothing announced it. The scene's GameObjects survived as
native objects, but every field that is not Unity-serializable came back
**null** — `PlayerController.Input` is an interface field, `BotPilot.memory` is
a plain C# object — and `Awake()` does not re-run on an object that already
exists. So four components dereferenced null every frame, forever:

```
PlayerController.Update:75    Facing.UpdateFromInput(Input.MoveInput)
PlayerHiding.Update:56        controller.Input.InteractPressed
ClockRepair.Repair:58         player.Input.InteractPressed
BotPilot.Update:108           memory.Observe()
```

**498,704 NullReferenceExceptions and a 190 MB `Editor.log`**, until Play Mode
was stopped by hand. The measured split is what makes this airtight: 0
exceptions during runs 1–10, all 498,704 after the reload.

**The batch did not die of anything in the game. It died of its own source file
being recompiled underneath it.**

### The fix — three defences, because they fail differently

| # | defence | where |
|---|---|---|
| 1 | **Prevent** — `LockReloadAssemblies` + `DisallowAutoRefresh` for the batch's lifetime, so a compile request is *deferred*, not executed | `BatchGuard.Hold/Release` |
| 2 | **Prevent at source** — the window flushes the AssetDatabase and refuses to enter Play Mode while a compile is pending, so a batch never starts on a stale assembly | `BotPlaytestWindow.Launch` |
| 3 | **Record** — if a reload happens anyway, write an explicit `{"aborted":true,"reason":"domain_reload","atRun":11,...}` row *before the domain dies* | `BatchGuard.OnBeforeReload` |
| 4 | **Contain** — after such a reload, stop Play Mode immediately; the scene is unrecoverable and every further frame is pure spam | `BatchGuard.OnAfterReload` |

Defences 1 and 3 were verified against a **real forced domain reload**, not a
simulation: with the lock held, `RequestScriptReload()` did nothing; on
`Release()` the same deferred reload fired at once and wrote the abort row
naming run 11. There is also a `TimeKiller/Setup/34` escape hatch, because a
leaked assembly lock silently stops the editor compiling and that is a horrible
thing to debug.

### The deeper bug: failures that looked like data

The reload was the proximate cause, but it exposed something worse. The harness
had **no way to say "I failed"** — every problem came out shaped like a game
result. A run whose preconditions were broken wrote no row at all (the batch
silently shrank); a wedged bot wrote an ordinary loss.

That matters more than the crash, because §"Pre-registration" already commits to
this: *every wedged run replaces a coin flip with a guaranteed loss, which
shrinks the observed spread toward zero*. A silent instrument failure does not
add noise — it biases every profile toward "flat", which is already the most
likely wrong answer at this sample size.

So a run now ends in exactly one of five ways, and three are the harness
admitting fault rather than reporting a result:

| endReason | meaning |
|---|---|
| `escape` / `death` | real outcomes |
| `timeout` | ran out of clock — genuinely ambiguous |
| `stalled` | stopped moving, stopped repairing, not hidden — a wedge, caught in ~75s instead of burning the full 480 |
| `harness_error` | preconditions failed, or the scene threw during the run |

Plus an exception watchdog (20 thrown exceptions voids the run and aborts the
batch — one bad frame must never become half a million), a per-run preflight
check that `PlayerController.Input` is actually wired, and an `exceptions` count
recorded on otherwise-healthy rows so a slow-growing fault cannot hide.

`analyze.py` now splits the file into `meta` / `rows` / `faults`, prints a
**HARNESS STATUS block before any number**, and states what fraction of the
planned runs are usable. A truncated batch is not a small batch — it is a
biased one, because it stopped on whatever broke it.

One subtlety worth keeping: the stall detector treats **skill-check presses as
liveness**, not just movement and clock completions. A bot working a clock is
stationary by design, and a novice missing check after check can sit on one
clock a long time without `FixedCount` moving. Getting that wrong would have
voided precisely the runs where the bot is trying hardest.

### Verified: the harness now survives a batch

`results/2026-07-25_010141.jsonl` — 15 x `Bot_average` @ 6x, seeds 2000–2014,
run immediately after the fix:

```
15/15 runs completed      no abort, no harness error
0 NullReferenceExceptions in the entire batch (was 498,704)
clean teardown: play mode exited, assembly lock released, black box disarmed
```

Run 11 — the one that killed the previous batch — completed normally.

## Still open

- **The escape wedge — FIXED AND CONFIRMED LIVE 2026-07-25.**
  The truncated batch showed 2 wedges in 10 runs and they were disguised as
  timeouts. With the stall detector labelling them honestly: **6 of 15 runs
  stalled, 5 of them clustered at the same coordinate `(-4.5, 7.5)` beside the
  ExitDoor, every one with all 3 clocks fixed.** Overall, 47% of runs fix every
  clock and then fail to leave, against a single escape.

  That brackets the win rate between **7% (wedges as losses) and 40% (wedges as
  wins)** — a 33-point spread, wider than the ~30-point minimum this design can
  resolve at all. **No difficulty verdict from that batch is worth reading**;
  the pre-registration's rule 4 applies exactly.

  The suspicion on record was that the bot was *overshooting* — arriving at its
  steering target and standing **above** the trigger. **Measured against the live
  colliders, that was wrong on the axis.** Trigger is `x[-3.90..-2.10]
  y[6.45..7.65]`; the feet collider is `0.55 x 0.55` at transform `+(0,-0.35)`.
  The wedge sits **inside the trigger's y range and 0.325 west of its x range** —
  beside the door, never above it.

  Sweeping the player's real body over the area found **three** faults, all fixed
  in `EscapeState`:

  1. `exit.position + up * 0.8` = `(-3.00, 7.25)` is **inside solid wall**. Only
     the trigger's lower slice is standable (`y[6.55..7.05]`) — the upper half is
     the wall the gate hangs on. → now aims just inside the trigger's **near
     edge**, read from the live collider.
  2. A\* was routed **at the gate**, which is not on the grid: the grid bakes
     while the gate is still shut, so the opening reads solid and the path
     resolved to a **dead-end pocket 1.5m west, behind a wall**. → now paths to a
     **doorstep staging point** (`gate + down*0.45`) that *is* on the grid.
  3. `nav.Clear()` **disabled the stuck detector** at the one spot it was needed,
     turning a failed last metre into an infinite press. → the push now carries
     its own stall guard and re-approaches instead of grinding.

  **Measure with the gate OPEN.** The first pass of this analysis was misled by
  probing while `block` was still enabled, which hides the real approach and
  makes the door look walled off entirely. Verified in the open state: A\* reaches
  the staging point from the player start, both far clocks, and the old wedge
  coordinate; the push arms at staging and not at the wedge; a simulated walk
  fires `GameWonEvent` after 0.56 units.

  **Confirmed live, 10-run smoke batch** (`Bot_average`, seeds 2000–2009 @6x,
  `2026-07-25_013249.jsonl`): **4 escapes and 0 gate wedges, against 0 escapes
  and 4 gate wedges in the baseline.** Four of the five runs that fixed all
  three clocks got out. All four exits landed within 0.3 units of each other —
  the last metre is deterministic now, not lucky.

  Ten runs still cannot measure a *win rate*; this cleared the instrument, it did
  not answer the difficulty question.

- **Do not trust per-seed replay.** A seed fixes the profile and maniac rolls, not
  the whole run: at 6x, physics stepping and frame timing diverge, so the same
  seed does **not** replay the same trajectory. Baseline seed 2001 fixed 3 clocks
  and wedged; the same seed post-fix died with 1 clock, on a change that cannot
  execute until all 3 are fixed. Compare batches in **aggregate** — a per-seed
  before/after table looks rigorous and is not.

- **A live batch is the only real test of bot changes.** The gate fix was verified
  against static geometry and still shipped a regression that a 3-run smoke batch
  caught immediately: an early `return` from `EscapeState.Tick` that skipped
  `Drive()` while the gate was unknown, leaving honest profiles standing still
  until the timeout. Geometry proves the *destination* is right; only a run proves
  the bot still *walks*.

- **A second wedge site, around the wardrobes.** `stalled` rows at WardrobeA and
  WardrobeB (baseline seed 2000, post-fix seed 2006 at `[22.5, 24.9]`). Present
  before the gate fix, so it is its own bug — likely the same class: the bot
  pressing into geometry a hiding spot's collider makes unreachable.
- **Seed 2002 timed out holding 3 clocks** at 81% explored, while the four
  escapes finished in 143–224s of the 480s budget. Probably a late third clock,
  but the result row does not record *when* each clock was fixed, so it cannot be
  confirmed. Worth adding a `clockFixTimes` field before the full batch.
- **Catacombs uses the same wall-mounted gate pattern** and has never been
  checked. The near-edge rule (aim just inside the trigger, never at its centre)
  should be verified there before trusting any Catacombs escape numbers.

- **First real batch launched 2026-07-25**: novice/average/expert x 20 at 6x,
  then 15 matched-seed control runs at 1x. 75 runs, ~2h40m of wall clock.
  **It died at run 11 — see the post-mortem above. The numbers from it are 10
  runs of one profile and are not actionable.**
- **A full honest run takes ~400 in-game seconds** (the bot explores the whole
  castle). At 6x that is ~70s of wall clock, so 60 runs is about an hour of
  Unity sitting in Play Mode. **The 1x control block is the expensive half** —
  it runs at real speed by definition, so 15 runs is ~100 minutes on its own.
  The window's old estimate used half the timeout per run and understated this.
- **The control block is CONCENTRATED on one profile**, not spread across all
  three. Same wall clock either way, because the cost is the run count. But
  win rate is binary, so 5 runs resolve to roughly +-20 points and cannot see
  the gap the guard exists to catch, while 15 runs on one profile resolve to
  ~+-13 and can. Three unusable answers, or one usable one. `analyze.py` now
  names the profiles that were never validated instead of letting them look
  identical to validated ones once the rows are aggregated.
- **The fast block runs first, the control last.** Rows append as they finish,
  so a batch killed halfway leaves the whole experiment on disk with only its
  validation missing — the recoverable failure. The other order loses the
  experiment and keeps the check.
- **Catacombs**, when you want it: it already has `CatacombsRooms.json`, so
  `nearestLandmark` can be upgraded to a real named-room death map there.
