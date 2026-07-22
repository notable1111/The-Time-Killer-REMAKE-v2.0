"""Furniture prop pipeline (2026-07-23, map v2 furniture pass).

Takes an AI-generated prop sheet (objects on a flat magenta key background),
slices it into individual props, and processes each one game-ready:
  1. Key out the magenta background -> transparency.
  2. Connected-component slicing (props must not touch on the sheet).
  3. Components are ordered row-major (top-to-bottom, left-to-right) and
     matched to the expected prop list for that sheet.
  4. Each prop: crop -> downscale to its target pixel size (16 PPU) with
     NEAREST -> quantize every opaque pixel to the RF Castle palette.
  5. Saved to Assets/Resources/Assets/Furniture/<name>.png.

Usage:  python Tools/ArtPipeline/process_props.py <sheet.png> <room>
Rooms and their expected prop order are defined in SHEETS below.
A contact sheet (4x zoom, labeled) is written next to the input for review.
"""
import sys
import os
from PIL import Image, ImageDraw

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
TILESET = os.path.join(ROOT, "Assets", "Resources", "Outsource", "RF Castle", "Sliced", "mainlevbuild.png")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Assets", "Furniture")

KEY = (255, 0, 255)      # magenta background
KEY_TOLERANCE = 90       # per-channel-ish distance: catches AA fringes
MIN_AREA = 900           # ignore specks (sheet is ~2048px)

# (name, target_w_px, target_h_px) at 16 PPU — world size = px/16.
SHEETS = {
    "kitchen": [
        ("kitchen_table",    64, 40),
        ("kitchen_hearth",   48, 48),
        ("kitchen_shelf",    32, 40),
        ("kitchen_block",    24, 24),
        ("kitchen_bench",    40, 20),
        ("kitchen_barrel",   20, 24),
        ("kitchen_firewood", 28, 18),
        ("kitchen_sacks",    30, 20),
        ("kitchen_crates",   28, 32),
        ("kitchen_stool",    14, 14),
    ],
    "armory": [
        ("armory_weaponrack", 40, 40),
        ("armory_spearrack",  36, 48),
        ("armory_armorstand", 28, 48),
        ("armory_shield",     24, 28),
        ("armory_chest",      30, 24),
        ("armory_anvil",      26, 26),
        ("armory_dummy",      28, 44),
        ("armory_arrowbarrel", 20, 26),
        ("armory_grindstone", 30, 28),
    ],
    "library": [
        ("library_bookcase_full", 36, 56),
        ("library_bookcase_lean", 36, 56),
        ("library_desk",       44, 32),
        ("library_lectern",    22, 36),
        ("library_globe",      22, 32),
        ("library_armchair",   28, 32),
        ("library_bookstack",  20, 16),
        ("library_candelabra", 18, 40),
        ("library_sidetable",  26, 26),
    ],
}

# If the model laid the grid out in a different order than prompted, fix the
# mapping here per room: index-in-reading-order -> expected-list index.
ORDER_OVERRIDE: dict[str, list[int]] = {
    # armory sheet: model drew row2 as shield/chest/DUMMY, row3 anvil/arrows/grind
    "armory": [0, 1, 2, 3, 4, 6, 5, 7, 8],
}

# Pre-quantize brightness lift per room: (gamma, gain). Gamma < 1 lifts the
# shadows without blowing highlights; gain is a straight multiply after.
# Kitchen came out too murky on the first pass (user call 2026-07-23).
BRIGHTEN: dict[str, tuple[float, float]] = {
    "kitchen": (0.78, 1.18),
}


def brighten(im, gamma, gain):
    lut = [min(255, round(((v / 255) ** gamma) * 255 * gain)) for v in range(256)]
    r, g, b, a = im.split()
    return Image.merge("RGBA", (r.point(lut), g.point(lut), b.point(lut), a))


def load_palette():
    im = Image.open(TILESET).convert("RGBA")
    colors = set()
    for r, g, b, a in im.convert("RGBA").getdata():
        if a > 200:
            colors.add((r, g, b))
    return sorted(colors)


def key_out(im):
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    kr, kg, kb = KEY
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if abs(r - kr) + abs(g - kg) + abs(b - kb) < KEY_TOLERANCE * 3:
                px[x, y] = (0, 0, 0, 0)
    return im


