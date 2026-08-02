#!/usr/bin/env python3
"""Give every project audio asset -3 dB of peak headroom.

    python Tools/AudioPipeline/add_headroom.py --dry-run
    python Tools/AudioPipeline/add_headroom.py --apply

WHY. The project's earlier normalisation pass targeted peak 0 dBFS, which is the
wrong target for audio that LAYERS. Two full-scale clips played together clip by
arithmetic, and this game deliberately stacks heartbeat + sting + hit + drone at
exactly the moments that matter most. Measured on a real recorded session: the
master output reached 0.000 dB with a flat factor of 18.2 (the waveform flattening
at the ceiling -- the signature of clipping) across 57 samples.

Ruling 2026-08-02: re-normalise to -3 dB peak rather than trimming one config
value, because that also fixes the 12.3 dB loudness spread inside Heartbeat/ that
the Soften() call was hand-written to work around.

SCOPE IS DELIBERATELY NARROW: only the project's OWN assets. Third-party packs
under Outsource/ are left alone -- they are mostly unused, and rewriting a
vendor's files makes the next pack update a merge conflict.

Reversible: everything here is git-tracked, so `git checkout` undoes it.
"""
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET_PEAK_DB = -3.0
ONLY_IF_ABOVE_DB = -1.0        # leave anything already quiet enough alone
DIRS = [
    "Assets/Resources/Assets/Voices",
    "Assets/Resources/Assets/AudioNormalized",
    "Assets/Resources/Assets/Heartbeat",
]


def peak_db(path):
    out = subprocess.run(
        ["ffmpeg", "-hide_banner", "-i", path, "-af", "volumedetect", "-f", "null", "-"],
        capture_output=True, text=True, errors="ignore").stderr
    m = re.search(r"max_volume: (-?[\d.]+) dB", out)
    return float(m.group(1)) if m else None


def main():
    apply = "--apply" in sys.argv
    if not apply and "--dry-run" not in sys.argv:
        print(__doc__)
        return 1

    changed = skipped = failed = 0
    print(f"{'file':<42} {'peak':>7} {'gain':>7} {'new':>7}")
    for rel in DIRS:
        d = os.path.join(ROOT, rel)
        if not os.path.isdir(d):
            continue
        for name in sorted(os.listdir(d)):
            if not name.lower().endswith(".wav"):
                continue
            path = os.path.join(d, name)
            p = peak_db(path)
            if p is None:
                print(f"{name:<42} {'?':>7}  unreadable")
                failed += 1
                continue
            if p < ONLY_IF_ABOVE_DB:
                skipped += 1
                continue

            gain = TARGET_PEAK_DB - p
            if not apply:
                print(f"{name:<42} {p:>7.1f} {gain:>+7.1f} {TARGET_PEAK_DB:>7.1f}  (dry run)")
                changed += 1
                continue

            tmp = path + ".tmp.wav"
            # Straight gain, no limiting and no normalisation filter: the goal is
            # HEADROOM, not a different sound. Every sample scales by the same
            # factor, so relative dynamics inside the clip are untouched.
            r = subprocess.run(
                ["ffmpeg", "-hide_banner", "-y", "-i", path,
                 "-af", f"volume={gain:.2f}dB", "-c:a", "pcm_s16le", tmp],
                capture_output=True, text=True, errors="ignore")
            if r.returncode != 0 or not os.path.exists(tmp):
                print(f"{name:<42} FAILED: {r.stderr.strip().splitlines()[-1:]}")
                failed += 1
                continue
            shutil.move(tmp, path)
            after = peak_db(path)
            print(f"{name:<42} {p:>7.1f} {gain:>+7.1f} {after:>7.1f}")
            changed += 1

    print(f"\n{'would change' if not apply else 'changed'}: {changed}   "
          f"already quiet enough: {skipped}   failed: {failed}")
    if apply:
        print("Unity will re-import these on focus. Re-run audit_audio.py to confirm.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
