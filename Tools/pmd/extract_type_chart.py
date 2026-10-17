"""Type chart for the game, in ROM type ids.

Usage: python3 -I Tools/pmd/extract_type_chart.py <project root>

Reads the PokéAPI CSV files in Tools/pmd/external (type_efficacy.csv, type_efficacy_past.csv, types.csv), puts back
the generation 4 values (the ROM's era: Steel still resisted Ghost and Dark, no Fairy type) and writes
Assets/Resources/Data/type_chart.json with every matchup that is not neutral. Factors: 2, 0.5 or 0; the game turns
them into its own multipliers (meta.json).
"""
import csv
import json
import sys
from pathlib import Path

GENERATION = 4


def read_csv(path):
    with open(path, encoding="utf-8") as f:
        return list(csv.DictReader(f))


def main(root):
    root = Path(root)
    ext = root / "Tools/pmd/external"
    rom_types = json.loads((root / "Assets/Data/types.json").read_text(encoding="utf-8"))
    api_names = {r["id"]: r["identifier"] for r in read_csv(ext / "types.csv")}
    rom_ids = {t["name_en"].lower(): t["id"] for t in rom_types}

    factors = {(r["damage_type_id"], r["target_type_id"]): int(r["damage_factor"])
               for r in read_csv(ext / "type_efficacy.csv")}
    # A past row holds the value used up to and including its generation.
    for r in sorted(read_csv(ext / "type_efficacy_past.csv"), key=lambda r: -int(r["generation_id"])):
        if int(r["generation_id"]) >= GENERATION:
            factors[(r["damage_type_id"], r["target_type_id"])] = int(r["damage_factor"])

    entries = []
    for (attack, defend), factor in sorted(factors.items(), key=lambda kv: (int(kv[0][0]), int(kv[0][1]))):
        a, d = rom_ids.get(api_names.get(attack)), rom_ids.get(api_names.get(defend))
        if a is None or d is None or factor == 100:
            continue  # Fairy and other types the ROM does not have
        entries.append({"attack": a, "defend": d, "factor": factor / 100})

    out = root / "Assets/Resources/Data/type_chart.json"
    out.write_text(json.dumps({"matchups": entries}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"{out}: {len(entries)} matchups")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
