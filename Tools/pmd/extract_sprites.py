#!/usr/bin/env python3
"""Export Pokémon sprite sheets and portraits from an Explorers of Sky ROM.

Usage: Tools/.venv/bin/python -I extract_sprites.py <rom.nds> <out_dir> [first] [last]
Sprites go to <out_dir>/sprites/<sprite_index>/ (AnimData.xml + one PNG sheet per animation),
portraits to <out_dir>/portraits/<pokemon_id>/<emotion>.png.
"""
import sys
from pathlib import Path

from ndspy.rom import NintendoDSRom
from skytemple_files.common.types.file_types import FileType
from skytemple_files.common.util import get_ppmdu_config_for_rom


def load_wan(pack, index):
    data = pack[index]
    if data[:4] != b"SIR0":  # m_ground entries are stored uncompressed
        data = FileType.COMMON_AT.deserialize(data).decompress()
    return FileType.WAN.CHARA.deserialize(data)


def main(rom_path, out_dir, first=0, last=None):
    rom = NintendoDSRom.fromFile(rom_path)
    config = get_ppmdu_config_for_rom(rom)
    out = Path(out_dir)

    packs = [FileType.BIN_PACK.deserialize(rom.getFileByName(f"MONSTER/{n}.bin"))
             for n in ("monster", "m_ground", "m_attack")]
    count = len(packs[0])
    last = count - 1 if last is None else min(last, count - 1)
    done, failed = 0, []
    for i in range(first, last + 1):
        try:
            wan = FileType.WAN.CHARA.merge_wan(*(load_wan(p, i) for p in packs))
            target = out / "sprites" / f"{i:04d}"
            target.mkdir(parents=True, exist_ok=True)
            FileType.WAN.CHARA.export_sheets(str(target), wan, config.animation_names[i])
            done += 1
        except Exception as e:  # keep going, report at the end
            failed.append((i, f"{type(e).__name__}: {e}"))
    print(f"sprites: {done} exported / {count} in ROM, {len(failed)} failed")
    for i, err in failed[:20]:
        print(f"  {i}: {err}")

    if first == 0 and last == count - 1:
        kao = FileType.KAO.deserialize(rom.getFileByName("FONT/kaomado.kao"))
        n = 0
        for index, sub, img in kao:
            if img is None:
                continue
            target = out / "portraits" / f"{index + 1:04d}"
            target.mkdir(parents=True, exist_ok=True)
            img.get().save(target / f"{sub:02d}.png")
            n += 1
        print(f"portraits: {n}")


if __name__ == "__main__":
    a = sys.argv
    main(a[1], a[2], *(int(x) for x in a[3:5]))
