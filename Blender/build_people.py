"""Patient and first-person gloved hands from MPFB 2 (MakeHuman).
Run after build_clinic.py:  blender -b -P Blender/build_people.py
Needs the MPFB extension and the makehuman_system_assets (CC0) pack installed in Blender.
MPFB itself is GPL and is only used here as a tool; the generated meshes and the CC0 textures
are what ship. The script writes Patient.clinicmesh.json and adds the glove meshes to the
ClinicianHands package written by build_clinic.py.

Skinned export: each mesh carries up to four weights per vertex onto named clinical pivots
(HeadPivot, Jaw, Lid_OD/OS, Hand_R/L, Finger_R0..3/L0..3) so Unity drives head sway, blinking,
speech and finger grips with smooth deformation. The bind pose is the exported (seated) pose.
"""
import bpy, os, json, math, shutil
from mathutils import Vector, Matrix
from bl_ext.user_default.mpfb.services import HumanService, TargetService, LocationService

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'UnityProject', 'Assets', 'Models')
TEX = os.path.join(OUT, 'Textures'); os.makedirs(TEX, exist_ok=True)
for f in os.listdir(TEX):
    if f.endswith('.png'): os.remove(os.path.join(TEX, f))
LIB = LocationService.get_user_data()
def asset(p): return os.path.join(LIB, p)

# clinical targets (Unity metres): pupil plane 6 m from the chart, mean case PD 63.5 mm
PUPIL_Z, EYE_Y, IPD = 4.4752, 1.445, .0635
PATIENT_ROOT = Vector((0, 0, 4.55))

for o in list(bpy.data.objects): bpy.data.objects.remove(o)
m = TargetService.get_default_macro_info_dict()
m['race'] = {'african': 0.0, 'asian': .85, 'caucasian': .15}
m['gender'] = .88; m['age'] = .5; m['muscle'] = .55; m['weight'] = .52; m['height'] = .62
body = HumanService.create_human(scale=0.1, macro_detail_dict=m)
rig = HumanService.add_builtin_rig(body, 'default')
HumanService.set_character_skin(asset('skins/young_asian_male/young_asian_male.mhmat'), body, skin_type='GAMEENGINE')
PARTS = {}
for typ, p in [('Eyes', 'eyes/high-poly/high-poly.mhclo'), ('Eyebrows', 'eyebrows/eyebrow001/eyebrow001.mhclo'),
               ('Eyelashes', 'eyelashes/eyelashes01/eyelashes01.mhclo'), ('Hair', 'hair/short02/short02.mhclo'),
               ('Clothes', 'clothes/male_casualsuit06/male_casualsuit06.mhclo'), ('Clothes', 'clothes/shoes02/shoes02.mhclo')]:
    o = HumanService.add_mhclo_asset(asset(p), body, asset_type=typ, subdiv_levels=0, material_type='GAMEENGINE')
    PARTS[os.path.basename(p).split('.')[0]] = o
for o in [body] + list(PARTS.values()):
    for md in o.modifiers:
        if md.type == 'SUBSURF': md.show_viewport = md.show_render = False
assert rig.matrix_world == Matrix.Identity(4)
dg = bpy.context.evaluated_depsgraph_get
def upd(): bpy.context.view_layer.update()
PB = rig.pose.bones
def head(n): upd(); return (rig.matrix_world @ PB[n].head).copy()
def tail(n): upd(); return (rig.matrix_world @ PB[n].tail).copy()

# ---- alignment: native MPFB space -> Blender scene space (Unity x, z, y) ----
eyes_obj = PARTS['high-poly']
ev = eyes_obj.evaluated_get(dg()); em = ev.to_mesh()
def ball(sgn):
    pts = [eyes_obj.matrix_world @ v.co for v in em.vertices if v.co.x * sgn > 0]
    lo = Vector([min(p[k] for p in pts) for k in range(3)]); hi = Vector([max(p[k] for p in pts) for k in range(3)])
    c = (lo + hi) / 2; r = (hi.z - lo.z) / 2
    return Vector((c.x, lo.y + r, c.z)), lo.y      # globe centre one vertical radius behind the apex
(eR, front), (eL, _) = ball(-1), ball(1)
ev.to_mesh_clear()
S = IPD / (eL - eR).length
R_FRONT = (eR.y - front) * S                      # eyeball centre to corneal apex
mid = (eR + eL) / 2
T = Vector((0, PUPIL_Z + R_FRONT, EYE_Y)) - mid * S
def W(p): return Vector(p) * S + T                  # native -> scene (Blender axes)
def N(p): return (Vector(p) - T) / S                # scene -> native
def U(p): p = Vector(p); return Vector((p.x, p.z, p.y))   # scene (Blender) <-> Unity axes swap

