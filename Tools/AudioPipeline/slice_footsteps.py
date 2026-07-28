"""Cut a walking SEQUENCE into individual footstep one-shots (pure stdlib).

The PSX pack ships footsteps only as continuous walks — 'tunnel steps' is 8.1s
of someone crossing a stone corridor. ManiacVoice triggers one clip per stride,
so handing it the sequence would stack eight-second walks on top of each other.

Method: rectify -> short-window energy envelope -> pick onsets that clear a
threshold with a refractory gap -> cut from just before each onset to just
before the next -> per-step peak normalise. Reports what it found so the result
can be judged from numbers rather than trusted.

Usage: python slice_footsteps.py            (analyse + write)
       python slice_footsteps.py --dry-run  (analyse only, write nothing)
"""
import math
import os
import struct
import sys
import wave

SRC = r"D:\The Time Killer Remake\Assets\Resources\Outsource\Audio\PSXHorrorSFX\pack itchio PSX\sfx\footsteps\tunnel steps.wav"
OUT_DIR = r"D:\The Time Killer Remake\Assets\Resources\Assets\ManiacVoice"
PREFIX = "maniac_step"

WINDOW_MS = 8.0        # energy window
# Comfortably under the measured 0.470s stride but well above the ~0.18s
# double-triggers the envelope produces on a single boot (heel then toe). At
# 180ms those doubles survived and truncated four of the eight clips to ~0.19s,
# cutting the tail off the step.
REFRACTORY_MS = 350.0
PRE_ROLL_MS = 12.0     # keep the attack transient, do not clip it
# A PERCENTILE of the envelope, not a share of its peak. The steps in this file
# range from 4640 to 18337 in amplitude, so any threshold set high enough to
# ignore the floor between loud steps silently drops the quiet ones — and every
# dropped step merges two real steps into one clip. Measured: at 0.22-of-peak
# the detector found 9 onsets with 0.96s and 1.46s gaps against a true stride of
# ~0.48s, i.e. it had missed roughly a third of them.
THRESHOLD_PERCENTILE = 80.0
MAX_STEP_MS = 420.0    # hard cap: one clip can never swallow the following step
MAX_STEPS = 8          # more than enough variety; keeps the asset set small


def read_mono(path):
    with wave.open(path, "rb") as w:
        channels, width, rate, frames = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        if width != 2:
            raise SystemExit(f"expected 16-bit source, got {width * 8}-bit")
        raw = w.readframes(frames)
    total = len(raw) // 2
    all_samples = struct.unpack("<%dh" % total, raw)
    if channels == 1:
        mono = list(all_samples)
    else:  # average the channels; these steps are near-identical L/R anyway
        mono = [sum(all_samples[i:i + channels]) // channels
                for i in range(0, total - channels + 1, channels)]
    return mono, rate, channels


def envelope(samples, rate):
    win = max(1, int(rate * WINDOW_MS / 1000.0))
    env, acc = [], 0
    for i, s in enumerate(samples):
        acc += abs(s)
        if i >= win:
            acc -= abs(samples[i - win])
        env.append(acc / win)
    return env


def percentile(values, pct):
    ordered = sorted(values)
    if not ordered:
        return 0.0
    k = (len(ordered) - 1) * pct / 100.0
    lo, hi = int(math.floor(k)), int(math.ceil(k))
    if lo == hi:
        return float(ordered[lo])
    return ordered[lo] * (hi - k) + ordered[hi] * (k - lo)


def find_onsets(env, rate):
    if not env or max(env) <= 0:
        return [], 0.0
    threshold = percentile(env, THRESHOLD_PERCENTILE)
    refractory = int(rate * REFRACTORY_MS / 1000.0)
    onsets, last = [], -refractory
    for i in range(1, len(env)):
        if env[i] >= threshold > env[i - 1] and i - last >= refractory:
            onsets.append(i)
            last = i
    return onsets, threshold


def stride_report(onsets, rate):
    """Gaps between onsets. A detector that missed steps shows up here as gaps
    at 2x and 3x the true stride, which is exactly how the first pass was caught."""
    if len(onsets) < 2:
        return "n/a"
    gaps = [(onsets[i + 1] - onsets[i]) / rate for i in range(len(onsets) - 1)]
    median = sorted(gaps)[len(gaps) // 2]
    doubles = sum(1 for g in gaps if g > median * 1.6)
    return (f"median gap {median:.3f}s, range {min(gaps):.3f}-{max(gaps):.3f}s, "
            f"{doubles} gap(s) look like a missed step")


def main():
    dry = "--dry-run" in sys.argv
    samples, rate, channels = read_mono(SRC)
    print(f"source: {os.path.basename(SRC)}")
    print(f"  {len(samples) / rate:.2f}s, {rate}Hz, {channels}ch -> mono")

    env = envelope(samples, rate)
    onsets, threshold = find_onsets(env, rate)
    print(f"  envelope peak {max(env):.0f}, onset threshold {threshold:.0f} "
          f"(p{THRESHOLD_PERCENTILE:.0f})")
    print(f"  detected {len(onsets)} step onsets at: "
          + ", ".join(f"{o / rate:.2f}s" for o in onsets))
    print(f"  stride check: {stride_report(onsets, rate)}")
    if not onsets:
        raise SystemExit("no onsets found - lower THRESHOLD_PERCENTILE")

    # Keep the loudest, cleanest steps rather than simply the first N — the
    # opening and closing steps of a walk are often half-faded.
    cap = int(rate * MAX_STEP_MS / 1000.0)
    scored = []
    for idx, onset in enumerate(onsets):
        end = min(len(samples), onset + cap)
        if idx + 1 < len(onsets):
            end = min(end, onsets[idx + 1])
        scored.append((max((abs(s) for s in samples[onset:end]), default=0), onset, end))
    scored.sort(key=lambda t: -t[0])
    chosen = sorted(scored[:MAX_STEPS], key=lambda t: t[1])

    pre = int(rate * PRE_ROLL_MS / 1000.0)
    written = 0
    for idx, (_, onset, end) in enumerate(chosen):
        start = max(0, onset - pre)
        chunk = samples[start:end]
        if not chunk:
            continue
        peak = max(abs(s) for s in chunk) or 1
        gain = (0.89 * 32767) / peak          # normalise to about -1 dBFS
        out = os.path.join(OUT_DIR, f"{PREFIX}_{idx + 1}.wav")
        print(f"  step {idx + 1}: {len(chunk) / rate:.3f}s  peak {peak:5d} -> gain x{gain:.2f}  {os.path.basename(out)}")
        if dry:
            continue
        os.makedirs(OUT_DIR, exist_ok=True)
        with wave.open(out, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(rate)
            w.writeframes(b"".join(
                struct.pack("<h", max(-32768, min(32767, int(s * gain)))) for s in chunk))
        written += 1
    print(f"{'(dry run) ' if dry else ''}wrote {written} step clip(s) to {OUT_DIR}")


if __name__ == "__main__":
    main()
