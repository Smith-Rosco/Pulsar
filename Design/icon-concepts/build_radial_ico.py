# -*- coding: utf-8 -*-
"""Build radial-dial light/dark .ico + 256 masters (and a transparent UI mark).

The two renders keep their own background gradients (light ice field / deep
indigo field); only subject bbox is normalized so both pair at identical scale.
Transparent mark is white-keyed from the LIGHT render (clean on light surfaces).

Since 2026-09-09: masters are output as ROUNDED-RECTANGLE tiles (RGBA, corners
transparent, radius CORNER_RADIUS px at 256). Dial geometry is safe for this
radius (dial radius ~90/256, corner circle bound ~37 px; see repo journal).
"""
import math
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

CORNER_RADIUS = 36  # px on the 256 master; scales proportionally for ico sizes

JOBS = [
    {"name": "light", "src": "15-radial-dial-light.png", "mask_t": 40,
     "out_ico": "Pulsar-light.ico", "out_png": "Pulsar-light-256.png"},
    {"name": "dark", "src": "14-radial-dial-noborder.png", "mask_t": 60,
     "out_ico": "Pulsar-dark.ico", "out_png": "Pulsar-dark-256.png"},
]

PADDING_RATIO = 0.10
ICON_SIZES = [16, 24, 32, 48, 64, 128, 256]


def rounded_rect_alpha(size, radius):
    """Anti-aliased alpha mask: opaque inside the rounded rect, transparent
    corners. Smooth 1px falloff at the corner arc (distance-based)."""
    mask = Image.new("L", (size, size), 0)
    mp = mask.load()
    r = float(radius)
    inner = size - 1 - r
    for y in range(size):
        for x in range(size):
            cx = r if x < r else (inner if x > inner else float(x))
            cy = r if y < r else (inner if y > inner else float(y))
            d = math.hypot(x - cx, y - cy)
            if d >= r + 0.5:
                a = 0
            elif d <= r - 0.5:
                a = 255
            else:
                a = int(255 * (r + 0.5 - d))
            mp[x, y] = a
    return mask



def corner_anchor(px, w, h, k=12):
    pts = []
    for y in range(k):
        for x in range(k):
            pts.append(px[x, y])
            pts.append(px[w - 1 - x, y])
            pts.append(px[x, h - 1 - y])
            pts.append(px[w - 1 - x, h - 1 - y])
    n = len(pts)
    return tuple(sum(p[c] for p in pts) // n for c in range(3))


def centered_square_master(path, mask_t):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    px = im.load()
    ar, ag, ab = corner_anchor(px, w, h)
    mask = Image.new("L", (w, h), 0)
    mp = mask.load()
    t2 = mask_t * mask_t
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            dr, dg, db = r - ar, g - ag, b - ab
            mp[x, y] = 255 if dr * dr + dg * dg + db * db > t2 else 0
    x0, y0, x1, y1 = mask.getbbox()
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    # never exceed the source frame; otherwise the centered square crop is clamped
    side = min(int(max(x1 - x0, y1 - y0) * (1 + 2 * PADDING_RATIO)), w, h)
    l = min(max(0, int(cx - side / 2)), w - side)
    t = min(max(0, int(cy - side / 2)), h - side)
    sub = im.crop((l, t, l + side, t + side))
    master = sub.resize((256, 256), Image.LANCZOS).convert("RGBA")
    master.putalpha(rounded_rect_alpha(256, CORNER_RADIUS))
    return master


def transparent_mark_from_render(path):
    # Source is Doubao's no-background render (RGB; model emits a faint near-white
    # checker, not a real alpha channel). Only NEAR-NEUTRAL bright pixels count as
    # background, so saturated wedge bodies/highlights stay 100% opaque; the narrow
    # anti-alias rim is feathered and white-decontaminated (un-blend over ~white).
    A_HI, A_LO, CHROMA_KEEP = 238, 215, 18
    BG = 248.0
    im = Image.open(path).convert("RGB")
    w, h = im.size
    px = im.load()
    out = Image.new("RGBA", (w, h)); op = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            mx, mn = max(r, g, b), min(r, g, b)
            chroma = mx - mn
            bgness = mn if chroma < CHROMA_KEEP else 0
            if bgness >= A_HI:
                a = 0
            elif bgness <= A_LO:
                a = 255
            else:
                a = int(255 * (A_HI - bgness) / (A_HI - A_LO))
            if 0 < a < 255:
                af = a / 255.0
                r = max(0, min(255, int((r - (1 - af) * BG) / af)))
                g = max(0, min(255, int((g - (1 - af) * BG) / af)))
                b = max(0, min(255, int((b - (1 - af) * BG) / af)))
            op[x, y] = (r, g, b, a)
    al = out.getchannel("A")
    x0, y0, x1, y1 = al.point(lambda v: 255 if v > 10 else 0).getbbox()
    pad = int(max(x1 - x0, y1 - y0) * PADDING_RATIO)
    x0 = max(0, x0 - pad); y0 = max(0, y0 - pad)
    x1 = min(w, x1 + pad); y1 = min(h, y1 + pad)
    sub = out.crop((x0, y0, x1, y1))
    side = max(sub.size)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(sub, ((side - sub.width) // 2, (side - sub.height) // 2), sub)
    return canvas.resize((256, 256), Image.LANCZOS)


def main():
    for job in JOBS:
        master = centered_square_master(os.path.join(HERE, job["src"]), job["mask_t"])
        master.save(os.path.join(HERE, job["out_png"]))
        master.save(os.path.join(HERE, job["out_ico"]),
                    format="ICO", sizes=[(s, s) for s in ICON_SIZES])
        corners = [master.getpixel((1, 1)), master.getpixel((254, 1)),
                   master.getpixel((1, 254)), master.getpixel((254, 254))]
        print("[%s] corners=%s saved" % (job["name"], corners))

    mark = transparent_mark_from_render(os.path.join(HERE, "16-radial-mark-nobg.png"))
    mark.save(os.path.join(HERE, "pulsar-mark-256.png"))
    print("[mark] transparent pulsar-mark-256.png saved (from Doubao no-bg render)")


if __name__ == "__main__":
    main()