# ---- posing helpers ----
def aim(name, d):
    upd(); pb = PB[name]; M = pb.matrix.copy()
    cur = (M.to_3x3() @ Vector((0, 1, 0))).normalized()
    q = cur.rotation_difference(Vector(d).normalized())
    h = M.translation
    pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ M
    upd()
def two_bone(a, c, L1, L2, pole):
    d = c - a; dist = min(d.length, L1 + L2 - 1e-4); dn = d.normalized()
    ca = (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist); sa = math.sqrt(max(0, 1 - ca * ca))
    p = (pole - dn * pole.dot(dn)).normalized()
    return a + dn * L1 * ca + p * L1 * sa
def chain(bones_a, bones_b, target, pole):
    a = head(bones_a[0])
    L1 = sum(PB[b].length for b in bones_a); L2 = sum(PB[b].length for b in bones_b)
    k = two_bone(a, target, L1, L2, pole)
    for b in bones_a: aim(b, k - a)
    k2 = head(bones_b[0])
    for b in bones_b: aim(b, target - k2)
def curl(side, ang, thumb=0.0):
    for f in range(1, 6):
        for j in range(1, 4):
            pb = PB['finger%d-%d.%s' % (f, j, side)]; pb.rotation_mode = 'XYZ'
            pb.rotation_euler = (math.radians(thumb if f == 1 else ang * (1.15 if j == 1 else 1)), 0, 0)
    upd()

# ---- export helpers ----
def evaluated(o):
    upd(); e = o.evaluated_get(dg())
    return bpy.data.meshes.new_from_object(e, preserve_all_data_layers=True, depsgraph=dg())
def texture_of(o):
    for n in o.active_material.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and 'normal' not in n.image.filepath.lower():
            src = bpy.path.abspath(n.image.filepath); dst = os.path.join(TEX, os.path.basename(src))
            shutil.copyfile(src, dst); return 'Textures/' + os.path.basename(src)
    return ''
def node(name, parent, pos, nodes):
    nodes.append({'name': name, 'parent': parent, 'position': list(pos), 'vertices': [], 'triangles': [], 'uv': [],
                  'normals': [], 'material': '', 'color': [1, 1, 1], 'metallic': 0.0, 'roughness': .5, 'texture': '',
                  'cutout': 0.0, 'bones': [], 'boneIndex': [], 'boneWeight': []})
    return len(nodes) - 1
def world_of(nodes, i):
    p = Vector((0, 0, 0))
    while i >= 0: p += Vector(nodes[i]['position']); i = nodes[i]['parent']
    return p
def fill(nodes, idx, me, xf, faces, weights=None, pivots=None, mat=None):
    """me: Blender mesh; xf(co)->Unity position; faces: polygon indices; weights(v)->{pivot:w}."""
    entry = nodes[idx]; origin = world_of(nodes, idx); me.calc_loop_triangles()
    keep = set(faces); uvl = me.uv_layers.active.data if me.uv_layers.active else None
    remap = {}; V = []; UV = []; NR = []; TRI = []; BI = []; BW = []
    used = []
    if pivots: used = sorted(set(pivots))
    for tri in me.loop_triangles:
        if tri.polygon_index not in keep: continue
        ids = []
        for li in tri.loops:
            vi = me.loops[li].vertex_index; uv = tuple(round(c, 5) for c in uvl[li].uv) if uvl else (0, 0)
            key = (vi, uv)
            if key not in remap:
                remap[key] = len(V); p = xf(me.vertices[vi].co) - origin
                V.append(p); UV.append(uv)
                n = xf(me.vertices[vi].co + me.vertices[vi].normal * .001) - xf(me.vertices[vi].co)
                NR.append(n.normalized())
                if weights:
                    w = sorted(weights(vi).items(), key=lambda kv: -kv[1])[:4]; tot = sum(x for _, x in w) or 1
                    w = [(used.index(k), x / tot) for k, x in w] + [(0, 0.0)] * (4 - len(w))
                    for k, x in w: BI.append(k); BW.append(round(x, 4))
            ids.append(remap[key])
        TRI.extend([ids[0], ids[2], ids[1]] if mat is None or mat.get('flip', True) else ids)
    entry['vertices'] = [round(c, 6) for p in V for c in p]; entry['uv'] = [c for uv in UV for c in uv]
    entry['normals'] = [round(c, 4) for n in NR for c in n]; entry['triangles'] = TRI
    if weights: entry['bones'] = used; entry['boneIndex'] = BI; entry['boneWeight'] = BW
    return len(V)
