"""Sanctuaires des gardiens des fragments d'Engrenage du Temps (GDD « Quart du volcan », gardiens des fragments).

Tools/.venv/bin/python -I Design/effects_v24_sanctuaires/generate.py
Planches (vue de 3/4, contour sombre) :
  fragment_shrine.png      72×72, 3 images = 3 matières (moussue, grès, givrée : comme key_dungeon, EntranceArt.KeyMaterialOf)
  fragment_shrine_glow.png 72×72, 8 images en boucle : les runes et le logement du fragment qui s'allument en turquoise
  entrance_icons.png       une 9e icône ajoutée (un tiers d'engrenage doré, cœur turquoise : EntranceArt.Icon.Shrine)
Dans la langue du dolmen à engrenage et du rempart : pierre grise, bronze vert-de-gris riveté, runes turquoise.
Écrit les planches ici et dans Assets/Resources/Effects, et leurs entrées dans Assets/Resources/Effects/effects.json.
"""
import json
import math
import os
import random
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
EFFECTS = os.path.join(ROOT, "Assets", "Resources", "Effects")
sys.path.insert(0, os.path.join(ROOT, "Design", "effects_v21_entrees"))
from generate_lot1 import Sheet, C, strip, shade, K0, K1, L0, L1, L2, L3, T0, T1, T2  # noqa: E402
from generate_lot3 import MATERIALS, MOSS, block  # noqa: E402



# ---------------------------------------------------------------- l'emblème : un tiers d'engrenage

