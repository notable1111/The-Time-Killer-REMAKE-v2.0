# Changelog

Newest entries on top. Updated with every push to `main`.

## 2026-08-28 — Four things the player could not hear, and one report nobody read

**The pattern behind all four: a system that worked and that nobody could
perceive.** Counters said fired, configs said wired, tests said green — and none
of those instruments can answer "did a human notice".

**Hiding changed your body but not the room.** The heartbeat already boosted 1.3x
while hidden and the breath was held to 5%, but measured this day the project
contained **no `AudioLowPassFilter`, no `AudioHighPassFilter`, no
`AudioReverbFilter` and no AudioMixer asset, anywhere** — so from inside a
wardrobe the world was byte-identical to the corridor. That costs the mechanic
its trade: you should be safer AND deafer, so you cannot tell when he has gone.
`WorldMuffle` low-passes only sources with `spatialBlend >= 0.5` — the line the
codebase already draws in its own comments, where the heartbeat is "YOUR heart:
fully 2D" and the drone is "inside your head, not in the room". Verified in play:
it attached to exactly **5 world sources** (his Footsteps, Breath and Vocal, the
door Wind, the clock noise) at 900 Hz, left the player's own body untouched, and
released all 5 to 22000 Hz on unhide.

**Repairing a clock broadcast your position, silently.** `WorldNoiseEvent` had
exactly one subscriber, `ManiacPerception`. `ClockRepair` publishes one on a timer
the whole time you work — its own comment says "the clock is loud while you work
it" — so the maniac heard it and the player did not, and he simply began walking
at you for no perceivable reason. That is a mechanic lost, not a mood. The clip
had to avoid one specific trap: the two nearest sources measured **67% and 85% of
their energy above 2 kHz**, which is the one band the music leaves empty and
therefore where his footsteps live. A bright click would have masked his approach
while you are locked in a repair. Pitched down 2.4x from a metal latch: 30.1% in
460-2k, 27.0% above 2 kHz.

**Escalation was inaudible.** `ManiacEscalatedEvent` fires every time an objective
completes and he steps up; nothing in audio subscribed. The cue is his own roar
pitched down and low-passed — **67.6% below 460 Hz and 0.0% above 2 kHz**, against
the close roar's 60.1% and 0.1%, because distance eats the high end first. It
reads as him, further away, rather than as a UI sting.

**The music never stepped back for him.** `ManiacFootsteps` outranks Music and
Ambience on paper but never called `Announce`, and the naive fix was wrong: at
`strideMeters 0.78` he steps every 0.433s at patrol against ~0.43s clips, so
announcing per step would have parked the music at a flat -9 dB for as long as he
was within 11m. `AudioMix.SetPresence` is a claim that lasts instead of expiring
and applies half the duck. Measured live: music gain **0.850 -> 0.574** with him
near, back to 0.850 when he leaves.

**Two measurement lessons, both paid for.** A clip that measures correctly in
isolation can be inaudible in the mix — the first threat SFX moved their own band
by **+0.9 dB** at real gains, and the fix was not level but *masking*: v1 left
26.9% of its energy inside the roar's dominant band. Re-cut clear of it, the same
clip lifts that moment's 2-20 kHz band by **14.9 dB**. And the first "+0.6 dB,
inaudible" verdict was partly an artifact of averaging across a whole 1.54s
audition file when the clip occupied only its first 0.62s. **Measure the window a
sound occupies, not the file it sits in.**

**The audit had never measured the music.** `audit_audio.py` scanned two folders;
all 22 tracks the director plays live in a third. Real totals are **277 assets,
not 182**. Baseline blessed 157 -> 279 entries, drift 122 -> 0 — it was reporting
every music track as "never checked by ear" every run, which is how a report stops
being read. Of the 99 clips the game actually loads, 45 had never been blessed;
those are batched into four listening sittings.

Also: per-track music trims, because a layer picks one track at random and the
pools spanned up to **5.3 dB** — level was carrying information the dice were
setting. 17 trims, each layer matched to its own median so the layer's centre
stays where it was tuned by ear.

## 2026-08-27 — The MCP toolchain was half-upgraded, and the banner only knew about one half

**MCP for Unity is two programs, and updating the one Unity shows you leaves the
other one stale.** The editor package (`com.coplaydev.unity-mcp`, pinned by git URL)
was v10.1.0 against a v10.1.2 release — that is the update the editor banner reports.
The MCP *server* is a separate PyPI package (`mcpforunityserver`) launched through
`uvx`, and it is not covered by that check at all. Both sat at 10.1.0.

**Bumping the package does not restart the server.** After the package resolved to
v10.1.2, the two server processes from 13:01 were still running out of the uv cache
archive holding `mcpforunityserver-10.1.0.dist-info`. Nothing in the upgrade path
touches them. `ServerManagementService.StartLocalHttpServer` is the restart (it stops
the running server first), and it has to be called deliberately — scheduling it on
`EditorApplication.delayCall` silently never fired, twice, so it was called
synchronously and the tool response was allowed to die with the old process. The
result was read back from a report file on disk, which is the only channel that
survives a transport that is being restarted underneath it.

**Verified, not assumed:**

| check | result |
|---|---|
| `PackageInfo.version` inside Unity | `10.1.2` |
| running server's uv archive | `mcpforunityserver-10.1.2.dist-info` |
| package's own update checker, day-cache cleared | `UpdateAvailable=False, LatestVersion=10.1.2` |
| `scriptCompilationFailed` | `False` |
| `SmokeCheck.Report()` | `ok:true` |
| EditMode tests | **38/38 passed** |

The last row of that table is the banner's own code path, so the notice is genuinely
resolved rather than inferred from a version string.

**What teammates get.** `Packages/manifest.json` is `skip-worktree`'d and stays local,
but **`Packages/packages-lock.json` is tracked** — so this bump ships, and everyone's
MCP server restarts onto 10.1.2 on their next resolve. 10.1.2 carries fixes that touch
traps written down in `CLAUDE.md` §9: a Windows stdin redirect, a `read_console`
multi-line repair, `run_tests clear_stuck` for jobs orphaned by a domain reload, and
one that stops 34 tools forcing an approval prompt on every call. Whether the
"`read_console` returns 0 entries even when Unity has clearly logged" trap is actually
cured is **not** verified here — that needs a session to reproduce the old case.

## 2026-08-28 — Sanity, and a light sampler that went blind after every restart

**The dark now costs something.** An unlit corner used to be a pure win. Composure
(sanity, and never a bar — health here is diegetic and a meter would break that
language) drains in darkness and while hiding, restores in light and on fixing a
clock, and its single mechanical output is that low composure makes the player's
footsteps carry further. Aimed at **hearing** because hearing is the sense the
player controls: walk instead of run and you are quiet again.

Built to three rulings, each now an assertion rather than a note: only genuine
darkness drains, there is a floor, it resets each run. It deliberately does not
touch the heartbeat, the breathing or the audible mix — all finished, ear-tuned
work.

**The threshold is a measurement and the first one was wrong.** CastleWing has a
global light, so the minimum level anywhere is **0.32** — there is no total
darkness to threshold against, and any value under 0.32 makes the feature
completely inert. Worse, an initial survey reported 77.7% of the map dark; that
counted **walls and void**. Re-measured over walkable ground only, using the
maniac's own walkability grid: **55.1% of 3610 walkable samples**. A real number
describing the wrong surface — the third time in two days.

**A live bug the user's recording found.** `LightSampler2D` caches the scene's
lights and rebuilds every 5s. A scene reload destroys them all, and the cached
references survive as Unity fake-null, so every contribution was skipped and the
player read as being in **total darkness wherever they stood** for up to five
seconds. `GameFlow` reloads on restart, so every run after the first opened with
a false dark reading. It hid because a stale cache and an unlit room produce the
same number; it surfaced only when a spot measured at 0.97 read 0.00 minutes
later. Now rebuilds on staleness and times on `realtimeSinceStartup`.

**"Sanity is not working" was right in the way that mattered.** It was running
perfectly: replaying the user's 598 recorded positions through the real lights
shows **62% of his session in darkness and composure down to 0.32 — ×1.41
louder** — and nothing told him. Two fixes: the drain went 75s → 40s (justified
by the corrected 55.1%, not by taste), and **F8 spends composure instantly**,
because a feature that takes sixty seconds to observe is a feature nobody
observes.

**The recorder was blind to its own subject.** A session recorded specifically to
check Sanity contained no sanity at all. `state.jsonl` now carries `comp`,
`light`, `dark` and `loud` — the value, why it moved, and what it cost.

⚠️ **Still open: composure has no in-game cue.** Nothing subscribes to
`ComposureChangedEvent`; the player only finds out by dying. Amnesia does sanity
vision-first, which is the visual lane's channel — flagged, not taken.

**Two standing rules arrived from the user and are now in `CLAUDE.md` §3**, so
every lane picks them up: *answer it yourself first* (measure before handing a
check back to him; ask only what genuinely needs playing, and always with an
example of the right answer), and **the five-line report format** — WHAT IT WAS /
WHAT I DID / WHAT HAPPENED / CHECK THIS / SHOULD BE. Both exist because reports
were narrating process instead of saying what changed in the game.

Verified throughout: compiles, `SmokeCheck ok:true`, **66/66 EditMode tests**.

## 2026-08-27 (last) — Catacombs is a map, not yet a level

**The sweep for dead features found none — and found something bigger.**

After ClockEffects turned out to have been inert for three weeks and been caught
by accident, the obvious move was to stop looking by hand.
`TimeKiller/Verify/Feature Install Audit` asks, for every gameplay
MonoBehaviour: is there any path by which this reaches a running game? Three
verdicts — in a scene/prefab, self-installing, or **DEAD**. It reads the `.unity`
and `.prefab` files as TEXT and searches for the script's GUID, so it opens
nothing and cannot disturb a hand-tuned scene.

**Verdict: nothing dead.** Every gameplay component is now either placed or
installs itself.

**But the LEVEL GAP section is the real finding. 23 components are in
`CastleWingLDtk` and absent from `Catacombs`:**

| missing from Catacombs | what that means there |
|---|---|
| FearConductor, FearDrone, FearSting | **no tension system at all** — the one number every channel answers to |
| PlayerHeartbeat, PlayerBreathing, PlayerVoice | the player has no body: no heartbeat, no panting, no pain |
| ManiacVoice | **he is completely silent** — the counterplay that lets you place him through stone |
| ManiacDirector | no second brain; lose him and the encounter is simply over |
| ManiacWardrobeSearch | he never opens a wardrobe, so hiding is perfect safety |
| BloodTrail, BloodStainField | you do not bleed |
| PauseMenu | **you cannot pause** |
| SessionRecorder, SessionAudioCapture | playtests there record nothing |
| ClockMissRing, InteractPrompt, MusicZone, MusicEq, and 4 more | assorted feedback |

Some of those are legitimately level-specific — a hand-placed `MusicZone`, a
castle-only prop light. Most are not. **Catacombs is a playable map with the
maniac, clocks, hiding and the run flow, and almost none of the systems that make
the castle frightening.** Setup/31 placed gameplay there in July and the eight
features built since have all landed in CastleWing only.

Not fixed here, deliberately: filling it in means running many setup scripts
against a protected scene, which is a decision and a lock, not a drive-by.

**`TimeKiller/Verify/Escalation Ladder`** prints what escalation actually does,
per clock, against the player's own numbers:

```
clocks |  patrol | search | earshot(run) | earshot(walk) | wardrobe | hint wait
  0/3  |    1.80 |   2.60 |         7.65 |          2.70 |     0.12 |     22.0s
  3/3  |    2.07 |   2.99 |         9.56 |          3.38 |     0.30 |     15.4s
```

with both ceilings re-checked in the output (patrol 2.07 stays under the player's
walk 2.20; walking at the last clock still carries less than running does
un-escalated). Difficulty is the thing this project has been burned tuning from a
feeling, so the numbers get a table — and the report says outright that the ear
test is still the user's.

**The gate's art brief now prints from Setup/50**, the same way Setup/52's does:
cell size, pivot, PPU, the visibility threshold to judge it against, and the two
reasons it must stay silent. It also states, where whoever looks for it will
find it, that winning cannot have a world effect while `timeScale` is 0.

**Verified:** compiles, 54/54 EditMode tests, both probes run and their reports
written to `Temp/`. Editor lock claimed and released; nothing opened, nothing
saved.

## 2026-08-27 (later still) — The clock effects were documented as shipped and had never run once

**The find.** ClockEffects was written on 2026-08-04, described in ARCHITECTURE as
part of the objective loop's presentation, and committed. It was also completely
inert: it was a scene object placed by `Setup/50`, and **`Setup/50` had never been
run**. No scene contained a `ClockEffects`. No `ClockHit` or `ClockFixed` recipe
existed on disk. `clock_hit.png` and `clock_wake.png` had been imported on
2026-08-04 and never referenced by anything. The docs said shipped; the game had
nothing, in both levels.

Found by checking rather than trusting: grepping both scenes for the binder
returned zero, and the Recipes folder held only the player's.

**The fix is the one the threat binder already uses.** ClockEffects now
self-installs from Resources, so it is present in CastleWing, Catacombs and any
scene added later with nothing to remember and no scene to dirty. A hand-placed
instance still wins if one exists. `Setup/50` no longer creates a scene object at
all — it builds assets and re-wires an existing binder if there is one.

Verified in play: `[ClockEffects]` installs itself, the beats each fire once, and
**two live `[VFX]` objects** spawn from the now-sliced 7-frame sheets. That is the
first time the clock art has ever been on screen.

**The gate now has a beat, and it is information rather than decoration.**
`AllClocksFixedEvent` plays `GateOpened` **at the exit door**, not at the player,
because that moment is when the run's question changes from "where are the clocks"
to "where is the door". It ships with a heavy shake (0.30 / 0.70s) and
deliberately **no sound** — `ExitDoor` already creaks locally and `AudioDirector`
fires a map-wide unlock sting, and a third source on the same frame is mud — and
no sheet until gate art exists.

⚠️ **The escape itself is deliberately NOT an effect, and this is a measurement
not a preference.** Winning publishes `RunEndedEvent`, `GameFlow` sets
`timeScale = 0` on that beat, `SpriteAnimator` advances on `Time.deltaTime`, and
`EffectPlayer` cleans up with a *scaled* delayed `Destroy`. A burst on
`GameWonEvent` freezes on frame one and is never destroyed — a sprite stuck under
the end screen. The escape flourish belongs on `RunEndScreen`, which already fades
on `unscaledDeltaTime`, and that is UI work.

