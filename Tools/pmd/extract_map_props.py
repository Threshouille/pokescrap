#!/usr/bin/env python3
"""Cut the decor elements listed in map_props.json out of the upper layer of the story maps of an Explorers of Sky
ROM (MAP_BG), with their transparency.

Usage: Tools/.venv/bin/python -I extract_map_props.py <unpacked rom data dir> <map_props.json> <out_dir> [preview.png]
<unpacked rom data dir> is Rom/files/data. Writes <out_dir>/<key>.png; an optional preview shows them all by group.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw
from skytemple_files.common.types.file_types import FileType

sys.path.insert(0, str(Path(__file__).resolve().parent))
from find_map_props import upper_layer  # noqa: E402


def main(data_dir, catalog, out_dir, preview=None):
    data = str(Path(data_dir))
    props = json.loads(Path(catalog).read_text(encoding="utf-8"))["props"]
    bg_list = FileType.BG_LIST_DAT.deserialize((Path(data_dir) / "MAP_BG/bg_list.dat").read_bytes())
    by_name = {level.bma_name.upper(): level for level in bg_list.level}
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    layers, images = {}, []
    for prop in props:
        name = prop["map"].upper()
        if name not in layers:
            layers[name] = upper_layer(by_name[name], data)
        x, y, w, h = prop["rect"]
        image = layers[name].crop((x, y, x + w, y + h))
        image.save(out / f"{prop['key']}.png")
        images.append((prop, image))
    print(f"{len(images)} decor elements")
    if preview:
        cell, per_row = 104, 10
        rows = (len(images) + per_row - 1) // per_row
        sheet = Image.new("RGBA", (per_row * cell, rows * cell), (90, 150, 70, 255))
        draw = ImageDraw.Draw(sheet)
        for k, (prop, image) in enumerate(images):
            cx, cy = (k % per_row) * cell, (k // per_row) * cell
            sheet.alpha_composite(image, (cx + (cell - image.width) // 2, cy + 14 + max(0, (cell - 14 - image.height) // 2)))
            draw.text((cx + 2, cy + 1), prop["key"][-12:], fill=(255, 255, 255, 255))
        sheet.save(preview)


if __name__ == "__main__":
    main(*sys.argv[1:5])
