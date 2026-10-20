import bpy, json, os
root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..', '..', '..'))
source = os.path.join(root, 'Assets', '_Project', 'Scenes', 'Gameplay3D', 'Art', 'Models', 'PassengerHuman.fbx')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=source)
result = {
    'armatures': [{ 'name': o.name, 'bones': [{ 'name': b.name, 'head': list(b.head_local), 'tail': list(b.tail_local) } for b in o.data.bones] } for o in bpy.data.objects if o.type == 'ARMATURE'],
    'actions': [{'name': a.name, 'range': list(a.frame_range)} for a in bpy.data.actions],
    'meshes': [{'name': o.name, 'size': list(o.dimensions), 'vertices': len(o.data.vertices)} for o in bpy.data.objects if o.type == 'MESH'],
}
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
result['pose_bounds'] = []
for action in bpy.data.actions:
    arm.animation_data.action = action
    bpy.context.scene.frame_set(1)
    deps = bpy.context.evaluated_depsgraph_get()
    points = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == 'MESH' for v in o.evaluated_get(deps).data.vertices]
    result['pose_bounds'].append({'action': action.name, 'min': [min(p[i] for p in points) for i in range(3)], 'max': [max(p[i] for p in points) for i in range(3)]})
with open(os.path.join(root, 'Temp', 'ThreeDRebuild', 'human_inspect.json'), 'w', encoding='utf8') as out:
    json.dump(result, out, indent=2)
