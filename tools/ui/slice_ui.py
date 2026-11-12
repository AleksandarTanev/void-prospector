"""
Cuts the generated UI sprite sheets (art/ui-source/) into one PNG per element for Unity.

    python tools/ui/slice_ui.py art/ui-source Assets/_Project/Resources/UI

- ui_frames.png  : transparent; elements found as separate blobs, named in reading order (FRAME_NAMES).
- ui_icons.png   : transparent; strict 8x4 grid (ICON_NAMES).
- ui_minerals.png: white (or fake checker) background, keyed out by flood fill from the edges; 4x2 grid.

Each element is trimmed to its pixels and saved with a small transparent margin. Icons and minerals are
centred on a square canvas so they line up in the UI. 9-slice borders are set on import by
Assets/_Project/Scripts/Editor/UiSpriteImporter.cs.
Needs Pillow and numpy.
"""
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

# Reading order of ui_frames.png (rows top to bottom, left to right). The generator didn't follow the
# requested 4x4 grid, so these names follow what it actually drew.
FRAME_NAMES = [
    "panel", "row", "header",
    "btn_primary", "btn_confirm", "btn_gold",
    "btn_disabled", "btn_pressed", "btn_secondary",
    "card", "icon_btn", "icon_btn_selected",
    "tabbar", "tab_selected", "bar_track",
    "strip", "bar_fill",
]

ICON_NAMES = [
    "credits", "idle", "cargo", "sell", "autosell", "up", "down", "map",
    "codex", "refinery", "upgrades", "close", "lock", "check", "trophy", "jump",
    "drill", "crit", "autofire", "cargo_plus", "magnet", "drone_plus", "drone_damage", "drone_target",
    "scanner", "ingots", "refine_speed", "stabiliser", "refine_success", "warning", "settings", "info",
]

MINERAL_NAMES = ["rock", "copper", "tin", "iron", "nickel", "silver", "quartz", "gem"]

MARGIN = 4          # transparent pixels kept around every element
SQUARE_SIZE = {"icons": 192, "minerals": 192}


def trim(img: Image.Image, margin=MARGIN) -> Image.Image:
    a = np.array(img.getchannel("A"))
    ys, xs = np.nonzero(a > 8)
    if len(xs) == 0:
        raise ValueError("empty element")
    box = (max(xs.min() - margin, 0), max(ys.min() - margin, 0),
           min(xs.max() + 1 + margin, img.width), min(ys.max() + 1 + margin, img.height))
    return img.crop(box)


