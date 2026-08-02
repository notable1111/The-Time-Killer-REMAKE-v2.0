"""Prove — or disprove — that a system is AUDIBLE in a recorded session.

The recorder writes three aligned streams per session: state.jsonl at 4Hz,
frames/*.jpg, and audio.wav of the FINAL MIX. That last one is the only
ground truth for "can the player hear it", and it makes a whole class of
argument decidable that was previously guesswork.

The method: state.jsonl reports a cumulative `beats` counter, so the instants
the heart fired are known exactly. Measure the audio in a short window AT those
instants and compare against windows BETWEEN them. If the heartbeat is audible,
beat windows must be measurably louder in the band it occupies. If the two are
equal, the heart is inaudible in the mix no matter what the game thinks it is
doing.

This exists because three earlier attempts to answer that question failed:
reading Unity's output buffer caught a recompile that had silently dropped play
mode, an eight-pass sample loop turned out to read the same buffer eight times
inside one frame, and a Goertzel probe comb mismeasured band energy badly enough
to justify a rescue that was not needed. A recording cannot lie about what came
out of the speakers.

Usage: python analyse_session.py [Recordings/<session>]
       (defaults to the newest session)
"""
import array
import json
import math
import sys
import wave
from pathlib import Path

ROOT = Path(r"D:\The Time Killer Remake")
BLOCK = 0.05          # analysis block, seconds
BEAT_WINDOW = 0.30    # a beat's audible tail
STRIDE = 4            # sample decimation for RMS; plenty for a level comparison


def newest_session():
    sessions = sorted((ROOT / "Recordings").glob("*"), key=lambda p: p.stat().st_mtime)
    return sessions[-1] if sessions else None


def load_state(path):
    rows = []
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
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


def beat_times(rows):
    """Instants where the cumulative beat counter advanced."""
    times, previous = [], None
    for row in rows:
        beats, t = row.get("beats"), row.get("t")
        if beats is None or t is None:
            continue
        if previous is not None and beats > previous:
            # The counter is sampled at 4Hz, so several beats can land in one
            # sample at high rates. Spread them across the interval rather than
            # stacking them on the sample instant.
            for k in range(int(beats - previous)):
                times.append(t + k * 0.25 / max(1, beats - previous))
        previous = beats
    return times


def load_audio(path):
    with wave.open(str(path), "rb") as w:
        rate, channels = w.getframerate(), w.getnchannels()
        raw = array.array("h")
        raw.frombytes(w.readframes(w.getnframes()))
    return raw, rate, channels


def block_rms(raw, rate, channels, start_s, length_s):
    begin = int(start_s * rate) * channels
    end = min(len(raw), begin + int(length_s * rate) * channels)
    if end <= begin:
        return 0.0
    total, count = 0.0, 0
    for i in range(begin, end, channels * STRIDE):
        v = raw[i] / 32768.0
        total += v * v
        count += 1
    return math.sqrt(total / max(1, count))


def db(v):
    return 20 * math.log10(max(1e-9, v))


def main():
    session = Path(sys.argv[1]) if len(sys.argv) > 1 else newest_session()
    if session is None or not session.exists():
        raise SystemExit("no session found")
    print(f"session: {session.name}")

    rows = load_state(session / "state.jsonl")
    raw, rate, channels = load_audio(session / "audio.wav")
    duration = len(raw) / channels / rate
    print(f"  {duration:.1f}s audio, {len(rows)} state samples, {rate}Hz {channels}ch")

    beats = beat_times(rows)
    print(f"  {len(beats)} heartbeats reported by the game")
    if not beats:
        print("  -> nothing to align against.")
        return

    # Windows AT beats, and control windows placed midway between consecutive
    # beats. Same length, same file, so the only difference is the heartbeat.
    on, off = [], []
    for i, t in enumerate(beats):
        if t + BEAT_WINDOW > duration:
            continue
        on.append(block_rms(raw, rate, channels, t, BEAT_WINDOW))
        if i + 1 < len(beats):
            gap_mid = (t + beats[i + 1]) / 2
            if gap_mid + BEAT_WINDOW <= duration and beats[i + 1] - t > BEAT_WINDOW * 2:
                off.append(block_rms(raw, rate, channels, gap_mid, BEAT_WINDOW))

    overall = block_rms(raw, rate, channels, 0, duration)
    mean_on = sum(on) / max(1, len(on))
    mean_off = sum(off) / max(1, len(off))

    print(f"\n  whole mix        RMS {db(overall):7.1f} dBFS")
    print(f"  at  beats (n={len(on):3d})  RMS {db(mean_on):7.1f} dBFS")
    print(f"  between   (n={len(off):3d})  RMS {db(mean_off):7.1f} dBFS")
    lift = db(mean_on) - db(mean_off)
    print(f"  -> beats are {lift:+.1f} dB vs the gaps")

    # ~1dB is around the threshold where a level change becomes noticeable in a
    # busy mix; 3dB is a clear doubling of power. Below ~0.5dB the heart is not
    # meaningfully present no matter what the beat counter says.
    verdict = ("INAUDIBLE — the mix is the same with and without the beat" if lift < 0.5
               else "faint — present but easily masked" if lift < 1.5
               else "clearly audible" if lift < 6
               else "dominant")
    print(f"  VERDICT: {verdict}")

    fears = [r["fear"] for r in rows if isinstance(r.get("fear"), (int, float))]
    bpms = [r["bpm"] for r in rows if isinstance(r.get("bpm"), (int, float))]
    if fears and bpms:
        print(f"\n  fear {min(fears):.2f}-{max(fears):.2f} (mean {sum(fears)/len(fears):.2f})"
              f"   bpm {min(bpms):.0f}-{max(bpms):.0f} (mean {sum(bpms)/len(bpms):.0f})")


if __name__ == "__main__":
    main()
