"""Redraw the two clock beats: the earned press, and the clock coming alive.

WHY THEY ARE BEING REDRAWN. The user, 2026-08-28: "the effect for the clocks they
are not bad but looks cheap you should work on it."

WHAT WAS ACTUALLY WRONG, measured rather than guessed:

1. COLOUR INCOHERENCE. The clock's own light is GREEN — rgba(0.45, 1.0, 0.4) on
   the Glow child of every ClockObjective. The bursts were GOLD, peaking at
   (255, 242, 78). One object was giving feedback in two unrelated colours, which
   is one of the loudest cheap tells there is: it reads as an effect borrowed from
   somewhere else and dropped on top, because that is exactly what it was.

2. THE SHAPE WAS A SPARKLE. A symmetrical many-pointed star is the visual idiom of
   a coin pickup. Nothing in a dark castle emits a symmetrical star.

3. IT WAS THE ONLY MARKER OF A GOOD PRESS. Now ClockProgressGlow pulses the
   clock's own light on each earned press, so the sprite no longer has to carry
   that alone and can afford to be quieter.

WHAT THESE ARE INSTEAD. Both stay in the clock's green so the object reads as one
thing:

  clock_hit  — a short burst of SPARKS thrown off a mechanism, irregular and
               directional, biased upward and outward the way struck metal throws
               them. Not symmetrical, not a star, and gone in a few frames.
  clock_wake — the face lighting from the centre outward with a ring of escaping
               dust, held rather than flashed. This is the clock catching, so it
               swells and settles instead of popping.

Usage: python gen_clock_vfx.py [--dry-run]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image

CELL = 32                      # 32px / 32ppu = exactly 1 world unit, 1:1, no resample
HIT_FRAMES = 7                 # matches the existing clip length
WAKE_FRAMES = 7
OUT_DIR = Path("Assets/Resources/Assets/Effects/Vfx")

# The clock's OWN green, sampled from its Light2D, running dark -> pale.
# The pale end is deliberately near-white rather than saturated green: a spark's
# core is always hotter and whiter than its tail, whatever colour it burns.
CLOCK_RAMP = [(10, 30, 14), (24, 74, 32), (58, 140, 62), (128, 205, 122), (226, 255, 224)]


def ramp(colours, t):
    t = max(0.0, min(1.0, t))
    span = t * (len(colours) - 1)
    i = int(span)
    if i >= len(colours) - 1:
        return colours[-1]
    f = span - i
    a, b = colours[i], colours[i + 1]
    return tuple(int(a[c] + (b[c] - a[c]) * f) for c in range(3))


def put(px, x, y, intensity):
    if intensity <= 0.02:
        return
    xi, yi = int(round(x)), int(round(y))
    if not (0 <= xi < CELL and 0 <= yi < CELL):
        return
    a = px[xi, yi][3]
    alpha = int(255 * min(1.0, intensity))
    if alpha <= a:
        return
    r, g, b = ramp(CLOCK_RAMP, intensity)
    px[xi, yi] = (r, g, b, alpha)


def hit_frame(index, sparks):
    """Sparks thrown off the mechanism. Irregular, directional, brief."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    px = img.load()
    t = index / float(HIT_FRAMES - 1)
    cx = cy = (CELL - 1) / 2.0
    # Hard start, quick death — a strike, not a swell.
    energy = (1.0 - t) ** 1.5

    for ang, speed, life, size in sparks:
        travel = speed * t * CELL * 0.46
        if t > life:
            continue
        fade = (1.0 - t / life) ** 0.9 * energy * 1.6
        # Sparks arc: they fall slightly as they fly.
        x = cx + math.cos(ang) * travel
        y = cy + math.sin(ang) * travel + (travel * travel) * 0.014
        # A LONG streak behind each. The first pass used 3px tails and measured
        # 2.4% lit - a faint dot. A spark is mostly its trail.
        steps = 10 + size * 4
        for s in range(steps):
            f = s / float(steps)
            bx = x - math.cos(ang) * f * 7.5
            by = y - math.sin(ang) * f * 7.5
            w = 1.0 - f
            put(px, bx, by, fade * w ** 0.65)
            if size > 0 and f < 0.4:
                put(px, bx, by - 1, fade * w ** 0.65 * 0.55)

    # The struck point itself, only for the first couple of frames.
    if index < 2:
        core = (1.0 - index * 0.5)
        for yy in range(CELL):
            for xx in range(CELL):
                d = math.hypot(xx - cx, yy - cy)
                put(px, xx, yy, math.exp(-(d / 2.4) ** 2) * core)
    return img


