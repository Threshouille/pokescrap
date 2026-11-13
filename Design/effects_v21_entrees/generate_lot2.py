"""Entrées des donjons, lot 2 : cachettes des escaliers secrets et bordures par biome (prompt : Design/prompt_entrees.md).

Tools/.venv/bin/python -I Design/effects_v21_entrees/generate_lot2.py Design/effects_v21_entrees
Écrit les 5 planches, les ajoute à effects.json, et sous preview/ les sols et l'arbre de la présentation.
"""
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_lot1 as l1                          # noqa: E402
from generate_lot1 import Sheet, C, strip, shade, puff, K0, K1, S0, S1, S2, S3, L0, L1, L2, D0, D1, D2  # noqa: E402

OUTLINE = "#2a1810"
WOOD = ("#4a2c18", "#7a4a28", "#a87040")
EARTH = ("#3a2414", "#5a3a20", "#8a5a30")
MOSS = ("#3a6a2a", "#6a9a3a", "#9ac858")
LEAVES = ("#8a4a20", "#c07030", "#e0a050", "#a85a28")
CAP = ("#902820", "#c03828", "#e05838")
STEM = "#e8dcc4"
ROCK = ("#2a1c14", "#5a4030", "#7a5838", "#a8805a", "#c8a478")

CX, CY = 24, 25          # centre de la cachette


# ---------------------------------------------------------------- clairière

def ring(s):
    """Cercle de champignons et de pierres moussues, identique pour les deux états."""
    n = 10
    for k in range(n):
        ang = 2 * math.pi * k / n - math.pi / 2 + 0.2
        x = round(CX + math.cos(ang) * 18.5)
        y = round(CY + math.sin(ang) * 15.5) + 1
        if k % 3 == 1:
            for dy in range(-1, 2):
                for dx in range(-3, 3):
                    if (dx + 0.5) ** 2 / 9 + dy ** 2 / 2.2 <= 1:
                        s.p(x + dx, y + dy, S2)
            s.rect(x - 2, y + 1, x + 1, y + 1, S1)
            s.rect(x - 2, y - 2, x + 1, y - 2, MOSS[1])
            s.p(x - 1, y - 2, MOSS[2])
            s.p(x + 1, y - 1, MOSS[0])
            s.p(x - 3, y, S3)
        else:
            big = k % 2 == 0
            s.rect(x, y - 1, x + 1, y, STEM)
            s.p(x + 1, y, D1)
            s.rect(x - 1, y - 3, x + 2, y - 2, CAP[1])
            if big:
                s.rect(x - 2, y - 2, x + 3, y - 2, CAP[1])
                s.rect(x - 1, y - 4, x + 2, y - 4, CAP[1])
                s.p(x + 3, y - 2, CAP[0])
            s.rect(x, y - 4 if big else y - 3, x + 1, y - 4 if big else y - 3, CAP[2])
            s.rect(x - 1, y - 2, x + 2, y - 2, CAP[0]) if not big else None
            s.p(x - 1 if big else x, y - 3, "#ffffff")
            if big:
                s.p(x + 2, y - 3, "#ffffff")


def hole_mask(rx, ry, cx=CX, cy=CY):
    return lambda x, y: ((x + 0.5 - cx - 0.5) / rx) ** 2 + ((y + 0.5 - cy - 0.5) / ry) ** 2 <= 1


