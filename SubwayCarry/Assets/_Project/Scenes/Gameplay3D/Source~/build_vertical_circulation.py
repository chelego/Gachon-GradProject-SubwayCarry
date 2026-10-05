"""Blender-only, metre-scale station circulation assets; never rebuilds a Unity scene.

Blender 5.2: blender --background --factory-startup --python this_file.py
The assets are dimensioned originals, NOT a surveyed copy of Gachon's installation.
Escalator envelope: Schindler 9300AE Type20 30K metric layout data (reference only).
https://www.schindler.com/content/dam/website/us/docs/escalators/schindler-9300/9300ae-20-30k-layout-data.pdf/_jcr_content/renditions/original./9300ae-20-30k-layout-data.pdf
Stair dimensions: explicit design assumptions; 150mm rise, 300mm going, 1800mm clear.
Root origin = lower floor / entrance centre, +Y = ascent, +Z = up, 1 unit = 1m.
Existing StairFlight/EscalatorFlight/StationKit and the playable scene are preserved.
"""
import bpy
import math
import os
import sys
import numpy as np
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
PROJECT = os.path.abspath(os.path.join(ROOT, '..', '..', '..', '..'))
ART = os.path.join(ROOT, 'Art')
REVIEW = os.path.join(PROJECT, 'Temp', 'ThreeDRebuild', 'AssetReview')
for folder in [REVIEW, os.path.join(ART, 'Models'), os.path.join(ART, 'Textures', 'Circulation')]:
    os.makedirs(folder, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = 'METERS'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.7, .75, .8, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45

materials = {}
asset = None
review_only = []


def texture(name, values, data=True):
    """Retain the five independent source maps, not just a packed engine texture."""
    if values.ndim == 2:
        values = np.repeat(values[..., None], 3, axis=-1)
    if values.shape[-1] == 3:
        values = np.concatenate([values, np.ones((*values.shape[:2], 1))], axis=-1)
    img = bpy.data.images.new(name, width=values.shape[1], height=values.shape[0], alpha=True)
    img.colorspace_settings.name = 'Non-Color' if data else 'sRGB'
    img.pixels.foreach_set(np.clip(values, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = os.path.join(ART, 'Textures', 'Circulation', name + '.png')
    img.file_format = 'PNG'
    img.save()
    return img


def material(name, color, roughness, metallic=0, kind='paint'):
    rng = np.random.default_rng(221)
    n = 512
    yy, xx = np.mgrid[0:n, 0:n] / n
    grain = rng.random((n, n)) - .5
    if kind == 'metal':
        # Tangential brushing is subtle; no fake baked light/shadow in Base Color.
        # Brushing changes the highlight softly; it is not a corrugated 1.5mm relief.
        # Strong periodic lines at this UV scale alias into dark zebra bands at eye distance.
        relief = np.sin(yy * math.tau * 174) * .000035
        rough = roughness + .012 * np.sin(yy * math.tau * 174) + grain * .008
        variation = grain * .008
    elif kind == 'stone':
        flecks = rng.random((n, n)) > .96
        relief = grain * .001
        rough = roughness + grain * .06 + flecks * .025
        variation = grain * .033 + flecks * .035
    elif kind == 'tile':
        cx, cy = (xx * 4) % 1, (yy * 4) % 1
        grout = (np.minimum(cx, 1 - cx) < .010) | (np.minimum(cy, 1 - cy) < .010)
        relief = np.where(grout, -.002, 0) + grain * .0001
        rough = np.where(grout, .76, roughness + grain * .02)
        variation = grain * .008
    else:
        relief = grain * .00025
        rough = roughness + grain * .025
        variation = grain * .006
    dy, dx = np.gradient(relief)
    normal = np.stack([-dx * 38, -dy * 38, np.ones_like(xx)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    base = np.stack([np.clip(c + variation, 0, 1) for c in color], axis=-1)
    if kind == 'tile':
        base[grout] = (.38, .37, .34)
    maps = {
        'BaseColor': texture(name + '_BaseColor', base, False),
        'Normal': texture(name + '_Normal', normal * .5 + .5),
        'Roughness': texture(name + '_Roughness', rough),
        'Metallic': texture(name + '_Metallic', np.ones_like(xx) * metallic),
        # Standalone tile has no cavity; cavities are geometric, not false dark stains.
        'AO': texture(name + '_AO', np.ones_like(xx)),
    }
    # Unity URP packing: R metallic, A smoothness; independent source maps stay above.
    packed = np.zeros((n, n, 4))
    packed[..., 0] = metallic
    packed[..., 3] = 1 - np.clip(rough, 0, 1)
    texture(name + '_MetallicSmoothness', packed)
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    for channel, socket in [('BaseColor', 'Base Color'), ('Roughness', 'Roughness'), ('Metallic', 'Metallic')]:
        node = nodes.new('ShaderNodeTexImage')
        node.image = maps[channel]
        node.label = channel
        links.new(node.outputs['Color'], bsdf.inputs[socket])
    node = nodes.new('ShaderNodeTexImage')
    node.image = maps['Normal']
    norm = nodes.new('ShaderNodeNormalMap')
    norm.inputs['Strength'].default_value = .22
    links.new(node.outputs['Color'], norm.inputs['Color'])
    links.new(norm.outputs['Normal'], bsdf.inputs['Normal'])
    # AO is retained separately; white is correct for a seamless source tile.
    node = nodes.new('ShaderNodeTexImage')
    node.image = maps['AO']
    node.label = 'AO source; use geometry/contact lighting for cavities'
    materials[name] = mat
    return mat


material('Circulation_Granite', (.47, .49, .48), .64, kind='stone')
material('Circulation_CreamTile', (.72, .7, .61), .32, kind='tile')
material('Circulation_Steel', (.52, .55, .57), .29, 1, 'metal')
material('Circulation_Aluminium', (.48, .51, .52), .38, 1, 'metal')
material('Circulation_Rubber', (.025, .029, .033), .69)
material('Circulation_Yellow', (.91, .66, .015), .47)
material('Circulation_StopRed', (.64, .025, .012), .46)
glass = bpy.data.materials.new('Circulation_SafetyGlass')
glass.use_nodes = True
gb = glass.node_tree.nodes.get('Principled BSDF')
gb.inputs['Base Color'].default_value = (.87, .94, .94, 1)
gb.inputs['Transmission Weight'].default_value = 1
gb.inputs['Roughness'].default_value = .06
gb.inputs['IOR'].default_value = 1.48
materials[glass.name] = glass
clay = bpy.data.materials.new('Review_Clay')
clay.diffuse_color = (.48, .48, .48, 1)
clay.use_nodes = True
clay.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.48, .48, .48, 1)
clay.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .5


def attach(obj, name, mat):
    obj.name = name
    obj.parent = asset
    obj.data.materials.append(materials[mat])
    return obj


def finish(obj, bevel=.002):
    if bevel:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        mod = obj.modifiers.new('Millimetre edge radius', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        mod.affect = 'EDGES'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Dominant-axis planar UV in physical metres, .5m repeat period for source tiles.
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name='PhysicalMetreUV')
    uv = obj.data.uv_layers.active.data
    repeat = 1 / .6 if obj.data.materials[0].name == 'Circulation_CreamTile' else 2
    for p in obj.data.polygons:
        axes = [i for i in range(3) if i != max(range(3), key=lambda a: abs(p.normal[a]))]
        for li in p.loop_indices:
            v = obj.data.vertices[obj.data.loops[li].vertex_index].co
            uv[li].uv = (v[axes[0]] * repeat, v[axes[1]] * repeat)
    obj.select_set(False)
    return obj


def box(name, pos, size, mat='Circulation_Steel', bevel=.002):
    # Data API prevents thousands of dependency-graph updates for small tread ribs.
    x, y, z = [v * .5 for v in size]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(-x,-y,-z), (x,-y,-z), (x,y,-z), (-x,y,-z),
                     (-x,-y,z), (x,-y,z), (x,y,z), (-x,y,z)], [],
                    [(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = pos
    attach(obj, name, mat)
    finish(obj, bevel)
    return obj


def profile(name, x0, x1, yz, mat, bevel=.002):
    """Closed side profile extruded across X: genuinely continuous plates, not stair boxes."""
    count = len(yz)
    verts = [(x, y, z) for x in [x0, x1] for y, z in yz]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    attach(obj, name, mat)
    # Recalculate closed-volume normals, including concave profile caps.
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    finish(obj, bevel)
    return obj


def ribbon(name, x0, x1, path, low, high, mat, bevel=.002):
    return profile(name, x0, x1, [(y, z + low) for y, z in path]
                   + [(y, z + high) for y, z in reversed(path)], mat, bevel)


def sweep(name, points, radius=.025, mat='Circulation_Steel', ellipse=None, closed=False):
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    sides = 12
    for i, p in enumerate(pts):
        prev = pts[(i - 1) % len(pts)] if closed or i else pts[i]
        nxt = pts[(i + 1) % len(pts)] if closed or i < len(pts) - 1 else pts[i]
        tangent = (nxt - prev).normalized()
        side = Vector((1, 0, 0)) if abs(tangent.x) < .98 else Vector((0, 1, 0))
        normal = tangent.cross(side).normalized()
        for j in range(sides):
            a = j * math.tau / sides
            rx, rz = ellipse if ellipse else (radius, radius)
            verts.append(p + side * math.cos(a) * rx + normal * math.sin(a) * rz)
    for i in range(len(pts) if closed else len(pts) - 1):
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            c, d = ((i + 1) % len(pts)) * sides + (j + 1) % sides, ((i + 1) % len(pts)) * sides + j
            faces.append((a, b, c, d))
    if not closed:
        faces.extend([tuple(reversed(range(sides))), tuple(range((len(pts) - 1) * sides, len(pts) * sides))])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    attach(obj, name, mat)
    finish(obj, 0)
    for poly in mesh.polygons:
        poly.use_smooth = len(poly.vertices) == 4
    return obj


def anchor(name, pos):
    obj = bpy.data.objects.new(asset.name + '_' + name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = asset
    obj.location = pos
    obj.empty_display_size = .12
    return obj


def root(name, properties):
    global asset
    asset = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(asset)
    asset['unit_metres'] = 1.0
    asset['forward'] = '+Y ascent / Unity -Z after FBX axis conversion'
    asset['pivot'] = 'lower finished floor entrance centre'
    asset['approval'] = 'Asset review only; Unity placement and user Play approval pending'
    for k, v in properties.items():
        asset[k] = v
    return asset


def build_stair():
    r = root('StationStaircase', dict(rise_m=3.6, wall_to_wall_width_m=1.8, clear_between_handrails_m=1.648, total_length_m=11.1,
              riser_m=.15, going_m=.30, risers_per_flight=12, landing_m=1.5,
              dimensions_source='Design assumptions, NOT a Gachon measured survey'))
    # Concrete carrying slab has a sloping underside. No full-height boxes below each tread.
    noseline = [(0, 0), (1.2, 0), (4.8, 1.8), (6.0, 1.8), (9.6, 3.6), (11.1, 3.6)]
    ribbon('Continuous reinforced carrying slab', -.95, .95, noseline, -.42, -.08, 'Circulation_Granite', .003)
    for flight, start, base in [(0, 1.5, 0), (1, 6.3, 1.8)]:
        for i in range(12):
            y, h = start + i * .3, base + (i + 1) * .15
            depth = .3 if i < 11 else 1.5
            box('Tread_%02d' % (flight * 12 + i), (0, y + depth / 2, h - .0975),
                (1.8, depth, .195), 'Circulation_Granite', .0025)
            box('Contrasting nosing_%02d' % (flight * 12 + i), (0, y + .017, h + .001),
                (1.79, .034, .002), 'Circulation_Yellow', .0005)
            anchor('StepContact_%02d' % (flight * 12 + i), (0, y + .05, h))
    box('Lower landing finish', (0, .75, -.035), (1.8, 1.5, .07), 'Circulation_Granite')
    for side in [-1, 1]:
        x = side * 1.0
        ribbon('Continuous tiled parapet_%d' % side, x - .10, x + .10,
               noseline, -.42, 1.0, 'Circulation_CreamTile', .003)
        ribbon('Stone parapet coping_%d' % side, x - .112, x + .112,
               noseline, .995, 1.035, 'Circulation_Granite', .003)
        rail_x = side * .845
        rail_path = [(rail_x, y, z + .9) for y, z in noseline]
        # Ends turn into the wall rather than leaving sharp unsupported cut tubes.
        rail_path = [(side * .90, 0, .9)] + rail_path + [(side * .90, 11.1, 4.5)]
        sweep('Wall-mounted continuous handrail_%d' % side, rail_path, .021)
        for y, z in [(0.5, 0), (1.8, .3), (3.0, .9), (4.4, 1.6), (5.4, 1.8),
                     (6.7, 2.15), (8, 2.8), (9.3, 3.45), (10.6, 3.6)]:
            sweep('Handrail bracket', [(rail_x, y, z + .875), (rail_x, y, z + .79), (side * .90, y, z + .79)], .009)
            box('Handrail fixing plate', (side * .899, y, z + .79), (.005, .052, .08), bevel=.002)
    for y, z in [(.5, 0), (10.55, 3.6)]:
        # Raised dots give an actual warning surface, not a painted sticker.
        box('Landing tactile backing', (0, y, z + .002), (1.78, .6, .004), 'Circulation_Yellow', .001)
        for xi in range(23):
            for yi in range(7):
                p = (-.84 + xi * .076, y - .228 + yi * .076, z + .006)
                sweep('Warning tactile stud', [(p[0], p[1], z + .004), p], .012, 'Circulation_Yellow')
    anchor('EntranceFloor', (0, 0, 0))
    anchor('ExitFloor', (0, 11.1, 3.6))
    anchor('MiddleLanding', (0, 5.55, 1.8))
    return r


def build_escalator():
    rise, run, lower, upper = 3.6, 3.6 * 1.732, 2.239, 2.619
    end = lower + run
    length = run + lower + upper
    k = rise / run
    r = root('StationEscalator', dict(rise_m=rise, step_width_m=1.0, overall_width_m=1.54,
              total_length_m=length, inclination_degrees=30.0, nominal_speed_mps=.5,
              handrail_centres_m=1.238, clear_between_handrails_m=1.158,
              reference='Schindler 9300AE Type20 30K metric layout; generic original geometry',
              dimensions_source='Manufacturer envelope; transition/fittings approximated, installed Gachon brand unknown'))

    def height(y):
        if y <= lower - .5:
            return 0.0
        if y < lower + .5:
            return k * (y - lower + .5) ** 2 / 2
        if y <= end - .5:
            return k * (y - lower)
        if y < end + .5:
            d = y - end + .5
            return rise - k * .5 + k * (d - d * d / 2)
        return rise

    ys = np.linspace(0, length, 160)
    path = [(float(y), height(float(y))) for y in ys]
    ribbon('Continuous closed underside truss casing', -.75, .75, path, -.83, -.22, 'Circulation_Steel', .002)
    # Entrance/exit bearing plates explicitly connect the machine to building supports.
    for y, h in [(.20, 0), (length - .20, rise)]:
        box('Building bearing plate', (0, y, h - .89), (1.54, .20, .12), bevel=.003)
    for side in [-1, 1]:
        ribbon('Continuous inner skirt_%d' % side, side * .506 - .013, side * .506 + .013,
               path, -.20, .26, 'Circulation_Steel', .001)
        ribbon('Outer stainless deck_%d' % side, min(side * .532, side * .77), max(side * .532, side * .77),
               path, .20, .285, 'Circulation_Steel', .002)
        # Side cladding is a true inclined continuous panel, never per-step upright boxes.
        ribbon('Outer cladding_%d' % side, side * .746 - .012, side * .746 + .012,
               path, -.80, .23, 'Circulation_Steel', .002)
        rail_x = side * .619
        start_y, stop_y = .80, length - .80
        top_path = [(rail_x, float(y), height(float(y)) + 1.0)
                    for y in np.linspace(start_y, stop_y, 112)]
        back_arc = [(rail_x, stop_y + math.cos(a) * .41, rise + .59 + math.sin(a) * .41)
                    for a in np.linspace(math.pi / 2, -math.pi / 2, 21)[1:]]
        return_path = [(rail_x, float(y), height(float(y)) + .18)
                       for y in np.linspace(stop_y, start_y, 112)[1:]]
        front_arc = [(rail_x, start_y + math.cos(a) * .41, .59 + math.sin(a) * .41)
                     for a in np.linspace(-math.pi / 2, -3 * math.pi / 2, 21)[1:-1]]
        sweep('Rounded continuous rubber handrail_%d' % side, top_path + back_arc + return_path + front_arc,
              mat='Circulation_Rubber', ellipse=(.040, .027), closed=True)
        # Separate flush safety-glass panes follow the same smooth profile as the rail.
        seams = np.linspace(start_y, stop_y, 7)
        for i in range(6):
            y0, y1 = float(seams[i]), float(seams[i + 1])
            pp = [(float(y), height(float(y))) for y in np.linspace(y0 + .002, y1 - .002, 24)]
            ribbon('10mm safety glass_%d_%d' % (side, i), rail_x - .005, rail_x + .005,
                   pp, .29, .963, 'Circulation_SafetyGlass', .0005)
            if i > 0:
                sweep('Flush balustrade joint', [(rail_x, y0, height(y0) + .3), (rail_x, y0, height(y0) + .96)], .0015, 'Circulation_Rubber')
        # Actual curved newel glazing under the handrail's rounded return.
        for centre_y, h, front in [(start_y, 0, True), (stop_y, rise, False)]:
            angles = np.linspace(math.pi / 2, 3 * math.pi / 2, 32) if front else np.linspace(-math.pi / 2, math.pi / 2, 32)
            yz = [(centre_y + .37 * math.cos(a), h + .59 + .37 * math.sin(a)) for a in angles]
            profile('Curved newel glass', rail_x - .005, rail_x + .005, yz, 'Circulation_SafetyGlass', .0005)
        # Black safety brush lies along the step/skirt gap; direction box is near entrance.
        sweep('Skirt deflector brush', [(side * .50, y, z + .065) for y, z in path],
              .015, 'Circulation_Rubber', ellipse=(.008, .018))
        for y, h in [(start_y, 0), (stop_y, rise)]:
            box('Handrail return inlet', (rail_x, y, h + .22), (.16, .20, .17), 'Circulation_Rubber', .028)
            box('Emergency stop housing', (side * .72, y, h + .35), (.055, .14, .10), 'Circulation_Yellow', .01)
            sweep('Emergency mushroom stop', [(side * .72, y - .071, h + .35), (side * .72, y - .092, h + .35)],
                  .017, 'Circulation_StopRed')
        # Outer fascia sits in front of the deck end faces; never coplanar/z-fighting.
        for y, h in [(-.006, 0), (length + .006, rise)]:
            box('Closed stainless newel end fascia', (side * .643, y, h - .26),
                (.252, .030, 1.10), 'Circulation_Steel', .001)
    # Arc-length placement: .4m pitch ALONG the 30 degree band, not .4m horizontal going.
    arc = np.concatenate([[0], np.cumsum(np.sqrt(np.diff(ys) ** 2 + np.diff([height(float(y)) for y in ys]) ** 2))])
    for i, dist in enumerate(np.arange(.44, arc[-1] - .32, .40)):
        previous_ids = {o.name for o in bpy.data.objects if o.parent == r}
        y = float(np.interp(dist, arc, ys))
        h = height(y)
        box('Step_%02d' % i, (0, y, h - .11), (1.0, .392, .22), 'Circulation_Aluminium', .0015)
        box('Step yellow front_%02d' % i, (0, y - .187, h + .001), (.986, .018, .003), 'Circulation_Yellow', .0005)
        for side in [-1, 1]:
            box('Step yellow side', (side * .491, y, h + .001), (.015, .38, .003), 'Circulation_Yellow', .0005)
        # Real geometry for readable tread/riser grooves, not opaque black texture stripes.
        for j in range(49):
            x = -.472 + j * .01965
            box('Tread rib', (x, y, h + .002), (.009, .354, .004), 'Circulation_Aluminium', 0)
            box('Riser rib', (x, y - .197, h - .10), (.009, .004, .184), 'Circulation_Aluminium', 0)
        anchor('StepSurface_%02d' % i, (0, y, h + .004))
        for part in [o for o in bpy.data.objects if o.parent == r]:
            if part.type == 'MESH' and part.name not in previous_ids:
                part['moving_step'] = i
    for y, h, front in [(.17, 0, True), (length - .18, rise, False)]:
        box('Removable landing access plate', (0, y, h - .021), (1.515, .32, .036), bevel=.001)
        for n in range(4):
            box('Landing anti-slip groove', (0, y - .105 + n * .07, h - .0009), (1.44, .003, .002), 'Circulation_Rubber', 0)
        yy = y + .195 if front else y - .195
        box('Comb carrier', (0, yy, h + .007), (1.05, .034, .016), 'Circulation_Yellow', .001)
        for j in range(49):
            box('Comb tooth', (-.472 + j * .01965, yy + (.027 if front else -.027), h + .008),
                (.009, .055, .008), 'Circulation_Yellow', 0)
    collision = ribbon('CollisionSurface', -.50, .50, path, -.10, 0, 'Circulation_Rubber', 0)
    collision['collision_only'] = True
    collision.hide_render = True
    anchor('EntranceFloor', (0, 0, 0))
    anchor('ExitFloor', (0, length, rise))
    anchor('WellOpeningMin', (-.8, 0, -.96))
    anchor('WellOpeningMax', (.8, length, rise))
    return r


stairs = build_stair()
escalator = build_escalator()
roots = [stairs, escalator]


def children(r):
    return [o for o in bpy.data.objects if o.parent == r]


def export(r):
    objects = children(r)
    # Consolidate stationary components by material; keep the independently moving step band.
    groups = {}
    for obj in objects:
        if obj.type != 'MESH':
            continue
        group = 'CollisionSurface' if obj.get('collision_only') else (('MovingStep_%02d' % obj['moving_step']) if 'moving_step' in obj else 'Static')
        key = (group, obj.data.materials[0].name)
        groups.setdefault(key, []).append(obj)
    copies = []
    step_pivots = {}
    for (group, mat), group_objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        current = []
        for original in group_objects:
            copy = original.copy()
            copy.data = original.data.copy()
            bpy.context.collection.objects.link(copy)
            copy.select_set(True)
            current.append(copy)
        bpy.context.view_layer.objects.active = current[0]
        if len(current) > 1:
            bpy.ops.object.join()
        merged = bpy.context.object
        merged.name = group + '_' + mat
        if group.startswith('MovingStep_'):
            if group not in step_pivots:
                pivot = bpy.data.objects.new(group, None)
                bpy.context.collection.objects.link(pivot)
                pivot.parent = r
                marker = next(o for o in objects if o.type == 'EMPTY' and o.name.endswith('StepSurface_' + group[-2:]))
                pivot.location = marker.location - Vector((0, 0, .004))
                step_pivots[group] = pivot
            # Joined geometry keeps its original object origin. Translate the transform relative
            # to the explicit tread top pivot; all ribs/nosings now move with their own step.
            pivot = step_pivots[group]
            merged.parent = pivot
            merged.location -= pivot.location
        copies.append(merged)
    bpy.ops.object.select_all(action='DESELECT')
    r.select_set(True)
    for obj in copies + list(step_pivots.values()) + [o for o in objects if o.type == 'EMPTY']:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = r
    bpy.ops.export_scene.fbx(filepath=os.path.join(ART, 'Models', r.name + '.fbx'), use_selection=True,
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, use_custom_props=True,
        bake_anim=False, add_leaf_bones=False, path_mode='RELATIVE')
    for obj in copies:
        data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if data.users == 0:
            bpy.data.meshes.remove(data)
    for pivot in step_pivots.values():
        bpy.data.objects.remove(pivot, do_unlink=True)


for r in roots:
    export(r)
    tris = sum(sum(max(1, len(p.vertices) - 2) for p in o.data.polygons) for o in children(r) if o.type == 'MESH')
    print('BLENDER_ASSET', r.name, 'TRIANGLES', tris, 'METRES', r['rise_m'], r['total_length_m'])

# Editable source has components preserved and two non-overlapping asset collections.
stairs.location.x = -2.6
escalator.location.x = 2.6
for img in bpy.data.images:
    if img.filepath:
        img.filepath = bpy.path.relpath(img.filepath, start=os.path.join(ROOT, 'Source~'))
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.use_file_extension = True
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.shading.use_scene_world = False
            space.shading.use_scene_lights = False
            space.clip_end = 200
            space.region_3d.view_location = (0, 5.55, 1.8)
            space.region_3d.view_distance = 17
            space.region_3d.view_rotation = Vector((7, 11, -6)).to_track_quat('-Z', 'Y')
            space.region_3d.view_perspective = 'PERSP'
# Studio source is tidy on reopen; brush data and Blender's optional thumbnail are not assets.
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, 'Source~', 'VerticalCirculation.blend'))

# Generated asset-local renders only: no desktop/Unity/Play-mode capture.
asset = None
bpy.ops.mesh.primitive_plane_add(size=200)
ground = bpy.context.object
ground.name = 'ReviewOnly_Ground'
ground.location.z = -.025
groundmat = bpy.data.materials.new('ReviewNeutralFloor')
groundmat.diffuse_color = (.17, .19, .21, 1)
ground.data.materials.append(groundmat)
review_only.append(ground)
for name, pos, energy, size in [('Key', (-5, 2, 12), 2200, 8), ('Fill', (6, 8, 10), 1500, 7), ('Rim', (0, 15, 10), 1800, 6)]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.shape, data.size = energy, 'DISK', size
    lamp = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(lamp)
    lamp.location = pos
    lamp.rotation_euler = (Vector((0, 5, 1.8)) - lamp.location).to_track_quat('-Z', 'Y').to_euler()
camera = bpy.data.objects.new('AssetReviewCamera', bpy.data.cameras.new('AssetReviewCamera'))
bpy.context.collection.objects.link(camera)
scene.camera = camera


def render(r, label, pos, target, ortho=None, clay_view=False):
    for other in roots:
        for obj in children(other):
            obj.hide_render = other != r
    offset = Vector((r.location.x, 0, 0))
    camera.location = Vector(pos) + offset
    camera.rotation_euler = (Vector(target) + offset - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO' if ortho else 'PERSP'
    camera.data.lens = 28 if label == 'EyeHeight' else 44
    if ortho:
        camera.data.ortho_scale = ortho
    scene.view_layers[0].material_override = clay if clay_view else None
    scene.render.filepath = os.path.join(REVIEW, r.name + '_' + label + '.png')
    bpy.ops.render.render(write_still=True)


if '--no-renders' not in sys.argv:
    for r in roots:
        if '--escalator-review-only' in sys.argv and r != escalator:
            continue
        length = r['total_length_m']
        mid = length / 2
        render(r, 'ClayStructure', (-8, -7, 7), (0, mid, 1.8), clay_view=True)
        render(r, 'Front', (0, -10, 2.4), (0, mid, 1.8), 13)
        render(r, 'Back', (0, length + 10, 5.8), (0, mid, 1.8), 13)
        render(r, 'Left', (-13, mid, 2.0), (0, mid, 1.8), 13)
        render(r, 'Right', (13, mid, 2.0), (0, mid, 1.8), 13)
        render(r, 'Top', (0, mid, 20), (0, mid, 0), 18)
        render(r, 'Contact', (-2.5, -2.0, 1.4), (0, .65, .3))
        render(r, 'EyeHeight', (.14, -.8, 1.62), (0, 4, 2.1))
        render(r, 'MaterialThreeQuarter', (-8, -9, 8), (0, mid, 1.8))
print('CIRCULATION_ASSET_REVIEW_READY', REVIEW)
