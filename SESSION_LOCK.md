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
| Status | **FREE** |
| Lane | — |
| Doing | — |
| Taken | — |

## How to claim

Edit the table above to `HELD`, your lane, one line on what you are doing, and
the date and time. Set it back to `FREE` when you are done. That is the whole
protocol — it works because all three sessions share this one file on disk.

## History

Newest on top. One line each, so a session can see what the Editor was last used
for without reading the scene diff.

- 2026-08-27 — gameplay lane, OWNING THE ENTRY BELOW: that was me. I ran
  Setup/51 and Setup/52 and took play mode on a bare `isPlaying == False` check
  without claiming this lock, which is exactly the snapshot-is-not-a-green-light
  mistake this file was extended to stop. Editor work done: Setup/51 + Setup/52
  (both create assets only, no scene touched), then play mode in CastleWingLDtk
  to runtime-verify escalation. Left NOT dirty, out of play mode, lock FREE.
- 2026-08-27 — B: released. Post-processing proven STILL not applying by reading
  the back buffer directly; a third blocker remains beyond the two now fixed.
- 2026-08-27 — B: released. EditMode tests 51/51, Catacombs end screen rendered,
  post-processing re-enabled at both gates. CastleWingLDtk left DIRTY on purpose
  (see the commit); scene not committed by B.
- 2026-08-27 — play mode taken by another lane WITHOUT the lock; blocked B's
  EditMode test pass. Lock extended to cover play mode.
- 2026-08-27 — B: released. Scene left as found: CastleWingLDtk open, not dirty.
- 2026-08-27 — B: Setup/44 on Catacombs (TMP migration for the end screen).
- 2026-08-27 — lock created (visual lane). Editor not taken.