def bone_groups(o):
    return {g.index: g.name for g in o.vertex_groups if g.name in rig.data.bones}
def ancestors(b):
    out = []
    while b: out.append(b.name); b = b.parent
    return out

# ======================= First-person gloved hands (rest A-pose, relaxed fingers) =======================
curl('R', 14, 6)
hm = evaluated(body); groups = bone_groups(body)
HAND = {'wrist.R': 'Hand', 'finger1-1.R': 'Hand', 'finger1-2.R': 'Hand', 'finger1-3.R': 'Hand'}
for f in range(1, 5): HAND['metacarpal%d.R' % f] = 'Hand'
for i, f in enumerate((2, 3, 4, 5)):
    for j in (1, 2, 3): HAND['finger%d-%d.R' % (f, j)] = 'F%d' % i
def hand_w(vi):
    w = {}
    for g in hm.vertices[vi].groups:
        n = groups.get(g.group)
        if n in HAND: w[HAND[n]] = w.get(HAND[n], 0) + g.weight
    return w
hand_faces = [p.index for p in hm.polygons if all(sum(hand_w(v).values()) > .55 for v in p.vertices)]
Wr = head('wrist.R'); K = head('finger3-1.R')
F = (K - Wr).normalized(); side = (head('finger2-1.R') - head('finger5-1.R')); side = (side - F * side.dot(F)).normalized()
def basis_unity(f, s):  # matrix whose columns are f, s, f x s in Unity component order
    f, s = U(f), U(s); return Matrix((f, s, f.cross(s))).transposed()
# right hand target: fingers forward (+Z), thumb side inward and slightly up, palm down
tf = Vector((0, -.12, 1)).normalized(); ts = Vector((-1, .28, 0)); ts = (ts - tf * ts.dot(tf)).normalized()
Rh = Matrix((tf, ts, tf.cross(ts))).transposed() @ basis_unity(F, side).inverted()
HS = .072 / (K - Wr).length
WR_POS = Vector((.223, -.305, .398))
def hand_xf(co, mirror=False):
    p = WR_POS + Rh @ (U(co - Wr) * HS)
    return Vector((-p.x, p.y, p.z)) if mirror else p
with open(os.path.join(OUT, 'ClinicianHands.clinicmesh.json')) as f: pack = json.load(f)
nodes = pack['nodes']
drop = {i for i, n in enumerate(nodes) if n['name'].split('_')[0] in ('Palm', 'Thumb', 'FingerMesh', 'Glove') or n['name'].startswith('Finger_')}
def descends(i):
    while i >= 0:
        if i in drop: return True
        i = nodes[i]['parent']
    return False
keep = [i for i in range(len(nodes)) if not descends(i)]
newi = {o: k for k, o in enumerate(keep)}
nodes = [dict(nodes[i], parent=newi.get(nodes[i]['parent'], -1)) for i in keep]
for n in nodes:
    for k, d in (('normals', []), ('texture', ''), ('cutout', 0.0), ('bones', []), ('boneIndex', []), ('boneWeight', [])): n.setdefault(k, d)
for sd, mirror in (('R', False), ('L', True)):
    hi = next(i for i, n in enumerate(nodes) if n['name'] == 'Hand_' + sd)
    hw = world_of(nodes, hi)
    piv = {'Hand': hi}
    for i, f in enumerate((2, 3, 4, 5)):
        kp = hand_xf(head('finger%d-1.R' % f), mirror)
        piv['F%d' % i] = node('Finger_%s%d' % (sd, i), hi, kp - hw, nodes)
    gi = node('Glove_' + sd, 0, (0, 0, 0), nodes)
    nodes[gi].update(material='Glove', color=[.52, .66, .88], roughness=.35)
    fill(nodes, gi, hm, lambda co, m=mirror: hand_xf(co, m), hand_faces,
         weights=lambda vi: {piv[k]: x for k, x in hand_w(vi).items()}, pivots=list(piv.values()),
         mat={'flip': not mirror})
pack['nodes'] = nodes
with open(os.path.join(OUT, 'ClinicianHands.clinicmesh.json'), 'w') as f: json.dump(pack, f, separators=(',', ':'))
bpy.data.meshes.remove(hm)
curl('R', 0, 0)