def wake_frame(index, motes):
    """The clock catching: light from the centre, dust escaping outward."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    px = img.load()
    t = index / float(WAKE_FRAMES - 1)
    cx = cy = (CELL - 1) / 2.0
    # Swell and settle. This is a mechanism starting, not a spark.
    env = math.sin(math.pi * (0.12 + 0.76 * t)) ** 0.7

    # A RING that expands, not a blob. The gold original's shape was the good
    # part of it - a mechanism throwing a shock outward - and the first redraw
    # threw that away for a gaussian, which read as a smudge. What was wrong with
    # the original was its COLOUR (gold against the clock's green light), so the
    # ring stays and the palette changes.
    radius = CELL * (0.10 + 0.30 * t)
    width = 1.5 + 2.6 * t
    for yy in range(CELL):
        dy = yy - cy
        for xx in range(CELL):
            dx = xx - cx
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)
            # Irregular edge so it is a shock through air, not a drawn circle.
            wob = 1.0 + 0.16 * math.sin(ang * 3.0 + 1.1) + 0.10 * math.sin(ang * 7.0 + 2.7)
            band = math.exp(-((d - radius * wob) / width) ** 2) ** 0.6
            put(px, xx, yy, band * env * 1.5)
            # A hot core that fades as the ring leaves it behind.
            core = math.exp(-(d / (CELL * 0.11)) ** 2)
            put(px, xx, yy, core * env * (1.0 - t) ** 0.8 * 1.3)

    # Dust shaken loose, drifting out and up.
    for ang, speed, phase in motes:
        r = speed * t * CELL * 0.46
        x = cx + math.cos(ang) * r
        y = cy + math.sin(ang) * r - t * 2.2
        put(px, x, y, env * (1.0 - t) ** 0.6 * 0.95)
        put(px, x + 1, y, env * (1.0 - t) ** 0.6 * 0.45)
    return img


def strip(frames):
    sheet = Image.new("RGBA", (CELL * len(frames), CELL), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * CELL, 0))
    return sheet


def main():
    dry = "--dry-run" in sys.argv
    rng = random.Random(7734)
    # Biased upward and outward: struck metal throws sparks away from the blow.
    sparks = []
    for _ in range(14):
        ang = rng.uniform(-math.pi * 0.95, -math.pi * 0.05) if rng.random() < 0.75 \
              else rng.uniform(0, math.pi)
        sparks.append((ang, rng.uniform(0.55, 1.15), rng.uniform(0.55, 1.0), rng.choice((0, 1, 1))))
    motes = [(rng.uniform(0, math.pi * 2), rng.uniform(0.5, 1.0), rng.random()) for _ in range(10)]

    out = {
        "clock_hit": strip([hit_frame(i, sparks) for i in range(HIT_FRAMES)]),
        "clock_wake": strip([wake_frame(i, motes) for i in range(WAKE_FRAMES)]),
    }
    for name, img in out.items():
        data = list(img.getdata())
        op = 100.0 * sum(1 for p in data if p[3] > 200) / len(data)
        lit = 100.0 * sum(1 for p in data if p[3] > 8) / len(data)
        print("%-11s %dx%d  opaque %.1f%%  lit %.1f%%" % (name, img.size[0], img.size[1], op, lit))
        if not dry:
            OUT_DIR.mkdir(parents=True, exist_ok=True)
            img.save(OUT_DIR / (name + ".png"), optimize=True)
    print("dry run - nothing written" if dry else "written to %s (re-run Setup/38)" % OUT_DIR)


if __name__ == "__main__":
    main()
