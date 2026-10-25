#!/usr/bin/env python3
"""Cut cliff faces out of the story maps of an Explorers of Sky ROM (MAP_BG), as listed in cliff_styles.json.

Usage: Tools/.venv/bin/python -I extract_cliffs.py <unpacked rom data dir> <cliff_styles.json> <out_dir> [preview.png]
<unpacked rom data dir> is Rom/files/data. Writes <out_dir>/<style>/<role>_<n>.png (24×24 RGBA); an optional preview
sheet shows every piece by style and role, then a small plateau drawn with them. The upper layer keeps its transparent
pixels (palette index 0 of a 16-colour palette); "both" lays the upper layer over the lower one.

Per style, besides the pieces cut from the map:
- ground: the map's own ground above a face, cleared from the top of the pieces of `roles` (flood fill from their top
  edge through the colours of `samples`, within `tolerance`, no deeper than `depth` pixels) so that the game's ground
  shows through;
- mirror: role -> role, a piece made by flipping another one left to right (the left end of a face from its right end);
- mirrorLoop: the columns of a face's rows followed by the same mirrored, in reverse order (seamless when they repeat);
- lip: rows `rows` of piece `piece` (variant 0, after the ground is cleared): the edge of the level seen from above,
  the cliff's drop at its bottom. Turned to make the other sides of a level: back (north), left, right, the outer
  corners backLeft / backRight and the inner ones innerLeft / innerRight (only the corner of the lip); with `ends`, the
  ends of a face too (its first column edged with the side's lip, its outer bottom corner rounded by `corner` pixels);
- foot: how the bottom row of a face (`roles`) meets the ground: the map's ground cleared from its bottom (samples,
  tolerance, depth as for ground), or without samples a wavy cut `depth` pixels high (± `amplitude`), outlined;
- shadow: the shadow a face casts on the ground at its foot (alpha at the top, fading out over depth pixels).
"""
import json
import sys
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw
from skytemple_files.common.types.file_types import FileType

PIECE = 24


def layer_image(bma, bpc, bpl, bpas, layer):
    """One layer of the map as RGBA, its transparent pixels (index 0 of a palette) cleared."""
    indexed = bma.to_pil_single_layer(bpc, bpl.palettes, bpas, layer)
    rgba = indexed.convert("RGBA")
    pixels = rgba.load()
    values = indexed.load()
    for y in range(indexed.height):
        for x in range(indexed.width):
            if values[x, y] % 16 == 0:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def ground_colours(image, samples):
    colours = set()
    for x, y, w, h in samples:
        for j in range(y, y + h):
            for i in range(x, x + w):
                colours.add(image.getpixel((i, j))[:3])
    return list(colours)


def clear_ground(piece, colours, tolerance, depth, side=None, edge="top"):
    """Clears the ground at the top of a piece (or at its bottom, for a face's foot): what is reached from that edge (and
    its left or right edge, for the end of a face, then at any depth) through ground colours."""
    pixels = piece.load()

    def ground(c):
        return c[3] > 0 and any(abs(c[0] - g[0]) + abs(c[1] - g[1]) + abs(c[2] - g[2]) <= tolerance for g in colours)

    def too_deep(y):
        return y >= depth if edge == "top" else y < PIECE - depth

    queue = deque((x, 0 if edge == "top" else PIECE - 1) for x in range(PIECE))
    if side:
        queue.extend((0 if side == "left" else PIECE - 1, y) for y in range(PIECE))
        depth = PIECE
    seen = set(queue)
    while queue:
        x, y = queue.popleft()
        if too_deep(y) or not ground(pixels[x, y]):
            continue
        pixels[x, y] = (0, 0, 0, 0)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if 0 <= n[0] < PIECE and 0 <= n[1] < PIECE and n not in seen:
                seen.add(n)
                queue.append(n)
    return piece


def darkest(piece):
    opaque = [c for c in piece.get_flattened_data() if c[3] > 0]
    return min(opaque, key=lambda c: c[0] + c[1] + c[2]) if opaque else (0, 0, 0, 255)


