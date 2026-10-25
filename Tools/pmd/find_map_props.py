#!/usr/bin/env python3
"""Find the decor elements drawn on the upper layer of the story maps (MAP_BG): each group of opaque pixels is an
element already cut out (bushes, flowers, signs, fences...). Duplicates are dropped.

Usage: Tools/.venv/bin/python -I find_map_props.py <unpacked rom data dir> <out_dir> [min_side] [max_side]
Writes <out_dir>/candidates.json (id, map, rect [x, y, w, h] in the rendered map) and <out_dir>/board_<n>.png
(the candidates on a grey background, numbered). Local only: the output is ROM art.
"""
import hashlib
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw
from skytemple_files.common.types.file_types import FileType

SKIP = ("s", "w")  # title screens, weather overlays


def upper_layer(level, data):
    bma = level.get_bma(data)
    if bma.number_of_layers < 2:
        return None
    bpc, bpl, bpas = level.get_bpc(data), level.get_bpl(data), level.get_bpas(data)
    indexed = bma.to_pil_single_layer(bpc, bpl.palettes, bpas, 1)
    rgba = indexed.convert("RGBA")
    px, idx = rgba.load(), indexed.load()
    for y in range(indexed.height):
        for x in range(indexed.width):
            if idx[x, y] % 16 == 0:
                px[x, y] = (0, 0, 0, 0)
    return rgba


def components(image):
    """Bounding boxes of the groups of opaque pixels (8-connected)."""
    w, h = image.size
    alpha = image.getchannel("A").load()
    seen = bytearray(w * h)
    boxes = []
    for y in range(h):
        for x in range(w):
            if seen[y * w + x] or alpha[x, y] == 0:
                continue
            stack = [(x, y)]
            seen[y * w + x] = 1
            x0, y0, x1, y1, count = x, y, x, y, 0
            while stack:
                cx, cy = stack.pop()
                count += 1
                x0, y0, x1, y1 = min(x0, cx), min(y0, cy), max(x1, cx), max(y1, cy)
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and alpha[nx, ny]:
                            seen[ny * w + nx] = 1
                            stack.append((nx, ny))
            boxes.append((x0, y0, x1 - x0 + 1, y1 - y0 + 1, count))
    return boxes


def main(data_dir, out_dir, min_side=8, max_side=96):
    min_side, max_side = int(min_side), int(max_side)
    data = str(Path(data_dir))
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    bg_list = FileType.BG_LIST_DAT.deserialize((Path(data_dir) / "MAP_BG/bg_list.dat").read_bytes())
    seen, found = set(), []
    for index, level in enumerate(bg_list.level):
        name = level.bma_name.upper()
        if name.lower().startswith(SKIP):
            continue
        try:
            image = upper_layer(level, data)
        except Exception as error:
            print(f"{index} {name}: {error}")
            continue
        if image is None:
            continue
        for x, y, w, h, count in components(image):
            if max(w, h) < min_side or max(w, h) > max_side or count < 0.25 * w * h:
                continue
            crop = image.crop((x, y, x + w, y + h))
            key = hashlib.md5(crop.tobytes()).hexdigest()
            if key in seen:
                continue
            seen.add(key)
            found.append({"id": len(found), "map": name, "index": index, "rect": [x, y, w, h], "image": crop})
    (out / "candidates.json").write_text(json.dumps([{k: v for k, v in f.items() if k != "image"} for f in found], indent=1))
    # Boards of 100 candidates, each in a cell of 104 px, numbered.
    cell, per_row, per_board = 104, 10, 100
    for b in range(0, len(found), per_board):
        part = found[b:b + per_board]
        rows = (len(part) + per_row - 1) // per_row
        board = Image.new("RGBA", (per_row * cell, rows * cell), (70, 70, 70, 255))
        draw = ImageDraw.Draw(board)
        for k, f in enumerate(part):
            img = f["image"]
            scale = max(1, min(2, 88 // max(img.width, img.height)))
            img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
            cx, cy = (k % per_row) * cell, (k // per_row) * cell
            board.alpha_composite(img, (cx + (cell - img.width) // 2, cy + 12 + (cell - 12 - img.height) // 2))
            draw.text((cx + 2, cy + 1), str(f["id"]), fill=(255, 255, 0, 255))
        board.save(out / f"board_{b // per_board}.png")
    print(f"{len(found)} candidates")


if __name__ == "__main__":
    main(*sys.argv[1:5])
