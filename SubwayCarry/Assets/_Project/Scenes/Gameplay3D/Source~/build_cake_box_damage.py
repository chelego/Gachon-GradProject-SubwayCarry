"""Build four metre-scaled cake-box damage stages without replacing CakeBox.fbx.

The FBX is loaded only by PackageDamageVisual3D in the player's prototype scene.
All stages keep the original 0.38 m package envelope and pivot at the box floor.
"""
import bpy
import math
import os
from mathutils import Vector


SOURCE_DIR = os.path.dirname(os.path.abspath(__file__))
GAMEPLAY3D = os.path.abspath(os.path.join(SOURCE_DIR, ".."))
OUTPUT = os.path.join(GAMEPLAY3D, "Resources", "CakeBoxDamageStages.fbx")
BLEND = os.path.join(SOURCE_DIR, "CakeBoxDamageStages.blend")
PROJECT = os.path.abspath(os.path.join(GAMEPLAY3D, "../../../.."))
PREVIEW = os.path.join(PROJECT, "Temp", "ThreeDRebuild", "CakeBoxDamageStages.png")

os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 360
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
scene.world.color = (0.035, 0.045, 0.045)


def make_material(name, color, roughness):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1.0)
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    return material


cardboard_material = make_material("Metro_Cardboard", (0.57, 0.39, 0.22), 0.82)
brown_material = make_material("Metro_Brown", (0.24, 0.18, 0.12), 0.56)
ribbon_material = make_material("Circulation_Yellow", (0.92, 0.63, 0.05), 0.48)
MATERIALS = {
    "cardboard": cardboard_material,
    "crease": brown_material,
    "ribbon": ribbon_material,
    "inside": brown_material,
}


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bevel(obj, width=0.002, segments=2):
    activate(obj)
    modifier = obj.modifiers.new("Carton edge radius", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def cube(name, parent, location, dimensions, material, rotation=(0.0, 0.0, 0.0), edge=0.002):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.parent = parent
    obj.data.materials.append(MATERIALS[material])
    if edge > 0.0:
        bevel(obj, min(edge, min(dimensions) * 0.2))
    return obj


BOX_FACES = [
    (3, 2, 1, 0),
    (4, 5, 6, 7),
    (0, 1, 5, 4),
    (1, 2, 6, 5),
    (2, 3, 7, 6),
    (3, 0, 4, 7),
]


def crushed_carton(name, parent, bottom, top, top_offsets, top_heights, material="cardboard"):
    bx, by = bottom[0] * 0.5, bottom[1] * 0.5
    tx, ty = top[0] * 0.5, top[1] * 0.5
    base = [(-bx, -by, 0.0), (bx, -by, 0.0), (bx, by, 0.0), (-bx, by, 0.0)]
    top_ring = []
    corners = [(-tx, -ty), (tx, -ty), (tx, ty), (-tx, ty)]
    for index, ((x, y), offset) in enumerate(zip(corners, top_offsets)):
        top_ring.append((x + offset[0], y + offset[1], top_heights[index]))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(base + top_ring, [], BOX_FACES)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    mesh.materials.append(MATERIALS[material])
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    bevel(obj, 0.002, 2)
    return obj


def seam(parent, x, z=0.272, length=0.38, rotation=(0.0, 0.0, 0.0)):
    return cube("Fold seam", parent, (x, 0.0, z), (0.003, length, 0.004), "crease", rotation, 0.0004)


def ribbon_pair(parent, z, across_rotation=(0.0, 0.0, 0.0), along_rotation=(0.0, 0.0, 0.0), offset=(0.0, 0.0)):
    cube("Ribbon across", parent, (offset[0], 0.0, z), (0.035, 0.396, 0.006), "ribbon", across_rotation, 0.001)
    cube("Ribbon along", parent, (0.0, offset[1], z + 0.001), (0.396, 0.035, 0.006), "ribbon", along_rotation, 0.001)


master = bpy.data.objects.new("CakeBoxDamageStages", None)
bpy.context.collection.objects.link(master)
master["unit_metres"] = 1.0
master["stage_thresholds"] = "Intact > 75, Slight > 50, Heavy > 0, Destroyed = 0"
stages = []


def stage(name):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    root.parent = master
    root["damage_stage"] = name
    stages.append(root)
    return root


intact = stage("Stage_Intact")
cube("Intact carton", intact, (0.0, 0.0, 0.13), (0.38, 0.38, 0.26), "cardboard", edge=0.004)
cube("Intact lid", intact, (0.0, 0.0, 0.272), (0.392, 0.392, 0.025), "cardboard", edge=0.002)
seam(intact, -0.182)
seam(intact, 0.182)
ribbon_pair(intact, 0.288)

slight = stage("Stage_Slight")
crushed_carton(
    "Slightly dented carton",
    slight,
    (0.38, 0.38),
    (0.38, 0.38),
    ((0.0, 0.0), (0.0, 0.0), (-0.025, -0.018), (0.0, -0.005)),
    (0.26, 0.26, 0.225, 0.252),
)
cube("Slightly lifted lid", slight, (-0.006, 0.0, 0.269), (0.392, 0.392, 0.024), "cardboard", (0.0, math.radians(-3.0), math.radians(1.5)), 0.002)
seam(slight, -0.182, 0.270, rotation=(0.0, math.radians(-3.0), math.radians(1.5)))
seam(slight, 0.182, 0.270, rotation=(0.0, math.radians(-3.0), math.radians(1.5)))
ribbon_pair(slight, 0.286, along_rotation=(0.0, math.radians(-5.0), math.radians(2.0)), offset=(0.008, -0.004))
cube("Corner crease", slight, (0.145, 0.192, 0.18), (0.12, 0.004, 0.008), "crease", (0.0, math.radians(16.0), math.radians(-20.0)), 0.0004)

heavy = stage("Stage_Heavy")
crushed_carton(
    "Heavily crushed carton",
    heavy,
    (0.39, 0.39),
    (0.35, 0.35),
    ((0.025, 0.02), (-0.015, 0.018), (-0.055, -0.042), (0.035, -0.025)),
    (0.22, 0.245, 0.145, 0.20),
)
cube("Heavy lid left", heavy, (-0.098, 0.0, 0.235), (0.19, 0.39, 0.022), "cardboard", (0.0, math.radians(11.0), math.radians(-4.0)), 0.002)
cube("Heavy lid right", heavy, (0.095, 0.0, 0.201), (0.19, 0.39, 0.022), "cardboard", (0.0, math.radians(-16.0), math.radians(5.0)), 0.002)
cube("Torn corner shadow", heavy, (0.173, 0.17, 0.13), (0.035, 0.06, 0.12), "inside", (math.radians(8.0), 0.0, math.radians(-18.0)), 0.001)
cube("Loose ribbon across", heavy, (-0.03, 0.0, 0.252), (0.035, 0.43, 0.006), "ribbon", (math.radians(4.0), math.radians(12.0), math.radians(-5.0)), 0.001)
cube("Loose ribbon along", heavy, (0.0, -0.025, 0.235), (0.42, 0.035, 0.006), "ribbon", (math.radians(-12.0), math.radians(-3.0), math.radians(5.0)), 0.001)

destroyed = stage("Stage_Destroyed")
crushed_carton(
    "Collapsed carton",
    destroyed,
    (0.42, 0.41),
    (0.34, 0.34),
    ((0.04, 0.03), (-0.05, 0.025), (-0.075, -0.06), (0.055, -0.04)),
    (0.105, 0.155, 0.07, 0.125),
)
cube("Collapsed lid front", destroyed, (0.0, -0.135, 0.115), (0.40, 0.20, 0.02), "cardboard", (math.radians(20.0), math.radians(5.0), math.radians(-3.0)), 0.002)
cube("Collapsed lid back", destroyed, (-0.025, 0.14, 0.145), (0.39, 0.19, 0.02), "cardboard", (math.radians(-26.0), math.radians(-10.0), math.radians(5.0)), 0.002)
cube("Exposed dark interior", destroyed, (0.015, 0.0, 0.105), (0.31, 0.28, 0.025), "inside", (0.0, math.radians(7.0), 0.0), 0.001)
cube("Fallen ribbon", destroyed, (0.03, -0.07, 0.17), (0.045, 0.46, 0.006), "ribbon", (math.radians(18.0), math.radians(26.0), math.radians(-12.0)), 0.001)
cube("Loose ribbon end", destroyed, (0.13, 0.13, 0.105), (0.27, 0.035, 0.006), "ribbon", (math.radians(-14.0), math.radians(-10.0), math.radians(28.0)), 0.001)


for root in stages:
    root.location = (0.0, 0.0, 0.0)
    for obj in root.children:
        if obj.type != "MESH":
            continue
        for vertex in obj.bound_box:
            point = obj.matrix_world @ Vector(vertex)
            assert -0.34 <= point.x <= 0.34, (root.name, obj.name, "x", point.x)
            assert -0.34 <= point.y <= 0.34, (root.name, obj.name, "y", point.y)
            assert -0.04 <= point.z <= 0.38, (root.name, obj.name, "z", point.z)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND)

