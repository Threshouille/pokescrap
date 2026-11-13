"""Entrées des donjons, lot 1 : escaliers, repères et icônes (prompt : Design/prompt_entrees.md).

Tools/.venv/bin/python -I Design/effects_v21_entrees/generate_lot1.py Design/effects_v21_entrees
Écrit les 9 planches, effects.json, et sous preview/ les sols de la planche de présentation.
"""
import json
import math
import os
import random
import sys

from PIL import Image


def C(h, a=255):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), a)


S0, S1, S2, S3 = "#3a3836", "#6a6560", "#9a948c", "#c8c0b4"
K0, K1 = "#100c14", "#2a2030"
L0, L1, L2, L3 = "#a07010", "#f0c030", "#fff0a0", "#ffffff"
D0, D1, D2 = "#8a7a64", "#c0b090", "#e8dcc4"
T0, T1, T2 = "#0a6a72", "#1ec8c8", "#a0fff0"
V0, V1, V2 = "#4a2a6a", "#8a5ac8", "#d0b0ff"


class Sheet:
    def __init__(self, w, h):
        self.im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.px = self.im.load()
        self.w, self.h = w, h

    def p(self, x, y, c, a=255):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = C(c, a) if isinstance(c, str) else c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return (0, 0, 0, 0)

    def rect(self, x0, y0, x1, y1, c, a=255):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.p(x, y, c, a)

    def frame(self, x0, y0, x1, y1, c, a=255, round_=False):
        for x in range(x0, x1 + 1):
            if round_ and x in (x0, x1):
                continue
            self.p(x, y0, c, a)
            self.p(x, y1, c, a)
        for y in range(y0 + 1, y1):
            self.p(x0, y, c, a)
            self.p(x1, y, c, a)

    def disc(self, cx, cy, r, c, a=255):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                    self.p(x, y, c, a)

    def outline(self, c, a=255):
        """Contour d'une couleur autour de tout ce qui est opaque (4 voisins)."""
        add = []
        for y in range(self.h):
            for x in range(self.w):
                if self.px[x, y][3]:
                    continue
                if any(self.get(x + dx, y + dy)[3] and self.get(x + dx, y + dy)[:3] != C(c)[:3]
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    add.append((x, y))
        for x, y in add:
            self.p(x, y, c, a)

    def paste(self, other, ox, oy):
        self.im.alpha_composite(other.im, (ox, oy))


def strip(frames):
    w, h = frames[0].w, frames[0].h
    out = Sheet(w * len(frames), h)
    for i, f in enumerate(frames):
        out.paste(f, i * w, 0)
    return out


def shade(s, base, hi, lo, light_only_if=None):
    """Volume d'une forme pleine de couleur `base` : clair en haut à gauche, sombre en bas à droite."""
    b = C(base)
    cells = [(x, y) for y in range(s.h) for x in range(s.w) if s.px[x, y] == b]
    for x, y in cells:
        if s.get(x, y - 1) != b and s.get(x, y - 1)[3] == 0 or s.get(x - 1, y)[3] == 0:
            s.p(x, y, hi)
        elif s.get(x, y + 1)[3] == 0 or s.get(x + 1, y)[3] == 0:
            s.p(x, y, lo)


# ---------------------------------------------------------------- escaliers (48×48)

OUT0, OUT1 = 5, 42          # bord extérieur (x et y du haut)
TOPB = 40                    # dernière rangée du dessus du rebord, avant la face avant
FRONT = 44                   # contour du bas de la face avant
HX0, HX1, HY0, HY1 = 11, 36, 11, 34   # trou


def rim(s):
    s.rect(OUT0, OUT0, OUT1, FRONT, S2)
    # face avant du rebord (vue de 3/4)
    s.rect(OUT0 + 1, TOPB + 1, OUT1 - 1, FRONT - 1, S1)
    for x in (17, 28):
        s.rect(x, TOPB + 1, x, FRONT - 1, S0)
    s.rect(OUT0 + 1, TOPB, OUT1 - 1, TOPB, S3)
    # lumière en haut à gauche, ombre en bas à droite des pierres
    s.rect(OUT0 + 1, OUT0 + 1, OUT1 - 1, OUT0 + 1, S3)
    s.rect(OUT0 + 1, OUT0 + 1, OUT0 + 1, TOPB, S3)
    s.rect(OUT1 - 1, OUT0 + 2, OUT1 - 1, TOPB - 1, S1)
    # joints des pierres taillées : sombre, puis clair (arête de la pierre suivante)
    for x in (15, 24, 33):
        s.rect(x, OUT0 + 1, x, HY0 - 2, S1)
        s.rect(x + 1, OUT0 + 2, x + 1, HY0 - 2, S3)
    for y in (18, 28):
        s.rect(OUT0 + 1, y, HX0 - 2, y, S1)
        s.rect(OUT0 + 2, y + 1, HX0 - 2, y + 1, S3)
    for y in (16, 26):
        s.rect(HX1 + 2, y, OUT1 - 1, y, S1)
        s.rect(HX1 + 2, y + 1, OUT1 - 2, y + 1, S3)
    for x in (19, 30):
        s.rect(x, HY1 + 2, x, TOPB - 1, S1)
        s.rect(x + 1, HY1 + 2, x + 1, TOPB - 1, S3)
    # contours : extérieur et bord du trou
    s.frame(OUT0, OUT0, OUT1, FRONT, S0, round_=True)
    s.frame(HX0 - 1, HY0 - 1, HX1 + 1, HY1 + 1, S0)
    # petites écornures pour casser la régularité
    for x, y in ((OUT0 + 1, OUT0 + 1), (OUT1 - 1, OUT0 + 1), (OUT0 + 1, FRONT - 1), (OUT1 - 1, FRONT - 1)):
        s.p(x, y, S0)


def steps(s, treads, risers, shrink=True, beam=None, narrow_top=False):
    s.rect(HX0, HY0, HX1, HY1, K0)
    for k in range(6):
        y = HY0 + 4 * k
        inset = (k if shrink else 0) if not narrow_top else (5 - k)
        x0, x1 = HX0 + inset, HX1 - inset
        s.rect(x0, y, x1, y + 1, treads[k])
        s.rect(x0, y + 2, x1, y + 3, risers[k])
        if k < 5:
            s.rect(x0, y + 3, x1, y + 3, K1 if risers[k] not in (K1, K0) else K0)
        # nez de marche plus clair à gauche, ombre de la paroi à droite
        s.p(x0, y, S3 if treads[k] not in (K1, K0) else S1)
        s.rect(x1, y, x1, y + 3, K1 if risers[k] != K0 else K0)
    # parois du trou : la gauche reçoit la lumière, la droite est dans l'ombre
    for y in range(HY0, HY1 + 1):
        k = (y - HY0) // 4
        inset = (k if shrink else 0) if not narrow_top else (5 - k)
        for x in range(HX0, HX0 + inset):
            s.p(x, y, K1)
        for x in range(HX1 - inset + 1, HX1 + 1):
            s.p(x, y, K0)
    if beam:
        beam(s)


def stairs_down():
    s = Sheet(48, 48)
    rim(s)
    steps(s, [S3, S2, S2, S1, K1, K1], [S2, S1, S1, K1, K1, K0])
    # le fond se perd dans le noir (tramage)
    for y in range(HY1 - 5, HY1 + 1):
        for x in range(HX0, HX1 + 1):
            if (x + y) % 2 == 0 or y > HY1 - 2:
                s.p(x, y, K0)
    return s


def stairs_up(lit=True):
    s = Sheet(48, 48)
    rim(s)
    if lit:
        treads = [L3, L2, S3, S3, S2, S1]
        risers = [L2, S3, S2, S2, S1, K1]
    else:
        treads = [S2, S2, S2, S1, S1, K1]
        risers = [S1, S1, S1, K1, K1, K0]
    steps(s, treads, risers, narrow_top=lit)
    if lit:
        # la sortie : lueur sur le haut du rebord et halo au sommet
        s.rect(HX0, HY0 - 1, HX1, HY0 - 1, L1)
        for x in range(HX0 + 1, HX1, 2):
            s.p(x, HY0 - 2, L2)
    return s


def stairs_locked():
    s = stairs_up(lit=False)
    # grille de pierre : barreaux verticaux et deux traverses, gris ternes
    for x in (13, 19, 25, 31):
        s.rect(x, HY0, x + 2, HY1, S0)
        s.rect(x + 1, HY0, x + 1, HY1, S2)
        s.p(x + 1, HY0, S3)
    for y in (16, 28):
        s.rect(HX0, y, HX1, y + 2, S0)
        s.rect(HX0, y + 1, HX1, y + 1, S2)
        for x in (14, 20, 26, 32):
            s.p(x, y + 1, S3)   # tête des rivets aux croisements
    return s


# ---------------------------------------------------------------- effets (48×48)

def glow_frames():
    frames = []
    a = 153
    sparks = [(15, 0), (20, 9), (26, 4), (31, 13), (18, 18), (29, 7), (23, 21)]
    for f in range(8):
        s = Sheet(48, 48)
        ph = (math.sin(2 * math.pi * f / 8) + 1) / 2          # 0..1
        d = 1 if ph > 0.5 else 0
        s.frame(OUT0 - 1 - d, OUT0 - 1 - d, OUT1 + 1 + d, FRONT + 1 + d, L1, a, round_=True)
        if ph > 0.25:
            s.frame(OUT0, OUT0, OUT1, FRONT, L2 if ph > 0.75 else L1, a, round_=True)
        if ph > 0.5:
            s.frame(HX0 - 1, HY0 - 1, HX1 + 1, HY1 + 1, L2, a)
        for x, seed in sparks:
            y = HY1 - ((f * 4 + seed * 3) % 28)
            col = L3 if y < HY0 + 8 else L2
            s.p(x, y, col, a)
            if (f + seed) % 4 == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    s.p(x + dx, y + dy, L1, a)
        frames.append(s)
    return frames


def puff(s, cx, cy, r, a, dither=0):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            dd = (x - cx) ** 2 + (y - cy) ** 2
            if dd > r * r:
                continue
            if dither and (x + y) % dither:
                continue
            c = D1
            if (x - cx + 1) ** 2 + (y - cy + 1) ** 2 > r * r:
                c = D0                                            # bord ombré en bas à droite
            elif (x - cx + r * 0.35) ** 2 + (y - cy + r * 0.35) ** 2 < (r * 0.45) ** 2:
                c = D2                                            # reflet en haut à gauche
            s.p(x, y, c, a)


def reveal_frames():
    frames = []
    a = 179
    rnd = random.Random(7)
    clods = [(rnd.uniform(0, 2 * math.pi), rnd.uniform(0.8, 1.2)) for _ in range(7)]
    for f in range(8):
        s = Sheet(48, 48)
        r = 3 + f * 2.4
        n = 9
        pr = [3, 4, 5, 5, 5, 4, 3, 2][f]
        dither = 0 if f < 5 else (2 if f < 7 else 3)
        if f < 2:
            puff(s, 24, 24, 4 + f * 3, a)
        for k in range(n):
            ang = 2 * math.pi * k / n + 0.35
            puff(s, 24 + math.cos(ang) * r, 24 + math.sin(ang) * r * 0.8, pr, a, dither)
        if f < 6:
            for ang, sp in clods:
                dist = 6 + f * 3.2 * sp
                x = 24 + math.cos(ang) * dist
                y = 24 + math.sin(ang) * dist * 0.8 + f * f * 0.25
                s.rect(int(x), int(y), int(x) + 1, int(y) + 1, D0, a)
                s.p(int(x), int(y), D1, a)
        if 1 <= f <= 4:                                           # étoile blanche au centre
            L = [0, 3, 5, 4, 2][f]
            for t in range(-L, L + 1):
                s.p(24 + t, 24, L3, a)
                s.p(24, 24 + t, L3, a)
            if L >= 4:
                for t in (-1, 1):
                    s.p(24 + t, 24 + t, L2, a)
                    s.p(24 + t, 24 - t, L2, a)
        frames.append(s)
    return frames


def enter_frames():
    frames = []
    a = 179
    for f in range(6):
        s = Sheet(48, 48)
        R = 20 - f * 3.4
        for arm in range(3):
            base = arm * 2 * math.pi / 3 + f * 0.9
            rr = R
            while rr > 1:
                th = base + (R - rr) * 0.22
                x, y = 24 + math.cos(th) * rr, 24 + math.sin(th) * rr * 0.8
                s.p(x, y, L3 if rr < R * 0.6 else L2, a)
                if rr < R * 0.4:
                    s.p(x + 1, y, L3, a)
                rr -= 0.7
        core = [1, 1, 2, 2, 3, 2][f]
        s.disc(24, 24, core, L3, a)
        if f == 5:
            for t in (-4, -3, 3, 4):
                s.p(24 + t, 24, L2, a)
                s.p(24, 24 + t, L2, a)
        frames.append(s)
    return frames


# ---------------------------------------------------------------- icônes (16×16)

def icon_stairs(up):
    s = Sheet(16, 16)
    # marches de profil : trois paliers, la descente va vers la droite
    for i, (x0, y0) in enumerate(((1, 6), (5, 9), (9, 12))):
        s.rect(x0, y0, 13 if i == 2 else x0 + 3, 14, S2)
        s.rect(x0, y0, x0 + 3, y0, S3)
    s.rect(1, 6, 1, 14, S3)
    s.rect(1, 14, 13, 14, S1)
    s.rect(13, 12, 13, 14, S1)
    s.rect(4, 7, 4, 8, S1)
    s.rect(8, 10, 8, 11, S1)
    # flèche dorée : vers le bas pour descendre, vers le haut pour remonter
    if up:
        for y, (x0, x1) in zip(range(0, 3), ((11, 11), (10, 12), (9, 13))):
            s.rect(x0, y, x1, y, L1)
        s.rect(11, 3, 11, 6, L1)
        s.p(11, 0, L2)
    else:
        s.rect(11, 0, 11, 3, L1)
        for y, (x0, x1) in zip(range(4, 7), ((9, 13), (10, 12), (11, 11))):
            s.rect(x0, y, x1, y, L1)
        s.p(11, 0, L2)
    if up:
        s.im = s.im.transpose(Image.FLIP_LEFT_RIGHT)
        s.px = s.im.load()
    s.outline(K1)
    return s


def icon_secret():
    s = Sheet(16, 16)
    for y in range(16):
        for x in range(16):
            if ((x - 7.5) / 6.5) ** 2 + ((y - 8.5) / 6.0) ** 2 <= 1:
                s.p(x, y, S2)
    shade(s, S2, S3, S1)
    q = [".###.", "#...#", "....#", "..##.", "..#..", ".....", "..#.."]
    for j, row in enumerate(q):
        for i, ch in enumerate(row):
            if ch == "#":
                s.p(5 + i, 4 + j, K1)
                s.p(5 + i + 1, 4 + j + 1, S3) if s.get(6 + i, 5 + j) == C(S2) else None
    s.outline(K1)
    return s


def icon_seal():
    s = Sheet(16, 16)
    s.disc(7.5, 7.5, 6.6, V1)
    shade(s, V1, V2, V0)
    # œil fermé : paupière en arc, cils vers le bas
    for x in range(3, 13):
        y = 7 + round(((x - 7.5) / 4.5) ** 2 * 2)
        s.p(x, y - 1 if x in (3, 12) else y, K1)
    s.rect(5, 9, 5, 10, K1)
    s.rect(7, 10, 8, 11, K1)
    s.rect(10, 9, 10, 10, K1)
    s.rect(4, 4, 6, 4, V2)
    s.outline(K1)
    return s


def icon_key():
    s = Sheet(16, 16)
    s.disc(4.5, 7.5, 3.4, L1)
    s.disc(4.5, 7.5, 1.3, "#000000", 0)
    s.px[4, 7] = (0, 0, 0, 0)
    s.rect(7, 7, 14, 8, L1)
    s.rect(11, 9, 11, 11, L1)
    s.rect(13, 9, 13, 10, L1)
    shade(s, L1, L2, L0)
    s.outline(K1)
    return s


def icon_crown():
    s = Sheet(16, 16)
    s.rect(2, 10, 13, 13, L1)
    for cx in (2, 13):
        s.rect(cx, 5, cx, 9, L1)
    for y in range(4, 10):
        w = (y - 4) // 2
        s.rect(7 - w, y, 8 + w, y, L1)
    for y in range(7, 10):
        s.rect(3, y, 3 + (y - 7), y, L1)
        s.rect(12 - (y - 7), y, 12, y, L1)
    shade(s, L1, L2, L0)
    s.rect(3, 11, 12, 11, L0)
    for x in (4, 11):
        s.p(x, 12, V1)
    s.rect(7, 11, 8, 12, T1)
    s.p(7, 11, T2)
    s.p(2, 4, L2)
    s.p(13, 4, L2)
    s.rect(7, 2, 8, 3, L2)
    s.outline(K1)
    return s


def gear_mask(cx, cy, r_out, r_in, teeth, hole):
    pts = set()
    for y in range(16):
        for x in range(16):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)
            tooth = (math.cos(ang * teeth) > 0.2)
            if d <= hole:
                continue
            if d <= r_in or (tooth and d <= r_out):
                pts.add((x, y))
    return pts


