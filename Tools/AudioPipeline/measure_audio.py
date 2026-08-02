"""Measure what is actually in an audio file, correctly.

WHY THIS EXISTS. On 2026-07-28 the heartbeat was declared "99% below 150Hz,
inaudible on laptop speakers" and a rescue was built for it. Both the claim and
the rescue were wrong, and the cause was the measurement:

    a Goertzel probe comb is NOT band energy.

Sampling power at 14 discrete frequencies and calling the sum "energy in the
band" undercounts everything between the probes and misses broadband transient
content entirely. A percussive sound like a heartbeat is mostly transient, so the
error was large and confident:

    band        probe comb      true (filters)
    <150Hz         98.4%            79.0%
    150-460Hz       1.5%             5.0%   (-20.9 dBFS: quiet, but audible)

The rescue built on the false number could only gain +0.9dB across 24 parameter
combinations while giving up 24dB of sub, and was scrapped.

THE RIGHT WAY, used here: split the signal with cascaded biquads and take the RMS
of each band. That integrates all the energy in the band, transients included.

Note for anyone re-checking older audio claims in this repo: the "97.2% of energy
below 100Hz" figure recorded against an earlier heartbeat may have come from the
same flawed method. Re-measure before trusting it.

Usage:
    python measure_audio.py <file.wav> [more.wav ...]
    python measure_audio.py <file.wav> --speaker    # also simulate a small speaker
"""
import array
import math
import sys
import wave
from pathlib import Path

# Bands chosen for game-audio decisions rather than for mastering:
#   <150    what only headphones, big speakers and subwoofers reproduce
#   150-460 the body a laptop/TV/phone speaker actually delivers
#   460-2k  presence and intelligibility; where a sound cuts through a mix
#   >2k     air, sibilance, transient snap
BANDS = [(0, 150), (150, 460), (460, 2000), (2000, 20000)]


def read_wav(path):
    with wave.open(str(path), "rb") as w:
        frames, rate, channels = w.getnframes(), w.getframerate(), w.getnchannels()
        raw = array.array("h")
        raw.frombytes(w.readframes(frames))
    samples = list(raw)
    if channels > 1:
        samples = [sum(samples[i:i + channels]) / channels
                   for i in range(0, len(samples), channels)]
    return [s / 32768.0 for s in samples], rate, channels


def biquad(samples, rate, kind, freq, q=0.707):
    w0 = 2 * math.pi * freq / rate
    alpha = math.sin(w0) / (2 * q)
    cos0 = math.cos(w0)
    if kind == "lowpass":
        b0, b1, b2 = (1 - cos0) / 2, 1 - cos0, (1 - cos0) / 2
    else:
        b0, b1, b2 = (1 + cos0) / 2, -(1 + cos0), (1 + cos0) / 2
    a0, a1, a2 = 1 + alpha, -2 * cos0, 1 - alpha
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0

    out, x1, x2, y1, y2 = [], 0.0, 0.0, 0.0, 0.0
    for x in samples:
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        out.append(y)
        x2, x1, y2, y1 = x1, x, y1, y
    return out


def rms(samples):
    if not samples:
        return 0.0
    return math.sqrt(sum(x * x for x in samples) / len(samples))


def db(value):
    return 20 * math.log10(max(1e-9, value))


def band(samples, rate, lo, hi):
    """Cascaded 2nd-order sections -> ~24dB/oct skirts. Real band energy."""
    out = samples
    if lo > 0:
        for _ in range(2):
            out = biquad(out, rate, "highpass", lo)
    if hi < rate / 2:
        for _ in range(2):
            out = biquad(out, rate, "lowpass", min(hi, rate / 2 - 1))
    return out


def small_speaker(samples, rate):
    """Pessimistic stand-in for a driver that cannot move air below ~200Hz."""
    out = samples
    for _ in range(2):
        out = biquad(out, rate, "highpass", 200.0)
    return out


def report(path, simulate_speaker=False):
    samples, rate, channels = read_wav(path)
    total_power = rms(samples) ** 2 or 1e-12
    peak = max((abs(s) for s in samples), default=0.0)

    print(f"\n{Path(path).name}")
    print(f"  {len(samples)/rate:.2f}s  {rate}Hz  {channels}ch   "
          f"peak {db(peak):.1f} dBFS   RMS {db(rms(samples)):.1f} dBFS")
    for lo, hi in BANDS:
        b = band(samples, rate, lo, hi)
        share = 100 * rms(b) ** 2 / total_power
        bar = "#" * int(round(share / 2.5))
        label = f"{lo}-{hi}Hz" if lo else f"<{hi}Hz"
        print(f"  {label:<12} {db(rms(b)):7.1f} dBFS  {share:5.1f}%  {bar}")

    if simulate_speaker:
        thin = small_speaker(samples, rate)
        print(f"  through a small speaker: RMS {db(rms(thin)):.1f} dBFS "
              f"({db(rms(thin)) - db(rms(samples)):+.1f} dB vs full range)")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not args:
        raise SystemExit(__doc__)
    for path in args:
        report(path, simulate_speaker="--speaker" in sys.argv)


if __name__ == "__main__":
    main()
