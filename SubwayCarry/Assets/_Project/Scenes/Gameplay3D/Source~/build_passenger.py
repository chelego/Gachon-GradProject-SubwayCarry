"""CC0 Quaternius human refinement and metre-sized, seat-contact-aware clips.
Original third-party FBX is untouched. Editable source and Unity FBX share one rig.
"""
import bpy, math, os, sys
sys.dont_write_bytecode=True
from mathutils import Vector, Matrix, Quaternion
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT=os.path.abspath(os.path.join(ROOT,'../../../../'))
OUT=os.path.join(ROOT,'Art','Models','PassengerHuman.fbx')
REVIEW=os.path.join(PROJECT,'Temp','ThreeDRebuild','PassengerReview'); os.makedirs(REVIEW,exist_ok=True)
review_only='--review-only' in sys.argv
if review_only: OUT=os.path.join(REVIEW,'PassengerCandidate.fbx')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.path.join(PROJECT,'Assets','ThirdParty','Quaternius','AnimatedHuman','AnimatedHuman.fbx'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.data.objects if o.type=='MESH']
idle=next(a for a in bpy.data.actions if a.name.endswith('|Idle'))
walk=next(a for a in bpy.data.actions if a.name.endswith('|Walk'))
idle.name='Idle'; walk.name='Walk'
for a in list(bpy.data.actions):
    if a not in (idle,walk): bpy.data.actions.remove(a)
arm.animation_data.action=idle; bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get()
points=[o.matrix_world @ v.co for o in meshes for v in o.evaluated_get(deps).data.vertices]
low=min(v.z for v in points); scale=1.74/(max(v.z for v in points)-low)
root=bpy.data.objects.new('PassengerRoot',None); bpy.context.collection.objects.link(root); arm.parent=root
for o in meshes:
    if not o.parent: o.parent=root
root.scale=(scale,)*3; root.location.z=-low*scale
forward=arm.matrix_world.to_3x3() @ (arm.data.bones['LeftToeBase'].head_local-arm.data.bones['LeftFoot'].head_local)
root.rotation_euler.z=-math.atan2(forward.x,-forward.y)
bpy.context.view_layer.update()
arm.animation_data.action=idle; scene=bpy.context.scene; scene.frame_set(1); bpy.context.view_layer.update()
arm.animation_data.action=idle; bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
arm.animation_data.action=None
for p in arm.pose.bones: p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
arm.animation_data.action=None
for p in arm.pose.bones: p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
def aim_world(name, direction, normal=None):
    p=arm.pose.bones[name]; b=p.bone
    local=arm.matrix_world.inverted().to_3x3() @ Vector(direction)
    if normal is None:
        q=(b.tail_local-b.head_local).normalized().rotation_difference(local.normalized())
        m=q.to_matrix().to_4x4() @ b.matrix_local
    else:
        y=local.normalized();z=arm.matrix_world.inverted().to_3x3()@Vector(normal);z=(z-y*z.dot(y)).normalized();x=y.cross(z).normalized();z=x.cross(y).normalized()
        m=Matrix((x,y,z)).transposed().to_4x4()
    m.translation=p.head;p.matrix=m
    bpy.context.view_layer.update()
# Build the new anatomical body and refit its same-name rig before authoring poses.
sys.path.insert(0,os.path.dirname(__file__))
from build_commuter_body import rebuild
rebuild(arm,meshes,os.path.dirname(__file__))
for side,sign in [('Left',1),('Right',-1)]:
    clavicle=arm.data.bones[side+'Shoulder'];direction=arm.matrix_world.to_3x3()@(clavicle.tail_local-clavicle.head_local)
    direction.z-=.03;aim_world(side+'Shoulder',direction)
    aim_world(side+'UpLeg',(sign*.035,0,-1),(0,-1,0)); aim_world(side+'Leg',(0,.015,-1),(0,-1,0))
    aim_world(side+'Foot',(0,-1,-.10),(0,0,1)); aim_world(side+'ToeBase',(0,-1,-.10),(0,0,1))
    aim_world(side+'Arm',(sign*.09,-.035,-1),(0,-1,0)); aim_world(side+'ForeArm',(sign*.025,-.14,-1),(0,-1,0)); aim_world(side+'Hand',(0,-.17,-1),(-sign,0,0))
    for digit,label in enumerate(['Thumb','Index','Middle','Ring','Pinky']):
        for segment in range(1,4):
            p=arm.pose.bones[side+'Hand'+label+str(segment)];p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion((1,0,0),math.radians(([3,6,4] if digit==0 else [8+digit,16,8])[segment-1]))
