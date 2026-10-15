"""Own-source clinic assets. Run Blender 5.2 --background --python Blender/build_clinic.py.
Coordinates in all modelling helpers are metres, Unity (X right,Y up,Z forward).
FBX retains named pivots for Unity-driven physical interactions; .blend remains editable.
"""
import bpy, math, os, random, json
from mathutils import Vector
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'UnityProject','Assets','Models')
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
random.seed(731)
def v(p): return Vector((p[0],p[2],p[1]))
def mat(name,c,metal=0,rough=.5):
    m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    return m
M={
 'WarmPlaster':mat('WarmPlaster',(.78,.77,.69)), 'Sage':mat('Sage',(.26,.39,.36)),
 'Oak':mat('Oak',(.44,.29,.17)), 'OakLight':mat('OakLight',(.59,.43,.27)),
 'Porcelain':mat('Porcelain',(.86,.87,.82),0,.28), 'Graphite':mat('Graphite',(.055,.075,.08),.15,.27),
 'Rubber':mat('Rubber',(.035,.045,.047)), 'Chrome':mat('Chrome',(.55,.62,.63),.85,.18),
 'Brass':mat('Brass',(.65,.46,.23),.65,.27), 'Teal':mat('Teal',(.12,.32,.34)),
 'Lens':mat('Lens',(.15,.29,.32),.25,.14), 'White':mat('White',(.96,.95,.89)),
 'Black':mat('Black',(.01,.014,.017)), 'Red':mat('Red',(.72,.12,.1)),
 'Skin':mat('Skin',(.62,.36,.24),0,.62), 'SkinLight':mat('SkinLight',(.71,.44,.31)),
 'Lip':mat('Lip',(.44,.20,.15)), 'Hair':mat('Hair',(.035,.025,.02)),
 'Shirt':mat('Shirt',(.19,.34,.38)), 'ShirtSeam':mat('ShirtSeam',(.26,.43,.46)),
 'Trousers':mat('Trousers',(.16,.19,.22)), 'Shoe':mat('Shoe',(.12,.09,.07)),
 'Iris':mat('Iris',(.23,.13,.065)), 'Leaf':mat('Leaf',(.16,.30,.16)),
 'Window':mat('Window',(.66,.80,.82)), 'Cuff':mat('Cuff',(.78,.84,.82)),
 'Glove':mat('Glove',(.68,.79,.80)), 'Paper':mat('Paper',(.93,.90,.8)),
}
group=None
def finish(o,name,ma,parent=None):
    o.name=name; o.data.materials.clear();o.data.materials.append(M[ma]);
    for p in o.data.polygons:p.use_smooth=True
    if parent is None:parent=group
    if parent:
        bpy.context.view_layer.update()
        w=o.matrix_world.copy();o.parent=parent;o.matrix_world=w
    return o
def pivot(name,p,parent=None):
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=v(p)
    if parent:
        bpy.context.view_layer.update()
        w=o.matrix_world.copy();o.parent=parent;o.matrix_world=w
    return o
def box(name,p,size,ma,bevel=.02,parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(p));o=bpy.context.object;o.scale=(size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft machined edges','BEVEL');mod.width=bevel;mod.segments=3
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,name,ma,parent)
def ell(name,p,size,ma,parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,radius=1,location=v(p));o=bpy.context.object
    o.scale=(size[0],size[2],size[1]);return finish(o,name,ma,parent)
def cyl(name,p,r,depth,ma,axis='Y',parent=None,vertices=40):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=v(p));o=bpy.context.object
    direction={'X':Vector((1,0,0)),'Y':Vector((0,0,1)),'Z':Vector((0,1,0))}[axis]
    o.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    mod=o.modifiers.new('Lathed rim','BEVEL');mod.width=min(.008,r*.12);mod.segments=2
    bpy.ops.object.modifier_apply(modifier=mod.name);return finish(o,name,ma,parent)
def ring(name,p,r,thick,ma,parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=thick,major_segments=48,minor_segments=10,location=v(p),rotation=(math.pi/2,0,0))
    return finish(bpy.context.object,name,ma,parent)
def link(name,a,b,r,ma,parent=None):
    d=v(b)-v(a);bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=r,depth=d.length,location=(v(a)+v(b))/2)
    o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return finish(o,name,ma,parent)
