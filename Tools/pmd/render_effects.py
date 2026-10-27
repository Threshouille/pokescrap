"""Exports every animation of effect.bin (move effects of Explorers of Sky) as sheets, for picking by eye.

Usage: python -I render_effects.py <data dir of the unpacked ROM> <output dir> [first last]
Writes <out>/<index:04d>/... (SkyTemple's sheet export) and a log of what failed. Local only (ROM art).
"""
import os
import sys
import traceback

from skytemple_files.common.types.file_types import FileType
from skytemple_files.graphics.effect_wan.handler import EffectWanHandler
from skytemple_files.graphics.effect_wan.model import mergeWithBasePalette

BASE_PALETTE = 292  # the last file holds the 16 shared rows; effects with a palette offset patch over them


def main():
    data_dir, out = sys.argv[1], sys.argv[2]
    pack = FileType.BIN_PACK.deserialize(open(os.path.join(data_dir, "EFFECT", "effect.bin"), "rb").read())
    first = int(sys.argv[3]) if len(sys.argv) > 3 else 0
    last = int(sys.argv[4]) if len(sys.argv) > 4 else len(pack) - 1
    print(f"{len(pack)} files in effect.bin")
    base = EffectWanHandler.deserialize(pack[BASE_PALETTE]).customPalette
    for i in range(first, min(last, len(pack) - 1) + 1):
        raw = pack[i]
        try:
            wan = EffectWanHandler.deserialize(raw)
            mergeWithBasePalette(wan, base)
            EffectWanHandler.export_sheets(os.path.join(out, f"{i:04d}"), wan)
            print(i, "ok", "imgType", wan.imgType)
        except Exception as e:  # many entries are palettes or screen effects, not WAN
            print(i, "skip", type(e).__name__, str(e)[:80], "head", raw[:4])


if __name__ == "__main__":
    main()