def components(im):
    """Connected components over opaque pixels (8-neighbour, coarse grid)."""
    w, h = im.size
    alpha = im.getchannel("A").load()
    CELL = 8  # coarse grid keeps this fast and bridges 1-2px gaps
    gw, gh = (w + CELL - 1) // CELL, (h + CELL - 1) // CELL
    occupied = [[False] * gw for _ in range(gh)]
    for gy in range(gh):
        for gx in range(gw):
            for y in range(gy * CELL, min((gy + 1) * CELL, h), 2):
                for x in range(gx * CELL, min((gx + 1) * CELL, w), 2):
                    if alpha[x, y] > 40:
                        occupied[gy][gx] = True
                        break
                if occupied[gy][gx]:
                    break
    seen = [[False] * gw for _ in range(gh)]
    boxes = []
    for gy in range(gh):
        for gx in range(gw):
            if not occupied[gy][gx] or seen[gy][gx]:
                continue
            stack = [(gx, gy)]
            seen[gy][gx] = True
            minx, miny, maxx, maxy = gx, gy, gx, gy
            while stack:
                cx, cy = stack.pop()
                minx, maxx = min(minx, cx), max(maxx, cx)
                miny, maxy = min(miny, cy), max(maxy, cy)
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < gw and 0 <= ny < gh and occupied[ny][nx] and not seen[ny][nx]:
                            seen[ny][nx] = True
                            stack.append((nx, ny))
            box = (minx * CELL, miny * CELL, min((maxx + 1) * CELL, w), min((maxy + 1) * CELL, h))
            if (box[2] - box[0]) * (box[3] - box[1]) >= MIN_AREA:
                boxes.append(box)
    # row-major reading order: bucket into rows by vertical overlap
    boxes.sort(key=lambda b: b[1])
    rows: list[list[tuple]] = []
    for b in boxes:
        for row in rows:
            if b[1] < row[0][3] - (row[0][3] - row[0][1]) * 0.5:
                row.append(b)
                break
        else:
            rows.append([b])
    ordered = []
    for row in rows:
        ordered.extend(sorted(row, key=lambda b: b[0]))
    return ordered


def tight_crop(im):
    bbox = im.getbbox()
    return im.crop(bbox) if bbox else im


def quantize(im, palette):
    px = im.load()
    w, h = im.size
    cache = {}
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 90:
                px[x, y] = (0, 0, 0, 0)
                continue
            key = (r, g, b)
            if key not in cache:
                cache[key] = min(palette, key=lambda c: (c[0] - r) ** 2 + (c[1] - g) ** 2 + (c[2] - b) ** 2)
            nr, ng, nb = cache[key]
            px[x, y] = (nr, ng, nb, 255)
    return im


def main():
    sheet_path, room = sys.argv[1], sys.argv[2]
    specs = SHEETS[room]
    palette = load_palette()
    os.makedirs(OUT_DIR, exist_ok=True)

    sheet = key_out(Image.open(sheet_path))
    if room in BRIGHTEN:
        sheet = brighten(sheet, *BRIGHTEN[room])
    boxes = components(sheet)
    print(f"{room}: found {len(boxes)} components, expected {len(specs)}")

    order = ORDER_OVERRIDE.get(room, list(range(len(boxes))))
    results = []
    for i, box in enumerate(boxes):
        if i >= len(order) or order[i] >= len(specs):
            print(f"  extra component at {box} — skipped")
            continue
        name, tw, th = specs[order[i]]
        crop = tight_crop(sheet.crop(box))
        # fit inside target box, preserve aspect
        scale = min(tw / crop.width, th / crop.height)
        nw, nh = max(1, round(crop.width * scale)), max(1, round(crop.height * scale))
        small = quantize(crop.resize((nw, nh), Image.NEAREST), palette)
        out = os.path.join(OUT_DIR, name + ".png")
        small.save(out)
        results.append((name, small))
        print(f"  {name}: {crop.width}x{crop.height} -> {nw}x{nh}")

    # labeled 4x contact sheet for approval
    zoom = 4
    pad = 12
    cw = sum(s.width * zoom + pad for _, s in results) + pad
    ch = max(s.height * zoom for _, s in results) + 40
    contact = Image.new("RGBA", (cw, ch), (40, 40, 46, 255))
    draw = ImageDraw.Draw(contact)
    x = pad
    for name, s in results:
        big = s.resize((s.width * zoom, s.height * zoom), Image.NEAREST)
        contact.paste(big, (x, ch - 30 - big.height), big)
        draw.text((x, ch - 24), name.split("_", 1)[1], fill=(220, 220, 220, 255))
        x += big.width + pad
    review = os.path.splitext(sheet_path)[0] + "_contact.png"
    contact.save(review)
    print(f"contact sheet: {review}")


if __name__ == "__main__":
    main()