def outline(piece, colour):
    """Darkens the opaque pixels next to a transparent one below or beside them (the foot's new edge)."""
    pixels = piece.load()
    edge = [(x, y) for y in range(PIECE) for x in range(PIECE) if pixels[x, y][3] > 0 and any(
        0 <= x + dx < PIECE and 0 <= y + dy < PIECE and pixels[x + dx, y + dy][3] == 0
        for dx, dy in ((0, 1), (1, 0), (-1, 0)))]
    for x, y in edge:
        pixels[x, y] = colour


def rough_foot(piece, depth, amplitude):
    """A face's foot cut along a wavy line, the same at both ends of the piece (pieces side by side join), outlined."""
    import math
    pixels = piece.load()
    colour = darkest(piece)
    for x in range(PIECE):
        t = 2 * math.pi * x / PIECE
        wave = 0.6 * math.sin(2 * t + 0.7) + 0.4 * math.sin(3 * t + 2.1)
        cut = PIECE - depth + round(amplitude * wave)
        for y in range(max(0, cut), PIECE):
            pixels[x, y] = (0, 0, 0, 0)
    outline(piece, colour)
    return piece


def round_corner(piece, side, radius):
    """Rounds a face's outer bottom corner (side: left or right), outlined."""
    pixels = piece.load()
    colour = darkest(piece)
    cx = radius if side == "left" else PIECE - 1 - radius
    bottom = max((y for y in range(PIECE) for x in range(PIECE) if pixels[x, y][3] > 0), default=PIECE - 1)
    cy = bottom - radius
    for y in range(PIECE):
        for x in range(PIECE):
            outside = x < cx if side == "left" else x > cx
            if outside and y > cy and (x - cx) ** 2 + (y - cy) ** 2 > radius * radius:
                pixels[x, y] = (0, 0, 0, 0)
    outline(piece, colour)
    return piece


def lip_pieces(band):
    """The other sides of a level from its south edge (band: PIECE wide, the drop at its bottom)."""
    h = band.height
    back = Image.new("RGBA", (PIECE, PIECE))
    back.alpha_composite(band.transpose(Image.Transpose.FLIP_TOP_BOTTOM), (0, 0))
    left = Image.new("RGBA", (PIECE, PIECE))
    left.alpha_composite(band.rotate(-90, expand=True), (0, 0))  # its bottom (the drop) to the left
    right = Image.new("RGBA", (PIECE, PIECE))
    right.alpha_composite(band.rotate(90, expand=True), (PIECE - h, 0))
    pieces = {"back": back, "left": left, "right": right}
    for side, image in (("Left", left), ("Right", right)):
        corner = image.copy()
        corner.alpha_composite(back)
        pieces["back" + side] = corner
    # Inner corners: lower ground only across the corner; just the corner of the lip.
    corner = back.crop((0, 0, h, h))
    inner_left = Image.new("RGBA", (PIECE, PIECE))
    inner_left.alpha_composite(corner, (0, 0))
    inner_right = Image.new("RGBA", (PIECE, PIECE))
    inner_right.alpha_composite(corner.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (PIECE - h, 0))
    pieces["innerLeft"], pieces["innerRight"] = inner_left, inner_right
    return pieces


# The plateau of the preview: digits are levels; the faces (2 rows per level) are added below them.
MOCK = [
    "...............",
    "..111111111....",
    "..111111111....",
    "..111111111111.",
    "..11222221111..",
    "..11222221111..",
    "..111111111....",
    "..111111111....",
    "...............",
    "...............",
    "...............",
    "...............",
]


