"""Mine a recorded session for design problems, from telemetry rather than memory.

The recorder writes state at 4Hz. That is enough to answer questions nobody can
answer reliably by playing: how long the player spent bored, whether a death had
any warning, whether the tension curve has a middle or only two ends.

Everything here reports MEASUREMENTS and, where it draws a conclusion, says what
threshold it used. A judgement you cannot see the threshold for is an opinion
wearing a number's clothes.

Usage: python mine_session.py [Recordings/<session>]
       (defaults to the newest session)
"""
import json
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(r"D:\The Time Killer Remake")
HZ = 4.0
DT = 1.0 / HZ

# A stretch is "boring" if the player is Safe, the maniac is far, and it lasts
# long enough to notice. 12s is about the point at which a horror game's silence
# stops building dread and starts reading as an empty level.
BORING_SECONDS = 12.0
BORING_FEAR = 0.12
BORING_DISTANCE = 18.0

# A hit is "unwarned" if fear was low for the whole run-up. 1.5s is roughly a
# human reaction window; if fear never rose above this in that time, the player
# had no audible or visual cue that anything was coming.
WARNING_WINDOW = 2.5
WARNING_FEAR = 0.35


def newest_session():
    sessions = sorted((ROOT / "Recordings").glob("*"), key=lambda p: p.stat().st_mtime)
    return sessions[-1] if sessions else None


def load(session):
    rows = []
    for line in (session / "state.jsonl").read_text(encoding="utf-8", errors="ignore").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            row = json.loads(line)
        except json.JSONDecodeError:
            continue
        if not row.get("meta"):
            rows.append(row)
    return rows


def spans(rows, predicate):
    """Contiguous runs where predicate(row) holds -> (start_t, end_t, samples)."""
    out, start, count = [], None, 0
    for row in rows:
        if predicate(row):
            if start is None:
                start = row["t"]
            count += 1
        elif start is not None:
            out.append((start, row["t"], count))
            start, count = None, 0
    if start is not None:
        out.append((start, rows[-1]["t"], count))
    return out


def main():
    session = Path(sys.argv[1]) if len(sys.argv) > 1 else newest_session()
    rows = load(session)
    if not rows:
        raise SystemExit("no state rows")
    duration = rows[-1]["t"]
    print(f"session {session.name}   {duration:.1f}s   {len(rows)} samples\n")

    # ---- where the time actually went -------------------------------------
    print("TENSION CURVE - share of the run in each stage")
    stages = Counter(r.get("stage") for r in rows)
    for stage, n in stages.most_common():
        print(f"  {str(stage):12} {n*DT:6.1f}s  {100*n/len(rows):5.1f}%  {'#'*int(40*n/len(rows))}")

    print("\nMANIAC — share of the run in each state")
    for state, n in Counter(r.get("mstate") for r in rows).most_common():
        print(f"  {str(state):18} {n*DT:6.1f}s  {100*n/len(rows):5.1f}%")

    # ---- damage, and whether it was fair ----------------------------------
    print("\nDAMAGE EVENTS")
    hits = []
    previous = None
    for row in rows:
        hp = row.get("hp")
        if hp is None:
            continue
        # -1 is the recorder's "not initialised yet", not a real HP value.
        if previous is not None and hp >= 0 and previous >= 0 and hp < previous:
            hits.append(row)
        if hp >= 0:
            previous = hp
    if not hits:
        print("  none")
    for hit in hits:
        t = hit["t"]
        lead = [r for r in rows if t - WARNING_WINDOW <= r["t"] < t]
        peak_fear = max((r.get("fear", 0) for r in lead), default=0)
        min_dist = min((r.get("directDist", 99) for r in lead), default=99)
        warned = peak_fear >= WARNING_FEAR
        print(f"  t={t:6.1f}s  hp->{hit['hp']}  peak fear in the {WARNING_WINDOW}s before: {peak_fear:.2f}"
              f"   closest he got: {min_dist:.1f}u   -> {'warned' if warned else 'NO WARNING'}")

    # ---- dead air ----------------------------------------------------------
    print(f"\nBORING STRETCHES  (fear < {BORING_FEAR}, maniac > {BORING_DISTANCE}u, "
          f"lasting > {BORING_SECONDS}s)")
    quiet = spans(rows, lambda r: r.get("fear", 1) < BORING_FEAR
                  and r.get("directDist", 0) > BORING_DISTANCE)
    long_quiet = [s for s in quiet if s[1] - s[0] >= BORING_SECONDS]
    if not long_quiet:
        print("  none")
    total_quiet = 0.0
    for start, end, _ in long_quiet:
        total_quiet += end - start
        print(f"  {start:6.1f}s -> {end:6.1f}s   ({end-start:.1f}s of nothing)")
    if long_quiet:
        print(f"  total: {total_quiet:.1f}s = {100*total_quiet/duration:.0f}% of the run")

    # ---- the chase economy -------------------------------------------------
    print("\nCHASES")
    chases = spans(rows, lambda r: r.get("mstate") == "ChaseState")
    if not chases:
        print("  none")
    for start, end, _ in chases:
        print(f"  {start:6.1f}s -> {end:6.1f}s  ({end-start:4.1f}s)")
    if chases:
        lengths = [e - s for s, e, _ in chases]
        print(f"  {len(chases)} chases, mean {sum(lengths)/len(lengths):.1f}s, "
              f"longest {max(lengths):.1f}s")

    # ---- how close he got without the player being told --------------------
    print("\nCLOSE APPROACHES WHILE UNAWARE  (he never noticed, but was near)")
    sneaky = [r for r in rows if r.get("aware") == "unaware" and r.get("directDist", 99) < 8]
    if sneaky:
        closest = min(sneaky, key=lambda r: r["directDist"])
        print(f"  {len(sneaky)*DT:.1f}s spent within 8u of an unaware maniac; "
              f"closest {closest['directDist']:.1f}u at t={closest['t']:.1f}s "
              f"(fear was {closest.get('fear', 0):.2f})")
    else:
        print("  none")


if __name__ == "__main__":
    main()
