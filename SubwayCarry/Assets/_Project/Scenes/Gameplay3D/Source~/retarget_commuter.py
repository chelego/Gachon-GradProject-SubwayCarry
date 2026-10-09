"""Retarget measured human motion to the fitted, same-name commuter skeleton."""
import os, math
import bpy
from mathutils import Vector, Matrix, Quaternion
from cmu_motion import Recording

def audit_waiting(arm,action):
    """Game-rig checks on anatomical segment directions, not bind Euler angles.

    Axis reference: ISB JCS parts I/II, https://www.isbweb.org/standards/.
    Adult reference ROM: https://archive.cdc.gov/www_cdc_gov/ncbddd/jointrom/index_1704899060.html.
    These are conservative authored NPC limits, not universal medical maxima.
    """
    ranges={};previous=arm.animation_data.action;arm.animation_data.action=action
    def direction(name):
        p=arm.pose.bones[name];return (arm.matrix_world.to_3x3()@(p.tail-p.head)).normalized()
    def record(name,value,minimum,maximum):
        values=ranges.setdefault(name,[]);values.append(value)
        if not minimum-.01<=value<=maximum+.01:
            raise ValueError('Waiting '+name+' outside calibrated range: '+str(value))
    for frame in range(1,int(action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        for side in ['Left','Right']:
            upper=direction(side+'Arm');lower=direction(side+'ForeArm');hand=direction(side+'Hand')
            thigh=direction(side+'UpLeg');shin=direction(side+'Leg')
            record(side+' elbow flexion',math.degrees(upper.angle(lower)),18,30)
            record(side+' knee flexion',math.degrees(thigh.angle(shin)),2,130)
            record(side+' wrist direction',math.degrees(lower.angle(hand)),0,15)
            # The fitted foot's lateral axis must remain horizontal in grounded
            # waiting. Plantar/dorsiflexion cannot be inferred from toe pitch alone.
            foot=arm.pose.bones[side+'Foot']
            lateral=(arm.matrix_world@foot.matrix).to_3x3()@Vector((1,0,0))
            bank=math.degrees(math.asin(max(-1,min(1,lateral.normalized().z))))
            record(side+' foot bank',bank,-3,3)
            # A flat shoe does not rule out a sideways-kinked tibia. Check the
            # frontal tibia/sole relationship as well, in this planted pose.
            tibia_bank=math.degrees(math.atan2(shin.x,-shin.z))
            record(side+' tibia frontal tilt',tibia_bank,-1,1)
            record(side+' ankle frontal mismatch',tibia_bank-bank,-2,2)
        neck=direction('Neck');head=arm.pose.bones['Head']
        shoulders=[arm.matrix_world@arm.pose.bones[s+'Arm'].head for s in ['Left','Right']]
        shoulder_mid=(shoulders[0]+shoulders[1])*.5
        # The skull's joint centre should not drift in front of the shoulders.
        # This is a fitted-rig regression bound, not a clinical diagnosis.
        record('neck sagittal tilt',math.degrees(math.atan2(-neck.y,neck.z)),-3,3)
        record('head forward offset m',shoulder_mid.y-(arm.matrix_world@head.head).y,-.035,.025)
        for name in ['Spine','Spine1']:
            p=arm.pose.bones[name]
            bind_world=arm.matrix_world@p.bone.matrix_local
            swing=bind_world.to_3x3().col[1].normalized().rotation_difference(direction(name))
            reference=swing@bind_world.to_quaternion()
            deviation=reference.rotation_difference((arm.matrix_world@p.matrix).to_quaternion()).angle
            record(name+' bind-roll deviation',math.degrees(deviation),0,2)
    arm.animation_data.action=previous
    for name,values in ranges.items():print('WAITING_JOINT_RANGE',name,round(min(values),3),round(max(values),3),flush=True)

def author(arm, standing, aim_world, folder):
    scene=bpy.context.scene
    bind_hip=(arm.matrix_world@arm.pose.bones['Hips'].head).z
    pairs={'Spine':'lowerback','Spine1':'upperback','Spine2':'thorax','Neck':'lowerneck','Head':'head'}
    for side,short in [('Left','l'),('Right','r')]:
        pairs.update({side+'Shoulder':short+'clavicle',side+'Arm':short+'humerus',side+'ForeArm':short+'radius',side+'Hand':short+'hand',side+'UpLeg':short+'femur',side+'Leg':short+'tibia',side+'Foot':short+'foot',side+'ToeBase':short+'toes'})
    def make(name,take,fps,limits):
        rec=Recording(folder,take,fps)
        first,last=rec.standing_prefix() if limits is None else rec.cycle(*limits)
        convert=rec.heading(first,last)
        origin=rec.sample(first)[0]['root'];finish=rec.sample(last)[0]['root']
        count=round((last-first)/fps*30)+1
        samples=[]
        arm.animation_data.action=None
        for i in range(count):
            t=i/(count-1);positions,rotations,heads=rec.sample(first+(last-first)*t)
            for p in arm.pose.bones: p.matrix_basis=standing[p.name].copy()
            bpy.context.view_layer.update()
            # Acclaim root axes are not the target pelvis' facing convention.
            # Keep in-place motion facing the motor, retain measured lean/sway.
            aim_world('Hips',convert@rotations['root']@Vector((0,1,0)),Vector((0,-1,0)))
            hip=arm.pose.bones['Hips'];m=hip.matrix.copy();w=arm.matrix_world@m.translation
            delta=convert@(positions['root']-origin-(finish-origin)*t)
            w.x+=delta.x;w.y+=delta.y;w.z=bind_hip+(positions['root'].y-origin.y)
            m.translation=arm.matrix_world.inverted()@w;hip.matrix=m
            bpy.context.view_layer.update()
            for target,source in pairs.items():
                if target.endswith('Shoulder'):
                    # Acclaim clavicles have an elevated neutral offset. Copying
                    # that direction literally shrugs a different anatomical rig.
                    # Keep the fitted clavicle; torso motion still drives it.
                    continue
                direction=convert@(positions[source]-heads[source])
                if target.endswith(('Arm','ForeArm','Hand')):
                    # Swing the fitted anatomical rest frame. Its palm and
                    # garment bind already encode the correct axial roll.
                    normal=None
                elif target.endswith(('UpLeg','Leg')):
                    # Calibrate hinge roll to the target sagittal plane, not the
                    # Acclaim joint-coordinate convention (which twists sleeves).
                    normal=Vector((0,-1,0))
                else:
                    reference=Vector((0,1,0)) if target.endswith(('Foot','ToeBase')) else Vector((0,0,1))
                    normal=convert@rotations[source]@reference if target.endswith(('Foot','ToeBase')) else Vector((0,-1,0))
                aim_world(target,direction,normal)
            samples.append({p.name:p.matrix_basis.copy() for p in arm.pose.bones})
        # Small capture drift cannot create a visible loop pop. Preserve the
        # recorded cycle, close only its last 15% to the first measured pose.
        for i in range(count):
            t=max(0,(i/(count-1)-.85)/.15);t=t*t*(3-2*t)
            if t==0: continue
            for bone in samples[i]:
                loc,rot,scale=samples[i][bone].decompose();a,b,c=samples[0][bone].decompose()
                samples[i][bone]=Matrix.LocRotScale(loc.lerp(a,t),rot.slerp(b,t),scale.lerp(c,t))
        action=bpy.data.actions.new(name);action.use_fake_user=True;arm.animation_data.action=action
        for i,pose in enumerate(samples,1):
            for p in arm.pose.bones:
                loc,rot,scale=pose[p.name].decompose();p.location=loc;p.rotation_mode='QUATERNION';p.rotation_quaternion=rot;p.scale=scale
                p.keyframe_insert('location',frame=i);p.keyframe_insert('rotation_quaternion',frame=i);p.keyframe_insert('scale',frame=i)
        action['motion_source']='CMU '+take;action['duration_seconds']=(count-1)/30
        print('RETARGETED',name,count,'frames',flush=True)
        return action
    # Waiting is not the start of a travelling recording. Use the fitted
    # anatomical stance; preserve garment bind roll instead of imposing the
    # source skeleton's wrist / upper-arm coordinate convention.
    arm.animation_data.action=None
    for p in arm.pose.bones: p.matrix_basis=standing[p.name].copy()
    bpy.context.view_layer.update()
    # Preserve the fitted thorax's near-neutral curvature. Rotating this long
    # segment forward had also translated the entire neck/skull, even when the
    # face itself looked level. Correct the chain, not only the face rotation.
    # Anatomical world frame: forward -Y, superior +Z.
    # Reduce the excessive lumbar bow without pitching the entire pelvis or
    # replacing its anatomical joint centres. Leave a small natural curvature.
    # Retain each lumbar segment's fitted bind roll. Forcing a new transverse
    # frame here introduces axial twist into the shirt and waistband.
    aim_world('Spine',(0,-.018,.072))
    aim_world('Spine1',(0,-.005,.098))
    aim_world('Spine2',(0,.015,.326),Vector((0,-1,0)))
    aim_world('Neck',(0,.015,1))
    skull=arm.data.bones['Head']
    aim_world('Head',arm.matrix_world.to_3x3()@(skull.tail_local-skull.head_local))
    for side,sign in [('Left',1),('Right',-1)]:
        left=side=='Left'
        # Let the upper arms hang, with a small asymmetric relaxed elbow bend.
        # These are authored pose targets, not population ROM maxima.
        # Garment thickness needs genuine clearance from the torso/thigh, not
        # just separated bone centres. Avoid the former hand/waist overlap.
        upper=Vector((sign*(.20 if left else .18),.025 if left else .045,-1)).normalized()
        # Flexion is measured between the actual limb segments, not a raw
        # Blender Euler channel. Elbow bends toward body-front, knee toward back.
        def hinge(direction,toward,angle,minimum,maximum):
            axis=direction.cross(toward).normalized()
            return Quaternion(axis,math.radians(max(minimum,min(maximum,angle))))@direction
        lower=hinge(upper,Vector((0,-1,0)),24 if left else 22,3,140)
        aim_world(side+'Arm',upper)
        aim_world(side+'ForeArm',lower)
        hand=hinge(lower,Vector((0,1,0)),3 if left else 4,-15,15)
        aim_world(side+'Hand',hand)
        # Keep flexion in the sagittal plane. The old lateral leg slant paired
        # with a level foot created visible ankle inversion despite bank == 0.
        turnout=Quaternion((0,0,1),math.radians(sign*8))
        thigh=turnout@Vector((0,-.02,-1)).normalized()
        # Turn the hinge plane with the leg, rather than twisting just a shoe
        # against an otherwise forward-facing knee.
        shin=turnout@hinge(Vector((0,-.02,-1)).normalized(),Vector((0,1,0)),4,2,130)
        leg_front=turnout@Vector((0,-1,0))
        aim_world(side+'UpLeg',thigh,leg_front)
        aim_world(side+'Leg',shin,leg_front)
        aim_world(side+'Foot',turnout@Vector((0,-1,-.10)),Vector((0,0,1)))
        aim_world(side+'ToeBase',turnout@Vector((0,-1,-.10)),Vector((0,0,1)))
        for digit,label in enumerate(['Thumb','Index','Middle','Ring','Pinky']):
            for segment in range(1,4):
                p=arm.pose.bones[side+'Hand'+label+str(segment)]
                angles=[8,12,6] if digit==0 else [12+digit*2,22+digit*2,12]
                p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion((1,0,0),math.radians(angles[segment-1]))
    waiting={p.name:p.matrix_basis.copy() for p in arm.pose.bones}
    bpy.context.view_layer.update()
    breath_targets=[('Spine1',.35),('Spine2',-.20),('LeftArm',.65),('RightArm',-.55)]
    breath_axes={name:(arm.matrix_world@arm.pose.bones[name].matrix).to_quaternion().inverted()@Vector((1,0,0)) for name,angle in breath_targets}
    idle=bpy.data.actions.new('Idle');idle.use_fake_user=True
    arm.animation_data.action=idle
    for i in range(91):
        phase=2*math.pi*i/90
        for p in arm.pose.bones:
            loc,rot,scale=waiting[p.name].decompose()
            p.location=loc;p.rotation_mode='QUATERNION';p.rotation_quaternion=rot;p.scale=scale
        # Subtle breathing, no pelvis translation or toe lift. Feet and the
        # fitted clavicles stay planted / relaxed for the whole waiting loop.
        for name,angle in breath_targets:
            p=arm.pose.bones[name]
            p.rotation_quaternion=p.rotation_quaternion@Quaternion(breath_axes[name],math.radians(angle)*math.sin(phase))
        for p in arm.pose.bones:
            p.keyframe_insert('location',frame=i+1);p.keyframe_insert('rotation_quaternion',frame=i+1);p.keyframe_insert('scale',frame=i+1)
    idle['motion_source']='Fitted anatomical waiting stance, authored breathing'
    idle['duration_seconds']=3.0
    walk=make('Walk','07_03',120,(.8,1.45))
    return idle,walk
