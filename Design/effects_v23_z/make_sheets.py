"""Draws the Z move sheets (GDD « Objets spéciaux », mise en scène par type; canvas « Capacités Z — planches »).

Pixel art drawn by code, one palette per type, 24 px = 1 tile, frames in a row, pointing right (the game turns them
towards their direction); upright pieces (vines, spikes, hands, bolts) are drawn standing. Writes png/ and
Assets/Resources/Effects/<name>.png, and their entries in Assets/Resources/Effects/effects.json (sheets "z_*" replaced).

Usage: Tools/.venv/bin/python -I Design/effects_v23_z/make_sheets.py
"""
import json
import math
import pathlib
import random
import sys

from PIL import Image, ImageDraw

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent.parent
OUT = HERE / "png"
EFFECTS = ROOT / "Assets" / "Resources" / "Effects"


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


# Palettes: dark outline, dark, mid, light, highlight.
PAL = {
    "normal": ["#2a2620", "#8a8070", "#c8bea8", "#ece4d0", "#ffffff"],
    "fire": ["#3a0c04", "#a82410", "#f06018", "#ffb030", "#fff4b0"],
    "water": ["#08184a", "#1846b0", "#2f82f2", "#8ccaff", "#f0faff"],
    "grass": ["#0e2a0c", "#2a6a1e", "#4ea830", "#9ade5a", "#e8ffc0"],
    "electric": ["#3a2600", "#d68a00", "#ffd21f", "#fff6a8", "#ffffff"],
    "ice": ["#0c2a40", "#3a88b8", "#7cc8e8", "#c8f0ff", "#ffffff"],
    "fight": ["#3a1006", "#a83a18", "#e8702a", "#ffb870", "#fff0d8"],
    "poison": ["#200a2a", "#5a1e78", "#9a40c0", "#d080f0", "#f6d8ff"],
    "ground": ["#24140a", "#6a4220", "#a87038", "#d8a868", "#f4dcae"],
    "flying": ["#1a2a48", "#5a86c0", "#a8d0f0", "#e0f2ff", "#ffffff"],
    "psychic": ["#3a0828", "#a02870", "#f058a8", "#ffa8d8", "#fff0f8"],
    "bug": ["#1a2406", "#5a7a10", "#98b828", "#d0e870", "#f8ffd8"],
    "rock": ["#1e1a14", "#5a5040", "#8a7c60", "#b8a888", "#e8dcc0"],
    "ghost": ["#100818", "#3a1e58", "#6a3c98", "#a878d8", "#e8d0ff"],
    "dragon": ["#10083a", "#3a1ea8", "#7038f8", "#b49aff", "#f0e8ff"],
    "dark": ["#060408", "#2a1a2e", "#5a2a58", "#a04888", "#f0a0c8"],
    "steel": ["#1a1e26", "#5a6878", "#98a8b8", "#d0dce8", "#ffffff"],
}


def P(key, i, a=255):
    return hexc(PAL[key][i], a)


def blank(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def outline(img, colour):
    """A 1 px outline around the opaque pixels."""
    src = img.load()
    w, h = img.size
    out = img.copy()
    o = out.load()
    for x in range(w):
        for y in range(h):
            if src[x, y][3] > 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] > 140:
                    o[x, y] = colour
                    break
    return out


def disc(d, cx, cy, r, c):
    if r <= 0:
        return
    d.ellipse((cx - r, cy - r, cx + r - 1, cy + r - 1), fill=c)


def ring(d, cx, cy, r, c, width=1):
    if r <= 0:
        return
    d.ellipse((cx - r, cy - r, cx + r - 1, cy + r - 1), outline=c, width=width)


def strip(frames):
    w, h = frames[0].size
    s = blank(w * len(frames), h)
    for i, f in enumerate(frames):
        s.paste(f, (i * w, 0))
    return s


def jagged(rng, a, b, n, jag):
    pts = [a]
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy) or 1
    px, py = -dy / length, dx / length
    for i in range(1, n):
        t = i / n
        k = rng.uniform(-jag, jag)
        pts.append((a[0] + dx * t + px * k, a[1] + dy * t + py * k))
    pts.append(b)
    return pts


# ---------------------------------------------------------------- generic pieces