def clearing_open():
    s = Sheet(48, 48)
    outer, inner = hole_mask(12, 10), hole_mask(10, 8)
    for y in range(48):
        for x in range(48):
            if outer(x, y):
                s.p(x, y, EARTH[1])
                if not outer(x - 1, y - 1):
                    s.p(x, y, EARTH[2])
                elif not outer(x + 1, y + 1):
                    s.p(x, y, EARTH[0])
    # trou : paroi du fond, puis marches de bois qui descendent
    top = CY - 8
    for y in range(48):
        for x in range(48):
            if not inner(x, y):
                continue
            if y < top + 4:
                s.p(x, y, EARTH[0])
                continue
            k = (y - (top + 4)) // 3
            r = (y - (top + 4)) % 3
            treads = [WOOD[2], WOOD[1], WOOD[0], K1, K1]
            risers = [WOOD[1], WOOD[0], K1, K0, K0]
            c = K0 if k >= len(treads) else (treads[k] if r == 0 else risers[k])
            # les bouts de planche s'enfoncent dans l'ombre sur les côtés
            if not inner(x - 3, y) or not inner(x + 3, y):
                c = K1 if k < 2 else K0
            s.p(x, y, c)
    for k in range(4):
        y = top + 3 + 3 * k
        for x in (CX - 4, CX + 4):
            if inner(x, y):
                s.p(x, y + 1, WOOD[0] if k < 2 else K1)     # clous et jointures des planches
    # racines qui pendent dans le trou
    for path in (((13, 21), (14, 23), (15, 25), (15, 27), (16, 29)),
                 ((34, 20), (33, 22), (33, 24), (32, 26)),
                 ((20, 16), (21, 18), (21, 20))):
        for i, (x, y) in enumerate(path):
            s.p(x, y, WOOD[1])
            s.p(x - 1, y, WOOD[2]) if i % 2 == 0 else None
    ring(s)
    s.outline(OUTLINE)
    return s


def clearing_hidden():
    s = Sheet(48, 48)
    rnd = random.Random(11)
    outer = hole_mask(12, 10)
    for y in range(48):
        for x in range(48):
            if not outer(x, y):
                continue
            v = rnd.random()
            c = LEAVES[0] if v < 0.3 else LEAVES[3] if v < 0.55 else LEAVES[1] if v < 0.8 else LEAVES[2] if v < 0.9 else MOSS[1]
            if not outer(x + 1, y + 1) or not outer(x + 2, y + 2):
                c = LEAVES[0] if v < 0.7 else EARTH[1]       # bord du tas, dans l'ombre
            s.p(x, y, c)
    # quelques feuilles entières et touffes de mousse posées dessus
    for x, y in ((18, 21), (27, 19), (30, 27), (20, 29), (24, 24), (15, 26)):
        s.rect(x, y, x + 1, y, LEAVES[2])
        s.p(x + 1, y + 1, LEAVES[1])
        s.p(x, y + 1, LEAVES[0])
    for x, y in ((22, 18), (31, 23), (17, 24)):
        s.rect(x, y, x + 2, y, MOSS[1])
        s.p(x + 1, y - 1, MOSS[2])
    ring(s)
    s.outline(OUTLINE)
    return s


# ---------------------------------------------------------------- rocher fissuré

CRACK = ((24, 13), (22, 19), (26, 24), (23, 30), (25, 37), (24, 44))


def crack_x(y):
    for (x0, y0), (x1, y1) in zip(CRACK, CRACK[1:]):
        if y0 <= y <= y1:
            return round(x0 + (x1 - x0) * (y - y0) / max(1, y1 - y0))
    return None


def rock_mask():
    blobs = ((24, 31, 19.5, 14), (17, 23, 10, 10), (30, 21, 11, 11), (24, 17, 8, 7))
    return lambda x, y: y <= 45 and any(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1 for cx, cy, rx, ry in blobs)


def rock_body(s, mask, dx=0, dy=0, keep=None):
    rnd = random.Random(5)
    for y in range(48):
        for x in range(48):
            if not mask(x, y) or (keep and not keep(x, y)):
                continue
            v = (x - 24) * 0.45 + (y - 24) * 0.9 + rnd.uniform(-2, 2)
            if not mask(x - 1, y - 1):
                c = ROCK[4]
            elif v < -9:
                c = ROCK[3]
            elif v < 4:
                c = ROCK[2]
            elif v < 13:
                c = ROCK[1]
            else:
                c = ROCK[0]
            if y >= 43:
                c = ROCK[1] if y == 43 else ROCK[0]          # pied posé au sol
            s.p(x + dx, y + dy, c)
    # facettes : quelques arêtes claires
    for x, y in ((14, 25), (15, 24), (16, 23), (31, 15), (32, 14), (33, 15), (20, 34), (21, 33)):
        if mask(x, y):
            s.p(x + dx, y + dy, ROCK[3])


