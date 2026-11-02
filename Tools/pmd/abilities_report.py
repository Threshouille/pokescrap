#!/usr/bin/env python3
"""Ability statistics, to choose which abilities to adapt to real time.

Usage: python3 -I abilities_report.py <project_dir> [<out.md>]
Reads Resources/Data/abilities_rt.json, pokemon_rt.json, megas.json, ability_effects.json (✓: has an effect), Assets/Data/pokemon.json (National Dex number)
and Tools/pmd/ability_families.json (adaptation families, by keyword on the English effect). Counts per species
(National Dex number: the ROM's forms of one species are merged), per evolution line (baseForm) and per mega.
Writes a Markdown report (Docs/TALENTS.md by default).
"""
import collections
import json
import sys
from pathlib import Path

TOP = 25


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def main(project, out=None):
    root = Path(project)
    data = root / "Assets/Resources/Data"
    catalogue = {a["id"]: a for a in load(data / "abilities_rt.json")["abilities"]}
    pokemon = load(data / "pokemon_rt.json")["pokemon"]
    dex_of = {e["id"]: e["dex"] for e in load(root / "Assets/Data/pokemon.json")}
    megas = load(data / "megas.json")["megas"]
    rules = load(root / "Tools/pmd/ability_families.json")
    in_game = {a["id"] for a in load(data / "ability_effects.json")["abilities"]}

    # Species: union of its forms' abilities; line: baseForm of its first form in the table.
    regular, hidden, line, name = collections.defaultdict(set), {}, {}, {}
    for e in pokemon:
        dex = dex_of[e["id"]]
        regular[dex].update(e["abilities"])
        if e.get("hiddenAbility"):
            hidden.setdefault(dex, e["hiddenAbility"])
        line.setdefault(dex, e["baseForm"])
        name.setdefault(dex, e["name"])
    for dex in hidden:
        if hidden[dex] in regular[dex]:
            hidden[dex] = None
    line_name = {e["id"]: e["name"] for e in pokemon}

    holders = collections.defaultdict(lambda: {"regular": set(), "hidden": set(), "mega": []})
    for dex, ids in regular.items():
        for a in ids:
            holders[a]["regular"].add(dex)
    for dex, a in hidden.items():
        if a:
            holders[a]["hidden"].add(dex)
    for m in megas:
        holders[m["ability"]]["mega"].append(m["name"])

    def family(a):
        forced = rules["byId"].get(str(a["id"]))
        text = (a.get("effectEn") or "").lower()
        for f in rules["families"]:
            if forced == f["key"] or (not forced and any(k in text for k in f["keywords"])):
                return f["key"]
        return rules["families"][-1]["key"]

    def label(a):
        c = catalogue[a]
        return f"{c['name']} ({c['nameEn']})"

    def lines_of(dexes):
        return {line[d] for d in dexes}

    used = [a for a in catalogue if holders[a]["regular"] or holders[a]["hidden"] or holders[a]["mega"]]
    rom_used = [a for a in used if holders[a]["regular"]]
    only_extra = [a for a in used if not holders[a]["regular"]]
    species = sorted(regular)
    out_lines = []
    w = out_lines.append

    w("# Talents : statistiques\n")
    w(f"Généré par `Tools/pmd/abilities_report.py` d'après `abilities_rt.json`, `pokemon_rt.json` et `megas.json`. "
      f"Comptes par espèce (n° de Pokédex : les formes de la ROM sont réunies), par lignée (`baseForm`) et par méga.\n")
    w("## Vue d'ensemble\n")
    w(f"- Catalogue : {len(catalogue)} talents de la série principale, dont {sum(c['rom'] for c in catalogue.values())} présents dans la ROM.")
    w(f"- Talents portés par au moins un Pokémon du projet : {len(used)} ({len(rom_used)} talents normaux de la ROM, "
      f"{len(only_extra)} seulement en talent caché ou sur une méga).")
    w(f"- Espèces : {len(species)} ; lignées : {len(lines_of(species))} ; mégas : {len(megas)}.")
    counts = collections.Counter(len(regular[d]) for d in species)
    w(f"- Talents normaux par espèce : " + ", ".join(f"{n} talent{'s' if n > 1 else ''} : {counts[n]}" for n in sorted(counts)) + ".")
    no_hidden = [d for d in species if not hidden.get(d)]
    w(f"- Sans talent caché distinct : {len(no_hidden)} espèces (légendaires surtout, ou caché identique à un talent normal).")
    w(f"- Sans aucun talent : {sum(1 for d in species if not regular[d])}.")
    w(f"- Avec un effet en jeu (`ability_effects.json`, ✓ ci-dessous) : {len(in_game)} talents.\n")

    w(f"## Les plus communs (talents normaux, {TOP} premiers)\n")
    w("| Talent | Espèces | Lignées | En caché | Mégas | Famille | En jeu |")
    w("|---|---:|---:|---:|---:|---|:---:|")
    for a in sorted(rom_used, key=lambda a: (-len(holders[a]["regular"]), catalogue[a]["name"]))[:TOP]:
        h = holders[a]
        w(f"| {label(a)} | {len(h['regular'])} | {len(lines_of(h['regular']))} | {len(h['hidden'])} | {len(h['mega'])} | {family(catalogue[a])} | {"✓" if a in in_game else ""} |")
    w("")

    unique = [a for a in rom_used if len(lines_of(holders[a]["regular"] | holders[a]["hidden"])) == 1]
    w(f"## Uniques à une lignée ({len(unique)}, talent normal ou caché compris)\n")
    for a in sorted(unique, key=lambda a: catalogue[a]["name"]):
        ln = next(iter(lines_of(holders[a]["regular"] | holders[a]["hidden"])))
        w(f"- {label(a)} : lignée de {line_name.get(ln, ln)} ({family(catalogue[a])})")
    w("")

    w(f"## Seulement en talent caché ou sur une méga ({len(only_extra)})\n")
    for a in sorted(only_extra, key=lambda a: (-len(holders[a]["hidden"]) - len(holders[a]["mega"]), catalogue[a]["name"])):
        h = holders[a]
        where = []
        if h["hidden"]:
            where.append(f"caché de {len(h['hidden'])} espèces ({', '.join(name[d] for d in sorted(h['hidden'])[:4])}{'…' if len(h['hidden']) > 4 else ''})")
        if h["mega"]:
            where.append("méga : " + ", ".join(h["mega"]))
        w(f"- {label(a)}, {catalogue[a]['generation']}e gén. : {' ; '.join(where)} ({family(catalogue[a])})")
    w("")

    w("## Familles d'adaptation au temps réel\n")
    w("Classement automatique d'après l'effet anglais de PokéAPI (`Tools/pmd/ability_families.json`, `byId` pour corriger "
      "un talent) ; poids = espèces qui portent le talent (normal + caché) + mégas.\n")
    by_family = collections.defaultdict(list)
    for a in used:
        by_family[family(catalogue[a])].append(a)
    weight = lambda a: len(holders[a]["regular"]) + len(holders[a]["hidden"]) + len(holders[a]["mega"])
    for f in rules["families"]:
        items = sorted(by_family[f["key"]], key=lambda a: -weight(a))
        if not items:
            continue
        w(f"### {f['name']} : {len(items)} talents, poids {sum(weight(a) for a in items)}\n")
        for a in items:
            w(f"- {'✓ ' if a in in_game else ''}{label(a)} [{weight(a)}] : {catalogue[a]['effectEn']}")
        w("")

    target = Path(out) if out else root / "Docs/TALENTS.md"
    target.write_text("\n".join(out_lines) + "\n", encoding="utf-8")
    print(f"{target}: {len(used)} talents portés, {len(unique)} uniques à une lignée, {len(only_extra)} hors ROM")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
