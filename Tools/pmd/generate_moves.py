"""Generic real-time moves for every move a Pokémon can learn by level.

Usage: python3 -I Tools/pmd/generate_moves.py <project root>

Reads Assets/Data/moves.json (ROM: type, category, power, range, hits) and the PokéAPI CSV files in
Tools/pmd/external (effects: status, stat changes, healing, drain, contact / charge / recharge flags), applies
Tools/pmd/move_rules.json and writes Assets/Resources/Data/generated_moves.json. Moves that cannot be made generic
are listed in Tools/pmd/generated_moves_report.txt. Hand-written moves in realtime_moves.json take priority in game.
"""
import csv
import json
import re
import sys
from pathlib import Path

# ROM move target / range (low and high nibble of target_range).
T_ENEMIES, T_PARTY, T_ALL, T_USER, T_CHARGING, T_ALL_BUT_USER, T_TEAMMATES = 0, 1, 2, 3, 4, 5, 6
R_FRONT, R_FRONT_SIDES, R_NEARBY, R_ROOM, R_TWO_TILES, R_LINE, R_FLOOR, R_USER = 0, 1, 2, 3, 4, 5, 6, 7
FLAG_CONTACT, FLAG_CHARGE, FLAG_RECHARGE = 1, 2, 3
DAMAGE_CLASS = {"1": "status", "2": "physical", "3": "special"}

# PokéAPI identifiers that the camel-case split does not find.
ALIASES = {"faint-attack": "feint-attack", "hi-jump-kick": "high-jump-kick", "selfdestruct": "self-destruct",
           "softboiled": "soft-boiled", "smellingsalt": "smelling-salts", "vicegrip": "vice-grip",
           "solarbeam": "solar-beam", "sonicboom": "sonic-boom", "ancientpower": "ancient-power",
           "extremespeed": "extreme-speed", "grasswhistle": "grass-whistle", "dynamicpunch": "dynamic-punch",
           "bubblebeam": "bubble-beam", "featherdance": "feather-dance", "dragonbreath": "dragon-breath",
           "thundershock": "thunder-shock", "thunderpunch": "thunder-punch", "doubleslap": "double-slap",
           "poisonpowder": "poison-powder"}


def api_key(name):
    name = name.lower().replace("'", "").replace(".", "")
    name = re.sub(r"[^a-z0-9]+", "-", name).strip("-")
    return ALIASES.get(name, ALIASES.get(name.replace("-", ""), name))


def read_csv(path):
    with open(path, encoding="utf-8") as f:
        return list(csv.DictReader(f))


