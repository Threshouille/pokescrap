"""Sheet of the original effect animations of chosen moves (move_effects.json + render_effects.py output):
one row per move, every frame of each of its effects (file / animation), 2x. Local reference only.
Usage: python3 move_effects_sheet.py <move_effects.json> <effects dir> <out.png> <move id>..."""
import json
import os
import sys

from PIL import Image, ImageDraw

SCALE, LABEL, MAXW = 2, 170, 1600


def strip(folder, anim):
    path = os.path.join(folder, f"A-{anim:02d}-00-N.png")
    return Image.open(path).convert("RGBA") if os.path.exists(path) else None


def main():
    table = {m["move"]: m for m in json.load(open(sys.argv[1]))}
    src, out, ids = sys.argv[2], sys.argv[3], [int(x) for x in sys.argv[4:]]
    rows = []
    for i in ids:
        m = table[i]
        parts = []
        for s in m["steps"]:
            if s["file"] == 0:
                continue  # file 0: shared screen and charge effects (flat colours), not the move's own art
            img = strip(os.path.join(src, f"{s['file']:04d}"), s["unk2"])
            if img is not None:
                parts.append((f"f{s['file']} a{s['unk2']}", img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST)))
        h = max([p[1].height for p in parts] + [40]) + 14
        row = Image.new("RGBA", (MAXW, h), (24, 24, 32, 255))
        d = ImageDraw.Draw(row)
        d.text((4, 4), f"{i} {m['name']}", fill=(255, 220, 90, 255))
        d.text((4, 18), m["name_en"], fill=(150, 150, 170, 255))
        x = LABEL
        for label, img in parts:
            if x + img.width > MAXW:
                img = img.crop((0, 0, MAXW - x, img.height))
            d.text((x, 0), label, fill=(120, 200, 255, 255))
            row.alpha_composite(img, (x, 12))
            x += img.width + 12
            d.line((x - 6, 0, x - 6, h), fill=(60, 60, 80, 255))
            if x >= MAXW:
                break
        rows.append(row)
    page = Image.new("RGBA", (MAXW, sum(r.height + 2 for r in rows)), (10, 10, 14, 255))
    y = 0
    for r in rows:
        page.alpha_composite(r, (0, y))
        y += r.height + 2
    page.save(out)
    print(out, page.size)


if __name__ == "__main__":
    main()
