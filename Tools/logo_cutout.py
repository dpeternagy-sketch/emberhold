import numpy as np
from PIL import Image
from scipy import ndimage
src='/mnt/user-data/uploads/Desktop/game/New/last/Mega Fort Chrome Logo-1.png'
im=np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
h,w,_=im.shape
v=im.max(-1)
a=np.clip((v-18)/120,0,1)**0.85
# solid parts (letters, flag, pole) stay fully opaque: bright or strongly blue-saturated with sharp detail
solid=ndimage.binary_fill_holes(ndimage.binary_opening(v>150,iterations=2))
lab,n=ndimage.label(solid); s=ndimage.sum(solid,lab,range(1,n+1)); solid=np.isin(lab,np.where(s>3000)[0]+1)
solid=ndimage.binary_fill_holes(ndimage.binary_closing(solid,iterations=4))
yy0,xx0=np.mgrid[0:h,0:w]
pole=((xx0>=537)&(xx0<=603)&(yy0>=60)&(yy0<=425))
pole|=(((xx0-570)/62.0)**2+((yy0-72)/50.0)**2<=1)&(yy0>=28)
pole|=((xx0>=521)&(xx0<=617)&(((yy0>=135)&(yy0<=200))|((yy0>=330)&(yy0<=395))))
zone=(xx0<606)&(yy0<415)
solid=np.where(zone,pole,solid)
solid[(xx0>=600)&(xx0<800)&(yy0<86)]=False
sa=ndimage.gaussian_filter(solid.astype(np.float32),1.2)
sat=(im.max(-1)-im.min(-1))/np.maximum(im.max(-1),1)
top=np.clip((0.42*h-np.mgrid[0:h,0:w][0])/(0.12*h),0,1)
a=a*(1-top*(1-np.clip(sat*1.5,0,1)))
a=np.maximum(a,sa)
# smooth fade toward the picture's borders so no edge is ever cut straight
yy,xx=np.mgrid[0:h,0:w]
dist=np.minimum(np.minimum(yy,h-1-yy),np.minimum(xx,w-1-xx)).astype(np.float32)
win=np.clip(dist/110,0,1); win=win*win*(3-2*win)
glowonly=1-np.clip(sa*1.0,0,1)
a=a*(win+(1-win)*(1-glowonly))
rgb=np.clip(im/np.maximum(a[...,None],1e-3),0,255)
rgb=np.where(a[...,None]>0.02,rgb,0)
out=np.dstack([rgb,a*255]).astype(np.uint8)
ys,xs=np.nonzero(a>0.01)
img=Image.fromarray(out).crop((max(0,xs.min()-4),max(0,ys.min()-4),min(w,xs.max()+5),min(h,ys.max()+5)))
s=1024/img.width; img=img.resize((1024,int(img.height*s)),Image.LANCZOS); img.save('logo_new.png')
A=np.asarray(img)[...,3]; print(img.size,'edge max alpha top/bottom/left/right',A[0].max(),A[-1].max(),A[:,0].max(),A[:,-1].max())
t=Image.open('/home/user/emberhold-git/Assets/Emberhold/Resources/UI/Backgrounds/title.png').convert('RGBA')
t=t.resize((1080,int(t.height*1080/t.width)))
lg=img.resize((900,int(img.height*900/1024)))
t.alpha_composite(lg,(90,150)); t.convert('RGB').crop((0,0,1080,1100)).save('logo_title_chk.jpg')
