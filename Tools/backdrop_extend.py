# Makes the mountain painting opaque all the way down: below each column's lowest painted pixel it adds
# plain dark painted-grass (the column's own grass tone, softly varied), so no jagged cut edge or
# far-away hazy terrain ever shows under the painting. The 3D hills in front hide most of the fill.
# usage: python3 Tools/backdrop_extend.py in.png out.png
import sys, numpy as np
from PIL import Image
from scipy import ndimage
src, dst = sys.argv[1], sys.argv[2]
im = np.array(Image.open(src).convert('RGBA')).astype(float)
H, W = im.shape[:2]
a = im[..., 3]
solid = a > 127
low = np.array([np.where(solid[:, x])[0].max() if solid[:, x].any() else -1 for x in range(W)])
cols = np.zeros((W, 3)); have = np.zeros(W)
for x in range(W):
    r = low[x]
    if r < 0: continue
    seg = im[max(0, r - 40):r + 1, x]
    g = seg[(seg[:, 1] > seg[:, 0] + 15) & (seg[:, 1] > seg[:, 2] + 15) & (seg[:, 3] > 200)]
    if len(g) >= 3: cols[x] = np.median(g[:, :3], axis=0); have[x] = 1
glob = np.median(cols[have > 0], axis=0)
cols[have == 0] = glob
w = ndimage.gaussian_filter1d(have, 25) + 1e-6
cols = np.stack([ndimage.gaussian_filter1d(cols[:, c] * have, 25) / w for c in range(3)], 1)
cols = cols * np.clip(ndimage.gaussian_filter1d(have, 25) / 0.5, 0, 1)[:, None] + glob * (1 - np.clip(ndimage.gaussian_filter1d(have, 25) / 0.5, 0, 1))[:, None]
rng = np.random.default_rng(7)
noise = ndimage.gaussian_filter(rng.normal(0, 1, (H, W)), (2.0, 3.0))
noise = 1 + 0.06 * noise / (noise.std() + 1e-6)
out = im.copy()
yy = np.arange(H)[:, None]
fill = yy > low[None, :]
fill[:, low < 0] = False          # empty columns (outside the painting) stay empty
# the pool: where a column ends in water, keep it open so the 3D river shows the pool feeding it
water = np.zeros(W, bool)
for x in range(W):
    r = low[x]
    if r < 0: continue
    seg = im[max(0, r - 12):r + 1, x]
    water[x] = np.mean((seg[:, 2] > seg[:, 0] + 40) & (seg[:, 3] > 200)) > 0.5
water = ndimage.binary_closing(water, iterations=8)
lab, _ = ndimage.label(water)
fx = int(0.524 * W)   # the pool under the main fall (Backdrop footUv.x)
near = [l for l in np.unique(lab[max(0, fx - 60):fx + 60]) if l > 0]
water = np.isin(lab, near)
water = ndimage.binary_dilation(water, iterations=6)
fill[:, water] = False
for c in range(3):
    ch = out[..., c]
    ch[fill] = np.clip((cols[:, c][None, :] * noise)[fill], 0, 255)
out[..., 3][fill] = 255
Image.fromarray(out.astype(np.uint8)).save(dst)
print('filled', int(fill.sum()), 'px; grass', glob)
