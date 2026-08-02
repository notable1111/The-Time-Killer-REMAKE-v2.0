---
name: watch
description: Study a recorded play session — run the bot or a human tester, then analyse the state log, frames and audio to find real problems. Use when asked to "watch", "record", "playtest and find problems", or to analyse a folder under Recordings/.
---

# /watch — study a play session and find what is wrong

Claude has no screen and no ears. This skill exists because the game can record
**itself**: state, frames and the final audio mix all land on disk, and a
recording can be studied far more precisely than a screen could be watched.

**The rule that makes this work: text finds the problem, images confirm it.**
Never open frames looking for something. Read the log, rank the suspects, then
open the two or three frames that settle it. Flipping through screenshots burns
the session and finds less than the analyser does in a second.

## 1. Get a session

**Bot tester** (no human needed — good for pacing, stuck-spots, fairness):

```
UnityEditor.SessionState.SetString("TimeKiller.BotPlaytest.Pending",
    "Bot_average|1|1000|1|0|180|0||0");   // profiles|runs|seed|speed|alsoAt1x|timeout|controlRuns|controlProfile|abPathfinding
```
then enter play mode and call `SessionRecorder.Begin()`.

**Speed MUST be 1.** Audio capture and frame grabs are meaningless accelerated,
and the batch runner sets `Time.timeScale` from that field.

**Human tester** (the only source of "was it boring"): `TimeKiller/Setup/46`
once, then **F8** start/stop, **F9** boring, **F10** unfair, **F11** that was great.

Sessions land in `<project>/Recordings/<timestamp>/` — gitignored, and outside
`Assets/` so Unity never imports the frames.

## 2. Analyse

```bash
python Tools/Playtest/analyze_session.py Recordings/<timestamp>
```

Ranked findings, each with a timestamp and the frame file that shows it.
Detectors: unfair death, dead air, flat tension, ghost maniac, stuck player,
no emotional arc, and the tester's own marks (ranked first — a human saying
"this was boring" is information no measurement can reconstruct).

## 3. Confirm before believing

- **Open only the flagged frames.** `Read` the `.jpg` — the F1 overlay is burned
  into every frame, so the picture carries its own ground truth.
- **Measure the audio** rather than guessing at it:
  ```bash
  ffmpeg -i Recordings/<t>/audio.wav -af volumedetect -f null - 2>&1 | grep volume
  ```
  Window it with `-ss`/`-t` to compare a calm stretch against a chase.

## 4. Fix

Treat every finding as a hypothesis until the frame or the audio agrees with it.
The analyser reports what the numbers say, and the numbers have been wrong
before — a landmark-label artifact once turned 21% of deaths into "66% wardrobe
deaths" and a whole conclusion was built on it.

Then follow the normal ritual: fix, `refresh_unity`, `SmokeCheck.Report()`,
EditMode tests, and re-record to prove the fix in a session rather than in a
comment.

## Gotchas paid for already

- **`Time.timeScale` is set by the batch runner.** The recorder schedules on
  `unscaledTime` so it survives that, but audio and frames only mean anything
  at 1x.
- **Never install Unity Recorder.** It modifies `Packages/manifest.json`, which
  is `skip-worktree`'d and local-only.
- **Filenames carry a timestamp the analyser parses back out.** They are written
  with `InvariantCulture` on purpose — a comma-decimal machine wrote
  `f00003_3,3.jpg` and every finding silently lost its evidence.
- **The overlay used to clip at a fixed 500px** and drop the entire Fear block
  offscreen, which looked exactly like systems failing to register.
- **The bot cannot tell you what is boring.** It has no F9. For pacing and feel,
  a human session is worth more than ten bot runs.
