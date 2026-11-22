# Seamless wall segment: repeat the plain slab between the two end pillars along the 27.2 deg ground line,
# exactly one cell long (plate px), so consecutive cells butt into one continuous wall. Posts go only at ends/corners.
import sys, numpy as np
from PIL import Image
G=0.5141
src, dst, x0, x1, ov = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), 3
for suf in ('', '_emis'):
    try: im=Image.open(f'{src}{suf}.png')
    except FileNotFoundError: continue
    a=np.asarray(im.convert('RGBA') if suf=='' else im.convert('L'))
    cx,cy,pl=map(float,open(src+'.txt').read().split())
    out=np.zeros_like(a); per=x1-x0
    L0=int(round(cx-pl/2))-ov; L1=int(round(cx+pl/2))+ov
    for x in range(L0,L1+1):
        xs=x0+((x-x0)%per)
        dy=int(round(-G*(x-xs)))
        col=a[:,xs]
        if dy<0: out[:dy,x]=col[-dy:]
        elif dy>0: out[dy:,x]=col[:-dy]
        else: out[:,x]=col
    Image.fromarray(out).save(f'{dst}{suf}.png')
    if suf=='': open(dst+'.txt','w').write('%.1f %.1f %.1f\n'%(cx,cy,pl))
