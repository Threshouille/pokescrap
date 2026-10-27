"""Contact sheets of the effects exported by render_effects.py: one cell per effect file (its first animation,
up to 5 frames sampled evenly), numbered. Usage: python3 effects_contact.py <effects dir> <out prefix>"""
import glob
import os
import sys

from PIL import Image, ImageDraw

FRAME, FRAMES, COLS, ROWS = 48, 5, 4, 12


def cell(folder):
    files = sorted(glob.glob(os.path.join(folder, "*.png")))
    img = Image.new("RGBA", (FRAME * FRAMES + 40, FRAME + 4), (24, 24, 32, 255))
    d = ImageDraw.Draw(img)
    d.text((2, 2), os.path.basename(folder), fill=(255, 220, 90, 255))
    d.text((2, 16), f"{len(files)}a", fill=(150, 150, 170, 255))
    if not files:
        return img
    strip = Image.open(files[0]).convert("RGBA")
    h = strip.height
    n = max(1, strip.width // h)
    picks = sorted({round(i * (n - 1) / max(1, FRAMES - 1)) for i in range(min(FRAMES, n))})
    for k, f in enumerate(picks):
        frame = strip.crop((f * h, 0, f * h + h, h))
        frame.thumbnail((FRAME, FRAME), Image.NEAREST)
        img.alpha_composite(frame, (40 + k * FRAME + (FRAME - frame.width) // 2, 2 + (FRAME - frame.height) // 2))
    return img


def main():
    src, prefix = sys.argv[1], sys.argv[2]
    folders = sorted(f for f in glob.glob(os.path.join(src, "*")) if os.path.isdir(f))
    per = COLS * ROWS
    for p in range(0, len(folders), per):
        cells = [cell(f) for f in folders[p:p + per]]
        w, h = cells[0].size
        page = Image.new("RGBA", (w * COLS, h * ROWS), (12, 12, 16, 255))
        for i, c in enumerate(cells):
            page.alpha_composite(c, ((i % COLS) * w, (i // COLS) * h))
        page.save(f"{prefix}_{p // per + 1}.png")
        print(f"{prefix}_{p // per + 1}.png")


if __name__ == "__main__":
    main()