def icon_gear(open_):
    s = Sheet(16, 16)
    base, hi, lo = (T1, T2, T0) if open_ else (S2, S3, S1)
    for x, y in gear_mask(7.5, 7.5, 7.0, 5.0, 8, 1.6):
        s.p(x, y, base)
    shade(s, base, hi, lo)
    if open_:
        # cerclé d'or
        for y in range(16):
            for x in range(16):
                d = math.hypot(x - 7.5, y - 7.5)
                if 3.2 <= d <= 4.2:
                    s.p(x, y, L1 if (x + y) % 5 else L2)
    else:
        for y in range(16):
            for x in range(16):
                if 3.3 <= math.hypot(x - 7.5, y - 7.5) <= 4.1:
                    s.p(x, y, S1)
    s.outline(K1)
    return s


# ---------------------------------------------------------------- bulle, ombre

def bubble_frames():
    frames = []
    for oy in (0, 0, 1, 1):
        s = Sheet(20, 22)
        y0 = oy
        s.rect(1, y0 + 1, 18, y0 + 16, L3)
        for x, y in ((1, y0 + 1), (18, y0 + 1), (1, y0 + 16), (18, y0 + 16)):
            s.px[x, y] = (0, 0, 0, 0)
        s.rect(2, y0 + 16, 17, y0 + 16, D2)
        s.rect(18, y0 + 2, 18, y0 + 15, D2)
        s.rect(8, y0 + 17, 11, y0 + 17, L3)
        s.rect(9, y0 + 18, 10, y0 + 18, L3)
        s.p(11, y0 + 17, D2)
        s.outline(K1)
        frames.append(s)
    return frames


