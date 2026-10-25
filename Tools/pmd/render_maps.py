#!/usr/bin/env python3
"""Render the story maps (ground mode backgrounds, MAP_BG) of an Explorers of Sky ROM, to look for art to reuse.

Usage: Tools/.venv/bin/python -I render_maps.py <unpacked rom data dir> <out_dir> [name_filter]
<unpacked rom data dir> is Rom/files/data (from nds_unpack.py). Writes <out_dir>/<index>_<bma name>.png (first frame,
both layers, no collision) and <out_dir>/sheet.png (thumbnails with their index). Local only: the output is ROM art.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw
from skytemple_files.common.types.file_types import FileType

THUMB = 160


def main(data_dir, out_dir, name_filter=""):
    data = Path(data_dir)
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    bg_list = FileType.BG_LIST_DAT.deserialize((data / "MAP_BG/bg_list.dat").read_bytes())
    thumbs = []
    for index, level in enumerate(bg_list.level):
        name = level.bma_name.lower()
        if name_filter and name_filter.lower() not in name:
            continue
        try:
            bma = level.get_bma(str(data))
            bpc = level.get_bpc(str(data))
            bpl = level.get_bpl(str(data))
            bpas = level.get_bpas(str(data))
            image = bma.to_pil(bpc, bpl, bpas, include_collision=False, include_unknown_data_block=False,
                               pal_ani=False, single_frame=True)[0].convert("RGB")
        except Exception as error:  # some entries are empty or broken
            print(f"{index} {name}: {error}")
            continue
        image.save(out / f"{index:03d}_{name}.png")
        thumb = image.copy()
        thumb.thumbnail((THUMB, THUMB))
        thumbs.append((index, name, thumb))
    columns = 12
    rows = (len(thumbs) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * (THUMB + 4), rows * (THUMB + 16)), (20, 20, 20))
    draw = ImageDraw.Draw(sheet)
    for k, (index, name, thumb) in enumerate(thumbs):
        x, y = (k % columns) * (THUMB + 4), (k // columns) * (THUMB + 16)
        sheet.paste(thumb, (x, y + 14))
        draw.text((x + 2, y + 1), f"{index} {name}", fill=(255, 255, 255))
    sheet.save(out / "sheet.png")
    print(f"{len(thumbs)} maps")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else "")
