"""Find the maniac behaving badly, from recorded sessions (pure stdlib).

"He sometimes makes mistakes and it looks weird" is a real report but not an
actionable one — you cannot fix a feeling. The state log samples his position at
4 Hz alongside his awareness, which is enough to catch the specific pathologies
that read as "weird" to a player:

  STALL      standing still while actively hunting. The single most broken-looking
             thing an AI can do — the player is watching a killer do nothing.
  JITTER     heading reversing repeatedly in a short window: the classic symptom
             of a path being recomputed every frame and flip-flopping between two
             nodes.
  GRIND      moving, but far below his configured speed, for a sustained stretch —
             what wall-scraping looks like in the data.
  RETREAT    losing ground while DETECTED. He has the player in sight and is
             getting further away, which reads as him giving up for no reason.
  WARP       moving faster than any configured speed — a teleport, usually a
             path reset snapping him across geometry.

Usage: python maniac_pathology.py [Recordings/<session> ...]
       python maniac_pathology.py            (all sessions)
"""
import glob
import json
import math
import os
import sys

STALL_SPEED = 0.15        # world units/sec below which he is "not moving"
STALL_SECONDS = 1.5       # ... for this long, while hunting, is a stall
GRIND_LOW, GRIND_HIGH = 0.2, 1.2
GRIND_SECONDS = 2.0
REVERSAL_DEG = 130.0      # heading change that counts as a reversal
JITTER_WINDOW = 2.0       # reversals inside this window ...
JITTER_COUNT = 3          # ... this many, and it is flip-flopping
WARP_SPEED = 12.0         # above any configured maniac speed
HUNTING = ("suspicious", "detected")


def load(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                d = json.loads(line)
            except ValueError:
                continue
            if d.get("meta") or "mx" not in d:
                continue
            rows.append(d)
    return rows


def analyse(session):
    path = os.path.join(session, "state.jsonl")
    if not os.path.exists(path):
        return None
    rows = load(path)
    if len(rows) < 8:
        return None

    steps = []           # (t, dt, speed, heading, aware, dist_to_player)
    for i in range(1, len(rows)):
        a, b = rows[i - 1], rows[i]
        dt = b["t"] - a["t"]
        if dt <= 0:
            continue
        dx, dy = b["mx"] - a["mx"], b["my"] - a["my"]
        dist = math.hypot(dx, dy)
        heading = math.degrees(math.atan2(dy, dx)) if dist > 1e-4 else None
        steps.append({
            "t": b["t"], "dt": dt, "speed": dist / dt, "heading": heading,
            "aware": (b.get("aware") or "unaware").lower(),
            "toPlayer": math.hypot(b["px"] - b["mx"], b["py"] - b["my"]),
        })

    findings = {"stall": [], "jitter": [], "grind": [], "retreat": [], "warp": []}

    # --- stalls and grinds: runs of consecutive samples matching a condition
    def runs(match, min_seconds):
        out, start, last = [], None, None
        for s in steps:
            if match(s):
                if start is None:
                    start = s["t"]
                last = s["t"]
            else:
                if start is not None and last - start >= min_seconds:
                    out.append((start, last - start))
                start = None
        if start is not None and last - start >= min_seconds:
            out.append((start, last - start))
        return out

    findings["stall"] = runs(
        lambda s: s["speed"] < STALL_SPEED and s["aware"] in HUNTING, STALL_SECONDS)
    findings["grind"] = runs(
        lambda s: GRIND_LOW < s["speed"] < GRIND_HIGH and s["aware"] in HUNTING, GRIND_SECONDS)

    # --- jitter: reversals clustered in time
    reversals = []
    prev = None
    for s in steps:
        h = s["heading"]
        if h is None:
            continue
        if prev is not None:
            diff = abs((h - prev + 180) % 360 - 180)
            if diff >= REVERSAL_DEG:
                reversals.append(s["t"])
        prev = h
    for i, t in enumerate(reversals):
        near = [r for r in reversals if 0 <= r - t <= JITTER_WINDOW]
        if len(near) >= JITTER_COUNT:
            if not findings["jitter"] or t - findings["jitter"][-1][0] > JITTER_WINDOW:
                findings["jitter"].append((t, len(near)))

    # --- retreat while detected
    start, gained = None, 0.0
    for i in range(1, len(steps)):
        s, p = steps[i], steps[i - 1]
        if s["aware"] == "detected" and s["toPlayer"] > p["toPlayer"]:
            if start is None:
                start, gained = s["t"], 0.0
            gained += s["toPlayer"] - p["toPlayer"]
        else:
            if start is not None and gained >= 2.0:
                findings["retreat"].append((start, gained))
            start, gained = None, 0.0
    if start is not None and gained >= 2.0:
        findings["retreat"].append((start, gained))

    findings["warp"] = [(s["t"], s["speed"]) for s in steps if s["speed"] > WARP_SPEED]

    duration = steps[-1]["t"] - steps[0]["t"]
    hunting = sum(s["dt"] for s in steps if s["aware"] in HUNTING)
    return {"session": os.path.basename(session), "duration": duration,
            "hunting": hunting, "samples": len(steps), "findings": findings,
            "topSpeed": max(s["speed"] for s in steps)}


def main():
    sessions = sys.argv[1:] or sorted(glob.glob(os.path.join("Recordings", "*")))
    reports = [r for r in (analyse(s) for s in sessions if os.path.isdir(s)) if r]
    if not reports:
        raise SystemExit("no usable sessions found")

    total = {k: 0 for k in ("stall", "jitter", "grind", "retreat", "warp")}
    for r in reports:
        f = r["findings"]
        print(f"\n=== {r['session']}  {r['duration']:.0f}s  "
              f"({r['hunting']:.0f}s hunting)  peak {r['topSpeed']:.1f} u/s")
        for key in ("stall", "jitter", "grind", "retreat", "warp"):
            total[key] += len(f[key])
        if not any(f.values()):
            print("   clean")
            continue
        for t, secs in f["stall"]:
            print(f"   STALL    t={t:6.1f}s  froze {secs:.1f}s while hunting")
        for t, n in f["jitter"]:
            print(f"   JITTER   t={t:6.1f}s  {n} heading reversals in {JITTER_WINDOW:.0f}s")
        for t, secs in f["grind"]:
            print(f"   GRIND    t={t:6.1f}s  crawling {secs:.1f}s while hunting")
        for t, gain in f["retreat"]:
            print(f"   RETREAT  t={t:6.1f}s  lost {gain:.1f}u while DETECTED")
        for t, sp in f["warp"]:
            print(f"   WARP     t={t:6.1f}s  {sp:.1f} u/s")

    print("\n=== totals across all sessions ===")
    for k, v in total.items():
        print(f"   {k:<9}{v}")


if __name__ == "__main__":
    main()
