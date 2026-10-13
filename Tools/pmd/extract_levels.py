#!/usr/bin/env python3
"""Export the per-level stat growth of every Pokémon from an unpacked Explorers of Sky ROM.

Usage: Tools/.venv/bin/python -I extract_levels.py <unpacked_rom_dir> <out_file.bytes>
Binary layout (little endian): int32 count, then per Pokémon: int32 id, followed by 100 records of
uint32 experience, uint16 hp, uint8 attack, uint8 special attack, uint8 defense, uint8 special defense.
Record N holds what is gained when reaching level N+1 (the first record is all zeros).
"""
import struct
import sys
from pathlib import Path

from skytemple_files.common.types.file_types import FileType

LEVELS = 100


def main(rom_dir, out_file):
    pack = FileType.BIN_PACK.deserialize((Path(rom_dir) / "data/BALANCE/m_level.bin").read_bytes())
    out = bytearray(struct.pack("<i", len(pack)))
    for index in range(len(pack)):
        data = bytes(pack[index])
        if data[:4] == b"SIR0":
            data = bytes(FileType.SIR0.deserialize(data).content)
        if data[:5] in (b"PKDPX", b"AT4PX"):
            data = bytes(FileType.COMMON_AT.deserialize(data).decompress())
        levels = FileType.LEVEL_BIN_ENTRY.deserialize(data).levels
        assert len(levels) == LEVELS, (index, len(levels))
        out += struct.pack("<i", index + 1)  # entry N belongs to Pokémon id N + 1
        for lv in levels:
            out += struct.pack("<IHBBBB", lv.experience_required, lv.hp_growth, lv.attack_growth,
                               lv.special_attack_growth, lv.defense_growth, lv.special_defense_growth)
    Path(out_file).write_bytes(out)
    print(f"levels: {len(pack)} Pokémon, {len(out)} bytes")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
