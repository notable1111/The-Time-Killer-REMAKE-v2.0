"""Generate the maniac's two threat effects: being SEEN, and being SWUNG AT.

WHY GENERATED RATHER THAN DRAWN. The same reasoning gen_light_shaft.py gives:
a shockwave and a blade arc are geometry and light, not pictures. Generating
them buys three things a generated-then-keyed drawing cannot:

  * exact frame counts, so the strip can be timed to the audio it lands on
  * alpha that follows luminance by construction, so there is no dark interior
    to key out afterwards (the whole reason clean_burst.py had to exist)
  * a palette taken from the game's own art rather than from a prompt

(PixelLab, the usual route for real art, has an expired subscription as of
2026-08-25 - but even with it live these two belong here.)

WHAT THEY HAVE TO READ AGAINST. Measured from the shipped art, not guessed:
  * the castle floor is about (11, 16, 16) - almost black
  * the blood family runs (34,16,16) -> (118,50,44), dark and desaturated
  * clock_hit.png, the burst that already works, is 16.4% opaque - light effects
    here are SPARSE. A dense sheet reads as a plate laid over the scene.
Both recipes use the UNLIT material, so these are glows: bright is correct,
cartoon-saturated is not.

WHY THE TWO LOOK DIFFERENT. Red for being seen, cold steel for the swing. They
are two different warnings and the player must tell them apart in one glance and
with the sound off - which is the entire point of the feature. Colour does that
faster than shape.

SHAPE RULES
  spotted - a soft irregular bloom that is ALREADY loud on frame 0 (he has seen
            you before you see this; a swell would misreport when it happened),
            swelling slightly as it dies. No ring and no spikes: see the note in
            spotted_frame for the three shapes that were rendered and rejected.
  swing   - a crescent that sweeps through an arc, brightest mid-stroke, with a
            trailing after-image. Tapered at both ends: a blade leaves a comma,
            not a banana.

Usage: python gen_threat_vfx.py [--dry-run]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image

CELL = 64                       # 64px / 32ppu = 2 world units, ~2x a character
SPOTTED_FRAMES = 12             # 12 @ 8.28fps = 1.45s, the spotted audio
SWING_FRAMES = 8                # 8  @ 12.9fps = 0.62s, the swing audio
OUT_DIR = Path("Assets/Resources/Assets/Effects/Vfx")

# Ramps run COOL/DARK -> HOT/PALE. Index by intensity 0..1. Alpha follows the
# same intensity, so nothing dark is ever drawn opaque.
SEEN_RAMP = [(58, 10, 14), (116, 22, 28), (178, 40, 46), (222, 84, 88), (255, 208, 210)]
STEEL_RAMP = [(28, 36, 46), (64, 82, 100), (120, 146, 170), (196, 214, 230), (245, 249, 255)]


def _table(rng, n=48):
    """A ring of random values, sampled by angle with smooth interpolation.

    This replaced a sum of sin(angle * fixed frequency) harmonics. Harmonics
    give EVENLY SPACED teeth, and the first render of this effect duly came out
    looking like a cog or a loading spinner — the exact mechanical regularity
    the dread vignette had just been rebuilt to remove. Irregular noise tears
    the ring unevenly, which is what damage looks like.
    """
    return [rng.random() for _ in range(n)]


def _sample(table, ang):
    n = len(table)
    x = ((ang / (2.0 * math.pi)) % 1.0) * n
    i = int(x)
    f = x - i
    f = f * f * (3.0 - 2.0 * f)                # smoothstep: wobble, not steps
    a, b = table[i % n], table[(i + 1) % n]
    return a + (b - a) * f


def ramp(colours, t):
    """Sample a colour ramp with linear interpolation between stops."""
    t = max(0.0, min(1.0, t))
    span = t * (len(colours) - 1)
    i = int(span)
    if i >= len(colours) - 1:
        return colours[-1]
    f = span - i
    a, b = colours[i], colours[i + 1]
    return tuple(int(a[c] + (b[c] - a[c]) * f) for c in range(3))


def put(px, x, y, colours, intensity):
    """Additive-ish write: keep the brightest contribution at each pixel.

    Additive would blow out where the ring crosses its own trail; taking the max
    keeps the peak honest and the coverage sparse, which is what the existing
    burst does.
    """
    if intensity <= 0.004:
        return
    if not (0 <= x < CELL and 0 <= y < CELL):
        return
    r, g, b, a = px[x, y]
    alpha = int(255 * min(1.0, intensity))
    if alpha <= a:
        return
    nr, ng, nb = ramp(colours, intensity)
    px[x, y] = (nr, ng, nb, alpha)


def spotted_frame(index, shape, tear_tbl):
    """One frame of the 'he has seen you' ring."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    px = img.load()
    t = index / float(SPOTTED_FRAMES - 1)          # 0..1 through the effect
    cx = cy = (CELL - 1) / 2.0

    # The ring: starts tight and bright, ends wide and faint.
    radius = 3.0 + t * (CELL * 0.46 - 3.0)
    width = 2.4 + t * 5.2                          # thickens as it weakens
    # No ease-in. Frame 0 is the loudest, because by the time you see this he
    # has already seen you - a swell would be a lie about when it happened.
    energy = (1.0 - t) ** 1.15

    # THE BLOOM. A soft glow gathered on the player - no ring, no spikes.
    #
    # Two earlier shapes were rejected here and both failed the same way, so the
    # reason is worth keeping. An expanding RING with harmonic break-up read as a
    # cog; the same ring with noise break-up read as a firework. Both were
    # OUTWARD, and outward says "you exploded". Being seen is the opposite: it is
    # something locking on to you. So the light gathers instead of bursting.
    bloom_peak = 0.22                              # frame ~2 of 12 is the loudest
    if t <= bloom_peak:
        bloom = 0.62 + 0.38 * (t / bloom_peak) ** 0.5   # already loud on frame 0
    else:
        bloom = (1.0 - (t - bloom_peak) / (1.0 - bloom_peak)) ** 1.5
    spread = CELL * (0.16 + 0.14 * t)              # swells slightly as it fades

    for y in range(CELL):
        dy = y - cy
        for x in range(CELL):
            dx = x - cx
            ang = math.atan2(dy, dx)
            # Irregular radius so the glow is not a clean disc.
            d = math.hypot(dx, dy) * (1.0 + 0.18 * (_sample(shape, ang) - 0.5))
            core = math.exp(-(d / spread) ** 2)
            if core < 0.02:
                continue
            put(px, x, y, SEEN_RAMP, (core ** 0.8) * bloom * 1.25)

    # NO WISPS, NO RING, NO SPIKES - and that is the finding, not an omission.
    # Four shapes were rendered against the real floor colour and three were
    # rejected: an expanding ring with harmonic break-up read as a COG, the same
    # ring with noise break-up read as a FIREWORK, and inward streaks over this
    # bloom read as a SPARKLE. Every rejected version failed by decorating the
    # beat. What survives is light gathering on the player and going out again,
    # which is the one shape that cannot look cheap because there is nothing in
    # it to look cheap. "Barely noticed" was the brief; decoration is the
    # opposite of it.
    return img