**SetupGuard sweep: 11 of 69 guarded → 66 of 69.** 55 menu items gained the one
line that stops them half-running in play mode. That hazard is not theoretical: it
cost Setup/43 a whole rig on 2026-08-03, and it bit this project again today with
three sessions sharing one Editor.

Three are exempt on purpose, because for them play mode is not the error case:
`BotPlaytestWindow.Open` (opening the window is how you start a bot run),
`BatchGuard.ForceUnlock` (the escape hatch for a batch wedged mid-play — guarding
it would disarm it exactly when it is needed), and `CatacombsAudit.Run` (read-only,
and auditing the play-mode scene is a legitimate thing to want).

⚠️ `BatchGuard` also *cannot* call the guard: it lives in the runtime folder under
`#if UNITY_EDITOR`, so it compiles into `Assembly-CSharp`, which may not reference
the Editor assembly `SetupGuard` lives in. A blanket sweep would have broken the
build. Every inserted guard was checked to be inside an `Editor/` folder.

**Verified in play mode that `SetupGuard.Blocked` actually returns true** — a
guard nobody has watched refuse is a guard nobody has tested.

**All of it:** compiles, `SmokeCheck ok:true`, **54/54 EditMode tests**, Editor
lock claimed and released, `CastleWingLDtk` left not dirty.

## 2026-08-27 (later) — He can read the floor now, and the castle has less dead air

**Blood pass 2, built and shipped OFF.** Approved in July and deliberately left
unwired; the seam ARCHITECTURE promised turned out to be exactly right.
`BloodTrail` publishes a Core `WorldTraceEvent` beside `BloodSpilledEvent`,
`ManiacBloodTracker` listens for it, and `NoiseCause` gained a third member.
Neither feature references the other in either direction.

**The fairness rule is the whole design: a trail is a LEAD, never a detection.**
All the tracker may do is call `ManiacPerception.NoticeTrace`, which writes the
noise channel and nothing else — no awareness, no `LastSeenPosition`, no belief.
Runtime-verified in play: a lead fired, `LastNoiseCause` read `Blood`, and
**awareness stayed `Unaware` at raw 0**. He gets sent to a spot on the floor and
still has to find the player there like anyone else, which is the same limit the
Director works under and for the same reason.