def burst(key, size=48, n=6, rays=8, seed=1, ring_last=True):
    """An impact: a bright core that swells, rays, then sparks and a fading ring."""
    rng = random.Random(seed)
    frames = []
    c = size // 2
    sparks = [(rng.uniform(0, 2 * math.pi), rng.uniform(0.6, 1.0)) for _ in range(14)]
    for f in range(n):
        t = f / (n - 1)
        img = blank(size, size)
        d = ImageDraw.Draw(img)
        r = int(c * (0.25 + 0.6 * math.sin(min(1, t * 1.6) * math.pi / 2)))
        if t < 0.75:
            for k in range(rays):
                a = k * 2 * math.pi / rays + f * 0.15
                L = r * (1.5 if k % 2 == 0 else 1.1)
                w = 0.22
                pts = [(c + math.cos(a - w) * r * 0.5, c + math.sin(a - w) * r * 0.5), (c + math.cos(a) * L, c + math.sin(a) * L),
                       (c + math.cos(a + w) * r * 0.5, c + math.sin(a + w) * r * 0.5)]
                d.polygon(pts, fill=P(key, 3))
            disc(d, c, c, int(r * 0.85), P(key, 2))
            disc(d, c, c, int(r * 0.55), P(key, 3))
            disc(d, c, c, int(r * 0.3 * (1 - t)), P(key, 4))
        if ring_last and t > 0.3:
            ring(d, c, c, int(c * (0.5 + 0.48 * t)), P(key, 3, int(255 * (1 - t * 0.7))), 2 if t < 0.7 else 1)
        for a, s in sparks:
            dist = c * (0.3 + 0.7 * t) * s
            x, y = c + math.cos(a) * dist, c + math.sin(a) * dist
            if t > 0.15:
                d.rectangle((x, y, x + 1, y + 1), fill=P(key, 4 if f % 2 else 3))
        frames.append(outline(img, P(key, 0)) if t < 0.75 else img)
    return frames


# ---------------------------------------------------------------- per type

def normal_impact():
    """A white star that flashes a Z."""
    frames = burst("normal", 48, 6, 4, seed=2)
    for f, img in enumerate(frames[1:4], 1):
        d = ImageDraw.Draw(img)
        k = 9 + f
        pts = [(24 - k, 24 - k), (24 + k, 24 - k), (24 - k, 24 + k), (24 + k, 24 + k)]
        d.line(pts, fill=P("normal", 0), width=5, joint="curve")
        d.line(pts, fill=P("normal", 4), width=3, joint="curve")
    return frames


def normal_streak():
    frames = []
    for f in range(4):
        img = blank(32, 24)
        d = ImageDraw.Draw(img)
        for k, y in enumerate((6, 10, 12, 14, 18)):
            L = 14 + ((k * 7 + f * 5) % 12)
            d.line((31 - L, y, 31, y), fill=P("normal", 4 if k % 2 else 3), width=1 if k % 2 else 2)
        disc(d, 26, 12, 5, P("normal", 4))
        frames.append(outline(img, P("normal", 1)))
    return frames


def fire_sun():
    frames = []
    for f in range(6):
        img = blank(64, 64)
        d = ImageDraw.Draw(img)
        for k in range(12):
            a = k * math.pi / 6 + f * math.pi / 36
            L = 30 if (k + f) % 2 == 0 else 26
            pts = [(32 + math.cos(a - 0.2) * 16, 32 + math.sin(a - 0.2) * 16), (32 + math.cos(a) * L, 32 + math.sin(a) * L),
                   (32 + math.cos(a + 0.2) * 16, 32 + math.sin(a + 0.2) * 16)]
            d.polygon(pts, fill=P("fire", 2))
        disc(d, 32, 32, 20 + (f % 2), P("fire", 2))
        disc(d, 32, 32, 16, P("fire", 3))
        disc(d, 32, 32, 10, P("fire", 4))
        for k in range(5):  # surface flares
            a = k * 1.3 + f * 0.6
            disc(d, int(32 + math.cos(a) * 12), int(32 + math.sin(a) * 12), 2, P("fire", 4))
        frames.append(outline(img, P("fire", 1)))
    return frames


def fire_meteor():
    rng = random.Random(3)
    frames = []
    for f in range(4):
        img = blank(32, 24)
        d = ImageDraw.Draw(img)
        for k in range(3):  # tail flames
            y0 = 12 + (k - 1) * 4 + (1 if (f + k) % 2 else -1)
            L = 16 + ((f * 3 + k * 5) % 8)
            d.polygon([(24, 12 - 6 + k * 2), (24 - L, y0), (24, 12 + 2 + k * 2)], fill=P("fire", 2 if k != 1 else 3))
        disc(d, 24, 12, 7, P("fire", 2))
        disc(d, 25, 12, 5, P("fire", 3))
        disc(d, 26, 12, 3, P("fire", 4))
        for _ in range(4):
            x, y = rng.randint(2, 16), rng.randint(4, 20)
            d.point((x, y), fill=P("fire", 4))
        frames.append(outline(img, P("fire", 0)))
    return frames


