#!/usr/bin/env python3
"""Render a small sample (a patch of floor framed by walls) of several dungeon tilesets side by side.

Usage: Tools/.venv/bin/python -I preview_tilesets.py <rom.nds> <out.png> <tileset_id>...
Only meant to choose a tileset by eye; use extract_tileset.py to actually export one.
"""
import sys

from ndspy.rom import NintendoDSRom
from PIL import Image, ImageDraw
from skytemple_files.common.types.file_types import FileType
from skytemple_files.common.util import get_ppmdu_config_for_rom
from skytemple_files.graphics.dma.protocol import DmaType

CHUNK = 24
# Small map: 1 = floor, 0 = wall.
SAMPLE = ["000000", "011110", "011110", "011010", "000000"]
OFFSETS = [(0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1)]  # S SE E NE N NW W SW (y down)


def main(rom_path, out_path, ids):
    rom = NintendoDSRom.fromFile(rom_path)
    pack = FileType.DUNGEON_BIN.deserialize(rom.getFileByName("DUNGEON/dungeon.bin"), get_ppmdu_config_for_rom(rom))
    w, h = len(SAMPLE[0]) * CHUNK, len(SAMPLE) * CHUNK
    sheet = Image.new("RGB", (len(ids) * (w + 4), h + 12), (0, 0, 0))
    draw = ImageDraw.Draw(sheet)
    for n, i in enumerate(ids):
        dma, dpc, dpci, dpl = (pack.get(f"dungeon{i}.{ext}") for ext in ("dma", "dpc", "dpci", "dpl"))
        chunks = dpc.chunks_to_pil(dpci, dpl.palettes, 16).convert("RGB")

        def cell(x, y):
            return SAMPLE[y][x] == "1" if 0 <= y < len(SAMPLE) and 0 <= x < len(SAMPLE[0]) else False

        for y in range(len(SAMPLE)):
            for x in range(len(SAMPLE[0])):
                me = cell(x, y)
                mask = sum(1 << b for b, (dx, dy) in enumerate(OFFSETS) if cell(x + dx, y + dy) == me)
                c = dma.get(DmaType.FLOOR if me else DmaType.WALL, mask)[0]
                tile = chunks.crop((c % 16 * CHUNK, c // 16 * CHUNK, c % 16 * CHUNK + CHUNK, c // 16 * CHUNK + CHUNK))
                sheet.paste(tile, (n * (w + 4) + x * CHUNK, 12 + y * CHUNK))
        draw.text((n * (w + 4) + 2, 0), str(i), fill=(255, 255, 255))
    sheet.save(out_path)
    print(sheet.size)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], [int(x) for x in sys.argv[3:]])