def fragment_cells(cx, cy, r_out, r_in, hole, teeth=9, start=-150, end=-30):
    """Les cases d'un tiers d'engrenage (secteur de start à end degrés, y vers le bas) centré en cx, cy."""
    cells = []
    for y in range(int(cy - r_out - 1), int(cy + r_out + 2)):
        for x in range(int(cx - r_out - 1), int(cx + r_out + 2)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            ang = math.degrees(math.atan2(dy, dx))
            if not (start <= ang <= end) or d <= hole:
                continue
            tooth = math.cos(math.radians(ang) * teeth) > 0.15
            if d <= r_in or (tooth and d <= r_out):
                cells.append((x, y))
    return cells




# ---------------------------------------------------------------- le sanctuaire (72×72)
# Dans la langue du dolmen à engrenage et du rempart (Design/effects_v22_remparts) : pierre grise, bandes de bronze
# vert-de-gris rivetées de cuivre, dalle posée dessus, socle de blocs sombres, runes qui s'allument en turquoise.

ST = ("#3a3836", "#6a6560", "#9a948c", "#c8c0b4")   # pierre : contour, ombre, face, lumière
VOID = "#1c1b1a"
BR = ("#3a4a3a", "#5a7a5a", "#8ab08a")               # bronze vert-de-gris : contour, corps, reflet
RIVET = "#b07840"
RUNE = "#4a5a6a"                                     # runes éteintes
GOLD = ("#a07010", "#f0c030", "#fff0a0")
TQ = ("#0a6a72", "#1ec8c8", "#a0fff0", "#4ab6b3")    # turquoise : sombre, vif, clair, halo

def gear_cells(cx, cy, r_out, r_in, hole, teeth):
    """Les cases d'un engrenage entier centré en cx, cy."""
    return fragment_cells(cx, cy, r_out, r_in, hole, teeth, start=-180, end=180)


SOCKET = gear_cells(36, 15.5, 7.5, 5.6, 0, teeth=8)            # le logement : un engrenage entier, comme au dolmen
THIRD = fragment_cells(36, 15.5, 7.5, 5.6, 1.6, teeth=8, start=-150, end=-30)  # le tiers que garde le gardien
RUNES = [  # (x, y) des runes : trois sur chaque pilier, deux sur la dalle
    [(12, 33), (12, 34), (12, 35), (13, 34), (14, 33), (14, 35)],
    [(12, 41), (13, 41), (14, 41), (13, 42), (13, 43)],
    [(12, 49), (12, 50), (13, 51), (14, 50), (14, 49)],
    [(56, 33), (57, 34), (58, 33), (57, 35)],
    [(56, 41), (56, 42), (56, 43), (57, 42), (58, 41), (58, 43)],
    [(56, 49), (57, 49), (58, 49), (57, 50), (57, 51)],
    [(18, 15), (19, 14), (20, 15), (19, 16)],
    [(52, 15), (53, 14), (53, 16), (54, 15)],
]


def stone_block(s, x0, y0, x1, y1):
    """Bloc de pierre du dolmen : face grise, arête claire à gauche et en haut, ombre à droite et en bas, contour."""
    s.rect(x0, y0, x1, y1, ST[2])
    s.rect(x0, y0, x1, y0, ST[3])
    s.rect(x0, y0, x0, y1, ST[3])
    s.rect(x1, y0 + 1, x1, y1, ST[1])
    s.rect(x0 + 1, y1, x1, y1, ST[1])
    s.frame(x0 - 1, y0 - 1, x1 + 1, y1 + 1, ST[0])


def bronze_band(s, x0, x1, y):
    """Bande de bronze rivetée de 3 rangées (y, y+1, y+2), comme celles du dolmen et du rempart."""
    s.rect(x0, y, x1, y, BR[0])
    s.rect(x0, y + 1, x1, y + 1, BR[1])
    s.rect(x0, y + 2, x1, y + 2, BR[0])
    for x in range(x0 + 1, x1, 2):
        if s.get(x, y + 1) == C(BR[1]) and (x - x0) % 2 == 1:
            s.p(x, y + 1, BR[2] if (x - x0) % 6 else BR[1])
    for x in range(x0 + 2, x1 - 1, 5):
        s.p(x, y + 1, RIVET)


def shrine(material):
    name, _ = MATERIALS[material]
    s = Sheet(72, 72)
    # socle de trois blocs sombres
    for x0, x1 in ((5, 25), (27, 45), (47, 67)):
        s.rect(x0, 63, x1, 69, ST[1])
        s.rect(x0, 63, x1, 63, ST[2])
        s.frame(x0 - 1, 62, x1 + 1, 70, ST[0])
    # l'ouverture et l'escalier qui s'enfonce entre les piliers
    s.rect(21, 30, 50, 61, VOID)
    for k, y in enumerate((58, 54, 50, 46, 42)):
        s.rect(21 + k, y, 50 - k, y, ST[1] if k < 2 else ST[0])
        s.rect(21 + k, y + 1, 50 - k, y + 1, ST[2] if k == 0 else ST[0] if k < 3 else VOID)
    # deux piliers cerclés de bronze, leurs runes éteintes
    for x0 in (8, 52):
        stone_block(s, x0, 29, x0 + 11, 61)
        bronze_band(s, x0 - 1, x0 + 12, 30)
        bronze_band(s, x0 - 1, x0 + 12, 56)
        s.rect(x0 + 8, 52, x0 + 9, 52, ST[1])                 # petite entaille, comme sur le dolmen
    for rune in RUNES[:6]:
        for x, y in rune:
            s.p(x, y, RUNE)
    # linteau de bronze sous la dalle, au-dessus de l'ouverture
    bronze_band(s, 20, 51, 27)
    # la dalle posée dessus, le logement du fragment au milieu
    stone_block(s, 4, 7, 67, 24)
    s.rect(5, 25, 66, 25, ST[0])                               # ombre portée de la dalle
    for x, y in ((12, 11), (13, 11), (14, 11), (46, 13), (47, 13), (60, 10), (60, 11), (60, 12)):
        s.p(x, y, ST[1])                                       # éclats et fentes, comme sur le dolmen
    s.p(30, 9, ST[3])
    for x, y in SOCKET:                                         # le logement : creux noir cerclé de bronze
        s.p(x, y, VOID)
    rim = [(x + dx, y + dy) for x, y in SOCKET for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
           if (x + dx, y + dy) not in SOCKET]
    for x, y in rim:
        s.p(x, y, BR[2] if y < 15.5 else BR[1])
    for x, y in THIRD:                                          # le tiers gardé : dessiné en creux plus clair
        if (x + y) % 2 == 0:
            s.p(x, y, "#2c2a28")
    for rune in RUNES[6:]:
        for x, y in rune:
            s.p(x, y, RUNE)
    # matière, discrète : mousse, sable ou givre sur la dalle et le socle
    rnd = random.Random(40 + material)
    if name == "moussue":
        for x in range(5, 67):
            if rnd.random() < 0.5:
                s.p(x, 6, MOSS[1])
                if rnd.random() < 0.4:
                    s.p(x, 7, MOSS[0])
            if rnd.random() < 0.35:
                s.p(x, 62, MOSS[1])
    elif name == "grès":
        for x in range(5, 67):
            if rnd.random() < 0.35:
                s.p(x, 6, "#e0c890")
            if rnd.random() < 0.3:
                s.p(x, 69, "#d8b878")
    else:
        for x in range(4, 68):
            s.p(x, 6, "#ffffff")
            if rnd.random() < 0.5:
                s.p(x, 7, "#e8f0ff")
        for x in range(6, 66, 5):
            s.rect(x, 26, x, 26 + rnd.choice((1, 2)), "#c8d8f0")  # glaçons sous la dalle
    return s


def shrine_glow():
    """Les runes et le logement s'allument en turquoise, l'une après l'autre, puis tous ensemble."""
    frames = []
    for f in range(8):
        s = Sheet(72, 72)
        ph = (math.sin(2 * math.pi * f / 8 - math.pi / 2) + 1) / 2
        rim = [(x + dx, y + dy) for x, y in SOCKET for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
               if (x + dx, y + dy) not in SOCKET]
        for x, y in rim:
            s.p(x, y, TQ[2] if ph > 0.7 else TQ[1])
        if ph > 0.35:
            for x, y in rim:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    if (x + dx, y + dy) not in rim and (x + dx, y + dy) not in SOCKET:
                        s.p(x + dx, y + dy, TQ[3], 90)
        for x, y in THIRD:                                      # le tiers gardé luit, or et turquoise
            d = math.hypot(x + 0.5 - 36, y + 0.5 - 15.5)
            if d > 4.2:
                s.p(x, y, GOLD[2] if ph > 0.8 and d > 6 else GOLD[1] if ph > 0.3 else GOLD[0], 230)
            elif (x + y + f) % 3:
                s.p(x, y, TQ[1] if ph > 0.4 else TQ[0], 220)
        for k, rune in enumerate(RUNES):
            p = (math.sin(2 * math.pi * (f - k * 0.5) / 8 - math.pi / 2) + 1) / 2
            for x, y in rune:
                s.p(x, y, TQ[2] if p > 0.75 else TQ[1] if p > 0.3 else TQ[0])
        frames.append(s)
    return frames


# ---------------------------------------------------------------- l'icône (16×16) : un tiers d'engrenage doré, cœur turquoise

def icon_fragment():
    """Un engrenage gris dont un tiers est doré, cœur turquoise : le fragment que garde le sanctuaire."""
    s = Sheet(16, 16)
    whole = gear_cells(7.5, 8.5, 7.2, 5.2, 1.7, teeth=8)
    third = set(fragment_cells(7.5, 8.5, 7.2, 5.2, 1.7, teeth=8, start=-150, end=-30))
    for x, y in whole:
        s.p(x, y, GOLD[1] if (x, y) in third else ST[1])
    for x, y in third:
        if math.hypot(x + 0.5 - 7.5, y + 0.5 - 8.5) <= 4.2:
            s.p(x, y, TQ[1])
    shade(s, GOLD[1], GOLD[2], GOLD[0])
    shade(s, ST[1], ST[2], ST[0])
    s.outline(K1)
    return s


# ---------------------------------------------------------------- écriture

def write(name, frames, durations, loop, palette, effects):
    st = strip(frames)
    st.im.save(os.path.join(HERE, name + ".png"))
    st.im.save(os.path.join(EFFECTS, name + ".png"))
    entry = {"file": name + ".png", "frame_width": frames[0].w, "frame_height": frames[0].h, "frame_count": len(frames),
             "frame_duration_ms": durations, "loop": loop, "palette": palette}
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
    write("fragment_shrine", [shrine(i) for i in range(3)], [0] * 3, False, list(ST) + list(BR) + [VOID, RIVET, RUNE] + list(MOSS), effects)
    write("fragment_shrine_glow", shrine_glow(), [110] * 8, True, list(TQ), effects)
    # The icons: the 8 of the entrances kept as they are, the fragment's added after them (9th).
    icons = Image.open(os.path.join(EFFECTS, "entrance_icons.png")).convert("RGBA")
    entry = next(s for s in effects["sheets"] if s["file"] == "entrance_icons.png")
    count = min(8, icons.width // 16)
    out = Image.new("RGBA", (16 * (count + 1), 16), (0, 0, 0, 0))
    out.paste(icons.crop((0, 0, 16 * count, 16)), (0, 0))
    out.alpha_composite(icon_fragment().im, (16 * count, 0))
    out.save(os.path.join(EFFECTS, "entrance_icons.png"))
    out.save(os.path.join(HERE, "entrance_icons.png"))
    entry["frame_count"] = count + 1
    entry["frame_duration_ms"] = [0] * (count + 1)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(effects, f, ensure_ascii=False, indent=2)
    print("fragment_shrine, fragment_shrine_glow, entrance_icons (+1)")


if __name__ == "__main__":
    main()