def curve(name,points,r,ma,parent=None):
    d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.bevel_depth=r;d.bevel_resolution=3
    s=d.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
    for bp,p in zip(s.bezier_points,points):bp.co=v(p);bp.handle_left_type=bp.handle_right_type='AUTO'
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.select_set(False);return finish(o,name,ma,parent)

group=pivot('Architecture',(0,0,0))
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
# acoustic oak slats at the patient end
for i in range(27):box('AcousticSlat',(-3.6+i*.27,1.48,5.87),(.045,2.83,.06),'OakLight',.005)
# window, sill, louvers
box('WindowGlass',(-3.79,1.95,.6),(.035,1.6,3.0),'Window')
for yy in [1.12,2.78]:box('WindowHorizontal',(-3.75,yy,.6),(.09,.06,3.15),'Porcelain')
for zz in [-.93,2.13]:box('WindowUpright',(-3.75,1.95,zz),(.09,1.7,.06),'Porcelain')
for i in range(18):box('Blind',(-3.70,1.17+i*.088,.6),(.10,.028,2.98),'White',.004)
box('WindowSill',(-3.63,1.1,.6),(.34,.06,3.28),'OakLight')
# entrance with a window and handle
box('DoorLeaf',(2.65,1.15,-2.36),(1.2,2.3,.065),'Sage')
box('DoorInset',(2.65,1.66,-2.31),(.6,.66,.02),'Window')
box('DoorFrameTop',(2.65,2.33,-2.3),(1.36,.075,.13),'Oak')
for x in [2.0,3.3]:box('DoorFrame',(x,1.15,-2.3),(.075,2.3,.13),'Oak')
link('DoorHandle',(2.2,1.04,-2.21),(2.43,1.04,-2.21),.016,'Chrome')
# worktop with drawers at left
box('Cabinet',(-2.85,.45,4.2),(1.55,.9,2.55),'Porcelain',.045)
box('Countertop',(-2.85,.94,4.2),(1.62,.07,2.62),'OakLight',.025)
for z in [3.4,4.2,5.0]:
    for y in [.19,.49,.77]:
        box('Drawer',(-2.045,y,z),(.025,.25,.72),'Porcelain',.01)
        link('DrawerPull',(-2.015,y,z-.15),(-2.015,y,z+.15),.012,'Brass')
# writing desk and wheeled stool
box('DeskTop',(2.7,.85,1.7),(1.6,.07,2.0),'OakLight')
for x in [2.08,3.32]:
    for z in [.87,2.53]:box('DeskLeg',(x,.42,z),(.06,.84,.06),'Graphite')
cyl('StoolSeat',(.8,.58,2.8),.23,.085,'Teal');cyl('StoolStem',(.8,.31,2.8),.033,.5,'Chrome')
for a in range(5):
    an=a*math.tau/5;end=(.8+math.cos(an)*.3,.1,2.8+math.sin(an)*.3)
    link('StoolBase',(.8,.12,2.8),end,.018,'Chrome');ell('StoolCaster',end,(.042,.05,.042),'Rubber')
# planter and leaves
cyl('Pot',(3.2,.24,5.15),.27,.46,'Porcelain')
for i in range(18):
    a=i*2.4;h=.7+random.random()*.7;e=(3.2+math.cos(a)*.32,h,5.15+math.sin(a)*.32)
    link('PlantStem',(3.2,.42,5.15),e,.008,'Leaf')
    leaf=ell('PlantLeaf',e,(.085,.25,.025),'Leaf');leaf.rotation_euler=(random.random(),random.random(),a)