def main(root):
    root = Path(root)
    ext = root / "Tools/pmd/external"
    rules = json.loads((root / "Tools/pmd/move_rules.json").read_text(encoding="utf-8"))
    moves = json.loads((root / "Assets/Data/moves.json").read_text(encoding="utf-8"))
    pokemon = json.loads((root / "Assets/Data/pokemon.json").read_text(encoding="utf-8"))

    api = {r["identifier"]: r for r in read_csv(ext / "moves.csv")}
    meta = {r["move_id"]: r for r in read_csv(ext / "move_meta.csv")}
    ailments = {r["id"]: r["identifier"] for r in read_csv(ext / "move_meta_ailments.csv")}
    stat_changes, flags = {}, {}
    for r in read_csv(ext / "move_meta_stat_changes.csv"):
        stat_changes.setdefault(r["move_id"], []).append((r["stat_id"], int(r["change"])))
    for r in read_csv(ext / "move_flag_map.csv"):
        flags.setdefault(r["move_id"], set()).add(int(r["move_flag_id"]))

    learnable = {m["move"] for p in pokemon for m in p.get("learnset", {}).get("level_up", [])}
    templates = rules["templates"]
    generated, skipped = [], []

    for mv in moves:
        if mv["id"] not in learnable:
            continue
        a = api.get(api_key(mv["name_en"]))
        if a is None:
            skipped.append((mv, "absent de PokéAPI"))
            continue
        m = meta.get(a["id"], {})
        mflags = flags.get(a["id"], set())
        meta_cat = int(m.get("meta_category_id") or 0)
        if meta_cat in rules["excludedMetaCategories"]:
            skipped.append((mv, f"effet particulier (catégorie PokéAPI {meta_cat})"))
            continue

        target, rng = mv["target_range"] & 15, (mv["target_range"] >> 4) & 15
        category = mv["category"]
        damaging = category != "status"
        ailment = ailments.get(m.get("meta_ailment_id", "0"), "none")
        ailment = None if ailment in ("none", "unknown") else ailment
        changes = [(rules["statMap"][s], c) for s, c in stat_changes.get(a["id"], []) if s in rules["statMap"]]
        healing = int(m.get("healing") or 0)
        drain = int(m.get("drain") or 0)

        power = mv["power"]
        if damaging and power <= 0:
            power = round(int(a["power"]) * rules["apiPowerToPmd"]) if a["power"] else rules["fixedDamagePower"]
        notes = []

        # Shape of the move.
        if damaging:
            if rng == R_ROOM:
                kind = "burstRoom"
            elif rng == R_NEARBY:
                kind = "burstNear"
            elif rng == R_LINE:
                kind = "line"
            elif category == "physical" and FLAG_CONTACT in mflags:
                kind = "lunge" if rng == R_TWO_TILES or int(a["priority"] or 0) > 0 else "melee"
            else:
                kind = "projectile"
        elif healing > 0:
            kind = "heal"
        elif target in (T_USER, T_PARTY, T_TEAMMATES) or rng == R_USER:
            gains = [c for c in changes if c[1] > 0]
            if not gains:
                skipped.append((mv, "statut sur soi sans effet gérable"))
                continue
            kind = "modifierSelf" if target == T_USER else "modifierAllies"
        elif ailment:
            kind = "statusZone" if rng in (R_NEARBY, R_ROOM) else "statusProjectile"
        elif any(c[1] < 0 for c in changes):
            kind = "modifierEnemies" if rng in (R_NEARBY, R_ROOM) else "statusProjectile"
        else:
            skipped.append((mv, "attaque de statut sans effet gérable"))
            continue

        out = {"id": mv["id"], "name": mv["name"], "type": mv["type"], "category": category,
               "power": power if damaging else 0}
        out.update({k: v for k, v in templates[kind].items()})
        out["generated"] = kind

        # Stronger moves recharge longer and are announced longer.
        if damaging:
            scale = (max(power, 1) / rules["referencePower"]) ** rules["cooldownExponent"]
            out["cooldown"] = round(min(rules["maxCooldown"], max(rules["minCooldown"], out["cooldown"] * scale)), 2)
            out["windup"] = round(out.get("windup", 0.5) + rules["windupPerPower"] * power, 2)
        if target == T_CHARGING or FLAG_CHARGE in mflags:
            out["windup"] = round(out.get("windup", 0) + rules["chargeWindup"], 2)
            notes.append("charge")
        if FLAG_RECHARGE in mflags:
            out["cooldown"] = round(min(rules["maxCooldown"], out["cooldown"] * rules["rechargeCooldownFactor"]), 2)
            notes.append("recharge")
        if mv["hits"] == 0 or mv["hits"] > 1:
            if out["family"] == "projectile":
                out["count"] = rules["multiHit"]["count"] if mv["hits"] == 0 else mv["hits"]
                out["spread"] = rules["multiHit"]["spread"]
            else:
                out["power"] = round(out["power"] * rules["multiHit"]["meleePowerFactor"])
            notes.append("plusieurs coups")

        # Effects.
        if ailment:
            out["status"] = ailment
            chance = int(m.get("ailment_chance") or 0)
            out["statusChance"] = round(chance / 100, 2) if damaging and chance > 0 else 1
            out["statusDuration"] = rules["statusDurations"].get(ailment, 3)
        if kind in ("modifierSelf", "modifierAllies"):
            stat, change = next(c for c in changes if c[1] > 0)
            out["modStat"], out["modValue"] = stat, rules["statStages"][str(max(-3, min(3, change)))]
        elif changes and (not damaging or meta_cat == 6):  # 6 = damage and lower the target's stats
            lowered = [c for c in changes if c[1] < 0]
            if lowered:
                stat, change = lowered[0]
                out["modStat"], out["modValue"] = stat, rules["statStages"][str(max(-3, min(3, change)))]
                out.setdefault("modDuration", rules["onHitModDuration"])
                if damaging:
                    out["statChance"] = round(int(m.get("stat_chance") or 0) / 100, 2) or 1
        elif changes and damaging:
            notes.append("bonus au lanceur ignoré")
        if len(changes) > 1:
            notes.append("une seule statistique gardée")
        if healing > 0:
            out["healShare"] = round(min(rules["maxHealShare"], healing / 100), 2)
        if drain > 0:
            out["drain"] = round(drain / 100, 2)
        elif drain < 0:
            out["recoil"] = round(-drain / 100, 2)
        if notes:
            out["notes"] = notes
        generated.append(out)

    data_dir = root / "Assets/Resources/Data"
    (data_dir / "generated_moves.json").write_text(
        json.dumps({"moves": generated}, ensure_ascii=False, indent=1), encoding="utf-8")
    report = [f"{len(generated)} attaques générées, {len(skipped)} écartées (à faire à la main) :"]
    report += [f"  {mv['id']:3} {mv['name']} ({mv['name_en']}) : {why}" for mv, why in skipped]
    (root / "Tools/pmd/generated_moves_report.txt").write_text("\n".join(report) + "\n", encoding="utf-8")
    print(report[0])


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
