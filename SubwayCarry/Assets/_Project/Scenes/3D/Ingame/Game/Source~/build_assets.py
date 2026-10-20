"""Legacy blockout/reference kit, NOT the approved production-asset pipeline.
Asset-first instruction (2026-10-05): do not expand placement from this rough kit.
First replacement assemblies are authored in build_vertical_circulation.py and
VerticalCirculation.blend. Keep existing scene/GUIDs until the full required asset
set is built and reviewed. Static import checks are not visual approval.

Required families before replacement placement: wall/floor/ceiling/columns;
stairs/escalators/lifts; PSD/fare gates; station/exit/line/emergency signs;
lighting/vents; benches/ticket machines/fire and waste facilities; rails;
train exterior/roof/cab/doors/interior/seats/grab poles/handles.
All visible 3D geometry is Blender-authored. One Blender/Unity unit = one metre;
manufacturer dimensions and estimated station dimensions must remain distinct.
The Exit 8 is a visual reference only; no game content is extracted or redistributed.
"""
import bpy, math, os, json, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
PROJECT = os.path.abspath(os.path.join(ROOT, '..', '..', '..', '..'))
OUT = os.path.join(ROOT, 'Art')
os.makedirs(os.path.join(OUT, 'Models'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'Textures'), exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1

COLORS = {'Tile': (.86,.83,.67), 'Stone': (.47,.48,.46), 'Metal': (.55,.58,.59),
          'Glass': (.31,.42,.44), 'Yellow': (.94,.68,.025), 'Rubber': (.055,.06,.065),
          'Seat': (.26,.37,.43), 'Ceiling': (.71,.72,.69), 'Luminous': (.95,.96,.88),
          'Box': (.69,.45,.25), 'Wood': (.48,.29,.11), 'Green': (.075,.31,.19),
          'Brown': (.31,.22,.12), 'Blue': (.025,.14,.3), 'White': (.88,.89,.86),
          'Red': (.6,.04,.025)}
MATS = {}
for name, rgb in COLORS.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = .3 if name == 'Tile' else .58
    bsdf.inputs['Metallic'].default_value = .9 if name == 'Metal' else 0
    MATS[name] = m

modules = {}
current = []
def cube(name, loc, size, mat, bevel=.005):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object; o.name = name; o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # UVs are physical-space planar projections; repeated modular walls line up.
    if not o.data.uv_layers: o.data.uv_layers.new()
    uv = o.data.uv_layers.active.data
    for poly in o.data.polygons:
        axis = max(range(3), key=lambda i: abs(poly.normal[i]))
        axes = [i for i in range(3) if i != axis]
        for li in poly.loop_indices:
            v = o.data.vertices[o.data.loops[li].vertex_index].co + Vector(loc)
            uv[li].uv = (v[axes[0]], v[axes[1]])
    o.data.materials.append(MATS[mat])
    if bevel:
        mod = o.modifiers.new('Physical edge bevel', 'BEVEL'); mod.width = bevel; mod.segments = 3
        mod.affect = 'EDGES'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    current.append(o); return o

def tube(name, a, b, radius=.024, mat='Metal', vertices=16):
    a,b = Vector(a), Vector(b); delta=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=delta.length, location=(a+b)*.5)
    o=bpy.context.object; o.name=name; o.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    o.data.materials.append(MATS[mat]); current.append(o); return o

