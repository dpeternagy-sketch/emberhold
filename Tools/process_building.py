# Usage (in a folder with sk1..3.png from skirt_remove and em1..3.png from sprite_cutout): python3 process_building.py <id> [outdir]
# square_fix to the 27.2 deg ground, resize to 1024, writes {id}_t1/t3/t5 .png, _emis.png, .txt
import sys, json, math, numpy as np
sys.path.insert(0, __import__('os').path.dirname(__file__))
from PIL import Image
import angle_fix as af
D=sys.argv[2] if len(sys.argv)>2 else '/home/user/emberhold-git/Assets/Emberhold/Resources/Sprites/Buildings/'
ID=sys.argv[1]
for i,t in ((1,1),(2,3),(3,5)):
    a=np.asarray(Image.open(f'sk{i}.png').convert('RGBA'))
    e=np.asarray(Image.open(f'em{i}.png').convert('L'))
    xL,xR,xi,pl,pr=af.corner_fit(a)
    print(i,'before L %.1f R %.1f'%(math.degrees(math.atan(-pl[0])),math.degrees(math.atan(pr[0]))), 'halves',xi-xL,xR-xi)
    # anchor: plate centre = midpoint of side corners
    m=a[...,3]>128; ys,xs=np.nonzero(m); low=ys>a.shape[0]*0.45
    Lx=xs[low].min(); Rx=xs[low].max()
    Ly=ys[low][xs[low]==Lx].mean(); Ry=ys[low][xs[low]==Rx].mean()
    cx=(Lx+Rx)/2; cy=(Ly+Ry)/2
    out,ncx,ncy,s=af.square_fix(a,cx,cy)
    ea=a.copy(); ea[...,0]=ea[...,1]=ea[...,2]=e
    eo,_,_,_=af.square_fix(ea,cx,cy)
    xL2,xR2,xi2,pl2,pr2=af.corner_fit(out)
    print(i,'after L %.1f R %.1f'%(math.degrees(math.atan(-pl2[0])),math.degrees(math.atan(pr2[0]))),'scales',s)
    S=out.shape[0]; k=1024/S
    plate=(xR2-xL2)
    # recompute cy from sides after fix
    m=out[...,3]>128; ys,xs=np.nonzero(m); low=ys>S*0.45
    Lx=xs[low].min(); Rx=xs[low].max(); Ly=ys[low][xs[low]==Lx].mean(); Ry=ys[low][xs[low]==Rx].mean()
    cx2=(Lx+Rx)/2; cy2=(Ly+Ry)/2
    Image.fromarray(out).resize((1024,1024),Image.LANCZOS).save(D+f'{ID}_t{t}.png')
    Image.fromarray(eo[...,0]).resize((1024,1024),Image.LANCZOS).save(D+f'{ID}_t{t}_emis.png')
    open(D+f'{ID}_t{t}.txt','w').write('%.1f %.1f %.1f\n'%(cx2*k,cy2*k,(Rx-Lx)*k))
    print(open(D+f'{ID}_t{t}.txt').read().strip(), 'top', ys.min()*k)
