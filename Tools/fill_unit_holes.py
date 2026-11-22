# Fills holes that background removal punched into a painted unit (white armour keyed out with the backdrop):
# closes small openings in the silhouette, fills enclosed gaps, colours them from the nearest solid pixels.
# usage: python3 Tools/fill_unit_holes.py in.png out.png 10
import sys, numpy as np
from PIL import Image
from scipy import ndimage
src, dst, r = sys.argv[1], sys.argv[2], int(sys.argv[3])
im = np.array(Image.open(src).convert('RGBA'))
a = im[..., 3]
solid = a >= 128
yy, xx = np.mgrid[-r:r+1, -r:r+1]
disk = xx*xx + yy*yy <= r*r
closed = ndimage.binary_closing(np.pad(solid, r+2), structure=disk)[r+2:-r-2, r+2:-r-2]
filled = ndimage.binary_fill_holes(closed | solid)
new = filled & ~solid
# colour for new pixels: nearest solid pixel's colour, mixed with the original colour where it still had some
_, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
near = im[iy, ix, :3].astype(float)
orig = im[..., :3].astype(float)
k = np.clip(a / 128.0, 0, 1)[..., None]
col = orig * k + near * (1 - k)
out = im.copy()
out[new, :3] = col[new].astype(np.uint8)
out[new, 3] = 255
Image.fromarray(out).save(dst)
print(src.split('/')[-1], 'filled px', int(new.sum()))
