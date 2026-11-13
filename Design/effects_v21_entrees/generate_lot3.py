"""Entrées des donjons, lot 3 : donjon de la clé et portes de quartier (prompt : Design/prompt_entrees.md).

Tools/.venv/bin/python -I Design/effects_v21_entrees/generate_lot3.py Design/effects_v21_entrees
Écrit les 6 planches et les ajoute à effects.json.
"""
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generate_lot1 import Sheet, C, strip, K0, K1, S0, S1, S2, S3, L0, L1, L2, L3, T0, T1, T2  # noqa: E402

MOSS = ("#3a6a2a", "#6a9a3a")
MATERIALS = [
    ("moussue", (S0, S1, S2, S3)),
    ("grès", ("#4a2c14", "#7a4a24", "#b07840", "#e0b070")),
    ("givrée", ("#2a2a3a", "#4a4a64", "#a0b0d0", "#e8f0ff")),
]


def block(s, x0, y0, x1, y1, pal, top=0):
    """Pierre taillée vue de 3/4 : `top` rangées de dessus (claires), puis la face avant."""
    s.rect(x0, y0, x1, y1, pal[2])
    if top:
        s.rect(x0, y0, x1, y0 + top - 1, pal[3])
        s.rect(x0, y0 + top, x1, y0 + top, pal[1])
    s.rect(x0, y0 + top + 1, x0, y1, pal[3])
    s.rect(x1, y0 + top + 1, x1, y1, pal[1])
    s.rect(x0, y1, x1, y1, pal[1])
    s.frame(x0 - 1, y0 - 1, x1 + 1, y1 + 1, pal[0])


# ---------------------------------------------------------------- donjon de la clé (72×72)

def key_cells():
    cells = []
    for y in range(18, 29):
        for x in range(24, 34):
            if 1.6 <= math.hypot(x - 28, y - 23) <= 3.3:
                cells.append((x, y))
    cells += [(x, 23) for x in range(32, 46)]
    cells += [(41, 24), (41, 25), (44, 24), (44, 25), (44, 26)]
    return cells


def rune_cells():
    glyphs = [((0, 0), (1, 1), (2, 0), (1, 2), (1, 3)), ((0, 0), (0, 1), (0, 2), (0, 3), (1, 1), (2, 2)),
              ((1, 0), (0, 1), (2, 1), (0, 2), (2, 2), (1, 3))]
    cells = []
    for k, (gx, gy) in enumerate(((16, 36), (16, 46), (16, 56), (53, 38), (53, 48), (53, 58))):
        cells.append([(gx + dx, gy + dy) for dx, dy in glyphs[k % 3]])
    return cells