def to_square(img: Image.Image, size: int) -> Image.Image:
    """Fits the element into a size x size transparent canvas, centred, keeping its aspect ratio."""
    inner = size - 2 * MARGIN
    scale = min(inner / img.width, inner / img.height)
    w, h = max(1, round(img.width * scale)), max(1, round(img.height * scale))
    img = img.resize((w, h), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(img, ((size - w) // 2, (size - h) // 2), img)
    return out


def blobs(alpha: np.ndarray, threshold=64, min_pixels=400):
    """Bounding boxes of connected opaque regions (4-connected), sorted into reading order."""
    mask = alpha > threshold
    seen = np.zeros_like(mask)
    h, w = mask.shape
    boxes = []
    for y0 in range(0, h, 2):
        row = mask[y0] & ~seen[y0]
        for x0 in np.nonzero(row)[0]:
            if seen[y0, x0]:
                continue
            q = deque([(y0, x0)])
            seen[y0, x0] = True
            x1 = x2 = x0
            y1 = y2 = y0
            n = 0
            while q:
                y, x = q.popleft()
                n += 1
                x1, x2, y1, y2 = min(x1, x), max(x2, x), min(y1, y), max(y2, y)
                for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            if n >= min_pixels:
                boxes.append((x1, y1, x2 + 1, y2 + 1))
    # Reading order: group into rows by vertical centre, then left to right.
    boxes.sort(key=lambda b: (b[1] + b[3]) / 2)
    rows, current = [], []
    for b in boxes:
        if current and (b[1] + b[3]) / 2 - (current[-1][1] + current[-1][3]) / 2 > 30:
            rows.append(current)
            current = []
        current.append(b)
    if current:
        rows.append(current)
    return [b for r in rows for b in sorted(r, key=lambda b: b[0])]


def keep_largest(img: Image.Image) -> Image.Image:
    """Clears everything but the biggest opaque blob (removes leftover background specks)."""
    a = np.array(img.getchannel("A"))
    mask = a > 8
    labels = np.zeros(mask.shape, np.int32)
    h, w = mask.shape
    sizes = [0]
    for y0, x0 in zip(*np.nonzero(mask)):
        if labels[y0, x0]:
            continue
        label = len(sizes)
        q = deque([(y0, x0)])
        labels[y0, x0] = label
        n = 0
        while q:
            y, x = q.popleft()
            n += 1
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = label
                    q.append((ny, nx))
        sizes.append(n)
    biggest = int(np.argmax(sizes))
    rgba = np.array(img)
    rgba[labels != biggest, 3] = 0
    return Image.fromarray(rgba, "RGBA")


def grid_cells(img: Image.Image, cols: int, rows: int):
    cw, ch = img.width / cols, img.height / rows
    for r in range(rows):
        for c in range(cols):
            yield img.crop((round(c * cw), round(r * ch), round((c + 1) * cw), round((r + 1) * ch)))


def key_out_light_background(img: Image.Image) -> Image.Image:
    """
    Removes a white / light checker background that touches the image edges. The elements have a dark
    outline, so the flood fill stops there and light colours inside (silver, quartz) are kept. Pixels on the
    border between the two are anti-aliased black-on-white: they become black with alpha from their darkness.
    """
    rgb = img.convert("RGB")
    key = (255, 0, 255)
    filled = rgb.copy()
    for xy in ((0, 0), (filled.width - 1, 0), (0, filled.height - 1), (filled.width - 1, filled.height - 1)):
        if filled.getpixel(xy) != key:
            ImageDraw.floodfill(filled, xy, key, thresh=110)
    src = np.array(rgb).astype(np.float32)
    bg = np.all(np.array(filled) == key, axis=-1)

    # Pixels within 2 px of the background: the outline's soft edge.
    near_bg = bg.copy()
    for _ in range(2):
        grown = near_bg.copy()
        grown[1:] |= near_bg[:-1]
        grown[:-1] |= near_bg[1:]
        grown[:, 1:] |= near_bg[:, :-1]
        grown[:, :-1] |= near_bg[:, 1:]
        near_bg = grown
    edge = near_bg & ~bg

    lum = src.mean(axis=-1) / 255.0
    alpha = np.where(bg, 0.0, 1.0)
    alpha = np.where(edge, np.clip(1.0 - lum, 0, 1) ** 0.8, alpha)
    out = src.copy()
    out[edge] = 0.0 + np.array([11, 15, 24], np.float32)      # outline colour
    rgba = np.dstack([out, alpha * 255]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def main(src_dir: Path, out_dir: Path):
    frames = Image.open(src_dir / "ui_frames.png").convert("RGBA")
    boxes = blobs(np.array(frames.getchannel("A")))
    if len(boxes) != len(FRAME_NAMES):
        sys.exit(f"ui_frames.png: found {len(boxes)} elements, expected {len(FRAME_NAMES)}: {boxes}")
    (out_dir / "Frames").mkdir(parents=True, exist_ok=True)
    for name, (x1, y1, x2, y2) in zip(FRAME_NAMES, boxes):
        crop = frames.crop((x1 - 12, y1 - 12, x2 + 12, y2 + 12))
        trim(crop).save(out_dir / "Frames" / f"{name}.png")
        print(f"frame {name:18s} {x2 - x1}x{y2 - y1}")

    # A white copy of the bar fill that the game tints (red cargo-full, purple refinery, ...).
    fill = np.array(Image.open(out_dir / "Frames" / "bar_fill.png")).astype(np.float32)
    lum = fill[..., :3] @ np.array([0.299, 0.587, 0.114], np.float32)
    lum = np.clip(lum / max(lum[fill[..., 3] > 128].max(), 1), 0, 1) * 255
    white = np.dstack([lum, lum, lum, fill[..., 3]]).astype(np.uint8)
    Image.fromarray(white, "RGBA").save(out_dir / "Frames" / "bar_fill_white.png")

    icons = Image.open(src_dir / "ui_icons.png").convert("RGBA")
    (out_dir / "Icons").mkdir(parents=True, exist_ok=True)
    for name, cell in zip(ICON_NAMES, grid_cells(icons, 8, 4)):
        to_square(trim(cell), SQUARE_SIZE["icons"]).save(out_dir / "Icons" / f"{name}.png")
    print(f"icons: {len(ICON_NAMES)}")

    minerals = key_out_light_background(Image.open(src_dir / "ui_minerals.png"))
    (out_dir / "Minerals").mkdir(parents=True, exist_ok=True)
    for name, cell in zip(MINERAL_NAMES, grid_cells(minerals, 4, 2)):
        to_square(trim(keep_largest(cell)), SQUARE_SIZE["minerals"]).save(out_dir / "Minerals" / f"{name}.png")
    print(f"minerals: {len(MINERAL_NAMES)}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(Path(sys.argv[1]), Path(sys.argv[2]))