# chart on front wall: calibrated optotype patch created by Unity
box('ChartFrame',(0,1.5,-1.53),(1.18,.92,.07),'Graphite')
box('ChartSurface',(0,1.5,-1.482),(1.08,.82,.012),'White',0)
box('ChartStand',(0,.49,-1.56),(.05,.98,.05),'Chrome')
box('ChartFoot',(0,.05,-1.56),(.65,.05,.38),'Graphite')
box('Projector',(-2.75,2.83,-.9),(.4,.13,.3),'Porcelain');cyl('ProjectorLens',(-2.75,2.83,-1.06),.045,.03,'Lens','Z')
# compact on-desk chart repeat
box('MonitorBody',(2.5,1.22,1.9),(.78,.48,.055),'Graphite')
box('MonitorScreen',(2.5,1.22,1.86),(.70,.4,.012),'White',0)
box('MonitorStem',(2.5,1.0,1.92),(.05,.24,.05),'Chrome')
box('MonitorFoot',(2.5,.9,1.92),(.35,.025,.2),'Graphite')
# exam chair
group=pivot('ExamChair',(0,0,4.68))
box('ChairFoot',(0,.08,4.68),(.72,.14,.82),'Graphite',.10)
cyl('ChairHydraulics',(0,.36,4.68),.12,.55,'Chrome')
box('ChairSeat',(0,.61,4.66),(.65,.16,.65),'Teal',.10)
box('ChairBack',(0,1.02,4.99),(.64,.83,.15),'Teal',.1)
for x in [-.39,.39]:
    link('ArmSupport',(x,.64,4.72),(x,.94,4.72),.025,'Chrome')
    box('Armrest',(x,.95,4.65),(.12,.07,.46),'Graphite',.04)
box('Footrest',(0,.20,4.06),(.51,.045,.29),'Chrome')
# patient: anatomically oriented adult seated facing -Z
group=pivot('Patient',(0,0,4.55))
ell('Torso',(0,1.00,4.65),(.235,.31,.145),'Shirt')
ell('Waist',(0,.76,4.65),(.20,.17,.14),'Trousers')
for x in [-.135,.135]:
    ell('Thigh',(x,.65,4.40),(.107,.10,.29),'Trousers')
    ell('LowerLeg',(x,.39,4.15),(.085,.27,.085),'Trousers')
    ell('Ankle',(x,.15,4.13),(.067,.08,.067),'Trousers')
    ell('Shoe',(x,.08,4.03),(.09,.07,.19),'Shoe')
    box('ShoeSole',(x,.035,4.03),(.18,.03,.34),'Rubber',.03)
    for z in [3.99,4.025,4.06]:link('Laces',(x-.055,.135,z),(x+.055,.135,z),.004,'Paper')
    armx=1 if x>0 else -1
    ell('Sleeve',(armx*.25,1.10,4.64),(.09,.15,.105),'Shirt')
    link('UpperArm',(armx*.28,1.08,4.64),(armx*.34,.91,4.60),.062,'Skin')
    ell('Elbow',(armx*.34,.90,4.60),(.065,.065,.065),'Skin')
    link('Forearm',(armx*.34,.91,4.60),(armx*.34,.96,4.31),.052,'SkinLight')
    ell('PatientPalm',(armx*.34,.96,4.27),(.05,.028,.075),'SkinLight')
    for f in range(4):ell('PatientFinger',(armx*.34+(f-1.5)*.023,.955,4.19),(.011,.012,.052),'Skin')
    ell('PatientThumb',(armx*.28,.95,4.26),(.018,.018,.045),'Skin')
    curve('ShirtFold',[(armx*.05,.88,4.51),(armx*.15,.92,4.505),(armx*.19,1.00,4.51)],.0035,'ShirtSeam')
cyl('Neck',(0,1.25,4.62),.064,.17,'Skin')
ring('Collar',(0,1.205,4.51),.067,.012,'ShirtSeam')
head=pivot('HeadPivot',(0,1.31,4.56),group)
ell('Head',(0,1.43,4.59),(.113,.152,.105),'SkinLight',head)
ell('Jaw',(0,1.345,4.565),(.09,.067,.082),'Skin',head)
ell('Chin',(0,1.315,4.495),(.053,.026,.031),'SkinLight',head)
ell('NoseBridge',(0,1.421,4.478),(.016,.042,.025),'SkinLight',head)
ell('NoseTip',(0,1.391,4.461),(.023,.015,.024),'Skin',head)
for sign in [-1,1]:
    ell('Nostril',(sign*.014,1.381,4.454),(.006,.004,.005),'Lip',head)
    ell('Ear',(sign*.113,1.426,4.60),(.023,.044,.017),'Skin',head)
    ell('EarConcha',(sign*.12,1.428,4.586),(.011,.024,.008),'Lip',head)
    x=sign*.032
    eye=pivot('Eye_OD' if sign<0 else 'Eye_OS',(x,1.445,4.49),head)
    ell('Sclera',(x,1.445,4.49),(.024,.012,.012),'White',eye)
    cyl('Iris',(x,1.445,4.478),.010,.003,'Iris','Z',eye)
    cyl('Pupil_OD' if sign<0 else 'Pupil_OS',(x,1.445,4.475),.0048,.002,'Black','Z',eye)
    ell('Catchlight',(x-.003,1.449,4.473),(.002,.002,.001),'White',eye)
    lid=ell('Lid_OD' if sign<0 else 'Lid_OS',(x,1.454,4.478),(.025,.003,.013),'Skin',head)
    curve('Brow',[(x-.024,1.47,4.483),(x,1.477,4.476),(x+.024,1.471,4.484)],.004,'Hair',head)
    curve('LowerEyelid',[(x-.023,1.444,4.478),(x,1.433,4.477),(x+.023,1.444,4.48)],.002,'Skin',head)
