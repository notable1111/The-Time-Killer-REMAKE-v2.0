#!/usr/bin/env python3
"""Find the problems in a recorded play session.

Usage:  python analyze_session.py Recordings/2026-08-02_143000

Reads state.jsonl (written by SessionRecorder) and produces a RANKED list of
suspect moments, each with the timestamp and the exact frame file to look at.

WHY THIS EXISTS. The naive way to study a play session is to watch it, or to
flip through screenshots. That is both expensive and unreliable -- attention
drifts, and the interesting moments are precisely the ones that look ordinary
in a still. Text scales: this reads a 20-minute session in a second and points
at the ten seconds worth examining. Images then CONFIRM a finding rather than
being searched for one.

WHAT IT CANNOT DO. It cannot tell you whether the game is fun. That is what the
tester's F9/F10/F11 marks are for, and they are surfaced first for that reason.

Pure standard library on purpose -- numpy is not installed on this machine and a
tool that needs an install is a tool that stops being run.
"""
import json
import os
import sys
from statistics import mean, pstdev

# --- thresholds. Every one is a claim about the game, so each is named and
# --- justified rather than being a magic number buried in a condition.
DEAD_AIR_SECONDS = 45.0     # fear this flat for this long is not tension, it is a corridor
DEAD_AIR_FEAR = 0.08
UNFAIR_FEAR = 0.30          # died without the system having warned you
UNFAIR_WINDOW = 3.0
FLAT_WINDOW = 120.0         # constant tension is explicitly NOT the design goal
FLAT_STDEV = 0.06
STUCK_SECONDS = 6.0
STUCK_RADIUS = 0.4
GHOST_SECONDS = 90.0        # never even close to the maniac
GHOST_DISTANCE = 12.0


def load(path):
    meta, rows = {}, []
    with open(os.path.join(path, "state.jsonl"), encoding="utf-8") as fh:
        for raw in fh:
            raw = raw.strip()
            if not raw:
                continue
            try:
                row = json.loads(raw)
            except json.JSONDecodeError:
                continue          # a session killed mid-write leaves one torn line
            (meta.update(row) if row.get("meta") else rows.append(row))
    rows.sort(key=lambda r: r.get("t", 0.0))
    return meta, rows


def frame_for(path, t):
    """Nearest saved frame to a timestamp, so a finding carries its evidence."""
    d = os.path.join(path, "frames")
    if not os.path.isdir(d):
        return None
    best, best_gap = None, 1e9
    for name in os.listdir(d):
        if not name.endswith(".jpg"):
            continue
        try:
            stamp = float(name.rsplit("_", 1)[1][:-4])
        except (IndexError, ValueError):
            continue
        gap = abs(stamp - t)
        if gap < best_gap:
            best, best_gap = name, gap
    return best if best_gap < 3.0 else None


def spans(rows, ok, min_len):
    """Contiguous runs where ok(row) holds for at least min_len seconds."""
    out, start = [], None
    for i, r in enumerate(rows):
        if ok(r):
            if start is None:
                start = i
        else:
            if start is not None and rows[i - 1]["t"] - rows[start]["t"] >= min_len:
                out.append((rows[start]["t"], rows[i - 1]["t"]))
            start = None
    if start is not None and rows[-1]["t"] - rows[start]["t"] >= min_len:
        out.append((rows[start]["t"], rows[-1]["t"]))
    return out