def module(name, fn):
    global current
    current=[]; fn(); objects=list(current)
    root=bpy.data.objects.new(name, None); bpy.context.collection.objects.link(root)
    for o in objects: o.parent=root
    # Export one mesh per material; keep the fully editable parts in the .blend source.
    batches=[]
    for material in sorted({o.data.materials[0].name for o in objects}):
        bpy.ops.object.select_all(action='DESELECT')
        copies=[]
        for o in objects:
            if o.data.materials[0].name!=material: continue
            copy=o.copy(); copy.data=o.data.copy(); bpy.context.collection.objects.link(copy)
            copy.select_set(True); copies.append(copy)
        bpy.context.view_layer.objects.active=copies[0]
        if len(copies)>1: bpy.ops.object.join()
        merged=bpy.context.object; merged.name=name+'_'+material; batches.append(merged)
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for o in batches: o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Models',name+'.fbx'), use_selection=True,
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_anim=False, add_leaf_bones=False)
    modules[name]={'objects':len(objects),'triangles':sum(len(o.data.polygons)*2 for o in objects)}
    for o in batches:
        mesh=o.data; bpy.data.objects.remove(o,do_unlink=True)
        if mesh.users==0: bpy.data.meshes.remove(mesh)
    for o in objects: o.hide_set(True)

def station_wall():
    cube('Cream ceramic facing',(0,0,1.5),(2,.18,3),'Tile')
    cube('Stone skirting',(0,-.106,.13),(2,.045,.26),'Stone')
    cube('Brown tile band',(0,-.105,1.16),(2,.035,.15),'Brown',.002)
    cube('Yellow ceiling fascia',(0,-.106,2.72),(2,.035,.23),'Yellow',.002)
module('WallPanel', station_wall)
module('ConcourseWall',lambda:(cube('Green facing',(0,0,1.5),(2,.18,3),'Green'),cube('Granite skirting',(0,-.11,.12),(2,.04,.24),'Stone')))
module('FloorPanel', lambda: cube('Terrazzo floor',(0,0,-.1),(2,2,.2),'Stone'))
def suspended_ceiling():
    for x in [-.75,-.25,.25,.75]:
        for y in [-.75,-.25,.25,.75]:
            cube('Acoustic tile',(x,y,3.025),(.49,.49,.055),'Ceiling',.001)
    for p in [-1,-.5,0,.5,1]:
        cube('Exposed T runner',(p,0,2.992),(.012,2,.015),'Metal',.001)
        cube('Exposed cross runner',(0,p,2.992),(2,.012,.015),'Metal',.001)
module('CeilingPanel', suspended_ceiling)
def wood_ceiling():
    cube('Dark ceiling substrate',(0,0,3.07),(2,2,.07),'Rubber',.001)
    for i in range(8): cube('Timber ceiling batten',(0,-.875+i*.25,3.01),(2,.205,.08),'Wood',.002)
module('ConcourseCeiling',wood_ceiling)
module('Pillar', lambda: (cube('Tile clad column',(0,0,1.5),(.65,.65,3),'Tile',.018),cube('Column plinth',(0,0,.14),(.69,.69,.28),'Stone',.009)))

def light():
    cube('Housing',(0,0,2.96),(1.35,.28,.12),'Metal',.016)
    cube('Diffuser',(0,0,2.887),(1.27,.235,.032),'Luminous',.007)
    for x in [-.63,.63]: cube('End bracket',(x,0,2.94),(.045,.29,.16),'Ceiling')
module('FluorescentLight',light)

def bench(train=False):
    for i in range(3):
        x=(i-1)*.56
        cube('Seat cushion '+str(i),(x,0,.45),(.54,.48,.1),'Seat',.045)
        b=cube('Backrest '+str(i),(x,.23,.77),(.54,.08,.54),'Seat',.035); b.rotation_euler.x=math.radians(7)
    for x in [-.8,.8]:
        tube('Rear leg',(x,.15,.08),(x,.15,.42),.027)
        tube('Front leg',(x,-.14,.08),(x,-.14,.42),.027)
        tube('Arm upright',(x,-.14,.45),(x,-.14,.68),.021)
        tube('Arm rest',(x,-.14,.68),(x,.22,.68),.025)
        cube('Floor foot',(x,0,.025),(.1,.42,.05),'Metal')
    tube('Crossbar',(-.81,.1,.35),(.81,.1,.35),.03)
