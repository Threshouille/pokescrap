#!/usr/bin/env python3
"""Download the sprites and portraits of the mega evolutions from PMDCollab SpriteCollab.

Usage: python3 -I import_megas.py <project_dir> [--force]
Mega evolutions are not in Explorers of Sky: their PMD-style sprites come from the community repository
https://github.com/PMDCollab/SpriteCollab (CC BY-NC 4.0, credit the authors, no commercial use).
The list is Assets/Resources/Data/megas.json (id, base Pokémon, SpriteCollab dex and form, folder).
Writes Assets/Sprites/Pokemon/<folder>/ (AnimData.xml, <Anim>-Anim.png, id.txt), the portrait
Assets/Resources/Portraits/<id>.png (the base Pokémon's ROM portrait when SpriteCollab has none) and CREDITS_MEGAS.md.
Then run the Unity menu Pokescrap > Build Anim Sets.
"""
import json
import shutil
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

RAW = "https://raw.githubusercontent.com/PMDCollab/SpriteCollab/master"


def fetch(path):
    with urllib.request.urlopen(f"{RAW}/{path}", timeout=60) as response:
        return response.read()


def fetch_optional(path):
    try:
        return fetch(path)
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise


def credit_names():
    """Discord id -> (name, contact), from SpriteCollab's credit_names.txt."""
    names = {}
    for line in fetch("credit_names.txt").decode("utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) >= 2:
            names[parts[1]] = (parts[0], parts[2] if len(parts) > 2 else "")
    return names


def author(credit, names):
    name, contact = names.get(credit, (credit, ""))
    return f"{name} ({contact})" if contact and not contact.startswith("<@") else name


def main(project, force):
    project = Path(project)
    megas = json.loads((project / "Assets/Resources/Data/megas.json").read_text(encoding="utf-8"))["megas"]
    tracker = json.loads(fetch("tracker.json"))
    names = credit_names()
    portraits = project / "Assets/Resources/Portraits"
    portraits.mkdir(parents=True, exist_ok=True)
    credits = ["# Crédits des méga-évolutions", "",
               "Sprites et portraits de [PMDCollab SpriteCollab](https://github.com/PMDCollab/SpriteCollab), "
               "licence [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) : usage non commercial, "
               "auteurs crédités ci-dessous.", ""]
    for m in megas:
        dex, form = f"{m['dex']:04d}", m["form"]
        node = tracker[dex]["subgroups"][form]
        target = project / "Assets/Sprites/Pokemon" / m["folder"]
        target.mkdir(parents=True, exist_ok=True)
        (target / "id.txt").write_text(f"{m['id']}\n")
        if force or not (target / "AnimData.xml").exists():
            xml = fetch(f"sprite/{dex}/{form}/AnimData.xml")
            # Only the sheets of the animations themselves; <CopyOf> entries reuse another one's sheet.
            for anim in ET.fromstring(xml).iter("Anim"):
                if anim.find("CopyOf") is None:
                    sheet = f"{anim.findtext('Name')}-Anim.png"
                    (target / sheet).write_bytes(fetch(f"sprite/{dex}/{form}/{sheet}"))
            (target / "AnimData.xml").write_bytes(xml)
        portrait = portraits / f"{m['id']}.png"
        source = "SpriteCollab"
        if force or not portrait.exists():
            data = fetch_optional(f"portrait/{dex}/{form}/Normal.png")
            if data is not None:
                portrait.write_bytes(data)
            else:
                rom = project / "Rom/export/portraits" / f"{m['base']:04d}" / "00.png"
                source = "ROM (forme de base)" if rom.exists() else "aucun"
                if rom.exists():
                    shutil.copy(rom, portrait)
        sprite_authors = [node["sprite_credit"]["primary"]] + node["sprite_credit"].get("secondary", [])
        portrait_authors = [node["portrait_credit"]["primary"]] + node["portrait_credit"].get("secondary", []) \
            if node.get("portrait_files", {}).get("Normal") is not None else []
        credits.append(f"- **{m['name']}** : sprites {', '.join(author(a, names) for a in sprite_authors)}"
                       + (f" ; portrait {', '.join(author(a, names) for a in portrait_authors)}" if portrait_authors else ""))
        print(f"{m['name']}: id {m['id']}, portrait {source if not portrait_authors else 'SpriteCollab'}")
    (project / "CREDITS_MEGAS.md").write_text("\n".join(credits) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main(sys.argv[1], "--force" in sys.argv[2:])
