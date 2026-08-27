# Editor lock

There is **one** Unity Editor and **three** Claude sessions sharing this working
tree. Two sessions running `TimeKiller/Setup/NN` or saving a scene at the same
time overwrite each other, and a lost scene is unrecoverable — see `CLAUDE.md`
§5 and §7 ("Three sessions, one working tree").

**Before opening or saving a scene, or running ANY `Setup/NN`:** read the table
below. If the lock is FREE, claim it. If another lane holds it, do not touch the
Editor — write your change as a `Setup/NN` script, leave it unrun, and say
plainly that it is queued.

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

- 2026-08-27 — lock created (visual lane). Editor not taken.