def key_dungeon(material):
    name, pal = MATERIALS[material]
    s = Sheet(72, 72)
    # ombre de l'ouverture et escalier qui s'enfonce : marches claires devant, noir au fond
    s.rect(24, 28, 47, 66, K0)
    treads = [(62, pal[2], pal[1]), (58, pal[1], S0 if material != 2 else pal[0]), (54, K1, K0), (50, K1, K0)]
    for k, (y, tr, ri) in enumerate(treads):
        s.rect(24 + k, y, 47 - k, y + 1, tr)
        s.rect(24 + k, y + 2, 47 - k, y + 3, ri)
    for y in range(40, 50):
        for x in range(24, 48):
            if (x + y) % 2 == 0 and y > 46:
                s.p(x, y, K1)
    # seuil
    block(s, 11, 66, 60, 70, pal, top=2)
    # montants
    block(s, 13, 27, 23, 65, pal)
    block(s, 48, 27, 58, 65, pal)
    for x0 in (13, 48):
        for y in (39, 52):
            s.rect(x0, y, x0 + 10, y, pal[1])                # joints des assises
            s.rect(x0 + 1, y + 1, x0 + 9, y + 1, pal[3])
    # dalle posée dessus
    block(s, 6, 13, 65, 27, pal, top=4)
    s.rect(7, 28, 64, 28, K1)                                # ombre portée sous la dalle
    # clé et runes gravées : creux sombre, arête claire en bas à droite
    for x, y in key_cells():
        s.p(x, y, pal[0])
    for x, y in key_cells():
        if s.get(x + 1, y + 1) == C(pal[2]):
            s.p(x + 1, y + 1, pal[3])
    for rune in rune_cells():
        for x, y in rune:
            s.p(x, y, pal[0])
    # matière
    rnd = random.Random(30 + material)
    if name == "moussue":
        for x in range(6, 66):
            if rnd.random() < 0.6:
                s.p(x, 12, MOSS[1])
                s.p(x, 13, MOSS[0] if rnd.random() < 0.5 else MOSS[1])
        for x0 in (13, 48):
            for x in range(x0, x0 + 11):
                h = rnd.choice((1, 2, 3))
                s.rect(x, 66 - h, x, 65, MOSS[1])
                s.p(x, 65 - h, MOSS[0])
        for x, y in ((9, 22), (10, 21), (62, 24), (20, 31), (51, 44)):
            s.p(x, y, MOSS[1])
    elif name == "grès":
        for y in range(15, 27, 3):
            for x in range(8, 64, 5):
                if rnd.random() < 0.4:
                    s.rect(x, y, x + 2, y, pal[3] if y < 17 else pal[1])     # strates du grès
        for x, y in ((12, 68), (13, 68), (58, 69), (59, 68)):
            s.p(x, y, "#f0dca8")
    else:
        for x in range(5, 67):
            h = 2 + (rnd.random() < 0.5)
            s.rect(x, 11, x, 11 + h, "#ffffff")
        for x in range(8, 64, 4):
            n = rnd.choice((1, 2, 3))
            s.rect(x, 28, x, 28 + n, pal[2])
            s.p(x, 28 + n, pal[3])                          # glaçons sous la dalle
        for x, y in ((15, 30), (50, 33), (22, 60)):
            s.p(x, y, "#ffffff")
    return s


