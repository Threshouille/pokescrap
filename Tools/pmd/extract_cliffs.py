#!/usr/bin/env python3
"""Cut cliff faces out of the story maps of an Explorers of Sky ROM (MAP_BG), as listed in cliff_styles.json.

Usage: Tools/.venv/bin/python -I extract_cliffs.py <unpacked rom data dir> <cliff_styles.json> <out_dir> [preview.png]
<unpacked rom data dir> is Rom/files/data. Writes <out_dir>/<style>/<role>_<n>.png (24×24 RGBA); an optional preview
sheet shows every piece by style and role. The upper layer keeps its transparent pixels (palette index 0 of a
16-colour palette); "both" lays the upper layer over the lower one.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw
from skytemple_files.common.types.file_types import FileType

PIECE = 24


def layer_image(bma, bpc, bpl, bpas, layer):
    """One layer of the map as RGBA, its transparent pixels (index 0 of a palette) cleared."""
    indexed = bma.to_pil_single_layer(bpc, bpl.palettes, bpas, layer)
    rgba = indexed.convert("RGBA")
    pixels = rgba.load()
    values = indexed.load()
    for y in range(indexed.height):
        for x in range(indexed.width):
            if values[x, y] % 16 == 0:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def main(data_dir, styles_path, out_dir, preview=None):
    data = Path(data_dir)
    styles = json.loads(Path(styles_path).read_text(encoding="utf-8"))["styles"]
    bg_list = FileType.BG_LIST_DAT.deserialize((data / "MAP_BG/bg_list.dat").read_bytes())
    by_name = {level.bma_name.upper(): level for level in bg_list.level}
    rows = []
    for style in styles:
        level = by_name[style["map"].upper()]
        bma, bpc, bpl, bpas = level.get_bma(str(data)), level.get_bpc(str(data)), level.get_bpl(str(data)), level.get_bpas(str(data))
        lower = layer_image(bma, bpc, bpl, bpas, 0)
        upper = layer_image(bma, bpc, bpl, bpas, 1) if bma.number_of_layers > 1 else Image.new("RGBA", lower.size)
        both = lower.copy()
        both.alpha_composite(upper)
        source = {"lower": lower, "upper": upper, "both": both}[style.get("layer", "lower")]
        target = Path(out_dir) / style["key"]
        target.mkdir(parents=True, exist_ok=True)
        for role, corners in style["pieces"].items():
            image = both if role in style.get("both", []) else source
            pieces = []
            for n, (x, y) in enumerate(corners):
                piece = image.crop((x, y, x + PIECE, y + PIECE))
                piece.save(target / f"{role}_{n}.png")
                pieces.append(piece)
            rows.append((style["key"], role, pieces))
        print(f"{style['key']}: {sum(len(c) for c in style['pieces'].values())} pieces")
    if preview:
        scale = 3
        sheet = Image.new("RGBA", (260 + 8 * (PIECE * scale + 4), len(rows) * (PIECE * scale + 4)), (40, 40, 40, 255))
        draw = ImageDraw.Draw(sheet)
        for k, (key, role, pieces) in enumerate(rows):
            y = k * (PIECE * scale + 4)
            draw.text((4, y + 30), f"{key} {role}", fill=(255, 255, 255, 255))
            for n, piece in enumerate(pieces):
                big = piece.resize((PIECE * scale, PIECE * scale), Image.NEAREST)
                back = Image.new("RGBA", big.size, (90, 160, 70, 255))  # grass behind, to show transparency
                back.alpha_composite(big)
                sheet.paste(back, (260 + n * (PIECE * scale + 4), y))
        sheet.save(preview)


if __name__ == "__main__":
    main(*sys.argv[1:5])
