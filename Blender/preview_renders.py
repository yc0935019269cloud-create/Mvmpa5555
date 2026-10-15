"""Quick look renders of RefractionRoom.blend for model review.
blender --background Blender/RefractionRoom.blend --python Blender/preview_renders.py -- <out_dir> [view ...]
Camera positions are Unity metres (X right, Y up, Z toward the patient)."""
import bpy, sys, os, math
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
out=args[0] if args else os.path.join(os.path.dirname(bpy.data.filepath),'..','Reports','previews')
os.makedirs(out,exist_ok=True)
only=set(args[1:])
def v(p):return Vector((p[0],p[2],p[1]))
scene=bpy.context.scene
for engine in ('BLENDER_EEVEE','BLENDER_EEVEE_NEXT','BLENDER_WORKBENCH'):
    try:scene.render.engine=engine;break
    except Exception:pass
scene.render.resolution_x=1280;scene.render.resolution_y=800;scene.render.film_transparent=False
try:scene.view_settings.view_transform='AgX'
except Exception:pass
glass=bpy.data.materials.get('Glass')
if glass:
    p=glass.node_tree.nodes.get('Principled BSDF');p.inputs['Alpha'].default_value=.12
    try:glass.surface_render_method='BLENDED'
    except Exception:pass
world=bpy.data.worlds.new('Studio') if not scene.world else scene.world;scene.world=world;world.use_nodes=True
bg=world.node_tree.nodes.get('Background');bg.inputs[0].default_value=(.55,.58,.56,1);bg.inputs[1].default_value=.25
def light(name,kind,pos,target,energy,size=1):
    d=bpy.data.lights.new(name,kind);d.energy=energy
    if kind=='AREA':d.size=size
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=v(pos)
    o.rotation_euler=(v(target)-v(pos)).to_track_quat('-Z','Y').to_euler();return o
light('Key','AREA',(-1.2,2.6,2.6),(0,1.3,4.4),160,2.5)
light('Fill','AREA',(1.6,2.0,3.2),(0,1.4,4.4),55,2)
light('Rim','AREA',(0,2.8,5.6),(0,1.4,4.5),45,2)
cam_data=bpy.data.cameras.new('Preview');cam=bpy.data.objects.new('Preview',cam_data);scene.collection.objects.link(cam);scene.camera=cam
TOOLS=['Retinoscope','PDRuler','CleaningCloth','Remote','Clipboard','Pen','Sanitizer','ClinicianHands']
def hide(names,state):
    keep=[n[1:] for n in names if n.startswith('+')]
    for o in bpy.data.objects:
        top=o
        while top.parent:top=top.parent
        if (keep and top.name not in keep) or top.name in names or (not keep and top.name in TOOLS):o.hide_render=state
views={
 'examiner':((0,1.46,3.92),(0,1.43,4.42),50,[]),
 'phoropter34':((-.55,1.62,3.95),(0,1.42,4.42),40,['Architecture']),
 'phoropter_side':((.75,1.50,4.40),(0,1.43,4.43),38,['Architecture','PhoropterStand']),
 'face':((.16,1.47,4.12),(0,1.44,4.50),30,['Phoropter','PhoropterStand','Architecture']),
 'face_front':((0,1.45,4.05),(0,1.43,4.50),28,['Phoropter','PhoropterStand','Architecture']),
 'patient':((1.25,1.35,3.2),(0,1.0,4.55),42,[]),
 'eyes_through':((0,1.445,4.05),(0,1.445,4.47),12,['Architecture']),
 'hands':((0,0,0),(0,-.25,.6),60,['+ClinicianHands']),
 'ruler':((0,.03,-.16),(0,0,0),30,['+PDRuler']),
}
for name,(pos,target,lens,hidden) in views.items():
    if only and name not in only:continue
    hide(hidden,True)
    cam.location=v(pos);cam.rotation_euler=(v(target)-v(pos)).to_track_quat('-Z','Y').to_euler()
    cam_data.angle=math.radians(lens);cam_data.clip_start=.005
    scene.render.filepath=os.path.join(out,name+'.png');bpy.ops.render.render(write_still=True)
    hide(hidden,False)
print('PREVIEWS_DONE',out)
