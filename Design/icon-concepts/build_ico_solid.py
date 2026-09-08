# -*- coding: utf-8 -*-
"""Build borderless solid-background .ico + 256 PNG masters.

Light icon: subject on solid white. Dark icon: subject on solid black.
No outer tile, no inner frame.
"""
import math
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

JOBS = [
    {"name": "light", "src": "13-light-whitebg.png", "bg": (255, 255, 255),
     "out_ico": "Pulsar-light.ico", "out_png": "Pulsar-light-256.png"},
    {"name": "dark", "src": "12-dark-borderless.png", "bg": (0, 0, 0),
     "out_ico": "Pulsar-dark.ico", "out_png": "Pulsar-dark-256.png"},
]

MASK_T = 28          # distance from bg color above which a pixel counts as subject
PADDING_RATIO = 0.10
ICON_SIZES = [16, 24, 32, 48, 64, 128, 256]


def build_master(im, bg):
    im = im.convert("RGB")
    w, h = im.size
    px = im.load()
    mask = Image.new("L", (w, h), 0)
    mp = mask.load()
    br, bgc, bb = bg
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            d = math.sqrt((r - br) ** 2 + (g - bgc) ** 2 + (b - bb) ** 2)
            mp[x, y] = 255 if d > MASK_T else 0
    bbox = mask.getbbox()
    x0, y0, x1, y1 = bbox
    sw, sh = x1 - x0, y1 - y0
    pad = int(max(sw, sh) * PADDING_RATIO)
    x0 = max(0, x0 - pad); y0 = max(0, y0 - pad)
    x1 = min(w, x1 + pad); y1 = min(h, y1 + pad)
    sub = im.crop((x0, y0, x1, y1))
    side = max(sub.width, sub.height)
    canvas = Image.new("RGB", (side, side), bg)
    canvas.paste(sub, ((side - sub.width) // 2, (side - sub.height) // 2))
    return canvas.resize((256, 256), Image.LANCZOS)


def main():
    for job in JOBS:
        im = Image.open(os.path.join(HERE, job["src"]))
        master = build_master(im, job["bg"])
        w, h = master.size
        corners = [master.getpixel((1, 1)), master.getpixel((w - 2, 1)),
                   master.getpixel((1, h - 2)), master.getpixel((w - 2, h - 2))]
        print("[%s] corner colors (must match bg %s): %s" % (
            job["name"], job["bg"], corners))
        master.save(os.path.join(HERE, job["out_png"]))
        master.save(os.path.join(HERE, job["out_ico"]),
                    format="ICO", sizes=[(s, s) for s in ICON_SIZES])
        print("[%s] saved %s / %s" % (job["name"], job["out_png"], job["out_ico"]))


if __name__ == "__main__":
    main()
