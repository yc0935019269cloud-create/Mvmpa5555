"""Own-source clinic assets. Run Blender 5.2 --background --python Blender/build_clinic.py.
Coordinates in all modelling helpers are metres, Unity (X right,Y up,Z forward).
FBX retains named pivots for Unity-driven physical interactions; .blend remains editable.

The refractor head follows the published parts layout of a manual phoropter (Reichert
Phoroptor user's guide 11625-101, "Parts Identification"): two lens batteries hung from a
centre bar, weak sphere dials on the outer sides, strong sphere ring around the auxiliary
lens scale, concentric cylinder power/axis knobs over a protractor, an axis reference scale
around each main aperture, a turret carrying the Jackson cross cylinder and rotary prism,
P.D. and levelling knobs, spirit level, forehead rest, face shields and corneal aligning
device. Geometry is modelled here from scratch; no third-party mesh is used.
"""
import bpy, bmesh, math, os, random, json
from mathutils import Vector
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'UnityProject','Assets','Models')
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for block in (bpy.data.meshes,bpy.data.materials,bpy.data.curves):
    for item in list(block):block.remove(item)
random.seed(731)
def v(p): return Vector((p[0],p[2],p[1]))
def mat(name,c,metal=0,rough=.5):
    m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    return m
# Colours are display (sRGB) values; Unity converts them for linear lighting.
M={
 'WarmPlaster':mat('WarmPlaster',(.80,.78,.71),0,.85), 'Sage':mat('Sage',(.36,.47,.43),0,.8),
 'Oak':mat('Oak',(.47,.32,.20),0,.6), 'OakLight':mat('OakLight',(.64,.48,.31),0,.55),
 'Porcelain':mat('Porcelain',(.89,.89,.85),0,.3), 'Graphite':mat('Graphite',(.09,.10,.11),.1,.45),
 'Rubber':mat('Rubber',(.05,.055,.06),0,.8), 'Chrome':mat('Chrome',(.78,.80,.82),.95,.12),
 'Brass':mat('Brass',(.74,.56,.30),.8,.3), 'Teal':mat('Teal',(.20,.43,.45),0,.55),
 'Lens':mat('Lens',(.18,.30,.33),.25,.14), 'White':mat('White',(.97,.96,.92),0,.4),
 'Black':mat('Black',(.015,.016,.018),0,.5), 'Red':mat('Red',(.80,.10,.08),0,.4),
 'Skin':mat('Skin',(.87,.72,.64),0,.78), 'SkinShade':mat('SkinShade',(.80,.58,.48),0,.7),
 'Lip':mat('Lip',(.86,.65,.60),0,.6), 'Hair':mat('Hair',(.085,.065,.052),0,.62),
 'Brow':mat('Brow',(.12,.09,.07),0,.8),
 'Sclera':mat('Sclera',(.95,.93,.90),0,.08), 'Iris':mat('Iris',(.20,.13,.085),0,.08),
 'Pupil':mat('Pupil',(.006,.006,.008),0,.05), 'Shirt':mat('Shirt',(.24,.37,.47),0,.85),
 'ShirtSeam':mat('ShirtSeam',(.30,.45,.55),0,.85), 'Trousers':mat('Trousers',(.19,.20,.23),0,.8),
 'Shoe':mat('Shoe',(.24,.15,.10),0,.4), 'Leaf':mat('Leaf',(.20,.38,.20),0,.6),
 'Window':mat('Window',(.70,.83,.86),0,.1), 'Cuff':mat('Cuff',(.88,.92,.94),0,.75),
 'Glove':mat('Glove',(.52,.66,.88),0,.35), 'Paper':mat('Paper',(.95,.93,.86),0,.9),
 'CoatWhite':mat('CoatWhite',(.93,.94,.93),0,.8),
 # refractor finishes: satin black die-cast body, brushed aluminium controls, white scales
 'PhoroBody':mat('PhoroBody',(.065,.068,.075),.25,.36), 'Alu':mat('Alu',(.80,.81,.82),.9,.32),
 'AluDark':mat('AluDark',(.52,.53,.55),.9,.36), 'Scale':mat('Scale',(.93,.93,.90),0,.35),
 'Glass':mat('Glass',(.80,.88,.92),0,.03), 'ShieldPlastic':mat('ShieldPlastic',(.86,.88,.88),0,.25),
 'Upholstery':mat('Upholstery',(.17,.33,.36),0,.7), 'StandWhite':mat('StandWhite',(.74,.75,.74),.15,.35),
 'RulerWhite':mat('RulerWhite',(.96,.96,.93),0,.25), 'Bottle':mat('Bottle',(.86,.92,.95),0,.15),
}
group=None
def finish(o,name,ma,parent=None,smooth=True):
    o.name=name; o.data.materials.clear();o.data.materials.append(M[ma]);
    for p in o.data.polygons:p.use_smooth=smooth
    if parent is None:parent=group
    if parent:
        bpy.context.view_layer.update()
        w=o.matrix_world.copy();o.parent=parent;o.matrix_world=w
    return o
def pivot(name,p,parent=None):
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=v(p)
    if parent is None:parent=group
    if parent:
        bpy.context.view_layer.update()
        w=o.matrix_world.copy();o.parent=parent;o.matrix_world=w
    return o
def root(name,p):
    global group
    group=None;g=pivot(name,p);group=g;return g
def apply_mod(o,mod):
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
def box(name,p,size,ma,bevel=.02,parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p));o=bpy.context.object;o.scale=(size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft machined edges','BEVEL');mod.width=bevel;mod.segments=3;apply_mod(o,mod)
    return finish(o,name,ma,parent)
def ell(name,p,size,ma,parent=None,seg=24,rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,radius=1,location=v(p));o=bpy.context.object
    o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,name,ma,parent)
def cyl(name,p,r,depth,ma,axis='Y',parent=None,vertices=40,bevel=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=v(p));o=bpy.context.object
    direction={'X':Vector((1,0,0)),'Y':Vector((0,0,1)),'Z':Vector((0,1,0))}[axis]
    o.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    if bevel:
        mod=o.modifiers.new('Lathed rim','BEVEL');mod.width=min(.008,r*.12,depth*.3);mod.segments=2;apply_mod(o,mod)
    return finish(o,name,ma,parent)
def ring(name,p,r,thick,ma,parent=None,axis='Z'):
    rot={'Z':(math.pi/2,0,0),'Y':(0,0,0),'X':(0,math.pi/2,0)}[axis]
    bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=thick,major_segments=48,minor_segments=10,location=v(p),rotation=rot)
    return finish(bpy.context.object,name,ma,parent)
def link(name,a,b,r,ma,parent=None,vertices=16):
    d=v(b)-v(a);bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(v(a)+v(b))/2)
    o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return finish(o,name,ma,parent)
def curve(name,points,r,ma,parent=None):
    d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.bevel_depth=r;d.bevel_resolution=3
    s=d.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
    for bp,p in zip(s.bezier_points,points):bp.co=v(p);bp.handle_left_type=bp.handle_right_type='AUTO'
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.select_set(False);return finish(o,name,ma,parent)

# ---------- procedural surface helpers ----------
def smoothstep(a,b,x):
    t=max(0.0,min(1.0,(x-a)/(b-a)));return t*t*(3-2*t)
def new_mesh(name,verts,faces,ma,parent=None,outward=None,closed=False,smooth=True):
    """verts in Unity metres. outward: point the normals away from (Vector, Unity) or along a direction."""
    me=bpy.data.meshes.new(name);me.from_pydata([v(p) for p in verts],[],faces);me.update()
    o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o)
    bm=bmesh.new();bm.from_mesh(me)
    if closed:bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    elif outward is not None:
        kind,val=outward;val=v(val)
        for f in bm.faces:
            c=f.calc_center_median();n=f.normal
            d=(c-val) if kind=='from' else val
            if n.dot(d)<0:f.normal_flip()
    bm.to_mesh(me);bm.free();me.update()
    return finish(o,name,ma,parent,smooth)
