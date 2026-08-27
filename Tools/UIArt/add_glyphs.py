"""Add the glyphs PixelLab's fixed reference layout omits (0-9, /, the middle dot,
the em dash, the ellipsis and the percent sign) to the TimeKiller fonts, drawn in
each weight's own pixel skeleton.

The fonts are pixel fonts: every glyph is a set of axis-aligned rectangles on an
exact grid (Display 32 units/px with rows doubled, Body 64 units/px). So a new
glyph is authored as ASCII art here and emitted as one rectangle per horizontal
run -- identical in structure to the glyphs that shipped.

Every glyph below is placed against a MEASURED shipped glyph rather than an eyeball
-- read the outlines out of the TTF before adding another:

    Display (32 u/px, art doubled)   Body (64 u/px)
    hyphen  18x5 px, y 224..384      hyphen  6x2 px, y 256..384
    period   6x6 px, y   0..192      period  2x2 px, y   0..128
    'A'     24 px wide, cap 22 px    'A'    10 px wide, cap 9 px

So the em dash sits in the hyphen's own band and the ellipsis on the period's, which
is the whole reason they look like they belong to the font instead of beside it.

WHY THIS MATTERS MORE THAN IT LOOKS: a character missing from the baked TMP atlas
does NOT render as a box. TMP silently substitutes LiberationSans, at roughly double
the advance width, and every "missing glyph" check still reports zero. The em dash in
RunEndScreen's prompt shipped that way and nobody saw it. After running this, verify
membership against the font asset's characterTable, never by looking at the text."""
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen

# --- Display Bold: 11 logical rows, each drawn twice, 6px stems -------------
DISPLAY = {
'0': """....############....
..################..
..####........####..
####............####
####............####
####............####
####............####
####............####
..####........####..
..################..
....############....""",
'1': """....######....
..########....
....######....
....######....
....######....
....######....
....######....
....######....
....######....
..##########..
..##########..""",
'2': """..##############....
################....
##..........######..
............######..
..........######....
......########......
....######..........
..######............
######..............
####################
####################""",
'3': """..################..
####################
............######..
..........########..
....############....
....############....
..........########..
............######..
######......######..
####################
..##############....""",
'4': """..........######....
........########....
......##########....
....######..####....
..######....####....
######......####....
####################
####################
............####....
............####....
............####....""",
'5': """####################
####################
######..............
######..............
##############......
################....
............######..
............######..
######......######..
..################..
....############....""",
'6': """....############....
..################..
..####........####..
####................
####................
##############......
################....
####........######..
####........######..
..################..
....############....""",
'7': """####################
####################
##..........######..
..........######....
........######......
........######......
......######........
......######........
....######..........
....######..........
....######..........""",
'8': """....############....
..################..
..####........####..
..####........####..
....############....
....############....
..####........####..
####............####
####............####
..################..
....############....""",
'9': """....############....
..################..
..####........####..
####............####
####............####
..################..
......##########....
............######..
####........######..
..################..
....############....""",
'/': """..........######
..........######
........######..
........######..
......######....
......######....
....######......
....######......
..######........
..######........
######..........""",
'·': """........
........
........
........
..####..
..####..
........
........
........
........
........""",
# Rows 5-6 put this in units 256..384. The hyphen measures 224..384, so this shares
# its top edge and centres within half a pixel -- the closest the doubled grid allows
# without going 6px tall and out-weighting the hyphen it belongs beside.
# 24 px wide against the hyphen's 18: one cap-width, which is as long as an em dash
# can go here. This font's hyphen is already 0.75 cap-widths, so the usual "twice the
# hyphen" rule would produce a rule, not a dash.
'—': """........................
........................
........................
........................
........................
########################
########################
........................
........................
........................
........................""",
# Three periods at the font's own rhythm: the period is 6 px on a 7 px advance, so
# dots land at x 0, 7, 14. Typing "..." and typing this produce the same picture,
# which is the point -- an ellipsis that measures differently reads as a typo.
'…': """....................
....................
....................
....................
....................
....................
....................
....................
######.######.######
######.######.######
######.######.######""",
# 26 px wide -- wider than the 20 px digits on purpose. The first attempt kept it at
# digit width, which forced 6x6 solid blocks, and rendered it read as a slash with two
# specks beside it ("0/." rather than "0%"). Rings need wall+counter+wall, so at a 4 px
# wall and a 2 px counter the ring is 10 px and two of them plus a slash do not fit in
# 20. Widening is what buys the counters, and the counters are what make it a percent.
'%': """##########..........######
##########........######..
####..####......######....
##########....######......
##########..######........
..........######..........
........######..##########
......######....##########
....######......####..####
..######........##########
######..........##########""",
}