def fire_impact():
    frames = []
    for f in range(7):
        t = f / 6
        img = blank(64, 64)
        d = ImageDraw.Draw(img)
        r = int(8 + 22 * math.sin(min(1, t * 1.4) * math.pi / 2))
        if t < 0.85:
            for k in range(9):
                a = k * 2 * math.pi / 9 + f * 0.3
                L = r * (1.35 if k % 2 else 1.15)
                d.polygon([(32 + math.cos(a - 0.35) * r * 0.7, 32 + math.sin(a - 0.35) * r * 0.7), (32 + math.cos(a) * L, 32 + math.sin(a) * L),
                           (32 + math.cos(a + 0.35) * r * 0.7, 32 + math.sin(a + 0.35) * r * 0.7)], fill=P("fire", 2))
            disc(d, 32, 32, r, P("fire", 1 if t > 0.6 else 2))
            disc(d, 32, 31, int(r * 0.7), P("fire", 3 if t < 0.6 else 2))
            disc(d, 32, 30, int(r * 0.4 * (1 - t)), P("fire", 4))
        else:
            for k in range(6):  # smoke puffs
                a = k * math.pi / 3
                disc(d, int(32 + math.cos(a) * 20), int(32 + math.sin(a) * 20 - 4), 6, hexc("#4a3a34", 160))
        frames.append(outline(img, P("fire", 0)) if t < 0.85 else img)
    return frames


def water_wave():
    frames = []
    for f in range(4):
        img = blank(32, 48)
        d = ImageDraw.Draw(img)
        sway = (f % 2)
        body = [(0, 47), (0, 14), (6, 8), (14, 4), (22, 4 + sway), (28, 8), (31, 14), (26, 12), (22, 14), (24, 22), (20, 32), (14, 47)]
        d.polygon(body, fill=P("water", 2))
        d.polygon([(0, 47), (0, 24), (8, 18), (14, 24), (10, 47)], fill=P("water", 1))
        d.line([(4, 12), (12, 6), (22, 6 + sway), (28, 10)], fill=P("water", 3), width=2)
        for k in range(6):  # foam
            x = (k * 6 + f * 3) % 30
            disc(d, x, 5 + (k % 3), 2, P("water", 4))
        disc(d, 28, 12, 2, P("water", 4))
        frames.append(outline(img, P("water", 0)))
    return frames


def splash(key, seed):
    rng = random.Random(seed)
    drops = [(rng.uniform(-math.pi * 0.9, -math.pi * 0.1), rng.uniform(10, 20)) for _ in range(9)]
    frames = []
    for f in range(6):
        t = f / 5
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        d.ellipse((24 - 6 - 14 * t, 34 - 3 - 5 * t, 24 + 6 + 14 * t, 34 + 3 + 5 * t), outline=P(key, 3), width=2)
        if t < 0.6:
            d.polygon([(16, 36), (20, 30 - 14 * (1 - abs(t - 0.3) * 2)), (24, 34), (28, 28 - 16 * (1 - abs(t - 0.3) * 2)), (32, 36)], fill=P(key, 2))
        for a, s in drops:
            x = 24 + math.cos(a) * s * t * 1.2
            y = 34 + math.sin(a) * s * t * 1.6 + 30 * t * t
            disc(d, int(x), int(y), 2 if t < 0.7 else 1, P(key, 3 if f % 2 else 4))
        frames.append(outline(img, P(key, 0)))
    return frames


