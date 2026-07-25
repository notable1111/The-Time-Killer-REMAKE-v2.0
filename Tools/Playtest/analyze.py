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

# Half the player capsule - BotPilot.DefaultBodyRadius. Rows written before the
# A/B knob existed carry no bodyRadius field and were all run at this value, so
# it is also the right default when the key is missing.
ARM_DEFAULT = 0.275


def arm_label(radius):
    """0 is not 'a smaller body' - it is the OLD ALGORITHM (the hairline
    string-pull GridPathfinder documents). Labelling it by its number invites
    reading the A/B as a body-size sweep, which it is not."""
    if radius == 0:
        return "old"
    return f"{radius:g}"


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


def engine_health(rows):
    """What the ENGINE actually achieved per game-second, at each speed.

    Every conclusion in the 2026-07-25 speed investigation turned on this and
    no batch before it recorded it, so none of them can ever be re-audited.

    Two rates, because they answer different questions:

      fixed steps  the bot's think, its steering, and the tension sampler all
                   ride the physics clock. Nominal is 1/fixedDeltaTime (50).
                   A shortfall is game time the physics loop never simulated,
                   and it shortens every duration in this report at once.
      frames       ManiacPerception and ClockRepair's marker still advance on
                   Update, so this is the resolution of his senses and of the
                   skill-check bar. It falls with timeScale BY DESIGN - the
                   point is that it is now written down instead of guessed.
    """
    have = [r for r in rows if isinstance(r.get("engine"), dict)]
    if not have:
        print("\nENGINE   not recorded in this file (pre-2026-07-25 harness).")
        print("  Any speed claim below rests on a frame rate nobody measured.")
        return

    print("\nENGINE   what the engine achieved per GAME second")
    print(f"  {'x':>4}{'n':>5}{'frames/game-s':>16}{'fixed/game-s':>15}{'nominal':>10}{'worst run':>12}")
    by_speed = defaultdict(list)
    for r in have:
        by_speed[r.get("speed", 1)].append(r["engine"])

    short = False
    for speed in sorted(by_speed):
        eng = by_speed[speed]
        fps = statistics.median(e.get("framesPerGameSecond", 0) for e in eng)
        fx = statistics.median(e.get("fixedStepsPerGameSecond", 0) for e in eng)
        nominal = eng[0].get("nominalFixedSteps", 50.0)
        worst = min(e.get("fixedStepsPerGameSecond", 0) for e in eng)
        if worst < 0.95 * nominal:
            short = True
        print(f"  {speed:>4g}{len(eng):>5}{fps:>16.1f}{fx:>15.1f}{nominal:>10.1f}{worst:>12.1f}")

    if short:
        print("\n  WARNING: at least one run fell short of the nominal fixed-step rate.")
        print("  That is game time the physics loop skipped. Every duration in this")
        print("  report is measured, not assumed, so nothing is inflated - but the")
        print("  maniac genuinely thought less in those runs. Drop the speed.")


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
    # Instrument before result, same rule as everywhere else in this file: the
    # frame and physics rates are the hidden variable behind every speed claim.
    engine_health(rows)

    # The cell key carries bodyRadius. It MUST: an A/B batch puts two different
    # pathfindings in one file, and merging them into one cell would average a
    # baseline into the thing it is the baseline for - the exact mistake that
    # makes a change look like it did nothing.
    by_cell = defaultdict(list)
    for r in rows:
        by_cell[(r.get("profile", "?"), r.get("speed", 1), r.get("bodyRadius", ARM_DEFAULT))].append(r)

    honest = [k for k in by_cell if not by_cell[k][0].get("knowsEverything")]
    oracle = [k for k in by_cell if by_cell[k][0].get("knowsEverything")]
    arms = sorted({k[2] for k in by_cell})

    for title, keys in (("HONEST (had to find the clocks)", honest),
                        ("ORACLE (knew the map - upper bound, do not mix)", oracle)):
        if not keys:
            continue
        print(f"\n{title}")
        print(f"  {'profile':<16}{'x':>4}{'body':>7}{'n':>5}{'win%':>7}{'clocks':>9}"
              f"{'skill%':>8}{'found':>7}{'map%':>7}{'median s':>10}{'t/o':>5}")
        for key in sorted(keys):
            s = cell_summary(by_cell[key])
            print(f"  {key[0]:<16}{key[1]:>4g}{arm_label(key[2]):>7}{s['n']:>5}{s['win']:>7.0f}"
                  f"{s['clocks']:>6.1f}/{s['total_clocks']:<2}{s['skill']:>8.0f}"
                  f"{s['found']:>7.1f}{s['explored']:>7.0f}{s['seconds']:>10.0f}{s['timeouts']:>5}")
    if len(arms) > 1:
        print("\n  Two pathfindings above - 'body' is the arm. Never read a row against")
        print("  another arm's row by eye; the paired comparison below is the answer.")

    speed_check(by_cell)
    pathfinding_ab(rows)
    clock_timeline(rows)
    tension_report(rows)
    # Before any difficulty claim: whether the instrument was sound, and where
    # the runs actually ended. A win rate read past a wedged bot is a fiction.
    instrument_check(rows)
    failure_stages([r for r in rows if not r.get("knowsEverything")] or rows)
    difficulty_verdict(by_cell, honest)
    death_map(rows)
    cover_map(rows)
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


