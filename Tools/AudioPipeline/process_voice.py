"""Trim and normalise generated voice clips (pure stdlib).

Raw TTS output is unusable as a game one-shot: the first test clip came back
8.32s long carrying ~2.8s of content at RMS -18.7 dBFS, with silence at both
ends. Dropped into the mix as-is, every clip would sit at a different level and
the priority ducking would be fighting the source material instead of shaping it.

Per clip: stereo -> mono, trim silence off both ends (with a short pre-roll so
the attack is never clipped), peak-normalise to a common target, and report the
before/after so the batch can be judged from numbers.

Usage: python process_voice.py <src_dir> <out_dir> [--dry-run]
"""
import math
import os
import struct
import sys
import wave

# LOUDNESS-normalise, not peak-normalise. Peak alone leaves every clip at a
# different perceived level — a growl with one sharp transient and a quiet body
# peaks the same as a sustained one and sounds far quieter. Measured on the
# first batch: peak-normalising to -1 dBFS still left them at -13.7 to -15.7 dB
# RMS, roughly 7 dB under the heartbeat, i.e. buried. This is the same lesson
# the heartbeat itself went through (RMS 0.125 -> 0.402 via a soft limiter).
TARGET_RMS = 0.25         # about -12 dBFS; the mix config trims from here
CEILING = 0.95            # soft-limit above this so louder clips never clip
SILENCE_SHARE = 0.02      # below this share of peak counts as silence
PRE_ROLL_MS = 15.0        # keep the attack transient
TAIL_MS = 120.0           # let the decay breathe instead of chopping it


def read_mono(path):
    with wave.open(path, "rb") as w:
        channels, width, rate, frames = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        if width != 2:
            raise SystemExit(f"{path}: expected 16-bit, got {width * 8}-bit")
        raw = w.readframes(frames)
    total = len(raw) // 2
    samples = struct.unpack("<%dh" % total, raw)
    if channels > 1:
        samples = [sum(samples[i:i + channels]) // channels
                   for i in range(0, total - channels + 1, channels)]
    return list(samples), rate


def trim(samples, rate):
    peak = max((abs(s) for s in samples), default=0)
    if peak == 0:
        return samples, 0, len(samples)
    floor = peak * SILENCE_SHARE
    first, last = 0, len(samples) - 1
    while first < len(samples) and abs(samples[first]) < floor:
        first += 1
    while last > first and abs(samples[last]) < floor:
        last -= 1
    first = max(0, first - int(rate * PRE_ROLL_MS / 1000.0))
    last = min(len(samples) - 1, last + int(rate * TAIL_MS / 1000.0))
    return samples[first:last + 1], first, last


def dbfs(x):
    return 20 * math.log10(x) if x > 0 else -99.0


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    src_dir, out_dir = sys.argv[1], sys.argv[2]
    dry = "--dry-run" in sys.argv
    names = sorted(n for n in os.listdir(src_dir) if n.lower().endswith(".wav"))
    if not names:
        raise SystemExit(f"no .wav files in {src_dir}")
    if not dry:
        os.makedirs(out_dir, exist_ok=True)

    print(f"{'clip':<26}{'was':>18}   {'now':>18}")
    for name in names:
        samples, rate = read_mono(os.path.join(src_dir, name))
        before_len = len(samples) / rate
        before_peak = max((abs(s) for s in samples), default=1) / 32768.0
        before_rms = math.sqrt(sum(s * s for s in samples) / max(1, len(samples))) / 32768.0

        cut, _, _ = trim(samples, rate)
        if not cut:
            print(f"{name:<26}  EMPTY - skipped")
            continue
        rms = math.sqrt(sum(s * s for s in cut) / len(cut)) / 32768.0
        gain = TARGET_RMS / rms if rms > 0 else 1.0
        # Soft knee above the ceiling: tanh bends the loudest peaks over instead
        # of squaring them off, so raising the body of the clip cannot introduce
        # the clipping distortion that hard clamping would.
        out = []
        for s in cut:
            x = (s / 32768.0) * gain
            if abs(x) > CEILING:
                x = math.copysign(CEILING + (1 - CEILING) * math.tanh((abs(x) - CEILING) / (1 - CEILING)), x)
            out.append(max(-32768, min(32767, int(x * 32767))))
        after_len = len(out) / rate
        after_rms = math.sqrt(sum(s * s for s in out) / len(out)) / 32768.0

        print(f"{name:<26}{before_len:>6.2f}s {dbfs(before_rms):>6.1f}dB   "
              f"{after_len:>6.2f}s {dbfs(after_rms):>6.1f}dB")
        if dry:
            continue
        with wave.open(os.path.join(out_dir, name), "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(rate)
            w.writeframes(b"".join(struct.pack("<h", s) for s in out))
    print(f"{'(dry run) ' if dry else ''}done -> {out_dir}")


if __name__ == "__main__":
    main()