def grass_vine():
    frames = []
    for f in range(6):
        t = min(1, (f + 1) / 4)
        img = blank(24, 48)
        d = ImageDraw.Draw(img)
        h = int(6 + 38 * t)
        pts = []
        for y in range(h):
            pts.append((12 + math.sin(y / 6 + 1) * 3, 47 - y))
        if len(pts) > 1:
            d.line(pts, fill=P("grass", 2), width=4)
            d.line([(x - 1, y) for x, y in pts], fill=P("grass", 3), width=1)
        for k in range(0, h - 6, 9):  # leaves
            x, y = pts[k]
            side = 1 if (k // 9) % 2 else -1
            d.polygon([(x, y), (x + side * 7, y - 3), (x + side * 2, y - 6)], fill=P("grass", 3))
        if t >= 1 and pts:  # the tip curls
            x, y = pts[-1]
            d.arc((x - 4, y - 4, x + 4, y + 4), 180, 450 + f * 20, fill=P("grass", 2), width=2)
        if f == 5:
            img = Image.blend(blank(24, 48), img, 0.6)
        frames.append(outline(img, P("grass", 0)))
    return frames


def grass_petal():
    frames = []
    for f in range(4):
        img = blank(8, 8)
        d = ImageDraw.Draw(img)
        w = [3, 2, 1, 2][f]
        d.ellipse((4 - w, 1, 4 + w, 7), fill=hexc("#ff9ad0"))
        d.point((4, 2), fill=hexc("#ffe0f0"))
        frames.append(img)
    return frames


def electric_bolt():
    rng = random.Random(5)
    frames = []
    base = jagged(rng, (16, 0), (16, 127), 10, 7)
    branches = [jagged(rng, base[k], (base[k][0] + rng.choice([-12, 12]), base[k][1] + 18), 3, 3) for k in (3, 6)]
    for f in range(5):
        img = blank(32, 128)
        d = ImageDraw.Draw(img)
        if f == 0:
            d.line(base[:5], fill=P("electric", 3), width=1)
        elif f < 4:
            w = 5 if f < 3 else 3
            d.line(base, fill=P("electric", 2), width=w + 2)
            d.line(base, fill=P("electric", 3), width=w)
            d.line(base, fill=P("electric", 4), width=max(1, w - 3))
            for b in branches:
                d.line(b, fill=P("electric", 3), width=2)
            if f < 3:
                disc(d, 16, 122, 7, P("electric", 3))
        else:
            for k in range(0, len(base) - 1, 2):
                d.line(base[k:k + 2], fill=P("electric", 2), width=2)
        frames.append(outline(img, P("electric", 1)))
    return frames


def ice_spike():
    frames = []
    for f in range(6):
        t = min(1, (f + 1) / 3)
        img = blank(24, 48)
        d = ImageDraw.Draw(img)
        h = int(46 * t)
        top = 47 - h
        d.polygon([(12, top), (20, 47), (4, 47)], fill=P("ice", 2))
        d.polygon([(12, top), (12, 47), (4, 47)], fill=P("ice", 3))
        d.line([(12, top + 2), (12, 46)], fill=P("ice", 4), width=1)
        if f >= 3 and f <= 4:  # glint
            gx, gy = 9, top + 8 + (f - 3) * 10
            d.line([(gx - 2, gy), (gx + 2, gy)], fill=P("ice", 4))
            d.line([(gx, gy - 2), (gx, gy + 2)], fill=P("ice", 4))
        frames.append(outline(img, P("ice", 0)))
    return frames


def ice_impact():
    rng = random.Random(7)
    shards = [(rng.uniform(0, 2 * math.pi), rng.uniform(10, 22)) for _ in range(10)]
    frames = []
    for f in range(6):
        t = f / 5
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        if t < 0.4:
            disc(d, 24, 24, int(8 + 10 * t), P("ice", 3))
            disc(d, 24, 24, int(5 + 4 * t), P("ice", 4))
        for a, s in shards:
            x, y = 24 + math.cos(a) * s * t, 24 + math.sin(a) * s * t
            d.polygon([(x, y - 3), (x + 2, y), (x, y + 3), (x - 2, y)], fill=P("ice", 3 if f % 2 else 4))
        ring(d, 24, 24, int(6 + 16 * t), P("ice", 3, int(255 * (1 - t))))
        frames.append(outline(img, P("ice", 0)))
    return frames


def fight_fist():
    frames = burst("fight", 40, 5, 6, seed=8)
    for f, img in enumerate(frames[:3]):
        d = ImageDraw.Draw(img)
        x = 8 + f * 3
        d.rounded_rectangle((x, 14, x + 14, 26), 3, fill=P("fight", 3), outline=P("fight", 0))
        for k in range(3):
            d.line((x + 4 + k * 3, 14, x + 4 + k * 3, 19), fill=P("fight", 1))
        for k in range(3):
            d.line((0, 15 + k * 4, x - 2, 15 + k * 4), fill=P("fight", 4))
    return frames


def poison_drop():
    frames = []
    for f in range(4):
        img = blank(16, 12)
        d = ImageDraw.Draw(img)
        d.polygon([(1, 6), (9, 2), (9, 10)], fill=P("poison", 2))
        disc(d, 10, 6, 5, P("poison", 2))
        disc(d, 11, 5, 2, P("poison", 3 if f % 2 else 4))
        frames.append(outline(img, P("poison", 0)))
    return frames


def poison_puddle():
    frames = []
    for f in range(4):
        img = blank(48, 24)
        d = ImageDraw.Draw(img)
        d.ellipse((2, 6, 45, 21), fill=P("poison", 1, 230))
        d.ellipse((6, 8, 41, 18), fill=P("poison", 2, 230))
        for k, (x, y) in enumerate(((14, 12), (26, 10), (34, 14), (20, 15))):
            r = (f + k) % 4
            if r < 3:
                ring(d, x, y, 1 + r, P("poison", 4))
        frames.append(outline(img, P("poison", 0)))
    return frames


def ground_impact():
    rng = random.Random(9)
    rocks = [(rng.uniform(-math.pi * 0.95, -math.pi * 0.05), rng.uniform(14, 28), rng.randint(2, 4)) for _ in range(10)]
    frames = []
    for f in range(7):
        t = f / 6
        img = blank(64, 64)
        d = ImageDraw.Draw(img)
        for k in range(5):  # dust
            disc(d, int(32 + (k - 2) * 8 * (0.6 + t)), int(46 - 3 * t), int(5 + 5 * t), P("ground", 3, int(200 * (1 - t))))
        if t < 0.5:
            d.polygon([(22, 50), (28, 50 - 24 * (1 - t * 2)), (32, 48), (38, 50 - 18 * (1 - t * 2)), (44, 50)], fill=P("ground", 2))
        for a, s, r in rocks:
            x = 32 + math.cos(a) * s * t * 1.2
            y = 48 + math.sin(a) * s * t * 1.8 + 34 * t * t
            d.polygon([(x - r, y), (x, y - r), (x + r, y), (x, y + r)], fill=P("ground", 2 if r > 2 else 3))
        frames.append(outline(img, P("ground", 0)))
    return frames


def flying_dive():
    frames = []
    for f in range(4):
        img = blank(32, 24)
        d = ImageDraw.Draw(img)
        d.polygon([(31, 12), (6, 2 + f % 2), (12, 12), (6, 22 - f % 2)], fill=P("flying", 3, 220))
        d.polygon([(31, 12), (14, 7), (18, 12), (14, 17)], fill=P("flying", 4))
        for k in range(3):
            y = 5 + k * 7
            L = 8 + ((f * 4 + k * 3) % 8)
            d.line((0, y, L, y), fill=P("flying", 4))
        frames.append(outline(img, P("flying", 1)))
    return frames


def flying_impact():
    frames = []
    for f in range(6):
        t = f / 5
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        for k in range(3):
            r = int(6 + 18 * t + k * 3)
            d.arc((24 - r, 24 - r, 24 + r, 24 + r), f * 50 + k * 120, f * 50 + k * 120 + 200, fill=P("flying", 3 + k % 2), width=2)
        if t < 0.4:
            disc(d, 24, 24, 6, P("flying", 4))
        frames.append(outline(img, P("flying", 1)))
    return frames


def psychic_field():
    frames = []
    for f in range(6):
        img = blank(96, 96)
        d = ImageDraw.Draw(img)
        disc(d, 48, 48, 48, P("psychic", 2, 70))
        for k in range(4):
            r = int(((k * 12 + f * 2) % 48))
            if r > 2:
                ring(d, 48, 48, r, P("psychic", 3, 150 - r * 2))
        ring(d, 48, 48, 48, P("psychic", 4, 230), 2)
        for k in range(8):  # motes
            a = k * math.pi / 4 + f * 0.2
            r = 20 + (k * 5 + f * 3) % 24
            d.point((48 + math.cos(a) * r, 48 + math.sin(a) * r), fill=P("psychic", 4))
        frames.append(img)
    return frames


def psychic_impact():
    frames = []
    for f in range(6):
        t = f / 5
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        for k in range(3):
            r = int(22 * (1 - t) + k * 4) if t < 0.5 else int(4 + 20 * (t - 0.5) * 2 + k * 3)
            ring(d, 24, 24, r, P("psychic", 3 + (k % 2)), 2)
        if 0.3 < t < 0.7:
            disc(d, 24, 24, 6, P("psychic", 4))
        frames.append(outline(img, P("psychic", 0)))
    return frames


def bug_web():
    frames = []
    for f in range(2):
        img = blank(96, 96)
        d = ImageDraw.Draw(img)
        spokes = 12
        silk = hexc("#fffff0", 210 if f == 0 else 170)
        for k in range(spokes):
            a = k * 2 * math.pi / spokes
            d.line((48, 48, 48 + math.cos(a) * 47, 48 + math.sin(a) * 47), fill=silk)
        for r in range(6, 48, 7):
            pts = []
            for k in range(spokes + 1):
                a = k * 2 * math.pi / spokes
                rr = r - (1.5 if k % 2 else 0) + (f if k % 3 == 0 else 0)
                pts.append((48 + math.cos(a) * rr, 48 + math.sin(a) * rr))
            d.line(pts, fill=silk)
        frames.append(img)
    return frames


def bug_bind():
    rng = random.Random(11)
    threads = [(rng.uniform(0, math.pi), rng.uniform(-0.4, 0.4)) for _ in range(12)]
    frames = []
    for f in range(6):
        img = blank(32, 32)
        d = ImageDraw.Draw(img)
        n = 2 + f * 2
        for a, tilt in threads[:n]:
            d.arc((5, 3 + tilt * 4, 27, 29 - tilt * 4), math.degrees(a), math.degrees(a) + 220, fill=hexc("#fffff0"), width=1)
        if f >= 4:
            d.ellipse((8, 4, 24, 28), fill=hexc("#eef0d0", 120 + 60 * (f - 4)))
        frames.append(outline(img, P("bug", 1)))
    return frames


def rock_boulder():
    frames = []
    base = [(20 + math.cos(a) * r, 20 + math.sin(a) * r) for a, r in
            ((k * 2 * math.pi / 9, 15 + (k * 7 % 4)) for k in range(9))]
    for f in range(4):
        img = blank(40, 40)
        d = ImageDraw.Draw(img)
        a = f * math.pi / 8
        pts = [(20 + (x - 20) * math.cos(a) - (y - 20) * math.sin(a), 20 + (x - 20) * math.sin(a) + (y - 20) * math.cos(a)) for x, y in base]
        d.polygon(pts, fill=P("rock", 2))
        d.polygon([pts[5], pts[6], pts[7], (20, 20)], fill=P("rock", 1))
        d.polygon([pts[0], pts[1], pts[2], (20, 20)], fill=P("rock", 3))
        for k in range(3):
            ang = a + k * 2.1
            disc(d, int(20 + math.cos(ang) * 7), int(20 + math.sin(ang) * 7), 2, P("rock", 1))
        frames.append(outline(img, P("rock", 0)))
    return frames


def rock_rest():
    img = blank(32, 32)
    d = ImageDraw.Draw(img)
    d.ellipse((4, 22, 28, 30), fill=(0, 0, 0, 90))
    d.polygon([(5, 25), (8, 12), (16, 5), (25, 9), (28, 20), (24, 26), (12, 27)], fill=P("rock", 2))
    d.polygon([(8, 12), (16, 5), (25, 9), (16, 12)], fill=P("rock", 3))
    d.polygon([(24, 26), (28, 20), (20, 18), (14, 27)], fill=P("rock", 1))
    d.line([(12, 15), (16, 19), (15, 23)], fill=P("rock", 0))
    return [outline(img, P("rock", 0))]


def ghost_hand():
    frames = []
    for f in range(7):
        rise = [0.3, 0.7, 1, 1, 1, 0.7, 0.3][f]
        grip = [0, 0, 0, 0.5, 1, 1, 1][f]
        img = blank(24, 40)
        d = ImageDraw.Draw(img)
        top = int(34 - 34 * rise)
        d.rectangle((8, min(top + 12, 38), 15, 39), fill=P("ghost", 2))
        d.rounded_rectangle((5, top + 6, 18, top + 16), 3, fill=P("ghost", 2))
        for k in range(4):  # fingers, curling as it grips
            x = 6 + k * 3
            L = int(8 * (1 - grip * 0.6))
            d.line((x, top + 6, x + grip * 3, top + 6 - L), fill=P("ghost", 3), width=2)
        d.line((5, top + 12, 2 + grip * 4, top + 6), fill=P("ghost", 3), width=2)  # thumb
        for y in range(top + 18, 40, 4):
            d.point((10 + (y // 4) % 3, y), fill=P("ghost", 4))
        frames.append(outline(img, P("ghost", 0)))
    return frames


def dragon_head():
    frames = []
    for f in range(4):
        jaw = 2 + (f % 2) * 3
        img = blank(40, 32)
        d = ImageDraw.Draw(img)
        d.polygon([(4, 10), (22, 6), (36, 12), (38, 15), (22, 16), (4, 20)], fill=P("dragon", 2))  # snout
        d.polygon([(6, 18), (22, 16), (34, 16 + jaw), (20, 22 + jaw // 2), (6, 22)], fill=P("dragon", 1))  # jaw
        d.polygon([(10, 8), (2, 0), (16, 6)], fill=P("dragon", 3))  # horns
        d.polygon([(16, 7), (12, 1), (22, 6)], fill=P("dragon", 3))
        disc(d, 24, 11, 2, P("dragon", 4))  # eye
        d.line((22, 16, 34, 16), fill=P("dragon", 4))
        for k in range(3):  # mane of energy
            y = 8 + k * 6
            d.polygon([(4, y), (0 - (f + k) % 3, y + 2), (4, y + 4)], fill=P("dragon", 3))
        frames.append(outline(img, P("dragon", 0)))
    return frames


def dragon_scale():
    frames = []
    for f in range(2):
        img = blank(16, 16)
        d = ImageDraw.Draw(img)
        disc(d, 8, 8, 7, P("dragon", 2))
        d.arc((3, 4, 13, 14), 200, 340, fill=P("dragon", 3 + f), width=2)
        disc(d, 8, 6, 2, P("dragon", 4))
        frames.append(outline(img, P("dragon", 0)))
    return frames


def dark_hole():
    frames = []
    for f in range(6):
        img = blank(96, 96)
        d = ImageDraw.Draw(img)
        disc(d, 48, 48, 46, P("dark", 2, 90))
        for arm in range(4):
            pts = []
            for k in range(40):
                t = k / 39
                a = arm * math.pi / 2 + f * math.pi / 12 + t * 3.2
                r = 46 * (1 - t) + 10
                pts.append((48 + math.cos(a) * r, 48 + math.sin(a) * r))
            d.line(pts, fill=P("dark", 3, 220), width=3)
            d.line(pts[10:], fill=P("dark", 4, 200), width=1)
        disc(d, 48, 48, 16, P("dark", 0))
        ring(d, 48, 48, 17, P("dark", 4), 2)
        frames.append(img)
    return frames


def steel_drill():
    frames = []
    for f in range(4):
        img = blank(40, 24)
        d = ImageDraw.Draw(img)
        d.polygon([(39, 12), (8, 2), (8, 22)], fill=P("steel", 2))
        d.polygon([(39, 12), (8, 2), (8, 12)], fill=P("steel", 3))
        for k in range(5):  # grooves turning
            x = 10 + ((k * 6 + f * 2) % 28)
            half = 10 * (39 - x) / 31
            d.line((x, 12 - half, x + 3, 12 + half), fill=P("steel", 1), width=1)
        d.rectangle((0, 6, 8, 18), fill=P("steel", 1))
        d.line((0, 8, 8, 8), fill=P("steel", 4))
        frames.append(outline(img, P("steel", 0)))
    return frames


def leaf_impact():
    """Leaves bursting out, turning."""
    rng = random.Random(21)
    leaves = [(rng.uniform(0, 2 * math.pi), rng.uniform(10, 22)) for _ in range(9)]
    frames = []
    for f in range(6):
        t = f / 5
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        if t < 0.4:
            disc(d, 24, 24, int(6 + 8 * t), P("grass", 3))
        for k, (a, r) in enumerate(leaves):
            x, y = 24 + math.cos(a) * r * t, 24 + math.sin(a) * r * t + 6 * t * t
            ang = a + f * 0.8 + k
            dx, dy = math.cos(ang) * 4, math.sin(ang) * 4
            d.polygon([(x - dx, y - dy), (x - dy * 0.5, y + dx * 0.5), (x + dx, y + dy), (x + dy * 0.5, y - dx * 0.5)], fill=P("grass", 2 + k % 2))
        frames.append(outline(img, P("grass", 0)))
    return frames


def ghost_impact():
    """Wisps spiralling out and fading."""
    frames = []
    for f in range(6):
        t = f / 5
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        for k in range(5):
            pts = []
            for j in range(10):
                u = j / 9
                a = k * 2 * math.pi / 5 + u * 2.2 + t * 1.5
                r = 4 + 18 * t * u
                pts.append((24 + math.cos(a) * r, 24 + math.sin(a) * r))
            d.line(pts, fill=P("ghost", 3, int(255 * (1 - t * 0.6))), width=3 if t < 0.5 else 2)
        if t < 0.6:
            disc(d, 24, 24, int(7 * (1 - t)), P("ghost", 4))
        frames.append(outline(img, P("ghost", 0)))
    return frames


def needle_impact():
    """Needles flying out in every direction."""
    frames = []
    for f in range(6):
        t = f / 5
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        for k in range(12):
            a = k * math.pi / 6 + 0.2
            r0, r1 = 4 + 16 * t, 9 + 16 * t
            d.line((24 + math.cos(a) * r0, 24 + math.sin(a) * r0, 24 + math.cos(a) * r1, 24 + math.sin(a) * r1), fill=P("bug", 3 if k % 2 else 4), width=2)
        if t < 0.4:
            disc(d, 24, 24, 6, P("bug", 3))
        frames.append(outline(img, P("bug", 0)))
    return frames


def claw_impact():
    """Three dark claw slashes, then a burst."""
    frames = []
    for f in range(6):
        img = blank(48, 48)
        d = ImageDraw.Draw(img)
        grow = min(1, (f + 1) / 3)
        for k in range(3):
            x0, y0 = 10 + k * 9, 8
            x1, y1 = x0 + 14 * grow, y0 + 32 * grow
            w = 4 if f < 4 else 2
            d.line((x0, y0, x1, y1), fill=P("dark", 3), width=w + 2)
            d.line((x0, y0, x1, y1), fill=P("dark", 4), width=max(1, w - 2))
        if f >= 3:
            ring(d, 24, 24, 8 + (f - 3) * 6, P("dark", 3, 255 - (f - 3) * 70), 2)
        frames.append(outline(img, P("dark", 0)))
    return frames


def rubble_impact():
    """Rocks thrown up and falling back, with dust (Rock's)."""
    saved = PAL["ground"]
    PAL["ground"] = PAL["rock"]
    try:
        return ground_impact()
    finally:
        PAL["ground"] = saved


# name: (frames, ms per frame, loop)
def sheets():
    return {
        "z_normal_impact": (normal_impact(), 70, False),
        "z_normal_streak": (normal_streak(), 60, True),
        "z_fire_sun": (fire_sun(), 90, True),
        "z_fire_meteor": (fire_meteor(), 60, True),
        "z_fire_impact": (fire_impact(), 70, False),
        "z_water_wave": (water_wave(), 90, True),
        "z_water_impact": (splash("water", 4), 70, False),
        "z_grass_vine": (grass_vine(), 80, False),
        "z_grass_petal": (grass_petal(), 90, True),
        "z_grass_impact": (leaf_impact(), 70, False),
        "z_electric_bolt": (electric_bolt(), 50, False),
        "z_electric_impact": (burst("electric", 48, 6, 8, seed=6), 60, False),
        "z_ice_spike": (ice_spike(), 70, False),
        "z_ice_impact": (ice_impact(), 70, False),
        "z_fight_fist": (fight_fist(), 60, False),
        "z_poison_drop": (poison_drop(), 70, True),
        "z_poison_puddle": (poison_puddle(), 120, True),
        "z_poison_impact": (splash("poison", 13), 70, False),
        "z_ground_impact": (ground_impact(), 70, False),
        "z_flying_dive": (flying_dive(), 50, True),
        "z_flying_impact": (flying_impact(), 60, False),
        "z_psychic_field": (psychic_field(), 90, True),
        "z_psychic_impact": (psychic_impact(), 70, False),
        "z_bug_web": (bug_web(), 160, True),
        "z_bug_bind": (bug_bind(), 70, False),
        "z_bug_impact": (needle_impact(), 60, False),
        "z_rock_boulder": (rock_boulder(), 80, True),
        "z_rock_rest": (rock_rest(), 500, True),
        "z_rock_impact": (rubble_impact(), 70, False),
        "z_ghost_hand": (ghost_hand(), 90, False),
        "z_ghost_impact": (ghost_impact(), 70, False),
        "z_dragon_head": (dragon_head(), 90, True),
        "z_dragon_scale": (dragon_scale(), 120, True),
        "z_dragon_impact": (burst("dragon", 64, 7, 8, seed=17), 70, False),
        "z_dark_hole": (dark_hole(), 80, True),
        "z_dark_impact": (claw_impact(), 60, False),
        "z_steel_drill": (steel_drill(), 50, True),
        "z_steel_impact": (burst("steel", 48, 6, 10, seed=19), 60, False),
    }


def main():
    OUT.mkdir(exist_ok=True)
    index_path = EFFECTS / "effects.json"
    index = json.loads(index_path.read_text(encoding="utf-8"))
    index["sheets"] = [s for s in index["sheets"] if not s["file"].startswith("z_")]
    made = sheets()
    for name, (frames, ms, loop) in made.items():
        image = strip(frames)
        image.save(OUT / f"{name}.png")
        image.save(EFFECTS / f"{name}.png")
        w, h = frames[0].size
        index["sheets"].append({"file": f"{name}.png", "frame_width": w, "frame_height": h, "frame_count": len(frames),
                                "frame_duration_ms": [ms] * len(frames), "loop": loop})
    index_path.write_text(json.dumps(index, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"{len(made)} planches -> {OUT} et {EFFECTS}")


if __name__ == "__main__":
    sys.exit(main())
