#!/usr/bin/env python3
"""What can a batch of this size actually resolve? Answer it BEFORE reading results.

    python Tools/Playtest/power.py            # the batch as configured (20/profile)
    python Tools/Playtest/power.py 40         # what 40 runs per profile would buy

Why this exists
---------------
Once numbers are on screen it is nearly impossible not to read a story into
them. "Novice 25%, expert 40%" looks like skill being rewarded. At 20 runs per
profile it is equally consistent with the two being IDENTICAL - the sample
cannot tell. Deciding the resolution limit in advance is the cheap insurance
against that, and it is the same discipline that caught the speed check's
broken +-5 rule before it could condemn an honest accelerator.

This computes power EXACTLY (enumerating both binomials - n is small, so there
is no reason to approximate) and it scores the decision rules analyze.py really
applies, not a textbook test we never run:

    difficulty_verdict:  spread = expert - novice
                         spread <  20  -> "Flat: skill is not being rewarded"
                         novice >  60  -> "Too easy"
                         expert <  35  -> "Too hard"

So the question is not the abstract "is this significant" but the concrete
"how often does that verdict tell me the truth about the game I actually have".
"""

import sys
from math import comb


def binom(k, n, p):
    return comb(n, k) * (p ** k) * ((1 - p) ** (n - k))


def verdict_rates(n, p_novice, p_expert):
    """P(each verdict) given TRUE win rates, over every possible batch outcome.

    Exact: enumerate all (k_novice, k_expert) pairs and weight by their joint
    probability. n=20 means 441 pairs - cheaper than a simulation and no noise.
    """
    out = {"flat": 0.0, "too easy": 0.0, "too hard": 0.0, "healthy": 0.0}
    for kn in range(n + 1):
        pn = binom(kn, n, p_novice)
        if pn < 1e-15:
            continue
        for ke in range(n + 1):
            joint = pn * binom(ke, n, p_expert)
            if joint < 1e-15:
                continue
            lo, hi = 100.0 * kn / n, 100.0 * ke / n
            # Mirrors analyze.py's branch ORDER, which decides ties.
            if lo > 60:
                out["too easy"] += joint
            elif hi < 35:
                out["too hard"] += joint
            elif hi - lo < 20:
                out["flat"] += joint
            else:
                out["healthy"] += joint
    return out


def wilson_width(n, p=0.5, z=1.96):
    d = 1 + z * z / n
    half = z * ((p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5) / d
    return 200 * half


def min_detectable(n, p_novice=0.25, target=0.80):
    """Smallest TRUE expert rate the 'spread >= 20' rule catches `target` of the time."""
    for step in range(0, 76):
        p_expert = p_novice + step / 100.0
        if p_expert > 1.0:
            break
        if verdict_rates(n, p_novice, p_expert)["healthy"] >= target:
            return p_expert - p_novice
    return None


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 20
    print(f"\nRESOLUTION OF A {n}-RUN-PER-PROFILE BATCH")
    print("=" * 74)
    print(f"\nA single win rate at n={n} carries a 95% interval about "
          f"{wilson_width(n):.0f} points wide (at p=0.5).")
    print("Any two numbers closer together than that are the same number.")

    print(f"\nHOW OFTEN THE VERDICT IS RIGHT   (true rates -> P(each verdict), n={n})")
    print(f"  {'novice':>7}{'expert':>8}{'spread':>8}  |{'healthy':>9}{'flat':>8}"
          f"{'too easy':>10}{'too hard':>10}")
    scenarios = [
        (0.25, 0.70, "the target band - what we HOPE the level is"),
        (0.25, 0.65, "target band, low end"),
        (0.30, 0.50, "skill helps, modestly"),
        (0.35, 0.45, "skill barely matters"),
        (0.40, 0.40, "skill does NOT matter at all"),
        (0.05, 0.15, "level is brutal for everyone"),
        (0.70, 0.85, "level is soft for everyone"),
    ]
    for pn, pe, label in scenarios:
        r = verdict_rates(n, pn, pe)
        print(f"  {pn * 100:>6.0f}%{pe * 100:>7.0f}%{(pe - pn) * 100:>7.0f}  |"
              f"{r['healthy'] * 100:>8.0f}%{r['flat'] * 100:>7.0f}%"
              f"{r['too easy'] * 100:>9.0f}%{r['too hard'] * 100:>9.0f}%   {label}")

    mdd = min_detectable(n)
    print()
    if mdd is None:
        print(f"  No spread, however large, is caught 80% of the time at n={n}.")
    else:
        print(f"  MINIMUM RELIABLE SPREAD: about {mdd * 100:.0f} points.")
        print(f"  Below that, a real difference is more likely to be MISSED than found.")

    print("\nWHAT THIS MEANS FOR THE READING")
    print("  * A 'flat' verdict does NOT mean skill is not rewarded. At this n it is")
    print("    also the most likely verdict when skill IS rewarded but by less than")
    print("    the minimum spread above. Absence of evidence, again.")
    print("  * The 'too hard' branch fires on the EXPERT rate alone, so it is the")
    print("    most trustworthy of the three - one proportion, not a difference.")
    print("  * Instrument failures make this worse than it looks: every wedged run")
    print("    is a coin flip replaced by a guaranteed loss, which shrinks the")
    print("    observed spread toward zero and biases us toward 'flat' and 'too hard'.")
    print("\n  Commit to the reading BEFORE the numbers land, or the numbers will")
    print("  choose the reading for you.\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
