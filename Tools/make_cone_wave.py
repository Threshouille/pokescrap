"""Draws a cone-shaped shock wave sheet (pointing right, apex at the left middle) in flat pixel art.
Usage: python3 make_cone_wave.py <out.png> [size=96] [frames=5] [angle=90]"""
import math
import sys

from PIL import Image

out = sys.argv[1]
size = int(sys.argv[2]) if len(sys.argv) > 2 else 96
frames = int(sys.argv[3]) if len(sys.argv) > 3 else 5
half = math.radians((float(sys.argv[4]) if len(sys.argv) > 4 else 90) / 2)
EDGE, ARC, FILL, DARK = (255, 255, 255, 255), (255, 250, 220, 255), (255, 255, 255, 70), (200, 205, 220, 200)

sheet = Image.new("RGBA", (size * frames, size), (0, 0, 0, 0))
cy = size / 2
for f in range(frames):
    t = (f + 1) / frames
    front = t * (size - 1)            # radius of the wave front
    fade = 1 - max(0, f - frames + 3) * 0.3  # the last frames fade
    for y in range(size):
        for x in range(size):
            dx, dy = x + 0.5, y + 0.5 - cy
            r = math.hypot(dx, dy)
            a = abs(math.atan2(dy, dx))
            if a > half or r > front:
                continue
            col = None
            if r > front - 3:
                col = ARC                                   # bright front
            elif r > front - 7 and f > 0:
                col = DARK
            elif half - a < 1.6 / max(r, 1):
                col = EDGE if r > front * 0.3 else None      # the cone's sides
            elif r > front * 0.35:
                col = FILL                                   # translucent body
            if col:
                sheet.putpixel((f * size + x, y), col[:3] + (int(col[3] * fade),))
sheet.save(out)
print(out, sheet.size)
