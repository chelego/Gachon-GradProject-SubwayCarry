"""Editable Blender station/train production kit. 1 unit = 1 metre.
Replaces only this owned Gameplay3D kit's exports; preserves paths and Unity GUIDs.
Front = Blender -Y (Unity +Z). Train longitudinal +Unity Z = Blender -Y.
References (observation, not copied textures / licensed game content):
https://commons.wikimedia.org/wiki/File:Korail-Bundang-line-K223-Gachon-university-station-platform-20181124-152922.jpg
https://commons.wikimedia.org/wiki/File:Korail_Class_351000_EMU_3rd_batch.jpg
https://commons.wikimedia.org/wiki/File:KORAIL_351067_INSIDE.JPG
https://station.kric.go.kr/hc/ext/images/visual/handicapped/cnv/KR/KR_K1_0011.png
Envelope/finishes are design estimates, not surveyed Gachon vehicle/station dimensions.
Circulation manufacturer envelope is documented separately in VerticalCirculation.blend.
All geometry, UV and surface maps are authored here in Blender, no Unity primitive art.
"""
import bpy, math, os, sys, numpy as np
from mathutils import Vector

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
ART=os.path.join(ROOT,'Art'); PROJECT=os.path.abspath(os.path.join(ROOT,'../../../..'))
REVIEW=os.path.join(PROJECT,'Temp','ThreeDRebuild','KitReview')
os.makedirs(REVIEW,exist_ok=True)
os.makedirs(os.path.join(ART,'Textures','Metro'),exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
s=bpy.context.scene; s.unit_settings.system='METRIC'; s.unit_settings.scale_length=1
s.render.engine='CYCLES'; s.cycles.samples=16; s.cycles.use_denoising=True
s.render.resolution_percentage=100; s.render.image_settings.file_format='PNG'
s.view_settings.view_transform='AgX'; s.world.use_nodes=True
s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
with bpy.data.libraries.load(os.path.join(ROOT,'Source~','VerticalCirculation.blend'),link=False) as (src,dst):
    dst.materials=[n for n in src.materials if n.startswith('Circulation_')]
M={m.name:m for m in bpy.data.materials if m.name.startswith('Circulation_')}
aliases={'Metal':'Circulation_Steel','Stone':'Circulation_Granite','Tile':'Circulation_CreamTile',
         'Glass':'Circulation_SafetyGlass','Yellow':'Circulation_Yellow','Rubber':'Circulation_Rubber','Red':'Circulation_StopRed'}

def image(name,arr,data=True):
    if arr.ndim==2: arr=np.stack([arr,arr,arr],axis=-1)
    if arr.shape[-1]==3: arr=np.concatenate([arr,np.ones((*arr.shape[:2],1))],axis=-1)
    im=bpy.data.images.new(name,width=arr.shape[1],height=arr.shape[0],alpha=True)
    im.colorspace_settings.name='Non-Color' if data else 'sRGB'
    im.pixels.foreach_set(np.asarray(np.clip(arr,0,1),np.float32).ravel())
    im.filepath_raw=os.path.join(ART,'Textures','Metro',name+'.png'); im.file_format='PNG'; im.save()
    return im

def material(name,color,rough=.5,kind='paint',emission=0):
    n=512; y,x=np.mgrid[0:n,0:n]/n; rng=np.random.default_rng(51)
    noise=rng.normal(0,.25,(n,n)); change=noise*.008; roughmap=rough+noise*.025
    if kind=='wood':
        change+=np.sin(x*130+np.sin(y*7)*2)*.035+np.sin(x*590+y*5)*.012
        roughmap+=np.sin(x*130+np.sin(y*7)*2)*.035
    if kind=='fabric':
        weave=np.sin(x*math.pi*256)*np.sin(y*math.pi*256)
        change+=weave*.012; roughmap+=weave*.03
    base=np.stack([c+change for c in color],-1)
    normal=np.dstack([np.full((n,n),.5),np.full((n,n),.5),np.ones((n,n))])
    if kind=='floor':
        grout=(x<.003)|(y<.003)|(x>.997)|(y>.997)
        base[grout]=(.32,.33,.32); roughmap[grout]=.85
        normal[x<.006,0]=.57; normal[y<.006,1]=.57
    maps={'BaseColor':image(name+'_BaseColor',base,False),'Normal':image(name+'_Normal',normal),
          'Roughness':image(name+'_Roughness',roughmap),'Metallic':image(name+'_Metallic',np.zeros((n,n))), 'AO':image(name+'_AO',np.ones((n,n)))}
    packed=np.zeros((n,n,4)); packed[...,3]=1-np.clip(roughmap,0,1); image(name+'_MetallicSmoothness',packed)
    mat=bpy.data.materials.new(name); mat.use_nodes=True; mat.diffuse_color=(*color,1)
    nodes,links=mat.node_tree.nodes,mat.node_tree.links; bs=nodes['Principled BSDF']
    for ch,socket in [('BaseColor','Base Color'),('Roughness','Roughness'),('Metallic','Metallic')]:
        node=nodes.new('ShaderNodeTexImage'); node.image=maps[ch]; links.new(node.outputs['Color'],bs.inputs[socket])
    if emission: bs.inputs['Emission Color'].default_value=(*color,1); bs.inputs['Emission Strength'].default_value=emission
    M[name]=mat

for name,c,r,k in [('White',(.76,.78,.74),.42,'paint'),('Green',(.075,.24,.17),.48,'paint'),
                    ('Wood',(.43,.26,.12),.49,'wood'),('Blue',(.025,.08,.2),.44,'paint'),
                    ('Seat',(.16,.26,.32),.84,'fabric'),('Brown',(.24,.18,.12),.56,'paint'),
                    ('Luminous',(.87,.93,1),.33,'paint'),('Ballast',(.14,.15,.14),.9,'paint')]:
    material('Metro_'+name,c,r,k,2 if name=='Luminous' else 0); aliases[name]='Metro_'+name
material('Metro_Floor',(.46,.48,.47),.62,'floor')
material('Metro_Cardboard',(.57,.39,.22),.82)
root=None; roots=[]

def uv(o):
    layer=o.data.uv_layers.new(name='MetreUV') if not o.data.uv_layers else o.data.uv_layers.active
    repeat=1/.6 if o.data.materials[0].name in ['Circulation_CreamTile','Metro_Floor'] else 2
    for p in o.data.polygons:
        axes=[i for i in range(3) if i!=max(range(3),key=lambda a:abs(p.normal[a]))]
        for li in p.loop_indices:
            v=o.matrix_local@o.data.vertices[o.data.loops[li].vertex_index].co
            layer.data[li].uv=(v[axes[0]]*repeat,v[axes[1]]*repeat)

def mesh(name,verts,faces,mat='Metal',bevel=.003):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    o=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(o); o.parent=root
    data.materials.append(M[aliases.get(mat,mat)])
    if bevel:
        bevel=min(bevel,min(o.dimensions)*.24)
        bpy.context.view_layer.objects.active=o; o.select_set(True)
        mod=o.modifiers.new('Millimetre edge radius','BEVEL'); mod.width=bevel; mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name); o.select_set(False)
    uv(o); return o

