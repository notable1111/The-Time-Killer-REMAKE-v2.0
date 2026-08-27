# HUD art set — "carved stone & brass" (2026-07-25)

Staged here, **outside `Assets/`**, so Unity does not import them while a bot
batch is running. Move into `Assets/Resources/Assets/UI/` when wiring starts.

Generated with PixelLab `create_ui_asset` (140 generations), all four jobs on
seed `7734` with the same palette hint — which is why they share one palette
instead of looking like four separate purchases.

| File | Size | Use | Import settings |
|---|---|---|---|
| `Gauge.png` | 614×142 | Skill-check gauge frame | sprite, point filter, no compression |
| `GaugeNeedle.png` | 24×62 | Sweeping marker, pivot centre | sprite, point filter |
| `KeyCap_E.png` | 113×115 | Interact prompt key cap | sprite, point filter |
| `CounterPlaque.png` | 431×130 | Clocks-fixed counter | sprite, point filter |
| `EndFrame.png` | 392×243 | Win/lose screen border | sprite, **9-slice** — border ≈ 60/60/58/48 (L/R/T/B) |
| `Bar.png` | 130×36 | Spare plaque, 3 identical ones exist | sprite, 9-slice for buttons later |

## The one number the code needs

`Gauge.png` is only a frame — the fill, the hit zone and the needle are drawn by
the HUD *inside* its channel. In **cropped** `Gauge.png` pixel coordinates:

```
channel: x 57..555, y 46..93      (499 x 48)
```

Marker position `t` (0..1) maps to `x = 57 + t * 498`. Draw order is
frame → progress fill → hit zone → needle; the frame's channel is opaque, so
anything drawn under it is invisible.

## Post-processing that was applied

- **Needle cut out of the gauge.** PixelLab baked a needle into the channel. It
  was flood-filled out by warm colour, interior holes re-filled from the source
  (otherwise the blade's light spine became a black gash), and the channel
  behind it healed by copying a clean slab from 150px to the left.
- **EndFrame** arrived on a sheet with four bonus plaque bars; the bar
  overlapping the hanging chains was erased before cropping. Its centre was
  already transparent — the white in the preview is just preview backing.
- **CounterPlaque** generated with a mauve face that clashed with the slate of
  the other pieces. Corrected by a targeted nudge (+14 green, scaled by how
  mauve each pixel is, and only where `b > g` so the brass is never touched).

## Fonts

| File | Grid | Use |
|---|---|---|
| `TimeKiller_Display.ttf` | 32 units/px, cap height 22px, 6px stems | Headlines: run-end headline, clock counter |
| `TimeKiller_Body.ttf` | 64 units/px, cap height 9px, 2px stems | Body: detail line, prompt labels, debug overlay |

Both are pixel fonts — in Unity set **Rendering Mode = Hinted Raster** and use
integer font sizes that are multiples of the native grid (Display 32/64/96,
Body 16/32/48) or the glyphs will blur.

Glyphs are deliberately **neutral pale**, not brass, because `RunEndScreen`
tints its headline green on a win and red on a loss. A baked-in colour would
fight that.

### The digits are hand-authored, and why

PixelLab's `create_font` restyles a **fixed bundled reference layout** that
contains only:

```
!"'()+,-.:;?ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz
```

No `0-9`, no `/`, no `·` — exactly the characters this HUD is made of
(`CLOCKS 2 / 3`, `survived 3:45`). Regenerating cannot fix this; the layout is
bundled, not prompted. So `add_glyphs.py` authors the missing glyphs as ASCII
art in each weight's own pixel skeleton and injects them as real outlines,
matched to the shipped glyphs' rectangle-per-run structure, stem width, cap
height and advance rule (`advance = (width + 1) * unit`).

**15 glyphs now: `0-9 / · — … %`** (added 2026-08-04: em dash, ellipsis, percent).
Each new one is placed against a *measured* shipped glyph, not an eyeball — the em
dash sits in the hyphen's own vertical band, the ellipsis on the period's, and its
dots land on the period's advance so `…` and `...` draw identically.

The percent was **redrawn once before it shipped**. The first version kept it at
digit width (20 px), which left no room for counters, so the rings became solid
blocks and `60%` rendered as `60/.` at 16 px. Widening to 26 px is what buys the
counters, and the counters are what make it read as a percent. Look at a render
before trusting a glyph — `add_glyphs.py`'s header lists the reference metrics.

Re-run it against freshly downloaded PixelLab TTFs if the fonts are ever
regenerated, then re-run **Setup/47** in Unity to rebake the SDF atlases (not
Setup/46 — 47 re-rasterises from the TTF at the on-grid point size, which is what
picks up new outlines). Known cosmetic gap: the authored glyphs lack the small
weathering chips the AI put in the letters, so they read very slightly cleaner at
large sizes.

### A missing glyph does not look missing

Measured 2026-08-04: a character absent from the baked atlas does **not** render as
a box. TMP substitutes `LiberationSans` at roughly double the advance width, and
`isVisible` stays true, so every missing-glyph check reports zero. The em dash in
`RunEndScreen`'s prompt shipped that way and nobody noticed the line was set in two
typefaces. **Verify against `characterTable`, never by looking at the text.**

## Title screen

`TitleBackground.png` — 534×300, exactly 16:9, point-filter upscale in Unity.

Generated with PixelLab `create_map_object` (side view, high detail). Asked for
a castle prop on a transparent background; it returned a **complete scene** —
sky, moon, cliff, dead trees, lit windows. Kept as-is.

It came back 4:3, so the sides were extended to 16:9 in code rather than
regenerated. The extension takes each row's edge colour, **smoothed vertically
over ±18 rows**, and continues it outward with a slight darkening falloff. The
vertical smoothing is the load-bearing part: without it the rock's edge colour
extends as a flat band with a hard horizontal top edge, which reads as an
obvious rectangular seam. Smoothed, the same fill reads as distant haze. Stars,
a low mist band and a vignette are added on top; the vignette exists so menu
text stays readable over the centre.

`_menu_mock.png` is the layout reference, not an asset — main menu plus the
level-pick state. Notes for whoever wires it:

- Buttons are `Bar.png` **9-sliced horizontally**: keep the 22px brass end caps,
  stretch only the slate middle. Never scale the whole bar — the caps distort.
- Two scrims make text legible over art: a top band (title) and a left band
  (buttons). Text also carries a 1px dark shadow; without it the tagline
  disappears into the castle towers.
- Level pick exists so **Catacombs is reachable** — it is finished and audited
  but currently needs the scene opened by hand in the editor.

## Status

**Wired.** Verified 2026-08-04 by resolving each sprite's GUID against the scene
files: all nine are referenced by `CastleWingLDtk` and/or `MainMenu`. The three
claims that used to sit here are all obsolete — `ObjectiveHUD` became uGUI+TMP on
2026-07-28, `RunEndScreen` uses `TMP_Text`, no scene references `LegacyRuntime`,
and `InteractPrompt.cs` exists.

Still open:

- **Catacombs was never TMP-migrated.** Its `RunEndScreen` still points at three
  legacy `UnityEngine.UI.Text` components while the fields are `TMP_Text`, so Unity
  nulls them at load and the end screen renders no text at all. `Setup/44` only ever
  ran on CastleWing. The YAML *looks* wired — the fileIDs are non-zero — so this
  does not show up in a scene scan.
- **SETTINGS and CREDITS** from the 2026-07-25 menu plan were never built. The level
  pick did ship, so Catacombs is reachable from the main menu.
- Unauthored glyphs, if UI copy ever needs them: `– ‘ ’ “ ” & @ ° ×`.
