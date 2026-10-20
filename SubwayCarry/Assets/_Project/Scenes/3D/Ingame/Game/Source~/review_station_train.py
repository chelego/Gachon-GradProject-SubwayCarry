"""Asset-local views from the saved Blender source; no Unity or desktop capture."""
import bpy, os, sys, math
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
PROJECT=os.path.abspath(os.path.join(ROOT,'../../../..'))
REVIEW=os.path.join(PROJECT,'Temp','ThreeDRebuild','KitReview'); os.makedirs(REVIEW,exist_ok=True)
s=bpy.context.scene; s.render.engine='CYCLES'; s.cycles.samples=8; s.cycles.use_denoising=True
s.render.resolution_x=320; s.render.resolution_y=260; s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG'
env=s.world.node_tree.nodes.new('ShaderNodeTexEnvironment'); env.image=bpy.data.images.load(os.path.join(ROOT,'Art','Textures','Metro','IndoorReflection.hdr'),check_existing=True)
s.world.node_tree.links.new(env.outputs['Color'],s.world.node_tree.nodes['Background'].inputs['Color'])
s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=1
roots=[o for o in bpy.data.objects if o.type=='EMPTY' and o.get('unit_metres')==1 and o.parent is None]
for r in roots: r.location=(0,0,0)
bpy.context.view_layer.update()
clay=bpy.data.materials.new('Review clay'); clay.use_nodes=True
clay.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.48,.48,.48,1)
for p,power,size in [((6,-7,10),2300,7),((-6,1,6),1600,6),((1,8,9),2000,5)]:
    data=bpy.data.lights.new('Review softbox','AREA'); data.energy=power; data.shape='DISK'; data.size=size
    o=bpy.data.objects.new('Review softbox',data); bpy.context.collection.objects.link(o); o.location=p
    o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Review camera'); cam=bpy.data.objects.new('Review camera',data); bpy.context.collection.objects.link(cam); s.camera=cam
for r in roots:
    if '--only' in sys.argv and r.name not in sys.argv[sys.argv.index('--only')+1:]: continue
    for other in roots:
        for o in other.children: o.hide_render=other!=r
    corners=[o.matrix_world@Vector(c) for o in r.children if o.type=='MESH' for c in o.bound_box]
    mn=Vector(tuple(min(p[i] for p in corners) for i in range(3))); mx=Vector(tuple(max(p[i] for p in corners) for i in range(3)))
    target=(mn+mx)/2; size=max(mx-mn); data.type='ORTHO'; data.ortho_scale=size*1.38
    for label,direction in [('Front',(0,-1,.1)),('Back',(0,1,.1)),('Left',(-1,0,.1)),('Right',(1,0,.1)),('Top',(0,-.001,1)),('Material',(-1,-1,.65)),('Clay',(-1,-1,.65)),('Contact',(-1,-1,.2)),('Bottom',(-.3,-.3,-1))]:
        cam.location=target+Vector(direction).normalized()*size*2
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        s.view_layers[0].material_override=clay if label=='Clay' else None
        s.render.filepath=os.path.join(REVIEW,r.name+'_'+label+'.png'); bpy.ops.render.render(write_still=True)
    if r.name=='TrainCarBody':
        data.type='PERSP'; data.lens=24; s.render.resolution_x=900; s.render.resolution_y=600
        cam.location=(0,-3,1.62); cam.rotation_euler=(Vector((0,-15,1.5))-cam.location).to_track_quat('-Z','Y').to_euler()
        s.render.filepath=os.path.join(REVIEW,r.name+'_Interior.png'); bpy.ops.render.render(write_still=True)
        s.render.resolution_x=320; s.render.resolution_y=260
    print('REVIEWED_VIEWS_SAVED',r.name,flush=True)

