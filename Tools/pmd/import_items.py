#!/usr/bin/env python3
"""Download the icons of the special items (mega stones, Z crystals, Tera shards).

Usage: python3 -I import_items.py <project_dir> [--force]
The list is Assets/Resources/Data/items.json (field "icon" of each item). Sources:
- mega stones, Z crystals and the key stone (Rayquaza): PokeAPI/sprites, https://github.com/PokeAPI/sprites
  (bag icons of the main series games, © Nintendo / Game Freak / The Pokémon Company);
- Tera shards: Niko's Tera (Cobblemon addon), https://github.com/Nxkorasu/Nikostera, sprites by Marcie
  (CC BY-NC-SA 4.0).
- the run's items (bag_items.json, held_items.json, icons named "pmd_<sprite>"): the Mystery Dungeon item sprites of
  PMDCollab's RawAsset, https://github.com/PMDCollab/RawAsset (folder Item; © Nintendo / Chunsoft / The Pokémon Company).
Writes Assets/Resources/Items/<icon>.png (ignored by Git, like the other assets that are not ours) and
CREDITS_ITEMS.md. Offline once every file is there: only missing files are fetched, unless --force.
"""
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

POKEAPI = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items"
RAWASSET = "https://raw.githubusercontent.com/PMDCollab/RawAsset/HEAD/Item"
NIKOSTERA = "https://raw.githubusercontent.com/Nxkorasu/Nikostera/HEAD/resource_pack/assets/nikostera/textures/item"

CREDITS = """# Crédits des icônes d'objets

Téléchargées par `Tools/pmd/import_items.py` dans `Assets/Resources/Items/` (non versionnées).

- **Méga-gemmes, cristaux Z, Gemme Sésame** : icônes des jeux de la série principale, © Nintendo / Creatures /
  Game Freak / The Pokémon Company, via [PokeAPI/sprites](https://github.com/PokeAPI/sprites).
- **Objets de partie** (baies, graines, orbes, objets tenus) : sprites de Pokémon Donjon Mystère (© Nintendo /
  Chunsoft / The Pokémon Company), via [PMDCollab RawAsset](https://github.com/PMDCollab/RawAsset) (dossier `Item`).
- **Téra-Éclats** : sprites de Marcie pour [Niko's Tera](https://github.com/Nxkorasu/Nikostera) (addon Cobblemon),
  licence [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/) : citer l'auteur, pas d'usage
  commercial, partage dans les mêmes conditions.
"""


def fetch(url):
    try:
        with urllib.request.urlopen(url, timeout=60) as response:
            return response.read()
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return None
        raise


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    root = Path(sys.argv[1])
    force = "--force" in sys.argv
    items = json.loads((root / "Assets/Resources/Data/items.json").read_text(encoding="utf-8"))
    wanted = [(s["icon"], [POKEAPI, POKEAPI + "/gen9"]) for s in items["megaStones"] + items["zCrystals"]]
    wanted.append((items["rayquaza"]["icon"], [POKEAPI]))
    wanted += [(t["icon"], [NIKOSTERA]) for t in items["teraShards"]]
    # The run's items: Mystery Dungeon sprites, saved under their "pmd_" name.
    for name in ("bag_items.json", "held_items.json"):
        data = json.loads((root / "Assets/Resources/Data" / name).read_text(encoding="utf-8"))
        wanted += [(i["icon"], ["pmd:" + RAWASSET]) for i in data["items"] if i.get("icon", "").startswith("pmd_")]
    # Chests: the stage's (items.json loot.chestIcon) and the island rooms' (room_contents.json, chestIcon of each content).
    rooms = json.loads((root / "Assets/Resources/Data/room_contents.json").read_text(encoding="utf-8"))
    chests = {items.get("loot", {}).get("chestIcon", "")} | {c.get("chestIcon", "") for c in rooms.values() if isinstance(c, dict)}
    gears = json.loads((root / "Assets/Resources/Data/time_gears.json").read_text(encoding="utf-8")).get("boss", {})
    chests |= {gears.get("chestIcon", ""), gears.get("keyIcon", "")} # the boss's chest, the islet's key
    wanted += [(c, ["pmd:" + RAWASSET]) for c in sorted(chests) if c.startswith("pmd_")]
    out = root / "Assets/Resources/Items"
    out.mkdir(parents=True, exist_ok=True)
    fetched = missing = 0
    for icon, sources in wanted:
        target = out / f"{icon}.png"
        if target.exists() and not force:
            continue
        urls = [f"{s[4:]}/{icon[4:]}.None.png" if s.startswith("pmd:") else f"{s}/{icon}.png" for s in sources]
        data = next((d for d in (fetch(u) for u in urls) if d), None)
        if data is None:
            print(f"introuvable : {icon}")
            missing += 1
            continue
        target.write_bytes(data)
        fetched += 1
    (root / "CREDITS_ITEMS.md").write_text(CREDITS, encoding="utf-8")
    print(f"{fetched} icônes téléchargées, {len(wanted) - fetched - missing} déjà là, {missing} introuvables")
    sys.exit(1 if missing else 0)


if __name__ == "__main__":
    main()
