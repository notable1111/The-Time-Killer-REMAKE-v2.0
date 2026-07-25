"""
Turn a PixelLab character bundle into the horizontal sprite strips Unity slices.

PixelLab ships one PNG per frame per direction. Unity's Setup/33 importer expects
one strip per (animation, direction) with every frame in a row of fixed-width
cells -- the same layout the New_Leaf pack used, so the existing slicing code in
PlayerAnimationSetup carries over unchanged.

    python Tools/CharArt/build_sheets.py <bundle.zip|dir> <out_dir> [--prefix Survivor]

Direction names map 1:1 onto FacingDirection's declaration order, so the importer
can index by enum value without a lookup table.
"""
import argparse
import re
import shutil
import sys
import tempfile
import zipfile
from pathlib import Path

from PIL import Image

# PixelLab compass name -> FacingDirection member. Order matters: it is the
# enum's declaration order, which the Unity side indexes by (int)FacingDirection.
DIRECTIONS = [
    ("south", "Down"),
    ("south-west", "DownLeft"),
    ("west", "Left"),
    ("north-west", "UpLeft"),
    ("north", "Up"),
    ("north-east", "UpRight"),
    ("east", "Right"),
    ("south-east", "DownRight"),
]


def frame_index(path):
    """Sort frames numerically -- '10.png' must not sort before '2.png'."""
    digits = re.findall(r"\d+", path.stem)
    return int(digits[-1]) if digits else 0


def find_frames(root, animation, compass):
    """Locate a direction's frames, tolerating PixelLab's layout variations."""
    candidates = [
        root / "animations" / animation / compass,
        root / "animations" / animation,
        root / animation / compass,
    ]
    for folder in candidates:
        if not folder.is_dir():
            continue
        frames = sorted(folder.glob("*.png"), key=frame_index)
        if folder.name != compass:
            # Flat folder: frames are named "<compass>_0.png" or similar.
            frames = [f for f in frames if compass in f.stem]
        if frames:
            return frames
    return []


def build_strip(frames, out_path):
    """Compose frames left-to-right into one fixed-cell strip."""
    images = [Image.open(f).convert("RGBA") for f in frames]
    cell_w = max(im.width for im in images)
    cell_h = max(im.height for im in images)
    strip = Image.new("RGBA", (cell_w * len(images), cell_h), (0, 0, 0, 0))
    for i, im in enumerate(images):
        # Centre any odd-sized frame in its cell so the character never jitters.
        strip.paste(im, (i * cell_w + (cell_w - im.width) // 2,
                         (cell_h - im.height) // 2))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    strip.save(out_path)
    return cell_w, cell_h, len(images)


def resolve_root(source, workdir):
    """Accept either a bundle zip or an already-extracted folder."""
    if source.suffix == ".zip":
        with zipfile.ZipFile(source) as z:
            z.extractall(workdir)
        root = workdir
    else:
        root = source
    # The bundle nests everything under a single character-named folder.
    if not (root / "rotations").is_dir() and not (root / "animations").is_dir():
        subdirs = [d for d in root.iterdir() if d.is_dir()]
        if len(subdirs) == 1:
            root = subdirs[0]
    return root


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source", type=Path, help="PixelLab bundle .zip or extracted folder")
    ap.add_argument("out_dir", type=Path, help="where to write the strips")
    ap.add_argument("--prefix", default="Survivor")
    ap.add_argument("--animations", nargs="*", default=None,
                    help="animation folder names to convert (default: all found)")
    args = ap.parse_args()

    workdir = Path(tempfile.mkdtemp(prefix="pixellab_"))
    try:
        root = resolve_root(args.source, workdir)

        anim_root = root / "animations"
        if args.animations:
            animations = args.animations
        elif anim_root.is_dir():
            animations = sorted(d.name for d in anim_root.iterdir() if d.is_dir())
        else:
            animations = []

        if not animations:
            print("No animations found -- writing rotations as single-frame idles.")

        wrote = 0
        for animation in animations:
            for compass, facing in DIRECTIONS:
                frames = find_frames(root, animation, compass)
                if not frames:
                    print(f"  MISSING {animation}/{compass}", file=sys.stderr)
                    continue
                out = args.out_dir / f"{args.prefix}_{animation}_{facing}.png"
                w, h, n = build_strip(frames, out)
                print(f"  {out.name}: {n} frames, {w}x{h} cells")
                wrote += 1

        # Rotations always become a 1-frame Idle fallback so a missing idle
        # animation can never leave the player invisible.
        rot = root / "rotations"
        if rot.is_dir():
            for compass, facing in DIRECTIONS:
                src = rot / f"{compass}.png"
                if not src.exists():
                    continue
                out = args.out_dir / f"{args.prefix}_Rotation_{facing}.png"
                out.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(src, out)
                wrote += 1

        print(f"\nWrote {wrote} files to {args.out_dir}")
    finally:
        shutil.rmtree(workdir, ignore_errors=True)


if __name__ == "__main__":
    main()
