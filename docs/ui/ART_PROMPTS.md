# UI art prompts (Phase 9 UI pass)

Three sprite sheets, generated outside Unity and sliced in Unity (Sprite Editor → Slice → Grid By Cell Count).
Splitting them up gives better results than one huge sheet: image AIs keep a grid much better with fewer, similar items.

Save the results as PNG in `Assets/_Project/Art/UI/` with these names:
`ui_frames.png`, `ui_icons.png`, `ui_minerals.png`.

If the AI can't make a transparent background, use flat pure magenta (#FF00FF) and keep it there; I'll key it out.
If it can't make the exact pixel size, keep the **grid layout and aspect ratio** and say what size you got.

**What we got (first round):** the icons and minerals followed the grid (minerals came on a white background, keyed out
by the slicer). The frames sheet did not follow the 4×4 grid and has no gold toast pill; it's cut by finding separate
blobs, named in reading order by `FRAME_NAMES` in `tools/ui/slice_ui.py`. If you regenerate it, update that list.
Source sheets are kept in `art/ui-source/`.

---

## Sheet 1 — Frames and buttons (`ui_frames.png`)

```
A game UI sprite sheet for a sci-fi space-mining mobile game with a low-poly, faceted art style.
Image size 2048×2048 pixels, transparent background, laid out as a strict 4×4 grid of equal 512×512 cells.
Each cell holds exactly one element, centred, with at least 48 px of empty space around it. Nothing crosses
a cell border. No text, no letters, no numbers, no logos, no icons inside the elements.

Style: dark, sleek sci-fi interface. Deep navy/slate panels (#121826 to #1C2438), slightly translucent look,
subtle faceted low-poly bevels, 45° chamfered (cut) corners, thin crisp light-cyan edge lines (#4FD8FF).
Flat front view, no perspective, no drop shadows outside the shape, no outer glow, no background scenery.

Every frame must be 9-slice friendly: all decoration lives in the four corners and along the edges,
corners fit within 64 px, straight edges are perfectly uniform along their whole length, and the centre
is a flat, even fill with no pattern or gradient.

Row 1:
1. Large dialog panel, nearly square, dark navy fill, cyan edge, chamfered corners.
2. List row / card, wide rectangle (about 4:1), slightly lighter navy, thinner edge.
3. Title plate / header bar, wide rectangle (about 6:1), cyan-tinted fill with a brighter top edge.
4. Toast / notification pill, wide rounded-chamfer capsule (about 5:1), dark fill, gold edge (#FFC94A).

Row 2 (buttons, all the same shape: wide rectangle about 3:1, chamfered corners, gentle top highlight):
5. Primary button, cyan (#2FB8E6).
6. Confirm / sell button, green (#2FBF71).
7. Highlight button, gold (#E8A92E) for "ready" actions.
8. Secondary button, muted slate blue (#3A4560).

Row 3:
9. Disabled button, same shape, flat grey (#40444C), no highlight.
10. Pressed button, cyan, darker and with the highlight at the bottom instead of the top.
11. Square icon button, 1:1, slate fill with cyan edge.
12. Square icon button selected state, 1:1, cyan fill, brighter edge.

Row 4:
13. Bottom tab bar background, very wide strip (about 8:1), dark navy, cyan line along the top edge only.
14. Tab button selected, about 1:1, cyan-tinted fill with a bright cyan bar along its top edge.
15. Progress bar track, very wide thin bar (about 10:1), dark inset fill with a thin edge.
16. Progress bar fill, same shape and size as 15, solid light-cyan fill, no edge, slight top highlight.
```

## Sheet 2 — Icons (`ui_icons.png`)

```
A game icon sprite sheet for a sci-fi space-mining mobile game with a low-poly, faceted art style.
Image size 2048×1024 pixels, transparent background, laid out as a strict 8 columns × 4 rows grid of equal
256×256 cells. Each cell holds exactly one icon, centred, filling about 70% of the cell, with clear empty space
around it. Nothing crosses a cell border. No text, no letters, no numbers, no frames or backgrounds behind icons.

Style: bold, simple, readable at 64 px. White icons (#FFFFFF) with light grey facet shading (#C8D0DC) to give
a low-poly, cut-gem look, and a thin dark outline (#0B0F18). Monochrome: no other colours (they get tinted
in the game). Flat front view, consistent line weight and visual size across all icons.

Row 1: 1 credits coin (hexagonal coin), 2 idle income (clock with a small lightning bolt), 3 cargo crate,
4 sell (coin with an up arrow), 5 auto-sell (coin with a circular arrow), 6 chevron pointing up,
7 chevron pointing down, 8 star map (planet with an orbit ring).

Row 2: 9 codex (open book with a gem), 10 refinery (furnace / crucible with a flame), 11 upgrades (up arrow
inside a gear), 12 close (X), 13 padlock, 14 check mark, 15 trophy, 16 jump drive (rocket with speed lines).

Row 3 (upgrades): 17 mining drill bit, 18 critical hit (target with a spark), 19 auto-fire (crosshair with
three arrows), 20 cargo hold expansion (crate with a plus), 21 collector (magnet), 22 drone count (small
drone with a plus), 23 drone damage (drone with a lightning bolt), 24 drone targeting (drone with a crosshair).

Row 4: 25 scanner (radar sweep), 26 refinery batch (stacked ingots), 27 refinery speed (flame with speed
lines), 28 stabiliser (shield with a gem), 29 refine success (gem with a sparkle), 30 warning (triangle with
an exclamation mark), 31 settings gear, 32 info (circle with an "i" shape).
```

## Sheet 3 — Minerals (`ui_minerals.png`)

```
A sprite sheet of mineral chunk icons for a sci-fi space-mining mobile game with a low-poly, faceted art style.
Image size 1024×512 pixels, transparent background, laid out as a strict 4 columns × 2 rows grid of equal
256×256 cells. Each cell holds exactly one chunk, centred, filling about 70% of the cell. Nothing crosses a cell
border. No text, no frames, no backgrounds.

Style: chunky low-poly faceted rocks and crystals, few large flat facets, lit from the top left, thin dark
outline (#0B0F18), saturated but not neon. Same size and viewing angle for all of them.

Row 1: 1 rock (dull grey-brown stone), 2 copper (orange-copper metallic nuggets), 3 tin (pale blue-grey metal),
4 iron (dark rusty red-grey metal).
Row 2: 5 nickel (silvery green-tinted metal), 6 silver (bright polished silver), 7 quartz (clear pale pink-white
crystal cluster), 8 generic gem (white faceted gem, used for purity grades and tinted in the game).
```
