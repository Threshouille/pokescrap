"""Contact sheets of the story's scene objects (GROUND/*.wan: doors, gates, Time Gears, signs...), to pick the ones
the game uses. Local only: the sheets show ROM graphics and stay out of the repository.

Usage:  Tools/.venv/bin/python -I Tools/pmd/render_ground_objects.py Rom/files/data <output folder> [name filter]
Writes <output>/objects_<n>.png (the first frame of each object's first animation, 2x, with its file name) and
<output>/<name>.png (each object's first frame, 1x).
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw
from skytemple_files.graphics.wan_wat.handler import WanHandler

CELL, COLUMNS, ROWS, SCALE = 112, 10, 8, 2


def first_frame(wan):
    groups = [g for g in wan.anim_groups if g]
    if groups and groups[0] and groups[0][0].frames:
        index = groups[0][0].frames[0].frame_id
        if 0 <= index < len(wan.frames):
            return wan.render_frame(wan.frames[index])[0]
    return wan.render_frame(wan.frames[0])[0] if wan.frames else None


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    data, out = Path(sys.argv[1]), Path(sys.argv[2])
    wanted = sys.argv[3] if len(sys.argv) > 3 else ""
    out.mkdir(parents=True, exist_ok=True)
    images = []
    for path in sorted((data / "GROUND").glob("*.wan")):
        if wanted and wanted not in path.name:
            continue
        try:
            im = first_frame(WanHandler.deserialize(path.read_bytes()))
        except Exception as e:  # some objects use another format
            print(f"illisible : {path.name} ({e})")
            continue
        if im is None or im.getbbox() is None:
            continue
        im = im.crop(im.getbbox())
        im.save(out / f"{path.stem}.png")
        images.append((path.stem, im))
    per_sheet = COLUMNS * ROWS
    for sheet in range((len(images) + per_sheet - 1) // per_sheet):
        page = Image.new("RGBA", (COLUMNS * CELL, ROWS * CELL), (40, 44, 52, 255))
        draw = ImageDraw.Draw(page)
        for k, (name, im) in enumerate(images[sheet * per_sheet:(sheet + 1) * per_sheet]):
            x, y = (k % COLUMNS) * CELL, (k // COLUMNS) * CELL
            big = im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)
            big.thumbnail((CELL - 6, CELL - 18), Image.NEAREST)
            page.alpha_composite(big, (x + (CELL - big.width) // 2, y + 2 + (CELL - 18 - big.height) // 2))
            draw.text((x + 3, y + CELL - 14), name, fill=(230, 230, 230, 255))
        page.save(out / f"objects_{sheet + 1}.png")
    print(f"{len(images)} objets, {(len(images) + per_sheet - 1) // per_sheet} planches dans {out}")


if __name__ == "__main__":
    main()
