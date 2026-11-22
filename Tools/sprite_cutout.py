import sys, json, numpy as np
from PIL import Image
from scipy import ndimage
def _load_rgb(path):
    # transparent PNGs: flatten onto flat white so the usual backdrop logic applies
    img = Image.open(path)
    if img.mode in ('RGBA', 'LA', 'P'):
        img = img.convert('RGBA')
        if (np.asarray(img)[..., 3] < 10).mean() > 0.05:
            flat = Image.new('RGBA', img.size, (255, 255, 255, 255))
            flat.alpha_composite(img)
            img = flat
    return img.convert('RGB')
src, out, emis = sys.argv[1], sys.argv[2], sys.argv[3]
im = np.asarray(_load_rgb(src)).astype(np.float32)
h, w, _ = im.shape
# background colour = median of the border
border = np.concatenate([im[0], im[-1], im[:,0], im[:,-1]])
bg = np.median(border, axis=0)
d = np.sqrt(((im - bg) ** 2).sum(-1))
sat = im.max(-1) - im.min(-1)
cand = (d < 26) & (sat < 30)
if bg.min() > 245:  # white backdrop with a soft grey drop shadow: treat the light shadow as background too
    cand |= (im.min(-1) > 205) & (sat < 22)
lab, n = ndimage.label(cand)
edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:,0], lab[:,-1]]))) - {0}
bgmask = np.isin(lab, list(edge))
fg = ~bgmask
fg = ndimage.binary_opening(fg, iterations=1)
fg = ndimage.binary_fill_holes(fg)
lab2, n2 = ndimage.label(fg)
sizes = ndimage.sum(fg, lab2, range(1, n2 + 1))
fg = lab2 == (int(np.argmax(sizes)) + 1)
if bg.min() > 245:  # pure white backdrop leaves a bright rim: trim it
    fg = ndimage.binary_erosion(fg, iterations=1)
# soft edge: alpha ramps from d over a 2px band
dist_in = ndimage.distance_transform_edt(fg)
alpha = np.clip(dist_in / 2.0, 0, 1)
# de-fringe: un-mix bg from edge pixels
a3 = np.maximum(alpha[..., None], 1e-3)
rgb = np.where(alpha[..., None] < 1, np.clip((im - bg * (1 - a3)) / a3, 0, 255), im)
rgba = np.dstack([rgb, alpha * 255]).astype(np.uint8)
ys, xs = np.nonzero(fg)
# plate diamond: leftmost, rightmost, bottommost opaque pixels in lower half
low = ys > h * 0.45
L = (int(xs[low].min()), int(ys[low][xs[low].argmin()]))
R = (int(xs[low].max()), int(ys[low][xs[low].argmax()]))
B = (int(xs[ys.argmax()]), int(ys.max()))
Image.fromarray(rgba).save(out)
# emissive mask: bright saturated cyan/teal or warm yellow pixels
r, g, b = im[...,0], im[...,1], im[...,2]
cyan = (b > 225) & (g > 215) & (r < 170) & (sat > 60)
yellow = (r > 240) & (g > 170) & (b < 110)
m = ndimage.binary_opening(cyan | yellow, iterations=1) & fg
m = ndimage.gaussian_filter(m.astype(np.float32), 1.2)
Image.fromarray((np.clip(m * 1.4, 0, 1) * 255).astype(np.uint8)).save(emis)
print(json.dumps({"src": src, "size": [w, h], "bg": bg.round().tolist(), "left": L, "right": R, "bottom": B, "top": int(ys.min())}))
