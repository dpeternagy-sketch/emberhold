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

def full_fix(a, cx, cy, plate):
    """For pictures drawn from a turned view (one face too wide): re-width each half around the front
    corner so both faces show at 45 degrees, then shear the rest. Returns a square image (padded so
    nothing is cut) and the new cx, cy, plate."""
    xB, tl, tr = edges(a)
    sp = math.sqrt(max(tl * tr, 1e-6))                 # sin(pitch) of the picture's camera
    phiL = math.atan(tl / sp); phiR = math.atan(tr / sp)
    sL = math.cos(math.radians(45)) / math.cos(phiL)    # left half width factor
    sR = math.cos(math.radians(45)) / math.cos(phiR)
    h, w = a.shape[:2]
    left = Image.fromarray(a[:, :xB]); right = Image.fromarray(a[:, xB:])
    lw = max(1, int(round(left.width * sL))); rw = max(1, int(round(right.width * sR)))
    left = np.asarray(left.resize((lw, h), Image.LANCZOS)); right = np.asarray(right.resize((rw, h), Image.LANCZOS))
    W = max(w, lw + rw) + 40
    W += W % 2
    pady = int(h * 0.12)
    H = h + 2 * pady
    S = max(W, H)
    canvas = np.zeros((S, S, 4), a.dtype)
    ox = (S - (lw + rw)) // 2; oy = (S - h) // 2
    canvas[oy:oy + h, ox:ox + lw] = left; canvas[oy:oy + h, ox + lw:ox + lw + rw] = right
    ncx = ox + (lw - (xB - cx) * sL if cx < xB else lw + (cx - xB) * sR)
    sh, _ = shifts(canvas)
    out = apply(canvas, sh)
    ncy = oy + cy + sh[int(round(ncx))]
    return out, ncx, ncy, plate * (sL + sR) / 2, (math.degrees(phiL), math.degrees(phiR), sL, sR)

def corner_fit(a):
    """Fitted plate edges: returns xL, xR, virtual front corner xi, left/right slopes (rise per px)."""
    m = a[..., 3] > 128
    h, w = m.shape
    cols = np.arange(w); has = m.any(0)
    bot = np.where(has, h - 1 - np.argmax(m[::-1], 0), -1)
    xs = cols[has]; xL, xR = xs.min(), xs.max()
    xB = int(np.median(xs[bot[has] >= bot[has].max() - 2]))
    def fit(x0, x1):
        xx = np.arange(int(x0 + (x1 - x0) * 0.25), int(x0 + (x1 - x0) * 0.75))
        ok = bot[xx] > 0
        return np.polyfit(xx[ok], bot[xx][ok], 1)
    pl = fit(xL, xB); pr = fit(xB, xR)
    xi = (pr[1] - pl[1]) / (pl[0] - pr[0])
    return xL, xR, xi, pl, pr

def square_fix(a, cx, cy):
    """Square footprint: make the left and right halves of the plate equally wide around the fitted
    front corner, then shear both edges onto the ground angle pivoting on that corner."""
    h, w = a.shape[:2]
    xL, xR, xi, pl, pr = corner_fit(a)
    xi = int(round(xi))
    half = ((xi - xL) + (xR - xi)) / 2
    sL = half / (xi - xL); sR = half / (xR - xi)
    left = np.asarray(Image.fromarray(a[:, :xi]).resize((max(1, int(round(xi * sL))), h), Image.LANCZOS))
    right = np.asarray(Image.fromarray(a[:, xi:]).resize((max(1, int(round((w - xi) * sR))), h), Image.LANCZOS))
    W = left.shape[1] + right.shape[1]
    S = max(W, h)
    canvas = np.zeros((S, S, 4), a.dtype)
    ox = (S - W) // 2; oy = (S - h) // 2
    canvas[oy:oy + h, ox:ox + left.shape[1]] = left
    canvas[oy:oy + h, ox + left.shape[1]:ox + W] = right
    ncx = ox + (cx * sL if cx < xi else left.shape[1] + (cx - xi) * sR)
    ncy = oy + cy
    # shear around the fitted corner
    xL2, xR2, xi2, pl2, pr2 = corner_fit(canvas)
    x = np.arange(S)
    d = np.where(x < xi2, (xi2 - x) * (pl2[0] - GROUND), (x - xi2) * (-pr2[0] - GROUND))
    sh = np.round(d).astype(int)
    out = apply(canvas, sh)
    ncy += sh[int(round(ncx))]
    return out, ncx, ncy, (sL, sR)