def analyse(path):
    meta, rows = load(path)
    if not rows:
        return meta, [{"sev": 100, "kind": "no-data", "t": 0,
                       "what": "state.jsonl has no samples — did the recorder start?"}]

    findings = []
    duration = rows[-1]["t"]
    fears = [r.get("fear", 0.0) for r in rows if "fear" in r]

    def add(sev, kind, t, what):
        findings.append({"sev": sev, "kind": kind, "t": t, "what": what})

    # 1. THE TESTER'S OWN MARKS. First, and highest weight: a human saying
    #    "this was boring" is information no measurement can reconstruct.
    for r in rows:
        m = r.get("mark", "")
        if m in ("BORING", "UNFAIR", "GREAT"):
            sev = {"UNFAIR": 95, "BORING": 90, "GREAT": 40}[m]
            add(sev, "tester:" + m.lower(), r["t"],
                f"tester marked {m} (fear {r.get('fear', 0):.2f}, "
                f"{r.get('aware', '?')}, threat {r.get('threatDist', '?')}u)")

    # 2. UNFAIR DEATH — died with the fear system having said nothing.
    for r in rows:
        if r.get("mark") != "death":
            continue
        prior = [x.get("fear", 0.0) for x in rows
                 if r["t"] - UNFAIR_WINDOW <= x["t"] <= r["t"] and "fear" in x]
        if prior and mean(prior) < UNFAIR_FEAR:
            add(98, "unfair-death", r["t"],
                f"died with mean fear {mean(prior):.2f} over the prior {UNFAIR_WINDOW:.0f}s "
                f"— the warning system never fired")

    # 3. DEAD AIR — nothing happening, for a long time.
    for a, b in spans(rows, lambda r: r.get("fear", 0.0) < DEAD_AIR_FEAR, DEAD_AIR_SECONDS):
        add(min(90, 55 + int(b - a) // 10), "dead-air", a,
            f"{b - a:.0f}s with fear under {DEAD_AIR_FEAR} — no threat, no tension")

    # 4. FLAT TENSION — the design explicitly wants peaks and valleys, so a long
    #    stretch of UNVARYING fear is a defect even when that fear is high.
    step = max(1, int(len(rows) * FLAT_WINDOW / max(duration, 1)))
    for i in range(0, len(rows) - step, step):
        window = [r.get("fear", 0.0) for r in rows[i:i + step] if "fear" in r]
        if len(window) > 10 and pstdev(window) < FLAT_STDEV and mean(window) > DEAD_AIR_FEAR:
            add(70, "flat-tension", rows[i]["t"],
                f"fear sat at {mean(window):.2f} +/- {pstdev(window):.3f} for "
                f"{FLAT_WINDOW:.0f}s — no peaks or valleys")

    # 5. GHOST MANIAC — he was never anywhere near for a long stretch.
    for a, b in spans(rows,
                      lambda r: (r.get("directDist") or 999) > GHOST_DISTANCE,
                      GHOST_SECONDS):
        add(65, "ghost-maniac", a,
            f"{b - a:.0f}s with the maniac never within {GHOST_DISTANCE}u — he is not in the game")

    # 6. STUCK — position frozen with no legitimate reason.
    #
    # THE FIRST VERSION OF THIS WAS 100% NOISE. On the first real session it
    # reported four stuck players and every single one was the game working:
    # three were the clock repair mini-game (which REQUIRES standing still and
    # pressing SPACE) and the fourth was 24 seconds inside a wardrobe. Standing
    # still is not a defect in a game whose two core verbs are repair and hide.
    #
    # So a stall only counts when the player was neither hiding nor repairing.
    # Both are recoverable from the marks already in the log, which is why this
    # needed no change to the recorder.
    hidden = []          # (start, end) spans the player spent inside a spot
    open_hide = None
    for r in rows:
        if r.get("mark") == "hide" and open_hide is None:
            open_hide = r["t"]
        elif r.get("mark") == "unhide" and open_hide is not None:
            hidden.append((open_hide, r["t"]))
            open_hide = None
    if open_hide is not None:
        hidden.append((open_hide, rows[-1]["t"]))

    clocks = [(r["t"], r.get("px"), r.get("py")) for r in rows if r.get("mark") == "clock"]

    def excused(start, end, at):
        # Inside a wardrobe for any part of the stall.
        if any(a - 2 <= end and start <= b + 2 for a, b in hidden):
            return "hiding"
        # A clock completed at this spot shortly after — the stall WAS the repair.
        for t, cx, cy in clocks:
            if cx is None or at[0] is None:
                continue
            if start - 2 <= t <= end + 20 and abs(cx - at[0]) + abs(cy - at[1]) < 2.0:
                return "repairing a clock"
        return None

    stuck_start, anchor = None, None
    for r in rows:
        p = (r.get("px"), r.get("py"))
        if p[0] is None:
            continue
        if anchor is None or abs(p[0] - anchor[0]) + abs(p[1] - anchor[1]) > STUCK_RADIUS:
            if stuck_start is not None and r["t"] - stuck_start >= STUCK_SECONDS:
                why = excused(stuck_start, r["t"], anchor)
                if why is None:
                    add(80, "stuck", stuck_start,
                        f"player within {STUCK_RADIUS}u for {r['t'] - stuck_start:.0f}s at "
                        f"({anchor[0]}, {anchor[1]}) — not hiding, not repairing")
            anchor, stuck_start = p, r["t"]
    # 7. NO EMOTIONAL ARC across the whole run.
    stages = {r.get("stage") for r in rows}
    if "Panic" not in stages and duration > 120:
        add(75, "no-arc", 0,
            f"{duration / 60:.1f} minutes and fear never reached Panic — "
            f"peak fear was {max(fears) if fears else 0:.2f}")

    findings.sort(key=lambda f: (-f["sev"], f["t"]))
    for f in findings:
        f["frame"] = frame_for(path, f["t"])
    return meta, findings


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    path = sys.argv[1]
    meta, findings = analyse(path)

    print(f"session : {path}")
    print(f"scene   : {meta.get('scene', '?')}   started {meta.get('started', '?')}")
    print(f"findings: {len(findings)}\n")
    if not findings:
        print("Nothing flagged. That is not the same as 'the session was good' — "
              "it means none of the detectors fired.")
        return 0

    print(f"{'sev':>4}  {'time':>7}  {'kind':<18} {'frame':<26} what")
    for f in findings:
        mm, ss = divmod(int(f["t"]), 60)
        print(f"{f['sev']:>4}  {mm:>3}:{ss:02d}  {f['kind']:<18} "
              f"{(f.get('frame') or '-'):<26} {f['what']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