def station_bench():
    for i in range(6): cube('Wood seat slat',(0,-.225+i*.09,.45),(1.74,.077,.075),'Wood',.012)
    for x in [-.68,.68]:
        cube('Stainless pedestal',(x,0,.235),(.085,.34,.395),'Metal',.012)
        cube('Bolted floor plate',(x,0,.035),(.2,.42,.07),'Metal',.01)
    cube('Seat support beam',(0,0,.386),(1.62,.09,.08),'Metal',.008)
module('StationBench',station_bench)
module('TrainBench',lambda:bench(True))

def gate():
    cube('Steel body',(0,0,.53),(.3,1.15,1.06),'Metal',.045)
    cube('Base',(0,0,.08),(.34,1.2,.16),'Rubber',.012)
    c=cube('Card reader',(0,-.28,1.075),(.21,.24,.035),'Rubber',.012)
    cube('Reader light',(0,-.28,1.096),(.12,.15,.009),'Luminous',.005)
    cube('Gate status',(0,-.585,.78),(.19,.018,.07),'Yellow')
module('FareGate',gate)

def tactile():
    cube('Yellow backing',(0,0,.007),(.6,.6,.014),'Yellow',.002)
    for x in range(6):
        for y in range(6): tube('Stud',(x*.09-.225,y*.09-.225,.012),(x*.09-.225,y*.09-.225,.019),.012,'Yellow',12)
module('TactileBlock',tactile)

def panel():
    cube('Door lower panel',(0,0,.515),(.65,.075,1.03),'Metal',.012)
    cube('Door upper panel',(0,0,1.955),(.65,.075,.29),'Metal',.012)
    for x in [-.291,.291]: cube('Door window stile',(x,0,1.42),(.068,.075,.78),'Metal',.009)
    for x in [-.239,.239]: cube('Window vertical seal',(x,-.044,1.42),(.022,.023,.73),'Rubber',.004)
    for z in [1.065,1.775]: cube('Window horizontal seal',(0,-.044,z),(.5,.023,.022),'Rubber',.004)
    cube('Window',(0,-.058,1.42),(.455,.009,.685),'Glass',.004)
    cube('Edge seal',(.322,0,1.05),(.017,.081,2.1),'Rubber',.002)
module('SlidingDoor',panel)

def screen_door():
    # Platform screen doors are mostly glass, not recycled opaque train doors.
    for x in [-.306,.306]: cube('Slim steel door stile',(x,0,1.2),(.038,.055,2.4),'Metal',.004)
    for z in [.12,2.33]: cube('Steel door rail',(0,0,z),(.65,.055,.08),'Metal',.004)
    cube('Clear safety glass',(0,0,1.22),(.572,.012,2.08),'Glass',.002)
    cube('Safety glass identification strip',(0,-.012,.83),(.57,.013,.035),'White',.001)
    cube('Door lower band',(0,0,.27),(.572,.052,.25),'White',.004)
    cube('Meeting edge seal',(.323,0,1.2),(.009,.06,2.4),'Rubber',.001)
module('PlatformDoor',screen_door)

def screen_fixed():
    for x in [-.974,.974]: cube('Fixed glazing upright',(x,0,1.2),(.052,.075,2.4),'Metal',.006)
    for z in [.04,2.35]: cube('Fixed glazing rail',(0,0,z),(2,.075,.09),'Metal',.006)
    cube('Fixed safety glass',(0,0,1.2),(1.896,.012,2.2),'Glass',.002)
    cube('Glass lower band',(0,0,.27),(1.895,.035,.25),'White',.004)
    cube('Glass visibility stripe',(0,-.012,.83),(1.895,.013,.035),'White',.001)
module('PlatformGlazing',screen_fixed)

def screen_header():
    cube('PSD drive housing',(0,0,2.54),(2,.3,.3),'Metal',.008)
    cube('Yellow line identification',(0,-.16,2.47),(2,.012,.062),'Yellow',.001)
    cube('Door service cover',(0,-.159,2.59),(1.84,.01,.12),'White',.002)
