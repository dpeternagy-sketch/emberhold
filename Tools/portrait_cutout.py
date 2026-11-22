import sys, numpy as np
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
src,out=sys.argv[1],sys.argv[2]
im=np.asarray(_load_rgb(src)).astype(np.float32)
border=np.concatenate([im[0],im[-1],im[:,0],im[:,-1]]);bg=np.median(border,axis=0)
d=np.sqrt(((im-bg)**2).sum(-1));sat=im.max(-1)-im.min(-1)
cand=((d<26)&(sat<30))|((im.min(-1)>205)&(sat<22))
lab,n=ndimage.label(cand)
sizes=ndimage.sum(cand,lab,range(1,n+1))
edge=set(np.unique(np.concatenate([lab[0],lab[-1],lab[:,0],lab[:,-1]])))-{0}
big={i+1 for i,s in enumerate(sizes) if s>600}
fg=~np.isin(lab,list(edge|big))
fg=ndimage.binary_opening(fg,iterations=1)
lab2,n2=ndimage.label(fg);s2=ndimage.sum(fg,lab2,range(1,n2+1))
keep=[i+1 for i,s in enumerate(s2) if s>0.01*s2.max()]
fg=np.isin(lab2,keep);fg=ndimage.binary_erosion(fg,iterations=1)
alpha=np.clip(ndimage.distance_transform_edt(fg)/2.0,0,1)
a3=np.maximum(alpha[...,None],1e-3)
rgb=np.where(alpha[...,None]<1,np.clip((im-bg*(1-a3))/a3,0,255),im)
Image.fromarray(np.dstack([rgb,alpha*255]).astype(np.uint8)).save(out)