standing={p.name:p.matrix_basis.copy() for p in arm.pose.bones}
standing_hip=arm.pose.bones['Hips'].matrix.translation.copy()
# Pelvis joint centre sits above the 0.455m cushion; thighs rest near horizontal.
hips=arm.pose.bones['Hips']; m=hips.matrix.copy()
world_hip=arm.matrix_world @ m.translation; world_hip.z=.585
m.translation=arm.matrix_world.inverted() @ world_hip; hips.matrix=m
bpy.context.view_layer.update()
for side in ['Left','Right']:
    aim_world(side+'UpLeg',(0,-1,-.12),(0,-1,0))
    aim_world(side+'Leg',(0,.035,-1),(0,-1,0))
    aim_world(side+'Foot',(0,-1,-.10),(0,0,1)); aim_world(side+'ToeBase',(0,-1,-.10),(0,0,1))
    aim_world(side+'Arm',(0,-.12,-1))
    aim_world(side+'ForeArm',((.07 if side=='Left' else -.07),-.8,-.60),(0,-1,0))
    aim_world(side+'Hand',(0,-1,-.30),(0,0,-1))
    for label in ['Thumb','Index','Middle','Ring','Pinky']:
        for segment in range(1,4):
            p=arm.pose.bones[side+'Hand'+label+str(segment)];p.rotation_quaternion=Quaternion((1,0,0),math.radians([18,25,18][segment-1]))
seated={p.name:p.matrix_basis.copy() for p in arm.pose.bones}
def pose_action(name, start, end, frames, loop=False):
    action=bpy.data.actions.new(name); arm.animation_data.action=action
    action.use_fake_user=True
    for frame in range(1,frames+1):
        t=(frame-1)/(frames-1); t=t*t*(3-2*t)
        for p in arm.pose.bones:
            a=start[p.name]; b=end[p.name]
            loc1,rot1,scale1=a.decompose(); loc2,rot2,scale2=b.decompose()
            p.rotation_mode='QUATERNION'; p.location=loc1.lerp(loc2,t); p.rotation_quaternion=rot1.slerp(rot2,t); p.scale=scale1.lerp(scale2,t)
            p.keyframe_insert('location',frame=frame); p.keyframe_insert('rotation_quaternion',frame=frame); p.keyframe_insert('scale',frame=frame)
    return action
