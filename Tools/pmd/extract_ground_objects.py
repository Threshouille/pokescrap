#!/usr/bin/env python3
"""Cut the story's scene objects listed in ground_objects.json (GROUND/<file>.wan of an Explorers of Sky ROM) into
PNG images the game loads (Resources/GroundObjects/<key>.png): the first frame of each object's first animation,
cropped to what it shows. Called by setup.py; the images are ROM art and stay out of the repository.

Usage: Tools/.venv/bin/python -I extract_ground_objects.py <unpacked rom data dir> <ground_objects.json> <out_dir>
"""
import json
import sys
from pathlib import Path

from skytemple_files.graphics.wan_wat.handler import WanHandler

sys.path.insert(0, str(Path(__file__).resolve().parent))
from render_ground_objects import first_frame  # noqa: E402


def main(data_dir, catalog, out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    objects = json.loads(Path(catalog).read_text(encoding="utf-8"))["objects"]
    for o in objects:
        image = first_frame(WanHandler.deserialize((Path(data_dir) / "GROUND" / f"{o['file']}.wan").read_bytes()))
        image.crop(image.getbbox()).save(out / f"{o['key']}.png")
    print(f"{len(objects)} objets de scène")


if __name__ == "__main__":
    if len(sys.argv) < 4:
        sys.exit(__doc__)
    main(*sys.argv[1:4])
