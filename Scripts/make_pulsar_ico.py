import argparse
from pathlib import Path

from PIL import Image, ImageChops, ImageOps


def _looks_like_white(rgb):
    return rgb[0] >= 250 and rgb[1] >= 250 and rgb[2] >= 250


def _corner_samples(im_rgb):
    w, h = im_rgb.size
    pts = [
        (0, 0),
        (w - 1, 0),
        (0, h - 1),
        (w - 1, h - 1),
    ]
    return [im_rgb.getpixel(p) for p in pts]


def _ensure_rgba_with_transparency(im: Image.Image) -> Image.Image:
    """Ensure RGBA; if no alpha, attempt to derive alpha from white background."""

    if im.mode in ("RGBA", "LA"):
        return im.convert("RGBA")

    rgb = im.convert("RGB")
    corners = _corner_samples(rgb)
    if not all(_looks_like_white(c) for c in corners):
        # No alpha and corners are not white => do not guess transparency.
        return rgb.convert("RGBA")

    bg = Image.new("RGB", rgb.size, (255, 255, 255))
    diff = ImageChops.difference(rgb, bg)
    mask = diff.convert("L")
    mask = ImageOps.autocontrast(mask)

    rgba = rgb.convert("RGBA")
    rgba.putalpha(mask)
    return rgba


def _pad_to_square(im: Image.Image, pad_ratio: float) -> Image.Image:
    w, h = im.size
    side = max(w, h)
    pad = int(round(side * pad_ratio))
    side_padded = side + (2 * pad)

    out = Image.new("RGBA", (side_padded, side_padded), (0, 0, 0, 0))
    x = (side_padded - w) // 2
    y = (side_padded - h) // 2
    out.alpha_composite(im, (x, y))
    return out


def _trim_to_alpha_bounds(im: Image.Image, bleed_px: int = 2) -> Image.Image:
    """Trim transparent margins based on alpha channel bounds."""

    rgba = im.convert("RGBA")
    alpha = rgba.split()[3]
    bbox = alpha.getbbox()
    if bbox is None:
        return rgba

    left, top, right, bottom = bbox
    left = max(0, left - bleed_px)
    top = max(0, top - bleed_px)
    right = min(rgba.size[0], right + bleed_px)
    bottom = min(rgba.size[1], bottom + bleed_px)
    return rgba.crop((left, top, right, bottom))


def _trim_to_alpha_threshold_bounds(im: Image.Image, alpha_threshold: int, bleed_px: int = 2) -> Image.Image:
    """Trim using an alpha threshold so near-transparent AA doesn't inflate the bounds."""

    rgba = im.convert("RGBA")
    alpha = rgba.split()[3]

    # Build a binary mask for bbox computation only.
    # Keep pixels with alpha >= threshold.
    def to_mask(a: int) -> int:
        return 255 if a >= alpha_threshold else 0

    mask = alpha.point(to_mask)
    bbox = mask.getbbox()
    if bbox is None:
        return rgba

    left, top, right, bottom = bbox
    left = max(0, left - bleed_px)
    top = max(0, top - bleed_px)
    right = min(rgba.size[0], right + bleed_px)
    bottom = min(rgba.size[1], bottom + bleed_px)
    return rgba.crop((left, top, right, bottom))


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a multi-size .ico from a PNG.")
    parser.add_argument("--src", required=True, help="Source PNG path")
    parser.add_argument("--dst", required=True, help="Destination .ico path")
    parser.add_argument(
        "--pad",
        type=float,
        default=0.06,
        help="Extra padding ratio around the artwork (default: 0.06)",
    )
    parser.add_argument(
        "--trim-alpha-threshold",
        type=int,
        default=24,
        help="Alpha threshold (0-255) used for trimming bounds (default: 24)",
    )
    args = parser.parse_args()

    src = Path(args.src)
    dst = Path(args.dst)

    im = Image.open(src)
    im = _ensure_rgba_with_transparency(im)
    im = _trim_to_alpha_threshold_bounds(im, alpha_threshold=args.trim_alpha_threshold)
    im = _pad_to_square(im, pad_ratio=args.pad)

    dst.parent.mkdir(parents=True, exist_ok=True)

    sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
    im.save(dst, format="ICO", sizes=sizes)
    print(f"Wrote {dst} with sizes: {', '.join([str(s[0]) for s in sizes])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
