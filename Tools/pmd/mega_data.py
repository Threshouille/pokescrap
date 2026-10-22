#!/usr/bin/env python3
"""Fill Assets/Resources/Data/megas.json with the official stats and types of each mega evolution.

Usage: python3 -I mega_data.py <project_dir>
Reads the PokéAPI CSVs of Tools/pmd/external (pokemon.csv, pokemon_stats.csv, pokemon_types.csv, types.csv) and
Assets/Data/types.json (ROM type ids). Writes, for each mega: "stats" (the mega's official base stats) and
"baseStats" (those of its base form), as hp, atk, def, spAtk, spDef, speed: the game turns their ratio into a
bonus on the Pokémon's own stats; "types" in ROM ids (Fairy, absent from the ROM, is left out).
"""
import csv
import json
import re
import sys
from pathlib import Path

STAT_NAMES = {1: "hp", 2: "atk", 3: "def", 4: "spAtk", 5: "spDef", 6: "speed"}


def rows(path):
    with open(path, encoding="utf-8") as f:
        return list(csv.DictReader(f))


def main(project):
    project = Path(project)
    ext = project / "Tools/pmd/external"
    path = project / "Assets/Resources/Data/megas.json"
    doc = json.loads(path.read_text(encoding="utf-8"))
    api_ids = {r["identifier"]: int(r["id"]) for r in rows(ext / "pokemon.csv")}
    stats = {}
    for r in rows(ext / "pokemon_stats.csv"):
        stats.setdefault(int(r["pokemon_id"]), {})[STAT_NAMES[int(r["stat_id"])]] = int(r["base_stat"])
    types = {}
    for r in sorted(rows(ext / "pokemon_types.csv"), key=lambda r: int(r["slot"])):
        types.setdefault(int(r["pokemon_id"]), []).append(int(r["type_id"]))
    api_type_names = {int(r["id"]): r["identifier"] for r in rows(ext / "types.csv")}
    rom_types = {t["name_en"].lower(): t["id"] for t in json.loads((project / "Assets/Data/types.json").read_text(encoding="utf-8"))}
    for m in doc["megas"]:
        # Folder "MegaCharizardX" -> PokéAPI "charizard-mega-x".
        species, suffix = re.fullmatch(r"Mega(.+?)([XY]?)", m["folder"]).groups()
        identifier = f"{species.lower()}-mega" + (f"-{suffix.lower()}" if suffix else "")
        mega_id = api_ids[identifier]
        m["stats"] = stats[mega_id]
        m["baseStats"] = stats[m["dex"]]
        m["types"] = [rom_types[api_type_names[t]] for t in types[mega_id] if api_type_names[t] in rom_types]
        print(f"{m['name']}: {identifier} {m['stats']} types {m['types']}")
    path.write_text(json.dumps(doc, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main(sys.argv[1])
