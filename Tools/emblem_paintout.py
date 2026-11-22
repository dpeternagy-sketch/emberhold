# Paint out emblems: inside each box, white-ish blobs that don't touch the box edge are filled
# by diffusion from their surroundings (flat panel colour).
import sys, json, numpy as np
from PIL import Image
from scipy import ndimage
src, out, boxes = sys.argv[1], sys.argv[2], json.loads(sys.argv[3])
im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
for x0, y0, x1, y1 in boxes:
    sub = im[y0:y1, x0:x1]
    sat = sub.max(-1) - sub.min(-1)
    white = (sub.min(-1) > 135) & (sat < 90)
    lab, n = ndimage.label(white)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    m = white & ~np.isin(lab, list(edge))
    m = ndimage.binary_dilation(m, iterations=3)
    f = sub.copy()
    f[m] = 0
    for _ in range(400):
        p = np.pad(f, ((1, 1), (1, 1), (0, 0)), mode='edge')
        avg = (p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:]) / 4
        f[m] = avg[m]
    im[y0:y1, x0:x1] = f
Image.fromarray(im.clip(0, 255).astype(np.uint8)).save(out)
