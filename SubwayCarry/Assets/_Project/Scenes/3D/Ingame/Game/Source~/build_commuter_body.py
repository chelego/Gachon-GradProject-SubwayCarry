"""Ordinary clothed commuter geometry on the preserved Quaternius animation rig.
MakeHuman core basemesh is CC0; the original OBJ/license are retained in HumanBase.
No add-on, account, model upload or third-party application installation is needed.
"""
import bpy, os, math, json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

def rebuild(arm, meshes, source_folder):
    vertices=[]; texcoords=[]; faces=[]; groups={}; group=''
    for line in open(os.path.join(source_folder,'HumanBase','MakeHumanBase.obj'),encoding='utf-8'):
        parts=line.split()
        if not parts: continue
        if parts[0]=='v': vertices.append(Vector(tuple(map(float,parts[1:4]))))
        elif parts[0]=='vt': texcoords.append(tuple(map(float,parts[1:3])))
        elif parts[0]=='g': group=parts[1]
        elif parts[0]=='f':
            corners=[tuple(int(i)-1 for i in p.split('/')[:2]) for p in parts[1:]]
            groups.setdefault(group,[]).append(corners)
    body_indices={i for f in groups['body'] for i,t in f}
    ground=min(vertices[i].y for i in body_indices)
    height=max(vertices[i].y for i in body_indices)-ground
    metric=1.76/height
    # Local pectoral fit, from the same CC0 base's authored adult male target.
    # Do not change the face, height, arms or overall character identity.
    def smooth(a,b,value):
        t=max(0,min(1,(value-a)/(b-a)));return t*t*(3-2*t)
    for line in open(os.path.join(source_folder,'HumanBase','asian-male-young.target'),encoding='utf-8'):
        row=line.split()
        if not row or row[0].startswith('#'): continue
        i=int(row[0]);p=vertices[i];x=p.x*metric;z=(p.y-ground)*metric;front=p.z*metric
        mask=smooth(1.16,1.24,z)*(1-smooth(1.43,1.50,z))*(1-smooth(.15,.23,abs(x)))*smooth(.035,.095,front)
        p.z+=float(row[3])*.8*mask
    # Local abdomen refinement requested for this commuter; leave the pelvis,
    # face, stature and limb proportions alone. The garment proxy uses these
    # same fitted vertices, so body and clothing keep coherent correspondences.
    for p in vertices:
        x=p.x*metric;z=(p.y-ground)*metric;front=p.z*metric
        mask=smooth(.95,1.015,z)*(1-smooth(1.13,1.21,z))*(1-smooth(.14,.22,abs(x)))*smooth(.045,.085,front)
        p.z-=.012*mask/metric
    def point(p): return Vector((p.x*metric,-p.z*metric,(p.y-ground)*metric))
    def joint(name):
        indices={i for f in groups['joint-'+name] for i,t in f}
        return point(sum((vertices[i] for i in indices),Vector())/len(indices))
    source_rig=json.load(open(os.path.join(source_folder,'HumanBase','default.mhskel'),encoding='utf-8'))
    def rig_joint(name):
        indices=source_rig['joints'][name]
        return point(sum((vertices[i] for i in indices),Vector())/len(indices))
    def palm_normal(side):
        # This base has relaxed A-pose palms tilted toward the legs, not toward
        # world front. Use its actual MCP plane so wrist roll is anatomical.
        along=joint(side+'-finger-3-1')-joint(side+'-hand')
        across=joint(side+'-finger-2-1')-joint(side+'-finger-5-1')
        normal=along.cross(across).normalized()
        return normal if normal.z<0 else -normal
    # Fit the EXISTING named skeleton to the anatomical base, rather than stretching
    # an anatomical surface between incompatible cartoon joint positions.
    pairs={'Hips':('pelvis','spine-4'),'Spine':('spine-4','spine-3'),
           'Spine1':('spine-3','spine-2'),'Spine2':('spine-2','neck'),'Neck':('neck','head')}
    for label,side in [('Left','l'),('Right','r')]:
        pairs.update({label+'Shoulder':(side+'-clavicle',side+'-shoulder'),
          label+'Arm':(side+'-shoulder',side+'-elbow'),label+'ForeArm':(side+'-elbow',side+'-hand'),
          label+'Hand':(side+'-hand',side+'-finger-3-1'),label+'UpLeg':(side+'-upper-leg',side+'-knee'),
          label+'Leg':(side+'-knee',side+'-ankle'),label+'Foot':(side+'-ankle',side+'-toe-3-2'),
          label+'ToeBase':(side+'-toe-3-2',side+'-toe-3-4')})
    arm.animation_data.action=None
    for p in arm.pose.bones: p.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
    inverse=arm.matrix_world.inverted()
    for name,(first,last) in pairs.items():
        bone=arm.data.edit_bones[name];bone.use_connect=False;bone.head=inverse@joint(first);bone.tail=inverse@joint(last)
        normal=palm_normal('l' if name.startswith('Left') else 'r') if name.endswith('Hand') else Vector((0,0,1) if name.endswith(('Foot','ToeBase')) else (0,-1,0))
        bone.align_roll(inverse.to_3x3()@normal)
    # Retain the source's two-part shoulder chain instead of collapsing its
    # acromion/scapular helper weights onto the upper arm. Public limb names stay.
    for label,short in [('Left','L'),('Right','R')]:
        clavicle=arm.data.edit_bones[label+'Shoulder']
        definition=source_rig['bones']['clavicle.'+short]
        clavicle.head=inverse@rig_joint(definition['head']);clavicle.tail=inverse@rig_joint(definition['tail'])
        clavicle.align_roll(inverse.to_3x3()@Vector((0,-1,0)))
        helper=arm.data.edit_bones.get(label+'ShoulderHelper') or arm.data.edit_bones.new(label+'ShoulderHelper')
        definition=source_rig['bones']['shoulder01.'+short]
        helper.head=inverse@rig_joint(definition['head']);helper.tail=inverse@rig_joint(definition['tail'])
        helper.parent=clavicle;helper.use_connect=True;helper.use_deform=True
        helper.align_roll(inverse.to_3x3()@Vector((0,-1,0)))
        arm.data.edit_bones[label+'Arm'].parent=helper
    bone=arm.data.edit_bones['Head'];bone.use_connect=False;bone.head=inverse@joint('head');bone.tail=inverse@Vector((0,0,1.75))
    finger_names=['Thumb','Index','Middle','Ring','Pinky']
    for side,short in [('Left','l'),('Right','r')]:
        for bone in arm.data.edit_bones:
            if bone.name.startswith(side+'Hand') and bone.name!=side+'Hand': bone.use_deform=False
        for digit,label in enumerate(finger_names,1):
            for segment in range(1,4):
                name=side+'Hand'+label+str(segment)
                bone=arm.data.edit_bones.get(name) or arm.data.edit_bones.new(name)
                bone.head=inverse@joint(short+'-finger-'+str(digit)+'-'+str(segment))
                bone.tail=inverse@joint(short+'-finger-'+str(digit)+'-'+str(segment+1))
                bone.parent=arm.data.edit_bones[side+'Hand' if segment==1 else side+'Hand'+label+str(segment-1)]
                bone.use_connect=segment>1;bone.use_deform=True
                bone.align_roll(inverse.to_3x3()@palm_normal(short))
    bpy.ops.object.mode_set(mode='OBJECT');bpy.context.view_layer.update()
    segments=[]
    def segment(bone,first,last):
        a,b=joint(first),joint(last)
        old=arm.data.bones[bone]; c=arm.matrix_world@old.head_local;d=arm.matrix_world@old.tail_local
        segments.append((bone,a,b,c,d,(b-a).normalized().rotation_difference((d-c).normalized())))
    segment('Hips','pelvis','spine-4')
    segment('Spine','spine-4','spine-3')
    segment('Spine1','spine-3','spine-2')
    segment('Spine2','spine-2','neck')
    segment('Neck','neck','head')
    head_source=joint('head');head_target=arm.matrix_world@arm.data.bones['Head'].head_local
    for label,side in [('Left','l'),('Right','r')]:
        segment(label+'Shoulder',side+'-clavicle',side+'-shoulder')
        segment(label+'Arm',side+'-shoulder',side+'-elbow')
        segment(label+'ForeArm',side+'-elbow',side+'-hand')
        segment(label+'Hand',side+'-hand',side+'-finger-3-4')
        segment(label+'UpLeg',side+'-upper-leg',side+'-knee')
        segment(label+'Leg',side+'-knee',side+'-ankle')
        segment(label+'Foot',side+'-ankle',side+'-toe-3-4')
    def map_point(p):
        # Skull/face remain a coherent anatomical form, not a mix of jaw and neck segments.
        if p.z>joint('neck').z-.015 and abs(p.x)<.18:
            return p,[('Head',1)]
        choices=[]
        for name,a,b,c,d,rotation in segments:
            delta=b-a;t=max(0,min(1,(p-a).dot(delta)/delta.length_squared))
            distance=(p-a-delta*t).length
            # Avoid binding fingers on one side to the nearby torso.
            if name.startswith('Left') and p.x<0 or name.startswith('Right') and p.x>0: continue
            choices.append((distance,name,a,b,c,d,rotation))
        choices.sort(key=lambda row:row[0]);choices=choices[:2]
        weights=[1/max(.008,row[0])**4 for row in choices];total=sum(weights)
        result=p.copy();binding=[]
        for weight,row in zip(weights,choices):
            distance,name,a,b,c,d,rotation=row;w=weight/total
            along=(p-a).dot((b-a).normalized())
            radial=p-a-(b-a).normalized()*along
            binding.append((name,w))
        return result,binding
    # Retain the asset author's smooth anatomical weighting. Distance-to-bone
    # classification alone created hard deltoid/cuff transitions in the draft.
    authored_weights=json.load(open(os.path.join(source_folder,'HumanBase','default_weights.mhw'),encoding='utf-8'))['weights']
    anatomical=[{} for _ in vertices]
    def retained_bone(name):
        side='Left' if name.endswith('.L') else 'Right' if name.endswith('.R') else ''
        part=name.split('.')[0]
        if part.startswith('upperarm'): return side+'Arm'
        if part=='clavicle': return side+'Shoulder'
        if part=='shoulder01': return side+'ShoulderHelper'
        if part.startswith('lowerarm'): return side+'ForeArm'
        if part.startswith('finger'):
            digit,segment=map(int,part[6:].split('-'))
            return side+'Hand'+finger_names[digit-1]+str(segment)
        if part.startswith('metacarpal') or part=='wrist': return side+'Hand'
        if part.startswith('upperleg'): return side+'UpLeg'
        if part.startswith('lowerleg'): return side+'Leg'
        if part.startswith('toe'): return side+'ToeBase'
        if part=='foot': return side+'Foot'
        if part.startswith('pelvis') or part=='root': return 'Hips'
        if part in ('spine05','spine04'): return 'Spine'
        if part=='spine03': return 'Spine1'
        if part in ('spine02','spine01','breast'): return 'Spine2'
        if part.startswith('neck'): return 'Neck'
        return 'Head'
    for source_bone,entries in authored_weights.items():
        bone=retained_bone(source_bone)
        # Each source shoulder group now has its own anatomical bone.
        split=[(bone,1)]
        for index,weight in entries:
            for target,fraction in split: anatomical[index][target]=anatomical[index].get(target,0)+weight*fraction
    def normalized(binding,fallback):
        entries=sorted(((name,max(0,w)) for name,w in binding.items()),key=lambda row:row[1],reverse=True)[:4]
        total=sum(w for name,w in entries)
        return [(name,w/total) for name,w in entries if w>.0001] if total>.0001 else map_point(fallback)[1]
    source_triangles=[]
    for face in groups['body']:
        corners=[i for i,t in face]
        for k in range(1,len(corners)-1): source_triangles.append((corners[0],corners[k],corners[k+1]))
    body_surface=BVHTree.FromPolygons([point(p) for p in vertices],source_triangles,all_triangles=True)
    def surface_binding(p):
        hit,normal,index,distance=body_surface.find_nearest(p)
        if index is None: return map_point(p)[1]
        triangle=source_triangles[index];a,b,c=[point(vertices[i]) for i in triangle]
        weights=barycentric_transform(hit,a,b,c,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
        combined={}
        for source,w in zip(triangle,weights):
            for bone,weight in anatomical[source].items(): combined[bone]=combined.get(bone,0)+weight*w
        return normalized(combined,p)
    def material(name,color,roughness):
        mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
        node=mat.node_tree.nodes.get('Principled BSDF');node.inputs['Base Color'].default_value=(*color,1);node.inputs['Roughness'].default_value=roughness
        return mat
    skin=material('CommuterSkin',(.56,.37,.27),.58)
    coat=material('CommuterCoat',(.085,.12,.16),.78)
    trousers=material('CommuterTrousers',(.035,.045,.06),.84)
    hair=material('CommuterHair',(.018,.014,.012),.86)
    lips=material('CommuterLips',(.31,.12,.10),.6)
    # Small cloth weave belongs in bump/roughness, not noisy multicoloured albedo.
    for mat in (coat,trousers):
        nodes=mat.node_tree.nodes;links=mat.node_tree.links
        texture=nodes.new('ShaderNodeTexNoise');texture.inputs['Scale'].default_value=350;texture.inputs['Detail'].default_value=1
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.13;bump.inputs['Distance'].default_value=.0003
        links.new(texture.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],nodes.get('Principled BSDF').inputs['Normal'])
    body_faces=groups['body'];indices=sorted({i for f in body_faces for i,t in f});remap={i:j for j,i in enumerate(indices)}
    bindings={};mapped=[]
    for i in indices:
        p=point(vertices[i]);q=p;binding=normalized(anatomical[i],p);bindings[i]=binding
        mapped.append(tuple(arm.matrix_world.inverted()@q))
    data=bpy.data.meshes.new('Anatomical commuter topology');data.from_pydata(mapped,[],[[remap[i] for i,t in f] for f in body_faces]);data.update()
    body=bpy.data.objects.new('CommuterBody',data);bpy.context.collection.objects.link(body);body.parent=arm;body.matrix_parent_inverse=Matrix.Identity(4);body.matrix_basis=Matrix.Identity(4)
    for mat in (skin,coat,trousers,hair,lips): data.materials.append(mat)
    uv=data.uv_layers.new(name='Anatomical UV')
    for polygon,f in zip(data.polygons,body_faces):
        midpoint=sum((point(vertices[i]) for i,t in f),Vector())/len(f)
        # Exposed face/hands, tailored upper/lower clothing, hair cap with a natural forehead edge.
        hands=sum(weight for i,t in f for bone,weight in bindings[i] if bone.startswith(('LeftHand','RightHand')))/len(f)>.12
        exposed_head=midpoint.z>joint('neck').z-.015 and abs(midpoint.x)<.18
        polygon.material_index=0 if exposed_head or hands else 1 if midpoint.z>joint('pelvis').z+.04 else 2
        if midpoint.z>head_source.z+.10 or (midpoint.z>head_source.z+.055 and midpoint.y>head_source.y+.02): polygon.material_index=3
        polygon.use_smooth=True
        for loop,(i,t) in zip(polygon.loop_indices,f): uv.data[loop].uv=texcoords[t]
    for i,group_bindings in bindings.items():
        for name,weight in group_bindings:
            vertex_group=body.vertex_groups.get(name) or body.vertex_groups.new(name=name);vertex_group.add([remap[i]],weight,'REPLACE')
    modifier=body.modifiers.new('Preserved animation skeleton','ARMATURE');modifier.object=arm
    undershirt=material('CommuterUnderShirt',(.025,.028,.032),.86);data.materials.append(undershirt)
    # Use coherent authored clothing topology, not disconnected sleeve cylinders
    # or shoulder spheres. Fit the CC0 source garment to this exact basemesh.
    texture_folder=os.path.abspath(os.path.join(source_folder,'..','Art','Textures'))
    def image_material(mat,file_name,normal=None,ao=None,alpha=False):
        mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(1,1,1,1)
        image=bpy.data.images.load(os.path.join(texture_folder,file_name),check_existing=True)
        image.pack();node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
        principled=mat.node_tree.nodes.get('Principled BSDF');mat.node_tree.links.new(node.outputs['Color'],principled.inputs['Base Color'])
        if alpha:
            mat.surface_render_method='DITHERED';mat.node_tree.links.new(node.outputs['Alpha'],principled.inputs['Alpha'])
        if normal:
            image=bpy.data.images.load(os.path.join(texture_folder,normal),check_existing=True);image.colorspace_settings.name='Non-Color';image.pack()
            texture=mat.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image
            normal_node=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal_node.inputs['Strength'].default_value=.65
            mat.node_tree.links.new(texture.outputs['Color'],normal_node.inputs['Color']);mat.node_tree.links.new(normal_node.outputs['Normal'],principled.inputs['Normal'])
    image_material(skin,'CommuterSkin_BaseColor.png')
    image_material(coat,'CommuterSuit_BaseColor.png','CommuterSuit_Normal.png')
    image_material(trousers,'CommuterSuit_BaseColor.png','CommuterSuit_Normal.png')
    image_material(hair,'CommuterHair_BaseColor.png',alpha=True)
    shoe_mat=material('CommuterShoes',(.08,.08,.08),.6);image_material(shoe_mat,'CommuterShoes_BaseColor.png')
    def fitted_asset(asset_name,material_selector):
        folder=os.path.join(source_folder,'HumanBase');source_points=[];source_uv=[];source_faces=[]
        for line in open(os.path.join(folder,asset_name+'.obj'),encoding='utf-8'):
            row=line.split()
            if not row: continue
            if row[0]=='v': source_points.append(Vector(tuple(map(float,row[1:4]))))
            elif row[0]=='vt': source_uv.append(tuple(map(float,row[1:3])))
            elif row[0]=='f': source_faces.append([tuple(int(n)-1 for n in corner.split('/')[:2]) for corner in row[1:]])
        refs=[];proxy_bindings=[];reading=False;factors=[1.,1.,1.]
        for line in open(os.path.join(folder,asset_name+'.mhclo'),encoding='utf-8'):
            row=line.split()
            if not row or row[0].startswith('#'): continue
            if row[0] in ('x_scale','y_scale','z_scale'):
                axis={'x_scale':0,'y_scale':1,'z_scale':2}[row[0]];a,b=int(row[1]),int(row[2]);factors[axis]=abs(vertices[a][axis]-vertices[b][axis])/float(row[3])
            elif row[0]=='verts': reading=True
            elif reading:
                if not row[0].lstrip('-').isdigit():
                    if refs: break
                    continue
                if len(row)==1:
                    index=int(row[0]);refs.append(vertices[index].copy());proxy_bindings.append(anatomical[index].copy())
                elif len(row)>=9:
                    indices=list(map(int,row[:3]));weights=list(map(float,row[3:6]));offset=Vector(tuple(float(row[6+i])*factors[i] for i in range(3)))
                    refs.append(sum((vertices[i]*w for i,w in zip(indices,weights)),Vector())+offset)
                    combined={}
                    for index,w in zip(indices,weights):
                        for bone,weight in anatomical[index].items(): combined[bone]=combined.get(bone,0)+weight*w
                    proxy_bindings.append(combined)
        if len(refs)!=len(source_points): raise RuntimeError('Incomplete fitted garment mapping: '+asset_name)
        world_points=[point(p) for p in refs]
        if asset_name=='male_casualsuit01':
            # Small tailored ease at the untucked shirt waist. Preserve the
            # source hem/topology/UV and sleeves instead of scaling the outfit.
            for p in world_points:
                ease=smooth(1.02,1.06,p.z)*(1-smooth(1.17,1.255,p.z))
                radial=Vector((p.x,p.y+.006,0))
                if radial.length>.03:
                    radial.normalize();p.x+=radial.x*.012*ease;p.y+=radial.y*.004*ease
        mesh=bpy.data.meshes.new(asset_name+' fitted topology');mesh.from_pydata([tuple(arm.matrix_world.inverted()@p) for p in world_points],[],[[i for i,t in f] for f in source_faces]);mesh.update()
        obj=bpy.data.objects.new('Commuter_'+asset_name,mesh);bpy.context.collection.objects.link(obj);obj.parent=arm;obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4)
        for mat in (coat,trousers,hair,shoe_mat): mesh.materials.append(mat)
        layer=mesh.uv_layers.new(name='UVMap')
        for polygon,f in zip(mesh.polygons,source_faces):
            midpoint=sum((world_points[i] for i,t in f),Vector())/len(f);polygon.material_index=material_selector(midpoint);polygon.use_smooth=True
            for loop,(i,t) in zip(polygon.loop_indices,f): layer.data[loop].uv=source_uv[t]
        for i,p in enumerate(world_points):
            side='Left' if p.x>0 else 'Right'
            if asset_name.startswith('short'): binding=[('Head',1)]
            elif asset_name.startswith('shoes'):
                # The ankle cuff follows the calf; a rigid foot-weighted sock
                # otherwise swings backwards through the trouser hem.
                calf=max(0,min(1,(p.z-.075)/.045))
                binding=[(side+'Foot',1-calf),(side+'Leg',calf)]
            elif p.z<joint(('l' if p.x>0 else 'r')+'-ankle').z+.13:
                # Long trouser hems are not toe/foot skin. Keep the cuff on the
                # lower leg instead of twisting it with ankle roll/toe flexion.
                binding=[(side+'Leg',1)]
            else:
                binding=normalized(proxy_bindings[i],p)
                if asset_name.startswith('male_casualsuit') and abs(p.x)<.26 and p.z>joint('pelvis').z-.12 and p.z<1.06:
                    # A loose shirt hem/waistband is supported by the torso,
                    # not dragged into a thigh bend by source pelvis helpers.
                    leg_weight=sum(w for name,w in binding if name.endswith('UpLeg'))
                    binding=[(name,w) for name,w in binding if not name.endswith('UpLeg')]
                    combined=dict(binding);combined['Hips']=combined.get('Hips',0)+leg_weight
                    binding=normalized(combined,p)
            for bone,weight in binding:
                group=obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone);group.add([i],weight,'REPLACE')
        if asset_name.startswith('shoes'):
            # The source includes tall socks for shorts. This commuter wears long
            # trousers: retain only the short, hidden ankle cuff, not rigid sock
            # tubes bound to the foot and sticking out behind the trouser hem.
            import bmesh
            bm=bmesh.new();bm.from_mesh(mesh)
            plane=arm.matrix_world.inverted()@Vector((0,0,.16))
            normal=(arm.matrix_world.to_3x3().transposed()@Vector((0,0,1))).normalized()
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=plane,plane_no=normal,clear_outer=True)
            bm.to_mesh(mesh);bm.free();mesh.update()
        mod=obj.modifiers.new('Preserved commuter skeleton','ARMATURE');mod.object=arm
        obj['source']='MakeHuman core CC0 '+asset_name;return obj
    garments=[fitted_asset('male_casualsuit01',lambda p:0 if p.z>joint('pelvis').z+.06 else 1),
              fitted_asset('short04',lambda p:2),fitted_asset('shoes02',lambda p:3)]
    # Only exposed head/neck/hands survive underneath the real shirt and trousers.
    import bmesh
    bm=bmesh.new();bm.from_mesh(body.data)
    covered=[]
    for face in bm.faces:
        midpoint=arm.matrix_world@face.calc_center_median()
        if face.material_index==2 or (face.material_index==1 and (midpoint.z<joint('neck').z-.13 or abs(midpoint.x)>.105)):
            covered.append(face)
    bmesh.ops.delete(bm,geom=covered,context='FACES')
    for face in bm.faces: face.material_index=5 if face.material_index==1 else 0
    bm.to_mesh(body.data);bm.free();body.data.update()
    inner_vertices={i for face in body.data.polygons if face.material_index==5 for i in face.vertices}
    inverse_direction=arm.matrix_world.inverted().to_3x3()
    for i in inner_vertices:
        vertex=body.data.vertices[i];normal=(arm.matrix_world.to_3x3()@vertex.normal).normalized();vertex.co-=inverse_direction@(normal*.005)
    sclera=material('CommuterSclera',(.8,.8,.76),.38);iris=material('CommuterIris',(.027,.019,.014),.4)
    eyes=[]
    for side,sign in [('l',1),('r',-1)]:
        eye=joint(side+'-eye')
        for name,offset,scale,mat in [('Eye',Vector((0,0,0)),(.012,.012,.012),sclera),('Iris',Vector((0,-.0105,0)),(.0057,.002,.0057),iris)]:
            bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=eye+offset);obj=bpy.context.object;obj.name=side+'_'+name;obj.scale=scale
            bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
            world=obj.matrix_world.copy()
            for v in obj.data.vertices: v.co=arm.matrix_world.inverted()@(world@v.co)
            obj.parent=arm;obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4);obj.data.materials.append(mat)
            group=obj.vertex_groups.new(name='Head');group.add(list(range(len(obj.data.vertices))),1,'REPLACE');mod=obj.modifiers.new('Head skin','ARMATURE');mod.object=arm
            for face in obj.data.polygons: face.use_smooth=True
            eyes.append(obj)
    for old in list(meshes): bpy.data.objects.remove(old,do_unlink=True)
    meshes[:]=[body]+garments+eyes
    return
