r"""Catacombs level generator for CastleWing.ldtk.

Adds (or replaces) a ~42x41 cell "Catacombs" level built from the Rogue Fantasy
Catacombs tilesheet, alongside the existing CastleWing level.

Design intent
-------------
A crypt carved out of solid rock: every non-floor cell is filled with rock, so
there are no black voids, and the rooms read as excavated rather than drawn.
The layout is a LOOP (stair -> nave -> north hall -> east loop -> back to nave),
never a dead-end tree, so the player always has somewhere to run during a chase.

Correctness rules (both enforced, then re-checked by validate_v2.py)
  * Collision = 100% coverage: EVERY one of the 8 neighbours of a floor cell
    that is not itself floor is marked solid. No gaps, ever.
  * No collision is ever placed ON a floor cell.
  * Connectivity is verified by 4-neighbour BFS from the player spawn: every
    floor cell must be reachable, or the script refuses to write.

This script is RE-RUNNABLE: it replaces any existing Catacombs level and
Catacombs tileset def rather than appending duplicates.

Run from the repo root:  python Tools\MapPipeline\mapv3_catacombs.py
"""
import collections
import json
import os
import shutil
import uuid

SRC = r"Assets\Resources\Assets\Maps\CastleWing.ldtk"
BAK = SRC + ".pre-catacombs.bak"
ROOMS_OUT = r"Assets\Resources\Assets\Maps\CatacombsRooms.json"
# LDtkToUnity requires an exported tileset definition file per tileset,
# in a folder named after the project. Emitted here so it cannot drift.
TILESET_OUT = r"Assets\Resources\Assets\Maps\CastleWing\Catacombs.ldtkt"

G = 16
LEVEL_ID = "Catacombs"
LEVEL_UID = 505
TILESET_UID = 506
TILESET_ID = "Catacombs"
TILESET_REL = "../../Outsource/RogueFantasyCatacombs/mainlevbuild.png"
TS_COLS, TS_ROWS = 64, 40          # 1024x640 at 16px
WORLD_X, WORLD_Y = 0, 1200         # clear of CastleWing (66x54 at 0,0)

# ---------------------------------------------------------------- tile picks
# Read off a coordinate-labelled grid overlay of the sheet, not guessed.
# Each entry is (col0, row0, cols, rows) and tiles seamlessly by modulo.
FLOOR = (19, 21, 4, 3)      # cobblestone / rubble
FLOOR_ALT = (19, 25, 4, 3)  # second cobble patch, mossier — breaks up repetition
ROCK = (46, 26, 4, 4)       # near-black solid rock mass
WALL = (17, 17, 5, 3)       # brick wall face; row 17 top .. row 19 bottom
WALL_DECO = (22, 17, 5, 3)  # same wall, with hanging chains / skulls
TOMB = (24, 21, 4, 3)       # sarcophagus niche, 3 rows tall (row 23 = base)

# How much of the map gets varied tiles, as a percentage of eligible cells.
FLOOR_ALT_PCT = 30
WALL_DECO_PCT = 22


def h(x, y, salt):
    """Deterministic hash — decoration must be identical on every regeneration."""
    return ((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791)) & 0x7FFFFFFF


def chance(x, y, pct, salt):
    return h(x, y, salt) % 100 < pct

# ------------------------------------------------------------------- layout
# Script space: x grows right, y grows UP. Rectangles are (x, y, w, h).
ROOMS = {
    "stair_hall":   (16,  1,  8,  5),   # player spawn - stairs down from the surface
    "nave_s":       (18,  6,  4,  6),
    "west_link":    (14,  7,  4,  3),
    "west_crypt":   (3,   5, 11,  9),
    "east_link":    (22,  7,  4,  3),
    "east_ossuary": (26,  5, 11,  9),
    "nave_m":       (18, 12,  4,  8),
    "cistern_link": (10, 15,  8,  3),
    "cistern":      (3,  14,  7,  7),
    "loop_link":    (22, 15,  7,  3),
    "loop_east":    (29, 14,  8, 10),
    "nave_n":       (18, 20,  4,  5),
    "north_hall":   (8,  25, 22,  8),   # exit gate sits on this room's north wall
    "loop_north":   (29, 24,  6,  4),
}

# CAMERA BOUNDS — one zone spanning the whole carved area.
#
# Do NOT go back to per-room zones sized to the room rectangle. CinemachineConfiner2D
# clamps the camera so the VIEWPORT fits inside the bounding shape; the viewport is
# ~15x7 world units, so an 8x5 room zone cannot contain it. The confiner then snaps
# the camera to whichever zone is big enough, and the player walks around off-screen.
# (That is exactly what happened: spawn in stair_hall, camera parked 25 units north
# in north_hall, the only zone larger than the view.)
#
# A single map-wide zone is always safe because the map (34x32) is far larger than
# any viewport. Per-room "camera rooms" are possible later, but every zone must then
# be inflated well beyond the largest supported viewport, not sized to the room.
ZONE_MARGIN = 2

