"""CC0 Quaternius human refinement and metre-sized, seat-contact-aware clips.
Original third-party FBX is untouched. Editable source and Unity FBX share one rig.
"""
import bpy, math, os
from mathutils import Vector, Matrix
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT=os.path.abspath(os.path.join(ROOT,'../../../../'))
OUT=os.path.join(ROOT,'Art','Models','PassengerHuman.fbx')
REVIEW=os.path.join(PROJECT,'Temp','ThreeDRebuild','PassengerReview'); os.makedirs(REVIEW,exist_ok=True)
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
# One geometric refinement pass, not repeated subdivision of already refined exports.
for mesh in meshes:
    bpy.context.view_layer.objects.active=mesh
    if len(mesh.data.vertices)<1500:
        subdiv=mesh.modifiers.new('Preserve human silhouette and joint continuity','SUBSURF'); subdiv.levels=1
        bpy.ops.object.modifier_move_up(modifier=subdiv.name)
        bpy.ops.object.modifier_apply(modifier=subdiv.name)
    for face in mesh.data.polygons: face.use_smooth=True
    for edge in mesh.data.edges: edge.use_edge_sharp=False
    mesh.data.normals_split_custom_set([(0,0,0)]*len(mesh.data.loops))
    mesh['source_license']='Quaternius Animated Human CC0'; mesh['refinement']='single subdivision, original UV and skin weights'
    mat=mesh.data.materials[0]; mat.use_nodes=True
    principled=mat.node_tree.nodes.get('Principled BSDF'); principled.inputs['Roughness'].default_value=.72
    image_node=mat.node_tree.nodes.new('ShaderNodeTexImage')
    image_node.image=bpy.data.images.load(os.path.join(PROJECT,'Assets','ThirdParty','Quaternius','AnimatedHuman','Textures','ClothedLightSkin.png'),check_existing=True)
    image_node.image.pack()
    mat.node_tree.links.new(image_node.outputs['Color'],principled.inputs['Base Color'])
arm.animation_data.action=idle; bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
arm.animation_data.action=None
for p in arm.pose.bones: p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
def aim_world(name, direction):
    p=arm.pose.bones[name]; b=p.bone
    local=arm.matrix_world.inverted().to_3x3() @ Vector(direction)
    q=(b.tail_local-b.head_local).normalized().rotation_difference(local.normalized())
    m=q.to_matrix().to_4x4() @ b.matrix_local; m.translation=p.head; p.matrix=m
    bpy.context.view_layer.update()
for side,sign in [('Left',-1),('Right',1)]:
    aim_world(side+'UpLeg',(sign*.035,0,-1)); aim_world(side+'Leg',(0,.015,-1)); aim_world(side+'Foot',(0,-1,-.10))
    aim_world(side+'Arm',(sign*.12,-.06,-1)); aim_world(side+'ForeArm',(sign*.04,-.15,-1)); aim_world(side+'Hand',(0,-.12,-1))
standing={p.name:p.matrix_basis.copy() for p in arm.pose.bones}
standing_hip=arm.pose.bones['Hips'].matrix.translation.copy()
# Pelvis joint centre sits above the 0.455m cushion; thighs rest near horizontal.
hips=arm.pose.bones['Hips']; m=hips.matrix.copy()
world_hip=arm.matrix_world @ m.translation; world_hip.z=.585
m.translation=arm.matrix_world.inverted() @ world_hip; hips.matrix=m
bpy.context.view_layer.update()
for side in ['Left','Right']:
    aim_world(side+'UpLeg',(0,-1,-.12))
    aim_world(side+'Leg',(0,.035,-1))
    aim_world(side+'Foot',(0,-1,-.10))
    aim_world(side+'Arm',(0,-.12,-1))
    aim_world(side+'ForeArm',(0,-.7,-.72))
    aim_world(side+'Hand',(0,-1,-.12))
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
# Bake the source gait into the SAME forward axis as the new idle / seating poses.
original_walk=walk; arm.animation_data.action=original_walk; scene.frame_set(1); bpy.context.view_layer.update()
forward=arm.matrix_world.to_3x3() @ (arm.pose.bones['LeftToeBase'].head-arm.pose.bones['LeftFoot'].head)
yaw=-math.atan2(forward.x,-forward.y); rotation=Matrix.Rotation(yaw,4,'Z')
samples=[]
for frame in range(1,32):
    sample_frame=1+(frame-1)*24/30; scene.frame_set(int(sample_frame),subframe=sample_frame%1); bpy.context.view_layer.update()
    p=arm.pose.bones['Hips']; w=arm.matrix_world @ p.matrix
    translation=w.translation.copy(); w=rotation @ w; w.translation=translation
    local=arm.matrix_world.inverted() @ w; local.translation.x=standing_hip.x; local.translation.z=standing_hip.z
    p.matrix=local; bpy.context.view_layer.update()
    samples.append({b.name:b.matrix_basis.copy() for b in arm.pose.bones})