mouth=pivot('MouthPivot',(0,1.352,4.48),head)
ell('MouthOpening',(0,1.352,4.48),(.032,.0028,.002),'Lip',mouth)
curve('UpperLip',[(-.031,1.353,4.477),(-.011,1.357,4.474),(0,1.354,4.472),(.011,1.357,4.474),(.031,1.353,4.477)],.0023,'Lip',head)
# scalp surface excludes face
verts=[];faces=[]
for i in range(13):
    theta=(i/12)*1.58
    for j in range(40):
        phi=j*math.tau/40
        # taller hairline on front, natural around ears
        t=theta*(.72 if math.sin(phi)>0 else 1.0)
        verts.append(v((.118*math.sin(t)*math.cos(phi),1.452+.151*math.cos(t),4.60-.11*math.sin(t)*math.sin(phi))))
for i in range(12):
    for j in range(40):a=i*40+j;b=i*40+(j+1)%40;faces.append((a,b,b+40,a+40))
mesh=bpy.data.meshes.new('ScalpMesh');mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new('HairCap',mesh);bpy.context.collection.objects.link(o);finish(o,'HairCap','Hair',head)
for i in range(12):
    x=-.09+i*.015
    curve('SweptHair',[(x,1.556,4.535),(x+.02,1.594,4.57),(x+.02,1.575,4.67)],.008,'Hair',head)