def catmull(points,sub):
    pts=[Vector(p) for p in points];out=[]
    for i in range(len(pts)-1):
        p0=pts[max(i-1,0)];p1=pts[i];p2=pts[i+1];p3=pts[min(i+2,len(pts)-1)]
        for k in range(sub):
            t=k/sub;t2=t*t;t3=t2*t
            out.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3))
    out.append(pts[-1]);return out
def lerp_list(vals,n_in,sub):
    out=[]
    for i in range(n_in-1):
        a=vals[i];b=vals[i+1]
        for k in range(sub):
            t=k/sub;t=t*t*(3-2*t)
            out.append(tuple(a[j]+(b[j]-a[j])*t for j in range(2)))
    out.append(vals[-1]);return out
def tube(name,points,radii,ma,parent=None,seg=24,sub=6,caps=(True,True),up=(0,1,0),cap_rings=4):
    """Lofted organic tube along a smooth path; radii are (rx,ry) pairs (rx along the side vector)."""
    radii=[(r,r) if not isinstance(r,(tuple,list)) else tuple(r) for r in radii]
    P=catmull(points,sub);R=lerp_list(radii,len(points),sub)
    T=[]
    for i in range(len(P)):
        a=P[max(i-1,0)];b=P[min(i+1,len(P)-1)];T.append((b-a).normalized())
    upv=Vector(up);n=T[0].cross(upv)
    if n.length<1e-6:n=T[0].cross(Vector((1,0,0)))
    n.normalize();frames=[]
    for i in range(len(P)):
        t=T[i];n=(n-t*n.dot(t));n.normalize();b=t.cross(n);frames.append((n,b))
    verts=[];faces=[];rings=[]
    def add_ring(center,n,b,rx,ry):
        idx=len(verts)
        for k in range(seg):
            a=k*math.tau/seg;verts.append(tuple(center+n*(math.cos(a)*rx)+b*(math.sin(a)*ry)))
        rings.append(idx)
    order=[]
    if caps[0]:
        for c in range(cap_rings,0,-1):
            ang=c/(cap_rings+.6)*math.pi/2;s=math.cos(ang);n,b=frames[0];rx,ry=R[0]
            add_ring(P[0]-T[0]*(math.sin(ang)*min(rx,ry)*.9),n,b,rx*s,ry*s)
    for i in range(len(P)):
        n,b=frames[i];add_ring(P[i],n,b,*R[i])
    if caps[1]:
        for c in range(1,cap_rings+1):
            ang=c/(cap_rings+.6)*math.pi/2;s=math.cos(ang);n,b=frames[-1];rx,ry=R[-1]
            add_ring(P[-1]+T[-1]*(math.sin(ang)*min(rx,ry)*.9),n,b,rx*s,ry*s)
    for r in range(len(rings)-1):
        a=rings[r];b2=rings[r+1]
        for k in range(seg):faces.append((a+k,a+(k+1)%seg,b2+(k+1)%seg,b2+k))
    if caps[0]:
        c=len(verts);verts.append(tuple(P[0]-T[0]*(min(*R[0])*.9)));faces+=[(c,rings[0]+(k+1)%seg,rings[0]+k) for k in range(seg)]
    if caps[1]:
        c=len(verts);verts.append(tuple(P[-1]+T[-1]*(min(*R[-1])*.9)));last=rings[-1];faces+=[(c,last+k,last+(k+1)%seg) for k in range(seg)]
    return new_mesh(name,verts,faces,ma,parent,closed=all(caps),outward=None if all(caps) else ('from',tuple(P[len(P)//2])))
def chaikin(pts,it=3):
    for _ in range(it):
        out=[]
        for i in range(len(pts)):
            a=pts[i];b=pts[(i+1)%len(pts)]
            out+=[(a[0]*.75+b[0]*.25,a[1]*.75+b[1]*.25),(a[0]*.25+b[0]*.75,a[1]*.25+b[1]*.75)]
        pts=out
    return pts
def circle_pts(cx,cy,r,n=64,teeth=0,depth=0.0):
    out=[]
    for k in range(n):
        a=k*math.tau/n;rr=r-(depth if teeth and (k%2) else 0)
        out.append((cx+math.cos(a)*rr,cy+math.sin(a)*rr))
    return out
def slab(name,outline,z0,z1,ma,parent=None,bevel=.0006,seg=2,holes=(),split=True):
    """Extrude a front-view outline (Unity X,Y metres) from z0 to z1; optional round through-holes."""
    bm=bmesh.new();vb=[bm.verts.new(v((x,y,z0))) for x,y in outline];f=bm.faces.new(vb)
    res=bmesh.ops.extrude_face_region(bm,geom=[f]);vs=[e for e in res['geom'] if isinstance(e,bmesh.types.BMVert)]
    bmesh.ops.translate(bm,verts=vs,vec=v((0,0,z1-z0)));bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    me=bpy.data.meshes.new(name);bm.to_mesh(me);bm.free();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o)
    for hx,hy,hr in holes:
        bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=hr,depth=abs(z1-z0)+.02,location=v((hx,hy,(z0+z1)/2)),rotation=(math.pi/2,0,0))
        cutter=bpy.context.object;mod=o.modifiers.new('Bore','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter;apply_mod(o,mod)
        bpy.data.objects.remove(cutter,do_unlink=True)
    if bevel:
        mod=o.modifiers.new('Edge','BEVEL');mod.width=bevel;mod.segments=seg;mod.limit_method='ANGLE';mod.angle_limit=math.radians(35);apply_mod(o,mod)
    if split:
        mod=o.modifiers.new('Crisp','EDGE_SPLIT');mod.split_angle=math.radians(40);apply_mod(o,mod)
    return finish(o,name,ma,parent)
def disc(name,c,r,z0,z1,ma,parent=None,knurl=0,inner=0,bevel=.0005,n=None):
    """Front-facing turned part: plain or knurled cylinder, or annulus when inner>0 (axis Z)."""
    n=n or (knurl*2 if knurl else 64)
    return slab(name,circle_pts(c[0],c[1],r,n,knurl,.0007 if knurl else 0),z0,z1,ma,parent,bevel,2,((c[0],c[1],inner),) if inner else ())
def flat_marks(name,quads,z,ma,parent=None):
    """Printed/engraved marks: single-sided quads facing the examiner (-Z)."""
    verts=[];faces=[]
    for q in quads:
        i=len(verts);verts+=[(x,y,z) for x,y in q];faces.append((i,i+1,i+2,i+3))
    return new_mesh(name,verts,faces,ma,parent,outward=('dir',(0,0,-1)),smooth=False)
def tick_quads(cx,cy,r0,lengths,angles,width):
    out=[]
    for a,L in zip(angles,lengths):
        d=(math.cos(a),math.sin(a));n=(-d[1],d[0]);w=width/2
        p0=(cx+d[0]*r0,cy+d[1]*r0);p1=(cx+d[0]*(r0+L),cy+d[1]*(r0+L))
        out.append([(p0[0]-n[0]*w,p0[1]-n[1]*w),(p1[0]-n[0]*w,p1[1]-n[1]*w),(p1[0]+n[0]*w,p1[1]+n[1]*w),(p0[0]+n[0]*w,p0[1]+n[1]*w)])
    return out
def tri_quad(cx,cy,ang,r0,r1,w):
    d=(math.cos(ang),math.sin(ang));n=(-d[1],d[0])
    tip=(cx+d[0]*r1,cy+d[1]*r1);b=(cx+d[0]*r0,cy+d[1]*r0)
    return [(b[0]-n[0]*w,b[1]-n[1]*w),tip,tip,(b[0]+n[0]*w,b[1]+n[1]*w)]

# ================= Architecture =================
group=root('Architecture',(0,0,0))
box('Foundation',(0,-.08,1.7),(7.8,.16,8.6),'Oak',.01)
for i in range(20):
    for j in range(7):
        x=-3.7+i*.39;z=-2.38+j*1.25+(i%2)*.3
        box('OakBoard_%02d_%02d'%(i,j),(x,.005,z),(.38,.012,1.23),'OakLight' if (i+j)%4 else 'Oak',.001)
box('BackWall',(0,1.6,6),(7.8,3.2,.16),'Sage')
box('FrontWall',(0,1.6,-2.5),(7.8,3.2,.16),'WarmPlaster')
box('LeftWall',(-3.9,1.6,1.7),(.16,3.2,8.6),'WarmPlaster')
box('RightWall',(3.9,1.6,1.7),(.16,3.2,8.6),'WarmPlaster')
box('Ceiling',(0,3.2,1.7),(7.8,.10,8.6),'Porcelain')
for x in [-3.8,3.8]:box('Skirting',(x,.08,1.7),(.04,.16,8.4),'Oak')
for z in [-2.4,5.9]:box('SkirtingEnd',(0,.08,z),(7.6,.16,.04),'Oak')
for x in [-2.2,1.7]:
    box('CeilingPanel',(x,3.12,1.8),(1.1,.035,2.2),'White')
    box('PanelFrame',(x,3.14,1.8),(1.18,.03,2.28),'Chrome')
for i in range(27):box('AcousticSlat',(-3.6+i*.27,1.48,5.87),(.045,2.83,.06),'OakLight',.005)
box('WindowGlass',(-3.79,1.95,.6),(.035,1.6,3.0),'Window')
for yy in [1.12,2.78]:box('WindowHorizontal',(-3.75,yy,.6),(.09,.06,3.15),'Porcelain')
for zz in [-.93,2.13]:box('WindowUpright',(-3.75,1.95,zz),(.09,1.7,.06),'Porcelain')
for i in range(18):box('Blind',(-3.70,1.17+i*.088,.6),(.10,.028,2.98),'White',.004)
box('WindowSill',(-3.63,1.1,.6),(.34,.06,3.28),'OakLight')
box('DoorLeaf',(2.65,1.15,-2.36),(1.2,2.3,.065),'Sage')
box('DoorInset',(2.65,1.66,-2.31),(.6,.66,.02),'Window')
box('DoorFrameTop',(2.65,2.33,-2.3),(1.36,.075,.13),'Oak')
for x in [2.0,3.3]:box('DoorFrame',(x,1.15,-2.3),(.075,2.3,.13),'Oak')
link('DoorHandle',(2.2,1.04,-2.21),(2.43,1.04,-2.21),.016,'Chrome')
box('Cabinet',(-2.85,.45,4.2),(1.55,.9,2.55),'Porcelain',.045)
box('Countertop',(-2.85,.94,4.2),(1.62,.07,2.62),'OakLight',.025)
for z in [3.4,4.2,5.0]:
    for y in [.19,.49,.77]:
        box('Drawer',(-2.045,y,z),(.025,.25,.72),'Porcelain',.01)
        link('DrawerPull',(-2.015,y,z-.15),(-2.015,y,z+.15),.012,'Brass')
# instrument tray for the hygiene and measuring tools
box('ToolTray',(-2.58,.982,3.95),(.34,.012,.62),'Graphite',.006)
box('DeskTop',(2.7,.85,1.7),(1.6,.07,2.0),'OakLight')
for x in [2.08,3.32]:
    for z in [.87,2.53]:box('DeskLeg',(x,.42,z),(.06,.84,.06),'Graphite')
cyl('StoolSeat',(.8,.58,2.8),.23,.085,'Teal');cyl('StoolStem',(.8,.31,2.8),.033,.5,'Chrome')
for a in range(5):
    an=a*math.tau/5;end=(.8+math.cos(an)*.3,.1,2.8+math.sin(an)*.3)
    link('StoolBase',(.8,.12,2.8),end,.018,'Chrome');ell('StoolCaster',end,(.042,.05,.042),'Rubber')
cyl('Pot',(3.2,.24,5.15),.27,.46,'Porcelain')
for i in range(18):
    a=i*2.4;h=.7+random.random()*.7;e=(3.2+math.cos(a)*.32,h,5.15+math.sin(a)*.32)
    link('PlantStem',(3.2,.42,5.15),e,.008,'Leaf')
    leaf=ell('PlantLeaf',e,(.085,.25,.025),'Leaf');leaf.rotation_euler=(random.random(),random.random(),a)
box('ChartFrame',(0,1.5,-1.53),(1.18,.92,.07),'Graphite')
box('ChartSurface',(0,1.5,-1.482),(1.08,.82,.012),'White',0)
box('ChartStand',(0,.49,-1.56),(.05,.98,.05),'Chrome')
box('ChartFoot',(0,.05,-1.56),(.65,.05,.38),'Graphite')
box('Projector',(-2.75,2.83,-.9),(.4,.13,.3),'Porcelain');cyl('ProjectorLens',(-2.75,2.83,-1.06),.045,.03,'Lens','Z')
box('MonitorBody',(2.5,1.22,1.9),(.78,.48,.055),'Graphite')
box('MonitorScreen',(2.5,1.22,1.86),(.70,.4,.012),'White',0)
box('MonitorStem',(2.5,1.0,1.92),(.05,.24,.05),'Chrome')
box('MonitorFoot',(2.5,.9,1.92),(.35,.025,.2),'Graphite')

# ================= Exam chair =================
def rrect(cx,cy,w,h,r,n=6):
    pts=[]
    for (qx,qy,a0) in [(cx+w/2-r,cy+h/2-r,0),(cx-w/2+r,cy+h/2-r,90),(cx-w/2+r,cy-h/2+r,180),(cx+w/2-r,cy-h/2+r,270)]:
        for k in range(n+1):
            a=math.radians(a0+90*k/n);pts.append((qx+math.cos(a)*r,qy+math.sin(a)*r))
    return pts
group=root('ExamChair',(0,0,4.68))
cyl('ChairFoot',(0,.045,4.68),.30,.07,'Graphite',vertices=64)
cyl('ChairHydraulics',(0,.30,4.68),.07,.46,'Chrome');cyl('ChairBellows',(0,.20,4.68),.085,.22,'Rubber')
box('ChairSeatPan',(0,.55,4.66),(.56,.04,.58),'Graphite',.015)
tube('ChairSeat',[(-.25,.61,4.66),(0,.615,4.66),(.25,.61,4.66)],[(.075,.29),(.08,.30),(.075,.29)],'Upholstery',seg=32,sub=4,up=(0,0,1))
tube('ChairBack',[(0,.66,5.00),(0,.95,5.02),(0,1.22,5.04),(0,1.36,5.03)],[(.27,.065),(.29,.07),(.28,.065),(.20,.06)],'Upholstery',seg=32,sub=4,up=(0,0,1))
for x in [-.36,.36]:
    link('ArmSupport',(x,.56,4.78),(x,.88,4.74),.022,'Chrome')
    tube('Armrest',[(x,.92,4.92),(x,.925,4.70),(x,.915,4.45)],[(.045,.03),(.05,.032),(.045,.03)],'Upholstery',seg=20)
link('FootrestStrut',(0,.40,4.45),(0,.22,4.12),.02,'Chrome')
box('Footrest',(0,.20,4.06),(.50,.04,.29),'Graphite',.012)
for i in range(6):box('FootrestRib',(0,.222,3.95+i*.045),(.46,.006,.012),'Rubber',.002)

# ================= Patient =================
# Seated adult facing -Z. Head is one continuous sculpted surface; features are displacements.
group=root('Patient',(0,0,4.55))
EYE_X=.032;EYE_Y=1.445;PUPIL_Z=4.475;EYE_CZ=4.4865;EYE_R=.0115
HC=Vector((0,1.452,4.565))   # head centre (W = forward toward -Z)
def G(X,Y,x0,y0,sx,sy,a):return a*math.exp(-((X-x0)/sx)**2-((Y-y0)/sy)**2)
def nose_profile(Y):
    # forward projection along the midline: root, dorsum, tip, columella
    pts=[(.012,0),(.003,.0042),(-.012,.0088),(-.027,.0150),(-.037,.0190),(-.044,.0158),(-.051,.0085),(-.058,.0025),(-.064,.0)]
    if Y>=pts[0][0] or Y<=pts[-1][0]:return 0.0
    for (y0,a0),(y1,a1) in zip(pts,pts[1:]):
        if y1<=Y<=y0:t=(Y-y0)/(y1-y0);t=t*t*(3-2*t);return a0+(a1-a0)*t
    return 0.0
def head_point(theta,phi):
    dy=math.cos(theta);h=math.sin(theta);c=math.cos(phi);s=math.sin(phi)
    front=s>0;n=2.45 if front else 2.15
    f=(abs(c)**n+abs(s)**n)**(-1/n)   # smooth superellipse radius (no midline crease)
    cx=c*f;cw=s*f
    Y=dy*(.122 if dy>0 else .109)
    hp=h**(.7 if dy>0 else .42) if front else h
    X=.0735*h*cx;W=hp*cw*(.097 if front else .104)
    if W<0 and Y>0:W*=1+.08*(Y/.122)
    t=smoothstep(-.015,-.112,Y)
    X*=1-.27*t-.10*t*t
    X+=math.copysign(.004,X)*smoothstep(0,.03,abs(X))*math.exp(-((Y+.075)/.02)**2)*smoothstep(-.02,.03,W)   # mandible angle
    W*=(1-.48*t) if W<0 else (1-.08*t)
    # temples and cheek planes
    X-=math.copysign(.0045,X)*smoothstep(0,.035,abs(X))*math.exp(-((Y-.035)/.03)**2)*smoothstep(0,.05,W)
    X+=math.copysign(.003,X)*smoothstep(0,.035,abs(X))*math.exp(-((Y+.035)/.025)**2)*smoothstep(0,.06,W)
    wf=smoothstep(.03,.072,W)
    if wf>0:
        d=0.0
        for sx in (-1,1):
            d+=G(X,Y,sx*EYE_X,-.006,.0165,.011,-.0052)+G(X,Y,sx*EYE_X,-.003,.028,.022,-.0014)
            d+=G(X,Y,sx*.030,.0145,.028,.0075,.0030)            # soft brow ridge
            d+=G(X,Y,sx*.045,-.028,.022,.018,.0036)             # cheekbone
            d+=G(X,Y,sx*.036,-.050,.017,.015,.0030)             # full cheeks (relaxed smile)
            d+=G(X,Y,sx*.0155,-.047,.0068,.0058,.0072)          # alar wings
            d+=G(X,Y,sx*.023,-.0665,.006,.004,.0011)            # lifted mouth corners
        d+=.86*nose_profile(Y)*math.exp(-(X/(.0075+.004*smoothstep(0,-.04,Y)))**2)
        d+=G(X,Y,0,-.0625,.021,.0062,.0048)+G(X,Y,0,-.0755,.018,.0058,.0046)   # lips
        d+=G(X,Y,0,-.0688,.024,.0015,-.0024)+G(X,Y,0,-.057,.0035,.0055,-.0006)  # stomion, philtrum
        d+=G(X,Y,0,-.092,.022,.016,.017)+G(X,Y,0,-.083,.016,.005,-.0024)        # chin, mentolabial sulcus
        W+=d*wf
    return Vector((HC.x+X,HC.y+Y,HC.z-W))
head=pivot('HeadPivot',(0,1.31,4.56),group)
RINGS,SEGS=110,150
hverts=[tuple(HC+Vector((0,.122,0)))];grid={}
for i in range(1,RINGS):
    th=i*math.pi/RINGS
    for j in range(SEGS):
        grid[(i,j)]=len(hverts);hverts.append(tuple(head_point(th,j*math.tau/SEGS)))
bottom=len(hverts);hverts.append(tuple(head_point(math.pi*.999,math.pi/2)))
hfaces=[]
for j in range(SEGS):hfaces.append((0,grid[(1,(j+1)%SEGS)],grid[(1,j)]))
for i in range(1,RINGS-1):
    for j in range(SEGS):hfaces.append((grid[(i,j)],grid[(i,(j+1)%SEGS)],grid[(i+1,(j+1)%SEGS)],grid[(i+1,j)]))
for j in range(SEGS):hfaces.append((bottom,grid[(RINGS-1,j)],grid[(RINGS-1,(j+1)%SEGS)]))
headobj=new_mesh('Head',hverts,hfaces,'Skin',head,closed=True)
# vertex normals in Unity space for overlay patches
headobj.data.update()
hnorm=[Vector((vv.normal.x,vv.normal.z,vv.normal.y)) for vv in headobj.data.vertices]
def local(p):p=Vector(p);return p.x-HC.x,p.y-HC.y,HC.z-p.z
def patch(name,ma,region,thickness,parent,ramp=.012,sink=.0015):
    """Overlay shell (hair, brows, lips). region returns a signed margin in metres (inside > 0).
    Outside the region the shell sinks below the skin, so the visible border is the smooth
    intersection curve instead of a stair-stepped face boundary."""
    keep={};verts=[]
    for idx,p in enumerate(hverts):
        X,Y,W=local(p);m=region(X,Y,W)
        if m>-.006:
            off=thickness(X,Y,W)*smoothstep(0,ramp,m) if m>0 else -sink*smoothstep(0,.004,-m)
            keep[idx]=len(verts);verts.append(tuple(Vector(p)+hnorm[idx]*off))
    faces=[tuple(keep[k] for k in f) for f in hfaces if all(k in keep for k in f)]
    return new_mesh(name,verts,faces,ma,parent,outward=('from',tuple(HC)))
def hairline(X,Y,W):
    ax=abs(X)
    if W>0:edge=.062-.03*(ax/.07)**2-.012*smoothstep(.02,.06,W)*(ax>.045)
    else:edge=.012-.09*smoothstep(0,-.06,W)
    edge=min(edge,.064)
    if W>-.01 and ax>.058:edge=max(edge,.018)   # above the ears
    return Y-edge
patch('HairCap','Hair',hairline,lambda X,Y,W:.0065+.0075*smoothstep(.0,.09,Y)+.004*smoothstep(0,-.08,W),head)
# soft fringe: strands laid on the forehead, swept to the patient's left
def surf(X,Y,off):
    best=None
    for idx,p in enumerate(hverts):
        lx,ly,lw=local(p)
        if lw<.02:continue
        d=(lx-X)**2+(ly-Y)**2
        if best is None or d<best[0]:best=(d,idx)
    return tuple(Vector(hverts[best[1]])+hnorm[best[1]]*off)
for i in range(11):
    x0=-.058+i*.0105;th=.0042+.0007*math.sin(i*1.7)
    pts=[surf(x0,.074,.009),surf(x0+.012,.063,.0058),surf(x0+.021,.054,.0034),surf(x0+.027,.0485+.003*math.sin(i),.0017)]
    curve('Fringe',pts,th,'Hair',head)
for s in (-1,1):
    def brow(X,Y,W,s=s):
        if W<.04:return -1
        u=(s*X-.012)/.044;uc=max(0,min(1,u))
        yc=.0195+.0055*math.sin(uc*math.pi*.9)-.004*uc;half=.0027*(1-.55*uc)+.0007
        return min(half-abs(Y-yc),.25*.044*min(u,1-u)+.0006)
    patch('Brow','Brow',brow,lambda X,Y,W:.0011,head,ramp=.0018,sink=.0008)
patch('Lips','Lip',lambda X,Y,W:-1 if W<.06 else .004*(1-((X/.0215)**2+((Y+.0688)/.0110)**2)),lambda X,Y,W:.0009,head,ramp=.0025,sink=.0008)
mouth=pivot('MouthPivot',(0,1.3832,4.4740),head)
ell('MouthOpening',(0,1.3832,4.4732),(.0140,.00035,.0010),'Pupil',mouth)
for s in (-1,1):
    x=s*EYE_X
    rig=pivot('EyeRig_OD' if s<0 else 'EyeRig_OS',(x,EYE_Y,EYE_CZ),head)
    eye=pivot('Eye_OD' if s<0 else 'Eye_OS',(x,EYE_Y,EYE_CZ),rig)
    sc=ell('Sclera',(x,EYE_Y,EYE_CZ),(EYE_R*1.06,EYE_R,EYE_R),'Sclera',eye,32,24)
    cyl('Iris',(x,EYE_Y,PUPIL_Z+.0011),.0061,.0007,'Iris','Z',eye,48,False)
    ring('Limbus',(x,EYE_Y,PUPIL_Z+.0012),.0061,.0004,'Brow',eye)
    cyl('Pupil_OD' if s<0 else 'Pupil_OS',(x,EYE_Y,PUPIL_Z+.0002),.0021,.0004,'Pupil','Z',eye,32,False)
    ell('Catchlight',(x-.0021,EYE_Y+.0022,PUPIL_Z-.0004),(.0012,.0012,.0003),'White',rig,10,8)
    # Eyelids are spherical shells over the globe; the upper lid pivots on the eye centre to blink.
    # Eyelids: spherical shells over the globe whose free margins arch (almond fissure).
    RR=.01245;NA,NB=20,44
    for upper in (True,False):
        nm=('Lid_OD' if s<0 else 'Lid_OS') if upper else 'LowerLid'
        lp=pivot(nm,(x,EYE_Y,EYE_CZ),rig) if upper else rig
        lv=[];lf=[];margin=[]
        for ib in range(NB+1):
            ph=math.pi*(.06+.88*ib/NB);k=math.cos(ph)**2
            ye=(.0037-.0043*k) if upper else (-.0050+.0042*k)
            te=math.acos(max(-1,min(1,ye/RR)))
            t0,t1=(math.radians(6),te) if upper else (te,math.radians(174))
            for ia in range(NA+1):
                th=t0+(t1-t0)*ia/NA
                lv.append((x-RR*1.2*math.sin(th)*math.cos(ph),EYE_Y+RR*math.cos(th),EYE_CZ-RR*math.sin(th)*math.sin(ph)))
            margin.append((x-RR*1.215*math.sin(te)*math.cos(ph),EYE_Y+RR*math.cos(te),EYE_CZ-RR*1.012*math.sin(te)*math.sin(ph)))
        for ib in range(NB):
            for ia in range(NA):a2=ib*(NA+1)+ia;lf.append((a2,a2+1,a2+NA+2,a2+NA+1))
        new_mesh('UpperLidSkin' if upper else 'LidShell',lv,lf,'Skin',lp,outward=('from',(x,EYE_Y,EYE_CZ)))
        curve('LashLine' if upper else 'LowerLidMargin',margin[3:-3:4],.0006 if upper else .00022,'Hair' if upper else 'SkinShade',lp)
    ear=pivot('Ear',(s*.0715,1.440,4.572),head)
    ell('EarShell',(s*.0745,1.440,4.575),(.0075,.029,.017),'Skin',ear,24,16)
    ell('EarConcha',(s*.079,1.437,4.572),(.0032,.016,.009),'SkinShade',ear,16,12)
    curve('EarHelix',[(s*.078,1.418,4.568),(s*.081,1.452,4.566),(s*.079,1.466,4.578),(s*.077,1.456,4.592)],.0028,'Skin',ear)
tube('Neck',[(0,1.20,4.680),(0,1.30,4.632),(0,1.385,4.605)],[(.050,.048),(.045,.044),(.043,.042)],'Skin',group,seg=28,sub=5,caps=(False,False),up=(0,0,1))
tube('Torso',[(0,.735,4.74),(0,.90,4.75),(0,1.07,4.735),(0,1.19,4.705),(0,1.262,4.68)],
     [(.168,.118),(.150,.104),(.172,.112),(.162,.094),(.080,.066)],'Shirt',group,seg=40,sub=6,up=(0,0,1))
ring('Collar',(0,1.272,4.655),.050,.011,'ShirtSeam',group,axis='Y')
for s in (-1,1):
    tube('Sleeve',[(s*.05,1.255,4.70),(s*.165,1.225,4.70),(s*.205,1.145,4.705),(s*.222,.965,4.695),(s*.175,.875,4.555),(s*.152,.858,4.475)],
         [.047,.056,.051,.041,.034,.029],'Shirt',group,seg=24,sub=6)
    tube('Cuff',[(s*.156,.861,4.49),(s*.151,.857,4.468)],[.031,.030],'ShirtSeam',group,seg=24,sub=2)
    tube('PatientHand',[(s*.150,.857,4.47),(s*.143,.852,4.43),(s*.137,.849,4.392)],[(.022,.011),(.036,.012),(.038,.011)],'Skin',group,seg=20,sub=4,up=(0,1,0))
    for f,dx in enumerate((-.024,-.008,.008,.023)):
        xx=s*.137+(-s)*dx;L=[.044,.050,.047,.038][f]
        tube('PatientFinger',[(xx,.850,4.394),(xx,.846,4.394-L*.55),(xx,.834,4.394-L)],[.0083,.0077,.0066],'Skin',group,seg=12,sub=3)
    tube('PatientThumb',[(s*.118,.853,4.455),(s*.106,.849,4.428),(s*.103,.846,4.408)],[.0105,.0092,.0080],'Skin',group,seg=12,sub=3)
    tube('Thigh',[(s*.095,.768,4.73),(s*.103,.763,4.47),(s*.110,.748,4.30)],[.086,.074,.058],'Trousers',group,seg=32,sub=6)
    tube('LowerLeg',[(s*.110,.745,4.29),(s*.113,.52,4.25),(s*.115,.31,4.225)],[.056,.049,.037],'Trousers',group,seg=28,sub=6)
    tube('Shoe',[(s*.115,.262,4.27),(s*.117,.258,4.15),(s*.120,.250,4.02)],[(.040,.036),(.044,.035),(.036,.022)],'Shoe',group,seg=24,sub=6)
    tube('ShoeSole',[(s*.115,.230,4.285),(s*.120,.230,4.005)],[(.040,.007),(.036,.007)],'Rubber',group,seg=20,sub=2)

# ================= Refraction stand =================
group=root('PhoropterStand',(.62,0,4.425))
cyl('StandBase',(.62,.03,4.425),.24,.06,'StandWhite',vertices=64)
cyl('StandBaseTrim',(.62,.065,4.425),.17,.012,'Graphite',vertices=64)
cyl('StandColumn',(.62,1.05,4.425),.042,1.95,'StandWhite',vertices=48)
cyl('StandCap',(.62,2.04,4.425),.05,.04,'Graphite',vertices=48)
link('LampArm',(.62,2.03,4.425),(.33,2.0,4.32),.016,'StandWhite')
lamp=pivot('StandLamp',(.30,1.98,4.31),group)
cyl('LampHead',(.30,1.985,4.31),.055,.06,'StandWhite','Y',lamp,48)
cyl('LampDiffuser',(.30,1.953,4.31),.047,.006,'White','Y',lamp,48)
arm=pivot('ArmSwing',(.62,1.645,4.425),group)
cyl('ArmCollar',(.62,1.645,4.425),.062,.13,'StandWhite','Y',arm,48)
slab('Arm',rrect(.34,1.645,.56,.05,.018),4.402,4.448,'StandWhite',arm,.003)
lockp=pivot('LockLever',(.62,1.645,4.36),arm)
cyl('LockBoss',(.62,1.645,4.358),.016,.016,'Graphite','Z',lockp,24)
link('LockHandle',(.62,1.645,4.352),(.66,1.60,4.345),.007,'Graphite',lockp)

# ================= Phoropter head (refractor) =================
ZR=4.463    # rear (patient) face: 12 mm in front of the pupil plane (4.475)
ZF=4.390    # front (examiner) face of the lens batteries
group=root('Phoropter',(0,1.645,4.425))
# centre bar, mounting bracket and top controls
slab('CenterBar',rrect(0,1.527,.196,.044,.010),4.398,4.452,'PhoroBody',None,.0015)
slab('MountingPost',rrect(0,1.585,.042,.11,.006),4.405,4.447,'PhoroBody',None,.0015)
slab('MountingHead',rrect(.02,1.645,.09,.042,.010),4.400,4.450,'PhoroBody',None,.0015)
cyl('RotationAdjustKnob',(-.006,1.674,4.425),.013,.016,'Alu','Y',None,40)
cyl('TiltClampKnob',(.046,1.674,4.425),.013,.016,'Alu','Y',None,40)
slab('PDScaleWindow',rrect(-.047,1.530,.026,.011,.0015),4.3965,4.399,'Scale',None,.0003)
anchor=pivot('PDScaleAnchor',(-.047,1.530,4.3962),group)
slab('SpiritLevelWindow',rrect(.047,1.530,.026,.011,.0015),4.3965,4.399,'Black',None,.0003)
slab('BubbleLevel',rrect(.047,1.530,.020,.0055,.0027),4.3958,4.3966,'Glass',None,0,1,(),False)
bub=pivot('Bubble',(.047,1.530,4.3955),group);ell('BubbleMesh',(.047,1.530,4.3955),(.0035,.0022,.0006),'White',bub,12,8)
flat_marks('SpiritLevelMarks',[[(.047+dx-.0003,1.5265),(.047+dx-.0003,1.5335),(.047+dx+.0003,1.5335),(.047+dx+.0003,1.5265)] for dx in (-.0042,.0042)],4.3954,'Black')
lev=pivot('LevelKnob',(-.108,1.535,4.425),group)
cyl('LevelDial',(-.108,1.535,4.425),.011,.018,'Alu','X',lev,40);cyl('LevelDialCap',(-.118,1.535,4.425),.008,.004,'AluDark','X',lev,32)
pdk=pivot('PDWheel',(.108,1.535,4.425),group)
cyl('PDKnob',(.108,1.535,4.425),.012,.018,'Alu','X',pdk,40);cyl('PDKnobCap',(.118,1.535,4.425),.009,.004,'AluDark','X',pdk,32)
for s in (-1,1):
    vl=pivot('VergenceLever_'+('OD' if s<0 else 'OS'),(s*.088,1.549,4.42),group)
    link('VergenceLeverArm',(s*.088,1.549,4.42),(s*.094,1.572,4.412),.0035,'Alu',vl)
    ell('VergenceLeverTip',(s*.094,1.574,4.412),(.005,.005,.005),'Alu',vl)
# forehead rest: knob on the examiner side, pad against the brow on the patient side
slab('ForeheadPost',rrect(0,1.494,.016,.024,.004),4.402,4.445,'PhoroBody',None,.001)
fk=pivot('ForeheadKnob',(0,1.483,4.385),group)
disc('ForeheadKnobBody',(0,1.483),.012,4.378,4.398,'Alu',fk,knurl=36)
disc('ForeheadKnobCap',(0,1.483),.009,4.374,4.378,'AluDark',fk)
fr=pivot('ForeheadRest',(0,1.497,4.4705),group)
link('ForeheadRod',(0,1.499,4.44),(0,1.499,4.466),.0045,'Alu',fr)
slab('ForeheadPad',rrect(0,1.497,.050,.019,.008),4.4655,4.4755,'Rubber',fr,.003,3)
reading=pivot('ReadingRodHolder',(.020,1.506,4.40),group)
box('ReadingRodHinge',(.020,1.506,4.398),(.012,.010,.010),'Alu',.002,reading)

HOUSING=[(-18,60),(20,63),(70,64),(98,52),(122,33),(130,10),(124,-16),(108,-38),(96,-62),(88,-86),(80,-112),
         (76,-138),(62,-158),(40,-166),(18,-158),(6,-138),(1,-112),(-4,-90),(-9,-62),(-14,-36),(-18,-10),(-20,18),(-20,44)]
def battery(eye,sgn):
    ax=sgn*EYE_X;ay=EYE_Y
    def P(u,vv):return (ax+sgn*u/1000,ay+vv/1000)
    pod=pivot('Pod_'+eye,(ax,ay,(ZF+ZR)/2),group)
    pivot('Aperture_'+eye,(ax,ay,ZF-.0105),pod)
    outline=chaikin([P(u,w) for u,w in HOUSING],3)
    slab('Housing_'+eye,outline,ZF,ZR,'PhoroBody',pod,.0025,3,((ax,ay,.0095),))
    # rear face shield around the eye port and a front cover glass
    slab('FaceShield_'+eye,rrect(ax+sgn*.006,ay-.004,.058,.058,.016),ZR-.0005,ZR+.0015,'ShieldPlastic',pod,.0008,2,((ax,ay,.0125),))
    disc('ApertureCell_'+eye,(ax,ay),.0128,ZF-.0105,ZF,'Alu',pod,inner=.0095)
    disc('ApertureGlass_'+eye,(ax,ay),.0096,ZF-.0035,ZF-.0030,'Glass',pod,bevel=0)
    disc('AxisRefScale_'+eye,(ax,ay),.0215,ZF-.0035,ZF,'Scale',pod,inner=.0129)
    angs=[math.radians(d) for d in range(0,360,5)]
    flat_marks('AxisRefTicks_'+eye,tick_quads(ax,ay,.0178,[.0034 if d%15==0 else .0018 for d in range(0,360,5)],angs,.00042),ZF-.0037,'Black',pod)
    pivot('AxisRefAnchor_'+eye,(ax,ay,ZF-.0038),pod)
    ap=pivot('AxisPointer_'+eye,(ax,ay,ZF-.0108),pod)
    flat_marks('AxisPointerMarks',[tri_quad(ax,ay,a,.0102,.0125,.0013) for a in (0,math.pi)]+tick_quads(ax,ay,.0098,[.0006,.0006],(0,math.pi),.0003),ZF-.0110,'White',ap)
    # internal auxiliary lens wheel: occluder and pinhole discs index into the main aperture
    wheel=pivot('AuxWheel_'+eye,(ax,ay+.030,(ZF+ZR)/2),pod)
    for nm,ang in [('Occluder_'+eye,-60),('Pinhole_'+eye,60)]:
        a=math.radians(-90+ang);cx=ax+math.cos(a)*.030;cy=ay+.030+math.sin(a)*.030
        pp=pivot(nm,(cx,cy,(ZF+ZR)/2),wheel)
        disc(nm+'Disc',(cx,cy),.0108,(ZF+ZR)/2-.0008,(ZF+ZR)/2+.0008,'Black',pp,bevel=0,inner=.0010 if 'Pinhole' in nm else 0)
    # weak sphere dial: large knurled wheel on the outer side, partly inside the housing
    sc=P(73,-60)
    sph=pivot('Sph_'+eye,(sc[0],sc[1],ZF+.021),pod)
    disc('WeakSphereDial',sc,.066,ZF+.014,ZF+.028,'Alu',sph,knurl=120)
    disc('WeakSphereFace',sc,.058,ZF+.0125,ZF+.014,'AluDark',sph)
    flat_marks('WeakSphereTicks',tick_quads(sc[0],sc[1],.0545,[.003]*48,[k*math.tau/48 for k in range(48)],.0007),ZF+.0123,'Black',sph)
    win=P(100,-26)
    slab('SphereWindowBezel',rrect(win[0],win[1],.026,.016,.003),ZF-.003,ZF+.001,'AluDark',pod,.0008)
    slab('SphereWindow',rrect(win[0],win[1],.021,.0115,.002),ZF-.0035,ZF-.003,'Scale',pod,.0003)
    pivot('SphWindow_'+eye,(win[0],win[1],ZF-.0037),pod)
    # auxiliary lens knob inside the auxiliary scale, ringed by the strong sphere control
    ac=P(102,22)
    pivot('AuxScaleAnchor_'+eye,(ac[0],ac[1],ZF-.0045),pod)
    disc('AuxLensScale',ac,.0235,ZF-.0042,ZF,'Scale',pod,inner=.0105)
    strong=pivot('StrongSph_'+eye,(ac[0],ac[1],ZF-.004),pod)
    disc('StrongSphereRing',ac,.0285,ZF-.0055,ZF,'Alu',strong,knurl=90,inner=.0236)
    aux=pivot('Aux_'+eye,(ac[0],ac[1],ZF-.012),pod)
    disc('AuxLensKnob',ac,.0102,ZF-.0175,ZF-.004,'Alu',aux,knurl=40)
    disc('AuxKnobCap',ac,.0085,ZF-.0185,ZF-.0175,'AluDark',aux)
    flat_marks('AuxKnobIndex',[[(ac[0]-.0008,ac[1]+.003),(ac[0]-.0008,ac[1]+.0098),(ac[0]+.0008,ac[1]+.0098),(ac[0]+.0008,ac[1]+.003)]],ZF-.0187,'White',aux)
    # cylinder lobe: protractor, concentric axis (outer) and power (inner) knobs
    cc=P(42,-126)
    disc('CylAxisProtractor',cc,.0305,ZF-.004,ZF,'Scale',pod,inner=.0185)
    flat_marks('ProtractorTicks',tick_quads(cc[0],cc[1],.0255,[.0045 if d%15==0 else .0024 for d in range(0,360,5)],angs,.00045),ZF-.0042,'Black',pod)
    pivot('ProtractorAnchor_'+eye,(cc[0],cc[1],ZF-.0043),pod)
    axk=pivot('AxisKnob_'+eye,(cc[0],cc[1],ZF-.010),pod)
    disc('CylAxisKnob',cc,.0175,ZF-.016,ZF-.004,'Alu',axk,knurl=60)
    flat_marks('AxisKnobArrows',[tri_quad(cc[0],cc[1],a,.0186,.0252,.0019) for a in (0,math.pi)],ZF-.0045,'Red',axk)
    cyk=pivot('Cyl_'+eye,(cc[0],cc[1],ZF-.022),pod)
    disc('CylPowerKnob',cc,.0105,ZF-.029,ZF-.016,'Alu',cyk,knurl=36)
    disc('CylPowerCap',cc,.0088,ZF-.030,ZF-.029,'AluDark',cyk)
    flat_marks('CylKnobIndex',[[(cc[0]-.0007,cc[1]+.002),(cc[0]-.0007,cc[1]+.0082),(cc[0]+.0007,cc[1]+.0082),(cc[0]+.0007,cc[1]+.002)]],ZF-.0302,'White',cyk)
    cw=P(4,-92)
    slab('CylWindowBezel',rrect(cw[0],cw[1],.020,.012,.0025),ZF-.0025,ZF+.001,'AluDark',pod,.0006)
    slab('CylWindow',rrect(cw[0],cw[1],.016,.0085,.0015),ZF-.003,ZF-.0025,'Scale',pod,.0003)
    pivot('CylWindow_'+eye,(cw[0],cw[1],ZF-.0032),pod)
    # turret: cross cylinder unit (parked up and out) and rotary prism unit
    tc=P(40,-34);L=math.hypot(40,34)/1000
    tur=pivot('Turret_'+eye,(tc[0],tc[1],ZF-.016),pod)
    disc('TurretHub',tc,.0108,ZF-.020,ZF-.004,'Alu',tur)
    disc('TurretHubCap',tc,.0072,ZF-.0215,ZF-.020,'AluDark',tur)
    jc=P(48,18);pc=P(40-4.6,-34-52.3)
    for end in (jc,pc):
        d=Vector((end[0]-tc[0],end[1]-tc[1]));ang=math.atan2(d.y,d.x);r0,r1=.0085,.0062
        cap=[(tc[0]+math.cos(ang+math.pi/2+math.pi*k/10)*r0,tc[1]+math.sin(ang+math.pi/2+math.pi*k/10)*r0) for k in range(11)]
        cap+=[(end[0]+math.cos(ang-math.pi/2+math.pi*k/10)*r1,end[1]+math.sin(ang-math.pi/2+math.pi*k/10)*r1) for k in range(11)]
        slab('TurretArm',cap,ZF-.0185,ZF-.0125,'Alu',tur,.0012,3)
    cell=pivot('JCCCell_'+eye,(jc[0],jc[1],ZF-.021),tur)
    disc('JCCRetainer',jc,.0165,ZF-.0215,ZF-.0175,'Alu',cell,inner=.0128)
    for side in (-1,1):
        cyl('JCCRollKnob',(jc[0]+side*.0215,jc[1],ZF-.0215),.0042,.009,'AluDark','X',cell,20)
        link('JCCRollAxle',(jc[0]+side*.0165,jc[1],ZF-.0215),(jc[0]+side*.0175,jc[1],ZF-.0215),.0018,'Alu',cell)
    flip=pivot('JCCFlip_'+eye,(jc[0],jc[1],ZF-.0225),cell)
    disc('JCCLensRim',jc,.0135,ZF-.0255,ZF-.0195,'AluDark',flip,inner=.0118)
    disc('JCCLens',jc,.0119,ZF-.0228,ZF-.0222,'Glass',flip,bevel=0)
    for k,a in enumerate((45,135,225,315)):
        r=math.radians(a);cx=jc[0]+math.cos(r)*.0127;cy=jc[1]+math.sin(r)*.0127
        for zz in (ZF-.0257,ZF-.0193):
            ell('JCCDot',(cx,cy,zz),(.0012,.0012,.0004),'Red' if a in (45,225) else 'White',flip,10,6)
    prism=pivot('RotaryPrism_'+eye,(pc[0],pc[1],ZF-.021),tur)
    disc('PrismLoupe',pc,.0165,ZF-.0245,ZF-.0175,'Alu',prism,inner=.0118)
    disc('PrismLens',pc,.0119,ZF-.0215,ZF-.021,'Glass',prism,bevel=0)
    disc('PrismScale',pc,.0158,ZF-.0250,ZF-.0245,'Scale',prism,inner=.0123)
    flat_marks('PrismTicks',tick_quads(pc[0],pc[1],.0128,[.0024 if k%5==0 else .0013 for k in range(41)],[math.radians(-90+k*4.5) for k in range(41)],.0004),ZF-.0252,'Black',prism)
    cyl('PrismFingerRoll',(pc[0],pc[1]+.0195,ZF-.021),.0045,.010,'AluDark','X',prism,24)
    # corneal aligning device on the outer edge
    ca=P(146,30)
    slab('CornealAlignerMount',rrect((ca[0]+P(126,30)[0])/2,ca[1],.022,.012,.003),ZF+.020,ZF+.040,'PhoroBody',pod,.001)
    slab('CornealAligner_'+eye,rrect(ca[0],ca[1],.018,.020,.004),ZF+.008,ZF+.052,'PhoroBody',pod,.0015)
    slab('CornealAlignerWindow',rrect(ca[0],ca[1],.011,.012,.002),ZF+.0065,ZF+.008,'Window',pod,.0004)
battery('OD',-1);battery('OS',1)

# ================= Hand tools =================
group=root('Retinoscope',(0,0,0))
tube('RetHandle',[(0,-.075,0),(0,0,0),(0,.055,0)],[.0165,.0170,.0160],'Graphite',seg=24,sub=3)
for y in [-.06,-.048,-.036,-.024,-.012]:ring('RetGrip',(0,y,0),.0172,.0012,'Rubber',axis='Y')
cyl('RetSleeve',(0,.072,0),.0205,.034,'Chrome','Y',vertices=40)
ring('RetSleeveKnurl',(0,.062,0),.0207,.0013,'AluDark',axis='Y')
box('RetHead',(0,.112,-.004),(.050,.056,.040),'Graphite',.012)
cyl('RetForehead',(0,.112,.018),.012,.008,'Rubber','Z',vertices=24)
cyl('RetLens',(0,.115,-.0245),.0125,.004,'Chrome','Z',vertices=32)
cyl('RetMirror',(0,.115,-.027),.009,.002,'Lens','Z',vertices=32)
box('RetStreak',(0,.115,-.0285),(.0022,.016,.0008),'White',.0001)
cyl('RetPeephole',(0,.122,.022),.0035,.006,'Black','Z',vertices=16)
group=root('PDRuler',(0,0,0))
# flat rule with a nose notch on the lower edge; the scale runs along the upper edge
def ruler_outline():
    w,h,r=.150,.026,.004;pts=[]
    corner=lambda cx,cy,a0:[(cx+math.cos(math.radians(a0+90*k/6))*r,cy+math.sin(math.radians(a0+90*k/6))*r) for k in range(7)]
    pts+=corner(w/2-r,h/2-r,0)+corner(-w/2+r,h/2-r,90)+corner(-w/2+r,-h/2+r,180)
    pts+=[(math.cos(math.pi-math.pi*k/16)*.0105,-h/2+math.sin(math.pi-math.pi*k/16)*.0105) for k in range(17)]
    pts+=corner(w/2-r,-h/2+r,270);return pts
slab('RulerBody',ruler_outline(),-.0008,.0008,'RulerWhite',None,.0004,1)
ZERO=-.0355
q=[]
for mm in range(-4,107):
    x=ZERO+mm/1000;L=.006 if mm%10==0 else .0042 if mm%5==0 else .0026
    q.append([(x-.00013,.0128-L),(x-.00013,.0128),(x+.00013,.0128),(x+.00013,.0128-L)])
flat_marks('RulerTicks',q,-.00085,'Black')
flat_marks('RulerNoseMark',[tri_quad(0,-.0095,math.pi/2,-.0,.003,.0016)],-.00085,'Red')
pivot('RulerZero',(ZERO,.0128,-.0009),group)
group=root('CleaningCloth',(0,0,0))
cv=[];cf=[];N=10
for i in range(N+1):
    for j in range(N+1):
        x=-.05+.1*i/N;z=-.05+.1*j/N;cv.append((x,.004*math.sin(i*1.3)*math.cos(j*.9)+.002,z))
for i in range(N):
    for j in range(N):a=i*(N+1)+j;cf.append((a,a+N+1,a+N+2,a+1))
o=new_mesh('FoldedCloth',cv,cf,'Cuff',None,outward=('dir',(0,1,0)))
mod=o.modifiers.new('Thick','SOLIDIFY');mod.thickness=.004;apply_mod(o,mod)
group=root('Remote',(0,0,0));box('RemoteBody',(0,0,0),(.047,.13,.018),'Graphite',.012)
for y in [.043,.02,-.003,-.026]:
    for x in [-.012,.012]:ell('RemoteButton',(x,y,-.009),(.006,.006,.003),'White')
group=root('Clipboard',(0,0,0));box('Board',(0,0,0),(.24,.33,.012),'Oak',.006)
box('RecordSheet',(0,0,-.008),(.22,.30,.001),'Paper',0)
box('Clip',(0,.155,-.015),(.09,.03,.015),'Chrome',.004)
for y in [.105,.07,.035,0,-.035,-.07,-.105]:box('PaperLine',(0,y,-.01),(.19,.0007,.001),'Sage',0)
group=root('Pen',(0,0,0));cyl('PenBody',(0,0,0),.004,.14,'Teal');cyl('PenTip',(0,-.075,0),.0015,.012,'Chrome')
group=root('Sanitizer',(0,0,0))
tube('Bottle',[(0,-.07,0),(0,0,0),(0,.055,0),(0,.07,0)],[.031,.032,.031,.014],'Bottle',seg=36,sub=4,up=(1,0,0))
cyl('BottleLabel',(0,-.012,0),.0325,.06,'Teal','Y',vertices=36,bevel=False)
cyl('BottleNeck',(0,.078,0),.011,.014,'White','Y',vertices=24)
pump=pivot('SanitizerPump',(0,.09,0),group)
cyl('PumpStem',(0,.092,0),.0045,.02,'Chrome','Y',pump,16)
box('PumpHead',(.008,.106,0),(.034,.011,.016),'White',.004,pump)
cyl('PumpNozzle',(.026,.106,0),.0025,.006,'White','X',pump,12)
pivot('SanitizerNozzle',(.030,.106,0),pump)
group=root('LightSwitch',(3.78,1.28,3.2));box('SwitchPlate',(3.78,1.28,3.2),(.025,.14,.09),'Porcelain',.01)
lever=pivot('SwitchLever',(3.756,1.28,3.2),group);box('SwitchRocker',(3.756,1.28,3.2),(.018,.073,.04),'White',.006,lever)

# ================= First-person hands (camera space) =================
group=root('ClinicianHands',(0,0,0))
for side,sgn in [('R',1),('L',-1)]:
    wrist=pivot('Hand_'+side,(sgn*.22,-.30,.44),group)
    tube('Sleeve_'+side,[(sgn*.33,-.52,.12),(sgn*.27,-.40,.30),(sgn*.236,-.322,.386)],[.054,.048,.043],'CoatWhite',wrist,seg=24,sub=4)
    tube('GloveCuff_'+side,[(sgn*.240,-.326,.374),(sgn*.226,-.309,.404)],[.036,.034],'Glove',wrist,seg=24,sub=2)
    tube('Palm_'+side,[(sgn*.225,-.307,.398),(sgn*.221,-.301,.432),(sgn*.216,-.298,.468)],[(.031,.014),(.040,.016),(.042,.013)],'Glove',wrist,seg=24,sub=4,up=(0,1,0))
    for f,(dx,L) in enumerate([(-.025,.047),(-.0085,.053),(.0085,.050),(.024,.041)]):
        xx=sgn*(.216+dx);zz=.466
        finger=pivot('Finger_'+side+str(f),(xx,-.298,zz),wrist)
        tube('FingerMesh',[(xx,-.298,zz-.004),(xx,-.300,zz+L*.5),(xx,-.307,zz+L)],[.0088,.0076,.0064],'Glove',finger,seg=14,sub=4)
    tube('Thumb_'+side,[(sgn*.196,-.305,.412),(sgn*.176,-.302,.438),(sgn*.164,-.304,.460)],[.0105,.0090,.0078],'Glove',wrist,seg=14,sub=4)

# Scene assembly in Unity places tools on their rack. Save all editable parts here.
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Blender','RefractionRoom.blend'))
roots=[o for o in bpy.data.objects if o.parent is None]
def descendants(o):
    r=[o]
    for c in o.children:r.extend(descendants(c))
    return r
manifest=[]
for name in os.listdir(OUT):
    if name.endswith('.clinicmesh.json') or name.endswith('.fbx'):
        if name.split('.')[0] not in [r.name for r in roots]:os.remove(os.path.join(OUT,name))
for root_obj in roots:
    bpy.ops.object.select_all(action='DESELECT')
    for o in descendants(root_obj):o.select_set(True)
    target=os.path.join(OUT,root_obj.name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=target,use_selection=True,object_types={'EMPTY','MESH'},add_leaf_bones=False,
        axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
        bake_anim=False,path_mode='AUTO',mesh_smooth_type='FACE',use_mesh_modifiers=True)
    manifest.append({'asset':root_obj.name,'objects':len(descendants(root_obj)),'source':'RefractionRoom.blend'})
    # A lossless metres-based mesh/hierarchy interchange avoids FBX's nested-empty
    # bake-space-transform ambiguity. Vertices are baked in Blender, never recreated
    # as Unity primitives. Identity rest rotations make clinical pivots explicit.
    hierarchy=descendants(root_obj);entries=[]
    for obj in hierarchy:
        pos=obj.matrix_world.translation
        parentpos=obj.parent.matrix_world.translation if obj.parent else Vector((0,0,0))
        local=pos-parentpos
        entry={'name':obj.name,'parent':hierarchy.index(obj.parent) if obj.parent in hierarchy else -1,
            'position':[local.x,local.z,local.y],'vertices':[],'triangles':[],'uv':[],
            'material':'','color':[1,1,1],'metallic':0.0,'roughness':0.5}
        if obj.type=='MESH':
            mesh=obj.data;mesh.calc_loop_triangles()
            for vert in mesh.vertices:
                p=obj.matrix_world@vert.co-pos
                entry['vertices'].extend([round(p.x,7),round(p.z,7),round(p.y,7)])
            for tri in mesh.loop_triangles:entry['triangles'].extend([tri.vertices[0],tri.vertices[2],tri.vertices[1]])
            entry['uv']=[0.0]*(len(mesh.vertices)*2)
            if mesh.uv_layers.active:
                for loop in mesh.loops:
                    uv=mesh.uv_layers.active.data[loop.index].uv;i=loop.vertex_index*2
                    entry['uv'][i:i+2]=[round(uv.x,6),round(uv.y,6)]
            if mesh.materials:
                ma=mesh.materials[0];entry['material']=ma.name;entry['color']=list(ma.diffuse_color[:3]);bs=ma.node_tree.nodes.get('Principled BSDF')
                if bs:entry['metallic']=bs.inputs['Metallic'].default_value;entry['roughness']=bs.inputs['Roughness'].default_value
        entries.append(entry)
    with open(os.path.join(OUT,root_obj.name+'.clinicmesh.json'),'w') as f:json.dump({'name':root_obj.name,'nodes':entries},f,separators=(',',':'))
with open(os.path.join(ROOT,'Reports','blender-assets.json'),'w',encoding='utf-8') as f:json.dump(manifest,f,ensure_ascii=False,indent=2)
print('CLINIC_ASSETS_READY',len(roots))
