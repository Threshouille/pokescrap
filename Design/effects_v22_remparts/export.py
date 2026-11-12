"""Exports the rampart sheets (lots 4 and 4b, Claude Design canvas « Remparts et dolmen à engrenage ») to PNG.

The canvas's artboards are copied in src/ (their SVG <defs> hold every frame). Each sheet is drawn at its real size
in one page, rendered by headless Chrome, cut out, then written to png/ and Assets/Resources/Effects/. Also builds
the tiles the game lays (DungeonGenerator, RampartTiles):
  rampart_tiles.png: 16 × 16 tiles of 24 px, tile n = the top of a rampart tile whose 8 neighbours are rampart where
    bit set (1 N, 2 NE, 4 E, 8 SE, 16 S, 32 SW, 64 W, 128 NW), made of 12 px quarters of rampart_auto.png;
  rampart_faces.png: 4 faces of one tile (left end, middle, right end, alone).

Usage: Tools/.venv/bin/python -I Design/effects_v22_remparts/export.py
"""
import pathlib
import re
import subprocess
import sys
import tempfile

from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent.parent
OUT = HERE / "png"
EFFECTS = ROOT / "Assets" / "Resources" / "Effects"
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"


def strip(n, ids, step):
    return "".join(f'<use href="#{ids}{k}" x="{k * step}"></use>' for k in range(n))


AUTO = """
<rect width="24" height="24" fill="url(#slab)"></rect><rect width="24" height="4" fill="url(#bN)"></rect>
<rect y="20" width="24" height="4" fill="url(#bS)"></rect><rect width="4" height="24" fill="url(#bW)"></rect>
<rect x="20" width="4" height="24" fill="url(#bE)"></rect>
<use href="#cnr"></use><use href="#cnr" x="20"></use><use href="#cnr" y="20"></use><use href="#cnr" x="20" y="20"></use>
<rect x="24" width="24" height="24" fill="url(#slab)"></rect>
<use href="#cnr" x="24"></use><use href="#cnr" x="44"></use><use href="#cnr" x="24" y="20"></use><use href="#cnr" x="44" y="20"></use>
<rect y="24" width="48" height="48" fill="url(#slab)"></rect><rect y="24" width="48" height="4" fill="url(#bN)"></rect>
<rect y="68" width="48" height="4" fill="url(#bS)"></rect><rect y="24" width="4" height="48" fill="url(#bW)"></rect>
<rect x="44" y="24" width="4" height="48" fill="url(#bE)"></rect>
<use href="#cnr" y="24"></use><use href="#cnr" x="44" y="24"></use><use href="#cnr" y="68"></use><use href="#cnr" x="44" y="68"></use>
"""

# name: (width, height, markup)
SHEETS = {
    "gear_dolmen": (48, 72, '<use href="#dolIdle"></use>'),
    "gear_dolmen_insert": (480, 72, strip(10, "di", 48)),
    "gear_dolmen_active": (288, 72, strip(6, "da", 48)),
    "rampart_auto": (48, 72, AUTO),
    "rampart_face": (72, 24, '<use href="#kFace3"></use><use href="#kVein3" color="#4a5a6a"></use>'),
    "rampart_face_lit": (432, 24, strip(6, "kFl", 72)),
    "rampart_fill": (48, 48, '<rect width="48" height="48" fill="url(#kFill)"></rect>'),
    "rampart_gate": (72, 24, '<use href="#kGC"></use>'),
    "rampart_gate_opening": (720, 24, strip(10, "kGo", 72)),
    "rampart_gate_open": (432, 24, strip(6, "kGp", 72)),
    "rampart_gate_back": (72, 48, '<use href="#kHC"></use>'),
    "rampart_gate_back_opening": (720, 48, strip(10, "kHo", 72)),
    "rampart_gate_back_open": (432, 48, strip(6, "kHp", 72)),
    "rampart_gate_side": (48, 48, '<use href="#kTC"></use>'),
    "rampart_gate_side_opening": (480, 48, strip(10, "kTo", 48)),
    "rampart_gate_side_open": (288, 48, strip(6, "kTp", 48)),
    "gear_dolmen_quad": (360, 72, strip(5, "kQ", 72)),
    "gear_dolmen_quad_slot": (192, 24, strip(8, "kSl", 24)),
    "gear_dolmen_quad_active": (432, 72, strip(6, "kQa", 72)),
}


