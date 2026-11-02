#!/usr/bin/env python3
"""Ability catalogue: the ROM's abilities (ids 1-123) plus every other main-series ability from PokéAPI.

Used by extract_data.py (catalogue, hidden abilities) and mega_data.py (mega abilities). Reads the PokéAPI CSVs of
Tools/pmd/external (abilities.csv, ability_names.csv, ability_flavor_text.csv, ability_prose.csv,
pokemon_abilities.csv). An ability found in the ROM keeps its ROM id; any other gets API_OFFSET + its PokéAPI id.
"""
import csv
import re
from pathlib import Path

API_OFFSET = 1000
EXT = Path(__file__).parent / "external"
FR, EN = "5", "9"
ROM_SKIP = {"Unknown", "$$$"}  # placeholder entries of the ROM list


def rows(name):
    with open(EXT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def slug(name_en):
    """ROM English name -> PokéAPI identifier ("LightningRod" -> "lightning-rod")."""
    return re.sub(r"[^a-z0-9]+", "-", re.sub(r"(?<=[a-z])(?=[A-Z])", "-", name_en).lower()).strip("-")


class Catalogue:
    def __init__(self, rom_abilities):
        self.api = {r["identifier"]: int(r["id"]) for r in rows("abilities") if r["is_main_series"] == "1"}
        self.generation = {int(r["id"]): int(r["generation_id"]) for r in rows("abilities")}
        self.rom = {a["id"]: a for a in rom_abilities if a["name_en"] not in ROM_SKIP}
        self.rom_of_api = {self.api[slug(a["name_en"])]: i for i, a in self.rom.items()}
        missing = [a["name_en"] for a in self.rom.values() if slug(a["name_en"]) not in self.api]
        if missing:
            raise SystemExit(f"ROM abilities not found in PokéAPI: {missing}")

    def game_id(self, api_id):
        return self.rom_of_api.get(api_id, API_OFFSET + api_id)

    def entries(self, rom_desc):
        """Every main-series ability: ROM ones first (ROM order), then the others (PokéAPI order)."""
        names, flavor = {}, {}
        for r in rows("ability_names"):
            names[(int(r["ability_id"]), r["local_language_id"])] = r["name"]
        for r in rows("ability_flavor_text"):
            flavor[(int(r["ability_id"]), r["language_id"])] = " ".join(r["flavor_text"].split())
        effect = {int(r["ability_id"]): r["short_effect"] for r in rows("ability_prose") if r["local_language_id"] == EN}
        api_of_rom = {i: a for a, i in self.rom_of_api.items()}
        out = []
        for api_id in [api_of_rom[i] for i in sorted(self.rom)] + sorted(a for a in self.api.values() if a not in self.rom_of_api):
            gid = self.game_id(api_id)
            rom = self.rom.get(gid)
            e = {"id": gid, "name": names.get((api_id, FR)) or (rom or {}).get("name", ""),
                 "nameEn": names.get((api_id, EN), ""), "generation": self.generation[api_id], "rom": rom is not None}
            if rom and rom["name"] != e["name"]:
                e["nameRom"] = rom["name"]
            if rom and rom_desc.get(gid):
                e["descRom"] = rom_desc[gid]
            e["desc"] = flavor.get((api_id, FR), "")
            e["effectEn"] = effect.get(api_id, flavor.get((api_id, EN), ""))
            out.append(e)
        return out

    def by_pokemon(self, pokemon_id):
        """PokéAPI abilities of a Pokémon (PokéAPI id: the National Dex number for default forms), in game ids:
        (regular abilities, hidden ability or None)."""
        if not hasattr(self, "_by_pokemon"):
            self._by_pokemon = {}
            for r in sorted(rows("pokemon_abilities"), key=lambda r: int(r["slot"])):
                self._by_pokemon.setdefault(int(r["pokemon_id"]), []).append((int(r["ability_id"]), r["is_hidden"] == "1"))
        found = self._by_pokemon.get(pokemon_id, [])
        hidden = [self.game_id(a) for a, h in found if h]
        return [self.game_id(a) for a, h in found if not h], hidden[0] if hidden else None


def rom_descriptions(strings, first_name, count):
    """FR descriptions that follow the ability names in the ROM: "[CS:E]Name[CR]: text"."""
    out = {}
    for i in range(count):
        s = strings[first_name + count + i]
        out[i] = " ".join(s.split("[CR]", 1)[-1].lstrip(": ").split())
    return out