module('PlatformHeader',screen_header)

def escalator():
    # Actual inclined truss, comb plates and parallel balustrades; pivot at lower landing.
    for i in range(24):
        h=(i+1)*.15
        cube('Metal step %02d'%i,(0,(i+.5)*.28,h/2),(.9,.28,h),'Metal',.003)
        cube('Step safety edge',(0,i*.28+.013,h+.002),(.88,.024,.006),'Yellow',.001)
        for j in range(12): cube('Step groove',(j*.066-.365,(i+.5)*.28,h+.004),(.009,.24,.004),'Rubber',.001)
    for s in [-1,1]:
        for i in range(24):
            h=(i+1)*.15
            cube('Stainless skirt',(s*.51,(i+.5)*.28,h+.15),(.11,.28,.3),'Metal',.004)
            cube('Glass balustrade',(s*.54,(i+.5)*.28,h+.59),(.024,.28,.76),'Glass',.001)
        tube('Continuous moving handrail',(s*.54,.1,1.04),(s*.54,6.65,4.54),.045,'Rubber')
        cube('Lower comb plate',(s*.2,-.24,.015),(.4,.45,.03),'Metal',.002)
module('EscalatorFlight',escalator)

def elevator_front():
    for x in [-.8,.8]: cube('Lift jamb',(x,0,1.2),(.15,.14,2.4),'Metal',.008)
    cube('Lift header',(0,0,2.41),(1.75,.16,.2),'Metal',.01)
    for x in [-.35,.35]: cube('Lift closed sliding leaf',(x,0,1.17),(.69,.045,2.3),'Metal',.006)
    cube('Lift call panel',(.94,-.04,1.1),(.12,.07,.34),'Rubber',.008)
    tube('Lift call button',(.94,-.082,1.13),(.94,-.094,1.13),.026,'Luminous')
    cube('Lift position display',(0,-.085,2.39),(.35,.012,.12),'Rubber',.004)
module('ElevatorFront',elevator_front)

def notice_board():
    cube('Map board frame',(0,0,1.55),(1.56,.08,1.14),'Metal',.014)
    cube('Map board face',(0,-.052,1.55),(1.48,.018,1.06),'White',.001)
    cube('Map board blue title',(0,-.066,2.03),(1.48,.01,.1),'Blue',.001)
module('NoticeBoard',notice_board)

def fire_cabinet():
    cube('Fire equipment cabinet',(0,0,.8),(.7,.32,1.6),'White',.012)
    cube('Hose door',(0,-.175,1.14),(.59,.025,.64),'Metal',.004)
    cube('Extinguisher door',(0,-.175,.38),(.59,.025,.57),'Glass',.004)
    cube('Emergency location light',(0,0,1.64),(.14,.14,.08),'Red',.025)
    tube('Extinguisher cylinder',(0,0,.12),(0,0,.57),.085,'Red')
module('FireCabinet',fire_cabinet)

def ticket_machine():
    cube('Ticket machine chassis',(0,0,.8),(.72,.56,1.6),'Metal',.025)
    cube('Touchscreen',(0,-.3,1.22),(.47,.025,.36),'Blue',.008)
    cube('Cash input',(.2,-.299,.88),(.13,.03,.065),'Rubber',.004)
    cube('Ticket output',(0,-.299,.54),(.28,.03,.095),'Rubber',.004)
    cube('Machine title',(0,-.294,1.51),(.6,.012,.1),'Yellow',.002)
module('TicketMachine',ticket_machine)

