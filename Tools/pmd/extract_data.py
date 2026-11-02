#!/usr/bin/env python3
"""Extract Pokémon stats, moves and learnsets from an unpacked Explorers of Sky (EU) ROM.

Usage: python3 -I extract_data.py <unpacked_rom_dir> <out_dir>
Writes pokemon.json, moves.json, types.json, abilities.json into <out_dir>, plus the compact
Resources/Data/pokemon_rt.json (next to <out_dir>) that the game loads at runtime.
Base speed is not in the ROM: it is read from external/pokemon_stats.csv (PokéAPI) when present.
Abilities: ROM ones per Pokémon, plus the hidden ability from PokéAPI ("hiddenAbility"); catalogue of every
main-series ability in Resources/Data/abilities_rt.json (abilities_data.py).
"""
import csv
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from abilities_data import Catalogue, rom_descriptions  # noqa: E402

# String block starts in MESSAGE/text_*.str for the EU release.
STR_MOVES, STR_POKEMON, STR_TYPES, STR_ABILITIES, STR_ITEMS = 8175, 8736, 13772, 13791, 6775
N_TYPES, N_SPECIES = 19, 600
MD_ENTRY, MOVE_ENTRY = 0x44, 26
CATEGORIES = ["physical", "special", "status"]


def load_strings(path):
    b = path.read_bytes()
    n = struct.unpack_from("<I", b, 0)[0] // 4
    offs = struct.unpack_from(f"<{n}I", b, 0)
    return [b[o:b.index(b"\0", o)].decode("cp1252", "replace") for o in offs[:-1]]


def read_varints(b, pos):
    """Read 7-bit big-endian varints until a null byte."""
    out, cur = [], 0
    while True:
        byte = b[pos]
        pos += 1
        if byte == 0 and cur == 0:
            return out
        cur = (cur << 7) | (byte & 0x7F)
        if not byte & 0x80:
            out.append(cur)
            cur = 0