# ======================= Seated patient =======================
# legs: thighs along the seat, shins down to the footrest; arms resting on the thighs
for sd, sx in (('R', -1), ('L', 1)):
    chain(['upperleg01.' + sd, 'upperleg02.' + sd], ['lowerleg01.' + sd, 'lowerleg02.' + sd],
          N(Vector((sx * .112, 4.13, .31))), Vector((0, -1, .25)))
    aim('foot.' + sd, Vector((sx * .05, -1, -.35)))
    chain(['upperarm01.' + sd, 'upperarm02.' + sd], ['lowerarm01.' + sd, 'lowerarm02.' + sd],
          N(Vector((sx * .150, 4.47, .885))), Vector((sx * .5, 1, -.2)))
    aim('wrist.' + sd, Vector((-sx * .25, -1, -.32)))
    curl(sd, 22, 10)
HP = W(head('head')); JAW = W(head('jaw'))
EYE = {'OD': W(eR), 'OS': W(eL)}
nodes = []
root = node('Patient', -1, U(PATIENT_ROOT), nodes)
hp = node('HeadPivot', root, U(HP) - U(PATIENT_ROOT), nodes)
jaw = node('Jaw', hp, U(JAW) - U(HP), nodes)
lid = {}; rigs = {}
for e in ('OD', 'OS'):
    lid[e] = node('Lid_' + e, hp, U(EYE[e]) - U(HP), nodes)
    rigs[e] = node('EyeRig_' + e, hp, U(EYE[e]) - U(HP), nodes)
def pivot_for(bname):
    chain_ = ancestors(rig.data.bones[bname])
    if bname == 'orbicularis03.R': return lid['OD']
    if bname == 'orbicularis03.L': return lid['OS']
    if 'jaw' in chain_: return jaw
    if 'head' in chain_: return hp
    return root
PIV = {b.name: pivot_for(b.name) for b in rig.data.bones}
def xf_body(co): return U(W(co))
SKINNED = [('Body', body, 'MH_Skin', .62, 0), ('Shirt', PARTS['male_casualsuit06'], 'MH_Clothes', .85, 0),
           ('Shoes', PARTS['shoes02'], 'MH_Shoes', .45, 0), ('Hair', PARTS['short02'], 'MH_Hair', .55, .45),
           ('Eyebrows', PARTS['eyebrow001'], 'MH_Brows', .8, .4), ('Eyelashes', PARTS['eyelashes01'], 'MH_Lashes', .8, .4)]
for name, o, matname, rough, cut in SKINNED:
    me = evaluated(o); gm = bone_groups(o)
    def wfun(vi, me=me, gm=gm):
        w = {}
        for g in me.vertices[vi].groups:
            b = gm.get(g.group)
            if b and g.weight > 0: w[PIV[b]] = w.get(PIV[b], 0) + g.weight
        return w or {root: 1.0}
    allp = sorted({p for vi in range(len(me.vertices)) for p in wfun(vi)})
    i = node(name, root, (0, 0, 0), nodes)
    nodes[i].update(material=matname, texture=texture_of(o), roughness=rough, cutout=cut)
    n = fill(nodes, i, me, xf_body, [p.index for p in me.polygons], weights=wfun, pivots=allp)
    print('MESH', name, n, 'verts', len(allp), 'pivots')
    bpy.data.meshes.remove(me)
# Eyes: the project's own procedural globe (sclera, iris, limbus, pupil, catchlight) sized to the
# MakeHuman socket. Rigid parts turn with Eye_ for fixation; the pupil disc dilates and hosts the reflex.
def raw(i, verts, tris, normals, outward, **kw):
    """Unity-space mesh; every triangle is wound so its front faces along outward(centroid)."""
    T2 = []
    for a, b, c in tris:
        A, B, C = verts[a], verts[b], verts[c]
        if (B - A).cross(C - A).dot(outward((A + B + C) / 3)) < 0: b, c = c, b
        T2 += [a, b, c]
    nodes[i].update(vertices=[round(x, 7) for v in verts for x in v], uv=[0.0] * (2 * len(verts)),
                    normals=[round(x, 5) for n in normals for x in n], triangles=T2, **kw)
def cap(r, theta_max, rings=24, segs=48, back=0.0):
    """Spherical cap around the -Z pole (toward the examiner); full sphere when theta_max >= pi."""
    vs = [Vector((0, 0, -r))]; ts = []
    for i in range(1, rings + 1):
        th = theta_max * i / rings
        for j in range(segs):
            ph = j * math.tau / segs
            vs.append(Vector((r * math.sin(th) * math.cos(ph), r * math.sin(th) * math.sin(ph), -r * math.cos(th))))
    for j in range(segs): ts.append((0, 1 + j, 1 + (j + 1) % segs))
    for i in range(rings - 1):
        for j in range(segs):
            a = 1 + i * segs + j; b = 1 + i * segs + (j + 1) % segs
            ts += [(a, b, b + segs), (a, b + segs, a + segs)]
    return vs, ts
