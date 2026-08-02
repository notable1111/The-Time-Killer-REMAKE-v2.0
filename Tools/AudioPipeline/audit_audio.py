#!/usr/bin/env python3
"""Audit every audio asset in the project, and guard what has been approved.

    python Tools/AudioPipeline/audit_audio.py                  # audit + compare to baseline
    python Tools/AudioPipeline/audit_audio.py --save-baseline  # bless the current state

WHY. Claude has no ears, and no tool in this project can listen. But most of what
goes wrong with game audio is not a matter of taste -- it is clipping, a clip that
is 90% silence, a voice set where one line is 12 dB hotter than its neighbours, or
two layers occupying the same frequency band and turning to mud. All of that is
measurable, and measuring it costs no listening time at all.

THE BASELINE IS THE POINT. Taste questions ("is this MUTTER or a SCREAM?", "is the
drone eerie or just a hum?") can only ever be answered by a human ear. So the deal
is: the human listens ONCE and rules, that ruling is snapshotted here, and every
later change is checked against it automatically. The ear sets the line; the
measurement stops it drifting. This is the project's own rule -- if you would
eyeball the same question twice, build the measurement instead.

Uses ffmpeg only (astats / volumedetect). numpy is not installed on this machine
and a tool that needs an install is a tool that stops being run.
"""
import json
import os
import re
import subprocess
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCAN = [
    "Assets/Resources/Assets",
    "Assets/Resources/Outsource/Audio",
]
BASELINE = os.path.join(ROOT, "Tools", "AudioPipeline", "audio_baseline.json")

# --- thresholds, each a claim about the audio rather than a magic number ------
CLIP_PEAK_DB = -0.5     # at or above this a clip is riding full scale
QUIET_RMS_DB = -50.0    # effectively silent -- usually a broken export
MIN_SECONDS = 0.05
GROUP_SPREAD_DB = 10.0  # loudness spread inside one folder before it is a defect
DRIFT_DB = 1.5          # change from baseline worth reporting


def run(args):
    return subprocess.run(args, capture_output=True, text=True, errors="ignore").stderr


def measure(path):
    """Peak, RMS, duration and low-band share for one file, via ffmpeg only."""
    out = run(["ffmpeg", "-hide_banner", "-i", path,
               "-af", "astats=metadata=1:reset=0", "-f", "null", "-"])
    peak = rms = None
    for line in out.splitlines():
        if "Peak level dB" in line and peak is None:
            peak = float(line.split(":")[-1].strip())
        elif "RMS level dB" in line and rms is None:
            rms = float(line.split(":")[-1].strip())
    dur = None
    m = re.search(r"Duration: (\d+):(\d+):([\d.]+)", out)
    if m:
        dur = int(m.group(1)) * 3600 + int(m.group(2)) * 60 + float(m.group(3))

    # Low-band share: how much survives a 200 Hz lowpass. Near 0 dB of loss means
    # the content IS sub-bass (a drone); a big loss means it is air and detail.
    low = run(["ffmpeg", "-hide_banner", "-i", path,
               "-af", "lowpass=f=200,volumedetect", "-f", "null", "-"])
    full = run(["ffmpeg", "-hide_banner", "-i", path,
                "-af", "volumedetect", "-f", "null", "-"])

    def mean_of(text):
        m2 = re.search(r"mean_volume: (-?[\d.]+) dB", text)
        return float(m2.group(1)) if m2 else None

    lm, fm = mean_of(low), mean_of(full)
    return {
        "peak": peak, "rms": rms, "seconds": dur,
        "lowShare": round(lm - fm, 2) if (lm is not None and fm is not None) else None,
    }


def collect():
    found = {}
    for rel in SCAN:
        base = os.path.join(ROOT, rel)
        for dirpath, _, names in os.walk(base):
            for n in names:
                if not n.lower().endswith((".wav", ".ogg", ".mp3")):
                    continue
                full = os.path.join(dirpath, n)
                key = os.path.relpath(full, ROOT).replace("\\", "/")
                found[key] = measure(full)
    return found


