"""Professional pass on the CC0 blood overlays.

- Radial edge mask: blood concentrates at screen edges, center stays playable
  (critical keeps ~18% center presence, subtle ~6%).
- Two-tone color: dark rim red -> deep crimson highlights by alpha intensity.
- Contrast boost on alpha so splats read as wet blood, not fog.
Outputs *_v2 textures + a second out-of-phase layer per band (rotated source
so the two layers never look like copies).
"""
from PIL import Image, ImageEnhance
import math, os

SRC = r"C:\Users\Asus\AppData\Local\Temp\claude\D--The-Time-Killer-Remake\4ddea370-6588-4838-8b6d-669c8c573cb2\scratchpad\health_overlay"
DST = r"D:\The Time Killer Remake\Assets\Resources\Assets\HealthVfx"

RIM = (38, 2, 2)        # near-black red at the outer rim
CORE = (150, 12, 8)     # wet crimson where blood is thickest

def radial_keep(x, y, w, h, center_keep, power):
    # 0 at center -> 1 at the frame edge, shaped by power
    nx, ny = (x / w) * 2 - 1, (y / h) * 2 - 1
    d = min(1.0, math.sqrt(nx * nx + ny * ny) / math.sqrt(2))
    return center_keep + (1 - center_keep) * (d ** power)

def process(src_name, dst_name, center_keep, power, alpha_gain, rotate=0):
    im = Image.open(os.path.join(SRC, src_name)).convert("RGBA")
    if rotate:
        im = im.rotate(rotate, expand=False)
    w, h = im.size
    px = im.load()
    out = Image.new("RGBA", (w, h))
    po = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                po[x, y] = (0, 0, 0, 0)
                continue
            keep = radial_keep(x, y, w, h, center_keep, power)
            # contrast-boosted, edge-masked alpha
            na = a / 255.0
            na = na ** 0.75            # lift mids so splats read
            na = min(1.0, na * alpha_gain) * keep
            # two-tone: thicker blood -> brighter crimson
            t = na
            cr = int(RIM[0] + (CORE[0] - RIM[0]) * t)
            cg = int(RIM[1] + (CORE[1] - RIM[1]) * t)
            cb = int(RIM[2] + (CORE[2] - RIM[2]) * t)
            po[x, y] = (cr, cg, cb, int(na * 255))
    out.save(os.path.join(DST, dst_name))
    print("saved", dst_name, out.size)

# Subtle band (2 HP): soft edges only, faint center
process("Health_Brush_3_2.png", "band_subtle.png",       center_keep=0.06, power=2.2, alpha_gain=1.15)
process("Health_blotches.png",  "band_subtle_b.png",     center_keep=0.04, power=2.6, alpha_gain=0.9, rotate=180)
# Critical band (1 HP): heavy edges, center still ~readable
process("Health_Grunge_01.png", "band_critical.png",     center_keep=0.16, power=1.6, alpha_gain=1.35)
process("Health_texture.png",   "band_critical_b.png",   center_keep=0.10, power=2.0, alpha_gain=1.1, rotate=180)
# Hit flash: splatter, mild center-keep so the burst hits the whole frame
process("Health_Splats_Full.png", "hit_splatter.png",    center_keep=0.45, power=1.2, alpha_gain=1.3)