PAD_L, PAD_R, PAD_B, PAD_T = 4, 4, 4, 5   # rock margin around the carved space


def mod(a, m):
    return ((a % m) + m) % m


def cells_of(rect):
    x, y, w, h = rect
    return {(x + i, y + j) for i in range(w) for j in range(h)}


class Level:
    """Paints tiles in script space and converts to LDtk cell space."""

    LAYERS = ("Floor", "WallFace", "Overhead", "Rug", "DecorMain", "DecorDeco")

    def __init__(self, ox, k, cw, ch):
        self.ox, self.k, self.cw, self.ch = ox, k, cw, ch
        self.layers = {n: {} for n in self.LAYERS}

    def cell(self, x, y):
        return (x + self.ox, self.k - y)

    def in_bounds(self, x, y):
        cx, cy = self.cell(x, y)
        return 0 <= cx < self.cw and 0 <= cy < self.ch

    def put(self, layer, col, row_top, x, y):
        cx, cy = self.cell(x, y)
        if not (0 <= cx < self.cw and 0 <= cy < self.ch):
            return
        self.layers[layer][(cx, cy)] = {
            "px": [cx * G, cy * G], "src": [col * G, row_top * G], "f": 0,
            "t": row_top * TS_COLS + col, "d": [cy * self.cw + cx], "a": 1,
        }

    def patch(self, layer, pick, x, y):
        c0, r0, w, h = pick
        self.put(layer, c0 + mod(x, w), r0 + mod(y, h), x, y)