def main(rom_dir, out_dir):
    data = Path(rom_dir) / "data"
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    fr = load_strings(data / "MESSAGE/text_f.str")
    en = load_strings(data / "MESSAGE/text_e.str")

    def names(start, i):
        return {"name": fr[start + i], "name_en": en[start + i]}

    # --- moves and learnsets (waza_p.bin, SIR0) ---
    w = (data / "BALANCE/waza_p.bin").read_bytes()
    assert w[:4] == b"SIR0"
    header = struct.unpack_from("<I", w, 4)[0]
    moves_ptr, learn_ptr = struct.unpack_from("<II", w, header)
    moves = []
    for i in range((learn_ptr - moves_ptr) // MOVE_ENTRY):
        (power, type_, cat, target, ai_target, pp, ai_weight, acc1, acc2, _ai_chance,
         hits, max_upgrade, crit, magic_coat, snatch, mouth, _frozen, ignores_taunt,
         _range_text, _move_id, _msg) = struct.unpack_from("<HBBHHBBBBBBBBBBBBBBHH", w, moves_ptr + i * MOVE_ENTRY)
        if fr[STR_MOVES + i].startswith("[M:"):  # unused dummy slots
            continue
        moves.append({
            "id": i, **names(STR_MOVES, i),
            "type": type_, "category": CATEGORIES[cat] if cat < 3 else cat,
            "power": power, "pp": pp, "accuracy": acc1, "accuracy2": acc2,
            "target_range": target, "ai_target_range": ai_target, "ai_weight": ai_weight,
            "hits": hits, "crit_chance": crit, "max_upgrade": max_upgrade,
            "magic_coat": bool(magic_coat), "snatch": bool(snatch),
            "uses_mouth": bool(mouth), "ignores_taunt": bool(ignores_taunt),
        })

    learnsets = []
    for i in range((header - learn_ptr) // 12):
        lvl, tm, egg = struct.unpack_from("<III", w, learn_ptr + i * 12)
        pairs = read_varints(w, lvl)
        learnsets.append({
            "level_up": [{"move": m, "level": l} for m, l in zip(pairs[0::2], pairs[1::2])],
            "tm": read_varints(w, tm),
            "egg": read_varints(w, egg),
        })

    # --- species (monster.md) ---
    m = (data / "BALANCE/monster.md").read_bytes()
    assert m[:4] == b"MD\0\0"
    count = struct.unpack_from("<I", m, 4)[0]
    pokemon = []
    for i in range(1, min(count, N_SPECIES)):
        (ent, _u31, dex, speed, pre_evo, evo_method, evo_p1, evo_p2, sprite, gender, body,
         t1, t2, move_type, iq, ab1, ab2, _flags, exp, recruit1, hp, recruit2,
         atk, spatk, deff, spdef, weight, size) = struct.unpack_from(
            "<HHHHHHHHHBBBBBBBBHHhHhBBBBHH", m, 8 + i * MD_ENTRY)
        base_form = struct.unpack_from("<H", m, 8 + i * MD_ENTRY + 0x32)[0]
        if dex == 0:
            continue
        entry = {
            "id": i, **names(STR_POKEMON, i), "dex": dex, "sprite_index": sprite,
            "types": [t for t in (t1, t2) if t], "abilities": [a for a in (ab1, ab2) if a],
            "base_hp": hp, "base_atk": atk, "base_sp_atk": spatk,
            "base_def": deff, "base_sp_def": spdef,
            "exp_yield": exp, "recruit_rate": recruit1 / 10, "recruit_rate2": recruit2 / 10,
            "movement_type": move_type, "movement_speed": speed, "iq_group": iq,
            "body_size": body, "gender": gender, "weight": weight, "size": size,
            "base_form": base_form,
            "evolution": {"from": pre_evo, "method": evo_method, "param1": evo_p1, "param2": evo_p2},
        }
        # Learnsets follow the monster.md index, not the "entity id" field (they only match up to 201).
        if i < len(learnsets):
            entry["learnset"] = learnsets[i]
        pokemon.append(entry)

    speed_csv = Path(__file__).parent / "external" / "pokemon_stats.csv"
    if speed_csv.exists():
        with open(speed_csv, newline="") as f:
            speeds = {int(r["pokemon_id"]): int(r["base_stat"]) for r in csv.DictReader(f) if r["stat_id"] == "6"}
        for entry in pokemon:
            entry["base_speed"] = speeds.get(entry["dex"], 0)

    # Evolution condition of a form, in the ROM's terms (method, param1, param2) turned into named fields.
    move_ids = {m["name_en"]: m["id"] for m in moves}
    known_move = {12: "AncientPower", 13: "Rollout", 14: "Double Hit", 15: "Mimic"}
    extra = {1: "trade", 2: "atkAboveDef", 3: "atkBelowDef", 4: "atkEqualsDef", 5: "day", 6: "night",
             7: "beauty", 8: "randomA", 9: "randomB", 10: "male", 11: "female"}

    def evolution(e):
        method, p1, p2 = e["evolution"]["method"], e["evolution"]["param1"], e["evolution"]["param2"]
        if method == 0 or e["evolution"]["from"] == 0:
            return None
        ev = {"method": {1: "level", 2: "iq", 3: "item", 4: "party", 5: "trade"}.get(method, "unknown")}
        if method == 1:
            ev["level"] = p1
        elif method == 2:
            ev["iq"] = p1
        elif method == 3:
            ev["item"] = p1
            ev["itemName"] = fr[STR_ITEMS + p1]
        elif method == 4:
            ev["party"] = p1
        if p2 in known_move:
            ev["condition"], ev["move"] = "move", move_ids.get(known_move[p2], 0)
        elif p2 in extra and not (method == 5 and p2 == 1):
            ev["condition"] = extra[p2]
        return ev

    abilities = []
    while "[CS:" not in fr[STR_ABILITIES + len(abilities)]:  # descriptions follow the names
        abilities.append({"id": len(abilities), **names(STR_ABILITIES, len(abilities))})
    catalogue = Catalogue(abilities)
    for e in pokemon:
        # Hidden ability (5th generation on, absent from the ROM) of the default form, kept apart.
        hidden = catalogue.by_pokemon(e["dex"])[1]
        if hidden is not None and hidden not in e["abilities"]:
            e["hidden_ability"] = hidden

    runtime = [{
        "id": e["id"], "name": e["name"], "types": e["types"], "baseHp": e["base_hp"],
        "baseAtk": e["base_atk"], "baseSpAtk": e["base_sp_atk"], "baseDef": e["base_def"],
        "baseSpDef": e["base_sp_def"], "baseSpeed": e.get("base_speed", 0), "movementType": e["movement_type"], "expYield": e["exp_yield"],
        "baseForm": e["base_form"], "evolvesFrom": e["evolution"]["from"],
        "evolveLevel": e["evolution"]["param1"] if e["evolution"]["method"] == 1 and e["evolution"]["param2"] == 0 else 0,
        "recruitRate": e["recruit_rate"],
        "abilities": e["abilities"], **({"hiddenAbility": e["hidden_ability"]} if "hidden_ability" in e else {}),
        **({"evolution": ev} if (ev := evolution(e)) else {}),
        "levelUpMoves": list(dict.fromkeys(m["move"] for m in e.get("learnset", {}).get("level_up", []))),
    } for e in pokemon]
    rt_dir = out.parent / "Resources" / "Data"
    rt_dir.mkdir(parents=True, exist_ok=True)
    (rt_dir / "pokemon_rt.json").write_text(json.dumps({"pokemon": runtime}, ensure_ascii=False), encoding="utf-8")
    entries = catalogue.entries(rom_descriptions(fr, STR_ABILITIES, len(abilities)))
    (rt_dir / "abilities_rt.json").write_text(json.dumps({"abilities": entries}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"abilities_rt: {len(entries)} ({sum(e['rom'] for e in entries)} from the ROM)")

    types = [{"id": i, **names(STR_TYPES, i)} for i in range(N_TYPES)]

    for name, obj in (("pokemon", pokemon), ("moves", moves), ("types", types), ("abilities", abilities)):
        (out / f"{name}.json").write_text(json.dumps(obj, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"{name}: {len(obj)}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