# phoropter independent articulated assembly
group=pivot('Phoropter',(0,1.445,4.23))
box('InstrumentBase',(-.78,.08,4.65),(.65,.13,.7),'Porcelain',.07)
cyl('InstrumentColumn',(-.78,1.02,4.65),.055,1.87,'Chrome')
cyl('ColumnSleeve',(-.78,.55,4.65),.09,.9,'Porcelain')
link('SuspensionArm',(-.78,1.88,4.65),(0,1.88,4.23),.04,'Chrome')
link('SuspensionDrop',(0,1.88,4.23),(0,1.64,4.23),.028,'Chrome')
box('Bridge',(0,1.62,4.23),(.42,.064,.065),'Porcelain')
box('ForeheadRest',(0,1.555,4.40),(.14,.045,.045),'Rubber')
for eye,x in [('OD',-.032),('OS',.032)]:
    # patient 64mm IPD; faces of housings offset outwards, small clear central apertures
    pod=pivot('Pod_'+eye,(x,1.445,4.23),group)
    xx=x+(-.067 if x<0 else .067)
    shell=ell('Shell_'+eye,(xx,1.472,4.24),(.115,.135,.043),'Porcelain',pod)
    # A real bore through the housing: rings alone would leave an opaque eye port.
    bpy.context.view_layer.update()
    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.0235,depth=.22,location=v((x,1.445,4.24)),rotation=(math.pi/2,0,0))
    cutter=bpy.context.object
    bpy.context.view_layer.objects.active=shell
    boolean=shell.modifiers.new('Clear optical aperture','BOOLEAN');boolean.operation='DIFFERENCE';boolean.object=cutter
    bpy.ops.object.modifier_apply(modifier=boolean.name);bpy.data.objects.remove(cutter,do_unlink=True)
    ring('LensHousing_'+eye,(x,1.445,4.192),.027,.009,'Graphite',pod)
    ring('ApertureBezel_'+eye,(x,1.445,4.187),.021,.0025,'Chrome',pod)
    # no opaque glass in aperture; allows actual eye viewing
    axis=pivot('Axis_'+eye,(x,1.445,4.18),pod)
    ring('AxisRing_'+eye,(x,1.445,4.18),.037,.003,'Graphite',axis)
    for deg in range(0,180,10):
        a=math.radians(deg)
        link('AxisTick',(x+math.cos(a)*.033,1.445+math.sin(a)*.033,4.175),(x+math.cos(a)*.039,1.445+math.sin(a)*.039,4.175),.0008,'White',axis)
    sph=pivot('Sph_'+eye,(xx+(-.058 if x<0 else .058),1.49,4.185),pod)
    cx=xx+(-.058 if x<0 else .058)
    cyl('SphWheel',(cx,1.49,4.185),.049,.028,'Graphite','Z',sph)
    for a in range(28):
        an=a*math.tau/28;cyl('SphGrip',(cx+math.cos(an)*.048,1.49+math.sin(an)*.048,4.181),.0025,.028,'Chrome','Z',sph,12)
    for typ,yy in [('Cyl',1.386),('Aux',1.558)]:
        kn=pivot(typ+'_'+eye,(xx,yy,4.184),pod)
        cyl(typ+'Knob',(xx,yy,4.184),.022,.027,'Graphite','Z',kn)
        box(typ+'Indicator',(xx,yy+.013,4.167),(.003,.012,.003),'Brass',.001,kn)
    occ=pivot('Occluder_'+eye,(x+(.034 if x<0 else -.034),1.475,4.174),pod)
    cyl('OcclusionPlate',(x,1.445,4.174),.020,.002,'Graphite','Z',occ)
    # Occluder starts swung clear; runtime animates pivot.
    # Clear/closed poses are applied in Unity around the exported anatomical pivot.
    ph=pivot('Pinhole_'+eye,(x,1.445,4.17),pod)
    ring('PinholeDisc',(x,1.445,4.17),.0105,.009,'Graphite',ph)
    ph.hide_render=True # runtime toggles descendants
    jcc=pivot('JCC_'+eye,(x,1.503,4.135),pod)
    flip=pivot('JCCFlip_'+eye,(x,1.445,4.135),jcc)
    ring('CrossCylinderRim',(x,1.445,4.135),.024,.0025,'Chrome',flip)
    link('JCCHandle',(x,1.47,4.135),(x,1.51,4.135),.002,'Graphite',jcc)
    for i in range(4):
        an=i*math.pi/2;ell('JCCDot',(x+math.cos(an)*.024,1.445+math.sin(an)*.024,4.13),(.0025,.0025,.001),'Red' if i%2==0 else 'White',flip)
    # Store the deployed pose; Unity swings this rigid assembly out of the optical path.
pd=pivot('PDWheel',(0,1.63,4.18),group);cyl('PDKnob',(0,1.63,4.18),.020,.035,'Chrome','Z',pd)
level=pivot('LevelKnob',(0,1.59,4.18),group);cyl('LevelDial',(0,1.59,4.18),.013,.025,'Graphite','Z',level)
lock=pivot('LockLever',(0,1.73,4.23),group);link('LockHandle',(0,1.73,4.23),(.07,1.73,4.23),.010,'Graphite',lock)
box('BubbleLevel',(0,1.66,4.185),(.075,.012,.012),'Lens',.003)
ell('Bubble',(0,1.66,4.174),(.006,.004,.003),'White')
# dynamic tools are separate roots with their native local centre
group=pivot('Retinoscope',(0,0,0))
cyl('RetHandle',(0,0,0),.017,.16,'Graphite')
for y in [-.06,-.045,-.03,-.015]:ring('RetGrip',(0,y,0),.0175,.0015,'Rubber')
box('RetHead',(0,.115,0),(.056,.078,.035),'Graphite',.012)
cyl('RetLens',(0,.12,-.021),.018,.008,'Chrome','Z')
cyl('RetMirror',(0,.12,-.027),.012,.004,'Lens','Z')
box('RetStreak',(0,.12,-.030),(.0025,.023,.002),'White',.0001)
cyl('RetSleeve',(0,.065,0),.023,.032,'Chrome')
group=pivot('PDRuler',(0,0,0));box('Ruler',(0,0,0),(.18,.022,.003),'Porcelain',.002)
for i in range(36):box('RulerTick',(-.087+i*.005, .005 if i%2 else .002,-.002),(.0007,.007 if i%2 else .012,.001),'Black',0)
group=pivot('CleaningCloth',(0,0,0));box('FoldedCloth',(0,0,0),(.105,.012,.10),'Cuff',.012)
for i in range(8):box('ClothFold',(-.043+i*.012,.008,0),(.001,.001,.085),'White',0)
group=pivot('Remote',(0,0,0));box('RemoteBody',(0,0,0),(.047,.13,.018),'Graphite',.012)
for y in [.043,.02,-.003,-.026]:
    for x in [-.012,.012]:ell('RemoteButton',(x,y,-.013),(.006,.006,.003),'White')