Three limits carry it: he must nearly walk over the stain (`noticeRadius` 2u,
with line of sight, through his own `sightBlockers` mask so "can he see the
floor" cannot drift from "can he see anything"); traces expire after 45s, because
blood dries and a permanent trail turns the map into a record of everywhere you
have ever been; and he follows the *freshest* trace within 4u, so he reads your
direction of travel but gets the next few steps rather than the destination.

**It ships disabled on purpose**, and both reasons are written on the config:
bleeding starts at low HP by definition, so this presses hardest exactly when
the player has least left — which is why the original note asked for a bot A/B
first — and per-clock escalation is already in flight and unjudged, so turning
both on at once would make either impossible to attribute. Setup/56 prints
`noticeRadius` against his sight range and warns as it closes.

⚠️ **Before that A/B:** `TestTelemetry` counts anything that is not `Suspicion`
as `HeardSound`, so every blood lead would be filed under hearing. That split
belongs to the playtest lane and is not done.

**Escalation gains a fourth dial, aimed at dead air.** `hintQuietAtFull` 0.7
scales the Director's `hintAfterQuietSeconds` and `minSecondsBetweenHints` by the
same factor — 22s/15s become 15.4s/10.5s at the last clock. Scaling both
preserves the ratio it was tuned with: he comes back *sooner*, not *more often*.
The Director exists because a recorded session ran 80 seconds with zero
detections; this shortens those stretches in the back half, which is the other
half of "the curve has no middle".

Two tests guard it, and the second is the one worth having: the wait at full
escalation must stay above 10s, and **`hintError` must still exceed
`sightRange`** — the Director's entire claim to honesty, which until now was only
*printed* by Setup/45. A number checked only by a script nobody runs is not
checked.

**Verified:** compiles, `SmokeCheck ok:true`, **54/54 EditMode tests** (16 in the
escalation suite now), plus the runtime probe above. The Editor lock was claimed
before any of it this time, and released after; CastleWingLDtk left not dirty and
the blood config restored to disabled.

## 2026-08-27 — The third clock was the same as the first

**Three sessions now work in one tree, so the first delivery is a rulebook.**
[`SESSIONS.md`](SESSIONS.md) assigns lanes (programmer / artist / playtest-release)
and, more usefully, names the four things that collide no matter how the folders
are split: play mode blocks everyone's compiles, scenes are single-owner,
`CHANGELOG`/`ARCHITECTURE` are the real conflict file, and only one session may
run git because `push` takes the whole working tree. The three `claude/*` worktree
branches were checked and are **stale** — 0 commits ahead of `main`, 33–39 behind.

**Per-clock escalation, the queued half of the 2026-08-03 horror plan.** The
measured problem: over 407s the curve was Panic 44.3% / Safe 26.8% / Aftershock
20.5% / **Unease 5.7% / Threat 2.7%**. The hesitation pass stretched the moment of
being caught ~5x and did not fill the build-up band. So he now changes as the run
is won: patrol/investigate/search speed **x1.15**, hearing **x1.25**, wardrobe
check **+0.18** at the last clock, blended over 6s.

- **The seam is a Core event, not an Objectives one.** `WorldProgressEvent` says
  "the objective moved on" and knows nothing about clocks, so Maniac still
  compiles with the Objectives folder deleted — and any future objective
  escalates him for free.
- **Two fairness rails, both enforced by tests rather than by intention.**
  `moveSpeedAtFull` has a hard ceiling of **x1.22**: patrol 1.8 vs the player's
  walk 2.2, and above that, keeping distance requires *running*, which is the loud
  choice — escalation would have deleted the endgame's stealth layer instead of
  tightening it. And the wardrobe now has two bonus sources stacking, so the
  Director's promise that hiding never becomes useless had to be re-made about the
  **sum** (`ChanceWithCeiling`, capped 0.6).
- **Deliberately not escalated:** chase speed (already 5.2 vs 4.5 — raising it
  removes the escape, not the safety), sight range (the fairness dial), damage,
  the suspicious creep (that IS the hesitation beat), and the search rush.
- **Auto-added with `HideFlags.DontSave`** — no scene edit anywhere, which also
  means `FindObjectsByType` cannot see it: reach it via `ManiacController.Escalation`
  or subscribe to `ManiacEscalatedEvent`.

**Being seen finally has somewhere to happen.** `ManiacSpottedPlayerEvent` and
`ManiacAttackEvent` were the last threat beats with no visual — audio only, so a
player with the sound down was never told. `ManiacThreatEffects` binds both.
It **self-installs** from Resources instead of being a scene object, because
Catacombs never got the pass that placed `ClockEffects` and a scene-object binder
is silently absent from half the game's levels. It carries a **4s anti-strobe
cooldown** because that event fires on every LOS re-acquire, which mid-chase is
behind every pillar — measured: **3 published in one frame, 1 played**. The
recipes ship **empty**: wired, counted, and silent until the art exists.

**Verified, and labelled.** Both assemblies compile, `SmokeCheck ok:true`, **51/51
EditMode tests** (13 new, 0 skipped — the shipped-config guards really ran).
Runtime, in `CastleWingLDtk`: baseline identity (`MoveSpeed 1`), then
`WorldProgressEvent{3,3}` → Intensity 0→1, patrol **1.8→2.070**, hearing
**9→11.25u**, wardrobe **0.12→0.30**. Play mode was left with the scene
**not dirty**. **Not playtested by ear** — this is a difficulty change and is not
locked until the user rules on it.

## 2026-08-22 — The Editor compiling is not the game building

**The project had never produced a Player build, and nothing in the Editor said so.**
`EditorUtility.scriptCompilationFailed` reads `False` and `SmokeCheck.Report()` returns
`ok:true` while the build fails outright — they only ever describe the *Editor*
assembly. Two independent blockers were hiding behind that green light.

**Blocker 1 — 65 compiler errors, one cause.** `DebugOverlay` puts `Watch`/`Unwatch`
inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, but the `#else` branch only stubbed
`Init`. In a release build both methods vanish while ~65 call sites across
`ManiacController`, `PlayerController`, `AudioDirector`, `FearConductor`,
`ObjectiveManager` and the rest still call them → `CS0117` ×65. Verified as the sole
cause: `grep -v DebugOverlay` over all 65 unique errors returned nothing. Fixed by
adding the two missing no-op stubs, restoring the file header's stated intent. The
overlay itself stays stripped from release.

**Blocker 2 — `Failed to write file: resources.assets`.** With compilation passing the
build got further and died writing `resources.assets`. `CFXR4 Rain Splashes` and
`CFXR4 Rain Falling` carry a `CFXR_EmissionBySurface` whose `OnValidate` sets
`hideFlags = DontSaveInBuild` — the vendor marking it editor-only. Both prefabs sat
under `Assets/Resources/`, which force-ships everything, so Unity was told to include
and exclude the same object. Clearing the flag does not hold; `OnValidate` re-applies
it on every load. Moved the two prefabs to `Assets/OutsourceDemoOnly/` instead — the
game never referenced them, only the vendor's own demo scene did, and GUID references
survive the move. They were the only 2 offenders across all 66 Resources prefabs.

**Result: 155s, 674.84 MB, 0 errors, 0 warnings**, all four scenes packed
(`level0`–`level3`), `resources.assets` written at 13.8 MB. Output in
`Build/Windows/`, which is now gitignored — it was not, and `push` takes the whole
working tree, so a 676 MB build would have gone in as raw binaries.

**Worth keeping in mind:** every asset under `Assets/Resources/` is force-built,
third-party demo content included. That is what dragged an editor-only vendor
component into the Player and is why `resources.assets` is as large as it is.

## 2026-08-05 — A missing glyph doesn't look missing, it looks like another font

**`RunEndScreen`'s prompt has been rendering in two typefaces.** Line 60 writes
`"R — run it again        ESC — quit"`, and the em dash U+2014 was in neither TTF.
The assumption was that TMP shows a box for that. It does not. Measured from
`textInfo.characterInfo` on the probe `"A—A·A"`:

| char | resolved from | advance |
|---|---|---|
| `A` U+0041 | TimeKiller_Body SDF | 44.0 |
| `—` U+2014 | **LiberationSans SDF** | **86.0** |
| `·` U+00B7 | TimeKiller_Body SDF | 20.0 |

TMP falls through to `TMP_Settings.defaultFontAsset` even with every fallback table
empty (`fallbackFontAssets` 0, `fallbackFontAssetTable` 0, `missingGlyphCharacter`
0). So a smooth vector sans at double the advance width sat inside chunky pixel
text, `isVisible` stayed `true`, and **every missing-glyph check reported zero**.
That is why it survived: it reads as slightly off, not as broken. Scanning every
string that reaches a TMP label, this was the only affected line in the project —
`SliderPercentLabel` writes a bare number, and the `%` in `ManiacController` goes to
`DebugOverlay`, which is IMGUI and uses Unity's own font.

**Three glyphs authored, each placed against a measured shipped glyph.** The em
dash sits in the hyphen's own band (Body: y 256..384, exactly; Display: 256..384
against the hyphen's 224..384 — the closest the doubled grid allows). The ellipsis
sits on the period's band with its dots on the period's advance, so `…` and `...`
draw the same picture. Widths: em dash 10 px Body / 24 px Display, against hyphens
of 6 and 18.

**The percent was drawn twice, and the first one was not shippable.** Kept at digit
width (20 px) there is no room for counters, so the rings became solid blocks — and
rendered, `60%` read as `60/.` at 16 px. Widening Display to 26 px buys a 4 px wall
plus a 2 px counter; Body drops to 1 px ring walls, lighter than its 2 px stem,
because at 9 rows the counter is worth more to recognition than matching the stem.
Caught by rendering it and looking, which is the only reason it was caught.

**Verified end-to-end, not just "the font has it".** Both atlases now hold 80 glyphs
(was 77), Static, one texture, outline 0.18 and sharpness 0.4 preserved. Re-running
the fallback probe over the real `RunEndScreen` literal, the MainMenu tagline and
`"loading… 60%"` in both weights: **`FOREIGN=none`, `noGlyph=0`** on all six.

`Extras` gains `—` and `…` only — `%` is ASCII 0x25 and was already inside the
bake's 32..126 sweep; it had simply never had a glyph to find. Rebake with
**Setup/47**, not 46: 47 re-rasterises from the TTF at the on-grid point size, which
is what picks up new outlines.

Docs: `Tools/UIArt/README.md`'s "Not done yet" section was three claims and all
three were stale — `ObjectiveHUD` became uGUI+TMP on 2026-07-28, `RunEndScreen`
uses `TMP_Text`, and `InteractPrompt.cs` exists. Replaced with what is actually
open, including the Catacombs end screen below.

**Found, not fixed: the Catacombs end screen is blank.** `RunEndScreen`'s three
fields are `TMP_Text`; Catacombs still holds legacy `UnityEngine.UI.Text`, so Unity
type-checks them to null at load — confirmed by opening the scene and reading the
fields (`headline`/`detail`/`prompt` all NULL, `group` survives because its type
matches). The code null-guards every write, so it does not crash; you win or die in
Catacombs and the panel fades in saying nothing. The YAML looks correctly wired —
the fileIDs are non-zero — which is why no scan caught it. `Setup/44` only ever ran
on CastleWing.

EditMode tests were **not** run for this change: another session put the editor into
play mode before they could start. Compile is clean and smoke is `ok:true`; the test
pass is still owed.

## 2026-08-04 — The core loop had no presentation layer at all

**The audit that started this.** `EffectPlayer.Play` had exactly **two call sites
in the whole game** — the player being hit and the player dying. Fixing a clock,
the thing a run is *about*, published `ClockFixedEvent` to a single subscriber:
`TestTelemetry`. A correct skill-check press called `AddProgress` and nothing
else. And neither the working noise nor the miss noise is audible at all —
`WorldNoiseEvent` and `PlayerFootstepEvent` have one gameplay subscriber each,
`ManiacPerception`, so they feed the maniac's hearing model and play no clip.
The only clock sound in the game was the sting for the **last** clock.

So the repair loop was silent and, apart from the 2026-08-02 miss ring,
invisible. This pass gives two beats a presentation layer: the **earned press**
and the **clock waking up**.

**`ClockHitEvent`, the mirror of `ClockMissEvent`.** A fumble had a ring and a
noise; success had a bar that moved. Deliberately **not** published on the press
that finishes a clock — that press belongs to `ClockFixedEvent`, and firing both
would stack a small burst under a big one on the same frame.

**The burst plays at the clock's face, and that needed measuring.** The sprite
pivots at its base (0.5, 0.06) so the clock stands on the floor, which puts
`transform.position` **1.03u below the clock's middle** — measured in the scene,
all three clocks, bounds 2.33×2.33u. A burst played at the transform would have
gone off at its feet. `ClockObjective.EffectPoint` reads renderer bounds rather
than a hand-placed anchor child, so re-drawing the clock art cannot leave the
effect aimed at the wrong spot.

**The light now rises instead of snapping** (`lightRiseSeconds` 0.45, ease-out).
The sprite swap and a full-strength green light used to land in the same frame.
The target intensity (**1.10**, read from the scene) is captured in `Awake`,
before anything animates it — sampling it at the moment of the rise would latch
whatever an interrupted rise had reached and the clock would come back dimmer
after every reload. `0` restores the old snap exactly.

**Two art traps, both caught by measuring instead of looking.** The generated
wake ring had an interior of `(56,25,22)` at **full alpha** — a dark brown disc
that would have pasted over the clock face; 59% of that strip's pixels were
erased by the new `clean_burst.py`, whose rule is that *for a light effect,
alpha follows luminance* (a glow has no dark parts). And the ring **never faded
out**: its last frame still carried **959 ink pixels at mean alpha 59.7**, 58%
of opening strength, which does not end — it vanishes, because `EffectPlayer`
destroys a one-shot exactly when its last frame has played. `--fade` applies the
hold-then-drop envelope the BloodBurst particle already uses. PixelLab's final
two frames also collapsed the ring *inward*, so the strip flickered back to 432
ink after dropping to 2; trimmed to the monotonic part. Final strips:
hit **225→333 peak→0**, wake **859→984 peak→0**, both ending empty.

**A correction worth recording: the first eyeball read was wrong.** The spark
frames looked like a pulsing sun in the thumbnails. The ink curve says otherwise
— it swells to frame 2 and decays to 17 px. The thumbnails only showed the
swell. This is the fourth entry in this file where a number disagreed with a
glance and the number was right.

**Also learned about the tooling:** `create_1_direction_object` draws *objects*,
not effects — asked for "an old clock waking up" it returned 16 grandfather
clocks. `create_image_pixflux` (1 generation) plus `animate_image` is the path
for VFX. Both review packs were left undeleted rather than discarded unilaterally.

**Sound is wired but unlistened-to, and that is a real caveat.** `metalClick`
(0.45s) for the press and `metalLatch` (0.26s) for the wake, chosen by category
and measured duration. There is **no bell or chime anywhere in the project's
audio packs** — the PSX "event sounds" are 12–22s ambience beds. A synthesised
clock chime, the way `gen_heartbeat.py` synthesised the heart, is the obvious
upgrade for the wake and is a one-string change in `ClockEffectsSetup`.

**Verified:** compile clean, smoke `ok:true`, **38/38 EditMode tests**. **Not
verified:** anything about how this looks or sounds in play — `Setup/50` had not
been run when this was written, because Unity was in play mode and `SetupGuard`
correctly refuses to half-run a setup script there.

## 2026-08-04 — The hesitation works. The tension curve still has no middle.

**Measured, not assumed.** Every session on disk predated the `stalk`/`fade`/`susEps`
fields, so the archive could not answer this — it took a fresh batch: 3 runs,
`Bot_average`, **1x** (not accelerated, so the seconds compare to the old figures),
seeds 3001-3003 on CastleWingLDtk.

**Seconds of stalking per suspicion episode — the one number `awarenessCertaintyScale`
actually moves:**

| run | stalk | episodes | s/episode |
|-----|-------|----------|-----------|
| 1   | 5.54s | 6        | 0.92s     |
| 2   | 4.71s | 2        | 2.35s     |
| 3   | 4.03s | 4        | 1.01s     |

**Pooled: 14.28s across 12 episodes = 1.19s per episode** (mean-of-runs 1.43s, pulled
up by run 2's n=2). Against the pre-change figure of **under 0.25s**, that is a real
**~5x stretch** — larger than the ~2.9x the config predicts, which makes sense: the
band also holds time when he is suspicious at low exposure, not only the climb.
Reported per EPISODE on purpose; a run that simply met the maniac more often would
raise the total without stretching anything.

**But the curve is still bimodal, and that is the finding that matters.** Over 407s:
**Panic 44.3% · Safe 26.8% · Aftershock 20.5% · Unease 5.7% · Threat 2.7%.** The
build-up band did not fill in. Stretching his certainty made the *moment of being
caught* longer without giving the run a middle — which is the case for going ahead
with **per-clock escalation**, not a reason to touch this number again.

**Labelled honestly: this is not a controlled A/B.** The comparison baseline is a
single older session (Threat 3.4%) that predates several other changes. Proving the
hesitation moved the CURVE needs a matched-seed control arm at
`awarenessCertaintyScale = 1`. What is measured here is that the mechanism does what
it was built to do; what is inferred is what that did to the pacing.

**Run 3 has no result row.** The recording shows three run segments (2 deaths, 2
reloads, then 77s ending in `end`), but `results/*.jsonl` holds only two, and
`Editor.log` has the batch's start line with no `Done:` and no `ABORTED:` — so the
batch did not finish on its own; play mode ended under it. The hesitation numbers
still stand on all three segments, because the recorder writes as it goes while a
result row is only written when a run *finishes*. **Count runs from the recording.**

**New in `mine_session.py`** — a `HESITATION` section reporting per-run stalk, fade,
episodes and the derived s/episode, with the threshold it judges against printed
alongside. It splits runs on a counter DROP (a cumulative counter that falls is a
scene reload, not a bug) and says outright when a recording predates the fields
instead of printing a confident zero.

## 2026-08-04 — Shadow detail pass: penumbra, volume, breathing, and a tell

The shadows read in Play, so this pass makes them *interesting* rather than
merely present. Four details, all config-driven from `ShadowConfig`, all
reversible with `Setup/49b`.

**Penumbra — shadows soften as they stretch.** `shadowSoftnessFalloffIntensity`
was sitting at URP's 0.50 default, untouched, so a shadow was exactly as sharp
four units out as it was at the caster's foot. Now 0.80, with softness raised
0.25 → 0.50. Costs no geometry and nothing at runtime; it is a per-light value
URP already supported and we had simply never set.

**Props have volume.** Every caster was `CastShadow`: props shaded the world but
never themselves, so a barrel was lit identically on the torch side and the far
side. Props are now `CastAndSelfShadow` (47 of 58 casters). Walls stay
`CastShadow` deliberately — a wall self-shadowing its own face fights the torch
that is meant to be lighting it — and characters stay honest too. Per-category,
so any of the three can be changed without touching code.

**Shadows breathe with the flame.** `FlickerLight2D` already wobbled each torch
on layered Perlin noise, but `shadowSoftness` was static, so the shadow edge
never moved even as the flame guttered. Softness now rides the **same noise
value** as the intensity, ±0.18. That coupling is why it lives inside
`FlickerLight2D` rather than in its own component: a separate flicker would
compute its own noise and drift out of step with the flame it belongs to, which
looks worse than not doing it at all. At amount 0 the component behaves exactly
as it did before shadows existed.

**The maniac's shadow arrives before he does.** Deliberately stylised, and the
one change here that is a mechanic rather than a look: his caster moves to a
scaled child so his shadow reads larger than his body, sweeping into view before
he rounds a corner. Verified from the mesh vertices rather than assumed — his
shadow footprint is **1.00u against the player's 0.45u**, from a body only 9%
bigger (0.60u vs 0.55u). It hands the player real information, so he becomes
slightly easier to avoid and considerably more frightening to be near.
`maniacShadowScale` 1.0 makes it physically honest; 0 removes it.

**Two traps caught while building.** A `ShadowConfig.asset` already on disk keeps
its serialized `shadowSoftness` 0.25 and silently beats a new field default, so
the penumbra work would have been a no-op — the value is now written explicitly.
And `Remove()` built a pristine config to restore torch strength, whose
`shadowBreathAmount` default is 0.18, so removing the shadows would have switched
a shadow feature *on*; it now zeroes it.

Verified: compile clean, smoke `ok:true`, 38/38 EditMode tests, scene saved.
Contrast still has to be judged in Play — editor renders here cannot measure
lighting, for the reasons in the previous entry.

## 2026-08-03 — "It looks cheap": nothing in the castle cast a shadow

**The report was that the new ambience pass looked cheap.** It did, and the light
shafts were the obvious suspect. Measured at the game camera in CastleWingLDtk:
the beam never touches its own flame (a band of dark wall sits between the candles
and a hard horizontal top edge), it does not fall off (+12.0 → +14.7 → +14.3 down
its whole visible run — a constant stripe, not a beam), it never lands (`shaftLength`
is a fixed 4.2 for all 18 regardless of where the floor is, so it stops in mid-air),
its lit core is 0.39u wide at the source so it reads as a spotlight sliver, and all
18 are byte-identical — `lossyScale (0.65, 0.53)`, alpha `0.160`, all at `y = 30.40`.

**But the shafts were a symptom.** `ShadowCaster2D` count in the scene was **zero**
and all 25 `Light2D` had `shadowsEnabled = false`. Light passed through every wall,
pillar and wardrobe; a pillar standing under a torch threw nothing. That absence is
most of what made the room read as a lit backdrop with sprites laid on top.

**Setup/49 gives the castle occlusion.** 58 casters — 9 wall, 47 prop, 2 character —
derived live from the colliders via URP's `ShapeProvider`, deliberately not baked
outlines, because the colliders are hand-tuned and a baked shape would go stale the
moment one moved. Idempotent (58 after one run and after three), excludes triggers,
floors, rugs and the 66×52 `CameraBounds` volume, and `Setup/49b` returns the scene
to zero casters, zero children, zero casting lights.

**Two traps found the expensive way, both now handled in code.** `ShadowCaster2D`
is `[DisallowMultipleComponent]`, so `HallColliders` — seven colliders on one
GameObject — got one caster and the 20×1 hall wall cast nothing; extras now get a
`__Shadow_N` child each. And all 18 torches carry `FlickerLight2D`, which rewrites
`Light2D.intensity` every frame from its own `baseIntensity`, so a script setting
only the light would work in the viewport and be silently discarded on Play.

**All 25 lights sit on the Multiply blend style.** A Multiply light scales the
sprite's own colour toward full albedo rather than adding light of its own, so it
saturates — which is consistent with torch intensity 1.1 and 20 rendering
identically. Whether that is wrong for this art style is a judgement for Play, not
a defect being asserted here.

**What is NOT verified, stated plainly: how much the shadows actually read.**
Edit-mode renders in this project do not reflect `Light2D` property changes at all.
Through the real game camera and the real Game View path, toggling every light's
`shadowsEnabled`, toggling every caster's `castsShadows`, taking torch intensity
from 1.1 to 20, and switching every point light from Multiply to Additive each
changed **0.000% of pixels, peak 0/765**. Only adding or removing lights outright
shows up. Earlier coverage figures from this session were produced through that same
path and have been **retracted**; they were artifacts, not measurements.

**So the probe measures only what data can answer.** `TimeKiller/Verify/Shadow
Audit` reports caster counts by kind, how many lights cast, the blend-style
histogram, how many casters sit inside a casting light's radius, and the
maniac's fully-exposed radius (0.90u of the 4.5u torch radius) — and says outright
that contrast must be judged in Play. It would have caught both real faults, zero
casters and all-Multiply, in one call. The render-diff version was written first
and deleted: it returned a confident `0.00%` that read as "the shadows do nothing",
which is a measurement that lies.

**Lighting is untouched.** `torchBoost` defaults to 1.0, which writes back exactly
the authored values (`Light2D.intensity` 1.00, `FlickerLight2D.baseIntensity` 1.10,
blend style 0). Raising it is the lever for making shadows read, and it costs
stealth: `ManiacPerception.Exposure()` reads point-light intensity directly, so
brighter torches enlarge the radius inside which the maniac treats the player as
fully visible. Dimming the ambient instead was rejected — `Exposure()` starts from
the `ambientExposure` **constant** and never reads `GlobalLight`, so dimming would
darken the player's screen while leaving the stealth model untouched, the same
desync `BrightnessSettings.cs` was written to forbid.

## 2026-08-03 — The recorder was never truncating sessions; it was mislabelling them

**The report was "sessions end early". They did not.** Every one of the 17
recorded sessions that had stopped at a scene reload ended on a row marked
`"reload"` with nothing after it — which reads exactly like a recorder that
throws away the rest of the run. Four sessions were studied as a data-loss bug.

**The resume works, and it was proved by running it.** Forcing `GameFlow.Restart()`
mid-recording produced one continuous file: row 35 is the `reload` at t=**10.07**,
and rows 36–45 carry straight on at **10.08 → 12.35**. `Editor.log` shows the
matching `[SessionRecorder] RESUMED` from `Start()`. Nothing is lost across a
scene reload and the timeline stays monotonic.

**What actually ended those sessions was play mode ending.** `Editor.log` names
the culprit for the newest one: `[BatchRunner] 1/1 (... seed 3001: death)`, and
`BatchRunner` sets `EditorApplication.isPlaying = false` when its batch is done.
A one-run batch therefore dies at the first death — correctly. `OnDestroy` then
wrote `"reload"`, because it could not tell "a scene is reloading, expect more"
from "the play session is over, this is the end", and called both the same thing.

**Fixed by making the two endings different marks.** `OnApplicationQuit` sets a
flag before the `OnDestroy` storm, so a clean end now writes **`"end"`** and clears
the resume handle, while a real reload still writes `"reload"` and parks it.
Runtime-verified: entering play mode, recording, and stopping produced a file
whose last row is `{"t":9.15,"mark":"end",...}`.

**And the analyser now asks the question first.** `analyze_session.py` opens with
a session-integrity check: a file ending in `reload` is flagged `session:unresumed`
(sev 97, "everything after this point is missing"), a file with no end marker at all
is flagged `session:truncated` ("treat every number below as a floor"), and a clean
`end` is silent. Verified against three real sessions — one of each. The cost of
this bug was an investigation, not a byte of data, and that is exactly the cost the
check now removes.

## 2026-08-03 — The fonts were blurred at bake time, and no setting could undo it

**Both UI fonts are pixel fonts that were rasterised off their own grid.** Display
is drawn 32 units-per-pixel against a 1024 em, so its native em is 32px; Body is
drawn at 64, so its em is 16px. The atlases shipped at `samplingPointSize` **90** —
90/32 = 2.8125 and 90/16 = 5.625, both fractional. Every glyph edge was resampled
onto a half-texel and the softness was baked into the texture, where no runtime
setting could recover it. The fingerprint was in the glyph metrics: **54.1%**
(Display) and **50.0%** (Body) of them were non-integer — widths like `56.25`,
advances like `59.0625`.

Rebaked at **96** (Display, 3× native) and **64** (Body, 4× native).
Non-integer metrics: **54.1% → 0.0%** and **50.0% → 0.0%**.

**The shader was `TextMeshPro/Mobile/Distance Field`** on a PC-only game. The mobile
variant is the reduced-instruction one and does not expose Sharpness at all. Swapped
to the desktop `TextMeshPro/Distance Field` and set `_Sharpness` to **0.4**.

**Measured A/B, same string, same sizes, same dark floor value.** Rendering the old
assets restored from git against the new ones: mid-tone (soft-edge) pixels fell
**8274 → 6260, −24%**, while fully-lit pixels held at **15492 → 15393 (−0.6%)** —
the glyphs kept their weight and lost their fuzz. The difference is real but subtle,
and clearest on the small Body text.

**The rebake is in place.** The material and atlas are sub-assets that the scenes
reference by fileID, so `Setup/47` copies the new glyph data onto the existing
objects rather than creating new ones. All six fileIDs verified identical afterwards.
Padding stays 9 so `gradientScale` stays 10 and the tuned outline (0.18, black) keeps
its exact weight; population mode stays Static, so the git-churn fix from `Setup/46`
survives.

**Three off-grid text sizes, all in the pause menu** — Title 78, Caption 30, Percent
26 — snapped to 64/32/32. `Setup/48` encodes the *rule* (snap to the font's native em)
rather than those three numbers, so text added later cannot quietly drift off-grid.
Fixed at source in `PauseMenuSetup` too, so a re-run agrees.

**Canvas scale match made explicit.** `RunEndCanvas` and `MenuCanvas` had never set
`matchWidthOrHeight`, so it defaulted to 0 — width-only matching, which over-scales
on an ultrawide and under-scales on 4:3. Both now 0.5, matching what `PauseMenuSetup`
already chose. `ObjectiveHudCanvas` is **deliberately left at 1**: its setup script
says "the HUD hugs top and bottom", and that is a decision, not an oversight.

**The pause menu's layout was overlapping itself, and it was never a font problem.**
Reported from a screenshot: captions outside the frame, sliders running past the right
edge, RESUME/QUIT sitting on top of the MUSIC and SOUND rows. Two independent bugs,
both in explicit rect literals that predate this session:

- **`Anchor` forces pivot 0.5**, so an element anchored to a row's *left edge* must be
  offset by half its own width to line up. The caption used offset **+10** with a
  **210** width, putting its left edge at **−395** — 15px outside the 760-wide panel
  entirely, and deep into the frame's 60px decorative border. Same trap on the right
  for the readout.
- **Rows at −40/−140/−240 with buttons at +130** put the buttons at **−214..−146**,
  which overlapped both the Music row (−175..−105) *and* the Sfx row (−275..−205).

Re-laid out against the 9-slice border **measured off the sprite** (60/48/60/58 at
ppuScale 1 → true inner area x −320..320, y −262..252) rather than the numbers in the
art README: title 156..252, rows at 90/0/−90, buttons at −224..−156, gaps a symmetric
31/20/20/31. Verified by walking every RectTransform into panel-local boxes and
pair-testing them — **6/6 inside the frame, 0 overlapping pairs**.

**The pause panel got an interior, and the frame turned out not to be a frame.**
The menu was transparent in the middle — you could read the player standing behind
it. The obvious fix (a dark fill parented under `Panel`) would have been wrong:
measuring `EndFrame.png`'s 9-slice **centre** shows it is only **79% transparent** —
sprite rows y 56..72 are **100% opaque** and stretch into the stone ledge the buttons
rest on, at canvas y −232..−172. A child of `Panel` draws *over* the frame sprite and
would have erased that ledge. So `Interior` is a **sibling ordered ahead of the
frame**, and the art always wins. Its colour is `rgb(19,18,31)` at alpha 0.94 — the
darkest tone that actually occurs in the sprite (2964 px), taken from the art's own
palette rather than invented. Draw order is now set explicitly (Blackout 0, Interior
1, Panel 2) so a re-run cannot stack them wrongly.

**New `SetupGuard`, because Setup/43 half-ran in play mode.** Invoked while the editor
was playing, it built and re-anchored its entire rig and then threw at
`MarkSceneDirty` — *"This cannot be used during play mode."* Everything it did was
discarded when play stopped, so it looked like nothing happened while reporting an
exception. The half-run is the hazard, not the exception. `SetupGuard.Blocked(label)`
refuses in play mode **and** mid-compile, and says why (a `MenuItem` validate function
would grey the item out with no explanation). Wired into Setup/43, 47 and 48.

**Audited: 56 of the project's 65 menu scripts have no play-mode guard at all** — only
9 check `isPlaying`. Every one of them can half-run the same way. The helper now
exists so adding it is a one-line change per script; the sweep itself is not done.

**Found on the way out: Catacombs was never TMP-migrated.** `Setup/44` only ever ran
on CastleWing, so Catacombs still holds legacy `UnityEngine.UI.Text` while
`RunEndScreen`'s fields are `TMP_Text` — the references dangle the moment the scene
loads, and merely opening and **saving** it writes the nulls to disk (it also drops
`heartAudio`/`heartbeatClip` and gains `bodyRadius`, all re-serialisation of classes
that changed since the scene was last written). That diff was reverted and `Setup/48`
now skips Catacombs by name. **Its run-end screen is presently unwired** — worth
fixing before anyone ships that level.

**Still open — 30 characters cannot be baked.** `# $ % & * < = > @ [ \ ] ^ _ \` { | }
~` and every curly quote, dash and ellipsis are missing from *the TTFs themselves*.
`Setup/46` has been asking for them all along and logging `** could not add **`;
nobody read the warning. These are hand-authored pixel fonts, so the glyphs have to
be drawn as ASCII-art skeletons in `Tools/UIArt/add_glyphs.py`, in both weights.
Nothing in the UI uses them today — `SliderPercentLabel` emits a bare number — so
this is a latent trap, not a live bug.

## 2026-08-02 — Fear becomes one number, and the game learns to record itself

**The heartbeat had three opinions about danger.** The heart ran a distance curve
with awareness FLOORS bolted on, breathing ran its own exertion model, and the
world duck ran off the heart's intensity. Floors snap: a maniac 30u away flipping
to Detected slammed the heart from 88 to 150 bpm — maximum panic for a threat that
could not reach you.

**New feature `C#/Fear/`** — `FearConductor` owns one smoothed 0..1 value and
publishes it; heartbeat, breathing, drone, sting and the vignette all subscribe.
Awareness and closing speed are **multipliers, never floors**, which is the whole
trick: any multiple of near-zero proximity is still near-zero, so context can
colour fear without manufacturing it. `fearStartDistance` **26u** against a camera
showing ~10.7u — the old 10.4u range meant the warning arrived at the same moment
as the sight of him. Beats are **scheduled** (`60/bpm`), never pitch-shifted, and
volume now starts near zero instead of the old 0.55.

Rate verified against the design target: **0.15 → 58 bpm, 0.35 → 78, 0.60 → 110,
0.80 → 137, 1.00 → 165**, all inside spec, both curves monotonic.

**The tension drone was chosen by measurement.** Lowpassing `Monolith_1` at 200 Hz
costs it only **0.5 dB** — genuinely all sub-bass. `Void_1` and `An Empty Home_1`
lose 4-5 dB to the same filter (air and detail that would fight the heartbeat).
Its loop seam measures -34.3/-36.8 dB against a -22.8 dB average, so the loop
point is inaudible. It also **dips 55% below what fear alone justifies while
recovering** — a bed that only tracks fear is a loudness meter; the silence is
what makes the next swell land.

**Ambience now ducks 2.4 dB at panic, not to 0.08.** The old duck effectively
muted the castle and took the maniac's own footsteps with it, at exactly the
moment the player most needs to hear where he is.

**New feature `C#/Recording/`** — the game records itself, because a reviewer with
no screen and no ears can still study a recording. Three streams per session:
state JSONL at 4 Hz, downscaled frames, and the **final audio mix** captured off
the AudioListener via `OnAudioFilterRead`. Plus **F9/F10/F11** for the tester to
stamp *boring / unfair / great* — the only signal in the whole pipeline no machine
can produce. `Tools/Playtest/analyze_session.py` ranks the suspects; `/watch`
packages the ritual.

**Three bugs the pipeline found on its own first runs:**
- The analyser reported four stuck players and **all four were the game working**
  — three were the clock mini-game (which requires standing still) and one was 24s
  in a wardrobe. It now excuses hiding and repairing.
- The F1 overlay was clipping at a fixed 500px box, silently drawing **nine
  watches offscreen** including the entire Fear block. GUILayout gives no warning,
  so it looked exactly like systems failing to register.
- Frame filenames were written in the system locale (`f00003_3,3.jpg`), and the
  analyser parses seconds back out of that name — so every finding lost its
  evidence. Caught by running the tool, not by reading it.

**`detectedMultiplier` 1.75 → 1.25, measured down.** A recorded session showed
21.2s in the Panic band on the way up against **3.5s in Threat** — the build-up
was being skipped, because at 10u `0.53 × 1.75 = 0.93`. The guard test that should
have caught it only checked the OUTER radius, where proximity is ~0 and any
multiplier passes trivially. It now checks mid-range, and asserts no >0.2 fear
jump per metre.

**Audio headroom: 42 project assets were peak-normalised to 0 dBFS**, which is the
wrong target for audio that layers. Measured on a real session, the master output
reached 0.000 dB with a **flat factor of 18.2** — the waveform flattening at the
ceiling, the signature of real clipping. 31 files re-normalised to **-3 dB peak**;
project-owned clipping assets **42 → 0**. New `Tools/AudioPipeline/audit_audio.py`
measures every asset and guards an approved baseline.

**NOT fixed, and worth knowing:** the 12.3 dB loudness spread inside `Heartbeat/`
survived this pass at 12.2 dB. A uniform peak gain preserves relative loudness —
peak and RMS are different things, exactly as this changelog already recorded once
before. That needs LUFS normalisation, which changes relative loudness and is a
separate decision.

**Not playtested by a human.** The fear system, the hearing and chase fixes and the
re-normalised audio have all been measured, never heard or played.

## 2026-08-02 — The maniac stops hearing through stone, and stops walking into door frames

Two defects in what he already had. No new senses and no new behaviours — both had
been shipping for weeks and were found by measuring code, not by playing it.

**Hearing was the only sense that ignored geometry.** `OnFootstep` and
`OnWorldNoise` compared distance against `hearingRadius * loudness` and nothing
else — no linecast, unlike sight. A footstep two rooms away through solid castle
wall set the noise fields exactly as one taken beside him, so his 9u hearing was
really a 9u sphere of omniscience. Walls now muffle: each solid body between him
and the noise multiplies what is left by `hearingWallMuffle` (**0.45**), reusing
the same `sightBlockers` mask that stops his eyes so "solid" means one thing.
Note what the fix did *not* need to do — distance already handled the far case, so
muffling only has to fix the SAME distance heard THROUGH a wall.

Measured in CastleWingLDtk across **733 listening posts**, calling the shipped code
path: it removes **11.7%** of his running earshot overall — but the median post
loses only **2.6%** while the tight interior around (39, 9) loses **89.8%**. So it
deletes the "he heard me through a wall" moment without broadly nerfing him.
Walking is barely touched (**0.7%**), because walking earshot is 2.7u and rarely
crosses a wall at all — this is a change to running, whatever intuition says. Max
walls found on one sight line was **5**, and 19% of posts see 2+ somewhere, so the
per-wall decay does real work instead of collapsing to a binary.

Counting DISTINCT colliders is a floor, not a thickness: a tilemap merged into one
`CompositeCollider2D` reports a single hit however many of its walls the line
crosses. Deliberate — under-counting only ever makes him hear *better*, so it fails
toward the old behaviour rather than toward a deaf maniac.

**"If he can see you, the way is clear" was false.** ChaseState beelines with
`Motor.MoveTo` while `CanSeePlayer`, on the reasoning that line of sight proves the
path. But sight is a **centre-to-centre** linecast and his body is 0.57u wide, so a
doorway seen at an angle passes the line and stops the body. Worse, the motor
writes `linearVelocity` directly, so a maniac pressed into a wall reports full
chase speed while going nowhere — velocity cannot detect this, only displacement.

New `TimeKiller/Verify/Maniac Chase Grind` sweeps his real capsule along the line
he would beeline down, over 4000 sampled pairs (3361 with clear sight).
**9.46% of all sightings had a beeline his body cannot complete**, worst case
reaching 4% of the way. The distribution is the damning part:

| separation | beeline blocked |
|---|---|
| 1-2u | 0.80% |
| 3-4u | 6.58% |
| 5-6u | 13.57% |
| **6-7u** | **18.28%** |

Worst at 6-7u — which is `sightRange`. It was at its worst at the exact moment he
first acquires you. He now watches his own **displacement** and hands the chase to
the navigator for `beelineNavSeconds` whenever he covers less than
`beelineStallFraction` of the ground his speed predicts: **0.73u every 0.35s, or
the navigator drives for 0.9s**. A false positive costs nothing, because the
destination is identical — with a route he paths there, and with no route the
navigator falls back to the same straight line he was already on.

**The fallback was checked before it was trusted.** Of the 318 blocked beelines the
probe found, the navigator has a route for **318 — 100%**. Every case it fires on
is one it can actually rescue; had that number been low, the honest conclusion
would have been that the geometry was the problem and the fallback theatre.

**8 new EditMode tests** (4 hearing, 4 chase), 28 passing in total. The hearing
ones guard the muffle in both directions: that it muffles at all, and that
`hearingWallMuffle = 1` still reproduces the old geometry-blind behaviour exactly,
so the escape hatch back is real rather than assumed.

**Not runtime-verified.** Both changes compile, pass tests, and measure clean in
the editor — but neither has been played. The chase fallback in particular has
never been observed firing in a live chase.

## 2026-07-28 — The blood was invisible, then it was gumballs, and the castle now keeps the evidence

**One heart, and the dead wiring that outlived it.** `PlayerHeartbeat` had already
taken over the lub-dub, but `HidingConfig` still carried `heartbeatRange/farBpm/
nearBpm/heartbeatMaxVolume`, `HealthVfxDirector` still carried `heartAudio/
heartbeatClip/heartVolume`, and the scene still held two idle AudioSources nothing
played. All removed. `Setup/22` and `Setup/25` stopped creating them — and stopped
**destroying and rebuilding their whole rig on every run**, which had been quietly
deleting a `DamageSfx` object nobody put back. New `Setup/37` clears the orphans
from existing scenes, refusing to touch anything that is not a childless Transform
+ idle AudioSource.

**`EffectRecipe` grew a second visual path.** `sheetClip` plays a hand-drawn sprite
sheet through the `SpriteAnimator` we already had; `particlePrefab` stays for
dispersal work. A one-shot sheet dies exactly when its last frame has played, so a
mistuned lifetime cannot truncate it. `Setup/38` slices strips into clips and
**preserves any fps/loop tuned by eye** on re-run.

**The hit effect was measured, and it was invisible.** Rendered in the real scene
against the real floor (luminance 10/255), the burst covered **0.26% of the screen
and was gone in 0.75s**. It had been tuned against an isolated neutral backdrop and
then *darkened* for "palette match" — fine in a test harness, invisible in the
game. `Setup/23` had also been preferring CFXR's cartoon prefabs over our own art.
New `TimeKiller/Verify/VFX Visibility` measures coverage in-scene so this is never
guessed again: **0.26% → 1.52% → 3.53%, still visible past t=1.1s.**

**Then the droplets were gumballs.** The first redraw was supersampled metaballs at
saturation 0.93 and value 165, with a radial specular — against game art that
measures **median value 52, median saturation 0.07, hard pixel edges**. Rejected,
correctly. Redrawn as pixel-art spatter: **no antialiasing** (alpha is only 0 or
255), **four quantised colours**, irregular torn silhouettes with tendrils and
satellite specks. Authored at **32px cells** so a particle at size 1.0 draws 1:1
against the 32 px/unit world, Point-filtered. Count is the knob that decides wound
vs paint bomb — 46 spatters buried the player; 24 + 5 gouts reads as a hit.

**Blood now flies away from the blow.** `PlayerHitEvent` has carried
`SourcePosition` since the shove was added and the effects side never read it, so
every wound sprayed as a symmetric ring. `EffectPlayer.Play` takes an optional
direction and rotates the emission wedge, reading the wedge width off the prefab's
own shape module so the two cannot drift apart. Droplets also orient along their
flight path now — stretch kept tiny on purpose, because it distorts the quad and
smears pixel art.

**New feature: `C#/Blood` — the castle keeps the evidence.** You bleed at or below
2 HP, faster as you near death, and the floor remembers for the whole run. Every
stain is **one mesh**: a fixed ring buffer, one draw call and zero per-spill
allocation whether there are 3 stains or 220, oldest recycled so a long run cannot
degrade. Rotation is quantised to quarter turns (arbitrary angles resample off the
pixel grid); drying refreshes at 5Hz and **only while something is still wet**.
`BloodTrail` publishes, `BloodStainField` draws, neither references the other —
either is deletable alone. Sorting order −6, measured against the scene rather than
guessed. **Maniac tracking is approved but deliberately not wired yet**, so the
look can be judged before the difficulty changes.

## 2026-07-28 — The maniac makes a sound now, and relief has to be earned

**The relief breath was firing mid-chase.** It played the instant
`Perception.Level` left `Detected` — but `Detected` drops every time line of
sight breaks, which behind a pillar happens constantly while he is still
actively hunting you. So the "I got away" exhale was going off repeatedly during
the chase it was supposed to end. The chase ending now only **arms** it; he must
stay off you for `recoveryDelaySeconds` (**8.5s**, user ruling) or re-acquiring
you cancels it.

**Delaying it alone would have broken it.** Measured against the shipped config,
exertion decaying at the normal rate sits at **0.229** after 8.5s against a
`silenceBelow` of **0.22** — a margin of 0.009, so the exhale would have landed
out of dead silence. `settleDecayScale` 0.35 holds panting up through the wait
and it lands at **0.730** instead. The `recoveryNeedsExertion` gate also moved to
*arm* time, so the waiting cannot cancel a breath that was earned: a 2s scare
reaches 0.331 and correctly arms nothing, a 5s chase reaches 0.831 and does.

**The maniac had no AudioSource at all.** Not one, anywhere under `C#/Maniac/`.
The first information the game ever gave you about him was seeing him — which is
why hiding measured as inert in the bot playtests. `ManiacVoice` gives him a 3D
breathing bed whose volume reads as distance, plus state-driven growls, boots,
and rare idle mutters. Non-verbal only: he never speaks.

| distance | 14u | 9u | 6u | 4u | 2u | 1u |
|---|---|---|---|---|---|---|
| breath volume | 0.0000 | 0.0735 | 0.1840 | 0.2578 | 0.3315 | 0.3500 |

**Unity's 3D rolloff could not have delivered that.** The `AudioListener` rides
the Main Camera and `CameraFollow` preserves its authored Z — measured at
**−10** — so Unity scores the distance to him as `sqrt(d² + 100)`. Standing on
top of him reads as **10.0u**, leaving one usable unit inside an 11u hearing
radius. All three sources now use a flat custom curve and the component does its
own 2D attenuation.

**`PlayerVoice`** covers pain, the spotted gasp, repair effort and the death cry.
Pain keys off `PlayerHealthChangedEvent` falling, **not** `PlayerHitEvent` —
that one fires even inside the invulnerability window where it does nothing, so
it would have the player cry out for damage they never took.

**Footsteps had to be cut by hand.** Every "steps" file in the PSX pack is a
walking *sequence* (tunnel 8.1s, mud 31.6s) and the system triggers one clip per
stride. `Tools/AudioPipeline/slice_footsteps.py` cuts eight single steps out of
`tunnel steps.wav`. Its first pass silently merged steps — a share-of-peak
threshold tuned for the loud ones dropped the quiet ones (amplitudes span
4640–18337), leaving 0.96s and 1.46s gaps against a ~0.48s stride. An
80th-percentile threshold plus a 350ms refractory fixed it: **17 onsets, median
gap 0.486s, range 0.451–0.527s, zero missed.**

**The voice set is generated and wired — 27 clips, and fal.ai was never needed.**
Unity's built-in audio backend is unconfigured, but Higgsfield was already on a
paid plan, and growls and grunts are *voice*, which its speech models cover.
Cost: **~5.4 credits of 20.27**. Maniac at `pitch_rate -12`, player at −4..+3, so
the two are unmistakably different throats.

**Raw generator output spanned −12.4 to −52.2 dB RMS** — a 40 dB spread that
would have left `maniac_mutter_3` inaudible beside `maniac_attack_2`.
`Tools/AudioPipeline/process_voice.py` trims, mono-folds and **loudness**-matches
to −12 dBFS with a tanh soft limiter; 24 of 27 now land within 1 dB. Peak
normalising was tried first and rejected by measurement — it left the first batch
7 dB under the heartbeat, because a clip with one sharp transient peaks the same
as a sustained one.

## 2026-08-03 — TMP fonts baked to a Static atlas (they were churning in git)

Both font assets shipped in TMP's **Dynamic** atlas mode, which adds glyphs the
first time each character renders and writes them back into the `.asset`. The
files therefore changed whenever anyone played — measured as **826 deleted lines**
of glyph entries in a single session's diff. On a shared repo that is a recurring
conflict on a file nobody edited.

`Setup/46` bakes them once and switches to **Static**. Order matters and is the
whole trick: a Static atlas contains only what was baked, so the glyphs are
generated *before* the mode is switched. Flip the mode first and every uncached
character renders as nothing.

Verified: played a session with every label force-rendered including the pause
menu, then checked git — **the font assets no longer change**.

⚠️ **Both fonts only contain 76 of 95 printable ASCII characters.** Missing:
``#$%&*<=>@[\]^_`{|}~`` and every typographic extra (em-dash, curly quotes, …).
That is not a bake failure — the glyphs are absent from the AI-generated font
files themselves. All current UI copy was checked string by string and is fully
covered, but **new copy using `%`, `&`, `@` or an em-dash will render blank**, and
in Static mode it fails silently. Re-run `Setup/46` after adding any font.

## 2026-08-03 — Reverted: the attack "contact-stop" was never a bug

`AttackState` was changed to keep closing at half chase speed instead of calling
`Motor.Stop()`, because two recorded chases showed his speed collapsing to
0.40–0.54 u/s on contact while a running player gained 4.8u and 6.2u. **Measured
across two further sessions, the change did nothing** — 31/38 and 34/43 attack
samples still at zero displacement. Reverted, and `attackMoveShare` deleted rather
than left as a config nobody reads.

**Why it could never have worked:** their capsules touch at **0.58u** and
`attackRange` is **0.9u**, so he is pressed against the player for the whole swing.
The motor writes `linearVelocity` directly and physics cancels it against the
contact — the same trap the beeline stall-check already documents for walls.

**And the ground he loses is the feature working.** Player speed after a hit
measured **10.0–11.67 u/s against a 4.5 run cap** — that is `BeginPhaseThrough`,
the designed adrenaline escape. The pathology scan had been flagging a mechanic as
a defect.

The finding is written into `AttackState` and `ManiacConfig` so the next person
does not repeat the investigation.

## 2026-08-03 — The maniac gets a second brain

**Researched before building.** Alien: Isolation runs two brains: a **director**
that always knows where the player is and periodically points the creature at
their AREA, and the creature itself, which knows nothing and must use its own
senses. The director never hands over the position — it only steers. Players
trust that game precisely because it never cheats.

**The problem it fixes was measured, not assumed:** a recorded bot session ran
**80 seconds with zero detections**. When the maniac loses you, his belief map
decays, he returns to patrol, and the encounter is simply over. No amount of
sense-tuning fixes that, because nothing existed to bring him back.

**`ManiacDirector`** (`C#/Director/`) issues a hint after `hintAfterQuietSeconds`
of genuine quiet — he must be Unaware, and at least `minHintDistance` away, so it
never piles onto something already happening. The hint is a **smeared point plus
a radius**, never a position.

**The dial that keeps it honest is `hintError` (9u) against his `sightRange` (7u).**
If the error ever drops below the sight range, arriving at a hint *becomes*
finding the player and the Director has started cheating whatever the code says.
Setup/45 prints this comparison every run. Nothing in the feature writes his
awareness, `LastSeenPosition` or belief map.

Verified live: 6 hints over ~90s of a stationary player; the accepted hint landed
**17.2u from the real position**; he travelled 31.7u → 7.1u to it and then found
the player **himself** — ending Unaware→**Detected**, Patrol→**Chase**, 4.3u away.

**He learns from what you DO, never from dying.** Isolation gates behaviours on
player metrics; gating on deaths would punish losing, which is the one thing that
reads as unfair. Hides that you actually got away with (he never found you during
it) raise the wardrobe check chance past `hidesBeforeLearning`, capped at
`maxWardrobeBonus` 0.35 so hiding can never become useless — the fix for the
"hiding is inert" finding (81.8% vs 81.9% death) is to make it a decision, not to
remove it.

**Doubt.** `searchDoubtChance` 0.3 makes him take the second or third likeliest
cell instead of the best. A searcher who always walks to the optimum reads as a
pathfinder, not a person. `PlayerBeliefMap.RankedTargetBeyond(rank: 0)` is
byte-for-byte the old behaviour, so the feature is off at 0.

**Removability was designed in, and it changed the file layout.** `ManiacHintEvent`
and `ManiacLearnedEvent` live in the **Maniac** feature, not the Director that
sends them: declared in Director, deleting that folder would stop the maniac
compiling — exactly the coupling the removability rule exists to prevent. The
Director depends on the Maniac; the Maniac depends on nothing. Delete the object
and he behaves exactly as before.

38/38 EditMode tests still pass.

## 2026-08-02 — Every label is TextMeshPro now

**All 15 labels were legacy `UnityEngine.UI.Text`**, which renders from a bitmap
atlas baked at one size — fine at 24pt, visibly soft at the sizes this game uses:
the run-end Headline is **96pt**, the pause Title 78pt, the clock counter 64pt.
Legacy Text also has no outline, and all of this type sits over a dark scene, the
blood overlay and the hiding slats, where an unoutlined glyph loses its edge
against whatever is behind it. TMP was already installed (it ships inside
com.unity.ugui) and simply unused.

`Setup/44` builds SDF font assets from the two project TTFs, swaps every label,
and re-wires the serialised references in one pass — it has to be one pass,
because changing the runtime fields from `Text` to `TMP_Text` makes every stored
reference null with nothing left to trace it back to.

**Two failures found by running it, both worth writing down:**

1. **`tmp.outlineWidth` throws on a freshly added component.** `SetOutlineThickness`
   dereferences a material instance that does not exist until the component first
   renders, which never happens at edit time. It killed the loop after 2 of 15
   labels. The outline now lives on the **font asset's material** instead — same
   look, no per-label material instance, and batching survives.
2. **Re-wiring by dead instance id does not work.** Once a field's type is
   `TMP_Text`, the stored id no longer resolves, so every field reads null with no
   trace: measured **0 of 6 re-wired**. It matches by convention now — a `TMP_Text`
   on the component's own object, else a label whose name matches the field name
   — and only ever fills fields that are already null.

**And the fallback that convention needed:** `ObjectiveHUD` lives on
`Clocks/ObjectiveManager`, entirely outside any canvas, driving
`ObjectiveHudCanvas/CounterPlaque/CounterText` from there. Searching only the
component's own canvas found nothing and failed silently on exactly one field. It
now falls back to every canvas in the scene.

Verified: 0 legacy Text, 15 TMP labels, 8/8 references wired, outline 0.18 with
the keyword enabled on both font materials, and labels building real meshes in
play mode (CounterText 5 glyphs, Title 6). ⚠️ **`MainMenu.unity` still needs
`Setup/44` run in it** — the migration is per-scene.

## 2026-07-28 — You can pause now, and set the volume

**There was no pause and no audio settings.** An 11-channel mix had been built and
a player had no way to touch any of it, or to stop the game at all.

**`PauseMenu`** — Esc, one flat panel, three sliders (Master / Music / Sound) and
Resume / Quit. No nested settings screen: three sliders behind two extra clicks is
worse than three sliders. Built by `Setup/43` from art already in the project —
`EndFrame` for the panel and **`Bar.png` for the slider tracks, which had been
imported and used by nothing**. No new art: the direction is locked and this
screen has no business inventing a second look.

**Esc was free, and it was worth checking.** `GameFlow` binds Esc as its quit key,
but only reads it once `Phase != Playing` — so it quits from the end screen and
does nothing during play. Pause takes it during play and hands it back when the
run ends; the menu also force-closes if a run ends underneath it.

**Player volume is a separate layer from the mix, on purpose.** The sliders write
to **PlayerPrefs**, never to `AudioMixConfig`. Writing a player's slider into the
ScriptableObject would dirty the asset, overwrite tuning done by ear, and get
committed on the next push. Verified in play mode: with the music slider at 0.25
the music gain went 0.850 → 0.213 and master 0.5 stacked it to 0.106, while the
config's Music level stayed at 1.00 with `assetDirty=False`.

**Audio deliberately keeps playing at full level while paused** — sliders are
unusable if you cannot hear what they do. Duck timing moved to `Time.unscaledTime`
so a duck that was running when you paused expires instead of freezing, which
would otherwise have had players setting the music slider against a ducked level.

`Setup/43` also creates an **EventSystem** if the scene has none — the existing HUD
canvases are all display-only, so uGUI input had never been needed before and
would have silently done nothing.

## 2026-07-28 — Carving the low end, and the Mystery layer finally fires

**Researched before touching anything.** The literature that matters here is
**upward spread of masking** — low frequencies mask higher ones far more than the
reverse, a property of the basilar membrane, and the masking curves *widen* as
level rises. Also relevant: roughness (30–150 Hz amplitude modulation, not pitch)
is what makes screams read as danger, and 2–4 kHz is the reflex band.

**The "19 Hz fear frequency" is a myth and is deliberately NOT used.** It traces to
a single 1990s anecdote (Tandy) that never replicated; the follow-up measured
38 dB at 19 Hz, roughly **50 dB below the perception threshold** at that
frequency. Building sub-audible content on that basis would spend headroom on
nothing.

**Finer measurement moved the target.** The earlier "below 300 Hz" framing was too
coarse — the real pile-up is **below 60 Hz**: chase music 61%, heartbeat 60%, his
mutters 66%. And laptop speakers cannot reproduce below ~150–200 Hz at all, so
**81% of the chase track's energy never reached most players** while still masking
everything above it on headphones. The heartbeat learned this exact lesson in an
earlier pass (v1 was 97% below 100 Hz and inaudible); the music had never been
checked.

**`MusicEq`** high-passes the music at **80 Hz with two cascaded biquads
(24 dB/oct)** plus a −3 dB dip at 220 Hz, applied live via `OnAudioFilterRead` on
the music sources only — the stings measured 5% below 60 Hz and are left dry.
A single 12 dB/oct stage at 55 Hz was tried first and rejected by measurement: it
only took the chase track from 61% to 51%, because it barely touches 40–60 Hz
where the energy actually sits. Done at runtime rather than by baking 20 tracks,
which would have added ~530 MB to a repo already carrying a 1.6 GB pack.

Measured across all six layers: **sub-60 energy fell from 37–66% to 9–21%.**

**The EQ's cost was uneven (−2.3 to −7.3 dB) and broke the ladder** — mystery
dropped to −35.1, below even safe. Layer volumes were therefore recalibrated on
the **post-EQ** RMS, which is the correct order, and the music channel level went
to 1.00 to give back what the filter removed. Ladder now safe −34, dread −31,
mystery −28.5, investigate −26, endgame −25, chase −23.5. **Dread → chase 7.5 dB**
(it was 1.6 dB before any of this work).

**A wrong hypothesis, corrected by measurement.** His mutters at 66% sub-60 were
blamed on the monsteriser's sub-octave. They were not: the raw, un-monsterised
take is already **53% sub-60** — it is a slow low hum in the source, and cutting
the sub-octave made it no better (one variant measured *worse*). The fix that
works is the same high-pass, at 130 Hz, applied *before* the loudness normalise so
the audible part gets lifted: **66% → 15–32% sub-60, and 300–800 Hz up from 5% to
18–31%.** Only the four mutters changed; the approved growls and roars are untouched.

**The Mystery layer had never once fired in real play.** Its only entry was the
demo `Rect(0,0,3,3)` that `AudioConfig` itself asked someone to replace. It also
could not have worked properly, because `mysteryZones` lives in ONE asset shared
by CastleWingLDtk and Catacombs — a rect authored for one map is live in the other.

**`MusicZone` moves zones into the scene**, with gizmos, so they are draggable and
belong to the map they were placed in. Config rects are still read (the servant-
passage safe zones are untouched), so this is purely additive. `Setup/42` derives
placement from the scene: a zone on each **clock** — committing to a repair pins
you in place and owns your attention, so the layer should already be running
before you commit — and one on the **exit door**, which only fires before the gate
opens because Endgame outranks Mystery. Wardrobes were deliberately excluded:
hiding is a reaction, and seven of them would leave Mystery permanently on, and a
layer that is always on means nothing.

Verified in play mode: Dread at spawn and in corridors, Mystery at and approaching
all three clocks and the door, clean boundary at the edge. 4 zones, no overlaps,
377 sq units, and the player does not spawn inside one.

## 2026-07-28 — Full audio audit: the sting nobody could hear, and a 1.6 dB tension ladder

Every wired clip measured for RMS, peak and a three-band energy split, then
combined with its config volume and mix gain to get the **effective in-game
level** — the only number that matters, and one nobody had ever computed.

| sound | before | after |
|---|---|---|
| jumpscare sting | **−38.9 dB** | −17.6 |
| maniac breath | **−30.8 dB** | −15.9 |
| chase music | −27.7 | −21.0 |
| heartbeat (unchanged) | −10.7 | −10.7 |

**The jumpscare was the quietest thing in the game** — 28 dB under the heartbeat.
And `AudioMix` makes it tier 0, so every other channel was ducking to clear space
for a sound that could not be heard. Raising `stingVolume` could not fix it: at
0.70 the entire remaining range is +3 dB against a 20 dB shortfall. The fault was
in the files, so `Setup/41` normalises them (through Unity, because several are
.mp3 and Python's `wave` cannot read those). Originals are untouched; normalised
copies live in `Assets/Assets/AudioNormalized/` and the configs re-point.

**His breath sat 20 dB under the player's own heartbeat** — the warning that is
supposed to let you hide *before* being seen, buried under your pulse, and both
sub-300 Hz so it was masked twice. Now 5 dB under, and 5 dB *above* the chase
music instead of 4.6 dB below it.

**The tension ladder was 1.6 dB.** The music is meant to BE the threat detector,
yet dread → chase changed the level by less than a decibel and a half. Layer
volumes recomputed from each layer's measured average RMS to hit a real
progression — safe −33, dread −29, mystery −26.5, investigate −24, endgame −22.5,
chase −21. **Dread → chase is now 8.0 dB.**

**Still open, and deliberately not guessed at:**
- `WAV_MENU_FULL_Systolic_Menace` — a **menu** track — is wired as chase music.
  Replacing it is a taste call, not a measurement.
- Track levels inside one layer span up to **7.2 dB** (investigate −26.2..−33.3),
  so a random pick changes how loud the same threat sounds. Fixing it properly
  needs a per-track gain array in `AudioConfig`.
- **62–93% of every music track's energy is below 300 Hz**, as is the heartbeat
  (92%) and his monsterised mutters (93%). The whole game is competing for one
  octave. This is structural and is the next real piece of work.

## 2026-07-28 — Two rejections by ear, and the mix owed the player's lungs an apology

**The mix had been trimming the player's own body like background.** `PlayerBreath`
sat at level 0.75 / duckDepth 0.45, which put the recovery breath — the entire
reward for surviving a chase, and a sound already approved — at **0.638** normally
and **0.287** while anything else spoke, i.e. 36% and 71% quieter than the version
that was signed off. Now level 1.00 / duck 0.90: **0.850**, which is 1.4 dB off
instead of 3.9 dB, and the rest is the global headroom trim that everything shares.

**⚠️ `seed_audio` defaults to a FEMALE voice when no `voice_id` is passed.** The
whole first maniac set was generated without one, so `pitch_rate -12` was simply a
pitched-down woman — rejected by ear as "normal girl's voice going haaa". All 13
of his vocals regenerated with **Roman** (male preset, user's pick from a
three-way comparison).

**A deep male voice was not enough on its own** — a pitched-down man still reads
as a man. `Tools/AudioPipeline/monsterize.py` adds the three things no TTS
parameter offers: a **formant-shifting** pitch drop by resampling (−3.9 semitones,
moving throat and skull resonances down *with* the pitch, so it reads as a bigger
body and not a slowed tape), a **sub-octave** for weight, and a **detuned double**
whose beating is the strongest wrongness cue available — a real throat cannot
produce two pitches at once, so the ear refuses to hear one person. Player voices
are untouched: they were approved as they were.

## 2026-07-28 — Nine sounds wanted the same instant, so the mix got a priority

**Measured before building anything:** during a chase the linear amplitudes of
nine simultaneous sources summed to **6.72× full scale**, and Unity hard-clips
above 1.0. Three of them — heartbeat 1.00, maniac breath 0.85, growl 0.90 —
stacked to **2.75 below ~300Hz**, masking each other into mud. Volumes lived in
**eight separate config assets**, so the balance could not be seen, let alone
set, and **6 of ~12 sources ignored `AudioDucking` entirely**.

**`AudioMix` adds the missing idea: priority.** Tier 0 Sting → 1 vocals → 2
maniac breath / heartbeat → 3 footsteps-his / breath / effects → 4 your footsteps
→ 5 music / ambience. A channel ducks only for **strictly** lower tiers — equal
tiers never duck each other, or the mix would flip-flop on whichever arrived
last. Duck length comes from the clip via `Announce(channel, clip.length)`, called
*before* `PlayOneShot`: a fixed window is either too short for a death cry or too
long for a footstep, and announcing afterwards ducks for a sound already buried.

It returns a **multiplier**, never an absolute level, so every hand-tuned volume
survives (`chaseVolume` 0.42 and `stingVolume` 0.70 were set by ear and are kept).
No config → every call returns 1.0 and nothing changes. Unity exposes no public
API to build AudioMixer groups from script, so a mixer would have had to be
hand-built in the editor; a code bus stays scriptable, which is why the numbers
below exist at all.

| | before | after |
|---|---|---|
| peak-sum | 6.72 | **3.01** |
| power-sum (uncorrelated sources add as power) | 2.21 | **1.20** |
| low band <300Hz | 2.75 | **1.28** |

Runtime-verified: a sting pulls music 0.680→0.516 and falling while **footsteps
hold flat at 0.723** — they are trimmed by the mix but never ducked, because
muting the feedback you steer by reads as a bug rather than as tension. The heart
falls 0.519→0.173 as his breath rises, then clamps: they cannot both own the low
band, and information beats emotion — his breath says *where he is*, and the
heart's **rate** still carries proximity at any volume.

`Setup/40` creates the config and **never overwrites an existing one**; it is
meant to be tuned by ear in Play Mode. F1 line `Mix`.

## 2026-07-28 — Four defects in the maniac's arithmetic, and the body that hears him

**The maniac's problems were never in the pathfinding.** All four were in the
utility weights and in the "he has stopped moving" case, and all four were found
by replaying the shipped config's own maths rather than by watching him.

| defect | measured | fix |
|---|---|---|
| Heard to 9u, only *reacted* within 6.2u (2.0u for a 3s-old noise) | `investigate = 0.8 × recency × (1 − d/9)` could not beat Patrol 0.15 + stickiness 0.10 | `noiseFarWeight` 0.55 — distance biases, never vetoes |
| Blind past ~4u, and forgot 4× faster than he learned | 6u in shadow needed 9.1s of unbroken exposure; a full meter drained in 1.25s | `sightFalloffPower` 2, drain 0.8→0.35, new `awarenessHoldSeconds` 1.2 |
| Ignored a footstep 1u away for the first ~5s of a hunt | Search 0.98 vs a point-blank footstep 0.71 | `brainFreshNoiseBoost` 1.6, gated to the hunting window |
| "Pausing to look around" never moved his eyes | `FacingDirection` is only written from velocity | `SweepCone` on `ManiacStateBase`; Patrol and Investigate now scan |

**`CanSeePlayer` now requires LIVE contact, not a full meter.** The new awareness
hold would otherwise keep it true through a wall and send `ChaseState` beelining
into geometry — silently undoing the chase-pathfinding fix from 2026-07-24.

**Suspicion was not suspicion.** At `Awareness >= 0.4` perception called
`SetNoise(player.position)` *every frame*, handing the AI the player's exact live
coordinates, and `investigateSpeed` 3.0 outruns the player's walk of 2.2. Being
half-noticed was identical to being seen, and it could not be escaped. Now he
commits to a **guess** — offset by `suspicionGuessError`, direction rolled once
per episode and held, error shrinking to zero as he grows certain — then **stops
and stares for 1s** before closing at 1.4, below the player's walk. Verified
error: 2.73/2.75, 1.98/2.00, 0.98/1.00, 0.23/0.25.

**The stare had to stop being latched.** Component update order is undefined, so
reading `LastNoiseCause` in `Enter()` could see the previous frame and skip the
hesitation entirely (measured 0.03s against a configured 1.00s). Perception now
exposes `SuspicionStartedTime` and the state recomputes every frame.

**HealthVfx post-processing had never once run.** `volume.profile` writes a
runtime-only field — `sharedProfile` is the serialised one — so the assignment
died on every scene reload, and `VolumeProfile.Add<T>()` without
`AddObjectToAsset` never wrote the overrides to disk either. Vignette, chromatic
aberration, grain and desaturation all resolved to null. Both halves fixed, and
Setup/22 now round-trips the profile through disk and logs an error if it
reloads empty.

**The player now has a body.** One heartbeat clock and one voice, replacing two
unsynchronised ones (`HidingVfx` and `HealthVfxDirector` each ran their own).
Distance drives the rate continuously on a curve; Suspicious and Detected raise
floors under it; detection ducks the score to 5% via `AudioDucking`, a voluntary
dial the music respects. It climbs in 0.5s and falls in 4.2s — being safe and
feeling safe are deliberately different things. Breathing is **exertion, not
fear**: only an active chase winds you, and when he loses you one normalised
recovery breath plays while the heart steps back to let it through.

**The heartbeat sounds were synthesised in-house** (free, no licence to track).
Three measurement-driven revisions: v1 put 97.2% of its energy below 100Hz and
was inaudible on normal speakers; raising the fundamentals fixed that; a soft
limiter took it from RMS 0.125 to 0.402 (+10.1 dB perceived). The PSX pack's
"Deep breath" was 31 dB below the heartbeat and was really five breath cycles,
so one breath was cut from it and normalised (+19.6 dB).

**First tests in the project.** 20 EditMode tests over `ManiacBrain.Score` and
the new pure statics on `ManiacPerception` (`RateFor` / `StepAwareness` /
`GuessErrorFor`), running in 0.65s with no scene, no frames and no physics. Two
were checked against the pre-fix config and provably fail on it, so they are
real regression detectors. Also new: **F4 vision-cone overlay** (central,
peripheral, point-blank ring, and his suspicion guess), `TimeKiller/Verify/Probe
Maniac Senses`, and `Setup/35` to serialise the 15 detection fields that had
never been written to `ManiacConfig.asset`.

**Also landed, and NOT yet playtested by the user:** wardrobe search — the maniac
can open a hiding spot mid-hunt when his belief map is concentrated there
(`ManiacWardrobeSearch`, `WardrobeSearchConfig`, `Setup/37`), routed through the
same compromised-spot path as being watched climbing in. This is the other half
of making hiding matter; it compiles and the suite passes, but it has not been
played.

## 2026-07-25 — The run cycle existed all along, and walking was secretly sprinting

**The run animation was never missing — it was never exported.** The 8-direction,
8-frame `running-8-frames` set had been sitting completed on PixelLab since
2026-07-24. Locally there were only `Idle`, `Rotation` and `Walk` strips, so
`SurvivorAnimationSetup`'s fallback (`runSource = HasSheets("Run") ? "Run" :
idleSource`) quietly built every `Run_*` clip out of the **idle pose** and logged
a warning nobody was reading. Sprinting played standing still. Downloading the
64 frames and rebuilding cost nothing and fixed it outright.

**Then the probe found the thing the eye would not have.** `Walk` and `Run` were
both authored at 14 fps with contacts on frames `[2,6]` — identical. Same clip
length, same contact frames, same rate: the legs turned over **3.5 times a
second whether you crept or sprinted**, while the body moved 2.2 vs 4.5 u/s.

Measuring the art instead of guessing gave the correction. The side-view strips
depict a **0.41-unit walk stride and a 0.48-unit run stride**, so keeping the
feet planted would need 5.4 and 9.3 steps/s. 9.3 is a blur, so the fix takes
most of the correction and stops short of all of it:

| | was | now | foot slide |
|---|---|---|---|
| walk | 14 fps (3.5 steps/s) | **18 fps** (4.5) | 35% → **17%** |
| run | 14 fps (3.5 steps/s) | **24 fps** (6.0) | 62% → **35%** |

Cadence is now a named constant per gait in `SurvivorAnimationSetup`
(`fps = frames × steps ÷ 2`, because one cycle is two contacts) rather than one
shared `frames × 1.75` that run was tuned for and walk inherited by accident.

**The stealth scare was a false alarm, and that is worth recording.** The
identical rate looked like a balance bug, but `ManiacPerception` gates a footstep
on `hearingRadius × loudness` — 9×0.3 = **2.7u** walking against 9×0.85 =
**7.65u** running. Detection was never carried by the rate; sneaking already
worked. This was a *looks* bug wearing a gameplay bug's clothes. Contact frames
`[2,6]` turned out to be right on the money for the clean side views, so the one
part that was suspected of being wrong was the one part that was correct.

**Clock repair now looks like clock repair.** New PixelLab `Repair` set (8
directions × 6 frames, standing, hands working at chest height — the clock is
~1.6u tall, so its mechanism sits at the survivor's chest, not at their feet)
and a `RepairState` nested in `ClockRepair`, mirroring `PlayerHiding.HidingState`.
Starting a repair turns the player to face the clock and **locks them there**
(DbD-style commitment, user ruling): the state has no transitions of its own, so
movement input is simply ignored, and that *is* the lock. The repair clips carry
no `eventFrames`, so standing at a clock broadcasts no phantom footsteps.

Two seams added to make that safe rather than ad hoc: `PlayerController`
`.CurrentState` / `.IsFreeToInterrupt`. A feature takes the player only when free
(so pressing E inside a wardrobe still belongs to hiding), and hands control back
only if it still holds it (so two features cannot fight over returning to Idle).
Facing is now driven by input only while free — otherwise WASD spins a locked
player's pose on the spot. `PlayerAnimationDriver` matches `"RepairState"` **by
string, not `nameof`**: the player must not hold a compile-time reference to a
feature that is meant to be deletable.

**New: `TimeKiller/Verify/Probe Player Animations`** — play-mode assertions for
what screenshots cannot show. It reports all 16 octant×gait combinations, the
measured contact frames and real step rate per gait, and the repair lock
(drift under held run input, and release on E). Results: 16/16 correct,
`busEvents == contacts` in both gaits, `drift = 0.000u`, `RELEASED OK`.

Two Unity traps are baked into it, both hit for real on the way here: entering
play mode reloads the domain and **silently drops** a `playModeStateChanged`
subscription registered beforehand (the request now travels in `SessionState`),
and the probe's `[DefaultExecutionOrder(-200)]` is load-bearing — a key pulse
spanning two frames is read twice by `ClockRepair`, and its second read means
"walk away", cancelling the repair it had just begun.

Also worth knowing: **Setup/33 must be run out of play mode.** Its scene-attach
step cannot mark the scene dirty while playing, so it half-completes — the
ScriptableObject updates and the scene wiring does not.

## 2026-07-25 — The player has a face again, by not having one

The hero art pipeline was abandoned in July after ~58 generations: AI 8-direction
rotation kept re-interpreting a detailed painting, and the Watchman shimmered
between angles. The fix was not a better prompt — it was a **design that gives
the tool nothing to be inconsistent about**. A deep hood with a black void
instead of a face has no eyes, nose or mouth to drift. Every candidate came back
stable across all 8 directions, varying by at most 1px in height.

The player is now a **hooded, faceless survivor in a grey-green coat**, replacing
the borrowed New_Leaf placeholder.

Two measurements drove the whole build, and both contradicted an assumption:

- **The world is 32 pixels per world unit and its characters are 29px tall.**
  PixelLab's default 48 produces a 44px character that stands **52% taller than
  the maniac** — the victim looming over the killer. Caught by measuring the
  maniac's sprite rather than eyeballing a composite.
- **The level averages 11/255 luminance.** A worry that a dark hooded figure
  would vanish was simply wrong: the character sits at ~39, making it the
  brightest thing on screen after the clock face. Hue contrast, not brightness,
  is what separates the hero from the stone.

New this pass:

- `Tools/CharArt/build_sheets.py` — converts a PixelLab bundle (one PNG per frame
  per direction) into the horizontal strips Unity slices. Direction names map 1:1
  onto `FacingDirection`'s declaration order, so no lookup table is needed.
- **Setup/33 — Generate Survivor Player Animations.** Slices, builds 24 clips
  (idle + walk + run x 8), fills `PlayerAnimationSet` and attaches it.
  **Must be run out of play mode** — asset reimport throws there, and the menu
  item still reports success, so the failure is silent.
- **Pivot is derived from pixels, not hardcoded.** PixelLab centres a small
  character in a large padded canvas, so a plain centre pivot sinks it into the
  floor. The importer finds the lowest opaque pixel across the *whole strip* and
  places the pivot 17px above it — the placeholder's exact ground offset, which
  is why the swap needed no collider, camera or nav-grid changes. Measuring
  across the whole strip (not per-frame) is what keeps every frame on one ground
  line instead of bobbing through the floor mid-cycle.
- **Real walk cycles.** `PlayerAnimationSet` gained an optional `walk[8]`; at
  walkSpeed 2.2 vs runSpeed 4.5 the old "play the run clip at half speed" trick
  read as slow-motion arm pumping. `PlayerAnimationDriver` now normalises
  playback against whichever clip is playing — dividing a walk cycle by runSpeed
  would halve its rate and slide the feet. Packs with no walk art fall back to
  the old behaviour, so Setup/5 and the New_Leaf placeholder still work.

Verified in-engine, not assumed: all 24 state x direction combinations resolve to
the correct clip at runtime, and all 8 directions face the right way on screen.

**Still to tune:** walk and run currently share a 14fps cadence, so both step
~3.5 times/sec and differ only in stride. Those `eventFrames` are what the maniac
hears, so the cadence is gameplay — it wants a play-test, not a formula.

## 2026-07-25 — The tension instrument was measuring its own thresholds

First traced batch (`results/2026-07-25_025149.jsonl`, 7 runs, Bot_average, **1x**,
sampler healthy at 96% of its 4 Hz). It reported a calm game: chase 4.2% of all
play time, dread **1.3%**, `nearMissHidden` **0 in every run** against 25 hides,
and **74% of every threat-curve bucket flat at zero**.

None of that survived being read next to `ManiacConfig.asset`. The three
thresholds had been typed in — 4 / 12 / 0.25 — beside a config that says
`sightRange 7`, `hearingRadius 9`, `suspicionThreshold 0.4`. Two were wrong, and
wrong in a direction no amount of batch data could have exposed:

- **`FeltRadius` 12 sat three units beyond his maximum sensory reach.** Past 9 he
  cannot perceive the player by any channel, so the 9–12 band counted as neither
  "felt" nor "dead air" — a limbo that *undercounted* the one boredom metric this
  instrument exists to expose. Correcting it makes the headline number worse: the
  game is quieter than the old instrument admitted.
- **`AwareThreshold` 0.25 sat below the game's own `suspicionThreshold` 0.4.**
  Below 0.4 he does not investigate, does not turn, does nothing observable. So
  "dread" spanned a band containing no behaviour.
- **`NearMissRadius` 4 was the one that was fine** — it sits inside `sightRange`.

All three are now **derived from `ManiacConfig` at run start**, which also ends
the silent drift: a copied constant does not follow a retune, and nothing would
have failed. Each row records the thresholds it was measured with, and
`analyze.py` refuses to average two sets rather than quietly blending tunings.

**Dread was measuring the drain rate, not the level.** `awarenessDrainRate 0.8`
fixes the fall from spotted back to the threshold at `(1 − 0.4) / 0.8 = 0.75s`
per lost contact, every time — so at ~4 spots per run, dread could never have
been more than a few seconds regardless of level design. It was the forgetting
tail wearing the name of the stalking beat. Suspicion is now accumulated **inside
`ManiacPerception`, per frame** (at `awarenessFillRate 2.6` the band is often
shorter than one 4 Hz sample, so a sampler misses most crossings outright) and
split in two: `stalkSeconds` — still sensing, answers to level design — and
`fadeSeconds` — forgetting, answers only to tuning. The report prints the
structural ceiling beside it so the second can never be read as the first. The
maniac's feel is untouched; only the instrument changed.

**Three near-miss fixes**, all of which had been deleting real encounters:

| was | now |
|---|---|
| any hit **voided** the episode | third bucket, `nearMissClipped` |
| hidden if hidden ≥50% of samples | classified by how it **ended** |
| `heardNoise` = one counter | `heardSound` / `heardSuspicion` |

The hit filter alone ate most of the file — 13 hits against 4 surviving near
misses. Being clipped and escaping is the classic horror beat, and discarding it
made a violent run read as uneventful. The ≥50% rule scored a dive into a
wardrobe as **open**, because the first half of the episode was spent in the
open — which is exactly backwards, and the likeliest reason `nearMissHidden` was
0 across all 7 runs.

**Threat curve rebalanced** `0.6·awareness + 0.4·proximity` → `0.35·alarm +
0.65·proximity`, where `alarm` is awareness normalised against the suspicion
threshold. Awareness sits at ~0 outside chases, so the old weighting capped a
non-chase sample at **0.40** and left the curve's entire top half unreachable —
seeds 2003 and 2004 peak at 0.37 and 0.40, hitting that ceiling exactly.
Proximity is the term that actually varies, so it gets the larger share.

Backward compatible: pre-split files still parse and say what they cannot show,
rather than guessing. **Not yet re-run** — the numbers above describe the old
instrument, and the corrected one has never produced a batch.

## 2026-07-25 — The wall-stuck claim gets an actual measurement (and the retry cap never worked)

Body-width pathfinding was verified *offline*, by sweeping a collider over 3000
candidate shortcuts. That proves the smoother refuses bad shortcuts; it does not
prove a **live** bot stops scraping, where the maniac shoves, props sit off the
grid, and the gate opens a wall the grid sampled as solid. Nothing measured that,
so the harness now does.

**The bug found on the way in.** `BotPath`'s stuck detector documents "one free
repath, then give up on this destination — grinding a wall forever is the failure
mode this whole class exists to avoid." It did not do that. The retry counter was
incremented, then `SetDestination` reset it to zero on the very next line, so
`++repathsAtThisSpot > 1` **could never be true**. A wedged bot repathed every
1.2s indefinitely. Split the planning in two: `SetDestination` (new goal, fresh
retry budget) and `Plan` (same goal, same spot — no refund).

Left unfixed, this would have pinned the new metric's give-up count at zero and
made both arms look equally healthy.

**Four numbers per run**, written as `stuck` in every result row:

| field | meaning |
|---|---|
| `events` | times the 1.2s no-movement threshold tripped |
| `giveUps` | of those, ones that abandoned the destination |
| `wedgedSeconds` | in-game seconds confirmed wedged |
| `travelSeconds` | seconds with a path active — **the denominator** |

`travelSeconds` matters as much as the count. A bot that wedges less also lives
longer, so raw counts hand the *better* arm a bigger number and make it look
worse. Rates only.

Counters survive the mid-run `BotPath` rebuild that happens when the gate opens
(`RetireNav`). Reading the live instance would have dropped everything before the
escape leg — which is most of the run.

**A/B, because there is no baseline.** No earlier batch recorded any of this, so
comparing against history is impossible. `GridPathfinder` documents
`bodyRadius: 0` as the old hairline behaviour, so the batch runner now takes
**arms**: every seed runs twice, once per pathfinding, same maniac, same
skill-check rolls. The old arm runs **first** — a batch killed halfway then still
leaves the baseline on disk, which is the arm that cannot be reconstructed later.

`analyze.py` keys cells on `bodyRadius` as well as profile and speed. Without
that it would have averaged a baseline into the thing it is the baseline *for* —
the mistake that makes a real change look like it did nothing. New sections:
**WALL-STUCK** (pooled rates + matched-pair sign test) and **CLOCK TIMELINE**
(first exercise of `clockFixTimes`: median time to each clock, and how many runs
fixed everything and *still* lost — an escape problem, not a difficulty one).
Pre-instrumentation files still read correctly; they say so instead of guessing.

**Result** (`results/2026-07-25_022250.jsonl` — 25 seeds × 2 arms, Bot_average,
4x, 0 harness faults, 0 timeouts, the first fully clean batch this harness has
produced):

| | old hairline | body-width |
|---|---|---|
| stuck events / 100s travel | 8.08 | **0.60** |
| destinations abandoned | 73 | **2** |
| time wedged | 9.8% | **0.7%** |
| runs that never wedged | 12% | **80%** |

**22 of 25 seeds improved, 0 worsened.** A 13.5× reduction on identical seeds.
The offline sweep is confirmed in live play.

**The win rate did not move** — 24% vs 24%, with 3 seeds flipping each way. That
is noise, and at n=25 the win rate cannot resolve this. What *did* move: clocks
fixed 2.00 → 2.52, and runs completing all three 11/25 → 16/25. Of those, six
escaped in **both** arms — so the five extra runs the fix carried to "objective
complete" all died on the way out.

Which relocates the problem: **15 of 50 runs (30%) fixed every clock and still
lost.** The gate is the bottleneck, not the walking; bots dying earlier used to
hide that. Flagged for follow-up, not concluded: `spotted` rose 2.96 → 3.80 (14
seeds up, 7 down) — the hug penalty buys the corridor's centre, which is also
where the maniac's sight lines are.

Not validated here: 4x speed on this scene (the batch meant to check it died at
run 11). The A/B survives that — both arms ran at the same speed, so distortion
cancels in the pairing — but the absolute 24% and the clock times do not.

## 2026-07-25 — "Red isn't always impossible": the overlay was answering about the wrong body

Playtest note from the F3 overlay: *green and yellow behave right, but ~5–10% of
the **red** is walkable — dangerous, could collide.* Correct observation, and the
cause was a missing feature, not a wrong model.

`CastleWingLDtk` registered exactly one nav source: the **maniac**. So F3 painted
*his* map — sample box **0.85** — while the player drives a **0.55** capsule.
Measured:

- **186 cells = 4.9% of the player's walkable world** are red on the maniac's map.
  That is the reported 5%.
- **All 186 are YELLOW on the player's own map. Zero are green.** The overlay
  already encoded "walkable but dangerous" — it was just answering about the
  wrong character.

**Checked the scarier reading first:** 43 of those pockets fit the player and not
the maniac. If any were deep enough, you could camp there forever. Against his
real 0.60 collider and 0.9 attack range, the nearest he can physically stand to
the four deepest is **0.0u / 0.1u / 0.3u / 0.4u** — all inside reach. **No
safe-camp exploit.** His grid is conservative; his chase steers directly.

Two fixes:

1. **`PlayerNavDebug`** — F3 now cycles **Player → Maniac → off**. It reads the
   player's actual `CapsuleCollider2D` rather than hard-coding 0.55, because a
   debug view that quietly disagrees with the body it claims to describe is
   worse than none. Grid builds **on first view** (~33k overlap queries), not at
   startup. `PlayerController` attaches it at runtime, so no scene or prefab is
   edited and none can drift out of sync.
2. **Red split in two.** `WalkabilityGrid` now records *why* a cell is
   unwalkable: **red = a real collider**, **grey = no collider at all, merely
   cut off** from the reachable region. On the player map that is 9.8% red vs
   **78.7% grey** — the outside-the-castle void was being painted exactly like a
   wall, which is why so much of the screen looked solid.

The legend now names whose body the colours describe. Red has always meant "for
*this* body" and never "for anybody"; nothing said so.

## 2026-07-25 — You can now SEE where the AI can walk, and it stopped scraping walls

The bot kept touching walls. The instinct was right and the cause was not in the
bot: it was in `Navigation/`, the code the **maniac uses too**. Two faults, both
invisible because nothing ever drew the map.

**1. The path smoother tested a hairline.** `GridPathfinder.ClearLine` walked a
one-cell-wide Bresenham line between waypoints. A diagonal shortcut whose *centre
line* clears a corner still drags a 0.6-wide body straight through it. Measured
against live physics, sweeping the maniac's actual `CapsuleCollider2D` along 3000
candidate shortcuts:

| | shortcuts accepted | real body **cannot** clear |
|---|---|---|
| old hairline test | 1852 | **70 (3.8%)** |
| new body test | 1013 | **0 (0.0%)** |

That 3.8% was the grinding. The fix cannot lose a route: a refused shortcut just
keeps the corner, falling back to the raw A\* cells, which are body-safe by
construction — stricter smoothing only ever *adds* waypoints. Verified on 300
random routes: **0 null paths, 0 crossing a blocked cell**.

**2. A\* had no reason to stay off walls.** Hugging one cost exactly what walking
down the middle cost, and diagonals made hugging *cheaper*, so the shortest path
was the wall. `WalkabilityGrid` now carries a **clearance field** (multi-source
BFS out of every blocked cell, built after the reachability flood-fill so the
dropped void counts as wall) and A\* pays a small penalty for low-clearance
cells. A **cost, not a block** — a one-tile doorway is still taken when it is the
only way through, and the octile heuristic stays admissible.

**`bodyRadius` is not the sampling `clearance`.** They answer different questions
— "can I walk *through* there" vs "can I stand *here*" — and conflating them is
what made this bug survive. Converting a radius to cells adds half the sample
box, because the wall surface can sit that much nearer than the blocked node.

**And the part you asked for: `NavDebugView`.** Press **F3** in play mode to
cycle every AI and paint its own map in the game view — red blocked · **yellow
walkable-but-too-tight-for-this-body** · green open · cyan current route. Yellow
is the point: "walkable" and "walkable by a body this wide" are different
questions, and this bug lived in the gap. On CastleWing the maniac's grid is
138×238; of his 3602 walkable cells, **1269 (35%) are yellow** — a third of the
floor he can stand on is floor he cannot smoothly cut across. Agents register
themselves, so **a new AI character joins the F3 cycle for free**. Unlit on
purpose (URP 2D lights would black it out in the dark corners that matter), and
compiled out of release builds like `DebugOverlay`.

**Also:** `clockFixTimes` in the batch result row — the run-clock second each
clock landed, on the same scale as `runSeconds`. A timeout row used to be a
guess; now "found three by 0:40 then nothing" and "one at 3:10, never got time to
work" are two visibly different shapes.

## 2026-07-25 — The gate wedge: measured, and it was three bugs, not one

47% of runs fixed every clock and then failed to leave, wedging at `(-4.5, 7.5)`
beside the ExitDoor. The suspicion on record was that the bot was *overshooting* —
standing above the win trigger. **Measured against the live colliders, that was
wrong on the axis.** The real numbers:

- Win trigger: `x[-3.90..-2.10] y[6.45..7.65]`; player feet collider is
  `0.55 x 0.55` at transform `+(0,-0.35)`.
- The wedge sits **inside the trigger's y range and 0.325 units west of its x
  range**. It was never an overshoot — the bot was beside the door, not above it.

Sweeping the player's actual body over the area found three compounding faults:

1. **The steering target was inside solid wall.** `exit.position + up * 0.8` =
   `(-3.00, 7.25)`. That point *is* in the trigger, but no body can occupy it —
   only the trigger's **lower slice is standable** (`y[6.55..7.05]`); the upper
   half is the wall the gate sprite hangs on. The bot was walking at a point it
   could never reach.
2. **A\* was routed at the gate itself, which is not on the grid.** The
   walkability grid is baked while the gate is still **shut**, so the opening
   reads as solid. A path to the gate therefore resolved to the nearest
   walkable pocket — a **dead end 1.5m west, behind a wall**. Confirmed by
   flood fill: seeded at the hall the door channel is unreachable, and seeded
   at the channel the hall is unreachable. Two disconnected components.
3. **`nav.Clear()` disabled the stuck detector at the one spot it was needed**,
   so a failed last metre became an infinite press instead of a reported
   failure — which is precisely what turned winnable runs into `stalled` rows.

**Fixed in `EscapeState`:** path to a **doorstep** staging point
(`gate + down*0.45` = `(-3.00, 6.00)`, which *is* on the grid) instead of the
gate; arm the final push only when actually **squared up with the opening**
(within 1.2 units, below the gate, |Δx| ≤ 0.6) rather than from anywhere within
2.5; aim just inside the trigger's **near edge** (`trigger.min.y + 0.35`,
derived from the live collider, not hardcoded); and carry a **local stall guard**
so a failed push re-approaches instead of grinding.

Verified against the live scene, with the gate in its **open** state (the earlier
analysis was misled by measuring while it was locked): A\* finds a path to the
staging point from the player start, from both far clocks, and **from the old
wedge coordinate**; the push arms at the staging point and does **not** arm at
the old wedge; and a simulated walk from staging fires `GameWonEvent` after
0.56 units of travel.

**Confirmed by a 10-run smoke batch** (`Bot_average`, seeds 2000–2009 @6x):

| | baseline | after fix |
|---|---|---|
| escapes | **0** | **4** |
| `stalled` at ExitDoor | **4** | **0** |
| runs that fixed all 3 clocks | 6 | 5 (4 of them escaped) |

All four escapes exited within 0.3 units of the same spot — `[-3.2, 6.5]`,
`[-2.9, 6.5]`, `[-2.9, 6.5]`, `[-3.2, 6.5]` — and the win fires slightly *short*
of the aim point because `GameWonEvent` triggers the moment the body overlaps the
trigger. That reproducibility is the evidence the last metre is deterministic
now, not lucky. **First escapes ever recorded at that gate.**

*Ten runs cannot measure a win rate (±30 points at this n) — this was an
instrument check, not the experiment.*

**A regression caught and fixed mid-test.** The first cut of this change returned
early from `Tick` when `KnownExit` was still null, without calling `Drive()`.
`Retarget()` only sets a destination — `Drive()` is what walks it — so honest
profiles (which must *see* the gate before they know it) stopped moving the
instant the last clock was fixed and coasted to a timeout. Signature: 3 clocks
fixed, `timeout`, stranded mid-map. Caught at run 3 of 10, batch aborted, void
runs quarantined in `Tools/Playtest/results/void/` rather than left where
`analyze.py` would aggregate them.

**Still open, and not caused by this fix:** a second wedge site around the
wardrobes (`stalled` at WardrobeA/WardrobeB), present in the baseline too; and
seed 2002 timed out holding 3 clocks at 81% explored while the four escapes
finished in 143–224s of the 480s budget — most likely a late third clock, but
unexplained.

## 2026-07-25 — The playtest harness survives a batch (run-11 post-mortem)

The first 75-run batch stopped after 10 runs and **said nothing about it** — the
results file just ended. Root-caused from `Editor.log` rather than guessed:

`BatchRunner.cs` was saved at 00:30. The batch launched 40 seconds later from
the **stale, already-loaded assembly**, because Unity only auto-refreshes when
the editor regains focus. Runs 1–10 were clean (zero exceptions). At ~00:36 a
refresh fired, noticed the pending change, and Unity did a **synchronous domain
reload in Play Mode**. That killed the runner's coroutine mid-batch.

It also nulled every non-serializable runtime field on the surviving scene
objects — `PlayerController.Input` is an interface field, `BotPilot.memory` a
plain C# object, and `Awake()` does not re-run on an object that already exists.
Four components then threw every frame: **498,704 NullReferenceExceptions and a
190 MB `Editor.log`**. The split is what makes it airtight — 0 exceptions during
runs 1–10, all 498,704 after the reload.

*The batch did not die of anything in the game. It died of its own source file
being recompiled underneath it.*

**Fixed, in four layers:**

- `BatchGuard` holds `LockReloadAssemblies` + `DisallowAutoRefresh` for the
  batch, so a compile request is deferred instead of executed. Verified against
  a real forced reload: locked, `RequestScriptReload()` did nothing; released,
  the same deferred reload fired at once. `TimeKiller/Setup/34` is the escape
  hatch, because a leaked lock silently stops the editor compiling.
- The window now flushes the AssetDatabase and refuses to enter Play Mode with a
  compile pending — the stale-assembly launch can no longer happen at all.
- If a reload happens anyway, an explicit `{"aborted":true,"reason":
  "domain_reload","atRun":11}` row is written **before the domain dies**, and
  Play Mode is stopped immediately so the scene cannot spam.

**The deeper bug it exposed: failures that looked like data.** The harness had
no way to say "I failed" — broken preconditions wrote *no row at all* (the batch
silently shrank) and a wedged bot wrote an ordinary loss. That is worse than the
crash: a silent instrument failure does not add noise, it biases every profile
toward "flat", which is already the most likely wrong answer at this sample
size. Runs now end as `escape` / `death` / `timeout` / `stalled` /
`harness_error`, with an exception watchdog and a per-run preflight check that
the player is actually wired. `analyze.py` splits faults out of the sample and
prints a **HARNESS STATUS block before any number**, including what fraction of
the planned runs are usable — a truncated batch is not a small batch, it is a
biased one.

## 2026-07-25 — Bot playtester: a simulated player that has to *find* the clocks

Every balance number in this project was unverified — the maniac AI, the escape
loop and the catacombs were all "needs playtest" — because verifying one change
cost a human evening. A full playthrough now costs seconds, so *"is 4 clocks
better than 3?"* gets answered by running it 200 times.

**The design decision that matters:** the bot is a *player*, not an oracle.

- It **does not know where the clocks are.** `BotMemory` promotes a clock,
  wardrobe or the gate from "exists" to "known" only after the bot has had clear
  line of sight to it from inside its sight range; until then it explores a
  coarse grid of the castle looking. Requested explicitly — it has to feel
  realistic, no cheating.
- It **plays the skill check badly, on purpose.** `BotProfileConfig` adds a
  reaction delay, *un-compensatable* reaction jitter, and perception noise on the
  marker read. A naive bot reads `ClockRepair.Marker` and hits 100% forever —
  a broken instrument that looks like it works.
- Two profiles keep the cheat as a **labelled control**: `oracle_average` and
  `oracle_expert` set `knowsEverything` and answer a different question ("how
  hard once you have memorised the castle"). `analyze.py` never averages them
  together with the honest profiles.
- **The number that matters is the spread**, not any single win rate. Both
  families win 95% -> trivial; both lose -> unfair.

**Two bugs the harness found in its own first live run**, before a single batch:

- It reported **0 hits out of 16** skill checks while the clock was visibly
  being repaired. `QueueSkillCheck()` only sets a flag that `ScriptedInputSource`
  turns into a press *next* frame, which `ClockRepair` reads the frame after
  that; scoring on the next tick measured nothing. The scorer now waits for the
  clock's progress to actually move.
- It fixed all three clocks, walked to the gate and **did not win**. The win
  trigger starts at y 6.45 and the bot's feet collider topped out at 6.41 — four
  centimetres short. It stopped on the gate's coordinate; a human keeps holding
  W and walks *through* the threshold. `EscapeState` now does the same. Real
  players have about 0.15 units of margin there.

**Also now measured:** `accidentalHides` — E is read by both `ClockRepair` and
`PlayerHiding` at the same 2.2 range, and every CastleWing clock was hand-placed
beside a wardrobe, so pressing E to repair can open the wardrobe instead.

New: `C#/Testing/{BotProfileConfig, BotPath, BotMemory, BotPilot, BatchRunner}`,
`Editor/BotPlaytestWindow` (menu `TimeKiller/Setup/33`),
`TestDriver.PressSkillCheck/BotStart/BotStop`, `Tools/Playtest/analyze.py`.
**No gameplay file was modified** — delete `C#/Testing/` and the game is intact.

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
