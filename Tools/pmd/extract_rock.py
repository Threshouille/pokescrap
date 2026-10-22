#!/usr/bin/env python3
"""Export a rock (boulder) sprite from an Explorers of Sky dungeon tileset.

Usage: Tools/.venv/bin/python -I extract_rock.py <rom.nds> <out_dir> <tileset_id>[:core]...
The lone wall chunk of a tileset (a wall tile with no wall around it) is drawn as a rock on that tileset's floor;
the floor around it is made transparent: pixels equal to a floor chunk, then every pixel of a floor colour reached
from the edge of the tile through floor colours or colours absent from the rock's heart (its shadow on grass);
the rock's outline stops the fill. Writes <out_dir>/rock_<id>.png
(24x24 px), used for the boulders of the obstacle room (obstacles.json, "romRock").
"""
import sys
from pathlib import Path

from ndspy.rom import NintendoDSRom
from skytemple_files.common.types.file_types import FileType
from skytemple_files.common.util import get_ppmdu_config_for_rom
from skytemple_files.graphics.dma.protocol import DmaType

CHUNK = 24
COLUMNS = 16


def cut(image, removable):
    """A copy where every pixel reached from the edge through removable (or transparent) pixels is cleared."""
    image = image.copy()
    seen = set()
    stack = [(x, y) for x in range(CHUNK) for y in (0, CHUNK - 1)] + [(x, y) for x in (0, CHUNK - 1) for y in range(CHUNK)]
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or not (0 <= x < CHUNK and 0 <= y < CHUNK):
            continue
        seen.add((x, y))
        p = image.getpixel((x, y))
        if p[3] != 0 and not removable(p):
            continue
        image.putpixel((x, y), (0, 0, 0, 0))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    return image


def main(rom_path, out_dir, ids):
    rom = NintendoDSRom.fromFile(rom_path)
    pack = FileType.DUNGEON_BIN.deserialize(rom.getFileByName("DUNGEON/dungeon.bin"), get_ppmdu_config_for_rom(rom))
    Path(out_dir).mkdir(parents=True, exist_ok=True)
    for spec in ids:
        i, _, mode = spec.partition(":")
        i = int(i)
        dma, dpc, dpci, dpl = (pack.get(f"dungeon{i}.{ext}") for ext in ("dma", "dpc", "dpci", "dpl"))
        sheet = dpc.chunks_to_pil(dpci, dpl.palettes, COLUMNS).convert("RGBA")

        def chunk(kind, mask, variation=0):
            c = dma.get(kind, mask)[variation]
            x, y = (c % COLUMNS) * CHUNK, (c // COLUMNS) * CHUNK
            return sheet.crop((x, y, x + CHUNK, y + CHUNK))

        rock = chunk(DmaType.WALL, 0)
        floors = [chunk(DmaType.FLOOR, 255, v) for v in range(3)]
        floor_colours = {p for f in floors for p in f.get_flattened_data()}
        for y in range(CHUNK):
            for x in range(CHUNK):
                if any(f.getpixel((x, y)) == rock.getpixel((x, y)) for f in floors):
                    rock.putpixel((x, y), (0, 0, 0, 0))
        # "core": also cut, from the edge, every colour absent from the heart of the rock (its shadow on grass);
        # it would eat a rock whose outline has colours of its own, so it is chosen per rock.
        core = {rock.getpixel((x, y)) for x in range(7, CHUNK - 7) for y in range(7, CHUNK - 7)}
        if mode == "core":
            rock = cut(rock, lambda p: p in floor_colours or p not in core)
        else:
            rock = cut(rock, lambda p: p in floor_colours)
        rock.save(Path(out_dir) / f"rock_{i}.png")
        print(f"rock {i}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3:])
