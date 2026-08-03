"""Generate a light-shaft texture — the cone of visible air under a torch.

Nothing in the Kenney pack is a shaft: window_* are pane cookies, light_* are
radial rings, trace_01 is a thin streak. A shaft is a gradient, not a drawing,
so it is generated rather than sourced.

Shape rules that make it read as light in air rather than as a white triangle:
  * WIDENS with distance from the source, like a real cone of lit dust.
  * Fades along its length — the far end dissolves instead of stopping.
  * Fades across its width with a soft edge, brightest along the centre line.
  * Slight vertical streaking, so it looks like dust catching light rather than
    a flat gradient. This is the detail that stops it looking like a decal.

Output is WHITE. Colour comes from the material tint in AmbienceSetup, the same
way the Kenney particles work, so one texture serves torchlight and moonlight.

Usage: python gen_light_shaft.py [--dry-run]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image

OUT = Path(r"D:\The Time Killer Remake\Assets\Resources\Assets\Effects\light_shaft.png")
W, H = 128, 256          # tall: the shaft runs down the image
TOP_WIDTH = 0.16         # share of width at the source
BOTTOM_WIDTH = 0.92      # share of width at the far end
EDGE_SOFTNESS = 0.55     # how much of the half-width is falloff
LENGTH_FADE = 1.7        # higher = dies off sooner along its length
STREAK_COUNT = 26
STREAK_DEPTH = 0.22


def main():
    dry = "--dry-run" in sys.argv
    rng = random.Random(7734)
    # Per-column streak weights, smoothed, so brightness varies across the beam.
    streaks = [1.0] * W
    for _ in range(STREAK_COUNT):
        centre = rng.uniform(0, W)
        width = rng.uniform(1.5, 6.0)
        depth = rng.uniform(0.0, STREAK_DEPTH)
        for x in range(W):
            d = abs(x - centre) / width
            if d < 3:
                streaks[x] -= depth * math.exp(-d * d)

    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()
    for y in range(H):
        t = y / (H - 1)                       # 0 at the source, 1 at the far end
        half = (TOP_WIDTH + (BOTTOM_WIDTH - TOP_WIDTH) * t) * W * 0.5
        # Along-length falloff: bright at the source, dissolving downward.
        length_a = math.exp(-LENGTH_FADE * t)
        for x in range(W):
            dx = abs(x - W * 0.5)
            if dx > half:
                continue
            # Across-width falloff, soft-shouldered.
            edge = dx / half
            across = 1.0 - edge ** 2
            if edge > (1.0 - EDGE_SOFTNESS):
                k = (edge - (1.0 - EDGE_SOFTNESS)) / max(1e-6, EDGE_SOFTNESS)
                across *= max(0.0, 1.0 - k) ** 1.5
            a = length_a * across * max(0.0, streaks[x])
            if a <= 0.002:
                continue
            px[x, y] = (255, 255, 255, min(255, int(a * 255)))

    bbox = im.split()[3].getbbox()
    ink = sum(1 for p in im.split()[3].get_flattened_data() if p > 0)
    print(f"shaft {W}x{H}  bbox={bbox}  ink {100*ink/(W*H):.0f}% of texture")
    if dry:
        print("(dry run, nothing written)")
        return
    OUT.parent.mkdir(parents=True, exist_ok=True)
    im.save(OUT)
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
