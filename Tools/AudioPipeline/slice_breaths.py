"""Cut a breathing RECORDING into individual inhale / exhale one-shots.

WHY (research 2026-08-02). The player breathing was one looping clip, pitch-
shifted 0.9 -> 1.4 to suggest effort. That is wrong on three counts, and the
professional structure fixes all three at once:

  * CRI Middleware's player-breathing system: "the basis for a breathing system
    is the alternance between inhale and exhale sounds" — sequential one-shots,
    shuffled from a pool, with intensity driving the SEQUENCING RATE.
  * A single loop has a seam the ear locks onto within seconds.
  * Pitching a whole breath up 40% does not sound faster, it sounds SMALLER —
    the chipmunk effect. Real systems change the cycle rate and swap samples,
    and leave pitch close to natural.

Source: the PSX pack ships breathing only as continuous takes. 'strong breathe
person.wav' is 5.1s containing ~12 breath events — a whole pool hiding inside
the clip that was being looped whole.

Method: rectify -> 30ms RMS envelope -> onsets that rise through a threshold
with a refractory gap -> cut from just before each onset to just before the
next -> classify -> per-clip peak normalise. Numbers are reported so the result
can be judged rather than trusted.

CLASSIFICATION is by zero-crossing rate, which tracks brightness cheaply: an
inhale is turbulent and hissy (high ZCR), an exhale is lower and breathier. It
is a heuristic, not physics — the report prints every clip's ZCR so a wrong
split is visible instead of silent.

Usage: python slice_breaths.py            (analyse + write)
       python slice_breaths.py --dry-run  (analyse only, write nothing)
"""
import array
import math
import sys
import wave
from pathlib import Path

ROOT = Path(r"D:\The Time Killer Remake")
SOURCES = [
    ROOT / "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/strong breathe person.wav",
    ROOT / "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/sleeping breathe.wav",
]
DEST = ROOT / "Assets/Resources/Assets/Heartbeat/Breaths"

WINDOW = 0.03        # envelope window, seconds
# Threshold is a PERCENTILE of the envelope, not a share of the peak. A share of
# the peak fails whenever a recording contains one loud transient: on
# 'sleeping breathe.wav' (peaks at 0dBFS) it found ZERO breaths, because 28% of
# that peak sat above every actual breath in the file.
ONSET_PERCENTILE = 0.62
REFRACTORY = 0.18    # seconds. Short on purpose: an inhale and its exhale are
                     # separate events and must not be merged into one cycle.
TAIL_FLOOR_PERCENTILE = 0.35
MIN_LEN = 0.14       # discard anything shorter — it is a click, not a breath
MAX_LEN = 1.60
TARGET_PEAK = 0.89   # per-clip normalise, leaving headroom for the mix


def read_wav(path):
    with wave.open(str(path), "rb") as w:
        frames, rate, channels = w.getnframes(), w.getframerate(), w.getnchannels()
        raw = array.array("h")
        raw.frombytes(w.readframes(frames))
    samples = list(raw)
    if channels > 1:
        samples = [sum(samples[i:i + channels]) / channels
                   for i in range(0, len(samples), channels)]
    return [s / 32768.0 for s in samples], rate


def write_wav(path, samples, rate):
    path.parent.mkdir(parents=True, exist_ok=True)
    out = array.array("h", (int(max(-1.0, min(1.0, s)) * 32767) for s in samples))
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(out.tobytes())


def envelope(samples, rate):
    win = max(1, int(WINDOW * rate))
    env = []
    for i in range(0, len(samples) - win, win):
        chunk = samples[i:i + win]
        env.append(math.sqrt(sum(x * x for x in chunk) / win))
    return env, win


def zero_crossing_rate(samples):
    if len(samples) < 2:
        return 0.0
    crossings = sum(1 for i in range(1, len(samples))
                    if (samples[i - 1] >= 0) != (samples[i] >= 0))
    return crossings / len(samples)


def find_breaths(samples, rate):
    env, win = envelope(samples, rate)
    if not env:
        return []
    ordered = sorted(env)
    onset_level = ordered[min(len(ordered) - 1, int(len(ordered) * ONSET_PERCENTILE))]
    tail_level = ordered[min(len(ordered) - 1, int(len(ordered) * TAIL_FLOOR_PERCENTILE))]
    refractory = max(1, int(REFRACTORY / WINDOW))

    onsets = []
    last = -10 ** 6
    for i in range(1, len(env)):
        rising = env[i] > onset_level and env[i - 1] <= onset_level
        if rising and i - last > refractory:
            onsets.append(i)
            last = i

    cuts = []
    for n, start_i in enumerate(onsets):
        # Begin slightly before the onset so the breath's own attack survives.
        begin = max(0, (start_i - 1) * win)
        limit = onsets[n + 1] * win if n + 1 < len(onsets) else len(samples)
        # End where it decays, not where the next one starts — that keeps the
        # silence between breaths OUT of the sample, which is the whole point:
        # the sequencer supplies the gap, so the gap must be controllable.
        end = limit
        for j in range(start_i + 1, min(len(env), limit // win)):
            if env[j] < tail_level:
                end = min(limit, (j + 1) * win)
                break
        if MIN_LEN <= (end - begin) / rate <= MAX_LEN:
            cuts.append((begin, end))
    return cuts


def normalise(clip):
    peak = max((abs(s) for s in clip), default=0.0)
    if peak <= 1e-6:
        return clip
    return [s * (TARGET_PEAK / peak) for s in clip]


def main():
    dry = "--dry-run" in sys.argv
    written = 0

    for source in SOURCES:
        if not source.exists():
            print(f"MISSING {source}")
            continue
        samples, rate = read_wav(source)
        cuts = find_breaths(samples, rate)
        print(f"\n{source.name}  {len(samples)/rate:.2f}s  ->  {len(cuts)} breaths")

        pool = []
        for begin, end in cuts:
            clip = normalise(samples[begin:end])
            zcr = zero_crossing_rate(clip)
            print(f"    {begin/rate:5.2f}s  len {(end-begin)/rate:4.2f}s  zcr {zcr:.4f}")
            pool.append((zcr, clip, rate))
        if not pool:
            continue

        # Split at THIS source's own median. A single global median split the
        # pool by RECORDING instead of by phase — 'sleeping breathe' sits near
        # zcr 0.49 and 'strong breathe person' near 0.10, so every clip from one
        # file landed on one side. Inhale-vs-exhale is only meaningful within one
        # take, by the same lungs, in the same room.
        pool.sort(key=lambda item: item[0])
        median = pool[len(pool) // 2][0]
        inhale_set = [c for c in pool if c[0] >= median]
        exhale_set = [c for c in pool if c[0] < median]

        # Each recording keeps its own prefix. They are different people in
        # different rooms; mixing them into one pool would breathe like two
        # players. The config chooses a set — it never blends them.
        stem = source.stem.split()[0].lower()
        print(f"  -> median zcr {median:.4f}: {len(inhale_set)} inhale (brighter), "
              f"{len(exhale_set)} exhale (darker), prefix '{stem}'")

        if dry:
            continue
        for i, (_, clip, r) in enumerate(inhale_set):
            write_wav(DEST / f"{stem}_inhale_{i}.wav", clip, r)
        for i, (_, clip, r) in enumerate(exhale_set):
            write_wav(DEST / f"{stem}_exhale_{i}.wav", clip, r)
        written += len(inhale_set) + len(exhale_set)

    if dry:
        print("\n(dry run, nothing written)")
    else:
        print(f"\nwrote {written} one-shots to {DEST}")


if __name__ == "__main__":
    main()
