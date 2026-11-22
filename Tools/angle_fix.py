"""Line a building picture's plate up with the game ground (27.2 degrees each side at pitch 31 / yaw 45)
by sliding each pixel column up or down (vertical shear, left and right halves separately).
Verticals stay vertical; only the slope of the plate and roof lines changes.
Usage: python3 angle_fix.py in.png out.png   (prints the measured and new angles)
"""
import sys, math, numpy as np
from PIL import Image

GROUND = math.tan(math.radians(27.2))

def edges(a):
    m = a[..., 3] > 128
    h, w = m.shape
    cols = np.arange(w); has = m.any(0)
    bot = np.where(has, h - 1 - np.argmax(m[::-1], 0), -1)
    xs = cols[has]; xL, xR = xs.min(), xs.max()
    xB = int(np.median(xs[bot[has] >= bot[has].max() - 2]))
    def slope(x0, x1):
        xx = np.arange(int(x0 + (x1 - x0) * 0.2), int(x0 + (x1 - x0) * 0.8))
        yy = bot[xx]; ok = yy > 0
        return np.polyfit(xx[ok], yy[ok], 1)[0]
    return xB, slope(xL, xB), -slope(xB, xR)   # left rise per px, right rise per px (both positive)

def shifts(a):
    """Per-column downward shift (pixels) that brings the plate edges to the ground angle."""
    xB, tl, tr = edges(a)
    w = a.shape[1]
    x = np.arange(w)
    d = np.where(x < xB, (xB - x) * (tl - GROUND), (x - xB) * (tr - GROUND))
    return np.round(d).astype(int), (math.degrees(math.atan(tl)), math.degrees(math.atan(tr)))

def apply(a, sh):
    h, w = a.shape[:2]
    pad = int(h * 0.15)
    big = np.zeros((h + 2 * pad,) + a.shape[1:], a.dtype); big[pad:pad + h] = a
    out = np.zeros_like(a)
    for x in range(w):
        s = sh[x]; src = big[:, x]; col = np.zeros_like(src)
        if s >= 0: col[s:] = src[:len(src) - s]
        else: col[:s] = src[-s:]
        out[:, x] = col[pad:pad + h]
    return out

def fix(a):
    sh, ang = shifts(a)
    return apply(a, sh), ang

def _old_fix(a):
    xB, tl, tr = edges(a)
    h, w = a.shape[:2]
    out = np.zeros_like(a)
    pad = int(h * 0.15)
    big = np.zeros((h + 2 * pad, w, 4), a.dtype); big[pad:pad + h] = a
    for x in range(w):
        d = (xB - x) * (tl - GROUND) if x < xB else (x - xB) * (tr - GROUND)
        s = int(round(d))   # +: move this column down
        src = big[:, x]
        col = np.zeros_like(src)
        if s >= 0: col[s:] = src[:len(src) - s]
        else: col[:s] = src[-s:]
        out[:, x] = col[pad:pad + h]
    return out, (math.degrees(math.atan(tl)), math.degrees(math.atan(tr)))

if __name__ == '__main__':
    a = np.asarray(Image.open(sys.argv[1]).convert('RGBA'))
    o, before = fix(a)
    Image.fromarray(o).save(sys.argv[2])
    _, l2, r2 = edges(o)
    print(sys.argv[1], 'before %.1f/%.1f' % before, 'after %.1f/%.1f' % (math.degrees(math.atan(l2)), math.degrees(math.atan(r2))))