def swing_frame(index):
    """One frame of the blade arc."""
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    px = img.load()
    t = index / float(SWING_FRAMES - 1)
    cx = cy = (CELL - 1) / 2.0

    # The stroke sweeps through ~150 degrees. The arc's centre is pushed off to
    # one side so the crescent bows the way a swung arm does.
    sweep_start = math.radians(-105)
    sweep_end = math.radians(45)
    lead = sweep_start + (sweep_end - sweep_start) * t
    # Measured 2026-08-27 in CastleWing: at radius 0.34 and half-angle 38 the
    # swing peaked at 0.41% of screen - BELOW the 0.5% at which an effect is
    # missed entirely, even though it was the brighter of the two (mean lift
    # 291 vs spotted's 167). Intense and small still reads as nothing. Widened
    # to fill more of the cell rather than scaled up in the recipe, because a
    # non-integer sheetScale on pixel art is how crisp edges turn to mush.
    radius = CELL * 0.44
    # Brightest mid-stroke: a blade is fastest, and so brightest, in the middle.
    # Measured: the last frame rendered NOTHING (0.00% of screen) because this
    # curve hit sin(pi) exactly at t=1. An eighth of a 0.62s effect spent on a
    # blank frame. Stopping short of pi leaves the blade still dying as it ends.
    energy = math.sin(math.pi * (0.18 + 0.70 * t)) ** 0.6

    # Trail: several fading copies BEHIND the leading edge.
    trail = 5
    for s in range(trail):
        f = s / float(trail)
        ang_c = lead - f * math.radians(58)
        strength = energy * (1.0 - f) ** 1.6
        if strength < 0.02:
            continue
        half = math.radians(58) * (1.0 - 0.26 * f)
        width = 5.4 + 3.6 * f                       # the trail smears as it ages
        for y in range(CELL):
            dy = y - cy
            for x in range(CELL):
                dx = x - cx
                d = math.hypot(dx, dy)
                band = math.exp(-((d - radius) / width) ** 2)
                if band < 0.03:
                    continue
                band = band ** 0.55          # same reasoning as the ring above
                ang = math.atan2(dy, dx)
                # Shortest angular distance to the stroke centre.
                da = (ang - ang_c + math.pi) % (2 * math.pi) - math.pi
                if abs(da) > half:
                    continue
                # Taper to nothing at both ends so it reads as a comma.
                taper = math.cos(da / half * (math.pi / 2)) ** 1.4
                put(px, x, y, STEEL_RAMP, band * strength * taper * 1.5)
    return img


def strip(frames):
    sheet = Image.new("RGBA", (CELL * len(frames), CELL), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * CELL, 0))
    return sheet


def coverage(img):
    data = list(img.getdata())
    opaque = sum(1 for p in data if p[3] > 200)
    lit = sum(1 for p in data if p[3] > 8)
    return 100.0 * opaque / len(data), 100.0 * lit / len(data)


def main():
    dry = "--dry-run" in sys.argv
    rng = random.Random(7734)          # the project's shared art seed

    shape = _table(rng)
    tear_tbl = _table(rng)
    spotted = strip([spotted_frame(i, shape, tear_tbl) for i in range(SPOTTED_FRAMES)])
    swing = strip([swing_frame(i) for i in range(SWING_FRAMES)])

    for name, img, frames in (("maniac_spotted", spotted, SPOTTED_FRAMES),
                              ("maniac_swing", swing, SWING_FRAMES)):
        op, lit = coverage(img)
        print("%-16s %dx%d  %2d frames  opaque %.1f%%  lit %.1f%%"
              % (name, img.size[0], img.size[1], frames, op, lit))
        if not dry:
            OUT_DIR.mkdir(parents=True, exist_ok=True)
            img.save(OUT_DIR / (name + ".png"), optimize=True)
    if dry:
        print("dry run - nothing written")
    else:
        print("written to", OUT_DIR)
        print("next: Setup/38 to slice, then Setup/52 to hang them on the recipes")


if __name__ == "__main__":
    main()
