#!/usr/bin/env python3
"""Export dungeon tilesets from an Explorers of Sky ROM.

Usage: Tools/.venv/bin/python -I extract_tileset.py <rom.nds> <out_dir> <tileset_id>...
Writes <out_dir>/<id>/chunks.png (24x24 px chunks, 16 per row, each surrounded by PADDING px of
its own edge colours so neighbouring chunks never bleed into each other) and tileset.json, which maps
(neighbour mask * 3 + variation) to a chunk index for walls, floor and water/lava.
Mask bits (set when the neighbour is the same terrain): S=1 SE=2 E=4 NE=8 N=16 NW=32 W=64 SW=128.
"""
import json
import sys
from pathlib import Path

from ndspy.rom import NintendoDSRom
from PIL import Image
from skytemple_files.common.types.file_types import FileType
from skytemple_files.common.util import get_ppmdu_config_for_rom
from skytemple_files.graphics.dma.protocol import DmaType

COLUMNS = 16
CHUNK = 24
PADDING = 2


def extrude(sheet):
    """Re-lay the chunk sheet with every chunk's border pixels repeated PADDING px outwards."""
    cell = CHUNK + 2 * PADDING
    cols, rows = sheet.width // CHUNK, sheet.height // CHUNK
    out = Image.new("RGBA", (cols * cell, rows * cell))
    for r in range(rows):
        for c in range(cols):
            chunk = sheet.crop((c * CHUNK, r * CHUNK, (c + 1) * CHUNK, (r + 1) * CHUNK))
            # Stretching to the padded size then pasting the original on top leaves edge-coloured borders.
            x, y = c * cell, r * cell
            for box, pos in (
                ((0, 0, CHUNK, 1), (PADDING, 0, CHUNK, PADDING)),
                ((0, CHUNK - 1, CHUNK, CHUNK), (PADDING, PADDING + CHUNK, CHUNK, PADDING)),
                ((0, 0, 1, CHUNK), (0, PADDING, PADDING, CHUNK)),
                ((CHUNK - 1, 0, CHUNK, CHUNK), (PADDING + CHUNK, PADDING, PADDING, CHUNK)),
                ((0, 0, 1, 1), (0, 0, PADDING, PADDING)),
                ((CHUNK - 1, 0, CHUNK, 1), (PADDING + CHUNK, 0, PADDING, PADDING)),
                ((0, CHUNK - 1, 1, CHUNK), (0, PADDING + CHUNK, PADDING, PADDING)),
                ((CHUNK - 1, CHUNK - 1, CHUNK, CHUNK), (PADDING + CHUNK, PADDING + CHUNK, PADDING, PADDING)),
            ):
                out.paste(chunk.crop(box).resize((pos[2], pos[3]), Image.NEAREST), (x + pos[0], y + pos[1]))
            out.paste(chunk, (x + PADDING, y + PADDING))
    return out


def main(rom_path, out_dir, ids):
    rom = NintendoDSRom.fromFile(rom_path)
    pack = FileType.DUNGEON_BIN.deserialize(rom.getFileByName("DUNGEON/dungeon.bin"), get_ppmdu_config_for_rom(rom))
    for i in ids:
        dma, dpc, dpci, dpl = (pack.get(f"dungeon{i}.{ext}") for ext in ("dma", "dpc", "dpci", "dpl"))
        target = Path(out_dir) / f"{i:03d}"
        target.mkdir(parents=True, exist_ok=True)
        image = extrude(dpc.chunks_to_pil(dpci, dpl.palettes, COLUMNS).convert("RGBA"))
        image.save(target / "chunks.png")

        def table(kind):
            return [c for mask in range(256) for c in dma.get(kind, mask)]

        (target / "tileset.json").write_text(json.dumps({
            "chunkSize": CHUNK, "columns": COLUMNS, "padding": PADDING,
            "wall": table(DmaType.WALL), "floor": table(DmaType.FLOOR), "water": table(DmaType.WATER),
        }))
        print(f"tileset {i}: {image.size[0]}x{image.size[1]} px")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], [int(x) for x in sys.argv[3:]])