# Tension metrics are scored SEPARATELY from PAIRED_METRICS, not merged into
# them. Merging would raise that list's `scored - 1` verdict threshold from 3-of-4
# to 7-of-8 and quietly make the outcome speed check less sensitive than it is
# today. They also deserve their own verdict on the merits: ManiacPerception
# ticks on Update while the sampler is fixed-step, so tension degrades at speed
# EARLIER than win rate does. A speed that is honest for escape % can already be
# lying about whether the run was frightening.
TENSION_METRICS = [
    ("near misses", lambda r: near_misses(r), -1),
    ("chase seconds", lambda r: tget(r, "chaseSeconds"), -1),
    # stalkSeconds, not dreadSeconds: dread is 4 Hz-sampled and dominated by the
    # post-spot drain tail, whose length awarenessDrainRate fixes. Falls back to
    # dread for files recorded before the split so old batches still compare.
    ("stalk seconds", lambda r: tget(r, "stalkSeconds", tget(r, "dreadSeconds")), -1),
    ("longest dead air", lambda r: tget(r, "longestDeadAir"), +1),
]


def tget(row, key, default=None):
    """Read a tension field, or None if this row predates the instrument."""
    t = row.get("tension")
    return t.get(key, default) if isinstance(t, dict) else None


def near_misses(row):
    """All three outcomes. `clipped` is absent in pre-split files, where those
    episodes were discarded outright rather than counted - so it defaults to 0
    and an old file still reads correctly, just lower than it should have."""
    parts = [tget(row, k, 0) for k in ("nearMissHidden", "nearMissOpen", "nearMissClipped")]
    return sum(p for p in parts if p is not None) if parts[0] is not None else None


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
    for p, s, rad in sorted(by_cell):
        # The 1x control must be paired WITHIN an arm. Comparing 6x new against
        # 1x old would fold two different questions into one verdict.
        if s == 1 or (p, 1, rad) not in by_cell:
            continue
        if not header_done:
            print("\nSPEED CHECK (matched seeds)")
            header_done = True
        speed_pair(by_cell, p, s, rad)

    if not header_done:
        print("\nSPEED CHECK: no 1x control block - accelerated numbers are unvalidated.")
        return

    # A concentrated control validates ONE profile. Say so out loud: an
    # unvalidated profile and a validated one look identical once aggregated,
    # and that is exactly the confusion that gets a bad speed trusted.
    validated = {(p, rad) for p, s, rad in by_cell if s == 1}
    rest = sorted({(p, rad) for p, s, rad in by_cell if s != 1} - validated)
    rest = [f"{p} ({arm_label(rad)})" for p, rad in rest]
    if rest:
        print(f"\n  NOT validated (no 1x runs): {', '.join(rest)}")
        print("  They inherit the verdict above only if the accelerator distorts")
        print("  every profile equally - plausible, but not measured here.")


