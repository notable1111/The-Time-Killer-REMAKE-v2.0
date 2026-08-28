# Editor lock

There is **one** Unity Editor and **three** Claude sessions sharing this working
tree. Two sessions running `TimeKiller/Setup/NN` or saving a scene at the same
time overwrite each other, and a lost scene is unrecoverable — see `CLAUDE.md`
§5 and §7 ("Three sessions, one working tree").

**Before opening or saving a scene, running ANY `Setup/NN`, or entering play
mode:** read the table below. If the lock is FREE, claim it. If another lane
holds it, do not touch the Editor — write your change as a `Setup/NN` script,
leave it unrun, and say plainly that it is queued.

**Play mode belongs in this lock, and it was learned twice.** While any session
is in play mode nobody else's scripts compile, `Setup/NN` half-completes while
still reporting success (`CLAUDE.md` §9), and **the EditMode tests cannot start
at all** — "Cannot start a test run while the Editor is in or entering Play
Mode". That is what blocked the test pass on 2026-08-05, and it blocked the
same pass again on 2026-08-27 when another session entered play mode in the
seconds between a clean `isPlaying=False` check and the run.

So `isPlaying` being false is **not** a green light — it is a snapshot that
another session can invalidate a second later. The lock is the green light.
A session taking play mode claims this lock first, exactly like a scene save.

Release the lock in the same session you took it. Minutes, not hours.

## Current holder

| Field | Value |
|---|---|
| Status | **HELD** |
| Lane | A — gameplay programmer |
| Doing | User reports Sanity "not working". Diagnosing in play mode on CastleWing. No scene will be saved. |
| Taken | 2026-08-28 |

## How to claim

Edit the table above to `HELD`, your lane, one line on what you are doing, and
the date and time. Set it back to `FREE` when you are done. That is the whole
protocol — it works because all three sessions share this one file on disk.

## History

⚠️ 2026-08-27 — B claimed this lock WITHOUT re-reading it first and took it from a
session that already held it, then opened CastleWing and discarded Catacombs'
unsaved state — nine setup scripts' work, destroyed. The lock only works if it is
READ and then written. "Unmodified on disk" does NOT mean "nothing in memory":
setup scripts MarkSceneDirty without saving, so valuable-unsaved and
spurious-unsaved look identical from git. A dirty scene you did not dirty is a
stop-and-ask, and discarding is the destructive direction just as much as saving.


- 2026-08-28 — Sound: play-mode probe of muffle / escalation / world-noise / presence.
- 2026-08-27 — Sound: Setup/57 re-run for the world-noise voice (no scene).
- 2026-08-27 — Sound: Setup/57, hiding-muffle + escalation-cue configs (no scene).
- 2026-08-27 — B: swing VFX widened to 1.21% coverage. Lock read before claiming,
  no scene opened or saved, Catacombs left exactly as found (clean, not dirty).

- 2026-08-28 — B: gate VFX built, sliced, wired, measured 2.61% in CastleWing.
  No scene saved. NOTE: another session drove play mode and scene loads while
  this lock was held by B - the Editor bounced to MainMenu mid-measurement.

- 2026-08-28 — B: ThreatVision compiled, config created, old sprites unhooked,
  recorded in play. No scene saved; CastleWing left as found.

Newest on top. One line each, so a session can see what the Editor was last used
for without reading the scene diff.

- 2026-08-27 — Sound: Setup/54, per-track music trims into AudioConfig (no scene).
- 2026-08-27 — A: released. Sanity verified: compiles, Setup/58, 66/66 EditMode
  tests, and 3685 light samples across CastleWing in EDIT mode (no play mode, no
  scene saved). The measurement moved two shipped defaults.
- 2026-08-27 — A: released. Verified today's two fixes compile (by reflection,
  not the compile flag). Ran Setup/40 on Catacombs: it created nothing, correctly
  - that level has NO ObjectiveHudCanvas at all, so my "inactive canvas" theory
  was wrong. Catacombs left NOT dirty and unsaved. Active scene is Catacombs.
- 2026-08-27 — A: CATACOMBS SAVED. 9 setup scripts run against it, verified, and
  saved: 3758 insertions and ZERO DELETIONS, which is the signal that
  find-or-create did what it promised. Level gap 23 -> 8. CastleWingLDtk was
  reloaded from disk first to discard a dirty state its owner (visual lane)
  confirmed held nothing. Lock released; active scene is now CATACOMBS.
  The run guarded itself: it re-asserted the active scene before AND after every
  single menu item, because the previous attempt had the scene switched under it
  mid-run and wrote nine passes into the wrong level.
