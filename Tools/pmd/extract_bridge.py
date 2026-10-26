#!/usr/bin/env python3
"""The rope bridge of the island (TimeGears: the bridge to the boss's islet), cut out of the wooden pier of the
beach map S01P02A (upper layer of the story map, Explorers of Sky ROM): a section of planks to repeat along the
bridge (2 tiles wide, 1 long, pointing up), and its end with the two posts; ropes along both sides drawn in the
wood's own colours. Called by setup.py; the images are ROM art and stay out of the repository (their .meta in it).

Usage: Tools/.venv/bin/python -I extract_bridge.py <unpacked rom data dir> <out_dir> [preview.png]
Writes <out_dir>/rope_bridge_mid.png and rope_bridge_end.png.
"""
import sys
from pathlib import Path

from PIL import Image
from skytemple_files.common.types.file_types import FileType

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_cliffs import layer_image  # noqa: E402

MAP = "S01P02A"
PLANKS = (204, 310, 252, 334)  # x0, y0, x1, y1 in the upper layer: 48 x 24 px of planks, to repeat
END = (202, 335, 254, 359)  # the pier's end with its two posts
ROPE = 5  # px each side for the rope
SLAT, GAP = 6, 2  # px: a slat across the bridge, the gap between two


def wood_colours(image):
    """The darkest, a middle and the lightest colour of the planks (for the ropes)."""
    colours = sorted({p[:3] for p in image.getdata() if p[3] > 0}, key=lambda c: sum(c))
    return colours[0], colours[len(colours) // 2], colours[-1]


def slats(planks):
    """Slats across the bridge, the pier's planks turned a quarter (grain across), with a gap between two: the sky
    shows through, as on a rope bridge (and the sections join without a seam)."""
    turned = planks.rotate(90, expand=True)  # 24 wide, 48 tall: the grain now runs across
    across = Image.new("RGBA", (planks.width, planks.height), (0, 0, 0, 0))
    half = turned.crop((0, 0, 24, 24))
    across.alpha_composite(half, (0, 0))
    across.alpha_composite(half.transpose(Image.FLIP_LEFT_RIGHT), (24, 0))  # mirrored: no seam in the middle
    px = across.load()
    for y in range(across.height):
        if y % (SLAT + GAP) >= SLAT:
            for x in range(across.width):
                px[x, y] = (0, 0, 0, 0)
    return across


def with_ropes(planks, dark, mid, light):
    """The slats with a thick rope on each side: dark edges, a twist of light and middle wood colours."""
    w, h = planks.width + 2 * ROPE, planks.height
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(planks, (ROPE, 0))
    px = out.load()
    for x0 in (0, w - ROPE + 1):
        for y in range(h):
            px[x0, y] = dark + (255,)
            px[x0 + 1, y] = (light if y % 4 < 2 else mid) + (255,)
            px[x0 + 2, y] = (mid if y % 4 < 2 else light) + (255,)
            px[x0 + 3, y] = dark + (255,)
    return out


def main(data_dir, out_dir, preview=None):
    data = str(Path(data_dir))
    bg_list = FileType.BG_LIST_DAT.deserialize((Path(data_dir) / "MAP_BG/bg_list.dat").read_bytes())
    level = {lv.bma_name.upper(): lv for lv in bg_list.level}[MAP]
    upper = layer_image(level.get_bma(data), level.get_bpc(data), level.get_bpl(data), level.get_bpas(data), 1)
    planks = upper.crop(PLANKS)
    dark, mid, light = wood_colours(planks)
    mid_piece = with_ropes(slats(planks), dark, mid, light)
    end = upper.crop(END)
    end_piece = Image.new("RGBA", (mid_piece.width, end.height), (0, 0, 0, 0))
    end_piece.alpha_composite(end, ((mid_piece.width - end.width) // 2, 0))
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    mid_piece.save(out / "rope_bridge_mid.png")
    end_piece.save(out / "rope_bridge_end.png")
    print(f"pont de corde : {mid_piece.size[0]} x {mid_piece.size[1]} px par tronçon")
    if preview:
        # A bridge of 6 sections over the sky, both ways.
        sheet = Image.new("RGBA", (mid_piece.width * 3 + 7 * 24, 8 * 24), (107, 158, 219, 255))
        for k in range(6):
            sheet.alpha_composite(mid_piece, (8, 8 + k * 24))
        sheet.alpha_composite(end_piece.rotate(180), (8, 8 + 6 * 24 - end_piece.height + 24))
        across = mid_piece.rotate(90, expand=True)
        for k in range(6):
            sheet.alpha_composite(across, (mid_piece.width + 24 + k * 24, 60))
        sheet = sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST)
        sheet.save(preview)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    main(*sys.argv[1:4])
