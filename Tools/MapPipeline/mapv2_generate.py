"""Map v2 draft generator for CastleWing.ldtk.

Adds, using the exact wall-raising rules from CastleWingSetup.cs:
  GROUND LEVEL (grows the CastleWing level right+down; origin fixed, no shifts):
    - Kitchen        (26,-12,12,10) + corridor from Guardroom (30,-2,3,2)
    - Armory         (40,0,10,10)   + corridor from Guardroom (38,4,2,3)
    - Library        (40,20,12,10)  + corridor from Great Chamber (36,24,4,3)
    - Servant passage: hall south hidden door (8,-3,2,3) + vertical (8,-8,2,4)
                       + long east passage (10,-8,16,2) into the Kitchen
  NEW LEVELS: UpperGallery (balcony over the hall, stairs land from Great
    Chamber - teleport hookup later), Undercroft (crypt + 2 alcoves).

Every new floor cell CLEARS existing tiles on all tile layers first, so
doorway carving through existing walls/bands is automatic.
Collision = full recompute, 100%-coverage rule. Camera zones appended.
"""
import copy, json, shutil, uuid

SRC = r"Assets\Resources\Assets\Maps\CastleWing.ldtk"
BAK = SRC + ".pre-v2.bak"

MAIN_CW = 37  # mainlevbuild tileset __cWid
G = 16

d = json.load(open(SRC, encoding="utf-8"))
assert d["nextUid"] == 503, f"unexpected nextUid {d['nextUid']}"
assert len(d["levels"]) == 1, "v2 already applied?"
shutil.copyfile(SRC, BAK)

main_uid = next(t["uid"] for t in d["defs"]["tilesets"] if t["identifier"] == "Mainlevbuild")
main_rel = next(t["relPath"] for t in d["defs"]["tilesets"] if t["identifier"] == "Mainlevbuild")

def mod(a, m): return ((a % m) + m) % m

def tile_entry(col, row_top, cx, cy, cwid):
    return {"px": [cx * G, cy * G], "src": [col * G, row_top * G], "f": 0,
            "t": row_top * MAIN_CW + col, "d": [cy * cwid + cx], "a": 1}

# ---------------- shared painting engine ----------------
# Works in a per-level "script space" with mapping cx = x + OX, cy = K - y.

class LevelPaint:
    def __init__(self, ox, k, cwid, chei, existing=None):
        self.ox, self.k, self.cw, self.ch = ox, k, cwid, chei
        # maps layer -> {(cx,cy): tile-entry}
        self.layers = existing or {n: {} for n in
            ("Floor", "WallFace", "Overhead", "Rug", "DecorMain", "DecorDeco")}

    def cell(self, x, y): return (x + self.ox, self.k - y)

    def put(self, layer, col, row_top, x, y):
        cx, cy = self.cell(x, y)
        assert 0 <= cx < self.cw and 0 <= cy < self.ch, f"OOB {layer} {(x,y)}->{(cx,cy)}"
        self.layers[layer][(cx, cy)] = tile_entry(col, row_top, cx, cy, self.cw)

    def clear_cell(self, x, y):
        c = self.cell(x, y)
        for name, tiles in self.layers.items():
            tiles.pop(c, None)

    def has(self, layer, x, y): return self.cell(x, y) in self.layers[layer]

def paint_areas(p, new_floor, all_floor):
    for (x, y) in new_floor:
        p.clear_cell(x, y)                       # carve anything there
    for (x, y) in new_floor:                     # floor: seamless 8x8 patch
        p.put("Floor", mod(x, 8), 27 + mod(y, 8), x, y)
    for (x, y) in new_floor:
        # north face where the cell above is not floor
        if (x, y + 1) not in all_floor:
            for i in range(8):
                cy_ = y + 1 + i
                if (x, cy_) in all_floor: break
                if i < 4:   p.put("WallFace", 6 + mod(x, 10), 11 - i, x, cy_)
                elif i == 4: p.put("WallFace", 6 + mod(x, 10), 7, x, cy_)
                elif i < 7:  p.put("WallFace", 8 + mod(x, 4), 24 if i == 5 else 25, x, cy_)
                else:        p.put("WallFace", 6 + mod(x, 10), 0, x, cy_)
        # south band (overhead)
        if (x, y - 1) not in all_floor:
            for i in range(3):
                cy_ = y - 1 - i
                if (x, cy_) in all_floor: break
                if i == 0: p.put("Overhead", 6 + mod(x, 10), 0, x, cy_)
                else:      p.put("Overhead", 8 + mod(x, 4), 24 if i == 1 else 25, x, cy_)
        # side columns, 2 thick
        for dr in (-1, 1):
            if (x + dr, y) not in all_floor:
                for t in (1, 2):
                    cx_ = x + dr * t
                    if (cx_, y) in all_floor: break
                    if not p.has("WallFace", cx_, y):
                        p.put("WallFace", 7 + mod(cx_, 2), 8 + mod(y, 4), cx_, y)