def box(name,p,d,mat='Metal',bevel=.003):
    x,y,z=[v/2 for v in d]; a,b,c=p
    return mesh(name,[(a+u,b+v,c+w) for u,v,w in [(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]],
                [(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,bevel)

def tube(name,a,b,r=.02,mat='Metal',count=16):
    a,b=Vector(a),Vector(b); axis=(b-a).normalized(); cross=axis.cross(Vector((0,0,1)))
    if cross.length<.01: cross=axis.cross(Vector((0,1,0)))
    cross.normalize(); v=axis.cross(cross)
    verts=[tuple(p+r*(math.cos(i*2*math.pi/count)*cross+math.sin(i*2*math.pi/count)*v)) for p in [a,b] for i in range(count)]
    faces=[tuple(reversed(range(count))),tuple(range(count,count*2))]+[(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)]
    return mesh(name,verts,faces,mat,0)

def poly(name,xy,z0,z1,mat='Metal',bevel=.004):
    n=len(xy); verts=[(x,y,z) for z in [z0,z1] for x,y in xy]
    return mesh(name,verts,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat,bevel)

def begin(name):
    global root
    root=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(root)
    root['unit_metres']=1.; root['dimensions_source']='Design estimate from references; not a measured site survey'
    root['pivot']='finished floor / installation centre'; roots.append(root)

def anchor(name,p):
    o=bpy.data.objects.new(root.name+'_'+name,None); bpy.context.collection.objects.link(o); o.parent=root; o.location=p; return o

def end():
    objects=list(root.children); copies=[]
    for mat in sorted({o.data.materials[0].name for o in objects if o.type=='MESH'}):
        bpy.ops.object.select_all(action='DESELECT'); group=[]
        for original in objects:
            if original.type!='MESH' or original.data.materials[0].name!=mat: continue
            o=original.copy(); o.data=original.data.copy(); bpy.context.collection.objects.link(o); o.select_set(True); group.append(o)
        bpy.context.view_layer.objects.active=group[0]
        if len(group)>1: bpy.ops.object.join()
        o=bpy.context.object; o.name=root.name+'_'+mat; copies.append(o)
    bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
    for o in copies+[o for o in objects if o.type=='EMPTY']: o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=os.path.join(ART,'Models',root.name+'.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',
                             apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='RELATIVE')
    for o in copies:
        data=o.data; bpy.data.objects.remove(o,do_unlink=True)
        if data.users==0: bpy.data.meshes.remove(data)
    print('METRE_KIT',root.name,'TRIS',sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects if o.type=='MESH'),flush=True)

for name,concourse in [('WallPanel',False),('ConcourseWall',True)]:
    begin(name); box('Solid backing',(0,0,1.5),(2,.16,3),'Tile' if not concourse else 'Green',.001)
    box('Granite skirting',(0,-.096,.12),(2,.035,.24),'Stone',.002)
    if not concourse:
        box('Brown tile band',(0,-.086,1.18),(2,.014,.15),'Brown',.001)
        box('Line colour fascia',(0,-.086,2.74),(2,.014,.20),'Yellow',.001)
    end()
begin('FloorPanel'); box('Finished floor',(0,0,-.1),(2,2,.2),'Metro_Floor',0); end()
for name,wood in [('CeilingPanel',False),('ConcourseCeiling',True)]:
    begin(name); box('Ceiling support backing',(0,0,3.045),(2,2,.05),'Brown' if wood else 'White',0)
    if wood:
        for x in range(20): box('Timber linear baffle',(-.95+x*.1,0,3),( .072,2,.05),'Wood',.002)
    else:
        for x in range(4):
            for y in range(4): box('Acoustic panel',(-.75+x*.5,-.75+y*.5,3.008),(.49,.49,.035),'White',.001)
        for p in [-1,-.5,0,.5,1]:
            box('T runner',(p,0,2.984),(.012,2,.014),'Metal',.001); box('Cross runner',(0,p,2.983),(2,.012,.014),'Metal',.001)
    end()
begin('Pillar'); box('Ceramic column',(0,0,1.5),(.65,.65,3),'Tile',.007)
box('Granite plinth',(0,0,.12),(.68,.68,.24),'Stone',.004); end()
begin('FluorescentLight')
box('Ceiling surface mounted housing',(0,0,2.94),(1.35,.22,.09),'Metal',.012)
for x in [-.48,.48]: box('Ceiling fixing pad',(x,0,2.992),(.12,.13,.024),'White',.003)
for y in [-.062,.062]:
    tube('Fluorescent diffuser',(-.59,y,2.895),(.59,y,2.895),.018,'Luminous')
    for x in [-.618,.618]: box('Tube end holder',(x,y,2.9),(.045,.045,.05),'White',.004)
end()
begin('VentPanel'); box('Louvre frame',(0,0,1.4),(.85,.04,.52),'Metal',.006)
box('Dark air plenum',(0,.018,1.4),(.79,.02,.46),'Rubber',0)
for i in range(12): box('Angled louvre',(0,-.024,1.18+i*.04),(.78,.038,.02),'Metal',.001)
end()

begin('StationBench')
for i in range(6): box('Rounded hardwood slat',(0,-.225+i*.09,.45),(1.74,.077,.075),'Wood',.010)
for x in [-.67,.67]:
    box('Steel pedestal',(x,0,.23),(.08,.30,.39),'Metal',.007); box('Floor fixing plate',(x,0,.027),(.20,.37,.054),'Metal',.004)
    for y in [-.12,.12]: tube('Anchor bolt',(x,y,.049),(x,y,.060),.008)
box('Continuous carrying beam',(0,0,.385),(1.65,.10,.08),'Metal',.005); end()
begin('TrainBench')
# Five sculpted cushions, reclined backs, supported frame. Front -Y, inward facing in carriage.
for i in range(5):
    x=(i-2)*.53
    box('Fabric cushion',(x,-.025,.455),(.515,.49,.11),'Seat',.035)
    o=box('Upholstered back',(x,.214,.76),(.515,.075,.54),'Seat',.024)
    # Inclined back is modeled in vertices; bottom meets the cushion, not a detached split.
    for v in o.data.vertices: v.co.y+=(v.co.z-.50)*.13
    o.data.update(); uv(o)
for x in [-1.35,1.35]:
    box('Seat end screen',(x,.055,.67),(.045,.54,.48),'White',.025)
    tube('End grab rail',(x,-.22,.69),(x,-.22,1.62),.018)
    # Closed return to the seat screen: not a free-floating tube with an unsupported top.
    rail=[(x,-.22,1.62),(x,-.214,1.648),(x,-.192,1.67),(x,-.164,1.676),
          (x,.204,1.676),(x,.232,1.67),(x,.254,1.648),(x,.26,1.62),(x,.26,.88)]
    for a,b in zip(rail,rail[1:]): tube('Seat screen return rail',a,b,.018)
    box('Seat rail fixing shoe',(x,.26,.905),(.055,.055,.032),'Metal',.004)
    tube('Seat support leg',(x*.72,.06,.05),(x*.72,.06,.41),.025)
    box('Floor fixing foot',(x*.72,.06,.027),(.10,.22,.054),'Metal',.003)
box('Carrying seat frame',(0,.05,.36),(2.7,.27,.06),'Metal',.005)
for i in range(5): anchor('Seat_%d'%i,((i-2)*.53,-.13,0))
end()
begin('FareGate')
poly('Folded stainless housing',[(-.17,-.62),(.17,-.62),(.17,.51),(.12,.62),(-.12,.62),(-.17,.51)],.10,1.02)
box('Removable side cover',(0,0,.59),(.349,.94,.69),'Metal',.004)
box('Bolted pedestal',(0,0,.055),(.36,1.22,.11),'Rubber',.008)
box('Top reader bezel',(0,-.31,1.03),(.23,.28,.035),'Rubber',.018)
box('Card reader glass',(0,-.31,1.051),(.18,.22,.01),'Blue',.012)
box('Reader status LED',(0,-.31,1.057),(.11,.035,.005),'Luminous',.003)
box('Front indicator bezel',(0,-.628,.82),(.17,.018,.13),'Rubber',.008)
box('Green allowed indicator',(0,-.640,.82),(.08,.008,.044),'Green',.003)
end()
begin('FareLeaf'); box('Clear retracting gate leaf',(0,0,.69),(.76,.022,.5),'Glass',.014); end()
begin('GuardRail')
for x in [-.59,.59]:
    tube('Floor fixing post',(x,0,.035),(x,0,1.10),.028)
    box('Bolted base',(x,0,.024),(.12,.12,.048),'Metal',.006)
box('Safety glass panel',(0,0,.59),(1.12,.012,.91),'Glass',.005)
tube('Continuous cap',(-.6,0,1.06),(.6,0,1.06),.024); end()
begin('TactileBlock'); box('Yellow backing',(0,0,.004),(.6,.6,.008),'Yellow',.001)
for x in range(6):
    for y in range(6): tube('Raised warning dot',(x*.09-.225,y*.09-.225,.008),(x*.09-.225,y*.09-.225,.014),.013,'Yellow',12)
end()
begin('TactileGuideBlock'); box('Guide backing',(0,0,.004),(.6,.6,.008),'Yellow',.001)
for x in [-.20,-.10,0,.10,.20]: box('Raised directional bar',(x,0,.012),(.026,.52,.008),'Yellow',.006)
end()
begin('EmergencySign'); box('Emergency sign housing',(0,0,0),(.85,.05,.25),'Metal',.004)
box('Green illuminated face',(0,-.033,0),(.82,.015,.22),'Green',.002); end()
begin('DoorStatusLight'); box('Status lamp bezel',(0,0,0),(.20,.035,.055),'Rubber',.007)
box('Status diffuser',(0,-.022,0),(.16,.012,.034),'Luminous',.005); end()

for name,psd in [('SlidingDoor',False),('PlatformDoor',True),('LiftDoorLeaf',False)]:
    begin(name); width=.65; height=2.4 if psd else 2.10
    if name=='LiftDoorLeaf':
        box('Stainless elevator leaf',(0,0,1.10),(.65,.04,2.20),'Metal',.006)
        box('Meeting seal',(.323,0,1.10),(.007,.048,2.2),'Rubber',.001)
    elif psd:
        for x in [-.306,.306]: box('Door stile',(x,0,1.2),(.038,.055,2.4),'Metal',.004)
        for z in [.04,2.36]: box('Door rail',(0,0,z),(.65,.055,.08),'Metal',.004)
        box('Safety glazing',(0,0,1.2),(.576,.012,2.25),'Glass',.002)
        box('Lower frost band',(0,-.008,.23),(.57,.02,.28),'White',.002)
        box('Visibility band',(0,-.014,.92),(.57,.009,.035),'White',.001)
        box('Meeting seal',(.323,0,1.2),(.009,.06,2.4),'Rubber',.001)
    else:
        box('Lower stainless leaf',(0,0,.505),(.65,.065,1.01),'Metal',.008)
        box('Upper stainless leaf',(0,0,1.965),(.65,.065,.27),'Metal',.007)
        for x in [-.302,.302]: box('Window stile',(x,0,1.42),(.047,.065,.93),'Metal',.005)
        for z in [1.022,1.818]: box('Window rubber gasket',(0,-.034,z),(.56,.015,.036),'Rubber',.005)
        for x in [-.269,.269]: box('Window rubber gasket',(x,-.034,1.42),(.036,.015,.83),'Rubber',.004)
        box('Clear train window',(0,0,1.42),(.51,.009,.78),'Glass',.009)
        box('Meeting seal',(.322,0,1.05),(.012,.078,2.1),'Rubber',.002)
        box('Lower line stripe',(0,-.039,.68),(.65,.008,.09),'Yellow',.001)
    anchor('MeetingEdge',(.323,0,height*.5))
    end()
begin('TrainThreshold')
# One tread at the actual body/platform gap. No second coplanar sill baked into the shell.
box('Single boarding tread',(0,0,-.018),(.44,1.30,.036),'Metal',.002)
for x in [-.15,-.10,-.05,0,.05,.10,.15]:
    box('Anti-slip tread rib',(x,0,.0006),(.012,1.25,.0012),'Rubber',.0002)
for y in [-.61,.61]:
    for x in [-.17,.17]: tube('Flush sill fastener',(x,y,.0001),(x,y,.0008),.004,'Metal',8)
anchor('InnerFloorEdge',(-.22,0,0)); anchor('OuterPlatformEdge',(.22,0,0)); end()
begin('PlatformGlazing')
for x in [-.974,.974]: box('Fixed upright',(x,0,1.2),(.052,.075,2.4),'Metal',.004)
for z in [.04,2.36]: box('Glazing top bottom rail',(0,0,z),(2,.075,.08),'Metal',.003)
box('Clear laminated glass',(0,0,1.2),(1.896,.012,2.24),'Glass',.002)
box('Lower frost band',(0,-.009,.23),(1.895,.014,.28),'White',.001)
box('Visibility band',(0,-.014,.92),(1.895,.009,.035),'White',.001); end()
begin('PlatformHeader'); box('Door drive enclosure',(0,0,2.54),(2,.25,.28),'Metal',.007)
box('Removable inspection cover',(0,-.129,2.55),(1.96,.013,.22),'White',.003)
box('Line stripe',(0,-.138,2.475),(2,.007,.047),'Yellow',.001); end()
begin('DoorPocket')
# Covers the retracted leaf on the platform side, without hiding the fixed glazing.
for x in [-.70,.70]: box('Door pocket jamb',(x,0,1.2),(.09,.10,2.4),'Metal',.004)
box('Motor mounting beam',(0,0,2.38),(1.5,.12,.08),'Metal',.004); end()
begin('WayfindingBoard'); box('Folded sign frame',(0,0,0),(3.5,.08,.55),'Metal',.005)
box('Inset sign face',(0,-.047,0),(3.44,.012,.49),'Blue',.001)
end()
begin('SignSuspension')
tube('Suspension rod',(0,0,0),(0,0,.995),.009)
box('Ceiling mounting plate',(0,0,.995),(.065,.065,.010),'Metal',.001)
end()
begin('StationNameBoard'); box('Yellow border',(0,0,0),(2.8,.035,.66),'Yellow',.011)
box('Inset name face',(0,-.026,0),(2.69,.012,.55),'White',.006); end()
begin('NoticeBoard'); box('Frame',(0,0,1.55),(1.56,.07,1.14),'Metal',.008)
box('White notice backing',(0,-.043,1.55),(1.48,.012,1.06),'White',.002)
box('Title strip',(0,-.052,2.00),(1.48,.008,.12),'Blue',.001); end()
begin('FireCabinet')
box('Cabinet rear shell',(0,.145,.75),(.70,.02,1.5),'White',.003)
for x in [-.34,.34]: box('Cabinet side shell',(x,0,.75),(.02,.31,1.5),'White',.003)
for z in [.01,.73,1.49]: box('Cabinet shelf',(0,0,z),(.70,.31,.02),'White',.003)
box('Upper hose door',(0,-.166,1.10),(.61,.022,.62),'Metal',.004)
box('Extinguisher window',(0,-.166,.36),(.61,.01,.60),'Glass',.003)
tube('Extinguisher body',(-.17,-.04,.08),(-.17,-.04,.51),.078,'Red',24)
tube('Hose',(-.16,-.04,.55),(.02,-.04,.46),.014,'Rubber')
box('Valve grip',(-.17,-.04,.56),(.16,.04,.025),'Metal',.004)
for z in [.37,1.08]: box('Door handle',(.225,-.19,z),(.025,.029,.12),'Metal',.003)
box('Alarm red bezel',(0,-.01,1.55),(.12,.10,.10),'Red',.015); end()
begin('TicketMachine'); poly('Folded ticket kiosk',[(-.36,-.29),(.36,-.29),(.36,.23),(-.36,.23)],.07,1.60)
box('Service plinth',(0,0,.04),(.74,.55,.08),'Rubber',.005)
box('Screen recess',(0,-.303,1.19),(.50,.026,.38),'Rubber',.008)
box('Touchscreen',(0,-.321,1.19),(.45,.008,.33),'Blue',.004)
box('Header label',(0,-.305,1.51),(.62,.012,.11),'Yellow',.002)
for x,z,w in [(.21,.86,.12),(-.18,.86,.16),(0,.53,.31)]:
    box('Payment output bezel',(x,-.312,z),(w+.03,.032,.085),'Rubber',.003)
    box('Slot opening',(x,-.330,z),(w,.007,.025),'Rubber',.001)
box('Accessible tray',(0,-.37,.44),(.39,.20,.027),'Metal',.006); end()
begin('WasteBin')
for x in [-.25,.25]:
    box('Sorted waste body',(x,0,.42),(.46,.40,.80),'Metal',.025)
    box('Top lid',(x,0,.83),(.475,.415,.05),'Blue' if x<0 else 'Green',.018)
    box('Waste aperture',(x,-.09,.857),(.23,.15,.009),'Rubber',.020)
end()
begin('ServiceDoor'); box('Leaf',(0,0,1.03),(.90,.055,2.06),'White',.004)
for x in [-.48,.48]: box('Jamb',(x,0,1.06),(.06,.11,2.12),'Metal',.004)
box('Header',(0,0,2.13),(1.02,.11,.06),'Metal',.004)
tube('Spindle',(.32,-.032,1.02),(.32,-.08,1.02),.012)
tube('Lever',(.20,-.08,1.02),(.32,-.08,1.02),.012); end()
begin('ElevatorFront')
# FRAME ONLY. The movable leaves are separate and never baked into this facade.
for x in [-.75,.75]: box('Lift jamb',(x,0,1.18),(.18,.16,2.36),'Metal',.006)
box('Lift lintel',(0,0,2.38),(1.68,.16,.18),'Metal',.006)
box('Call panel',(.96,-.05,1.08),(.11,.055,.28),'Rubber',.007)
tube('Call button',(.96,-.081,1.1),(.96,-.091,1.1),.024,'Luminous')
box('Position display',(0,-.09,2.38),(.32,.018,.09),'Rubber',.005); end()
begin('LiftCar')
box('Lift cabin floor',(0,0,-.06),(1.94,1.94,.12),'Stone',.002)
for x in [-.97,.97]: box('Cabin side',(x,0,1.17),(.055,1.94,2.34),'Metal',.004)
box('Cabin back',(0,.97,1.17),(1.94,.055,2.34),'Metal',.004)
box('Cabin ceiling',(0,0,2.36),(1.94,1.94,.06),'White',.004)
tube('Back handrail',(-.83,.923,.9),(.83,.923,.9),.019)
for x in [-.84,.84]: tube('Handrail wall fixing',(x,.925,.9),(x,.951,.9),.012)
box('Inside button panel',(.90,-.48,1.10),(.025,.22,.30),'Rubber',.004)
for z in [1.04,1.17]: tube('Floor button',(.884,-.48,z),(.876,-.48,z),.022,'Luminous')
box('Ceiling diffuser',(0,0,2.32),(.7,.65,.02),'Luminous',.005); end()
begin('LiftShaft')
for x in [-1.065,1.065]: box('Tiled shaft side',(x,0,3.15),(.13,2.24,6.3),'Tile',.001)
box('Tiled shaft back',(0,1.065,3.15),(2.24,.13,6.3),'Tile',.001)
for x in [-.915,.915]: box('Entrance wall return',(x,-1.05,3.15),(.43,.13,6.3),'Tile',.001)
box('Interfloor front spandrel',(0,-1.05,2.925),(2.24,.13,1.35),'Tile',.001)
end()
begin('TunnelSegment')
for x in [-3.69,3.69]: box('Concrete tunnel side',(x,0,.9),(.18,2,4.8),'Stone',.002)
box('Concrete tunnel roof',(0,0,3.36),(7.56,2,.12),'Stone',.002)
for x in [-3.43,3.43]:
    box('Service cable tray',(x,0,1.9),(.22,2,.12),'Metal',.003)
    for y in [-.65,.65]: box('Tray wall bracket',(x+(.10 if x>0 else -.10),y,1.81),(.25,.035,.04),'Metal',.002)
end()
begin('TrackModule')
box('Ballast foundation',(0,0,-.44),(3.2,2,.16),'Ballast',0)
for y in [-.70,0,.70]:
    box('Concrete sleeper',(0,y,-.27),(2.35,.23,.16),'Stone',.012)
    for x in [-.7175,.7175]: box('Rail fastening',(x,y,-.17),(.23,.15,.04),'Rubber',.003)
for x in [-.7175,.7175]:
    box('Rail foot',(x,0,-.15),(.145,2,.022),'Metal',.002)
    box('Rail web',(x,0,-.09),(.018,2,.10),'Metal',.001)
    box('Rail head',(x,0,-.028),(.066,2,.035),'Metal',.004)
end()
begin('SupportPole')
tube('Floor to ceiling pole',(0,0,.04),(0,0,2.38),.018)
for z in [.025,2.38]: tube('Fixing collar',(0,0,z-.012),(0,0,z+.012),.045)
end()
begin('GrabRail')
tube('Overhead longitudinal rail',(0,-1.75,2.0),(0,1.75,2.0),.018)
for y in [-1.4,1.4]:
    tube('Roof hanger',(0,y,2),(0,y,2.38),.014)
    box('Roof fixing plate',(0,y,2.38),(.09,.07,.015),'Metal',.002)
for y in [-1.5,-1,-.5,0,.5,1,1.5]:
    # Upper strap loops around the rail; lower strap meets the handle's upper crossbar.
    box('Strap',(0,y,1.861),(.017,.017,.282),'Rubber',.003)
    tube('Strap rail collar',(0,y-.018,2.0),(0,y+.018,2.0),.025,'Rubber',12)
    pts=[(-.085,y,1.72),(-.085,y,1.62),(0,y,1.54),(.085,y,1.62),(.085,y,1.72),(-.085,y,1.72)]
    for a,b in zip(pts,pts[1:]): tube('Closed hanging handle',a,b,.012,'White',12)
end()
begin('TrainCarBody')
length=19.6; door_centres=[2.5,7.4,12.3,17.2]
root['car_length_m']=length; root['outer_width_m']=3.12; root['usable_floor_width_m']=2.85
box('Non-slip carriage floor',(0,-length/2,-.06),(2.85,length,.12),'Rubber',.002)
box('Interior supported ceiling lining',(0,-length/2,2.40),(2.70,length,.04),'White',.002)
# Side coves close the headliner/curved-shell junction; slim grilles feed the actual HVAC bays.
for side in [-1,1]:
    box('Headliner side cove',(side*1.355,-length/2,2.29),(.15,length,.24),'White',.006)
    for centre in [-5.5,-14.1]:
        box('Air grille recess',(side*1.105,centre,2.373),(.30,1.10,.008),'Rubber',.001)
        for i in range(9): box('Vent grille slat',(side*1.105,centre-.48+i*.12,2.366),(.29,.018,.012),'White',.001)
box('Underfloor chassis',(0,-length/2,-.27),(3.02,length,.3),'Metal',.01)
# Roof skin is a continuous curved cross-section with thickness; passenger headroom >2.2m.
cross=[(-1.56,2.12),(-1.48,2.40),(-1.26,2.59),(-.80,2.70),(0,2.74),(.8,2.70),(1.26,2.59),(1.48,2.40),(1.56,2.12)]
verts=[(x,y,z) for y in [0,-length] for x,z in cross]+[(x,y,z-.065) for y in [0,-length] for x,z in cross]
n=len(cross); faces=[]
for i in range(n-1):
    faces.extend([(i,i+1,n+i+1,n+i),(2*n+i,3*n+i,3*n+i+1,2*n+i+1),
                  (i,2*n+i,2*n+i+1,i+1),(n+i,n+i+1,3*n+i+1,3*n+i)])
faces.extend([(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)])
mesh('Continuous curved roof',verts,faces,'White',.003)
for side in [-1,1]:
    x=side*1.50; edges=[0]+[v for d in door_centres for v in [d-.65,d+.65]]+[length]
    for i in range(0,len(edges)-1,2):
        a,b=edges[i],edges[i+1]; span=b-a
        box('Lower body panel',(x,-(a+b)/2,.4825),(.12,span,.965),'Metal',.007)
        box('Cream internal lining',(x-side*.069,-(a+b)/2,.488),(.014,span,.956),'White',.002)
        box('Upper body panel',(x,-(a+b)/2,2.005),(.12,span,.25),'White',.005)
        box('Yellow exterior stripe',(x+side*.067,-(a+b)/2,.73),(.008,span,.095),'Yellow',.001)
        # One glazing opening per bay with substantial corner posts and a rubber perimeter.
        for y in [-a-.07,-b+.07]: box('Window pillar',(x,y,1.43),(.12,.14,1.0),'Metal',.005)
        width=max(.1,span-.28)
        box('Bay glazing',(x,-(a+b)/2,1.43),(.012,width,.91),'Glass',.01)
        for z in [.97,1.89]: box('Horizontal rubber gasket',(x,-(a+b)/2,z),(.07,width,.027),'Rubber',.004)
        for y in [-a-.15,-b+.15]: box('Vertical rubber gasket',(x,y,1.43),(.07,.027,.94),'Rubber',.004)
    for d in door_centres:
        box('Door motor cover',(x,-d,2.19),(.14,1.49,.16),'White',.009)
        for y in [-d-.69,-d+.69]: box('Door jamb',(x,y,1.06),(.12,.08,2.12),'Metal',.004)
# Open, framed inter-car passages. End cars get a separate cab/solid collision boundary in Unity.
for y in [0,-length]:
    for side in [-1,1]:
        box('Bulkhead beside open gangway',(side*1.015,y,1.20),(.87,.08,2.40),'White',.006)
        box('Passage stainless jamb',(side*.59,y,1.08),(.035,.13,2.16),'Metal',.004)
    box('Passage lintel',(0,y,2.30),(1.16,.08,.20),'White',.005)
for y in [-3.0,-length+3.0]:
    box('Bogie frame',(0,y,-.63),(2.30,2.15,.35),'Metal',.015)
    for yy in [y-.7,y+.7]:
        tube('Axle',(-1.02,yy,-.66),(1.02,yy,-.66),.075)
        for x in [-.76,.76]: tube('Flanged wheel',(x-.06,yy,-.66),(x+.06,yy,-.66),.36,'Metal',32)
for y in [-5.5,-14.1]: box('Roof HVAC housing',(0,y,2.81),(1.65,2.15,.15),'Metal',.055)
anchor('FloorMin',(0,0,0)); anchor('FloorMax',(0,-length,0)); end()
begin('TrainGangway')
root['clear_width_m']=1.16; root['connection_length_m']=.8
box('Continuous flexible walkway',(0,-.4,-.045),(1.16,.8,.09),'Rubber',.003)
box('Gangway roof',(0,-.4,2.18),(1.32,.8,.10),'Rubber',.007)
for side in [-1,1]:
    box('Inner protective wall',(side*.62,-.4,1.06),(.08,.8,2.12),'Rubber',.005)
    for i in range(11):
        y=-i*.08
        box('Accordion side fold',(side*.70,y,1.08),(.17,.037,2.16),'Rubber',.009)
        box('Accordion roof fold',(0,y,2.21),(1.56,.037,.12),'Rubber',.009)
    tube('Gangway handrail',(side*.54,-.05,1.00),(side*.54,-.75,1.00),.016,'Metal',12)
anchor('WalkwayStart',(0,0,0)); anchor('WalkwayEnd',(0,-.8,0)); end()
begin('TrainCabPartition')
# Passenger compartment end: visible crew-only door, matching the existing solid end collider.
for x in [-.94,.94]: box('Crew bulkhead wing',(x,0,1.20),(1.02,.07,2.40),'White',.004)
box('Crew doorway head',(0,0,2.285),(.86,.07,.23),'White',.003)
box('Closed crew door',(0,-.016,1.075),(.85,.047,2.15),'White',.004)
for x in [-.437,.437]: box('Crew door rubber reveal',(x,-.04,1.09),(.014,.016,2.18),'Rubber',.002)
box('Crew door upper reveal',(0,-.04,2.18),(.89,.016,.014),'Rubber',.002)
box('Crew door observation surround',(0,-.044,1.55),(.48,.018,.53),'Rubber',.016)
box('Crew door observation glass',(0,-.058,1.55),(.44,.008,.49),'Glass',.012)
tube('Crew door handle',(.28,-.05,.96),(.28,-.10,.96),.012)
tube('Crew door lever',(.18,-.10,.96),(.28,-.10,.96),.012)
end()
begin('TrainCab')
# A formed cab mask with inset windscreen, not a glass plane on a cubic shell.
poly('Cab nose',[(-1.56,.25),(1.56,.25),(1.40,-.65),(1.28,-.87),(-1.28,-.87),(-1.40,-.65)],-.10,2.12,'White',.075)
# The cab roof follows the SAME formed car cross-section and joins it at the rear.
# A flat, low cab box leaves a false 0.44m step/gap at the passenger roof junction.
def nose_front(x):
    x=abs(x)
    if x<=1.28: return -.87
    if x<=1.40: return -.87+(x-1.28)/.12*.22
    return -.65+(x-1.40)/.16*.85
cab_vertices=[(x,y,z) for rear in [True,False] for x,z in cross for y in [(.25 if rear else nose_front(x))]]
cab_vertices += [(x,y,2.10) for rear in [True,False] for x,z in cross for y in [(.25 if rear else nose_front(x))]]
cab_faces=[]
for i in range(n-1):
    cab_faces.extend([(i,i+1,n+i+1,n+i),(2*n+i,3*n+i,3*n+i+1,2*n+i+1),
                      (i,2*n+i,2*n+i+1,i+1),(n+i,n+i+1,3*n+i+1,3*n+i)])
cab_faces.extend([(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)])
mesh('Connected formed cab roof',cab_vertices,cab_faces,'White',.002)
box('Cab face dark mask',(0,-.867,1.47),(2.34,.035,1.15),'Rubber',.08)
box('Windscreen',(0,-.891,1.53),(2.17,.014,.87),'Glass',.072)
box('Route display',(0,-.886,2.03),(.77,.026,.12),'Blue',.012)
box('Line fascia',(0,-.893,.77),(2.28,.024,.12),'Yellow',.012)
for x in [-1.10,1.10]:
    box('Headlamp recessed bezel',(x,-.881,.49),(.29,.026,.12),'Rubber',.020)
    box('Headlamp diffuser',(x,-.898,.49),(.25,.009,.081),'Luminous',.012)
box('Anti-climber',(0,-.96,.12),(2.20,.22,.19),'Rubber',.025)
tube('Coupler stem',(0,-.86,-.15),(0,-1.16,-.15),.11)
box('Automatic coupler head',(0,-1.24,-.15),(.27,.16,.20),'Metal',.016)
for x in [-.71,.71]: tube('Windscreen wiper',(x,-.906,1.18),(x+.13,-.906,1.85),.007,'Rubber',10)
end()
begin('CakeBox')
box('Folded carton',(0,0,.13),(.38,.38,.26),'Metro_Cardboard',.004)
box('Overlapping lid',(0,0,.272),(.392,.392,.025),'Metro_Cardboard',.002)
for x in [-.182,.182]: box('Fold seam',(x,0,.272),(.002,.38,.003),'Brown',.0004)
box('Carry ribbon across',(0,0,.288),(.035,.396,.005),'Yellow',.001)
box('Carry ribbon along',(0,0,.289),(.396,.035,.005),'Yellow',.001)
end()

# Audit SOURCE before saving. Each generated part is closed, UV'd and metre-scaled.
# A neutral indoor reflection environment is generated in Blender as linear HDR.
# Without a reflection environment, physically metallic surfaces appear black in Unity.
yy,xx=np.mgrid[0:256,0:512]/np.array([256,512])[:,None,None]
env=np.zeros((256,512,4),np.float32); env[...,3]=1
base=.24+.20*yy
soft=np.exp(-((yy-.72)/.035)**2)*(.6+.4*np.cos(xx*8*math.pi)**8)
for c,tint in enumerate([.92,.97,1.]): env[...,c]=(base+soft*1.7)*tint
hdr=bpy.data.images.new('IndoorReflection',width=512,height=256,alpha=True,float_buffer=True)
hdr.colorspace_settings.name='Non-Color'; hdr.pixels.foreach_set(env.ravel())
hdr.filepath_raw=os.path.join(ART,'Textures','Metro','IndoorReflection.hdr'); hdr.file_format='HDR'; hdr.save()
hdr.use_fake_user=True
import bmesh
for r in roots:
    assert tuple(r.scale)==(1.,1.,1.)
    for o in r.children:
        if o.type!='MESH': continue
        assert o.data.uv_layers and o.data.materials
        bm=bmesh.new(); bm.from_mesh(o.data)
        assert not any(not e.is_manifold for e in bm.edges), o.name+' open edge'
        assert not any(f.calc_area()<1e-10 for f in bm.faces), o.name+' zero-area face'
        bm.free()
# Function-level checks, beyond every individual component being a closed box:
# the assembled door/window perimeter must not leave a strip of daylight between parts.
from mathutils.bvhtree import BVHTree
def surface_tree(r):
    vertices=[]; polygons=[]
    for o in r.children:
        if o.type!='MESH': continue
        offset=len(vertices); vertices.extend(o.matrix_local@v.co for v in o.data.vertices)
        polygons.extend(tuple(offset+i for i in face.vertices) for face in o.data.polygons)
    return BVHTree.FromPolygons(vertices,polygons)
coverage=0
for name in ['SlidingDoor','PlatformDoor']:
    tree=surface_tree(next(r for r in roots if r.name==name))
    heights=[.97,1.02,1.42,1.82,1.89,2.01] if name=='SlidingDoor' else [.075,.08,.92,1.20,2.32,2.36]
    for x in [-.32,-.28,-.255,-.20,0,.20,.255,.28,.32]:
        for z in heights:
            assert tree.ray_cast(Vector((x,-.2,z)),Vector((0,1,0)),.4)[0] is not None,(name,x,z,'uncovered seam')
            coverage+=1
tree=surface_tree(next(r for r in roots if r.name=='TrainCarBody'))
for side in [-1,1]:
    for y in [-.9,-4.95,-9.85,-14.75,-18.8]:
        for z in [.93,.96,.975,1.42,1.885,1.91,2.05]:
            assert tree.ray_cast(Vector((0,y,z)),Vector((side,0,0)),1.7)[0] is not None,(side,y,z,'uncovered carriage window seam')
            coverage+=1
print('KIT_WINDOW_SEAM_COVERAGE_PASS',coverage,flush=True)
for index,r in enumerate(roots): r.location=((index%6)*5,(index//6)*5,0)
for im in bpy.data.images:
    if im.filepath: im.filepath=bpy.path.relpath(bpy.path.abspath(im.filepath),start=os.path.join(ROOT,'Source~'))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Source~','StationTrain.blend'))
print('KIT_SOURCE_AUDIT_PASS',len(roots),flush=True)

if '--no-renders' not in sys.argv:
    # Original asset-local offline views; never capture desktop/Unity/Play mode.
    for r in roots: r.location=(0,0,0)
    clay=bpy.data.materials.new('Inspection clay'); clay.use_nodes=True
    clay.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.48,.48,.48,1)
    for p,power,size in [((6,-7,10),2300,7),((-6,1,6),1600,6),((1,8,9),2000,5)]:
        data=bpy.data.lights.new('Review softbox','AREA'); data.energy=power; data.shape='DISK'; data.size=size
        o=bpy.data.objects.new('Review softbox',data); bpy.context.collection.objects.link(o); o.location=p
        o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Review camera'); cam=bpy.data.objects.new('Review camera',data); bpy.context.collection.objects.link(cam); s.camera=cam
    s.render.resolution_x=360; s.render.resolution_y=300
    for r in roots:
        for other in roots:
            for o in other.children: o.hide_render=other!=r
        corners=[o.matrix_world@Vector(c) for o in r.children if o.type=='MESH' for c in o.bound_box]
        mn=Vector(tuple(min(p[i] for p in corners) for i in range(3))); mx=Vector(tuple(max(p[i] for p in corners) for i in range(3)))
        target=(mn+mx)/2; size=max(mx-mn); data.type='ORTHO'; data.ortho_scale=size*1.36
        for label,direction in [('Front',(0,-1,.1)),('Back',(0,1,.1)),('Left',(-1,0,.1)),('Right',(1,0,.1)),('Top',(0,-.001,1)),('Material',(-1,-1,.65)),('Clay',(-1,-1,.65)),('Contact',(-1,-1,.2))]:
            cam.location=target+Vector(direction).normalized()*size*2
            cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
            s.view_layers[0].material_override=clay if label=='Clay' else None
            s.render.filepath=os.path.join(REVIEW,r.name+'_'+label+'.png'); bpy.ops.render.render(write_still=True)
    print('KIT_ASSET_LOCAL_VIEWS_RENDERED',len(roots)*8,flush=True)