arm.animation_data.action=None; bpy.data.actions.remove(original_walk); walk=bpy.data.actions.new('Walk');arm.animation_data.action=walk
for frame,pose in enumerate(samples,1):
    for p in arm.pose.bones:
        loc,rot,scale=pose[p.name].decompose();p.location=loc;p.rotation_mode='QUATERNION';p.rotation_quaternion=rot;p.scale=scale
        p.keyframe_insert('location',frame=frame);p.keyframe_insert('rotation_quaternion',frame=frame);p.keyframe_insert('scale',frame=frame)
arm.animation_data.action=idle; scene.frame_set(1)
print('GAIT_FORWARD_CORRECTION_DEGREES',math.degrees(yaw),flush=True)
scene.frame_start=1; scene.frame_end=301
# Ordinary commuters wear shoes. Each authored shoe is bound to its existing foot bone.
arm.animation_data.action=idle; scene.frame_set(1); bpy.context.view_layer.update()
shoe_material=bpy.data.materials.new('CommuterShoes'); shoe_material.use_nodes=True
shoe_material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.035,.040,.048,1)
shoe_material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.62
for side in ['Left','Right']:
    foot=arm.matrix_world @ arm.pose.bones[side+'Foot'].head
    toe=arm.matrix_world @ arm.pose.bones[side+'ToeBase'].head
    direction=toe-foot; direction.z=0; direction.normalize()
    right=Vector((-direction.y,direction.x,0))
    outline=[(-.048,-.10),(-.063,0),(-.069,.17),(-.05,.27),(0,.295),(.05,.27),(.069,.17),(.063,0),(.048,-.10)]
    vertices=[]
    for upper in [False,True]:
        for x,y in outline:
            point=foot+right*x+direction*y; point.z=(.115 if y<.1 else .096) if upper else .003
            vertices.append(tuple(point))
    n=len(outline);faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    data=bpy.data.meshes.new(side+'_ShoeMesh');data.from_pydata(vertices,[],faces);data.update()
    shoe=bpy.data.objects.new(side+'_CommuterShoe',data);bpy.context.collection.objects.link(shoe);bpy.context.view_layer.objects.active=shoe
    bevel=shoe.modifiers.new('Rounded toe and welt','BEVEL');bevel.width=.009;bevel.segments=3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for face in shoe.data.polygons: face.use_smooth=True
    inverse_skin=(arm.pose.bones[side+'Foot'].matrix @ arm.data.bones[side+'Foot'].matrix_local.inverted()).inverted()
    for vertex in shoe.data.vertices: vertex.co=inverse_skin @ (arm.matrix_world.inverted() @ vertex.co)
    shoe.parent=arm; shoe.matrix_parent_inverse=Matrix.Identity(4); shoe.matrix_basis=Matrix.Identity(4)
    group=shoe.vertex_groups.new(name=side+'Foot');group.add(list(range(len(shoe.data.vertices))),1,'REPLACE')
    modifier=shoe.modifiers.new('Follow existing foot','ARMATURE');modifier.object=arm
    shoe.data.materials.append(shoe_material); meshes.append(shoe)
arm.animation_data.action=idle;scene.frame_set(1)
for o in bpy.context.scene.objects: o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Source~','PassengerHuman.blend'))
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0)
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
for action,frame,label in [(idle,1,'Idle'),(walk,8,'WalkContact'),(walk,16,'WalkPassing'),(down,15,'SitDown'),(sit,1,'Seated'),(up,15,'StandUp')]:
    arm.animation_data.action=action; scene.frame_set(frame)
    scene.render.filepath=os.path.join(REVIEW,label+'.png');bpy.ops.render.render(write_still=True)
print('PASSENGER_SOURCE_EXPORTED',sum(len(o.data.vertices) for o in meshes),'vertices; Idle Walk SitDown Sit StandUp; CC0 original preserved')
