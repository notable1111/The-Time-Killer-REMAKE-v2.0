"""Turn a VFX strip into something a human can JUDGE in two seconds.

WHY THIS EXISTS. The user, 2026-08-28: "if you do some design show the picture or
if you add effects play record and show that part... it will make my testing
faster". Every effect delivered before this ended with "play it and look", which
puts the slowest, most expensive step - launching the game and reproducing the
trigger - on the person with the least time. A strip of 64px cells is unreadable
as a PNG; the same strip animated over the real background colour is obvious.

It also enforces the project's first principle by default. The blood VFX were
tuned against a neutral backdrop and measured 0.26% coverage on the real floor -
invisible. This tool takes the background colour as an argument and defaults to
BLACK-ish rather than white, so a preview can never flatter an effect the way a
checkerboard or a white page would.

Two outputs, because they answer different questions:
  * <name>_sheet.png  - a contact sheet, every frame numbered. Answers "what is
    the SHAPE, and is any frame dead?" A blank frame is instantly visible here
    and invisible in motion.
  * <name>_preview.gif - the strip animated at its real fps, looped. Answers
    "does it read, and does it feel right?" - which no still image can.

This previews the ART over a flat colour. It does NOT replace measuring in the
real scene: it has no lights, no props and no camera. Use it to iterate quickly,
then confirm the survivor in-engine. When the two disagree, the engine wins.

Usage:
  python preview_effect.py Assets/.../gate_opened.png --bg 15,16,15 --fps 11
  python preview_effect.py <strip> --scale 5 --out <dir>
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageDraw


def load_frames(path):
    """A strip is horizontal, square cells, cell size == image height."""
    im = Image.open(path).convert("RGBA")
    cell = im.size[1]
    if im.size[0] % cell != 0:
        print("WARNING: width %d is not a whole number of %dpx cells" % (im.size[0], cell))
    n = im.size[0] // cell
    return [im.crop((i * cell, 0, (i + 1) * cell, cell)) for i in range(n)], cell


def over(frame, bg):
    plate = Image.new("RGBA", frame.size, bg + (255,))
    return Image.alpha_composite(plate, frame)


def coverage(frame):
    """Share of the cell carrying visible ink - the cheap 'is this frame dead?' check."""
    data = list(frame.getdata())
    return 100.0 * sum(1 for p in data if p[3] > 24) / len(data)


def contact_sheet(frames, bg, scale, cols=6):
    n = len(frames)
    rows = (n + cols - 1) // cols
    cell = frames[0].size[0] * scale
    pad, label = 6, 14
    W = cols * (cell + pad) + pad
    H = rows * (cell + pad + label) + pad
    sheet = Image.new("RGB", (W, H), (26, 26, 30))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(frames):
        r, c = divmod(i, cols)
        x = pad + c * (cell + pad)
        y = pad + r * (cell + pad + label)
        img = over(f, bg).convert("RGB").resize((cell, cell), Image.NEAREST)
        sheet.paste(img, (x, y))
        cov = coverage(f)
        tag = "f%d  %.1f%%" % (i, cov)
        if cov < 0.5:
            tag += "  DEAD"
        d.text((x + 2, y + cell + 2), tag, fill=(150, 150, 160) if cov >= 0.5 else (210, 90, 90))
    return sheet


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("strip")
    ap.add_argument("--bg", default="15,16,15",
                    help="background the effect must read against, R,G,B. Default is the "
                         "castle's dark stone - NEVER preview on white.")
    ap.add_argument("--fps", type=float, default=12.0)
    ap.add_argument("--scale", type=int, default=4)
    ap.add_argument("--out", default=None)
    a = ap.parse_args()

    bg = tuple(int(v) for v in a.bg.split(","))
    frames, cell = load_frames(a.strip)
    name = Path(a.strip).stem
    out = Path(a.out) if a.out else Path(a.strip).parent
    out.mkdir(parents=True, exist_ok=True)

    sheet = contact_sheet(frames, bg, a.scale)
    sheet_path = out / (name + "_sheet.png")
    sheet.save(sheet_path)

    big = [over(f, bg).convert("P", palette=Image.ADAPTIVE, colors=128)
           .resize((cell * a.scale, cell * a.scale), Image.NEAREST) for f in frames]
    gif_path = out / (name + "_preview.gif")
    big[0].save(gif_path, save_all=True, append_images=big[1:],
                duration=int(1000.0 / a.fps), loop=0, disposal=2)

    dead = [i for i, f in enumerate(frames) if coverage(f) < 0.5]
    print("%s: %d frames of %dpx, %.2fs at %.1ffps" % (name, len(frames), cell, len(frames) / a.fps, a.fps))
    print("  background %s   peak frame coverage %.1f%%" % (bg, max(coverage(f) for f in frames)))
    if dead:
        print("  DEAD FRAMES (under 0.5%% ink): %s" % dead)
    print("  %s" % sheet_path)
    print("  %s" % gif_path)


if __name__ == "__main__":
    main()