def build():
    floor = set()
    for rect in ROOMS.values():
        floor |= cells_of(rect)

    min_x, max_x = min(x for x, _ in floor), max(x for x, _ in floor)
    min_y, max_y = min(y for _, y in floor), max(y for _, y in floor)
    ox = PAD_L - min_x
    k = max_y + PAD_T
    cw = (max_x - min_x + 1) + PAD_L + PAD_R
    ch = (max_y - min_y + 1) + PAD_B + PAD_T
    p = Level(ox, k, cw, ch)
    print(f"level: {cw}x{ch} cells, {len(floor)} floor cells")

    # --- collision: 100% coverage of the floor's 8-neighbourhood -----------
    csv = [0] * (cw * ch)
    for (x, y) in floor:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (x + dx, y + dy)
                if n in floor:
                    continue
                cx, cy = p.cell(*n)
                if 0 <= cx < cw and 0 <= cy < ch:
                    csv[cy * cw + cx] = 1
    for (x, y) in floor:                      # nothing solid may sit on floor
        cx, cy = p.cell(x, y)
        assert csv[cy * cw + cx] == 0, f"collision on floor at {(x, y)}"

    # --- connectivity: every floor cell reachable from the spawn -----------
    sx, sy, sw, sh = ROOMS["stair_hall"]
    seed = (sx + sw // 2, sy + sh // 2)
    assert seed in floor, "spawn seed is not on floor"
    seen, q = {seed}, collections.deque([seed])
    while q:
        cx, cy = q.popleft()
        for n in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
            if n in floor and n not in seen:
                seen.add(n)
                q.append(n)
    unreachable = floor - seen
    assert not unreachable, (
        f"{len(unreachable)} floor cells unreachable from spawn, "
        f"e.g. {sorted(unreachable)[:5]}")
    print(f"connectivity: all {len(floor)} floor cells reachable from {seed}")

    # --- paint -------------------------------------------------------------
    # INVARIANT: the Floor layer holds ONLY walkable cells. The whole pipeline
    # (validate_v2.py, the gap/connectivity audit, the docs) treats Floor-layer
    # tiles as the definition of walkable ground, so the background rock mass
    # goes on Rug instead. Rug sorts at -15: above Floor (-20), below WallFace
    # (-10), and it never overlaps cobble, so the render is identical.
    for cy in range(ch):
        for cx in range(cw):
            x, y = cx - ox, k - cy
            if (x, y) not in floor:
                p.patch("Rug", ROCK, x, y)
    # Two cobble patches mixed by a stable hash, so the 4x3 tile does not read
    # as an obvious repeating grid across a 709-cell floor.
    for (x, y) in floor:
        p.patch("Floor", FLOOR_ALT if chance(x, y, FLOOR_ALT_PCT, 1) else FLOOR, x, y)

    # Brick face on the vertical surface where rock rises north of a floor cell.
    # A fraction of columns use the decorated variant (chains, skulls) so long
    # walls have something to look at.
    for (x, y) in floor:
        if (x, y + 1) in floor:
            continue
        pick = WALL_DECO if chance(x, y, WALL_DECO_PCT, 2) else WALL
        c0, r0, wcols, _ = pick
        for i in range(3):                       # bottom -> top: rows 19,18,17
            ny = y + 1 + i
            if (x, ny) in floor:
                break
            p.put("WallFace", c0 + mod(x, wcols), r0 + (2 - i), x, ny)

    # One-cell rock band south of each room occludes the player for depth.
    for (x, y) in floor:
        if (x, y - 1) not in floor:
            p.patch("Overhead", ROCK, x, y - 1)

    # Sarcophagus niches set into the walls of the burial chambers. Each is 3
    # rows tall (sheet row 23 = base on the floor cell, 22 and 21 rising up the
    # wall) and only goes where there is genuinely 3 cells of rock behind it, so
    # a tomb can never be carved into a doorway.
    decor_cells = []
    tc0, tr0, tcols, _ = TOMB
    for room in ("west_crypt", "east_ossuary", "north_hall", "loop_east", "cistern"):
        rx, ry, rw, rh = ROOMS[room]
        placed, last_x = 0, -99
        for x in range(rx, rx + rw):                  # scan every column...
            if x - last_x < 2:                        # ...but leave a gap between tombs
                continue
            for y in range(ry + rh - 1, ry - 1, -1):  # topmost valid row wins
                if (x, y) not in floor:
                    continue
                if any((x + dx, y + 1) in floor for dx in (-1, 0, 1)):
                    continue                          # doorway or open edge above
                for i in range(3):                    # base -> top: rows 23,22,21
                    p.put("DecorMain", tc0 + mod(x, tcols), tr0 + (2 - i), x, y + i)
                decor_cells.append((x, y))
                placed += 1
                last_x = x
                break
        print(f"  tombs in {room}: {placed}")

    return p, floor, csv, cw, ch, ox, k, decor_cells


def zone_entity(p, rect, cam_uid):
    x, y, w, h = rect
    left = (x + p.ox) * G
    top = (p.k - (y + h - 1)) * G
    return {"__identifier": "CameraZone", "__grid": [left // G, top // G],
            "__pivot": [0, 0], "__tags": ["camera"], "__tile": None,
            "__smartColor": "#FFCC00",
            "__worldX": WORLD_X + left, "__worldY": WORLD_Y + top,
            "iid": str(uuid.uuid4()), "width": w * G, "height": h * G,
            "defUid": cam_uid, "px": [left, top], "fieldInstances": []}


def main():
    if not os.path.exists(SRC):
        raise SystemExit(f"not found: {SRC} (run from the repo root)")
    d = json.load(open(SRC, encoding="utf-8"))
    if not os.path.exists(BAK):
        shutil.copyfile(SRC, BAK)
        print("backup written:", BAK)

    p, floor, csv, cw, ch, ox, k, decor_cells = build()

    # --- tileset def (replace if already present) --------------------------
    tileset_def = {
        "__cWid": TS_COLS, "__cHei": TS_ROWS, "identifier": TILESET_ID,
        "uid": TILESET_UID, "relPath": TILESET_REL, "embedAtlas": None,
        "pxWid": TS_COLS * G, "pxHei": TS_ROWS * G, "tileGridSize": G,
        "spacing": 0, "padding": 0, "tags": [], "tagsSourceEnumUid": None,
        "enumTags": [], "customData": [], "savedSelections": [],
        "cachedPixelData": {"opaqueTiles": "", "averageColors": ""},
    }
    d["defs"]["tilesets"] = [t for t in d["defs"]["tilesets"]
                             if t["identifier"] != TILESET_ID]
    d["defs"]["tilesets"].append(tileset_def)

    # Exported tileset file the Unity importer resolves by identifier.
    os.makedirs(os.path.dirname(TILESET_OUT), exist_ok=True)
    with open(TILESET_OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"Rects": None, "Def": tileset_def}, f, indent=1,
                  sort_keys=True)
    print("tileset file:", TILESET_OUT)

    cam_uid = next(e["uid"] for e in d["defs"]["entities"]
                   if e["identifier"] == "CameraZone")
    template = d["levels"][0]

    # Single camera zone spanning the carved area plus a margin (see ZONE_MARGIN).
    fx0 = min(x for x, _ in floor) - ZONE_MARGIN
    fy0 = min(y for _, y in floor) - ZONE_MARGIN
    fx1 = max(x for x, _ in floor) + ZONE_MARGIN
    fy1 = max(y for _, y in floor) + ZONE_MARGIN
    camera_rect = (fx0, fy0, fx1 - fx0 + 1, fy1 - fy0 + 1)
    print(f"camera zone: {camera_rect[2]}x{camera_rect[3]} cells covering the whole map")

    layer_is = []
    for ldef in d["defs"]["layers"]:
        name = ldef["identifier"]
        is_tiles = ldef["type"] == "Tiles"
        li = {"__identifier": name, "__type": ldef["type"],
              "__cWid": cw, "__cHei": ch, "__gridSize": G, "__opacity": 1,
              "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
              "__tilesetDefUid": TILESET_UID if is_tiles else None,
              "__tilesetRelPath": TILESET_REL if is_tiles else None,
              "iid": str(uuid.uuid4()), "levelId": LEVEL_UID,
              "layerDefUid": ldef["uid"], "pxOffsetX": 0, "pxOffsetY": 0,
              "visible": True, "optionalRules": [], "intGridCsv": [],
              "autoLayerTiles": [], "seed": LEVEL_UID * 7 + 1,
              "overrideTilesetUid": TILESET_UID if is_tiles else None,
              "gridTiles": [], "entityInstances": []}
        if name == "Collision":
            li["__opacity"] = 0.35
            li["intGridCsv"] = csv
        elif name == "Entities":
            li["entityInstances"] = [zone_entity(p, camera_rect, cam_uid)]
        elif name in p.layers:
            li["gridTiles"] = sorted(p.layers[name].values(),
                                     key=lambda t: t["d"][0])
        layer_is.append(li)

    level = {key: template[key] for key in template}
    level.update({"identifier": LEVEL_ID, "uid": LEVEL_UID,
                  "iid": str(uuid.uuid4()), "worldX": WORLD_X,
                  "worldY": WORLD_Y, "worldDepth": 0,
                  "pxWid": cw * G, "pxHei": ch * G,
                  "layerInstances": layer_is, "__neighbours": [],
                  "externalRelPath": None})

    d["levels"] = [l for l in d["levels"] if l["identifier"] != LEVEL_ID]
    d["levels"].append(level)
    d["nextUid"] = max(d["nextUid"], TILESET_UID + 1)

    with open(SRC, "w", encoding="utf-8", newline="\n") as f:
        json.dump(d, f, indent=1)

    tiles = {n: len(v) for n, v in p.layers.items() if v}
    print("tiles painted:", tiles)
    print(f"collision cells: {sum(csv)} | camera zones: 1 (map-wide)")
    print("saved:", SRC)

    # --- export room rects so Unity places gameplay from real data ---------
    # World rects assume the scene builder aligns the Floor tilemap's min cell
    # to world (0,0) -- Setup/30 does exactly that. Exporting world space here
    # means the C# side never re-derives the cell<->world mapping, which is
    # where off-by-one placement bugs come from.
    min_x = min(x for x, _ in floor)
    min_y = min(y for _, y in floor)

    def to_world(rect):
        rx, ry, rw, rh = rect
        return [rx - min_x, ry - min_y, rw, rh]

    rooms_json = {
        "level": LEVEL_ID,
        "cellWidth": cw, "cellHeight": ch,
        "originX": ox, "originY": k,   # cx = x + originX, cy = originY - y
        "floorMinX": min_x, "floorMinY": min_y,
        "worldX": WORLD_X, "worldY": WORLD_Y,
        "spawn": to_world(ROOMS["stair_hall"]),
        "rooms": {n: list(r) for n, r in ROOMS.items()},
        "worldRooms": {n: to_world(r) for n, r in ROOMS.items()},
        # Flat, JsonUtility-friendly mirrors: Unity's built-in JSON cannot parse
        # dictionaries or jagged arrays, and adding a JSON dependency for this
        # would be silly. Same data, shapes C# can actually read.
        "roomList": [dict(zip(("name", "x", "y", "w", "h"),
                              (n,) + tuple(to_world(r))))
                     for n, r in sorted(ROOMS.items())],
        "decorX": [x - min_x for (x, y) in sorted(decor_cells)],
        "decorY": [y - min_y for (x, y) in sorted(decor_cells)],
        "floorX": [x - min_x for (x, y) in sorted(floor)],
        "floorY": [y - min_y for (x, y) in sorted(floor)],
    }
    with open(ROOMS_OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(rooms_json, f, indent=1)
    print("room data:", ROOMS_OUT)


if __name__ == "__main__":
    main()
