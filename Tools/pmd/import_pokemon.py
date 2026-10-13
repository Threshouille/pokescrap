#!/usr/bin/env python3
"""Copy the sprites and portrait of Pokémon from the ROM export into the Unity project.

Usage: python3 -I import_pokemon.py <project_dir> Name=id [Name=id ...]
Name is the (ASCII) folder name under Assets/Sprites/Pokemon, id the Pokémon id of pokemon.json.
Then run the Unity menu Pokescrap > Build Anim Sets.
"""
import json
import shutil
import sys
from pathlib import Path


def main(project, pairs):
    project = Path(project)
    pokemon = {p["id"]: p for p in json.loads((project / "Assets/Data/pokemon.json").read_text())}
    for pair in pairs:
        name, pid = pair.split("=")
        pid = int(pid)
        sprites = project / "Rom/export/sprites" / f"{pokemon[pid]['sprite_index']:04d}"
        target = project / "Assets/Sprites/Pokemon" / name
        target.mkdir(parents=True, exist_ok=True)
        count = 0
        for f in list(sprites.glob("*-Anim.png")) + [sprites / "AnimData.xml"]:
            shutil.copy(f, target / f.name)
            count += 1
        (target / "id.txt").write_text(f"{pid}\n")
        portrait = project / "Rom/export/portraits" / f"{pid:04d}" / "00.png"
        has_portrait = portrait.exists()
        if has_portrait:
            dest = project / "Assets/Resources/Portraits"
            dest.mkdir(parents=True, exist_ok=True)
            shutil.copy(portrait, dest / f"{pid}.png")
        print(f"{name}: id {pid} ({pokemon[pid]['name']}), {count} files, portrait={'yes' if has_portrait else 'MISSING'}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