sit=pose_action('Sit',seated,seated,61,True)
down=pose_action('SitDown',standing,seated,28)
up=pose_action('StandUp',seated,standing,28)
bpy.data.actions.remove(idle); idle=pose_action('Idle',standing,standing,121,True)
scene=bpy.context.scene; scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1; scene.render.fps=30
# Retain public action names, now driven by measured human movement.
arm.animation_data.action=None
bpy.data.actions.remove(idle);bpy.data.actions.remove(walk)
from retarget_commuter import author
idle,walk=author(arm,standing,aim_world,os.path.join(os.path.dirname(__file__),'MotionCapture'))
arm.animation_data.action=idle; scene.frame_set(1)
scene.frame_start=1; scene.frame_end=301
arm.animation_data.action=None
for p in arm.pose.bones: p.matrix_basis=standing[p.name].copy()
bpy.context.view_layer.update()
# Calibrate footwear to its actual sole surface, not a presumed flat source.
# A level foot bone had hidden a ~10-degree inward bank in the fitted shoe.
# Keep the upper sock cuff on the calf's original skin transform, like trousers.
for shoe in [o for o in meshes if o.name=='Commuter_shoes02']:
    world_points=[shoe.matrix_world@v.co for v in shoe.data.vertices]
    sole_rotations={}
    for side,sign in [('Left',1),('Right',-1)]:
        low=min(p.z for p in world_points if p.x*sign>0)
        samples=[]
        for polygon in shoe.data.polygons:
            points=[world_points[i] for i in polygon.vertices]
            center=sum(points,Vector())/len(points)
            normal=(points[1]-points[0]).cross(points[2]-points[0]).normalized()
            if center.x*sign>0 and center.z<low+.08 and normal.z<-.6:
                samples.append(center)
        if len(samples)<12:raise RuntimeError('Insufficient shoe sole landmarks: '+side)
        # Least-squares sole plane z = ax + by + c. Its pitch includes natural
        # toe rocker; remove only frontal bank around the ankle, not that rocker.
        rows=[Vector((p.x,p.y,1)) for p in samples]
        normal_matrix=Matrix([[sum(r[i]*r[j] for r in rows) for j in range(3)] for i in range(3)])
        rhs=Vector(tuple(sum(r[i]*p.z for r,p in zip(rows,samples)) for i in range(3)))
        plane=normal_matrix.inverted()@rhs
        sole_rotations[side]=Quaternion((0,1,0),math.atan(plane.x))
        print('FOOTWEAR_BIND_BANK_CORRECTION',side,round(math.degrees(math.atan(plane.x)),3),flush=True)
    for vertex in shoe.data.vertices:
        side='Left' if (arm.matrix_world@vertex.co).x>0 else 'Right'
        source=vertex.co.copy()
        ankle_rest=arm.matrix_world@arm.data.bones[side+'Foot'].head_local
        ankle_posed=arm.matrix_world@arm.pose.bones[side+'Foot'].head
        planted_world=sole_rotations[side]@(shoe.matrix_world@source-ankle_rest)+ankle_posed
        planted=arm.matrix_world.inverted()@planted_world
        skin=Matrix(((0,0,0,0),)*4)
        desired=Vector((0,0,0))
        for group in vertex.groups:
            name=shoe.vertex_groups[group.group].name
            transform=arm.pose.bones[name].matrix@arm.data.bones[name].matrix_local.inverted()
            skin+=transform*group.weight
            desired+=(planted if name.endswith('Foot') else transform@source)*group.weight
        vertex.co=skin.inverted()@desired
# Ground-contact correction is baked into hips, not a Unity model offset. Preserve swing-foot lift.
for action in [idle,walk]:
    arm.animation_data.action=action
    frames=range(1,int(action.frame_range[1])+1)
    for frame in frames:
        scene.frame_set(frame);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
        lowest=min((o.matrix_world@v.co).z for o in meshes for v in o.evaluated_get(deps).data.vertices)
        if abs(lowest)<.001: continue
        hip=arm.pose.bones['Hips'];matrix=hip.matrix.copy();world=arm.matrix_world@matrix.translation
        world.z-=lowest;matrix.translation=arm.matrix_world.inverted()@world;hip.matrix=matrix
        hip.keyframe_insert('location',frame=frame)
arm.animation_data.action=idle;scene.frame_set(1)
from retarget_commuter import audit_waiting
audit_waiting(arm,idle)
arm.animation_data.action=idle;scene.frame_set(1)
# One skinning buffer per commuter, with explicit material slots and one UV set.
# Keep distinct skin/cloth/hair/eyes/shoes while avoiding a dozen separate skinned objects per NPC.
canonical=['CommuterSkin','CommuterCoat','CommuterTrousers','CommuterHair','CommuterSclera','CommuterIris','CommuterShoes','CommuterUnderShirt']
for o in meshes:
    if o.data.uv_layers: o.data.uv_layers.active.name='UVMap'
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active=next(o for o in meshes if o.name=='CommuterBody')
bpy.ops.object.join();body=bpy.context.object
old_materials=list(body.data.materials)
assignment=[canonical.index(old_materials[p.material_index].name) for p in body.data.polygons]
body.data.materials.clear()
for name in canonical: body.data.materials.append(bpy.data.materials[name])
for p,slot in zip(body.data.polygons,assignment): p.material_index=slot
meshes[:]=[body]
body['material_contract']='Skin, Coat, Trousers, Hair, Sclera, Iris, Shoes, UnderShirt'
body['crowd_lod']='One skinned mesh; no repeated subdivision'
for o in bpy.context.scene.objects: o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.context.preferences.filepaths.save_version=0
blend_path=os.path.join(REVIEW,'PassengerCandidate.blend') if review_only else os.path.join(ROOT,'Source~','PassengerHuman.blend')
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0)
if '--verify-fbx' in sys.argv:
    # Inspect the interchange file, not only Blender's pre-export deformation.
    source_materials={name:bpy.data.materials[name] for name in canonical}
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=OUT)
    arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    meshes[:]=[o for o in bpy.data.objects if o.type=='MESH']
    for mesh in meshes:
        for i,mat in enumerate(mesh.data.materials): mesh.data.materials[i]=source_materials[mat.name.split('.')[0]]
    idle,walk,down,sit,up=[next(a for a in bpy.data.actions if a.name.endswith('|'+name)) for name in ['Idle','Walk','SitDown','Sit','StandUp']]
    print('FBX_ROUNDTRIP_RENDER',flush=True)
    audit_waiting(arm,idle)