group=pivot('Clipboard',(0,0,0));box('Board',(0,0,0),(.24,.33,.012),'Oak',.006)
box('RecordSheet',(0,0,-.008),(.22,.30,.001),'Paper',0)
box('Clip',(0,.155,-.015),(.09,.03,.015),'Chrome',.004)
for y in [.105,.07,.035,0,-.035,-.07,-.105]:box('PaperLine',(0,y,-.01),(.19,.0007,.001),'Sage',0)
group=pivot('Pen',(0,0,0));cyl('PenBody',(0,0,0),.004,.14,'Teal');cyl('PenTip',(0,-.075,0),.0015,.012,'Chrome')
group=pivot('Sanitizer',(0,0,0));box('Bottle',(0,0,0),(.065,.15,.055),'Porcelain',.015)
link('Pump',(0,.085,0),(0,.11,0),.005,'Chrome');box('PumpHead',(.015,.115,0),(.05,.01,.015),'Chrome',.004)
group=pivot('LightSwitch',(3.78,1.28,3.2));box('SwitchPlate',(3.78,1.28,3.2),(.025,.14,.09),'Porcelain',.01)
lever=pivot('SwitchLever',(3.756,1.28,3.2),group);box('SwitchRocker',(3.756,1.28,3.2),(.018,.073,.04),'White',.006,lever)
# first person arms: separated source geometry, local positions relative to camera
group=pivot('ClinicianHands',(0,0,0))
for side,sgn in [('R',1),('L',-1)]:
    wrist=pivot('Hand_'+side,(sgn*.22,-.30,.44),group)
    ell('Palm_'+side,(sgn*.22,-.30,.44),(.043,.024,.064),'Glove',wrist)
    link('Sleeve_'+side,(sgn*.30,-.48,.20),(sgn*.23,-.32,.40),.054,'Cuff',wrist)
    ring('CuffSeam_'+side,(sgn*.23,-.32,.40),.045,.005,'Porcelain',wrist)
    for f in range(4):
        xx=sgn*.22+(f-1.5)*.020;zz=.49
        finger=pivot('Finger_'+side+str(f),(xx,-.30,zz),wrist)
        ell('FingerMesh',(xx,-.30,zz+.029),(.009,.010,.036),'Glove',finger)
        ell('Knuckle',(xx,-.30,zz),(.010,.011,.012),'Glove',finger)
    ell('Thumb_'+side,(sgn*.17,-.30,.45),(.015,.015,.04),'Glove',wrist)

# Scene assembly in Unity places tools on their rack. Save all editable parts here.
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Blender','RefractionRoom.blend'))
roots=[o for o in bpy.data.objects if o.parent is None]
def descendants(o):
    r=[o]
    for c in o.children:r.extend(descendants(c))
    return r
manifest=[]
for root in roots:
    bpy.ops.object.select_all(action='DESELECT')
    for o in descendants(root):o.select_set(True)
    target=os.path.join(OUT,root.name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=target,use_selection=True,object_types={'EMPTY','MESH'},add_leaf_bones=False,
        axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
        bake_anim=False,path_mode='AUTO',mesh_smooth_type='FACE',use_mesh_modifiers=True)
    manifest.append({'asset':root.name,'objects':len(descendants(root)),'source':'RefractionRoom.blend'})
    # A lossless metres-based mesh/hierarchy interchange avoids FBX's nested-empty
    # bake-space-transform ambiguity. Vertices are baked in Blender, never recreated
    # as Unity primitives. Identity rest rotations make clinical pivots explicit.
    hierarchy=descendants(root);entries=[]
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
    with open(os.path.join(OUT,root.name+'.clinicmesh.json'),'w') as f:json.dump({'name':root.name,'nodes':entries},f,separators=(',',':'))
with open(os.path.join(ROOT,'Reports','blender-assets.json'),'w',encoding='utf-8') as f:json.dump(manifest,f,ensure_ascii=False,indent=2)
print('CLINIC_ASSETS_READY',len(roots))