def collision_csv(p, all_floor):
    csv = [0] * (p.cw * p.ch)
    for (x, y) in all_floor:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (x + dx, y + dy)
                if n not in all_floor:
                    cx, cy = p.cell(*n)
                    if 0 <= cx < p.cw and 0 <= cy < p.ch:
                        csv[cy * p.cw + cx] = 1
    # sanity: nothing on floor
    for (x, y) in all_floor:
        cx, cy = p.cell(x, y)
        assert csv[cy * p.cw + cx] == 0
    return csv

def zone_entity(p, cx_, cy_, sx, sy, cam_uid, world_x, world_y):
    left = int(round((cx_ - sx / 2 + p.ox) * G))
    top = int(round((p.k + 1 - (cy_ + sy / 2)) * G))
    return {"__identifier": "CameraZone", "__grid": [left // G, top // G],
            "__pivot": [0, 0], "__tags": ["camera"], "__tile": None,
            "__smartColor": "#FFCC00", "__worldX": world_x + left, "__worldY": world_y + top,
            "iid": str(uuid.uuid4()), "width": int(sx * G), "height": int(sy * G),
            "defUid": cam_uid, "px": [left, top], "fieldInstances": []}

# ---------------- ground level: parse existing ----------------
lvl = d["levels"][0]
LIS = {li["__identifier"]: li for li in lvl["layerInstances"]}
OX, K = 12, 37   # verified mapping: cx = x+12, cy = 37-y
NEW_CW, NEW_CH = 66, 54

ground = LevelPaint(OX, K, NEW_CW, NEW_CH, existing={
    name: {(t["px"][0] // G, t["px"][1] // G): t for t in LIS[name]["gridTiles"]}
    for name in ("Floor", "WallFace", "Overhead", "Rug", "DecorMain", "DecorDeco")})

old_floor = {( cx - OX, K - cy) for (cx, cy) in ground.layers["Floor"].keys()}
assert len(old_floor) == 723

RECTS = {
    "kitchen":       (26, -12, 12, 10),
    "kitchen_cor":   (30, -2, 3, 2),
    "armory":        (40, 0, 10, 10),
    "armory_cor":    (38, 4, 2, 3),
    "library":       (40, 20, 12, 10),
    "library_cor":   (36, 24, 4, 3),
    "pass_door":     (8, -3, 2, 3),
    "pass_vert":     (8, -8, 2, 5),   # y -8..-4 — meets the door row at -3 (no gap!)
    "pass_east":     (10, -8, 16, 2),
}
new_floor = set()
for (rx, ry, rw, rh) in RECTS.values():
    for x in range(rx, rx + rw):
        for y in range(ry, ry + rh):
            new_floor.add((x, y))
all_floor = old_floor | new_floor

paint_areas(ground, new_floor, all_floor)
csv = collision_csv(ground, all_floor)

ZONES = [  # (center, size) script-space, mirroring the v1 zone style
    ((32, -5), (16, 20)),      # kitchen
    ((31.5, -1), (7, 10)),     # guardroom->kitchen corridor
    ((45, 7), (14, 20)),       # armory
    ((39, 5.5), (8, 9)),       # armory corridor
    ((46, 27), (16, 20)),      # library
    ((38, 25.5), (8, 9)),      # library corridor
    ((9, -3.5), (7, 13)),      # servant passage vertical + hidden door
    ((17.5, -7), (19, 8)),     # servant passage east run
]
cam_uid = next(e["uid"] for e in d["defs"]["entities"] if e["identifier"] == "CameraZone")
ents = LIS["Entities"]["entityInstances"]
for (c, s) in ZONES:
    ents.append(zone_entity(ground, c[0], c[1], s[0], s[1], cam_uid, lvl["worldX"], lvl["worldY"]))

# write back ground level
lvl["pxWid"], lvl["pxHei"] = NEW_CW * G, NEW_CH * G
for li in lvl["layerInstances"]:
    li["__cWid"], li["__cHei"] = NEW_CW, NEW_CH
    name = li["__identifier"]
    if name in ground.layers:
        tiles = sorted(ground.layers[name].values(), key=lambda t: t["d"][0])
        for t in tiles:  # recompute d for the new width
            cx, cy = t["px"][0] // G, t["px"][1] // G
            t["d"] = [cy * NEW_CW + cx]
        li["gridTiles"] = tiles
    elif name == "Collision":
        li["intGridCsv"] = csv
print(f"ground: +{len(new_floor)} floor cells, walls {sum(csv)}, zones {len(ents)}")

# ---------------- new levels ----------------
def make_level(ident, uid, world_x, world_y, rects, zones, torch_note):
    floor = set()
    for (rx, ry, rw, rh) in rects:
        for x in range(rx, rx + rw):
            for y in range(ry, ry + rh):
                floor.add((x, y))
    min_x = min(x for x, _ in floor); max_y = max(y for _, y in floor)
    min_y = min(y for _, y in floor); max_x = max(x for x, _ in floor)
    ox, k = 3 - min_x, max_y + 9           # 3 cells left pad, 9 rows above floor top
    cw, ch = (max_x - min_x) + 7, (k - min_y) + 5
    p = LevelPaint(ox, k, cw, ch)
    paint_areas(p, floor, floor)
    csv_ = collision_csv(p, floor)

    layer_is = []
    for ldef in d["defs"]["layers"]:
        li = {"__identifier": ldef["identifier"], "__type": ldef["type"],
              "__cWid": cw, "__cHei": ch, "__gridSize": G, "__opacity": 1,
              "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
              "__tilesetDefUid": ldef.get("tilesetDefUid"),
              "__tilesetRelPath": main_rel if ldef.get("tilesetDefUid") == main_uid else None,
              "iid": str(uuid.uuid4()), "levelId": uid, "layerDefUid": ldef["uid"],
              "pxOffsetX": 0, "pxOffsetY": 0, "visible": True,
              "optionalRules": [], "intGridCsv": [], "autoLayerTiles": [],
              "seed": uid * 7 + 1, "overrideTilesetUid": None,
              "gridTiles": [], "entityInstances": []}
        name = ldef["identifier"]
        if name == "Collision":
            li["__opacity"] = 0.35
            li["intGridCsv"] = csv_
        elif name == "Entities":
            li["entityInstances"] = [
                zone_entity(p, c[0], c[1], s[0], s[1], cam_uid, world_x, world_y)
                for (c, s) in zones]
        elif name in p.layers:
            li["gridTiles"] = sorted(p.layers[name].values(), key=lambda t: t["d"][0])
        layer_is.append(li)

    lv = copy.deepcopy({key: lvl[key] for key in lvl.keys()})
    lv.update({"identifier": ident, "uid": uid, "iid": str(uuid.uuid4()),
               "worldX": world_x, "worldY": world_y, "worldDepth": 0,
               "pxWid": cw * G, "pxHei": ch * G, "layerInstances": layer_is,
               "__neighbours": [], "externalRelPath": None})
    print(f"{ident}: {cw}x{ch} cells, {len(floor)} floor, walls {sum(csv_)} ({torch_note})")
    return lv

upper = make_level("UpperGallery", 503, 0, -560,
    rects=[(0, 0, 20, 4), (20, 0, 4, 6)],          # walkway + stair landing
    zones=[((10, 1.5), (26, 12)), ((21.5, 3), (9, 12))],
    torch_note="balcony over the hall; stairs land in the SE room")
under = make_level("Undercroft", 504, 0, 1064,
    rects=[(0, 0, 16, 10), (16, 2, 3, 2), (16, 6, 3, 2)],  # crypt + 2 east alcoves
    zones=[((8, 5), (20, 18)), ((16.5, 5), (11, 12))],
    torch_note="crypt + alcoves, unlit by design")
d["levels"] += [upper, under]
d["nextUid"] = 505

with open(SRC, "w", encoding="utf-8", newline="\n") as f:
    json.dump(d, f, indent=1)
print("saved; backup at", BAK)
