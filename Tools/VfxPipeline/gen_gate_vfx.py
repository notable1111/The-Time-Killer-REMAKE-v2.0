"""Generate the gate-opened effect: the way out, not a firework.

THE BEAT. AllClocksFixedEvent plays this AT THE EXIT DOOR, at the moment the
run's question changes from "where are the clocks" to "where is the door". So
this is a DIRECTION, not a celebration - the brief in ClockEffectsSetup is
explicit about it, and about staying silent (ExitDoor already creaks and
AudioDirector fires a map-wide sting; a third sound on that frame is noise).

WHAT IT READS AGAINST, measured in Catacombs at the door (16.5, 31.1) rather
than assumed: mean colour (14.9, 11.6, 8.9), with only 1.5% of pixels above
luminance 60. Very dark, and notably WARM - red > green > blue. So the shaft is
COOL. Cool on warm separates by hue as well as by brightness, which is what lets
it read without being bright enough to look like a firework.

CELL SIZE - A DELIBERATE DEPARTURE FROM THE BRIEF, and the reason matters.
The brief asks for 32px cells AND sheetScale 1.4, while also warning that
"anything other than 1:1 resamples and reads as mush". Those cannot both hold:
1.4 world units at 32 px/unit needs a 44.8px cell, which is not an integer. The
options were 32px at scale 1.4 (resampled and soft), or 64px at scale 1.0 (crisp
and 2.0 units). Chosen: 64px at scale 1.0.
  * It honours the brief's OWN reasoning - scale 1.4 is itself the resample the
    brief warns against.
  * Measured today on the threat sheets: an effect that is too small is not a
    subtle effect, it is an invisible one. The swing sat at 0.41% of screen and
    could not be seen at all despite being the brighter of the two.
The recipe's sheetScale is set to 1.0 to match. If it is ever wanted smaller,
change the CELL here, not the scale.

SHAPE
  * A shaft entering from the top and WIDENING downward - a doorway spilling
    light into a dark room, which is the shape of an opening rather than a burst.
  * Vertical streaking inside it, so it reads as light in dusty air and not as a
    flat white wedge. Same rule gen_light_shaft.py established.
  * Dust motes drifting DOWN and slightly outward, brightest where they cross the
    shaft. The motes are what make it feel like air rather than a decal.
  * It grows, holds, and settles - it does not flash. A flash says "well done";
    a holding shaft says "go there".

Usage: python gen_gate_vfx.py [--dry-run]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image

CELL = 64                       # 64px / 32ppu = 2.0 world units at sheetScale 1
FRAMES = 12                     # 12 @ 11fps = 1.09s, a touch longer than the 0.7s shake
OUT_DIR = Path("Assets/Resources/Assets/Effects/Vfx")

# Cool moonlight, against warm dark stone. Dark -> pale.
GATE_RAMP = [(18, 26, 38), (46, 66, 92), (96, 128, 166), (168, 196, 224), (238, 248, 255)]


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
    """Keep the brightest contribution; alpha follows luminance."""
    if intensity <= 0.004:
        return
    xi, yi = int(x), int(y)
    if not (0 <= xi < CELL and 0 <= yi < CELL):
        return
    a = px[xi, yi][3]
    alpha = int(255 * min(1.0, intensity))
    if alpha <= a:
        return
    r, g, b = ramp(GATE_RAMP, intensity)
    px[xi, yi] = (r, g, b, alpha)


def frame(index, streaks, motes):
    img = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    px = img.load()
    t = index / float(FRAMES - 1)

    # Grow, hold, settle. Never a flash.
    if t < 0.25:
        env = (t / 0.25) ** 0.7
    elif t < 0.60:
        env = 1.0
    else:
        env = (1.0 - (t - 0.60) / 0.40) ** 1.3

    # The shaft: narrow at the top (the opening), widening as it falls.
    top_half = 3.0 + 5.0 * min(1.0, t / 0.3)
    bot_half = 12.0 + 9.0 * min(1.0, t / 0.5)
    for y in range(CELL):
        v = y / float(CELL - 1)                    # 0 at top
        half = top_half + (bot_half - top_half) * (v ** 0.85)
        # Fades along its length: the far end dissolves rather than stopping.
        along = (1.0 - v) ** 0.55
        for x in range(CELL):
            dx = (x - (CELL - 1) / 2.0) / half
            if abs(dx) > 1.35:
                continue
            across = math.exp(-(dx * dx) * 1.9)    # soft edge, bright centre line
            # Dusty streaking, drifting slowly downward over the effect.
            s = streaks[x % len(streaks)]
            streak = 0.72 + 0.28 * math.sin(s * 6.283 + v * 7.0 - t * 2.2)
            put(px, x, y, across * along * streak * env * 0.95)

    # Dust motes: they are what make it air rather than a gradient.
    for m in motes:
        mx, my0, speed, size, phase = m
        my = (my0 + t * speed) % 1.15
        y = my * (CELL - 1)
        x = mx * (CELL - 1) + math.sin(t * 3.0 + phase) * 2.2
        v = y / float(CELL - 1)
        half = top_half + (bot_half - top_half) * (max(0.0, min(1.0, v)) ** 0.85)
        inside = math.exp(-(((x - (CELL - 1) / 2.0) / max(1.0, half)) ** 2) * 1.5)
        life = math.sin(math.pi * max(0.0, min(1.0, my / 1.15))) ** 0.6
        bright = env * life * inside
        if bright < 0.05:
            continue
        for oy in range(-size, size + 1):
            for ox in range(-size, size + 1):
                d = math.hypot(ox, oy) / (size + 0.6)
                if d > 1.0:
                    continue
                put(px, x + ox, y + oy, bright * (1.0 - d) ** 0.7 * 1.25)
    return img


def main():
    dry = "--dry-run" in sys.argv
    rng = random.Random(7734)                       # the project's shared art seed
    streaks = [rng.random() for _ in range(CELL)]
    motes = [(0.5 + (rng.random() - 0.5) * 0.72,    # x, clustered on the shaft
              rng.random(),                          # start height
              0.55 + rng.random() * 0.75,            # fall speed
              rng.choice((0, 0, 1)),                 # size in pixels
              rng.random() * 6.283) for _ in range(16)]

    frames = [frame(i, streaks, motes) for i in range(FRAMES)]
    sheet = Image.new("RGBA", (CELL * FRAMES, CELL), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * CELL, 0))

    data = list(sheet.getdata())
    opaque = 100.0 * sum(1 for p in data if p[3] > 200) / len(data)
    lit = 100.0 * sum(1 for p in data if p[3] > 8) / len(data)
    print("gate_opened   %dx%d  %d frames  opaque %.1f%%  lit %.1f%%"
          % (sheet.size[0], sheet.size[1], FRAMES, opaque, lit))
    if dry:
        print("dry run - nothing written")
        return
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    sheet.save(OUT_DIR / "gate_opened.png", optimize=True)
    print("written to", OUT_DIR)
    print("next: Setup/38 to slice, then set the clip to 11fps and assign to GateOpened.sheetClip")


if __name__ == "__main__":
    main()