def draw_crack(s, wide, bright=False):
    for y in range(CRACK[0][1], 45):
        x = crack_x(y)
        if wide:
            s.p(x - 1, y, OUTLINE)
            s.p(x + 1, y, OUTLINE)
            s.p(x, y, L2 if bright else (L1 if y % 3 else L0))
        else:
            s.p(x, y, OUTLINE if y % 2 else L0)
    if wide:
        for path in (((27, 25), (29, 26), (31, 27), (33, 27)), ((22, 31), (20, 32), (18, 33), (16, 35)),
                     ((23, 18), (20, 17), (18, 16))):
            for i, (x, y) in enumerate(path):
                s.p(x, y, OUTLINE)
                if i < len(path) - 1:
                    s.p(x, y + 1, L0)


def rock_state(stage):
    s = Sheet(48, 48)
    m = rock_mask()
    if stage < 2:
        rock_body(s, m)
        draw_crack(s, stage == 1)
    else:
        # presque brisé : la moitié droite se détache et penche, morceaux au sol
        rock_body(s, m, keep=lambda x, y: crack_x(y) is None or x < crack_x(y) - 1)
        rock_body(s, m, dx=2, dy=1, keep=lambda x, y: crack_x(y) is not None and x > crack_x(y) + 1 and y < 44)
        for y in range(CRACK[0][1], 45):
            x = crack_x(y)
            s.p(x, y, K1)
            s.p(x + 1, y, L1 if y % 2 else K1)
            s.p(x + 2, y, K1)
        for x, y in ((9, 44), (39, 43), (36, 45)):
            s.rect(x, y, x + 2, y + 1, ROCK[2])
            s.p(x, y, ROCK[3])
            s.p(x + 2, y + 1, ROCK[0])
    s.outline(OUTLINE)
    return s


def chunk(s, x, y, r):
    s.disc(x, y, r + 1, OUTLINE)
    s.disc(x, y, r, ROCK[2])
    s.p(x - r * 0.5, y - r * 0.5, ROCK[4])
    s.p(x + r * 0.5, y + r * 0.5, ROCK[0])
    if r >= 2:
        s.p(x - r * 0.5 + 1, y - r * 0.5, ROCK[3])


def rock_break():
    frames = []
    rnd = random.Random(9)
    pieces = [(rnd.uniform(0, 2 * math.pi), rnd.uniform(0.7, 1.3), rnd.choice((1, 2, 2, 3))) for _ in range(9)]
    for f in range(7):
        s = Sheet(48, 48)
        if f == 0:
            rock_body(s, rock_mask(), dx=0, dy=0)
            draw_crack(s, True, bright=True)
            s.outline(OUTLINE)
        else:
            if f <= 4:
                for k in range(8):
                    ang = 2 * math.pi * k / 8 + 0.2
                    r = 4 + f * 3.5
                    puff(s, 24 + math.cos(ang) * r, 32 + math.sin(ang) * r * 0.6, [0, 6, 6, 5, 4][f], 179,
                         0 if f < 4 else 2)
            elif f == 5:
                for k in range(8):
                    ang = 2 * math.pi * k / 8 + 0.2
                    puff(s, 24 + math.cos(ang) * 19, 32 + math.sin(ang) * 11, 3, 179, 3)
            for ang, sp, r in pieces:
                t = f / 6
                dist = 4 + 20 * sp * t
                x = 24 + math.cos(ang) * dist
                y = 30 + math.sin(ang) * dist * 0.6 - 14 * t + 22 * t * t
                if f >= 5:
                    y = min(y, 45)                              # retombés au sol
                rr = r + (2 if f == 1 else 1 if f == 2 else 0) if f < 4 else max(1, r - 1)
                chunk(s, x, y, rr)
        frames.append(s)
    return frames


# ---------------------------------------------------------------- bordures par biome

BIOMES = [
    ("herbe", ("#2e2c22", "#5a5a44", "#8a8a6c", "#b8b496")),
    ("sable", ("#4a2c14", "#7a4a24", "#b07840", "#e0b070")),
    ("roche", (ROCK[0], ROCK[1], ROCK[3], ROCK[4])),
    ("volcan", ("#141014", "#2e262c", "#4a3e44", "#6e6066")),
    ("glace", ("#22304e", "#4a6a9c", "#8eb0dc", "#e8f0ff")),
    ("ruines", ("#30323a", "#5a5e66", "#8a9098", "#bec4ca")),
]