if '--assembly' in sys.argv:
    # Asset relationship review, not an engine screenshot: same metre placement as CreateTrain.
    for r in roots:
        for o in r.children: o.hide_render=True
    lookup={r.name:r for r in roots}
    def place(name,position,angle=0,mirror=False):
        parent=bpy.data.objects.new('Assembly '+name,None); bpy.context.collection.objects.link(parent)
        parent.location=position; parent.rotation_euler.z=math.radians(angle)
        if mirror: parent.scale.x=-1
        for original in lookup[name].children:
            if original.type!='MESH': continue
            child=original.copy(); child.data=original.data; bpy.context.collection.objects.link(child)
            child.parent=parent; child.hide_render=False
        return parent
    for car in range(4):
        place('TrainCarBody',(0,-car*20.4,0))
        if car<3: place('TrainGangway',(0,-19.6-car*20.4,0))
        for y in [-4.95,-9.85,-14.75]:
            for x in [-1.1,1.1]:
                place('TrainBench',(x,y-car*20.4,0),90 if x<0 else 270)
                place('GrabRail',(x*.60,y-car*20.4,0))
        for d in [2.5,7.4,12.3,17.2]:
            place('SupportPole',(0,-d-car*20.4,0))
            for x in [-1.43,1.43]:
                for delta in [-.325,.325]: place('SlidingDoor',(x,-d-delta-car*20.4,0),270 if x<0 else 90,(delta<0)==(x>0))
                place('TrainThreshold',(math.copysign(1.64,x),-d-car*20.4,0))
    place('TrainCab',(0,.25,0),180); place('TrainCab',(0,-81.05,0))
    place('TrainCabPartition',(0,-.04,0)); place('TrainCabPartition',(0,-80.76,0),180)
    # The four-car assembly is reviewed both inside a connection and as a full train.
    # Keep the existing single-car foreground light setup as a close-range material reference.
    for y in range(-2,-81,-4):
        place('FluorescentLight',(0,y,-.615))
        lamp=bpy.data.lights.new('Assembly cabin light','POINT'); lamp.energy=40; lamp.shadow_soft_size=.4
        obj=bpy.data.objects.new(lamp.name,lamp); bpy.context.collection.objects.link(obj); obj.location=(0,y,2.12)
    bpy.context.view_layer.update(); s.view_layers[0].material_override=None
    data.type='PERSP'; data.lens=24; s.render.resolution_x=1200; s.render.resolution_y=800
    for label,origin,target in [('CabinFront',(0,-.8,1.62),(0,-15,1.55)),('Gangway',(.16,-18.7,1.62),(0,-26,1.55)),('DoorDetail',(0,-7.4,1.50),(1.43,-7.4,1.35)),('SeatEnd',(.45,-3.35,1.32),(1.1,-4.35,.85)),('CrewPartition',(0,-2,1.62),(0,-.04,1.48)),('TrainExterior',(50,10,32),(0,-38,1.2))]:
        cam.location=origin; cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        s.render.filepath=os.path.join(REVIEW,'Assembly_'+label+'.png'); bpy.ops.render.render(write_still=True)
    print('ASSEMBLY_VIEWS_SAVED',flush=True)

if '--seating' in sys.argv:
    # Review the existing exported human and bench together; no pose/mesh is regenerated here.
    for obj in list(bpy.context.scene.objects):
        if obj.type=='MESH': obj.hide_render=True
    bench=next(r for r in roots if r.name=='TrainBench')
    for obj in bench.children: obj.hide_render=False
    with bpy.data.libraries.load(os.path.join(ROOT,'Source~','PassengerHuman.blend'),link=False) as (src,dst):
        dst.objects=list(src.objects)
        dst.actions=['Sit']
    for obj in dst.objects:
        if obj is not None: bpy.context.collection.objects.link(obj); obj.hide_render=False
    human=next(o for o in dst.objects if o.name=='PassengerRoot')
    arm=next(o for o in dst.objects if o.type=='ARMATURE')
    arm.animation_data.action=dst.actions[0]
    if arm.animation_data.action.slots: arm.animation_data.action_slot=arm.animation_data.action.slots[0]
    s.frame_set(1)
    s.view_layers[0].material_override=None; data.type='PERSP';data.lens=45
    s.render.resolution_x=900;s.render.resolution_y=800
    for label,seat,origin in [('CentreFront',0,(1.6,-3.2,1.35)),('EndSide',2,(2.6,-1.9,1.35)),('EndBack',2,(2.1,1.8,1.3)),('SeatContact',0,(1.7,-.65,.65))]:
        human.location=(seat*.53,-.13,0); bpy.context.view_layer.update()
        target=Vector((seat*.53,-.03,.65)); cam.location=origin
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        s.render.filepath=os.path.join(REVIEW,'Seated_'+label+'.png');bpy.ops.render.render(write_still=True)
    print('SEATED_ASSET_CONTACT_VIEWS_SAVED',flush=True)