# Asset-only review of the actual refined skin at gait / sitting / transition poses.
scene.render.engine='CYCLES'; scene.cycles.samples=20; scene.cycles.use_denoising=True
scene.render.resolution_x=700; scene.render.resolution_y=800; scene.render.resolution_percentage=100
scene.world.color=(.22,.22,.22)
def area(name,location,energy,size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=energy; data.shape='DISK'; data.size=size
    o=bpy.data.objects.new(name,data); scene.collection.objects.link(o); o.location=location; o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
area('Soft key',(3,-4,4),350,4); area('Rim',(-3,2,3),240,3)
camera_data=bpy.data.cameras.new('Review'); camera=bpy.data.objects.new('Review',camera_data); scene.collection.objects.link(camera); scene.camera=camera
camera.location=(2.5,-4,2.0); camera.rotation_euler=(Vector((0,-.08,.85))-camera.location).to_track_quat('-Z','Y').to_euler(); camera_data.type='ORTHO';camera_data.ortho_scale=2.1
review_poses=[(idle,1,'Idle')] if '--waiting-only' in sys.argv else [(idle,1,'Idle'),(walk,8,'WalkContact'),(walk,16,'WalkPassing'),(down,15,'SitDown'),(sit,1,'Seated'),(up,15,'StandUp')]
for action,frame,label in review_poses:
    arm.animation_data.action=action; scene.frame_set(frame)
    scene.render.filepath=os.path.join(REVIEW,label+'.png');bpy.ops.render.render(write_still=True)
arm.animation_data.action=idle;scene.frame_set(1)
for location,target,scale,label in [((4,0,1.8),(0,0,.85),2.1,'Side'),((-2.5,4,2),(0,0,.85),2.1,'Back'),
        ((0,-4,1.8),(0,0,.85),2.1,'Front'),((0,-4,.32),(0,0,.32),.75,'Ankles')]:
    camera.location=location;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera_data.ortho_scale=scale
    scene.render.filepath=os.path.join(REVIEW,label+'.png');bpy.ops.render.render(write_still=True)
if '--waiting-motion' in sys.argv:
    camera_data.ortho_scale=2.1
    camera.location=(2.5,-4,2.0);camera.rotation_euler=(Vector((0,-.08,.85))-camera.location).to_track_quat('-Z','Y').to_euler()
    arm.animation_data.action=idle;scene.frame_start=1;scene.frame_end=int(idle.frame_range[1])
    if hasattr(scene.render.image_settings,'media_type'):scene.render.image_settings.media_type='VIDEO'
    scene.render.image_settings.file_format='FFMPEG';scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264'
    scene.render.ffmpeg.constant_rate_factor='HIGH';scene.render.filepath=os.path.join(REVIEW,'Waiting.mp4')
    bpy.ops.render.render(animation=True)
print('PASSENGER_SOURCE_EXPORTED',sum(len(o.data.vertices) for o in meshes),'vertices; Idle Walk SitDown Sit StandUp; CC0 original preserved')
