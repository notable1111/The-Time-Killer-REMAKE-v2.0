#!/usr/bin/env python3
"""Aggregate bot playtest runs into the report a level designer can act on.

Usage:
    python Tools/Playtest/analyze.py                      # newest results file
    python Tools/Playtest/analyze.py results/2026-07-25_143000.jsonl

Offline and re-runnable, like Tools/MapPipeline - no Unity needed.

Two things it refuses to let you miss:

  * The SPEED CHECK. Accelerated runs only measure the game if they agree with
    matched-seed 1x runs. It does NOT decide that on win rate alone: win/loss is
    binary, so a 15-run control carries a ~13-point standard error and a naive
    "differ by more than 5 points" rule fires on noise almost every time. The
    verdict leans on MATCHED-SEED PAIRS of continuous metrics instead, which
    move long before an outcome flips - if acceleration is quietly making the
    maniac dumber, then on the very same seed he closes less distance, spots the
    bot less often, and lands fewer hits.

  * The HONEST/ORACLE SPLIT. Profiles with knowsEverything walked in knowing
    where all three clocks were. They answer "how hard is this once you know
    the castle", never "how hard is this the first time", and the two are never
    averaged together.
"""

import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
RESULTS = HERE / "results"


def load(path):
    """Split the file into three kinds of line, because they are not comparable.

    meta    the header
    rows    runs the harness could honestly measure
    faults  runs it could not, plus any abort record

    Keeping faults OUT of `rows` is the whole point. A harness error is not a
    loss; averaging it into a win rate silently drags every profile toward
    "flat", which is already the most likely wrong answer at this sample size.
    """
    meta, rows, faults = {}, [], []
    with open(path, "r", encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            obj = json.loads(line)
            if obj.get("meta"):
                meta.update(obj)
            elif obj.get("harnessError") or obj.get("aborted"):
                faults.append(obj)
            else:
                rows.append(obj)
    return meta, rows, faults


def harness_report(meta, rows, faults):
    """Did the batch actually finish? Printed FIRST, before any number.

    The 2026-07-25 batch stopped at run 11 of 75 and the file gave no sign of
    it - you had to count the lines and know what the total should have been.
    """
    planned = None
    for f in faults:
        if f.get("aborted"):
            planned = f.get("plannedTotal", planned)

    if faults:
        print("\nHARNESS STATUS   the batch did not complete cleanly")
        print("-" * 78)
        for f in faults:
            if f.get("aborted"):
                print(f"  ABORTED at run {f.get('atRun','?')}/{f.get('plannedTotal','?')} "
                      f"- {f.get('reason','?')}")
                print(f"    {f.get('detail','')}")
            else:
                print(f"  run {f.get('atRun','?')} (seed {f.get('seed','?')}, "
                      f"{f.get('profile','?')}): {f.get('fault','?')}")
        done = len(rows)
        if planned:
            print(f"\n  {done} of {planned} planned runs are usable "
                  f"({100.0*done/planned:.0f}%). Everything below is that subset only.")
        print("  Fix the harness before reading any verdict - a truncated batch is\n"
              "  not a small batch, it is a biased one (it stops on whatever broke it).")

    noisy = [r for r in rows if r.get("exceptions")]
    if noisy:
        print(f"\n  {len(noisy)} otherwise-usable runs threw exceptions "
              f"(under the abort budget). First: {noisy[0].get('firstException','?')[:90]}")

    stalled = [r for r in rows if r.get("endReason") == "stalled"]
    if stalled:
        seeds = ",".join(str(r.get("seed")) for r in stalled[:8])
        print(f"\n  {len(stalled)} runs STALLED (bot stopped moving and stopped making "
              f"progress).\n  These are harness wedges, not losses. Seeds: {seeds}")


def pct(part, whole):
    return 100.0 * part / whole if whole else 0.0


def cell_summary(rows):
    n = len(rows)
    wins = sum(1 for r in rows if r.get("won"))
    attempted = sum(r.get("skillChecks", {}).get("attempted", 0) for r in rows)
    hit = sum(r.get("skillChecks", {}).get("hit", 0) for r in rows)
    return {
        "n": n,
        "win": pct(wins, n),
        "clocks": statistics.mean([r.get("clocksFixed", 0) for r in rows]) if n else 0,
        "total_clocks": rows[0].get("clocksTotal", 0) if n else 0,
        "seconds": statistics.median([r.get("runSeconds", 0) for r in rows]) if n else 0,
        "skill": pct(hit, attempted),
        "explored": statistics.mean([r.get("mapExplored", 0) for r in rows]) if n else 0,
        "found": statistics.mean([r.get("clocksFound", 0) for r in rows]) if n else 0,
        "timeouts": sum(1 for r in rows if r.get("endReason") == "timeout"),
        "accidental_hides": sum(r.get("accidentalHides", 0) for r in rows),
    }


def report(path):
    meta, rows, faults = load(path)
    print(f"\n{path.name}   scene={meta.get('scene', '?')}   "
          f"usable runs={len(rows)}   harness faults={len(faults)}")
    print("=" * 78)
    harness_report(meta, rows, faults)
    if not rows:
        print("\nNo usable runs in this file.")
        return

    by_cell = defaultdict(list)
    for r in rows:
        by_cell[(r.get("profile", "?"), r.get("speed", 1))].append(r)

    honest = [k for k in by_cell if not by_cell[k][0].get("knowsEverything")]
    oracle = [k for k in by_cell if by_cell[k][0].get("knowsEverything")]

    for title, keys in (("HONEST (had to find the clocks)", honest),
                        ("ORACLE (knew the map - upper bound, do not mix)", oracle)):
        if not keys:
            continue
        print(f"\n{title}")
        print(f"  {'profile':<16}{'x':>4}{'n':>5}{'win%':>7}{'clocks':>9}"
              f"{'skill%':>8}{'found':>7}{'map%':>7}{'median s':>10}{'t/o':>5}")
        for key in sorted(keys):
            s = cell_summary(by_cell[key])
            print(f"  {key[0]:<16}{key[1]:>4g}{s['n']:>5}{s['win']:>7.0f}"
                  f"{s['clocks']:>6.1f}/{s['total_clocks']:<2}{s['skill']:>8.0f}"
                  f"{s['found']:>7.1f}{s['explored']:>7.0f}{s['seconds']:>10.0f}{s['timeouts']:>5}")

    speed_check(by_cell)
    # Before any difficulty claim: whether the instrument was sound, and where
    # the runs actually ended. A win rate read past a wedged bot is a fiction.
    instrument_check(rows)
    failure_stages([r for r in rows if not r.get("knowsEverything")] or rows)
    difficulty_verdict(by_cell, honest)
    death_map(rows)
    quirks(rows)


def wilson(k, n, z=1.96):
    """95% interval for a proportion.

    Printed beside every win rate in the speed check, because a bare "47%" off
    15 runs invites a decision the sample cannot support.
    """
    if not n:
        return 0.0, 100.0
    p = k / n
    d = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / d
    half = z * ((p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5) / d
    return 100 * max(0.0, centre - half), 100 * min(1.0, centre + half)


# Metrics compared pair-by-pair on identical seeds. `easier` is the sign that
# means the game got EASIER at speed, i.e. the maniac got dumber:
#   he never closed as much distance, saw the bot less, hit it less, and it
#   therefore got more clocks done. runSeconds is deliberately NOT scored - a
#   dead bot and a winning bot both produce a short run, so its direction is
#   not interpretable on its own. It is printed for context only.
PAIRED_METRICS = [
    ("min maniac dist", lambda r: r.get("minManiacDistance"), +1),
    ("times spotted", lambda r: r.get("spotted"), -1),
    ("hits taken", lambda r: r.get("hits"), -1),
    ("clocks fixed", lambda r: r.get("clocksFixed"), +1),
    ("run seconds", lambda r: r.get("runSeconds"), 0),
]


def sign_test(pairs, extract):
    """(up, down, tied, mean signed delta) for fast-minus-slow on matched seeds."""
    up = down = tied = 0
    deltas = []
    for slow, fast in pairs:
        a, b = extract(slow), extract(fast)
        if a is None or b is None:
            continue
        delta = b - a
        deltas.append(delta)
        if delta > 1e-9:
            up += 1
        elif delta < -1e-9:
            down += 1
        else:
            tied += 1
    return up, down, tied, (statistics.mean(deltas) if deltas else 0.0)


def speed_check(by_cell):
    """Accelerated runs are only evidence if they agree with 1x on the same seeds."""
    header_done = False
    for p, s in sorted(by_cell):
        if s == 1 or (p, 1) not in by_cell:
            continue
        if not header_done:
            print("\nSPEED CHECK (matched seeds)")
            header_done = True
        speed_pair(by_cell, p, s)

    if not header_done:
        print("\nSPEED CHECK: no 1x control block - accelerated numbers are unvalidated.")
        return

    # A concentrated control validates ONE profile. Say so out loud: an
    # unvalidated profile and a validated one look identical once aggregated,
    # and that is exactly the confusion that gets a bad speed trusted.
    validated = {p for p, s in by_cell if s == 1}
    rest = sorted({p for p, s in by_cell if s != 1} - validated)
    if rest:
        print(f"\n  NOT validated (no 1x runs): {', '.join(rest)}")
        print("  They inherit the verdict above only if the accelerator distorts")
        print("  every profile equally - plausible, but not measured here.")


def speed_pair(by_cell, profile, speed):
    slow_rows, fast_rows = by_cell[(profile, 1)], by_cell[(profile, speed)]
    ks, ns = sum(1 for r in slow_rows if r.get("won")), len(slow_rows)
    kf, nf = sum(1 for r in fast_rows if r.get("won")), len(fast_rows)
    slo, shi = wilson(ks, ns)
    flo, fhi = wilson(kf, nf)

    print(f"\n  {profile}   1x {pct(ks, ns):.0f}% [{slo:.0f}-{shi:.0f}] n={ns}"
          f"   vs   {speed:g}x {pct(kf, nf):.0f}% [{flo:.0f}-{fhi:.0f}] n={nf}")
    if shi >= flo and fhi >= slo:
        print("    win rate: intervals overlap - at this sample size the win rate")
        print("              cannot tell these apart either way. Not evidence of")
        print("              agreement, just absence of evidence. See the pairs:")
    else:
        print("    win rate: intervals do NOT overlap - the accelerator is suspect.")

    slow_by_seed = {r["seed"]: r for r in slow_rows if "seed" in r}
    fast_by_seed = {r["seed"]: r for r in fast_rows if "seed" in r}
    pairs = [(slow_by_seed[s], fast_by_seed[s])
             for s in sorted(set(slow_by_seed) & set(fast_by_seed))]
    if not pairs:
        print("    no matched seeds - cannot pair. Was BaseSeed changed mid-batch?")
        return

    print(f"    matched pairs: {len(pairs)}      "
          f"(delta = {speed:g}x minus 1x, on the same seed)")
    suspicious = 0
    scored = 0
    for label, extract, easier in PAIRED_METRICS:
        up, down, tied, mean_delta = sign_test(pairs, extract)
        moved = up + down
        note = ""
        if easier and moved >= 6:
            scored += 1
            lean = up if easier > 0 else down
            # A lopsided sign test on identical seeds is the signal. ~80% of
            # movement in the easier direction is well past coin-flip for n>=6.
            if lean / moved >= 0.8:
                suspicious += 1
                note = "  <- leans EASIER at speed"
        print(f"      {label:<17}{mean_delta:+8.2f}   up {up:>3} / down {down:>3}"
              f" / tied {tied:>3}{note}")

    if scored and suspicious >= max(2, scored - 1):
        print(f"    VERDICT: UNTRUSTWORTHY - {suspicious}/{scored} scored metrics move")
        print(f"             the same way on identical seeds. {speed:g}x is making the")
        print("             maniac dumber. Drop the speed and re-run.")
    elif scored:
        print(f"    VERDICT: ok - only {suspicious}/{scored} scored metrics lean easier,")
        print(f"             which is what independent noise looks like. {speed:g}x holds.")
    else:
        print("    VERDICT: too few paired runs to score. Treat the speed as unproven.")


def difficulty_verdict(by_cell, honest_keys):
    """The number that matters is the SPREAD, not any single win rate.

    Read off the speed each profile has the most runs at - the 1x control block
    is deliberately small and would otherwise decide this on five samples.
    """
    best = {}
    for p, s in honest_keys:
        n = len(by_cell[(p, s)])
        if p not in best or n > best[p][0]:
            best[p] = (n, cell_summary(by_cell[(p, s)])["win"])
    if "novice" not in best or "expert" not in best:
        return
    lo, hi = best["novice"][1], best["expert"][1]
    print(f"\nDIFFICULTY   novice {lo:.0f}%  ->  expert {hi:.0f}%   (spread {hi - lo:+.0f})")
    if lo > 60:
        print("  Too easy: even a panicky novice escapes most of the time.")
    elif hi < 35:
        print("  Too hard: skilled play barely helps - check whether the maniac is beatable at all.")
    elif hi - lo < 20:
        print("  Flat: skill is not being rewarded. The outcome is dominated by luck or layout.")
    else:
        print("  Healthy band (roughly novice 20-35%, expert 65-80% is the target).")


def instrument_check(rows):
    """Runs the HARNESS lost, not the game.

    A timeout on its own is ambiguous - maybe the level is a maze. A timeout at
    the *same coordinate* on several different seeds is not: the seeds differ,
    so the only thing those runs share is the wall. That is the bot wedged on
    geometry, and counting it as a loss understates the win rate.

    Found the hard way on the first real batch: two runs ended at exactly
    (-4.3, 7.6) beside the exit, having fixed every clock, with the maniac never
    once spotting them. EscapeState's last-metre push clears the navigator to
    walk through the threshold, which also switches off BotPath's stuck
    detector at the one spot it is needed.
    """
    buckets = defaultdict(list)
    for r in rows:
        # "stalled" is the same phenomenon caught earlier by the stall detector,
        # so it must land in the same coordinate clustering - otherwise fixing
        # the detector would make the wedge map go quiet without fixing the wedge.
        if r.get("won") or r.get("endReason") not in ("timeout", "stalled") or not r.get("endPos"):
            continue
        buckets[(round(r["endPos"][0] * 2) / 2, round(r["endPos"][1] * 2) / 2)].append(r)
    traps = {k: v for k, v in buckets.items() if len({r.get("seed") for r in v}) >= 2}
    if not traps:
        return set()

    lost = [r for rs in traps.values() for r in rs]
    print(f"\nINSTRUMENT FAILURES   {len(lost)} of {len(rows)} runs "
          f"({pct(len(lost), len(rows)):.0f}%) - the harness, not the game")
    for key, rs in sorted(traps.items(), key=lambda kv: -len(kv[1])):
        spotted = statistics.mean(r.get("spotted", 0) for r in rs)
        clocks = statistics.mean(r.get("clocksFixed", 0) for r in rs)
        near = Counter(r.get("nearestLandmark", "?") for r in rs).most_common(1)[0][0]
        seeds = ",".join(str(r.get("seed")) for r in sorted(rs, key=lambda r: r.get("seed", 0))[:6])
        print(f"  ({key[0]:>6.1f},{key[1]:>6.1f})  {len(rs):>3} runs  near {near:<14}"
              f"clocks {clocks:.1f}  spotted {spotted:.1f}   seeds {seeds}")
        if spotted < 0.5:
            print("      the maniac never even saw these - they would likely have WON")

    # Bracket it rather than "correcting" it. Dropping the wedged runs is the
    # wrong operation - it removes them from the denominator too and barely
    # moves the number. What a reader needs is the range the true win rate can
    # sit in: every wedged run counted as a loss, versus every one as a win.
    wins = sum(1 for r in rows if r.get("won"))
    print(f"\n  win rate, wedged runs counted as losses : {pct(wins, len(rows)):>3.0f}%   <- what the table above says")
    print(f"  win rate, wedged runs counted as wins   : {pct(wins + len(lost), len(rows)):>3.0f}%")
    print("  The true value is somewhere between. That bracket is the width of")
    print("  your uncertainty, and it is entirely self-inflicted - fix the")
    print("  harness before tuning anything off these numbers.")
    return {id(r) for r in lost}


def failure_stages(rows):
    """WHERE a run ended matters more than whether it won.

    "0% win rate" is not a design brief. "40% never find a second clock" and
    "50% fix everything then cannot get out" are two different games with two
    different fixes, and the raw win rate cannot tell them apart.
    """
    stages = [
        ("died still looking (<2 clocks found)",
         lambda r: r.get("clocksFound", 0) < 2 and r.get("clocksFixed", 0) < r.get("clocksTotal", 3)),
        ("found them, died repairing",
         lambda r: r.get("clocksFixed", 0) < r.get("clocksTotal", 3)),
        ("all clocks fixed, no escape",
         lambda r: not r.get("won")),
        ("escaped", lambda r: True),
    ]
    print(f"\nHOW FAR THEY GOT   ({len(rows)} runs)")
    remaining = list(rows)
    for label, match in stages:
        hit = [r for r in remaining if match(r)]
        remaining = [r for r in remaining if r not in hit]
        if not hit:
            continue
        share = pct(len(hit), len(rows))
        print(f"  {label:<38}{len(hit):>4}  {share:>4.0f}%  {'#' * int(round(share / 3))}")


def death_map(rows):
    deaths = [r for r in rows if r.get("endReason") == "death"]
    if not deaths:
        return
    print(f"\nWHERE THEY DIE   ({len(deaths)} deaths)")
    for name, count in Counter(r.get("nearestLandmark", "?") for r in deaths).most_common(8):
        bar = "#" * int(round(30 * count / len(deaths)))
        print(f"  {name:<20}{pct(count, len(deaths)):>5.0f}%  {bar}")

    print("\n  raw positions (for a heatmap):")
    cells = Counter((round(r['endPos'][0] / 4) * 4, round(r['endPos'][1] / 4) * 4)
                    for r in deaths if r.get("endPos"))
    for (x, y), c in cells.most_common(6):
        print(f"    ({x:>5}, {y:>5})  {c}")


def quirks(rows):
    """Findings that are about the GAME, not the balance."""
    hides = sum(r.get("accidentalHides", 0) for r in rows)
    if hides:
        print(f"\nQUIRK: {hides} accidental wardrobe entries while trying to repair a clock.")
        print("  E is read by both ClockRepair and PlayerHiding at 2.2 range, and the")
        print("  clocks were placed beside wardrobes - a human hits this too.")
    stuck = [r for r in rows if r.get("endReason") == "timeout"]
    if stuck:
        print(f"\nQUIRK: {len(stuck)} runs timed out. Median clocks fixed in those: "
              f"{statistics.median([r.get('clocksFixed', 0) for r in stuck]):.0f}. "
              "Check for unreachable clocks or a bot that cannot find the exit.")


def main():
    if len(sys.argv) > 1:
        path = Path(sys.argv[1])
        if not path.is_absolute() and not path.exists():
            path = HERE / sys.argv[1]
    else:
        files = sorted(RESULTS.glob("*.jsonl"))
        if not files:
            print(f"No results yet. Run TimeKiller/Setup/33 in Unity first ({RESULTS}).")
            return 1
        path = files[-1]
    report(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
