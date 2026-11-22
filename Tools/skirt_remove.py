"""Remove the light lavender drop-shadow 'skirt' that background removal left under building plates.

The skirt makes buildings look like they float on a glowing pad. Only lavender pixels that touch the
transparent background AND lie below the plate (below the lowest non-lavender pixel of their column)
are removed, so white/blue walls are kept. Then the edge is re-feathered and de-fringed.
Usage: python3 skirt_remove.py in.png out.png
"""
import sys, numpy as np
from PIL import Image
from scipy import ndimage

def lavender(a):
    r, g, b, al = [a[..., i].astype(int) for i in range(4)]
    mn = np.minimum(np.minimum(r, g), b); mx = np.maximum(np.maximum(r, g), b)
    lav = (b > r + 14) & (b > g + 10) & (np.minimum(r, g) > 120)
    grey = (mn > 150) & (b >= r - 3) & (mx - mn < 40)
    return (lav | grey) & (al > 0)

def skirt_mask(a):
    h, w = a.shape[:2]
    al = a[..., 3]
    lav = lavender(a)
    solid = (al > 128) & ~lav
    ys = np.arange(h)[:, None].repeat(w, 1)
    has = solid.any(0)
    bottom = np.where(has, (ys * solid).max(0), -1)
    below = ys > bottom[None, :]
    trans = al < 20
    lab, _ = ndimage.label(lav | trans)
    seed = ndimage.binary_dilation(trans, iterations=2)
    ids = np.unique(lab[seed & (lav | trans)]); ids = ids[ids > 0]
    m = np.isin(lab, ids) & lav & below & (ys > h * 0.35)
    # grow a little into the anti-aliased blend between skirt and plate
    m = ndimage.binary_dilation(m, iterations=2) & (lav | (al < 250)) & below
    return m

def clean(a):
    a = a.copy()
    m = skirt_mask(a)
    if not m.any():
        return a, 0.0
    fg = (a[..., 3] > 128) & ~m
    fg = ndimage.binary_opening(fg, iterations=1)
    lab, n = ndimage.label(fg)
    if n > 1:
        sizes = ndimage.sum(fg, lab, range(1, n + 1))
        fg = lab == (int(np.argmax(sizes)) + 1)
    inner = ndimage.binary_erosion(fg, iterations=3)
    # copy colours from 3px inside onto the new edge band so no lavender tint survives
    _, (iy, ix) = ndimage.distance_transform_edt(~inner, return_indices=True)
    band = fg & ~inner & ndimage.binary_dilation(m, iterations=6)
    a[band, :3] = a[iy[band], ix[band], :3]
    dist = ndimage.distance_transform_edt(fg)
    newa = np.clip(dist / 2.0, 0, 1) * 255
    keep = ~ndimage.binary_dilation(m, iterations=6)
    a[..., 3] = np.where(keep, np.minimum(a[..., 3], np.where(fg, 255, a[..., 3])), newa).astype(np.uint8)
    a[~fg & ~keep, 3] = 0
    return a, m.mean()

if __name__ == '__main__':
    a = np.asarray(Image.open(sys.argv[1]).convert('RGBA'))
    out, frac = clean(a)
    Image.fromarray(out).save(sys.argv[2])
    print(sys.argv[1], round(frac * 100, 2))