def in_hole(x, y):
    return l1.HX0 <= x <= l1.HX1 and l1.HY0 <= y <= l1.HY1


def rim_biome(i):
    name, pal = BIOMES[i]
    s = l1.stairs_down()
    rnd = random.Random(20 + i)
    remap = {C(S0): C(pal[0]), C(S1): C(pal[1]), C(S2): C(pal[2]), C(S3): C(pal[3])}
    rim_cells = []
    for y in range(48):
        for x in range(48):
            if in_hole(x, y):
                continue
            c = s.px[x, y]
            if c in remap:
                s.px[x, y] = remap[c]
                rim_cells.append((x, y))
    top = [(x, y) for x, y in rim_cells if y <= l1.OUT0 + 1]
    if name == "herbe":
        for x in range(l1.OUT0, l1.OUT1 + 1):
            if rnd.random() < 0.55:
                s.p(x, l1.OUT0 - 1, MOSS[0])
                s.p(x, l1.OUT0, MOSS[1])
                if rnd.random() < 0.4:
                    s.p(x, l1.OUT0 - 2, MOSS[2])
        for x, y in rnd.sample(rim_cells, 26):
            s.p(x, y, MOSS[1] if rnd.random() < 0.6 else MOSS[2])
        for x, y in ((38, 9), (39, 11), (39, 13), (38, 15), (37, 17), (36, 19), (36, 21)):
            s.p(x, y, WOOD[1])
            s.p(x + 1, y, WOOD[2])                           # racine qui court sur le rebord
    elif name == "sable":
        for x, y in ((6, 39), (7, 39), (8, 39), (6, 38), (7, 38), (6, 37), (40, 7), (41, 7), (41, 8)):
            s.p(x, y, "#f0dca8")
        for x, y in rnd.sample(rim_cells, 14):
            s.p(x, y, "#e0b070" if rnd.random() < 0.5 else "#7a4a24")
    elif name == "roche":
        for path in (((8, 14), (9, 15), (9, 16), (8, 17)), ((31, 37), (32, 38), (33, 38))):
            for x, y in path:
                s.p(x, y, ROCK[0])
    elif name == "volcan":
        joints = [(x, y) for x, y in rim_cells if s.px[x, y] == C(pal[1])]
        for x, y in rnd.sample(joints, min(16, len(joints))):
            s.p(x, y, "#d03018" if rnd.random() < 0.6 else "#ff8a30")
        for x, y in ((12, 42), (26, 42), (36, 42)):
            s.p(x, y, "#ff8a30")
    elif name == "glace":
        for x in range(l1.OUT0 + 1, l1.OUT1):
            h = 2 + (rnd.random() < 0.5)
            s.rect(x, l1.OUT0 - 1, x, l1.OUT0 - 1 + h, "#ffffff")
            s.p(x, l1.OUT0 - 1 + h + 1, "#e8f0ff")
        for x in range(l1.HX0 + 1, l1.HX1, 3):
            n = rnd.choice((1, 2, 3))
            s.rect(x, l1.HY0, x, l1.HY0 + n - 1, "#a0c0e8")
            s.p(x, l1.HY0 + n - 1, "#e8f0ff")                # stalactites de glace
        for x, y in ((7, 20), (40, 30), (20, 38)):
            s.p(x, y, "#ffffff")
    elif name == "ruines":
        glyphs = [((0, 0), (1, 0), (2, 0), (1, 1), (1, 2)), ((0, 0), (0, 1), (0, 2), (1, 1), (2, 0), (2, 2)),
                  ((0, 1), (1, 0), (2, 1), (1, 2))]
        for k, (gx, gy) in enumerate(((10, 7), (19, 7), (28, 7), (7, 22), (38, 20), (23, 37), (34, 37))):
            for dx, dy in glyphs[k % 3]:
                s.p(gx + dx, gy + dy, pal[0])
                s.p(gx + dx + 1, gy + dy + 1, pal[3]) if (dx, dy) == (0, 0) else None
    return s


# ---------------------------------------------------------------- présentation (non livré)