def window():
    cube('Lower shell',(0,0,.48),(1.8,.17,.96),'Metal',.018)
    cube('Upper shell',(0,0,2.09),(1.8,.17,.3),'Metal',.015)
    for x in [-.85,.85]: cube('Window pillar',(x,0,1.48),(.1,.17,1.06),'Metal')
    for x in [-.79,.79]: cube('Rubber window vertical',(x,-.015,1.46),(.04,.09,1.04),'Rubber',.006)
    for z in [.96,1.96]: cube('Rubber window horizontal',(0,-.015,z),(1.6,.09,.04),'Rubber',.006)
    cube('Train glazing',(0,-.071,1.46),(1.54,.008,.95),'Glass',.008)
    cube('Line stripe',(0,-.093,.75),(1.8,.012,.105),'Yellow',.002)
module('TrainWindow',window)

def nose():
    cube('Cab body',(0,0,1.1),(2.85,1.1,2.2),'Metal',.16)
    cube('Front windshield',(0,-.56,1.55),(2.18,.02,.73),'Glass',.06)
    cube('Front line stripe',(0,-.59,.69),(2.7,.025,.19),'Yellow')
    for x in [-1,1]: cube('Headlight',(x,-.59,.45),(.24,.035,.12),'Luminous',.025)
    cube('Bumper',(0,-.61,.17),(2.4,.17,.23),'Rubber',.04)
module('TrainCab',nose)

def vent():
    cube('Vent frame',(0,0,1.4),(.85,.04,.52),'Metal')
    for i in range(12): cube('Louvre',(0,-.026,1.17+i*.042),(.78,.025,.018),'Rubber',.001)
module('VentPanel',vent)

def service():
    cube('Door',(0,0,1.03),(.9,.06,2.06),'Metal',.008)
    for x in [-.48,.48]: cube('Door frame',(x,0,1.06),(.06,.13,2.12),'Rubber')
    cube('Header',(0,0,2.13),(1.02,.13,.06),'Rubber')
    tube('Handle spindle',(.32,-.037,1.04),(.32,-.08,1.04),.012)
    tube('Handle grip',(.2,-.08,1.04),(.32,-.08,1.04),.012)
module('ServiceDoor',service)

def package():
    cube('Cardboard body',(0,0,.13),(.38,.38,.26),'Box',.006)
    cube('Lid',(0,0,.272),(.392,.392,.027),'Box',.003)
    cube('Ribbon longitudinal',(0,0,.288),(.035,.396,.006),'Yellow',.001)
    cube('Ribbon transverse',(0,0,.289),(.396,.035,.006),'Yellow',.001)
module('CakeBox',package)

def stairs():
    # Pivot is the bottom landing. Rise 3.6m, run 6.72m, 24 usable treads.
    for i in range(24):
        h=(i+1)*.15
        cube('Stone tread %02d'%i,(0,(i+.5)*.28,h/2),(2.4,.28,h),'Stone',.003)
        cube('Yellow stair nosing %02d'%i,(0,i*.28+.018,h+.002),(2.35,.035,.004),'Yellow',.001)
    for side in [-1,1]:
        for i in range(24):
            cube('Stair side guard %02d'%i,(side*1.25,(i+.5)*.28,(i+1)*.15+.39),(.1,.28,.78),'Tile',.003)
        for i in [0,6,12,18,23]:
            y=(i+.5)*.28; h=(i+1)*.15
            tube('Rail post',(side*1.16,y,h),(side*1.16,y,h+.9),.022)
        tube('Continuous handrail',(side*1.16,.14,1.05),(side*1.16,6.58,4.5),.028)
module('StairFlight',stairs)

def post():
    tube('Upright',(0,0,0),(0,0,2.25),.026)
    cube('Floor flange',(0,0,.015),(.11,.11,.03),'Metal')
    cube('Ceiling flange',(0,0,2.235),(.11,.11,.03),'Metal')
module('SupportPole',post)

