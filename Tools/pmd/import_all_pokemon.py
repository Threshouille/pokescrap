#!/usr/bin/env python3
"""Import every species of pokemon.json (first entry of each Pokédex number, plus all Unown letters).

Usage: python3 -I import_all_pokemon.py <project_dir>
Skips Pokémon already present (id.txt), writes the skipped alternate entries to Docs/POKEMON_EXCEPTIONS.md.
Then run the Unity menu Pokescrap > Build Anim Sets.
"""
import json
import subprocess
import sys
import unicodedata
from pathlib import Path

UNOWN_DEX = 201
UNOWN_LETTERS = [chr(c) for c in range(ord("A"), ord("Z") + 1)] + ["Exclamation", "Question"]
FIXED_NAMES = {29: "NidoranF", 32: "NidoranM"}  # ♀/♂ are garbled in the ROM text


def ascii_name(name):
    s = unicodedata.normalize("NFKD", name).encode("ascii", "ignore").decode()
    return "".join(c for c in s if c.isalnum())


def main(project):
    project = Path(project)
    pokemon = sorted(json.loads((project / "Assets/Data/pokemon.json").read_text()), key=lambda p: p["id"])
    root = project / "Assets/Sprites/Pokemon"
    present = {int(f.read_text()) for f in root.glob("*/id.txt")}
    used = {d.name for d in root.iterdir() if d.is_dir()}
    first, skipped, pairs, unown = {}, [], [], 0
    for p in pokemon:
        dex = p["dex"]
        if dex == UNOWN_DEX:
            name = f"Zarbi{UNOWN_LETTERS[unown]}"
            unown += 1
        elif dex in first:
            skipped.append((p, first[dex]))
            continue
        else:
            name = FIXED_NAMES.get(p["id"]) or ascii_name(p["name"])
        first.setdefault(dex, p)
        if p["id"] in present:
            continue
        if not (project / "Rom/export/sprites" / f"{p['sprite_index']:04d}" / "AnimData.xml").exists():
            print(f"NO SPRITES: {p['id']} {p['name']}")
            continue
        if name in used:
            name = f"{name}{p['id']}"
        used.add(name)
        pairs.append(f"{name}={p['id']}")
    for i in range(0, len(pairs), 50):
        subprocess.run([sys.executable, "-I", str(project / "Tools/pmd/import_pokemon.py"), str(project)] + pairs[i:i + 50], check=True)
    lines = ["# Entrées de la ROM non importées", "",
             "Formes alternatives et versions propres à l'histoire, écartées par `Tools/pmd/import_all_pokemon.py` "
             "(une entrée par numéro de Pokédex, sauf Zarbi). À revoir au cas par cas.", "",
             "| id | Nom | Nom EN | Pokédex | sprite | forme importée (id) |", "|---|---|---|---|---|---|"]
    for p, f in skipped:
        lines.append(f"| {p['id']} | {p['name']} | {p['name_en']} | {p['dex']} | {p['sprite_index']} | {f['name']} ({f['id']}) |")
    (project / "Docs/POKEMON_EXCEPTIONS.md").write_text("\n".join(lines) + "\n")
    print(f"imported {len(pairs)}, skipped {len(skipped)}")


if __name__ == "__main__":
    main(sys.argv[1])
