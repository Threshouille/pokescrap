#!/usr/bin/env python3
"""Export dungeon tilesets from an Explorers of Sky ROM.

Usage: Tools/.venv/bin/python -I extract_tileset.py <rom.nds> <out_dir> <tileset_id>...
Writes <out_dir>/<id>/chunks.png (24x24 px chunks, 16 per row, each surrounded by PADDING px of
its own edge colours so neighbouring chunks never bleed into each other) and tileset.json, which maps
(neighbour mask * 3 + variation) to a chunk index for walls, floor and water/lava.
The décor's palette animation (DPLA: palettes 10 and 11, the shimmer of water and lava, crystals...) goes to
<out_dir>/<id>/anim.png: one column per chunk that changes, one row per step of animFrameMs (padded like chunks.png),
listed in tileset.json (animChunks, animFrames, animFrameMs).
Mask bits (set when the neighbour is the same terrain): S=1 SE=2 E=4 NE=8 N=16 NW=32 W=64 SW=128.
"""
import json
import math
import sys
from collections import Counter
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


MAX_FRAMES = 64
TICK_MS = 1000 / 60  # DPLA durations are in frames of the DS (60 per second)


def animation(dpla, dpl, dpc, dpci):
    """The palette animation as steps of one length: (frames as chunk sheets, step in ms), or None."""
    if dpla is None:
        return None
    sets = []  # (palette slot 10 or 11, its DPLA set, frame count, duration in ticks)
    for k in (0, 1):
        if not dpla.has_for_palette(k):
            continue
        count = dpla.get_frame_count_for_palette(k)
        durations = [d for d in dpla.durations_per_frame_for_colors[k * 16:(k + 1) * 16] if d > 0]
        duration = Counter(durations).most_common(1)[0][0] if durations else 0
        if count > 1 and duration > 0:
            sets.append((10 + k, k, count, duration))
    if not sets:
        return None
    step = math.gcd(*[d for *_, d in sets]) if len(sets) > 1 else sets[0][3]
    period = math.lcm(*[n * d for _, _, n, d in sets])
    frames = period // step
    if frames > MAX_FRAMES:  # too long a common cycle: the slowest set's own steps
        step = max(d for *_, d in sets)
        frames = max(n for _, _, n, _ in sets)
    sheets = []
    for f in range(frames):
        t = f * step
        palettes = [list(p) for p in dpl.palettes]
        for slot, k, n, d in sets:
            palettes[slot] = dpla.get_palette_for_frame(k, (t // d) % n)
        sheets.append(dpc.chunks_to_pil(dpci, palettes, COLUMNS).convert("RGBA"))
    return sheets, step * TICK_MS


def main(rom_path, out_dir, ids):
    rom = NintendoDSRom.fromFile(rom_path)
    pack = FileType.DUNGEON_BIN.deserialize(rom.getFileByName("DUNGEON/dungeon.bin"), get_ppmdu_config_for_rom(rom))
    for i in ids:
        dma, dpc, dpci, dpl = (pack.get(f"dungeon{i}.{ext}") for ext in ("dma", "dpc", "dpci", "dpl"))
        target = Path(out_dir) / f"{i:03d}"
        target.mkdir(parents=True, exist_ok=True)
        image = extrude(dpc.chunks_to_pil(dpci, dpl.palettes, COLUMNS).convert("RGBA"))
        image.save(target / "chunks.png")
        try:
            dpla = pack.get(f"dungeon{i}.dpla")
        except Exception:
            dpla = None
        anim = {"animChunks": [], "animFrames": 0, "animFrameMs": 0}
        found = animation(dpla, dpl, dpc, dpci)
        if found:
            sheets, ms = found
            cols = sheets[0].width // CHUNK
            total = cols * (sheets[0].height // CHUNK)

            def crop(sheet, c):
                x, y = c % cols * CHUNK, c // cols * CHUNK
                return sheet.crop((x, y, x + CHUNK, y + CHUNK)).tobytes()

            moving = [c for c in range(total) if any(crop(sh, c) != crop(sheets[0], c) for sh in sheets[1:])]
            if moving:
                raw = Image.new("RGBA", (len(moving) * CHUNK, len(sheets) * CHUNK))
                for f, sh in enumerate(sheets):
                    for k, c in enumerate(moving):
                        x, y = c % cols * CHUNK, c // cols * CHUNK
                        raw.paste(sh.crop((x, y, x + CHUNK, y + CHUNK)), (k * CHUNK, f * CHUNK))
                extrude(raw).save(target / "anim.png")
                anim = {"animChunks": moving, "animFrames": len(sheets), "animFrameMs": round(ms, 2)}
        if not anim["animChunks"] and (target / "anim.png").exists():
            (target / "anim.png").unlink()

        def table(kind):
            return [c for mask in range(256) for c in dma.get(kind, mask)]

        (target / "tileset.json").write_text(json.dumps({
            "chunkSize": CHUNK, "columns": COLUMNS, "padding": PADDING,
            "wall": table(DmaType.WALL), "floor": table(DmaType.FLOOR), "water": table(DmaType.WATER), **anim,
        }))
        print(f"tileset {i}: {image.size[0]}x{image.size[1]} px, {len(anim['animChunks'])} animated chunks"
              f" ({anim['animFrames']} steps of {anim['animFrameMs']} ms)")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], [int(x) for x in sys.argv[3:]])
