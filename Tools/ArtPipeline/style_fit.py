"""Score candidate art against THIS game's measured style, before integrating it.

WHY. 65 free Cartoon FX particle prefabs were already in the project and were
being used for the player's blood. They made the hit read as cheap, and the
reason was measurable: the game's own art sits at median value 52 and median
saturation 0.07 with hard pixel edges, while the imported effects were bright,
saturated and soft. Nobody compared them until after it shipped.

Downloading a free pack is easy. Downloading a free pack that does not clash is
the actual problem, and it is a measurement, not a matter of taste:

  VALUE        how light the art is (max channel, 0-255). The castle is dark.
  SATURATION   how colourful. The castle is nearly grey; bright effects shout.
  SOFTNESS     share of pixels with partial alpha. Pixel art is ~0; a soft
               airbrushed glow approaches 1. This is the single strongest
               predictor of "looks pasted in".
  COLOURS      distinct opaque colours per 1000 opaque pixels. Hand-limited
               palettes are low; photographic or gradient art is high.

Usage:
    python style_fit.py --reference            recompute the game's own profile
    python style_fit.py <folder> [<folder>...]  score candidate art
"""
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(r"D:\The Time Killer Remake")

# Folders that define what this game looks like. Deliberately the things the
# player stares at: the character, the furniture, the tilesets.
REFERENCE_DIRS = [
    ROOT / "Assets/Resources/Assets/Characters/Survivor",
    ROOT / "Assets/Resources/Assets/Hiding",
    ROOT / "Assets/Resources/Assets/Furniture",
]

MAX_IMAGES = 40          # enough for a stable median, fast enough to be instant
MAX_PIXELS = 200_000     # per image, sampled if larger


def measure(path):
    try:
        im = Image.open(path).convert("RGBA")
    except Exception:
        return None
    pixels = list(im.getdata())
    if len(pixels) > MAX_PIXELS:
        stride = len(pixels) // MAX_PIXELS + 1
        pixels = pixels[::stride]

    # ADDITIVE TEXTURES have no alpha channel at all: the shape lives in the
    # brightness over a black field, and the engine adds it to the frame. Judging
    # them by alpha counts the black background as art and reports a bright glow
    # as "value 3, saturation 0.00" — which is how a first version of this tool
    # passed the Cartoon FX pack that had already been proven to clash. When
    # alpha carries no information, luminance IS the alpha.
    additive = all(a == 255 for _, _, _, a in pixels[:2000])
    if additive:
        pixels = [(r, g, b, int(0.299 * r + 0.587 * g + 0.114 * b))
                  for r, g, b, _ in pixels]

    opaque, partial, values, sats, colours = 0, 0, [], [], set()
    for r, g, b, a in pixels:
        if a == 0:
            continue
        if 8 < a < 248:
            partial += 1
        if a < 128:
            continue
        opaque += 1
        hi, lo = max(r, g, b), min(r, g, b)
        values.append(hi)
        sats.append(0.0 if hi == 0 else (hi - lo) / hi)
        colours.add((r, g, b))

    if opaque < 20:
        return None
    values.sort(); sats.sort()
    return {
        "value": values[len(values) // 2],
        "saturation": sats[len(sats) // 2],
        "softness": partial / max(1, partial + opaque),
        "colours": 1000.0 * len(colours) / opaque,
    }


def profile(paths, label):
    stats = [m for m in (measure(p) for p in paths) if m]
    if not stats:
        print(f"  {label}: no readable images")
        return None
    out = {}
    for key in ("value", "saturation", "softness", "colours"):
        column = sorted(s[key] for s in stats)
        out[key] = column[len(column) // 2]
    out["n"] = len(stats)
    return out


def images_in(folder, limit=MAX_IMAGES):
    found = []
    for path in sorted(Path(folder).rglob("*")):
        if path.suffix.lower() in (".png", ".jpg", ".jpeg", ".tga"):
            found.append(path)
            if len(found) >= limit:
                break
    return found


def reference_profile():
    paths = []
    for d in REFERENCE_DIRS:
        if d.exists():
            paths += images_in(d, MAX_IMAGES // max(1, len(REFERENCE_DIRS)))
    return profile(paths, "reference")


def verdict(candidate, reference):
    """Plain-language read. Thresholds are stated so they can be argued with."""
    notes = []
    if candidate["softness"] > reference["softness"] + 0.25:
        notes.append("SOFT EDGES — will look airbrushed against hard pixel art")
    if candidate["saturation"] > reference["saturation"] + 0.30:
        notes.append("TOO SATURATED — will shout against a near-grey castle")
    if candidate["value"] > reference["value"] + 70:
        notes.append("TOO BRIGHT — will glow against dark scenery")
    if candidate["colours"] > reference["colours"] * 3:
        notes.append("PALETTE TOO WIDE — gradients where the game uses steps")
    return notes


def show(label, p):
    print(f"  {label:<34} value {p['value']:3.0f}   saturation {p['saturation']:.2f}   "
          f"softness {p['softness']:.2f}   colours/1k {p['colours']:6.1f}   (n={p['n']})")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    reference = reference_profile()
    if reference is None:
        raise SystemExit("could not build a reference profile — check REFERENCE_DIRS")

    print("THIS GAME'S MEASURED STYLE")
    show("reference (character/props/tiles)", reference)

    if not args:
        print("\nPass one or more folders to score candidate art against it.")
        return

    print("\nCANDIDATES")
    for folder in args:
        p = profile(images_in(folder), folder)
        if p is None:
            print(f"  {folder}: no readable images")
            continue
        show(Path(folder).name, p)
        notes = verdict(p, reference)
        if notes:
            for n in notes:
                print(f"      ! {n}")
        else:
            print("      OK — within range of the game's own art on every axis")


if __name__ == "__main__":
    main()