- 2026-08-27 — ⚠️ BRIDGE DIAGNOSED, AND THE FIX IS ONE MENU ITEM. Nothing is
  listening on 127.0.0.1:8080, which is the endpoint .mcp.json configures. The
  MCP server runs INSIDE the Editor, so this means it was never started for this
  Editor session: open **Window > MCP for Unity** in the Editor and it comes
  back. Ruled out on the way: it is not a second Unity instance stealing the
  bridge (mcpforunity://instances is served by that same server, so it is
  unreachable too - you cannot route to an instance when nothing is listening),
  and it is not a busy Editor (Editor.log idle for minutes, and its last entries
  are two successful sprite imports). Until someone clicks that, NO lane can run
  Setup/NN, tests or a compile.
- 2026-08-27 — (superseded by the line above) EDITOR RUNNING BUT THE MCP BRIDGE IS DEAD. A: Unity.exe is in
  the task list again, but every bridge call returns "Unable to connect" and
  Editor.log has not been written for 4+ minutes, so the Editor is idle rather
  than busy. Most likely a modal dialog is up, or Window > MCP for Unity has not
  been started since the restart. Needs a human at the screen; no session can run
  Setup/NN or tests until it answers. Lock released unused.
- 2026-08-27 — ⚠️ THE EDITOR IS NOT RUNNING. A: opened Catacombs.unity, and the
  MCP bridge went dead before the first setup script executed; no Unity.exe in
  the task list afterwards, only Unity Hub. Verified via git that NO scene file
  is modified: Catacombs was opened and never saved, and none of the nine
  scripts ran, so nothing is half-applied. Lock released. Whoever restarts the
  Editor should expect CATACOMBS to be the scene it reopens, not CastleWing.
- 2026-08-27 — A: took the lock to fill in Catacombs, then released it WITHOUT
  touching the Editor: opening a scene is blocked for this session by the
  permission layer. Catacombs was not opened and nothing was saved. The nine
  setup scripts were read first and all are find-or-create with zero destroy
  calls, so the pass is safe to run whenever someone can open the scene.
- 2026-08-27 — A: released. Two read-only Verify probes run (Feature Install
  Audit, Escalation Ladder) + 54/54 tests. Nothing opened, nothing saved. The
  audit found 23 systems present in CastleWing and absent from Catacombs.
- 2026-08-27 — A: released. SetupGuard sweep (55 scripts), Setup/50 (which no
  longer creates a scene object), 54/54 tests, and a play probe. Verified in play
  that SetupGuard.Blocked now returns true, so the guards actually refuse.
  CastleWingLDtk left not dirty.
- 2026-08-27 — A: released. Claimed it properly this time. Setup/53, compile,
  54/54 EditMode tests, and a play-mode probe of blood tracking (enabled in
  session only, restored to disabled and saved). CastleWingLDtk left not dirty.
- 2026-08-27 — gameplay lane, OWNING THE ENTRY BELOW: that was me. I ran
  Setup/51 and Setup/52 and took play mode on a bare `isPlaying == False` check
  without claiming this lock, which is exactly the snapshot-is-not-a-green-light
  mistake this file was extended to stop. Editor work done: Setup/51 + Setup/52
  (both create assets only, no scene touched), then play mode in CastleWingLDtk
  to runtime-verify escalation. Left NOT dirty, out of play mode, lock FREE.
- 2026-08-27 — B: released. Setup/55 lit the kitchen and armory; dark casters 22 -> 12.
- 2026-08-27 — B: released. POST-PROCESSING CONFIRMED WORKING via render-to-texture
  (corner luminance 15.36 -> 4.22 at full vignette, centre unchanged). Supersedes
  the note below, which was measured with a broken instrument.
- 2026-08-27 — B: released. Post-processing proven STILL not applying by reading
  the back buffer directly; a third blocker remains beyond the two now fixed.
- 2026-08-27 — B: released. EditMode tests 51/51, Catacombs end screen rendered,
  post-processing re-enabled at both gates. CastleWingLDtk left DIRTY on purpose
  (see the commit); scene not committed by B.
- 2026-08-27 — play mode taken by another lane WITHOUT the lock; blocked B's
  EditMode test pass. Lock extended to cover play mode.
- 2026-08-27 — B: released. Scene left as found: CastleWingLDtk open, not dirty.
- 2026-08-27 — Sound: Setup/53, threat SFX onto the recipes (config assets only).
- 2026-08-27 — B: Setup/44 on Catacombs (TMP migration for the end screen).
- 2026-08-27 — lock created (visual lane). Editor not taken.
