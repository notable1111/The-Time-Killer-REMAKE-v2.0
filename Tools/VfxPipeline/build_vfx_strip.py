"""Stitch PixelLab VFX frames into a horizontal strip for Setup/38.

The VFX counterpart of Tools/CharArt/build_sheets.py. PixelLab returns one PNG
per animation frame; Unity's importer wants a single texture that is one row of
square cells (VfxSheetSetup reads the cell size straight off texture.height).

Frames are centred inside the cell rather than corner-pasted, because a VFX
burst is centre-pivoted -- an off-centre paste would make the impact drift
across the screen as it plays.

Usage:
    python build_vfx_strip.py <out.png> <frame0> <frame1> ...
    python build_vfx_strip.py <out.png> --urls <url0> <url1> ...

Frames may be local paths or https URLs (PixelLab's no-auth download links).
"""
import sys
import urllib.request
from io import BytesIO
from pathlib import Path

from PIL import Image


def load(source):
    if source.startswith("http://") or source.startswith("https://"):
        with urllib.request.urlopen(source) as response:
            return Image.open(BytesIO(response.read())).convert("RGBA")
    return Image.open(source).convert("RGBA")


def build_strip(frames, out_path):
    images = [load(f) for f in frames]
    if not images:
        raise SystemExit("no frames given")

    # Square cells sized to the largest frame, so a stray odd-sized frame can
    # never shift the grid Unity slices on.
    cell = max(max(im.width, im.height) for im in images)
    strip = Image.new("RGBA", (cell * len(images), cell), (0, 0, 0, 0))
    for i, im in enumerate(images):
        strip.paste(im, (i * cell + (cell - im.width) // 2,
                         (cell - im.height) // 2))

    out = Path(out_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    strip.save(out)
    print(f"{out}  {len(images)} frames  {cell}x{cell} cells  {strip.width}x{strip.height}")
    return out


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        raise SystemExit(__doc__)
    out_path, rest = args[0], args[1:]
    if rest and rest[0] == "--urls":
        rest = rest[1:]
    build_strip(rest, out_path)


if __name__ == "__main__":
    main()