def speed_pair(by_cell, profile, speed, radius=ARM_DEFAULT):
    slow_rows, fast_rows = by_cell[(profile, 1, radius)], by_cell[(profile, speed, radius)]
    ks, ns = sum(1 for r in slow_rows if r.get("won")), len(slow_rows)
    kf, nf = sum(1 for r in fast_rows if r.get("won")), len(fast_rows)
    slo, shi = wilson(ks, ns)
    flo, fhi = wilson(kf, nf)

    print(f"\n  {profile} [{arm_label(radius)}]   1x {pct(ks, ns):.0f}% [{slo:.0f}-{shi:.0f}] n={ns}"
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

    tension_speed_block(pairs, speed)


def tension_speed_block(pairs, speed):
    """The same matched-seed test, scored on tension instead of outcome.

    Kept apart from the verdict above because the two can legitimately
    disagree, and when they do the answer is "this speed is fine for balance
    numbers and NOT fine for tension numbers" - which is a usable answer, and
    one a single merged verdict could never express.
    """
    usable = [(a, b) for a, b in pairs
              if isinstance(a.get("tension"), dict) and isinstance(b.get("tension"), dict)]
    if not usable:
        return

    print(f"\n    tension, same pairs ({len(usable)} with a trace):")
    suspicious = scored = 0
    for label, extract, easier in TENSION_METRICS:
        up, down, tied, mean_delta = sign_test(usable, extract)
        moved = up + down
        note = ""
        if moved >= 6:
            scored += 1
            lean = up if easier > 0 else down
            if lean / moved >= 0.8:
                suspicious += 1
                note = "  <- leans DULLER at speed"
        print(f"      {label:<17}{mean_delta:+8.2f}   up {up:>3} / down {down:>3}"
              f" / tied {tied:>3}{note}")

    if scored and suspicious >= max(2, scored - 1):
        print(f"      VERDICT: tension is UNTRUSTWORTHY at {speed:g}x - {suspicious}/{scored}")
        print("               metrics say the run was calmer on the same seed. Read the")
        print("               tension section from 1x runs only.")
    elif scored:
        print(f"      VERDICT: ok - {suspicious}/{scored} lean duller, consistent with noise.")
    else:
        print("      VERDICT: too few paired traces to score tension at this speed.")


def stuck_rates(rows):
    """Pooled wall-stuck rates. Rates, never raw counts.

    The denominator is travel seconds, not runs, because the two arms do not
    survive equally long: a bot that wedges less also lives longer, which would
    hand the better arm a bigger count and make it look worse.
    """
    ev = sum(r.get("stuck", {}).get("events", 0) for r in rows)
    give = sum(r.get("stuck", {}).get("giveUps", 0) for r in rows)
    wedged = sum(r.get("stuck", {}).get("wedgedSeconds", 0.0) for r in rows)
    travel = sum(r.get("stuck", {}).get("travelSeconds", 0.0) for r in rows)
    return {
        "n": len(rows),
        "events": ev,
        "giveUps": give,
        "per100": 100.0 * ev / travel if travel else 0.0,
        "wedged_pct": pct(wedged, travel),
        "travel": travel,
        "clean": pct(sum(1 for r in rows if not r.get("stuck", {}).get("events", 0)), len(rows)),
    }


def pathfinding_ab(rows):
    """Old hairline string-pull vs body-width-aware, on identical seeds.

    This is the only comparison in the file that can answer "did the
    pathfinding change work". It exists because the stuck counters are new:
    no earlier batch recorded them, so the baseline has to be measured
    alongside, or it does not exist at all.
    """
    instrumented = [r for r in rows if "stuck" in r]
    if not instrumented:
        print("\nWALL-STUCK: not recorded in this file (pre-instrumentation batch).")
        return
    arms = sorted({r.get("bodyRadius", ARM_DEFAULT) for r in instrumented})

    print(f"\nWALL-STUCK   ({len(instrumented)} instrumented runs)")
    print(f"  {'arm':<8}{'n':>5}{'events':>8}{'/100s':>8}{'giveUps':>9}"
          f"{'wedged%':>9}{'clean runs%':>13}")
    for rad in arms:
        s = stuck_rates([r for r in instrumented if r.get("bodyRadius", ARM_DEFAULT) == rad])
        print(f"  {arm_label(rad):<8}{s['n']:>5}{s['events']:>8}{s['per100']:>8.2f}"
              f"{s['giveUps']:>9}{s['wedged_pct']:>8.1f}%{s['clean']:>12.0f}%")

    if len(arms) < 2:
        print("\n  One arm only - this is an absolute rate with nothing to compare it")
        print("  to. Re-run with 'A/B pathfinding' ticked to get the baseline.")
        return

    old, new = arms[0], arms[-1]
    # Pair on (profile, speed, seed): everything except the pathfinding is held
    # identical, so a per-pair delta has no other explanation available.
    def index(rad):
        return {(r.get("profile"), r.get("speed"), r.get("seed")): r
                for r in instrumented if r.get("bodyRadius", ARM_DEFAULT) == rad}

    a, b = index(old), index(new)
    pairs = [(a[k], b[k]) for k in sorted(set(a) & set(b), key=lambda k: (str(k[0]), k[1], k[2]))]
    if not pairs:
        print("\n  No matched seeds across arms - cannot pair. Was BaseSeed changed?")
        return

    print(f"\n  MATCHED PAIRS: {len(pairs)}   (delta = {arm_label(new)} minus {arm_label(old)},"
          " same seed, same maniac)")
    better = worse = tied = 0
    for old_row, new_row in pairs:
        def rate(r):
            t = r.get("stuck", {}).get("travelSeconds", 0.0)
            return 100.0 * r.get("stuck", {}).get("events", 0) / t if t else None
        ra, rb = rate(old_row), rate(new_row)
        if ra is None or rb is None:
            continue
        if rb < ra - 1e-9:
            better += 1
        elif rb > ra + 1e-9:
            worse += 1
        else:
            tied += 1

    moved = better + worse
    print(f"    stuck rate improved on {better} seeds, worsened on {worse}, tied on {tied}")
    if moved < 6:
        print("    VERDICT: too few seeds moved to score. The change is not visible here.")
    elif better / moved >= 0.75:
        print("    VERDICT: body-width pathfinding REDUCES wall-stuck. The offline")
        print("             collider sweep is confirmed by live runs.")
    elif worse / moved >= 0.75:
        print("    VERDICT: body-width pathfinding made stuck WORSE. Likely the hug")
        print("             penalty pushing routes into places A* would not have gone.")
    else:
        print("    VERDICT: no clear direction - the arms are indistinguishable on")
        print("             wall-stuck at this sample size. That is a real (null)")
        print("             result, not a reason to re-roll the batch.")


def clock_timeline(rows):
    """When each clock landed on the run clock.

    A timeout row without this is a guess. With it, "stopped finding clocks"
    and "never got time to work" are two different, visible shapes.
    """
    timed = [r for r in rows if r.get("clockFixTimes")]
    if not timed:
        print("\nCLOCK TIMELINE: not recorded in this file (pre-instrumentation batch).")
        return

    total = max((r.get("clocksTotal", 3) for r in timed), default=3)
    print(f"\nCLOCK TIMELINE   ({len(timed)} runs that fixed at least one)")
    print(f"  {'clock':<8}{'runs':>6}{'median s':>11}{'gap from prev':>16}")
    prev_median = 0.0
    for i in range(total):
        times = [r["clockFixTimes"][i] for r in timed if len(r.get("clockFixTimes", [])) > i]
        if not times:
            continue
        med = statistics.median(times)
        print(f"  #{i + 1:<7}{len(times):>6}{med:>11.0f}{med - prev_median:>+16.0f}")
        prev_median = med

    # The shape that matters: a run that got all its clocks early and still lost
    # is an ESCAPE problem; one whose clocks are spread to the buzzer is a
    # SEARCH problem. Same "0 wins", opposite fixes.
    lost_late = [r for r in timed
                 if not r.get("won") and len(r["clockFixTimes"]) == r.get("clocksTotal", 3)]
    if lost_late:
        last = [r["clockFixTimes"][-1] for r in lost_late]
        print(f"\n  {len(lost_late)} runs fixed EVERY clock and still lost "
              f"(last clock at median {statistics.median(last):.0f}s).")
        print("  That is an escape-route problem, not a difficulty problem.")


# 10 levels, ASCII on purpose. The Windows console this is read on does not
# reliably encode block-drawing characters, and a report that crashes on print
# is worse than one that draws a coarser curve.
RAMP = " .:-=+*#%@"


def spark(values):
    return "".join(RAMP[min(len(RAMP) - 1, max(0, int(v * len(RAMP))))] for v in values)


def threshold_note(traced):
    """State the thresholds this trace was measured with, and refuse to average
    across two of them.

    They are now DERIVED from ManiacConfig rather than hard-coded, which fixed a
    silent-drift bug and introduced a subtler one: retune the maniac and every
    tension number shifts underneath you with nothing on screen to say so. Rows
    carry their own thresholds precisely so this check is possible.
    """
    combos = {(tget(r, "nearMissRadius"), tget(r, "feltRadius"), tget(r, "awareThreshold"))
              for r in traced}
    combos.discard((None, None, None))
    if not combos:
        print("  thresholds: not recorded (batch predates derivation - it used the")
        print("              hard-coded 4 / 12 / 0.25, two of which were wrong).")
        return
    if len(combos) > 1:
        print("  WARNING: rows in this file were measured with DIFFERENT thresholds:")
        for nm, felt, aware in sorted(combos):
            print(f"           near {nm}  felt {felt}  aware {aware}")
        print("           The maniac was retuned mid-file. Do not average these.")
        return
    nm, felt, aware = combos.pop()
    print(f"  thresholds: near miss <= {nm}   felt <= {felt}   aware >= {aware}"
          f"   (derived from ManiacConfig)")

    # fadeSeconds cannot exceed this per lost contact no matter what the level
    # does. Printed so nobody reads a small fade as "the game lacks dread".
    drain = next((tget(r, "drainRate") for r in traced if tget(r, "drainRate")), None)
    if drain:
        print(f"  fade ceiling: {(1 - aware) / drain:.2f}s per lost contact "
              f"(drain {drain}/s) - a config fact, not a level one.")


def tension_report(rows):
    """The shape of a run, as opposed to its outcome.

    This section exists because win rate cannot distinguish a horror game from
    a chore. Two builds can both sit at 40% escape while one is five
    heart-stopping encounters and the other is a long quiet walk with one
    ambush at the end. Everything above measures whether the bot got out;
    this measures whether getting out was frightening.

    NOTHING HERE IS GRADED YET, by decision: unlike win rate, tension curve
    shape has no genre referent to target, so a verdict invented before seeing
    real traces would just be this file grading the game against a shape
    nobody has confirmed the level can even produce. Read the numbers, then
    write the verdict.
    """
    traced = [r for r in rows if isinstance(r.get("tension"), dict)]
    if not traced:
        print("\nTENSION: not recorded in this file (pre-instrumentation batch).")
        return

    print(f"\nTENSION   ({len(traced)} of {len(rows)} runs carry a trace)")

    # Instrument health FIRST, same as everywhere else in this file. The
    # sampler is fixed-step, but Unity drops fixed steps under load, and a
    # thinned trace shortens every duration below without ever looking wrong.
    rates = []
    for r in traced:
        secs, n = r.get("runSeconds", 0), tget(r, "samples", 0) or 0
        if secs > 5:
            rates.append(n / (secs * 4.0))   # 4 Hz nominal = 1 / SampleInterval
    if rates:
        achieved = statistics.median(rates)
        if achieved < 0.8:
            print(f"  WARNING: the sampler achieved only {100*achieved:.0f}% of its 4 Hz rate.")
            print("  Fixed steps were dropped, so every DURATION below is understated.")
            print("  Counts (near misses, episodes) survive this; seconds do not.")

    by_cell = defaultdict(list)
    for r in traced:
        by_cell[(r.get("profile", "?"), r.get("speed", 1))].append(r)

    threshold_note(traced)

    print(f"  {'profile':<16}{'x':>4}{'n':>5}{'near/run':>10}{'hid':>5}{'open':>6}{'clip':>6}"
          f"{'chase s':>9}{'longest':>9}{'stalk s':>9}{'fade s':>8}{'dead air':>10}")
    for key in sorted(by_cell):
        rs = by_cell[key]
        n = len(rs)

        def mean(fn):
            vals = [fn(r) for r in rs if fn(r) is not None]
            return statistics.mean(vals) if vals else 0.0

        hidden = mean(lambda r: tget(r, "nearMissHidden", 0))
        openm = mean(lambda r: tget(r, "nearMissOpen", 0))
        clip = mean(lambda r: tget(r, "nearMissClipped", 0))
        print(f"  {key[0]:<16}{key[1]:>4g}{n:>5}{hidden + openm + clip:>10.1f}"
              f"{hidden:>5.1f}{openm:>6.1f}{clip:>6.1f}"
              f"{mean(lambda r: tget(r, 'chaseSeconds')):>9.0f}"
              f"{mean(lambda r: tget(r, 'longestChase')):>9.0f}"
              f"{mean(lambda r: tget(r, 'stalkSeconds')):>9.0f}"
              f"{mean(lambda r: tget(r, 'fadeSeconds')):>8.0f}"
              f"{mean(lambda r: tget(r, 'longestDeadAir')):>10.0f}")

    if all(tget(r, "stalkSeconds") is None for r in traced):
        print("\n  stalk/fade: not recorded (pre-split batch). The 'dread seconds' this")
        print("  file carries mixes both, and is mostly the fade tail - see below.")

    # The curve. Unitless by construction (see TestTelemetry) - only its shape,
    # and the difference between two builds' shapes, carries any meaning.
    print(f"\n  MEAN THREAT CURVE   start -> end, 12 slices of each run")
    print(f"  scale: low [{RAMP.strip()}] high   (unitless - compare shapes, never the value)")
    for key in sorted(by_cell):
        curves = [tget(r, "curve") for r in by_cell[key]]
        curves = [c for c in curves if isinstance(c, list) and len(c) == 12]
        if not curves:
            continue
        mean_curve = [statistics.mean(c[i] for c in curves) for i in range(12)]
        print(f"  {key[0]:<16}{key[1]:>3g}x  |{spark(mean_curve)}|  "
              f"peak at slice {1 + mean_curve.index(max(mean_curve))}/12")

    print("\n  The number to watch first is DEAD AIR. A healthy escape rate with a")
    print("  90-second quiet stretch is a balanced game that is not worth playing,")
    print("  and it is the one failure nothing else in this report can see.")
    print("  STALK is the stalking beat and answers to level design. FADE is the")
    print("  forgetting tail after he loses you: its length is fixed by")
    print("  awarenessDrainRate, so it is a tuning readout, never a verdict.")


def difficulty_verdict(by_cell, honest_keys):
    """The number that matters is the SPREAD, not any single win rate.

    Read off the speed each profile has the most runs at - the 1x control block
    is deliberately small and would otherwise decide this on five samples.
    """
    # Difficulty is a claim about the SHIPPING game, so it is read off the real
    # body only. Averaging the old-pathfinding arm in would report a balance
    # figure for a build nobody will play.
    shipping = max((k[2] for k in honest_keys), default=ARM_DEFAULT)
    best = {}
    for p, s, rad in honest_keys:
        if rad != shipping:
            continue
        n = len(by_cell[(p, s, rad)])
        if p not in best or n > best[p][0]:
            best[p] = (n, cell_summary(by_cell[(p, s, rad)])["win"])
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


def cover_map(rows):
    """WHICH hiding spots get used - the counterpart to WHERE THEY DIE.

    `hides` alone cannot tell one wardrobe used nine times from nine wardrobes
    used once, and those are opposite verdicts: the first says the castle has
    one piece of real cover, the second says its cover is spread and working.
    The positions were already on PlayerHidEvent and were being discarded.
    """
    used, runs_with = Counter(), 0
    for r in rows:
        spots = r.get("hideSpots")
        if not isinstance(spots, list):
            continue
        if spots:
            runs_with += 1
        for p in spots:
            used[(round(p[0], 1), round(p[1], 1))] += 1
    if not used:
        return

    total = sum(used.values())
    print(f"\nWHICH COVER GETS USED   ({total} hides across {runs_with} runs, "
          f"{len(used)} distinct spots)")
    for (x, y), count in used.most_common(8):
        bar = "#" * int(round(30 * count / total))
        print(f"  ({x:>6}, {y:>6}){pct(count, total):>6.0f}%  {bar}")
    top = used.most_common(1)[0][1]
    if len(used) > 1 and top > 0.5 * total:
        print("\n  Over half of all hides happen at ONE spot. Either it is the only")
        print("  cover on the bot's real route, or the rest is too far off it to")
        print("  reach in a panic - both are level problems, not AI ones.")


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
