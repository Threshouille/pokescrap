#!/usr/bin/env python3
"""Cut the story's scene objects listed in ground_objects.json (GROUND/<file>.wan of an Explorers of Sky ROM) into
PNG images the game loads (Resources/GroundObjects/<key>.png): the first frame of each object's first animation,
cropped to what it shows; with "frames": true, also <key>_frames.png, every frame of its first animation group on
one row ("anims": only those animations of the group, by index), each cell centred on the object's origin (its foot), so the game can play them with a centred pivot. Called
by setup.py; the images are ROM art and stay out of the repository.

Usage: Tools/.venv/bin/python -I extract_ground_objects.py <unpacked rom data dir> <ground_objects.json> <out_dir>
"""
import json
import sys
from pathlib import Path

from PIL import Image
from skytemple_files.graphics.wan_wat.handler import WanHandler

sys.path.insert(0, str(Path(__file__).resolve().parent))
from render_ground_objects import first_frame  # noqa: E402


def strip(wan, anims=None):
    """Every frame of the first animation group (or of those of its animations), in order, on one row of equal cells
    centred on the origin."""
    cells = []
    group = next(g for g in wan.anim_groups if g)
    for anim in (group if anims is None else [group[k] for k in anims]):
        for f in anim.frames:
            image, (ox, oy) = wan.render_frame(wan.frames[f.frame_id])
            cells.append((image, ox - f.offset_x, oy - f.offset_y))
    half_w = max(max(x, im.width - x) for im, x, _ in cells)
    half_h = max(max(y, im.height - y) for im, _, y in cells)
    sheet = Image.new("RGBA", (2 * half_w * len(cells), 2 * half_h), (0, 0, 0, 0))
    for k, (image, x, y) in enumerate(cells):
        sheet.alpha_composite(image, (k * 2 * half_w + half_w - x, half_h - y))
    return sheet, len(cells)


def main(data_dir, catalog, out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    objects = json.loads(Path(catalog).read_text(encoding="utf-8"))["objects"]
    for o in objects:
        wan = WanHandler.deserialize((Path(data_dir) / "GROUND" / f"{o['file']}.wan").read_bytes())
        image = first_frame(wan)
        image.crop(image.getbbox()).save(out / f"{o['key']}.png")
        if o.get("frames"):
            sheet, count = strip(wan, o.get("anims"))
            sheet.save(out / f"{o['key']}_frames.png")
            print(f"{o['key']} : {count} images")
    print(f"{len(objects)} objets de scène")


if __name__ == "__main__":
    if len(sys.argv) < 4:
        sys.exit(__doc__)
    main(*sys.argv[1:4])