def mock_roles(rows=2):
    """Role per tile of MOCK, as IslandPlan.CliffRole would choose it (no stairs)."""
    h, w = len(MOCK), len(MOCK[0])
    level = [[0 if c == "." else int(c) for c in line] for line in MOCK]
    face = [[0] * w for _ in range(h)]
    face_of = [[0] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            lv = level[y][x]
            if lv and y + 1 < h and level[y + 1][x] < lv:
                for k in range(1, rows * (lv - level[y + 1][x]) + 1):
                    if y + k < h and level[y + k][x] < lv and face_of[y + k][x] < lv:
                        face[y + k][x], face_of[y + k][x] = k, lv
    height = [[max(level[y][x], face_of[y][x]) for x in range(w)] for y in range(h)]

    def lower(x, y, lv):
        return 0 <= x < w and 0 <= y < h and height[y][x] < lv and face_of[y][x] < lv

    roles = {}
    for y in range(h):
        for x in range(w):
            if face[y][x]:
                lv = face_of[y][x]
                length, j = face[y][x], y + 1
                while j < h and face_of[j][x] == lv and face[j][x] == face[j - 1][x] + 1:
                    length, j = face[j][x], j + 1
                row = "Top" if face[y][x] == 1 else "Bottom"

                def is_face(i):
                    return 0 <= i < w and face[y][i] > 0 and face_of[y][i] == lv
                left_end, right_end = not is_face(x - 1), not is_face(x + 1)
                if left_end != right_end:
                    roles[(x, y)] = ("left" if left_end else "right") + row, x
                else:
                    roles[(x, y)] = ("top" if face[y][x] == 1 else "bottom" if face[y][x] >= length else "middle"), x
                continue
            lv = level[y][x]
            if not lv:
                continue
            n, wst, est = lower(x, y - 1, lv), lower(x - 1, y, lv), lower(x + 1, y, lv)
            role = None
            if n:
                role = "backLeft" if wst else "backRight" if est else "back"
            elif wst or est:
                role = "left" if wst else "right"
            elif lower(x - 1, y - 1, lv):
                role = "innerLeft"
            elif lower(x + 1, y - 1, lv):
                role = "innerRight"
            if role:
                roles[(x, y)] = role, x
    return roles, w, h, height


def mock_preview(key, pieces, floor):
    roles, w, h, height = mock_roles()
    image = Image.new("RGBA", (w * PIECE, h * PIECE))
    for y in range(h):
        for x in range(w):
            shade = 1 - 0.06 * (2 - height[y][x])
            image.paste(tuple(int(c * shade) for c in floor) + (255,), (x * PIECE, y * PIECE, (x + 1) * PIECE, (y + 1) * PIECE))
    for (x, y), (role, column) in roles.items():
        variants = pieces.get(role) or pieces.get("bottom" if role == "middle" else "", [])
        if variants:
            image.alpha_composite(variants[column % len(variants)], (x * PIECE, y * PIECE))
    return image


def main(data_dir, styles_path, out_dir, preview=None):
    data = Path(data_dir)
    styles = json.loads(Path(styles_path).read_text(encoding="utf-8"))["styles"]
    bg_list = FileType.BG_LIST_DAT.deserialize((data / "MAP_BG/bg_list.dat").read_bytes())
    by_name = {level.bma_name.upper(): level for level in bg_list.level}
    maps = {}

    def layers(name):
        if name not in maps:
            level = by_name[name.upper()]
            bma, bpc, bpl, bpas = level.get_bma(str(data)), level.get_bpc(str(data)), level.get_bpl(str(data)), level.get_bpas(str(data))
            lower = layer_image(bma, bpc, bpl, bpas, 0)
            upper = layer_image(bma, bpc, bpl, bpas, 1) if bma.number_of_layers > 1 else Image.new("RGBA", lower.size)
            both = lower.copy()
            both.alpha_composite(upper)
            maps[name] = {"lower": lower, "upper": upper, "both": both}
        return maps[name]

    rows, mocks = [], []
    for style in styles:
        source = layers(style["map"])[style.get("layer", "lower")]
        ground = style.get("ground")
        colours = ground_colours(source, ground["samples"]) if ground else []
        foot = style.get("foot")
        foot_colours = ground_colours(source, foot["samples"]) if foot and "samples" in foot else []
        target = Path(out_dir) / style["key"]
        target.mkdir(parents=True, exist_ok=True)
        for old in target.glob("*.png"):
            old.unlink()
        made = {}
        for role, corners in style["pieces"].items():
            pieces = []
            for corner in corners:
                # [x, y] in the style's map, or {"map", "layer", "at"} from another map.
                image = source
                if isinstance(corner, dict):
                    image = layers(corner["map"])[corner.get("layer", "lower")]
                    corner = corner["at"]
                elif role in style.get("both", []):
                    image = layers(style["map"])["both"]
                x, y = corner
                piece = image.crop((x, y, x + PIECE, y + PIECE))
                if ground and role in ground["roles"]:
                    side = "left" if role.startswith("left") else "right" if role.startswith("right") else None
                    clear_ground(piece, colours, ground.get("tolerance", 0), ground.get("depth", PIECE), side)
                if foot and role in foot["roles"]:
                    if "samples" in foot:
                        clear_ground(piece, foot_colours, foot.get("tolerance", 0), foot.get("depth", PIECE), edge="bottom")
                    else:
                        rough_foot(piece, foot.get("depth", 4), foot.get("amplitude", 2))
                pieces.append(piece)
            made[role] = pieces
        if style.get("mirrorLoop"):
            # The rows of a face run A B ... Z then mirrored Z ... B A: no seam where the run starts over.
            for role in ("top", "middle", "bottom"):
                if role in made:
                    made[role] = made[role] + [p.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for p in reversed(made[role])]
        for role, of in style.get("mirror", {}).items():
            made[role] = [p.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for p in made[of]]
        lip = style.get("lip")
        if lip:
            top, bottom = lip["rows"]
            band = made[lip["piece"]][0].crop((0, top, PIECE, bottom))
            for role, piece in lip_pieces(band).items():
                made[role] = [piece]
            if lip.get("ends"):
                # The ends of a face: its first column, edged with the lip of the side above it.
                for side in ("left", "right"):
                    for row, of in (("Top", "top"), ("Bottom", "bottom")):
                        piece = made[of][0].copy()
                        edge = made[side][0]
                        if row == "Bottom":
                            # Down to the foot only, its outer corner rounded.
                            edge = edge.copy()
                            edge.putalpha(Image.composite(edge.getchannel("A"), Image.new("L", edge.size), piece.getchannel("A")))
                        piece.alpha_composite(edge)
                        if row == "Bottom" and lip.get("corner"):
                            round_corner(piece, side, lip["corner"])
                        made[side + row] = [piece]
        shadow = style.get("shadow")
        if shadow:
            # Cast on the ground at a face's foot: darkest at the top, fading out `depth` pixels down.
            piece = Image.new("RGBA", (PIECE, PIECE))
            pixels = piece.load()
            for y in range(shadow["depth"]):
                alpha = round(255 * shadow["alpha"] * (1 - y / shadow["depth"]) ** 1.5)
                for x in range(PIECE):
                    pixels[x, y] = tuple(shadow.get("colour", (20, 16, 24))) + (alpha,)
            made["shadow"] = [piece]
        for role, pieces in made.items():
            for n, piece in enumerate(pieces):
                piece.save(target / f"{role}_{n}.png")
            rows.append((style["key"], role, pieces))
        mocks.append(mock_preview(style["key"], made, tuple(style.get("previewFloor", (90, 160, 70)))))
        print(f"{style['key']}: {sum(len(p) for p in made.values())} pieces")
    if preview:
        scale = 3
        width = max(260 + 8 * (PIECE * scale + 4), sum(m.width * 2 + 8 for m in mocks))
        mock_height = max(m.height * 2 for m in mocks)
        sheet = Image.new("RGBA", (width, len(rows) * (PIECE * scale + 4) + mock_height + 8), (40, 40, 40, 255))
        draw = ImageDraw.Draw(sheet)
        for k, (key, role, pieces) in enumerate(rows):
            y = k * (PIECE * scale + 4)
            draw.text((4, y + 30), f"{key} {role}", fill=(255, 255, 255, 255))
            for n, piece in enumerate(pieces[:8]):
                big = piece.resize((PIECE * scale, PIECE * scale), Image.NEAREST)
                back = Image.new("RGBA", big.size, (90, 160, 70, 255))  # grass behind, to show transparency
                back.alpha_composite(big)
                sheet.paste(back, (260 + n * (PIECE * scale + 4), y))
        x = 0
        for mock in mocks:
            sheet.paste(mock.resize((mock.width * 2, mock.height * 2), Image.NEAREST), (x, len(rows) * (PIECE * scale + 4) + 8))
            x += mock.width * 2 + 8
        sheet.save(preview)


if __name__ == "__main__":
    main(*sys.argv[1:5])