def key_glow():
    frames = []
    key = key_cells()
    runes = rune_cells()
    for f in range(8):
        s = Sheet(72, 72)
        ph = (math.sin(2 * math.pi * f / 8 - math.pi / 2) + 1) / 2

        def lit(cells, p):
            core = L2 if p > 0.66 else L1 if p > 0.25 else L0
            if p > 0.4:
                for x, y in cells:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        if (x + dx, y + dy) not in cells:
                            s.p(x + dx, y + dy, L1, 120)
            for x, y in cells:
                s.p(x, y, core)
            if p > 0.85:
                x, y = cells[len(cells) // 2]
                s.p(x, y, L3)

        lit(key, ph)
        for k, rune in enumerate(runes):
            lit(rune, (math.sin(2 * math.pi * (f - k * 0.6) / 8 - math.pi / 2) + 1) / 2)
        frames.append(s)
    return frames


# ---------------------------------------------------------------- porte de quartier (72×72)

AC = (36, 36)          # centre de l'arche
R_OUT, R_IN = 28, 14
SOCK = (36, 15)


def in_arch(x, y):
    d = math.hypot(x - AC[0], y - AC[1])
    if y <= AC[1]:
        return R_IN <= d <= R_OUT and y >= 7
    return (8 <= x <= 21 or 51 <= x <= 64) and y <= 66


def in_opening(x, y):
    if y <= AC[1]:
        return math.hypot(x - AC[0], y - AC[1]) < R_IN
    return 22 <= x <= 50 and y <= 66


def gate_base(socket_lit=False):
    pal = (S0, S1, S2, S3)
    s = Sheet(72, 72)
    for y in range(72):
        for x in range(72):
            if not in_arch(x, y):
                continue
            c = pal[2]
            if not in_arch(x - 1, y - 1) and not in_opening(x - 1, y - 1):
                c = pal[3]
            elif not in_arch(x + 1, y + 1) and not in_opening(x + 1, y + 1):
                c = pal[1]
            elif in_opening(x - 1, y) or in_opening(x, y - 1):
                c = pal[1]                                   # intrados dans l'ombre
            s.p(x, y, c)
    # claveaux : joints rayonnants, assises des montants
    for ang in (-160, -135, -110, -70, -45, -20):
        a = math.radians(ang)
        for r in range(R_IN + 1, R_OUT):
            s.p(AC[0] + math.cos(a) * r, AC[1] + math.sin(a) * r, pal[1])
    for y in (46, 56):
        s.rect(9, y, 20, y, pal[1])
        s.rect(52, y, 63, y, pal[1])
        s.rect(9, y + 1, 20, y + 1, pal[3])
        s.rect(52, y + 1, 63, y + 1, pal[3])
    # socles
    block(s, 6, 63, 23, 69, pal, top=2)
    block(s, 49, 63, 66, 69, pal, top=2)
    # logement en roue dentée, creusé au sommet
    for y in range(72):
        for x in range(72):
            d = math.hypot(x - SOCK[0], y - SOCK[1])
            ang = math.atan2(y - SOCK[1], x - SOCK[0])
            tooth = math.cos(ang * 8) > 0.2
            if d <= 5.2 or (tooth and d <= 7.0):
                s.p(x, y, K1)
            elif d <= 8.2:
                hi = (x - SOCK[0]) + (y - SOCK[1]) > 0      # rebord éclairé côté bas droit (creux)
                s.p(x, y, "#ffffff" if socket_lit else (S3 if hi else S1))
    s.disc(SOCK[0], SOCK[1], 1.4, S1)
    # contour extérieur
    add = []
    for y in range(72):
        for x in range(72):
            if s.px[x, y][3] == 0 and not in_opening(x, y):
                if any(s.get(x + dx, y + dy)[3] == 255 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    add.append((x, y))
    for x, y in add:
        s.p(x, y, S0)
    return s


def veil(s, f=0, ripple=0.0, push=0.0, tear=0.0):
    """Voile gris terne entre les montants ; `tear` : part déchirée au milieu."""
    for y in range(72):
        for x in range(72):
            if not in_opening(x, y):
                continue
            if tear and abs(x - 36) < tear * 15:
                continue
            wave = math.sin(y * 0.5 + f * 1.7) * ripple
            v = (x + wave + y * 0.15) % 6
            c = S2 if v < 1.2 else S1
            d = math.hypot(x - 36, y - 44)
            if push and d < 6 + push * 8:
                c = S3 if d > 3 + push * 6 else S2
            if (x + y) % 2 == 0 and v > 4:
                c = S3 if (y // 3) % 3 == 0 else c
            s.p(x, y, c, 215)


def swirl(s, f, only=None):
    for y in range(72):
        for x in range(72):
            if not in_opening(x, y) or (only and not only(x, y)):
                continue
            dx, dy = x - 36, (y - 42) * 1.15
            r = math.hypot(dx, dy)
            th = math.atan2(dy, dx) + r * 0.28 - f * 2 * math.pi / 8
            v = math.sin(3 * th)
            c = T2 if v > 0.55 else T1 if v > -0.2 else T0
            if r < 3:
                c = "#ffffff"
            elif r < 6 and v > 0:
                c = T2
            s.p(x, y, c, 255 if r < 15 else 230)


def gear(s, cx, cy, rot):
    for y in range(int(cy) - 8, int(cy) + 9):
        for x in range(int(cx) - 8, int(cx) + 9):
            d = math.hypot(x - cx, y - cy)
            ang = math.atan2(y - cy, x - cx) - rot
            tooth = math.cos(ang * 8) > 0.2
            if d <= 1.4:
                s.p(x, y, K1)
            elif d <= 5.0 or (tooth and d <= 6.6):
                c = T1
                if 2.8 <= d <= 3.8:
                    c = L1                                   # cercle d'or
                elif (x - cx) + (y - cy) < -3:
                    c = T2
                elif (x - cx) + (y - cy) > 3:
                    c = T0
                s.p(x, y, c)


def gate_closed():
    s = gate_base()
    veil(s)
    return s


def gate_open():
    frames = []
    for f in range(8):
        s = gate_base()
        swirl(s, f)
        gear(s, SOCK[0], SOCK[1], f * (2 * math.pi / 8) / 8)
        frames.append(s)
    return frames


def flash(s, cx, cy, size, col=L3):
    for t in range(-size, size + 1):
        s.p(cx + t, cy, col)
        s.p(cx, cy + t, col)
    for t in range(-(size // 2), size // 2 + 1):
        s.p(cx + t, cy + t, L2)
        s.p(cx + t, cy - t, L2)


def gate_opening():
    frames = []
    for f in range(10):
        s = gate_base()
        if f < 4:
            veil(s, f)
            gear(s, SOCK[0], SOCK[1] - (12 - f * 4), 0)
        elif f == 4:
            veil(s, f)
            gear(s, SOCK[0], SOCK[1], 0)
            flash(s, SOCK[0], SOCK[1], 9)
        else:
            tear = min(1.0, (f - 4) / 3)
            swirl(s, f, only=lambda x, y, t=tear: abs(x - 36) < t * 15)
            if tear < 1:
                veil(s, f, ripple=1.5, tear=tear)
            gear(s, SOCK[0], SOCK[1], (f - 4) * (2 * math.pi / 8) / 8)
            if f == 5:
                flash(s, SOCK[0], SOCK[1], 5)
            if f in (7, 8):
                flash(s, 36, 42, 6 if f == 7 else 3, "#ffffff")
        frames.append(s)
    return frames


def gate_refuse():
    frames = []
    for f in range(5):
        s = gate_base(socket_lit=f % 2 == 0)
        veil(s, f, ripple=2.5 if f < 4 else 1, push=[0.3, 1, 0.7, 0.3, 0][f])
        if f in (1, 2):
            pts = ((36, 32), (34, 36), (38, 39), (35, 43), (39, 47), (36, 51))
            for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
                for t in range(6):
                    x, y = x0 + (x1 - x0) * t / 5, y0 + (y1 - y0) * t / 5
                    s.p(x, y, "#ffffff" if f == 1 else S3)
                    s.p(x + 1, y, S2)
        frames.append(s)
    return frames


def main(out):
    stone = [S0, S1, S2, S3]
    sheets = {
        "key_dungeon": ([key_dungeon(i) for i in range(3)], [0] * 3, False,
                        sorted({c for _, p in MATERIALS for c in p}) + list(MOSS) + [K0, K1, "#ffffff"]),
        "key_dungeon_glow": (key_glow(), [120] * 8, True, [L0, L1, L2, L3]),
        "quarter_gate": ([gate_closed()], [0], False, stone + [K1]),
        "quarter_gate_open": (gate_open(), [100] * 8, True, stone + [K1, T0, T1, T2, L1, "#ffffff"]),
        "quarter_gate_opening": (gate_opening(), [80] * 10, False, stone + [K1, T0, T1, T2, L1, L2, L3]),
        "quarter_gate_refuse": (gate_refuse(), [70] * 5, False, stone + [K1, "#ffffff"]),
    }
    path = os.path.join(out, "effects.json")
    with open(path, encoding="utf-8") as f:
        effects = json.load(f)
    for key, (frames, durations, loop, palette) in sheets.items():
        strip(frames).im.save(os.path.join(out, key + ".png"))
        effects[key] = {
            "file": key + ".png",
            "frame_width": frames[0].w,
            "frame_height": frames[0].h,
            "frame_count": len(frames),
            "frame_duration_ms": durations,
            "loop": loop,
            "palette": palette,
        }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(effects, f, indent=2, ensure_ascii=False)
        f.write("\n")


if __name__ == "__main__":
    main(sys.argv[1])
