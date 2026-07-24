r"""Composite the Catacombs level from real tilesheet pixels.

Two outputs:
  cat_render.png   - what the level actually looks like, tiles composited in
                     the same back-to-front order Unity uses.
  cat_schematic.png - floor (green) vs collision (red) vs camera zones (yellow),
                     for auditing coverage by eye.

Run from the repo root:  python Tools\MapPipeline\render_catacombs.py
"""
import json
import os

from PIL import Image, ImageDraw

SRC = r"Assets\Resources\Assets\Maps\CastleWing.ldtk"
SHEET = r"Assets\Resources\Outsource\RogueFantasyCatacombs\mainlevbuild.png"
OUT = (r"C:\Users\Asus\AppData\Local\Temp\claude\D--The-Time-Killer-Remake"
       r"\efc774ba-0763-4caf-909e-36b7ea302f6d\scratchpad")
LEVEL = "Catacombs"
G = 16
ZOOM = 2

# Back-to-front, matching the importer's sorting orders
# (Floor -20, Rug -15, WallFace -10, DecorMain/Deco -9, Overhead +10).
ORDER = ["Floor", "Rug", "WallFace", "DecorMain", "DecorDeco", "Overhead"]

os.makedirs(OUT, exist_ok=True)
d = json.load(open(SRC, encoding="utf-8"))
lv = next(l for l in d["levels"] if l["identifier"] == LEVEL)
li = {l["__identifier"]: l for l in lv["layerInstances"]}
W, H = lv["pxWid"] // G, lv["pxHei"] // G
sheet = Image.open(SHEET).convert("RGBA")

# ------------------------------------------------------------------ render
img = Image.new("RGBA", (lv["pxWid"], lv["pxHei"]), (10, 10, 12, 255))
counts = {}
for name in ORDER:
    layer = li.get(name)
    if not layer:
        continue
    counts[name] = len(layer["gridTiles"])
    for t in layer["gridTiles"]:
        sx, sy = t["src"]
        tile = sheet.crop((sx, sy, sx + G, sy + G))
        img.alpha_composite(tile, (t["px"][0], t["px"][1]))
img = img.resize((lv["pxWid"] * ZOOM, lv["pxHei"] * ZOOM), Image.NEAREST)
path = os.path.join(OUT, "cat_render.png")
img.convert("RGB").save(path)
print("saved", path, img.size, counts)

# --------------------------------------------------------------- schematic
S = 12
sch = Image.new("RGB", (W * S, H * S), (18, 18, 22))
dr = ImageDraw.Draw(sch)
csv = li["Collision"]["intGridCsv"]
for cy in range(H):
    for cx in range(W):
        if csv[cy * W + cx]:
            dr.rectangle([cx * S, cy * S, cx * S + S - 1, cy * S + S - 1],
                         fill=(150, 45, 45))
for t in li["Floor"]["gridTiles"]:
    cx, cy = t["px"][0] // G, t["px"][1] // G
    dr.rectangle([cx * S, cy * S, cx * S + S - 1, cy * S + S - 1],
                 fill=(50, 105, 60))
for e in li["Entities"]["entityInstances"]:
    x, y = e["px"][0] // G * S, e["px"][1] // G * S
    w, h = e["width"] // G * S, e["height"] // G * S
    dr.rectangle([x, y, x + w - 1, y + h - 1], outline=(250, 205, 40))

rooms = json.load(open(r"Assets\Resources\Assets\Maps\CatacombsRooms.json",
                      encoding="utf-8"))
ox, k = rooms["originX"], rooms["originY"]
for name, (rx, ry, rw, rh) in rooms["rooms"].items():
    px, py = (rx + ox) * S + 2, (k - (ry + rh - 1)) * S + 2
    dr.text((px + 1, py + 1), name, fill=(0, 0, 0))
    dr.text((px, py), name, fill=(255, 240, 200))
path = os.path.join(OUT, "cat_schematic.png")
sch.save(path)
print("saved", path, sch.size)
