"""Herses des grandes salles de combat des niveaux continus (GDD « Donjon du volcan » : portes qui se ferment quand
l'équipe entre dans une maison des monstres, un défi ou la salle du sous-boss, et se rouvrent une fois la salle vidée).

Tools/.venv/bin/python -I Design/effects_v25_herses/generate.py
Planches (une case de large, debout, pied en bas) :
  level_gate_rise.png  24×40, 7 images : la colonne de basalte sort du sol dans la poussière
  level_gate.png       24×40, 6 images en boucle : fissures de magma et rune qui battent
  level_gate_drop.png  24×40, 7 images : elle rentre dans le sol
Écrit les planches ici et dans Assets/Resources/Effects, et leurs entrées dans effects.json.
"""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
EFFECTS = os.path.join(ROOT, "Assets", "Resources", "Effects")
sys.path.insert(0, os.path.join(ROOT, "Design", "effects_v21_entrees"))
from generate_lot1 import Sheet, strip, K1  # noqa: E402

W, H = 24, 40
BAS = ("#1e1a1c", "#3a3236", "#55494c", "#776669")   # basalte : contour, ombre, face, lumière
MAG = ("#7a1e08", "#e0501a", "#ffb040", "#fff0b0")   # magma : sombre, vif, clair, cœur
DUST = "#8a7a6a"


def column(s, top, glow):
    """Une colonne de basalte à pans, sommet en pointe brisée à la rangée `top`, pied en bas de l'image ; fissures et
    rune de magma d'intensité `glow` (0-1)."""
    x0, x1 = 6, 17
    rnd = random.Random(7)
    for y in range(max(0, top), H - 1):
        for x in range(x0, x1 + 1):
            # The top: a broken point.
            peak = top + abs(x - 11.5) * 0.7 + (1 if x in (9, 14) else 0)
            if y < peak:
                continue
            c = BAS[2]
            if x <= x0 + 1:
                c = BAS[3]
            elif x >= x1 - 2:
                c = BAS[1]
            elif x == 11 or x == 12:
                c = BAS[2] if (y // 3) % 2 else BAS[3]
            s.p(x, y, c)
    # Facets: a dark seam down the middle face.
    for y in range(max(0, top + 4), H - 1):
        s.p(13, y, BAS[1])
    # Cracks of magma.
    cracks = [(8, 0.35), (15, 0.55), (10, 0.75)]
    for cx, f in cracks:
        y0 = int(top + (H - top) * f)
        x = cx
        for k in range(6):
            y = y0 + k
            if top + 3 < y < H - 2 and x0 < x < x1:
                s.p(x, y, MAG[1] if glow > 0.5 else MAG[0])
                if glow > 0.75:
                    s.p(x, y - 1, MAG[2])
            x += rnd.choice((-1, 0, 1))
    # The rune: a ring with a stroke, in the middle.
    ry = int(top + (H - top) * 0.45)
    if top + 6 < ry < H - 8:
        lit = [MAG[0], MAG[1], MAG[2], MAG[3]][min(3, int(glow * 4))]
        for a in range(0, 360, 30):
            s.p(11.5 + 2.6 * math.cos(math.radians(a)), ry + 2.6 * math.sin(math.radians(a)), lit)
        for k in range(-2, 3):
            s.p(11 + (k > 0), ry + k, lit)
    # Foot: a darker rim on the ground.
    for x in range(x0 - 1, x1 + 2):
        s.p(x, H - 2, BAS[1])
        s.p(x, H - 1, BAS[0])
    s.outline(K1)


def dust(s, amount, seed):
    rnd = random.Random(seed)
    for _ in range(int(amount * 28)):
        x = rnd.randint(1, W - 2)
        y = H - 1 - int(abs(rnd.gauss(0, 3 * amount + 1)))
        s.p(x, y, DUST, 200)


def rise():
    frames = []
    for i in range(7):
        s = Sheet(W, H)
        t = (i + 1) / 7
        column(s, int(H - 2 - (H - 6) * t), 0.3 * t)
        dust(s, 1 - abs(t - 0.4), i)
        frames.append(s)
    return frames


def loop():
    frames = []
    for i in range(6):
        s = Sheet(W, H)
        column(s, 4, 0.55 + 0.45 * math.sin(i / 6 * math.pi * 2) ** 2)
        frames.append(s)
    return frames


def write(name, frames, durations, looping, palette, effects):
    st = strip(frames)
    st.im.save(os.path.join(HERE, name + ".png"))
    st.im.save(os.path.join(EFFECTS, name + ".png"))
    entry = {"file": name + ".png", "frame_width": W, "frame_height": H, "frame_count": len(frames),
             "frame_duration_ms": durations, "loop": looping, "palette": palette}
    for i, s in enumerate(effects["sheets"]):
        if s["file"] == entry["file"]:
            effects["sheets"][i] = entry
            break
    else:
        effects["sheets"].append(entry)


def main():
    path = os.path.join(EFFECTS, "effects.json")
    with open(path, encoding="utf-8") as f:
        effects = json.load(f)
    palette = list(BAS) + list(MAG) + [DUST]
    up = rise()
    write("level_gate_rise", up, [60] * 7, False, palette, effects)
    write("level_gate", loop(), [120] * 6, True, palette, effects)
    write("level_gate_drop", list(reversed(up)), [60] * 7, False, palette, effects)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(effects, f, ensure_ascii=False, indent=2)
    print("level_gate_rise, level_gate, level_gate_drop")


if __name__ == "__main__":
    main()
