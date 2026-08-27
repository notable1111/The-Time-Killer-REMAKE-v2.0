"""Key a generated light-burst strip so its dark pixels stop being dark pixels.

WHY THIS EXISTS. PixelLab draws an effect the way it draws an object: on an
opaque canvas. The clock "wake" ring came back with a bright golden rim around
an interior of (56, 25, 22) at FULL alpha -- a dark brown disc. Alpha-blended
over the clock face that is not a halo, it is a mud-coloured plate covering the
thing the player just fixed. Measured before writing this: 40% of the ring's
pixels were opaque, and the centre pixel was one of them.

THE RULE: for a LIGHT effect, alpha follows luminance. A glow has no dark
parts. Anything dark inside a burst is the generator's background showing
through, not art, so it should not be drawn at all. That is one rule rather
than a special case for "the middle of a ring", and it also removes the dark
halo these generations leave around the rim.

Deliberately NOT a global luminance threshold with a hard cut -- that produces
a jagged keyed edge on pixel art. The ramp below keeps partial alpha across a
narrow band so the rim stays smooth at the resolution it was drawn.

This does not touch hue or saturation. The bursts are meant to stay BRIGHT: the
castle floor measures luminance 10/255, and the one time this project tinted an
effect down to "match the palette" it measured 0.26% screen coverage and was
invisible in play (see draw_droplets.py and ARCHITECTURE Principles).

Usage:
    python clean_burst.py <strip.png> [more.png ...]        # rewrites in place
    python clean_burst.py --fade <strip.png>                # + fade the tail out
    python clean_burst.py --dry-run <strip.png>             # report only

--fade is for a strip that never dies on its own. EffectPlayer destroys a
one-shot sheet exactly when its last frame has played, so a burst still at half
strength on that frame does not end, it VANISHES. Run it on any strip whose
last cell still carries real ink.
"""
import sys
from pathlib import Path

from PIL import Image

# Luminance band, 0-255. Below LO a pixel is background and is erased outright;
# above HI it is light and is kept at full strength; between, alpha ramps.
# The interior that motivated this measured luma 33, and the ring's own amber
# rim measured 107 -- LO sits at 40 rather than hard against 34 because a
# one-unit margin is not a margin.
LO = 40.0
HI = 110.0

# Fade envelope for --fade, in cell fractions: alpha is untouched until HOLD,
# then ramps to zero at the final cell. Same shape the BloodBurst particle uses
# (hold, then drop late) and for the same reason -- an effect that fades from
# frame 0 looks weak, and one that never fades POPS when the object is
# destroyed. Measured on the clock wake ring: without this the last frame still
# carried 959 ink pixels at mean alpha 59.7, 58% of its opening strength.
FADE_HOLD = 0.45


def luma(r, g, b):
    return 0.299 * r + 0.587 * g + 0.114 * b


def fade_scale(x, width, height, fade):
    """Envelope for the cell this pixel sits in. 1.0 everywhere without --fade.

    The strip is one row of square cells (the contract VfxSheetSetup slices on),
    so the cell index is simply x // height.
    """
    if not fade or height <= 0:
        return 1.0
    cells = max(1, width // height)
    if cells < 2:
        return 1.0
    position = (x // height) / float(cells - 1)   # 0 at the first cell, 1 at the last
    if position <= FADE_HOLD:
        return 1.0
    return 1.0 - (position - FADE_HOLD) / (1.0 - FADE_HOLD)


def clean(path, dry_run=False, fade=False):
    image = Image.open(path).convert("RGBA")
    pixels = image.load()
    width, height = image.size

    erased = kept = ramped = 0
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            level = luma(r, g, b)
            if level <= LO:
                scale = 0.0
                erased += 1
            elif level >= HI:
                scale = 1.0
                kept += 1
            else:
                scale = (level - LO) / (HI - LO)
                ramped += 1
            pixels[x, y] = (r, g, b, int(round(a * scale * fade_scale(x, width, height, fade))))

    total = erased + kept + ramped
    print("%-18s %dx%d  erased=%d (%.0f%%)  ramped=%d  kept=%d"
          % (Path(path).name, width, height, erased,
             100.0 * erased / total if total else 0.0, ramped, kept))

    if not dry_run:
        image.save(path)
    return image


def main(argv):
    dry_run = "--dry-run" in argv
    fade = "--fade" in argv
    targets = [a for a in argv if not a.startswith("--")]
    if not targets:
        raise SystemExit(__doc__)
    for target in targets:
        if not Path(target).exists():
            raise SystemExit("no such file: %s" % target)
        clean(target, dry_run, fade)
    if dry_run:
        print("(dry run - nothing written)")


if __name__ == "__main__":
    main(sys.argv[1:])
