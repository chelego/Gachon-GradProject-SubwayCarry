"""Read-only source audit: blender -b VerticalCirculation.blend --python this_file.py."""
import bpy
import bmesh
import math
import os

assert bpy.context.scene.unit_settings.scale_length == 1
issues = []
counts = {}
for name in ['StationStaircase', 'StationEscalator']:
    root = bpy.data.objects[name]
    children = [obj for obj in bpy.data.objects if obj.parent == root]
    assert root['unit_metres'] == 1
    assert root['rise_m'] == 3.6
    for suffix in ['EntranceFloor', 'ExitFloor']:
        assert bpy.data.objects.get(name + '_' + suffix) in children
    meshes = [obj for obj in children if obj.type == 'MESH']
    triangles = 0
    for obj in meshes:
        if not all(abs(value - 1) < 1e-6 for value in obj.scale):
            issues.append((obj.name, 'unapplied scale'))
        if not obj.data.uv_layers or len(obj.data.uv_layers.active.data) != len(obj.data.loops):
            issues.append((obj.name, 'missing UV'))
        if any(not math.isfinite(value) for vertex in obj.data.vertices for value in vertex.co):
            issues.append((obj.name, 'nonfinite vertex'))
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        if any(not edge.is_manifold for edge in bm.edges):
            issues.append((obj.name, 'open volume'))
        if any(face.calc_area() < 1e-12 for face in bm.faces):
            issues.append((obj.name, 'degenerate face'))
        bm.free()
        triangles += sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
    counts[name] = dict(editable_mesh_parts=len(meshes), triangles=triangles,
                        rise_m=root['rise_m'], length_m=root['total_length_m'])
for image in bpy.data.images:
    if image.source == 'FILE' and image.filepath and not os.path.isfile(bpy.path.abspath(image.filepath)):
        issues.append((image.name, 'missing source texture'))
if issues:
    print('CIRCULATION_SOURCE_ISSUES', issues[:30], 'TOTAL', len(issues))
    raise RuntimeError('Source topology/UV/path audit failed')
print('CIRCULATION_SOURCE_PASS', counts)
