"""Turn the solid blue HUD badges into small 'glass' buttons (Peter: transparent, glass effect).
Keeps each glyph (with its own colour), drops the blue tile, and draws a frosted see-through tile.
Usage: python3 glass_badges.py <badge dir>   (overwrites icon_*.png, keeps originals in <dir>/../BadgesSolid)
"""
import sys, os, glob, shutil, numpy as np
from PIL import Image
from scipy import ndimage

def glass(src):
    a = np.asarray(Image.open(src).convert('RGBA')).astype(np.float32)
    h, w = a.shape[:2]
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    tile = al > 128
    inner = ndimage.binary_erosion(tile, iterations=int(w * 0.12))
    # glyph coverage: the tile is strongly blue (b - r ~ 100+), glyphs are not
    cov = np.clip((95 - (b - r)) / 70.0, 0, 1) * inner
    cov = ndimage.gaussian_filter(cov, 0.6)
    bg = np.array([60, 110, 230], np.float32)
    ca = np.maximum(cov[..., None], 1e-3)
    rgb = np.clip((a[..., :3] - bg * (1 - ca)) / ca, 0, 255)
    # pale blue-ish glyph pixels left from the tile become white
    out = np.zeros((h, w, 4), np.float32)
    # frosted tile: rounded shape a little inside the old one
    shape = ndimage.binary_erosion(tile, iterations=int(w * 0.04))
    soft = np.clip(ndimage.distance_transform_edt(shape) / 1.5, 0, 1)
    ys = np.linspace(0, 1, h)[:, None]
    rim = shape & ~ndimage.binary_erosion(shape, iterations=max(2, int(w * 0.018)))
    # QC: a see-through dark smoked glass (about 45% black) so white glyphs stay readable over bright
    # trees and grass, with a light rim and a soft sheen on the top half
    dark = np.array([22, 30, 44], np.float32)
    white = np.array([235, 245, 255], np.float32)
    top = shape & (np.arange(h)[:, None] < h * 0.45) & ~rim
    out[..., :3] = dark
    alpha = (0.50 - 0.10 * ys) * np.ones((1, w)) * soft
    # sheen: blend some white over the top half
    sheen_a = np.where(top, 0.10, 0.0)
    out[..., :3] = (out[..., :3] * alpha[..., None] * (1 - sheen_a[..., None]) + white * sheen_a[..., None])
    alpha = sheen_a + alpha * (1 - sheen_a)
    out[..., :3] = out[..., :3] / np.maximum(alpha[..., None], 1e-3)
    out[rim, :3] = white
    alpha = np.where(rim, 0.65, alpha)
    # glyph drop shadow
    sh = ndimage.gaussian_filter(ndimage.shift(cov, (w * 0.016, 0), order=1), w * 0.012) * 0.45
    base_rgb = out[..., :3] * alpha[..., None]
    base_a = alpha
    # composite shadow (black) over the glass
    base_rgb = base_rgb * (1 - sh[..., None])
    base_a = sh + base_a * (1 - sh)
    # composite glyph
    ga = cov
    comp_rgb = rgb * ga[..., None] + base_rgb * (1 - ga[..., None])
    comp_a = ga + base_a * (1 - ga)
    outc = np.zeros((h, w, 4), np.float32)
    outc[..., :3] = np.where(comp_a[..., None] > 1e-3, comp_rgb / np.maximum(comp_a[..., None], 1e-3), 0)
    outc[..., 3] = comp_a * 255
    return Image.fromarray(np.clip(outc, 0, 255).astype(np.uint8))

if __name__ == '__main__':
    d = sys.argv[1]
    keep = os.path.join(os.path.dirname(os.path.abspath(d)), 'BadgesSolid')
    os.makedirs(keep, exist_ok=True)
    for f in sorted(glob.glob(os.path.join(d, 'icon_*.png'))):
        solid = os.path.join(keep, os.path.basename(f))
        if not os.path.exists(solid):
            shutil.copy(f, solid)
        glass(solid).save(f)
        print(os.path.basename(f))
