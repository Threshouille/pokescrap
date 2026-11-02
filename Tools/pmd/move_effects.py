"""Move → effect table of Explorers of Sky (EU): reads MOVE_ANIMATION_INFO and EFFECT_ANIMATION_INFO in overlay 10
(offsets from pmdsky_debug) and writes, for every move, its up to 4 effect animations with their effect.bin file.

Usage: python -I move_effects.py <unpacked ROM dir> <moves.json> <out.json>
"""
import json
import os
import sys

from skytemple_files.data.anim.model import GeneralAnim, MoveAnim

MOVE_INFO, MOVE_COUNT, MOVE_SIZE = 0xC5FC, 563, 24
EFFECT_INFO, EFFECT_COUNT, EFFECT_SIZE = 0xFAC4, 700, 28


def main():
    rom, moves_path, out = sys.argv[1:4]
    ov = open(os.path.join(rom, "overlay", "overlay_0010.bin"), "rb").read()
    effects = [GeneralAnim(ov[EFFECT_INFO + k * EFFECT_SIZE: EFFECT_INFO + (k + 1) * EFFECT_SIZE]) for k in range(EFFECT_COUNT)]
    names = {m["id"]: (m["name"], m.get("name_en", "")) for m in json.load(open(moves_path))}
    table = []
    for i in range(MOVE_COUNT):
        m = MoveAnim(ov[MOVE_INFO + i * MOVE_SIZE: MOVE_INFO + (i + 1) * MOVE_SIZE])
        steps = []
        for slot, k in enumerate((m.anim1, m.anim2, m.anim3, m.anim4), 1):
            if k == 0 or k >= EFFECT_COUNT:
                continue
            e = effects[k]
            steps.append({"slot": slot, "effect": k, "type": e.anim_type.name, "file": e.anim_file, "anim": e.unk1, "unk2": e.unk2, "loop": e.loop,
                          "point": e.point.name})
        name, en = names.get(i, ("", ""))
        table.append({"move": i, "name": name, "name_en": en, "userAnim": m.animation, "speed": m.speed,
                      "point": m.point.name, "steps": steps})
    json.dump(table, open(out, "w"), ensure_ascii=False, indent=1)
    print(len(table), "moves")


if __name__ == "__main__":
    main()
