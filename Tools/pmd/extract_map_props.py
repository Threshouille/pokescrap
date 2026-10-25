#!/usr/bin/env python3
"""Cut the decor elements listed in map_props.json out of the upper layer of the story maps of an Explorers of Sky
ROM (MAP_BG), with their transparency. A prop with "layer": "lower" comes from the lower layer (opaque): "clearTop"
floods the colour of its top left pixel from its top edge (the sky behind it), "cutSides": [fromY, left, right] clears
the columns left of `left` and right of `right` from row fromY down (the wall it stood in) and darkens the new edge.

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
from extract_cliffs import layer_image  # noqa: E402


def cut_out(image, prop):
    """Detours a piece of the lower layer (clearTop, cutSides), cropped to what is left."""
    if not prop.get("clearTop") and not prop.get("cutSides"):
        return image
    px, (w, h) = image.load(), image.size
    if prop.get("clearTop"):
        sky = px[0, 0]
        stack, seen = [(x, 0) for x in range(w)], set()
        while stack:
            x, y = stack.pop()
            if (x, y) in seen or not (0 <= x < w and 0 <= y < h) or sum(abs(px[x, y][i] - sky[i]) for i in range(3)) >= 30:
                continue
            seen.add((x, y))
            px[x, y] = (0, 0, 0, 0)
            stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    if prop.get("cutSides"):
        top, left, right = prop["cutSides"]
        for y in range(top, h):
            for x in range(w):
                if x < left or x > right:
                    px[x, y] = (0, 0, 0, 0)
        edged = image.copy()
        ep = edged.load()
        for y in range(top, h):
            for x in range(w):
                if px[x, y][3] and any(not 0 <= x + dx < w or px[x + dx, y][3] == 0 for dx in (-1, 1)):
                    r, g, b, _ = px[x, y]
                    ep[x, y] = (r * 45 // 100, g * 35 // 100, b * 30 // 100, 255)
        image = edged
    return image.crop(image.getbbox())


def main(data_dir, catalog, out_dir, preview=None):
    data = str(Path(data_dir))
    props = json.loads(Path(catalog).read_text(encoding="utf-8"))["props"]
    bg_list = FileType.BG_LIST_DAT.deserialize((Path(data_dir) / "MAP_BG/bg_list.dat").read_bytes())
    by_name = {level.bma_name.upper(): level for level in bg_list.level}
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    layers, images = {}, []
    for prop in props:
        name, lower = prop["map"].upper(), prop.get("layer") == "lower"
        if (name, lower) not in layers:
            level = by_name[name]
            layers[name, lower] = layer_image(level.get_bma(data), level.get_bpc(data), level.get_bpl(data),
                                              level.get_bpas(data), 0) if lower else upper_layer(level, data)
        x, y, w, h = prop["rect"]
        image = cut_out(layers[name, lower].crop((x, y, x + w, y + h)), prop)
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