# Source scene retains editable geometry, arranged by module rather than overlapping.
for i,name in enumerate(modules):
    bpy.data.objects[name].location=(i%4*10,i//4*11,0)
for o in bpy.data.objects: o.hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Source~','StationKit.blend'))
with open(os.path.join(PROJECT,'Temp','ThreeDRebuild','asset_metrics.json'),'w',encoding='utf8') as f:
    json.dump(modules,f,indent=2)

if '--station-only' in sys.argv:
    print('STATION_KIT_COMPLETE',len(modules))
    raise SystemExit(0)

# Original CC0 human, plus a same-rig seated animation. Keep source FBX unchanged.
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.path.join(PROJECT,'Assets','ThirdParty','Quaternius','AnimatedHuman','AnimatedHuman.fbx'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.data.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
for o in list(bpy.data.objects):
    if o not in meshes and o != arm: bpy.data.objects.remove(o,do_unlink=True)
idle=next(a for a in bpy.data.actions if a.name.endswith('|Idle'))
walk=next(a for a in bpy.data.actions if a.name.endswith('|Walk'))
idle.name='Idle'; walk.name='Walk'
for a in list(bpy.data.actions):
    if a not in (idle,walk): bpy.data.actions.remove(a)
arm.animation_data.action=idle; bpy.context.scene.frame_set(1)
deps=bpy.context.evaluated_depsgraph_get()
points=[o.matrix_world @ v.co for o in meshes for v in o.evaluated_get(deps).data.vertices]
low=min(v.z for v in points); high=max(v.z for v in points)
height=high-low
if height < .1: raise RuntimeError('Invalid original human vertical scale')
scale=1.74/height
root=bpy.data.objects.new('PassengerRoot',None); bpy.context.collection.objects.link(root)
arm.parent=root
for o in meshes:
    if not o.parent: o.parent=root
root.scale=(scale,)*3; root.location.z=-low*scale
# FBX rest facing is local +X. Determine world facing from the actual foot/toe bones.
forward=arm.matrix_world.to_3x3() @ (arm.data.bones['LeftToeBase'].head_local-arm.data.bones['LeftFoot'].head_local)
angle=math.atan2(forward.x,-forward.y)
root.rotation_euler.z=-angle
arm.animation_data.action=None
for p in arm.pose.bones: p.matrix_basis=Matrix.Identity(4)
sit=bpy.data.actions.new('Sit'); arm.animation_data.action=sit
def aim(bone,direction):
    p=arm.pose.bones[bone]; b=p.bone
    q=(b.tail_local-b.head_local).normalized().rotation_difference(Vector(direction).normalized())
    mat=q.to_matrix().to_4x4() @ b.matrix_local
    mat.translation=p.head
    p.matrix=mat; bpy.context.view_layer.update()
hips=arm.pose.bones['Hips']; hips.location.y=-1.95
bpy.context.view_layer.update()
for side in ['Left','Right']:
    aim(side+'UpLeg',(1,-.09,0))
    aim(side+'Leg',(0,-1,0))
    aim(side+'Foot',(1,-.22,0))
    aim(side+'Arm',(.12,-1,0))
    aim(side+'ForeArm',(.8,-.22,0))
for frame in [1,49]:
    for p in arm.pose.bones:
        p.rotation_mode='QUATERNION'
        p.keyframe_insert('rotation_quaternion',frame=frame)
        p.keyframe_insert('location',frame=frame)
bpy.context.scene.frame_set(1)
deps=bpy.context.evaluated_depsgraph_get()
sit_points=[o.matrix_world @ v.co for o in meshes for v in o.evaluated_get(deps).data.vertices]
sit_low=min(v.z for v in sit_points)
up_axis=arm.matrix_world.to_3x3() @ hips.bone.matrix_local.to_3x3() @ Vector((0,1,0))
if abs(up_axis.z)>.0001:
    hips.location.y-=sit_low/up_axis.z
    for frame in [1,49]: hips.keyframe_insert('location',frame=frame)
arm.animation_data.action=idle
for o in bpy.data.objects: o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Models','PassengerHuman.fbx'),use_selection=True,
    axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True)
print('STATION_KIT_COMPLETE',len(modules),'HUMAN_SCALE',scale)
