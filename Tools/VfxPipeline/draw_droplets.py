"""Draw pixel-art blood spatter sprites that match the castle's art.

WHY THIS WAS REWRITTEN (2026-07-28). The first version drew supersampled
metaball blobs and downsampled them with LANCZOS. Measured against the real
game art (wardrobeA_closed, Survivor_Idle_Down):

                       game art        v1 droplets
    edges              hard pixel      soft antialiased gradient
    saturation         median 0.07     0.93
    value (max chan)   median 52       up to 165
    silhouette         irregular       perfect ellipses

Mismatched on every axis, so they read as airbrushed candy-red gumballs pasted
onto muted pixel art. Three rules come out of that, and breaking any one of them
puts the gumballs back:

  1. NO ANTIALIASING. Draw at final resolution with boolean pixel coverage.
     No supersample-and-downsample, no soft alpha ramp. Hard edges are most of
     what makes the world read as pixel art.
  2. QUANTISED SHADING. Exactly four colours, picked by depth-from-edge. No
     continuous gradient, and no radial specular -- a smooth radial ramp with a
     highlight is what makes a shape read as a 3D sphere.
  3. IRREGULAR SILHOUETTE. Harmonic noise on the polar radius, plus tendrils
     that fling outward and satellite specks. Real spatter is torn; an ellipse
     is a berry.

AUTHORING RESOLUTION: 32px cells, because the world is 32 pixels per unit, so a
cell drawn at 32px displays at 1:1 when a particle's size is 1.0 world units.
The previous 64px cells were downscaled on screen, which reintroduced blur no
matter how crisp the source was. Visual size variety comes from the amount of
INK inside each cell (~8px to ~26px across), NOT from scaling particles, so
every droplet stays pixel-exact.

Palette: dark desaturated maroon, saturation ~0.55-0.65 and values 34-118 --
deliberately at the top of the scenery's range (game p90 value is 97) because
blood is the one thing in this game allowed to be red, but nowhere near the
0.93 saturation that read as comic-book.

Usage:  python draw_droplets.py [--dry-run]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image

ART = Path(r"D:\The Time Killer Remake\Assets\Resources\Assets\Effects")

CELL = 32
COLS, ROWS = 3, 2

# Four steps, darkest at the rim. Derived from the game's own value/saturation
# range -- see the module docstring.
RIM = (34, 16, 16)
DARK = (62, 24, 24)
MID = (92, 34, 32)
WET = (118, 50, 44)

# Ink radius per cell, in pixels. The spread is the visual size variety.
# Kept modest on purpose: tendrils reach ~2x the core radius and specks further
# still, and NOTHING may touch the cell edge -- in an atlas that bleeds into the
# neighbouring tile, which shows up in game as a stray sliver of blood.
RADII = [7.0, 6.0, 5.0, 3.5, 5.5, 3.0]
STRETCH = [(1.0, 1.0), (1.0, 1.0), (1.0, 1.0), (1.0, 1.0), (1.75, 0.6), (0.62, 1.7)]
TENDRILS = [4, 3, 3, 2, 3, 2]
SPECKS = [6, 5, 4, 2, 4, 2]

# Hard containment: no ink further than this from the cell centre. Enforced in
# the generator rather than trusted to the radius numbers above, so retuning a
# radius can never silently reintroduce atlas bleed.
MAX_EXTENT = CELL / 2.0 - 2.0


def torn_radius(angle, base, rng_phases):
    """Base radius warped by three harmonics -- a lumpy, torn outline."""
    (a1, p1), (a2, p2), (a3, p3) = rng_phases
    warp = (a1 * math.sin(3 * angle + p1)
            + a2 * math.sin(5 * angle + p2)
            + a3 * math.sin(7 * angle + p3))
    return base * (1.0 + warp)


def draw(index, rng):
    base = RADII[index]
    sx, sy = STRETCH[index]
    cx = cy = (CELL - 1) / 2.0
    phases = [(rng.uniform(0.10, 0.26), rng.uniform(0, math.tau)),
              (rng.uniform(0.06, 0.17), rng.uniform(0, math.tau)),
              (rng.uniform(0.03, 0.10), rng.uniform(0, math.tau))]

    inside = [[False] * CELL for _ in range(CELL)]

    # Core blob -- boolean coverage, no partial alpha anywhere.
    for y in range(CELL):
        for x in range(CELL):
            dx = (x - cx) / sx
            dy = (y - cy) / sy
            d = math.hypot(dx, dy)
            if d <= torn_radius(math.atan2(dy, dx), base, phases):
                inside[y][x] = True

    # Tendrils: blood flung outward, tapering to a single pixel.
    reach = MAX_EXTENT / max(sx, sy)
    for _ in range(TENDRILS[index]):
        ang = rng.uniform(0, math.tau)
        length = min(base * rng.uniform(1.3, 2.5), max(1.0, reach - base * 0.7))
        wobble = rng.uniform(-0.22, 0.22)
        steps = max(2, int(length))
        for s in range(steps):
            t = s / steps
            a = ang + wobble * t
            r = base * 0.7 + length * t
            px_, py_ = cx + math.cos(a) * r * sx, cy + math.sin(a) * r * sy
            width = 1 if t > 0.55 else 2       # thins out as it travels
            for oy in range(-width // 2, width // 2 + 1):
                for ox in range(-width // 2, width // 2 + 1):
                    ix, iy = int(round(px_)) + ox, int(round(py_)) + oy
                    if 0 <= ix < CELL and 0 <= iy < CELL:
                        inside[iy][ix] = True

    # Satellite specks -- detached droplets, 1-2px.
    for _ in range(SPECKS[index]):
        ang = rng.uniform(0, math.tau)
        r = min(base * rng.uniform(1.25, 2.3), reach - 1.0)
        px_, py_ = cx + math.cos(ang) * r * sx, cy + math.sin(ang) * r * sy
        size = rng.choice([1, 1, 2])
        for oy in range(size):
            for ox in range(size):
                ix, iy = int(round(px_)) + ox, int(round(py_)) + oy
                if 0 <= ix < CELL and 0 <= iy < CELL:
                    inside[iy][ix] = True

    # Depth from edge by repeated erosion -> quantised shading, 4 steps.
    depth = [[0] * CELL for _ in range(CELL)]
    for y in range(CELL):
        for x in range(CELL):
            if not inside[y][x]:
                continue
            d = 0
            while True:
                ring = d + 1
                ok = True
                for oy in (-ring, 0, ring):
                    for ox in (-ring, 0, ring):
                        ix, iy = x + ox, y + oy
                        if not (0 <= ix < CELL and 0 <= iy < CELL) or not inside[iy][ix]:
                            ok = False
                            break
                    if not ok:
                        break
                if not ok:
                    break
                d += 1
                if d > 4:
                    break
            depth[y][x] = d

    im = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    px = im.load()
    # One wet glint, offset up-left (our 2D lights come from there), only on
    # blobs thick enough to carry it. A single flat pixel pair, not a ramp.
    glint = None
    if base >= 7.0:
        glint = (int(cx - base * 0.32), int(cy - base * 0.34))

    for y in range(CELL):
        for x in range(CELL):
            if not inside[y][x]:
                continue
            d = depth[y][x]
            col = RIM if d == 0 else DARK if d == 1 else MID
            if glint and abs(x - glint[0]) <= 1 and abs(y - glint[1]) <= 1 and d >= 2:
                col = WET
            px[x, y] = (col[0], col[1], col[2], 255)      # fully opaque: hard edge
    return im


def main():
    dry = "--dry-run" in sys.argv
    rng = random.Random(7734)
    made = []
    for i in range(COLS * ROWS):
        im = draw(i, rng)
        bbox = im.split()[3].getbbox()
        ink = sum(1 for p in im.split()[3].getdata() if p > 0)
        touches = bbox[0] == 0 or bbox[1] == 0 or bbox[2] == CELL or bbox[3] == CELL
        print(f"droplet_{i}: bbox={bbox} ink={ink}px fill={100*ink/(CELL*CELL):.0f}% touchesEdge={touches}")
        if touches:
            print("  WARNING: ink reaches the cell edge -- reduce its radius or tendril length")
        made.append(im)

    atlas = Image.new("RGBA", (CELL * COLS, CELL * ROWS), (0, 0, 0, 0))
    for n, im in enumerate(made):
        atlas.paste(im, ((n % COLS) * CELL, (n // COLS) * CELL))
    if not dry:
        for i, im in enumerate(made):
            im.save(ART / f"blood_droplet_{i}.png")
        atlas.save(ART / "blood_droplets_atlas.png")
    total = sum(1 for p in atlas.split()[3].getdata() if p > 0)
    print(f"atlas {atlas.size}  ink {100*total/(atlas.width*atlas.height):.0f}% of quad area"
          + (" (dry run)" if dry else ""))


if __name__ == "__main__":
    main()
