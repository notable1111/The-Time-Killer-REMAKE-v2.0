"""Turn a human vocal take into something that is not human (pure stdlib).

Why this exists: the first maniac set was generated with seed_audio's DEFAULT
voice, which is female, and `pitch_rate -12` only pitched her down. The result
was rejected by ear on 2026-07-28 as "normal girl's voice going haaa". Choosing a
deep male preset fixes the source, but a pitched-down man still reads as a man.
What makes a voice read as a creature is three things a TTS parameter cannot do:

  1. FORMANT-SHIFTING pitch drop. Resampling moves the pitch AND the formants
     (the resonances of the skull and throat) down together, which is heard as a
     physically bigger body. A pitch-only shift keeps the formants where they
     were and just sounds like a slowed tape.
  2. A SUB-OCTAVE layer underneath, for weight the original never had.
  3. A DETUNED DOUBLE. Two copies a few cents apart beat against each other, and
     that roughness is the single most "wrong" cue available — a throat cannot
     produce two pitches at once, so the ear refuses to hear it as one person.

Then soft saturation for grit, and loudness-match so it drops into the mix at the
same level as everything else.

Usage: python monsterize.py <src_dir> <out_dir> [--drop 0.80] [--sub 0.30] [--detune 0.40]
"""
import math
import os
import struct
import sys
import wave

TARGET_RMS = 0.25     # same -12 dBFS target as process_voice.py
CEILING = 0.95
DRIVE = 1.9           # saturation amount; >1 adds harmonics


def read_mono(path):
    with wave.open(path, "rb") as w:
        channels, width, rate, frames = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(frames)
    samples = struct.unpack("<%dh" % (len(raw) // 2), raw)
    if channels > 1:
        samples = [sum(samples[i:i + channels]) // channels
                   for i in range(0, len(samples) - channels + 1, channels)]
    return [s / 32768.0 for s in samples], rate


def resample(samples, factor):
    """factor < 1 lowers pitch and lengthens; formants move with it, which is
    exactly the point — this is a bigger throat, not a slower tape."""
    if factor <= 0:
        return list(samples)
    out_len = int(len(samples) / factor)
    out = []
    for i in range(out_len):
        pos = i * factor
        j = int(pos)
        if j + 1 >= len(samples):
            break
        frac = pos - j
        out.append(samples[j] * (1 - frac) + samples[j + 1] * frac)
    return out


def mix_into(base, layer, gain):
    for i in range(min(len(base), len(layer))):
        base[i] += layer[i] * gain
    return base


def saturate(x, drive):
    return math.tanh(x * drive) / math.tanh(drive)


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    src_dir, out_dir = sys.argv[1], sys.argv[2]

    def arg(name, default):
        return float(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default

    drop = arg("--drop", 0.80)       # main pitch factor
    sub_gain = arg("--sub", 0.30)    # sub-octave level
    det_gain = arg("--detune", 0.40) # detuned double level

    names = sorted(n for n in os.listdir(src_dir) if n.lower().endswith(".wav"))
    if not names:
        raise SystemExit(f"no .wav in {src_dir}")
    os.makedirs(out_dir, exist_ok=True)
    print(f"drop {drop}  sub {sub_gain}  detune {det_gain}  drive {DRIVE}")
    print(f"{'clip':<26}{'in':>8}{'out':>8}   {'semitones':>10}   rms")

    for name in names:
        samples, rate = read_mono(os.path.join(src_dir, name))
        if not samples:
            continue
        main_layer = resample(samples, drop)
        body = list(main_layer)
        # An octave below the already-dropped voice.
        body = mix_into(body, resample(samples, drop * 0.5), sub_gain)
        # ~35 cents flat of the main layer: slow beating, not a chord.
        body = mix_into(body, resample(samples, drop * 0.98), det_gain)

        body = [saturate(x, DRIVE) for x in body]
        rms = math.sqrt(sum(x * x for x in body) / len(body))
        gain = TARGET_RMS / rms if rms > 0 else 1.0
        out = []
        for x in body:
            v = x * gain
            if abs(v) > CEILING:
                v = math.copysign(CEILING + (1 - CEILING) * math.tanh((abs(v) - CEILING) / (1 - CEILING)), v)
            out.append(max(-32768, min(32767, int(v * 32767))))

        final_rms = math.sqrt(sum(s * s for s in out) / len(out)) / 32768.0
        semis = 12 * math.log2(drop)
        print(f"{name:<26}{len(samples)/rate:>7.2f}s{len(out)/rate:>7.2f}s   {semis:>9.1f}   "
              f"{20*math.log10(final_rms):>5.1f}dB")

        with wave.open(os.path.join(out_dir, name), "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(rate)
            w.writeframes(b"".join(struct.pack("<h", s) for s in out))
    print(f"done -> {out_dir}")


if __name__ == "__main__":
    main()