def disc(r, z, segs=32):
    vs = [Vector((0, 0, z))] + [Vector((math.cos(k * math.tau / segs) * r, math.sin(k * math.tau / segs) * r, z)) for k in range(segs)]
    return vs, [(0, 1 + k, 1 + (k + 1) % segs) for k in range(segs)]
R = R_FRONT
for e, sgn in (('OD', -1), ('OS', 1)):
    c = U(EYE[e]); ei = node('Eye_' + e, rigs[e], (0, 0, 0), nodes)
    vs, ts = cap(R, math.pi * .999, 28, 48)
    si = node('Sclera', ei, (0, 0, 0), nodes)
    raw(si, [Vector((v.x * 1.04, v.y, v.z)) for v in vs], ts, [v.normalized() for v in vs], lambda p: p,
        material='Sclera', color=[.95, .93, .90], roughness=.08)
    # iris: a shallow cap just proud of the sclera, then the dark limbal ring around it
    ir = .0061; th = math.asin(ir / (R + .0003))
    vs, ts = cap(R + .0003, th, 10, 48)
    ii = node('Iris', ei, (0, 0, 0), nodes)
    raw(ii, vs, ts, [v.normalized() for v in vs], lambda p: p, material='Iris', color=[.20, .13, .085], roughness=.08)
    vs = []; ts = []
    for k in range(48):
        ph = k * math.tau / 48
        for rr in (ir - .0004, ir + .0001):
            t2 = math.asin(rr / (R + .00035)); vs.append(Vector(((R + .00035) * math.sin(t2) * math.cos(ph), (R + .00035) * math.sin(t2) * math.sin(ph), -(R + .00035) * math.cos(t2))))
    for k in range(48):
        a = 2 * k; b = 2 * ((k + 1) % 48); ts += [(a, b, b + 1), (a, b + 1, a + 1)]
    li = node('Limbus', ei, (0, 0, 0), nodes)
    raw(li, vs, ts, [v.normalized() for v in vs], lambda p: p, material='Limbus', color=[.12, .09, .07], roughness=.1)
    # pupil: flat disc just in front of the iris cap (radius 2.1 mm, scaled by the runtime for dilation)
    front = -(R + .0003) - .0002
    pi = node('Pupil_' + e, ei, (0, 0, front), nodes)
    vs, ts = disc(.0021, 0)
    raw(pi, vs, ts, [Vector((0, 0, -1))] * len(vs), lambda p: Vector((0, 0, -1)), material='Pupil', color=[.006, .006, .008], roughness=.05)
    # catchlight stays on the rig so it does not swim with eye rotation
    cl = node('Catchlight', rigs[e], (-.0021, .0022, front - .0003), nodes)
    vs, ts = disc(.0011, 0, 16)
    raw(cl, vs, ts, [Vector((0, 0, -1))] * len(vs), lambda p: Vector((0, 0, -1)), material='White', color=[.97, .96, .92], roughness=.4)
    print('EYE', e, tuple(round(x, 4) for x in c), 'apex z', round(c.z - R, 4), 'R', round(R, 4))
with open(os.path.join(OUT, 'Patient.clinicmesh.json'), 'w') as f: json.dump({'name': 'Patient', 'nodes': nodes}, f, separators=(',', ':'))
# diagnostics for the chair fit
bm = evaluated(body)
pts = [xf_body(v.co) for v in bm.vertices]
print('SEAT_CONTACT_Y', round(min(p.y for p in pts if abs(p.x) < .2 and 4.55 < p.z < 4.85 and p.y > .45), 4),
      'FEET_Y', round(min(p.y for p in pts), 4), 'BACK_Z', round(max(p.z for p in pts if p.y > .9), 4),
      'NOSE_Z', round(min(p.z for p in pts if p.y > 1.38), 4), 'HEAD_TOP', round(max(p.y for p in pts), 4))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, 'Blender', 'People.blend'))
print('PEOPLE_READY')
for b in ('upperleg01.R', 'lowerleg01.R', 'foot.R', 'wrist.R', 'head', 'spine05', 'root'):
    print('JOINT', b, tuple(round(x, 3) for x in U(W(head(b)))))
print('SCALE', round(S, 4), 'R_FRONT', round(R_FRONT, 4))