def tree():
    s = Sheet(32, 44)
    s.rect(14, 30, 17, 41, WOOD[1])
    s.rect(14, 30, 14, 41, WOOD[2])
    s.rect(17, 30, 17, 41, WOOD[0])
    s.rect(12, 41, 19, 41, WOOD[0])
    for cx, cy, r in ((16, 15, 12), (9, 20, 7.5), (23, 20, 7.5), (16, 24, 8), (16, 8, 7)):
        s.disc(cx, cy, r, "#3c8a30")
    for y in range(44):
        for x in range(32):
            if s.px[x, y] == C("#3c8a30"):
                if math.hypot(x - 11, y - 9) < 8:
                    s.p(x, y, "#6ac048")
                elif math.hypot(x - 20, y - 26) < 7 or y > 27:
                    s.p(x, y, "#2a6020")
    for x, y in ((9, 8), (10, 7), (13, 5), (6, 17)):
        s.p(x, y, "#9ae070")
    s.outline("#14280e")
    return s


def ground(base, dark, light, seed, extra=None):
    rnd = random.Random(seed)
    s = Sheet(32, 32)
    s.rect(0, 0, 31, 31, base)
    for _ in range(46):
        x, y = rnd.randrange(32), rnd.randrange(32)
        s.p(x, y, dark)
        s.p(x + 1, y, dark)
        s.p(x, y - 1, light)
    if extra:
        extra(s, rnd)
    return s


def embers(s, rnd):
    for _ in range(5):
        x, y = rnd.randrange(32), rnd.randrange(32)
        s.p(x, y, "#d03018")
        s.p(x + 1, y, "#ff8a30")


def slabs(s, rnd):
    for k in range(0, 32, 16):
        s.rect(0, k, 31, k, "#6a6e76")
        s.rect(k + (8 if k else 0), 0, k + (8 if k else 0), 31, "#6a6e76")


def main(out):
    sheets = {
        "secret_clearing": ([clearing_open()], [0], False, True,
                            list(WOOD) + list(EARTH) + list(MOSS) + list(CAP) + [STEM, K0, K1, OUTLINE]),
        "secret_clearing_hidden": ([clearing_hidden()], [0], False, True,
                                   list(LEAVES) + list(MOSS) + list(CAP) + [STEM, OUTLINE]),
        "secret_rock": ([rock_state(0), rock_state(1), rock_state(2)], [0, 0, 0], False, False,
                        list(ROCK) + [L0, L1, OUTLINE]),
        "secret_rock_break": (rock_break(), [60] * 7, False, False, list(ROCK) + [D0, D1, D2, L2, OUTLINE]),
        "entrance_rim_biomes": ([rim_biome(i) for i in range(6)], [0] * 6, False, True,
                                sorted({c for _, p in BIOMES for c in p}) + [S0, S1, S2, S3, K0, K1]),
    }
    path = os.path.join(out, "effects.json")
    with open(path, encoding="utf-8") as f:
        effects = json.load(f)
    for key, (frames, durations, loop, below, palette) in sheets.items():
        strip(frames).im.save(os.path.join(out, key + ".png"))
        entry = {
            "file": key + ".png",
            "frame_width": frames[0].w,
            "frame_height": frames[0].h,
            "frame_count": len(frames),
            "frame_duration_ms": durations,
            "loop": loop,
            "palette": palette,
        }
        if below:
            entry["below"] = True
        effects[key] = entry
    with open(path, "w", encoding="utf-8") as f:
        json.dump(effects, f, indent=2, ensure_ascii=False)
        f.write("\n")

    prev = os.path.join(out, "preview")
    os.makedirs(prev, exist_ok=True)
    tree().im.save(os.path.join(prev, "tree.png"))
    ground("#4a9a34", "#3c8228", "#66b448", 5).im.save(os.path.join(prev, "ground_forest.png"))
    ground("#3a3036", "#2a2228", "#5a4a50", 6, embers).im.save(os.path.join(prev, "ground_volcano.png"))
    ground("#8a9098", "#7a8088", "#a0a6ae", 7, slabs).im.save(os.path.join(prev, "ground_slabs.png"))


if __name__ == "__main__":
    main(sys.argv[1])