def defects(clips):
    out = []
    for key, m in sorted(clips.items()):
        if m["seconds"] is None:
            out.append((90, key, "unreadable — ffmpeg could not decode it"))
            continue
        if m["seconds"] < MIN_SECONDS:
            out.append((85, key, f"only {m['seconds']:.3f}s long — effectively empty"))
        if m["peak"] is not None and m["peak"] >= CLIP_PEAK_DB:
            out.append((95, key, f"peak {m['peak']:+.1f} dB — riding full scale, will clip when layered"))
        if m["rms"] is not None and m["rms"] < QUIET_RMS_DB:
            out.append((70, key, f"RMS {m['rms']:.1f} dB — effectively silent"))

    # Loudness consistency WITHIN a folder. A voice set where one line is 12 dB
    # hotter is the single most common audio defect in a game, and it is invisible
    # until that one line makes a player jump for the wrong reason.
    groups = defaultdict(list)
    for key, m in clips.items():
        if m["rms"] is not None and m["rms"] > QUIET_RMS_DB:
            groups[os.path.dirname(key)].append((key, m["rms"]))
    for folder, items in sorted(groups.items()):
        if len(items) < 3:
            continue
        loud = max(items, key=lambda x: x[1])
        quiet = min(items, key=lambda x: x[1])
        spread = loud[1] - quiet[1]
        if spread > GROUP_SPREAD_DB:
            out.append((80, folder,
                        f"{spread:.1f} dB spread across {len(items)} clips — "
                        f"loudest {os.path.basename(loud[0])} ({loud[1]:.1f}), "
                        f"quietest {os.path.basename(quiet[0])} ({quiet[1]:.1f})"))
    out.sort(key=lambda x: -x[0])
    return out


def compare(clips, base):
    out = []
    for key, m in sorted(clips.items()):
        if key not in base:
            out.append((40, key, "NEW since the baseline — never checked by ear"))
            continue
        b = base[key]
        for field in ("peak", "rms"):
            if m[field] is None or b.get(field) is None:
                continue
            d = m[field] - b[field]
            if abs(d) >= DRIFT_DB:
                out.append((75, key, f"{field} moved {d:+.1f} dB since the baseline "
                                     f"({b[field]:.1f} -> {m[field]:.1f})"))
    for key in sorted(base):
        if key not in clips:
            out.append((60, key, "GONE since the baseline — was it meant to be deleted?"))
    out.sort(key=lambda x: -x[0])
    return out


def main():
    save = "--save-baseline" in sys.argv
    print("scanning...", file=sys.stderr)
    clips = collect()
    print(f"{len(clips)} audio assets measured\n")

    if save:
        with open(BASELINE, "w", encoding="utf-8") as fh:
            json.dump(clips, fh, indent=1, sort_keys=True)
        print(f"baseline written: {BASELINE}")
        print("This is now the approved state. Re-run without --save-baseline to check drift.")
        return 0

    issues = defects(clips)
    print(f"=== DEFECTS ({len(issues)}) ===")
    for sev, key, what in issues:
        print(f"{sev:>3}  {key}\n     {what}")
    if not issues:
        print("none")

    if os.path.exists(BASELINE):
        with open(BASELINE, encoding="utf-8") as fh:
            base = json.load(fh)
        drift = compare(clips, base)
        print(f"\n=== DRIFT FROM BASELINE ({len(drift)}) ===")
        for sev, key, what in drift:
            print(f"{sev:>3}  {key}\n     {what}")
        if not drift:
            print("none — the mix is exactly as approved")
    else:
        print(f"\nNo baseline yet. Once the audio is approved by ear, run:\n"
              f"  python {os.path.relpath(__file__, ROOT)} --save-baseline")
    return 0


if __name__ == "__main__":
    sys.exit(main())