# --- Body Regular: 9 rows, 2px stems ---------------------------------------
BODY = {
'0': """.#####.
##...##
##...##
##...##
##...##
##...##
##...##
##...##
.#####.""",
'1': """..##.
.###.
..##.
..##.
..##.
..##.
..##.
..##.
#####""",
'2': """.#####.
##...##
.....##
....##.
...##..
..##...
.##....
##.....
#######""",
'3': """######.
....##.
....##.
.#####.
....##.
.....##
.....##
##...##
.#####.""",
'4': """...###.
..####.
.##.##.
##..##.
##..##.
#######
#######
....##.
....##.""",
'5': """#######
##.....
##.....
######.
....##.
.....##
.....##
##...##
.#####.""",
'6': """..####.
.##..##
##.....
##.....
######.
##...##
##...##
##...##
.#####.""",
'7': """#######
#####.#
....##.
....##.
...##..
...##..
..##...
..##...
..##...""",
'8': """.#####.
##...##
##...##
.#####.
.#####.
##...##
##...##
##...##
.#####.""",
'9': """.#####.
##...##
##...##
##...##
.######
.....##
.....##
##...##
.#####.""",
'/': """....##
....##
...##.
...##.
..##..
..##..
.##...
.##...
##....""",
'·': """....
....
....
....
.##.
.##.
....
....
....""",
# Rows 3-4 are exactly the hyphen's band (y 256..384) -- here the grid lines up, so
# this is the same stroke the hyphen uses, just longer. 10 px against the hyphen's 6.
'—': """..........
..........
..........
##########
##########
..........
..........
..........
..........""",
# Period is 2 px on a 3 px advance, so the dots land at x 0, 3, 6 -- identical to
# typing three periods.
'…': """........
........
........
........
........
........
........
##.##.##
##.##.##""",
# The rings drop to 1 px walls -- lighter than this weight's 2 px stem, and that is
# the deliberate trade. Solid 2x2 blocks were tried first and at 16 px "60%" read as
# "60/.". A 3x3 ring with a 1 px counter is what small pixel faces actually use: the
# counter is worth more to recognition here than matching the stem width.
'%': """###....##
#.#...##.
###..##..
....##...
...##....
..##.....
.##...###
##....#.#
......###""",
}


def rows_of(art, double):
    """ASCII art (top row first) -> list of output rows, bottom row first."""
    lines = [l for l in art.split('\n') if l.strip('') != '']
    out = []
    for line in reversed(lines):          # reverse: font y grows upward
        out.append(line)
        if double:
            out.append(line)
    return out


def build(font, table, unit, double):
    glyf = font['glyf']
    hmtx = font['hmtx']
    order = list(font.getGlyphOrder())
    added = []

    for ch, art in table.items():
        rows = rows_of(art, double)
        width = max(len(r) for r in rows)
        pen = TTGlyphPen(None)
        for y, row in enumerate(rows):
            x = 0
            while x < len(row):
                if row[x] == '#':
                    x0 = x
                    while x < len(row) and row[x] == '#':
                        x += 1
                    # one rectangle per horizontal run, wound like the shipped glyphs
                    X0, X1 = x0 * unit, x * unit
                    Y0, Y1 = y * unit, (y + 1) * unit
                    pen.moveTo((X0, Y0))
                    pen.lineTo((X0, Y1))
                    pen.lineTo((X1, Y1))
                    pen.lineTo((X1, Y0))
                    pen.closePath()
                else:
                    x += 1
        name = 'uni%04X' % ord(ch)
        glyf[name] = pen.glyph()
        hmtx[name] = ((width + 1) * unit, 0)
        if name not in order:
            order.append(name)
        added.append((ch, name, width))

    font.setGlyphOrder(order)
    for sub in font['cmap'].tables:
        for ch, name, _ in added:
            sub.cmap[ord(ch)] = name
    return added


def main():
    for path, table, unit, double, out in [
        ('TimeKiller_Display.ttf', DISPLAY, 32, True,  'TimeKiller_Display.ttf'),
        ('TimeKiller_Body.ttf',    BODY,    64, False, 'TimeKiller_Body.ttf'),
    ]:
        f = TTFont(path)
        added = build(f, table, unit, double)
        f['maxp'].numGlyphs = len(f.getGlyphOrder())
        f.save('fixed_' + out)
        print('%s: added %d glyphs -> fixed_%s' % (path, len(added), out))


if __name__ == '__main__':
    main()