bpy.ops.object.select_all(action="DESELECT")
master.select_set(True)
for root in stages:
    root.select_set(True)
    for obj in root.children:
        obj.select_set(True)
bpy.context.view_layer.objects.active = master
bpy.ops.export_scene.fbx(
    filepath=OUTPUT,
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    bake_anim=False,
    add_leaf_bones=False,
    use_custom_props=True,
    path_mode="RELATIVE",
)

# Asset-local review image. This is written under Temp and is not a game asset.
preview_positions = (-0.84, -0.28, 0.28, 0.84)
for root, x in zip(stages, preview_positions):
    root.location.x = x

ground_mat = make_material("PreviewGround", (0.07, 0.085, 0.08), 0.9)
ground = cube("Preview ground", None, (0.0, 0.0, -0.04), (2.5, 1.0, 0.05), "inside", edge=0.0)
ground.data.materials.clear()
ground.data.materials.append(ground_mat)

for position, energy, size in [((2.5, -3.0, 4.0), 900.0, 4.0), ((-2.0, 1.0, 2.5), 650.0, 3.0)]:
    data = bpy.data.lights.new("Review light", "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new("Review light", data)
    bpy.context.collection.objects.link(light)
    light.location = position
    light.rotation_euler = (Vector((0.0, 0.0, 0.13)) - light.location).to_track_quat("-Z", "Y").to_euler()

camera_data = bpy.data.cameras.new("Review camera")
camera = bpy.data.objects.new("Review camera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (2.25, -3.0, 1.75)
target = Vector((0.0, 0.0, 0.13))
camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
camera_data.type = "ORTHO"
camera_data.ortho_scale = 2.55
scene.camera = camera
scene.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)

for root in stages:
    root.location = (0.0, 0.0, 0.0)

triangles = sum(
    max(0, len(polygon.vertices) - 2)
    for root in stages
    for obj in root.children
    if obj.type == "MESH"
    for polygon in obj.data.polygons
)
print("CAKE_BOX_DAMAGE_BUILD_PASS", len(stages), triangles, OUTPUT, flush=True)
