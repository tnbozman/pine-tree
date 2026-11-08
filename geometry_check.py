import math, json
import numpy as np, trimesh
from shapely.geometry import Polygon, box
from shapely.ops import unary_union
from shapely import affinity
H=1210.; PHI=math.radians(18); W=42.; OV=130.
t,c,s=math.tan(PHI),math.cos(PHI),math.sin(PHI)
a=W/c
xo=lambda y,k:(H-y)*t-k*a
L3=470.; Ha=H; ym=Ha-W/s; L1=470.
y2lo=L3-OV; y1lo=Ha-L1; y2hi=y1lo+OV
def board(k,ya,yb): return [(xo(yb,k),yb),(xo(ya,k),ya),(xo(ya,k)-a,ya),(xo(yb,k)-a,yb)]
P3=board(2,0,L3); P2=board(1,y2lo,y2hi)
P1=[(0,Ha),(xo(y1lo,0),y1lo),(xo(y1lo,0)-a,y1lo),(0,ym)]
rail=[(-xo(0,3),0),(xo(0,3),0),(xo(W,3),W),(-xo(W,3),W)]
# in-plane bolts: (name, cx, cy, dx, dy, length)
lap=[]
for y in (y2lo+30,L3-30): lap.append((xo(y,2),y,c,s))          # P3|P2 shared face
for y in (y1lo+30,y2hi-30): lap.append((xo(y,1),y,c,s))        # P2|P1 shared face
apex=[(0,Ha-90,1,0),(0,Ha-140,1,0)]
def stock_len(poly,ax):
    d=[p[0]*ax[0]+p[1]*ax[1] for p in poly]; return max(d)-min(d)
axR=(s,-c)
cut={"P3 (x2)":stock_len(P3,axR),"P2 (x2)":stock_len(P2,axR),"P1 (x2)":stock_len(P1,axR),
     "RAIL (x1)":2*xo(0,3), "FOOT (x2)":450.}
print({k:round(v,1) for k,v in cut.items()})
# ---- stock packing (42x42, 1.8 m, kerf 5, 10 trim)
items=[("P3",cut["P3 (x2)"])]*2+[("P2",cut["P2 (x2)"])]*2+[("P1",cut["P1 (x2)"])]*2+[("RAIL",cut["RAIL (x1)"]),("FOOT",450),("FOOT",450)]
items.sort(key=lambda x:-x[1]); bins=[]
for n,l in items:
    for b in bins:
        if sum(x[1]+5 for x in b)+l+5<=1800-10: b.append((n,l)); break
    else: bins.append([(n,l)])
print("bins",[[ (n,round(l)) for n,l in b] for b in bins])
# ---- star cap
yb=Ha-72; yt=Ha+8; NH=34.; CLR=0.4
slot=Polygon([(-(Ha-yb)*t-CLR/c-1,yb-1),(0,Ha+CLR/s),((Ha-yb)*t+CLR/c+1,yb-1)])
slot=Polygon([(-((Ha+CLR/s)-(yb-1))*t,yb-1),(0,Ha+CLR/s),(((Ha+CLR/s)-(yb-1))*t,yb-1)])
R=85.; ri=.4*R; yc=Ha+45
star=Polygon([((R if k%2==0 else ri)*math.cos(math.radians(90+36*k)),yc+(R if k%2==0 else ri)*math.sin(math.radians(90+36*k))) for k in range(10)])
neckrect=box(-NH,yb,NH,yt)
neck=neckrect.difference(slot)
plate_sw=star.difference(neckrect)                  # SolidWorks plate: no overlap with neck
plate_stl=star.difference(slot)
print("plate parts",plate_sw.geom_type, "neck valid",neck.is_valid)
capbolts=[(0,Ha-40,1,0),(0,Ha-62,1,0)]
# ---- STL
def prism(poly,h,zc=0):
    m=trimesh.creation.extrude_polygon(poly,h); m.apply_translation([0,0,zc-h/2]); return m
body=prism(neck,42).union(prism(plate_stl,12),engine='manifold')
def xcyl(r,x0,x1,y,sections=48):
    m=trimesh.creation.cylinder(radius=r,height=abs(x1-x0),sections=sections)
    m.apply_transform(trimesh.transformations.rotation_matrix(math.pi/2,[0,1,0])); m.apply_translation([(x0+x1)/2,y,0]); return m
for _,y,_,_ in [(0,Ha-40,0,0),(0,Ha-62,0,0)]:
    body=body.difference(xcyl(3.25,-60,60,y),engine='manifold')
    body=body.difference(xcyl(6.0,-NH-1,-NH+5.2,y,6),engine='manifold')   # hex nut pocket (left)
    body=body.difference(xcyl(6.7,NH-4,NH+1,y),engine='manifold')         # washer/head recess (right)
# print orientation: x centred, y min 0, z min 0 (flat on bed, 42 tall)
body.apply_translation([0,-body.bounds[0][1],-body.bounds[0][2]])
print("star_cap watertight",body.is_watertight,"bounds",np.round(body.extents,1),"vol cm3",round(body.volume/1000,1))
body.export('/home/claude/out/stl/star_cap.stl')
json.dump({"plate_sw":list(plate_sw.exterior.coords) if plate_sw.geom_type=="Polygon" else None,
           "neck":list(neck.exterior.coords),"Ha":Ha},open('/home/claude/scripts/cap.json','w'))
print("plate_sw pts",len(plate_sw.exterior.coords) if plate_sw.geom_type=="Polygon" else plate_sw.geom_type)
print("holes lap:",[(round(x,1),round(y,1)) for x,y,_,_ in lap]," apex y:",[round(Ha-90),round(Ha-140)])
print("apex wood widths mm:",round(2*(90*t),1),round(2*(140*t),1))
