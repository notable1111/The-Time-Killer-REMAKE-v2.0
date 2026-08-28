"""Recolour the clock bursts to the clock's OWN light colour. Shape untouched.

WHY THIS AND NOT A REDRAW. The clock's Light2D is green — rgba(0.45, 1.0, 0.4) —
and its two sprite bursts were gold, peaking at (255, 242, 78). One object giving
feedback in two unrelated colours is a loud cheap tell: it reads as an effect
borrowed from elsewhere and dropped on top, which is what it was.

I tried redrawing both from scratch and BOTH ATTEMPTS CAME OUT WORSE than the
originals — the sparks read as a smear, the ring as an amoeba. The original art
is well made; its shapes were never the problem. So this keeps every pixel of
structure and changes only the hue, which is the part that was actually wrong.

Method: map each pixel's LUMINANCE onto a green ramp and keep its alpha exactly.
That preserves the artist's shading, edges and timing — a hue rotation would drag
the near-white cores toward green and lose the heat at the centre, which is the
thing that makes a spark read as a spark.

Usage: python recolour_clock_vfx.py [--dry-run] [--restore]
"""
import shutil
import sys
from pathlib import Path

from PIL import Image

SRC = Path("Assets/Resources/Assets/Effects/Vfx")
BACKUP = Path("Tools/VfxPipeline/originals")

# Dark -> pale, built around the clock's own green. The top stop stays near-white:
# a hot core is white whatever colour it burns, and dropping that is what made the
# from-scratch attempts look like glowing slime.
GREEN = [(6, 18, 8), (18, 62, 24), (52, 132, 58), (140, 214, 130), (232, 255, 228)]


def ramp(t):
    t = max(0.0, min(1.0, t))
    span = t * (len(GREEN) - 1)
    i = int(span)
    if i >= len(GREEN) - 1:
        return GREEN[-1]
    f = span - i
    a, b = GREEN[i], GREEN[i + 1]
    return tuple(int(a[c] + (b[c] - a[c]) * f) for c in range(3))


def recolour(path, dry):
    im = Image.open(path).convert("RGBA")
    px = im.load()
    w, h = im.size
    changed = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0
            nr, ng, nb = ramp(lum)
            px[x, y] = (nr, ng, nb, a)
            changed += 1
    print("  %-16s %dx%d  %d pixels recoloured" % (path.name, w, h, changed))
    if not dry:
        im.save(path, optimize=True)


def main():
    dry = "--dry-run" in sys.argv
    names = ["clock_hit.png", "clock_wake.png"]
    BACKUP.mkdir(parents=True, exist_ok=True)

    if "--restore" in sys.argv:
        for n in names:
            src = BACKUP / n
            if src.exists():
                shutil.copy2(src, SRC / n)
                print("restored", n)
        return

    for n in names:
        p = SRC / n
        keep = BACKUP / n
        if not keep.exists():
            shutil.copy2(p, keep)          # keep the gold original, once
            print("  backed up original ->", keep)
        recolour(p, dry)
    print("dry run - nothing written" if dry else "recoloured; re-run Setup/38")


if __name__ == "__main__":
    main()
