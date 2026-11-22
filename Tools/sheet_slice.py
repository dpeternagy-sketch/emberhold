# Slice a 3x3 sprite sheet (columns: wall segment, post, gate; rows: level 1..3) into 9 images.
# Usage: python3 -I sheet_slice.py sheet.png outdir
# Each cell is cropped from an equal 3x3 grid, then trimmed to its content with a margin, and
# re-padded onto a square canvas of the sheet's background colour so sprite_cutout.py can run on it.
import sys, os, numpy as np
from PIL import Image
src, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
im = Image.open(src).convert('RGB')
a = np.asarray(im).astype(np.float32)
h, w, _ = a.shape
border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
bg = np.median(border, axis=0)
names = ['segment', 'post', 'gate']
for r in range(3):
    for c in range(3):
        x0, x1, y0, y1 = c * w // 3, (c + 1) * w // 3, r * h // 3, (r + 1) * h // 3
        cell = a[y0:y1, x0:x1]
        d = np.sqrt(((cell - bg) ** 2).sum(-1))
        ys, xs = np.nonzero(d > 30)
        if len(xs) == 0: continue
        m = 12
        bx0, bx1 = max(xs.min() - m, 0), min(xs.max() + m, cell.shape[1])
        by0, by1 = max(ys.min() - m, 0), min(ys.max() + m, cell.shape[0])
        crop = Image.fromarray(cell[by0:by1, bx0:bx1].astype(np.uint8))
        s = max(crop.size) + 40
        canvas = Image.new('RGB', (s, s), tuple(int(v) for v in bg))
        canvas.paste(crop, ((s - crop.size[0]) // 2, (s - crop.size[1]) // 2))
        canvas.save(os.path.join(outdir, '%s_%d.png' % (names[c], r + 1)))
        print(names[c], r + 1, crop.size)
