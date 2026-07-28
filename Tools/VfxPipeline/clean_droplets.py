"""Rebuild the blood droplet particle sprites, and atlas them.

The originals (blood_droplet_0..3.png) were cropped straight out of the CC0
splats and have three faults that make the hit effect read as cheap:

  1. EVERY one is clipped by the frame edge -- their alpha bounding boxes all
     touch x=0/63 or y=0/63. A ParticleSystem draws each sprite centred on the
     particle, so a droplet whose art sits in a corner renders visibly offset
     from where the particle actually is, with a hard straight cut where the
     crop sliced it.
  2. All four average RGB ~(166,15,11) -- flat saturated red with no dark rim.
     The project's own blood palette, established in process_blood.py for the
     health overlays, is RIM (38,2,2) -> CORE (150,12,8). The rim is what makes
     blood read as wet instead of as a red dot.
  3. Soft/blurry alpha from an upscale, so the edges read as fog.

This crops each droplet to its content, re-centres it on a padded square,
recolours it rim->core by thickness, sharpens the alpha, and writes both the
individual sprites and a 2x2 atlas. The atlas feeds the particle system's
Texture Sheet Animation with a random start frame, so each droplet in a burst
picks a different shape -- the variety is what stops a burst looking stamped.

Usage:  python clean_droplets.py [--dry-run]
"""
import sys
from pathlib import Path

from PIL import Image

ART = Path(r"D:\The Time Killer Remake\Assets\Resources\Assets\Effects")

# Same palette the health overlays use -- consistency is the point.
RIM = (38, 2, 2)
CORE = (150, 12, 8)

CELL = 64          # output cell size
CONTENT = 44       # droplet is scaled to fit this, leaving a safe margin
ALPHA_GAIN = 1.7   # sharpen the blurry edge
ALPHA_FLOOR = 0.30 # below this the pixel is dropped, killing the fog halo


def clean(path):
    im = Image.open(path).convert("RGBA")
    bbox = im.split()[3].getbbox()
    if bbox is None:
        return None
    im = im.crop(bbox)

    # Fit inside CONTENT, preserving aspect, then centre on a CELL canvas so
    # nothing can ever touch the frame edge again.
    scale = min(CONTENT / im.width, CONTENT / im.height)
    w, h = max(1, round(im.width * scale)), max(1, round(im.height * scale))
    im = im.resize((w, h), Image.LANCZOS)

    out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    out.paste(im, ((CELL - w) // 2, (CELL - h) // 2), im)

    px = out.load()
    for y in range(CELL):
        for x in range(CELL):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            t = a / 255.0
            t = min(1.0, t * ALPHA_GAIN)
            if t < ALPHA_FLOOR:
                px[x, y] = (0, 0, 0, 0)
                continue
            # Thickness drives the colour: thin edge = near-black rim,
            # thick centre = wet crimson core.
            k = (t - ALPHA_FLOOR) / (1.0 - ALPHA_FLOOR)
            k = k ** 0.7
            col = tuple(round(RIM[i] + (CORE[i] - RIM[i]) * k) for i in range(3))
            px[x, y] = (col[0], col[1], col[2], 255 if t > 0.92 else round(t * 255))
    return out


def main():
    dry = "--dry-run" in sys.argv
    cleaned = []
    for i in range(4):
        src = ART / f"blood_droplet_{i}.png"
        im = clean(src)
        if im is None:
            print(f"skip {src.name}: fully transparent")
            continue
        bbox = im.split()[3].getbbox()
        touches = bbox[0] == 0 or bbox[1] == 0 or bbox[2] == CELL or bbox[3] == CELL
        print(f"{src.name}: bbox={bbox} touchesEdge={touches}")
        cleaned.append((i, im))
        if not dry:
            im.save(ART / f"blood_droplet_{i}.png")

    # 2x2 atlas for Texture Sheet Animation random-frame variety.
    atlas = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))
    for n, (_, im) in enumerate(cleaned):
        atlas.paste(im, ((n % 2) * CELL, (n // 2) * CELL), im)
    if not dry:
        atlas.save(ART / "blood_droplets_atlas.png")
    print(f"atlas {atlas.size} from {len(cleaned)} droplets"
          + (" (dry run, nothing written)" if dry else f" -> {ART / 'blood_droplets_atlas.png'}"))


if __name__ == "__main__":
    main()