def defs(path):
    text = path.read_text(encoding="utf-8")
    return re.search(r"<defs>(.*?)</defs>", text, re.S).group(1)


def render():
    width = max(w for w, _, _ in SHEETS.values())
    y, parts, places = 0, [], {}
    for name, (w, h, markup) in SHEETS.items():
        parts.append(f'<svg viewBox="0 0 {w} {h}" width="{w}" height="{h}" style="position:absolute;left:0;top:{y}px">{markup}</svg>')
        places[name] = (y, w, h)
        y += h + 8
    page = ("<!doctype html><html><head><meta charset='utf-8'><style>html,body{margin:0;background:transparent}"
            "svg{shape-rendering:crispEdges;display:block}</style></head><body>"
            "<svg width='0' height='0' style='position:absolute'><defs>"
            + defs(HERE / "src" / "lot4.dc.html") + defs(HERE / "src" / "lot4b.dc.html")
            + "</defs></svg>" + "".join(parts) + "</body></html>")
    with tempfile.TemporaryDirectory() as tmp:
        html = pathlib.Path(tmp) / "sheets.html"
        html.write_text(page, encoding="utf-8")
        shot = pathlib.Path(tmp) / "sheets.png"
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                        "--default-background-color=00000000", f"--window-size={width + 40},{y + 40}",
                        f"--screenshot={shot}", html.as_uri()], check=True, capture_output=True)
        full = Image.open(shot).convert("RGBA")
        full.load()
    return {name: full.crop((0, top, w, top + h)) for name, (top, w, h) in places.items()}


def tiles(auto):
    def quarter(x, y):
        return auto.crop((x, y, x + 12, y + 12))

    # Per quarter: (vertical bit, horizontal bit, diagonal bit, offset in the tile, and its pieces:
    # outer corner, edge open vertically, edge open horizontally, inner corner, inside).
    quarters = [
        (1, 64, 128, (0, 0), (0, 0), (24, 24), (0, 48), (24, 0), (24, 48)),
        (1, 4, 2, (12, 0), (12, 0), (12, 24), (36, 48), (36, 0), (12, 48)),
        (16, 64, 32, (0, 12), (0, 12), (24, 60), (0, 36), (24, 12), (24, 36)),
        (16, 4, 8, (12, 12), (12, 12), (12, 60), (36, 36), (36, 12), (12, 36)),
    ]
    sheet = Image.new("RGBA", (16 * 24, 16 * 24))
    for mask in range(256):
        tile = Image.new("RGBA", (24, 24))
        for v, h, d, at, outer, open_v, open_h, inner, inside in quarters:
            wall_v, wall_h, wall_d = mask & v, mask & h, mask & d
            piece = (outer if not wall_v and not wall_h else open_v if not wall_v else open_h if not wall_h
                     else inner if not wall_d else inside)
            tile.paste(quarter(*piece), at)
        sheet.paste(tile, ((mask % 16) * 24, (mask // 16) * 24))
    return sheet


def faces(face):
    sheet = Image.new("RGBA", (96, 24))
    sheet.paste(face.crop((0, 0, 72, 24)), (0, 0))
    sheet.paste(face.crop((0, 0, 12, 24)), (72, 0))
    sheet.paste(face.crop((60, 0, 72, 24)), (84, 0))
    return sheet


def main():
    OUT.mkdir(exist_ok=True)
    sheets = render()
    sheets["rampart_tiles"] = tiles(sheets["rampart_auto"])
    sheets["rampart_faces"] = faces(sheets["rampart_face"])
    for name, image in sheets.items():
        image.save(OUT / f"{name}.png")
        image.save(EFFECTS / f"{name}.png")
    print(f"{len(sheets)} planches -> {OUT} et {EFFECTS}")


if __name__ == "__main__":
    sys.exit(main())