def shadow():
    s = Sheet(48, 24)
    for y in range(24):
        for x in range(48):
            if ((x - 23.5) / 19) ** 2 + ((y - 11.5) / 7.5) ** 2 <= 1:
                s.p(x, y, "#000000", 102)
    return s


# ---------------------------------------------------------------- sols de présentation (non livrés)

def ground(base, dark, light, extra=None, seed=1):
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


def grass_tufts(s, rnd):
    for _ in range(9):
        x, y = rnd.randrange(32), rnd.randrange(32)
        for dx, dy in ((0, 0), (-1, 1), (1, 1), (0, -1)):
            s.p((x + dx) % 32, (y + dy) % 32, "#3c7a28")
        s.p(x % 32, (y - 2) % 32, "#90d060")


def main(out):
    os.makedirs(out, exist_ok=True)
    stone = [S0, S1, S2, S3]
    sheets = {
        "entrance_stairs_down": ([stairs_down()], 1, [0], False, True, stone + [K0, K1]),
        "entrance_stairs_up": ([stairs_up()], 1, [0], False, True, stone + [K1, L1, L2, L3]),
        "entrance_stairs_locked": ([stairs_locked()], 1, [0], False, True, stone + [K0, K1]),
        "entrance_glow": (glow_frames(), 8, [110] * 8, True, True, [L1, L2, L3]),
        "entrance_reveal": (reveal_frames(), 8, [70] * 8, False, True, [D0, D1, D2, L2, L3]),
        "entrance_enter": (enter_frames(), 6, [60] * 6, False, True, [L2, L3]),
        "entrance_icons": ([icon_stairs(False), icon_stairs(True), icon_secret(), icon_seal(), icon_key(),
                            icon_crown(), icon_gear(False), icon_gear(True)], 8, [0] * 8, False, False,
                           [K1] + stone[1:] + [L0, L1, L2, T0, T1, T2, V0, V1, V2]),
        "entrance_bubble": (bubble_frames(), 4, [160] * 4, True, False, [K1, D2, L3]),
        "entrance_shadow": ([shadow()], 1, [0], False, True, ["#000000"]),
    }
    effects = {}
    for key, (frames, count, durations, loop, below, palette) in sheets.items():
        st = strip(frames)
        st.im.save(os.path.join(out, key + ".png"))
        entry = {
            "file": key + ".png",
            "frame_width": frames[0].w,
            "frame_height": frames[0].h,
            "frame_count": count,
            "frame_duration_ms": durations,
            "loop": loop,
            "palette": palette,
        }
        if below:
            entry["below"] = True
        effects[key] = entry
    with open(os.path.join(out, "effects.json"), "w", encoding="utf-8") as f:
        json.dump(effects, f, indent=2, ensure_ascii=False)
        f.write("\n")

    prev = os.path.join(out, "preview")
    os.makedirs(prev, exist_ok=True)
    ground("#5aa83a", "#4a8c30", "#78c048", grass_tufts, 1).im.save(os.path.join(prev, "ground_grass.png"))
    ground("#e0c890", "#c8b070", "#f0dca8", None, 2).im.save(os.path.join(prev, "ground_sand.png"))
    ground("#8a6a48", "#6e5238", "#a8865e", None, 3).im.save(os.path.join(prev, "ground_rock.png"))
    ground("#e4ecf6", "#c4d0e4", "#ffffff", None, 4).im.save(os.path.join(prev, "ground_snow.png"))


if __name__ == "__main__":
    main(sys.argv[1])
