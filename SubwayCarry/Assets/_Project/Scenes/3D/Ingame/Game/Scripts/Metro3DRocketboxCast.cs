using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SubwayCarry.Gameplay3D
{
    // The animation and all contacts remain on the existing driver. Only the
    // publisher mesh and its original skin weights are visible to the camera.
    internal sealed class Metro3DRocketboxCast
    {
        const string PathPrefix="PassengerCast/";
        static readonly string[] Names={
            "Business_Male_02","Business_Female_04","Male_Adult_14","Female_Adult_04",
            "Male_Child_01","Female_Child_01","Male_Adult_09","Female_Adult_12"
        };
        static readonly string[] Prefixes={"m008","f020","m012","f004","cm001","cf001","m017","f012"};
        static readonly string[] LocalScans={"Eric","Claudia","Carla"};
        const string LocalScanFolder="PassengerCastLocal/";
        readonly Variant[] variants=new Variant[Names.Length+LocalScans.Length*3];
        readonly bool[] lookedUp=new bool[Names.Length+LocalScans.Length*3];
        sealed class Variant
        {
            internal GameObject model;
            internal Material[] materials;
        }

        internal bool TryMount(Metro3DPassenger actor,SkinnedMeshRenderer driver,Mesh sourceMesh,Transform[] driverBones,
            Metro3DRiderLook look,int seed,ref MountedAvatar current)
        {
            int index=2*(int)look+(seed&1);
            float driverScale=look==Metro3DRiderLook.Child?.73f:look==Metro3DRiderLook.Senior?.94f:1f;
            float height=look==Metro3DRiderLook.Child?1.33f:look==Metro3DRiderLook.Senior?1.67f:1.76f;
            MountedAvatar created=null;
            uint appearance=unchecked((uint)seed*2654435761u);
            if((look==Metro3DRiderLook.Office||look==Metro3DRiderLook.Student)&&appearance%5<2) {
                int variant=Names.Length+(int)((appearance>>8)%9);
                if(current!=null&&current.Index==variant)return true;
                var scan=LoadLocalScan(variant);
                if(scan!=null)
                    created=MountedAvatar.TryCreate(actor.transform,driver,sourceMesh,driverBones,scan.model,scan.materials,
                        variant,height,driverScale,localScan:true);
            }
            if(created==null) {
                var data=Load(index);
                if(data==null)return false;
                if(current!=null&&current.Index==index)return true;
                created=MountedAvatar.TryCreate(actor.transform,driver,sourceMesh,driverBones,data.model,data.materials,index,height,driverScale);
            }
            if(created==null)return false;
            current?.Dispose();
            current=created;
            return true;
        }

        Variant LoadLocalScan(int index)
        {
            if(lookedUp[index])return variants[index];
            lookedUp[index]=true;
            int variant=index-Names.Length;
            string name=LocalScans[variant/3];
            int shade=variant%3;
            string folder=LocalScanFolder+name+"/";
            var model=Resources.Load<GameObject>(folder+name);
            if(model==null)return null;
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            var texture=Resources.Load<Texture2D>(folder+name+(shade==0?"":shade.ToString()));
            if(skin==null||skin.sharedMesh==null||shader==null||texture==null)return null;
            var material=new Material(shader){name="Passenger "+name+" "+shade};
            material.SetTexture("_BaseMap",texture);
            material.SetFloat("_Smoothness",.18f);
            var normal=Resources.Load<Texture2D>(folder+name+"_nor_gl");
            if(normal!=null) {
                material.SetTexture("_BumpMap",normal);
                material.SetFloat("_BumpScale",.55f);
                material.EnableKeyword("_NORMALMAP");
            }
            var slots=new Material[skin.sharedMesh.subMeshCount];
            Array.Fill(slots,material);
            return variants[index]=new Variant{model=model,materials=slots};
        }

        Variant Load(int index)
        {
            if(lookedUp[index])return variants[index];
            lookedUp[index]=true;
            string folder=PathPrefix+Names[index]+"/";
            var model=Resources.Load<GameObject>(folder+Names[index]);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(model==null||shader==null) {
                Debug.LogWarning($"Passenger cast: missing model or shader for {Names[index]}");
                return null;
            }
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if(skin==null||skin.sharedMesh==null||skin.sharedMesh.subMeshCount<2||skin.sharedMesh.subMeshCount>3) {
                Debug.LogWarning($"Passenger cast: unexpected mesh or material slots for {Names[index]}");
                return null;
            }
            var publisherSlots=skin.sharedMaterials;
            var surfaces=new Material[skin.sharedMesh.subMeshCount];
            for(int i=0;i<surfaces.Length;i++){
                string slot=i<publisherSlots.Length?publisherSlots[i]?.name:null;
                string map=slot!=null&&slot.StartsWith(Prefixes[index],StringComparison.Ordinal)
                    ?slot.Substring(Prefixes[index].Length):"";
                if(map!="_body"&&map!="_head"&&map!="_opacity"){
                    Debug.LogWarning($"Passenger cast: unrecognized material {slot} for {Names[index]}");
                    foreach(var surface in surfaces)if(surface!=null)Object.Destroy(surface);
                    return null;
                }
                var color=Resources.Load<Texture2D>(folder+Prefixes[index]+map+"_color");
                if(color==null){
                    Debug.LogWarning($"Passenger cast: missing color map {folder}{Prefixes[index]}{map}");
                    foreach(var surface in surfaces)if(surface!=null)Object.Destroy(surface);
                    return null;
                }
                var material=new Material(shader){name=Names[index]+map};
                material.SetTexture("_BaseMap",color);
                if(map=="_body")material.SetColor("_BaseColor",index%2==0?new Color(.97f,.98f,1):new Color(1,.98f,.96f));
                material.SetFloat("_Smoothness",map=="_head"?.22f:.16f);
                var normal=Resources.Load<Texture2D>(folder+Prefixes[index]+map+"_normal");
                if(normal!=null) {
                    material.SetTexture("_BumpMap",normal);
                    material.SetFloat("_BumpScale",.58f);
                    material.EnableKeyword("_NORMALMAP");
                }
                if(map=="_opacity"){
                    material.SetFloat("_AlphaClip",1);
                    material.SetFloat("_Cutoff",.46f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    material.SetOverrideTag("RenderType","TransparentCutout");
                    material.SetFloat("_Cull",0);
                }
                surfaces[i]=material;
            }
            return variants[index]=new Variant{model=model,materials=surfaces};
        }

        internal void Dispose()
        {
            foreach(var data in variants)
                if(data!=null) {
                    var unique=new HashSet<Material>(data.materials);
                    foreach(var material in unique)Object.Destroy(material);
                }
        }

        internal sealed class MountedAvatar
        {
            struct Joint
            {
                internal Transform source,target;
                internal Quaternion rotationOffset;
                internal Quaternion restLocal;
                internal bool finger;
            }
            readonly Joint[] joints;
            readonly GameObject model;
            readonly Transform sourceHips,targetHips;
            readonly (Transform upper,Transform lower,Transform end,Transform driverEnd,Transform toe,Transform driverToe,Transform middle,float floorOffset,float toeSlope)[] contacts;
            readonly Transform actor;
            readonly float standingPelvisOffset;
            readonly Palm driverPalm, driverLeftPalm;
            Palm publisherPalm, publisherLeftPalm;
            struct LapSide
            {
                internal Transform thigh,knee,upper,elbow;
                internal Palm palm;
                internal Transform[] fingers;
                internal Vector3 ThighPoint=>Vector3.Lerp(thigh.position,knee.position,.53f);
                internal float Length=>Vector3.Distance(thigh.position,knee.position);
            }
            readonly struct LapFit
            {
                internal readonly float height,pitch,roll;
                internal LapFit(float height,float pitch=0,float roll=0)
                {this.height=height;this.pitch=pitch;this.roll=roll;}
            }
            LapSide[] lap;
            static readonly Dictionary<Mesh,LapFit[]> lapFits=new();
            readonly Transform publisherThumb;
            struct Palm
            {
                readonly Transform hand,index,middle,pinky;
                Quaternion? idleInput;
                internal Palm(Transform hand,Transform index,Transform middle,Transform pinky)
                {this.hand=hand;this.index=index;this.middle=middle;this.pinky=pinky;idleInput=null;}
                internal void AlignTo(Palm driver,float weight)
                {
                    if (!TryPose(out _, out var current) || !driver.TryPose(out _, out var desired)) return;
                    idleInput = hand.localRotation;
                    hand.rotation = Quaternion.Slerp(Quaternion.identity, desired * Quaternion.Inverse(current), weight) * hand.rotation;
                }
                internal void RestoreIdleInput()
                {
                    if(idleInput is Quaternion rotation && hand!=null) hand.localRotation=rotation;
                    idleInput=null;
                }
                internal Transform Hand=>hand;
                internal Quaternion RelaxedRotation(Transform elbow,Transform frame)
                    =>Metro3DPassengerContacts.RelaxedWristRotation(elbow,hand,middle,index,pinky,frame.forward,frame.up);

                internal bool TryPose(out Vector3 position,out Quaternion rotation)
                {
                    position=default;rotation=Quaternion.identity;
                    if(hand==null||index==null||middle==null||pinky==null)return false;
                    Vector3 toward=middle.position-hand.position;
                    Vector3 normal=Vector3.Cross(toward,index.position-pinky.position);
                    if(toward.sqrMagnitude<.00001f||normal.sqrMagnitude<.00000001f)return false;
                    position=Vector3.Lerp(hand.position,middle.position,.67f);
                    rotation=Quaternion.LookRotation(toward,normal);
                    return true;
                }
            }
            internal readonly int Index;
            internal bool IsLocalScan => Index>=Names.Length;
            internal SkinnedMeshRenderer Skin {get;}
            MountedAvatar(Joint[] joints,GameObject model,Transform sourceHips,Transform targetHips,int index,
                SkinnedMeshRenderer skin,(Transform,Transform,Transform,Transform,Transform,Transform,Transform,float,float)[] contacts,Transform actor,float pelvisOffset,
                Palm driverPalm,Palm publisherPalm,Palm driverLeftPalm,Palm publisherLeftPalm,Transform publisherThumb)
            {
                this.joints=joints;this.model=model;this.sourceHips=sourceHips;this.targetHips=targetHips;
                Index=index;Skin=skin;this.contacts=contacts;
                this.actor=actor;standingPelvisOffset=pelvisOffset;
                this.driverPalm=driverPalm;this.driverLeftPalm=driverLeftPalm;this.publisherPalm=publisherPalm;this.publisherLeftPalm=publisherLeftPalm;this.publisherThumb=publisherThumb;
            }

            static Dictionary<string,Transform> ByName(Transform root)
            {
                var result=new Dictionary<string,Transform>();
                foreach(var bone in root.GetComponentsInChildren<Transform>(true))result.TryAdd(bone.name,bone);
                return result;
            }

            static void AliasScanRig(Dictionary<string,Transform> bones)
            {
                void Alias(string key,string source) {
                    if(bones.TryGetValue(source,out var bone))bones["Scan "+key]=bone;
                }
                Alias("Pelvis","hip");Alias("Spine","spine_01");Alias("Spine1","spine_02");
                Alias("Spine2","spine_03");Alias("Neck","neck");Alias("Head","head");
                foreach(string side in new[]{"L","R"}) {
                    string suffix="_"+side.ToLowerInvariant();
                    Alias(side+" Clavicle","shoulder"+suffix);
                    Alias(side+" UpperArm","upperarm"+suffix);
                    Alias(side+" Forearm","lowerarm"+suffix);
                    Alias(side+" Hand","hand"+suffix);
                    Alias(side+" Thigh","upperleg"+suffix);
                    Alias(side+" Calf","lowerleg"+suffix);
                    Alias(side+" Foot","foot"+suffix);
                    Alias(side+" Toe0","ball"+suffix);
                    string[] digits={"thumb","index","middle","ring","pinky"};
                    for(int digit=0;digit<digits.Length;digit++)
                        for(int segment=0;segment<3;segment++)
                            Alias(side+" Finger"+digit+(segment==0?"":segment.ToString()),
                                digits[digit]+"_0"+(segment+1)+suffix);
                }
            }

            internal static MountedAvatar TryCreate(Transform actor,SkinnedMeshRenderer driver,Mesh sourceMesh,Transform[] driverBones,
                GameObject prefab,Material[] materials,int index,float height,float driverScale,bool localScan=false)
            {
                if(sourceMesh==null||driverBones==null)return null;
                var rig=Object.Instantiate(prefab,actor,false);
                rig.name=(localScan?"Local commuter ":"Rocketbox ")+prefab.name;
                var skin=rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var target=ByName(rig.transform);
                if(localScan)AliasScanRig(target);
                string prefix=localScan?"Scan":target.ContainsKey("Bip01 Pelvis")?"Bip01":"Bip02";
                var source=new Dictionary<string,(Transform bone, Matrix4x4 rest)>();
                var bind=sourceMesh.bindposes;
                Matrix4x4 modelFrame=driver.transform.localToWorldMatrix;
                for(int i=0;i<Mathf.Min(driverBones.Length,bind.Length);i++)
                    if(driverBones[i]!=null)source.TryAdd(driverBones[i].name,(driverBones[i],modelFrame*bind[i].inverse));
                string Key(string bone)=>prefix+" "+bone;
                if(skin==null||!source.TryGetValue("DEF-hips",out var hips)||!target.TryGetValue(Key("Pelvis"),out var pelvis)){
                    Debug.LogWarning($"Passenger cast: cannot find pelvis binding in {prefab.name} ({source.Count} source bones, {target.Count} target transforms)");
                    Object.Destroy(rig);return null;
                }

                if(localScan) {
                    foreach(string required in new[]{"Head","L Thigh","R Thigh","L Foot","R Foot","L Toe0","R Toe0",
                        "L UpperArm","R UpperArm","L Forearm","R Forearm","L Hand","R Hand","L Calf","R Calf","L Finger2","R Finger2"})
                        if(!target.ContainsKey(Key(required))) {Object.Destroy(rig);return null;}
                }
                Vector3 across=target[Key("R Thigh")].position-target[Key("L Thigh")].position;
                Vector3 forward=Vector3.Cross(Vector3.ProjectOnPlane(across,actor.up),actor.up);
                if(forward.sqrMagnitude>.0001f)
                    rig.transform.rotation=Quaternion.FromToRotation(forward,actor.forward)*rig.transform.rotation;

                Transform left=target[Key("L Foot")],right=target[Key("R Foot")],head=target[Key("Head")];
                Vector3 ankles=(left.position+right.position)*.5f;
                float boneHeight=Vector3.Dot(head.position-ankles,actor.up);
                if(boneHeight<.015f||!float.IsFinite(boneHeight)){Object.Destroy(rig);return null;}
                float heightToAnkle=.050f*height+.012f;
                // FBX bounds may contain extrema from the publisher animation
                // set. Fit the neutral skeleton, uniformly, instead.
                rig.transform.localScale*=height*.84f/boneHeight;
                ankles=(left.position+right.position)*.5f;
                rig.transform.position+=actor.up*(heightToAnkle-Vector3.Dot(ankles-actor.position,actor.up));
                var joints=new List<Joint>();
                void Add(string name,string puppet,string child=null,string puppetChild=null,bool finger=false)
                {
                    if(!source.TryGetValue(name,out var from)||!target.TryGetValue(Key(puppet),out var to))return;
                    var reference=to.rotation;
                    if(child!=null&&source.TryGetValue(child,out var lead)&&target.TryGetValue(Key(puppetChild),out var goal)){
                        Vector3 a=goal.position-to.position;
                        Vector3 b=lead.rest.GetColumn(3)-from.rest.GetColumn(3);
                        if(a.sqrMagnitude>.00001f&&b.sqrMagnitude>.00001f)reference=Quaternion.FromToRotation(a,b)*reference;
                    }
                    joints.Add(new Joint{source=from.bone,target=to,rotationOffset=Quaternion.Inverse(from.rest.rotation)*reference,
                        restLocal=to.localRotation,finger=finger});
                }
                Add("DEF-hips","Pelvis");
                Add("DEF-spine.001","Spine");
                Add("DEF-spine.002","Spine1");
                Add("DEF-spine.003","Spine2");
                Add("DEF-neck","Neck");
                Add("DEF-head","Head");
                var touches=new List<(Transform,Transform,Transform,Transform,Transform,Transform,Transform,float,float)>();
                foreach(string side in new[]{"L","R"}){
                    string s="."+side,p=side+" ";
                    Add("DEF-shoulder"+s,p+"Clavicle");
                    Add("DEF-upper_arm"+s,p+"UpperArm","DEF-forearm"+s,p+"Forearm");
                    Add("DEF-forearm"+s,p+"Forearm","DEF-hand"+s,p+"Hand");
                    Add("DEF-hand"+s,p+"Hand");
                    Add("DEF-thigh"+s,p+"Thigh","DEF-shin"+s,p+"Calf");
                    Add("DEF-shin"+s,p+"Calf","DEF-foot"+s,p+"Foot");
                    Add("DEF-foot"+s,p+"Foot","DEF-toe"+s,p+"Toe0");
                    Add("DEF-toe"+s,p+"Toe0");
                    string[] digits={"thumb","f_index","f_middle","f_ring","f_pinky"};
                    for(int digit=0;digit<digits.Length;digit++)
                        for(int segment=0;segment<3;segment++)
                            Add($"DEF-{digits[digit]}.0{segment+1}{s}",p+"Finger"+digit+(segment==0?"":segment.ToString()),finger:true);
                    if(source.TryGetValue("DEF-foot"+s,out var foot)) {
                        Transform ankle=target[Key(p+"Foot")];
                        Transform toe=target[Key(p+"Toe0")];
                        float slope=Vector3.Dot((toe.position-ankle.position).normalized,actor.up);
                        source.TryGetValue("DEF-toe"+s,out var driverToe);
                        touches.Add((target[Key(p+"Thigh")],target[Key(p+"Calf")],ankle,foot.bone,toe,driverToe.bone,null,
                            heightToAnkle-.1005f*driverScale,Mathf.Clamp(slope,-.65f,.2f)));
                    }
                    if(source.TryGetValue("DEF-hand"+s,out var hand))
                        touches.Add((target[Key(p+"UpperArm")],target[Key(p+"Forearm")],target[Key(p+"Hand")],hand.bone,null,null,
                            target[Key(p+"Finger2")],0,0));
                }
                if(joints.Count<25){
                    Debug.LogWarning($"Passenger cast: only {joints.Count} mapped joints for {prefab.name}");
                    Object.Destroy(rig);return null;
                }
                skin.sharedMaterials=materials;
                skin.quality=SkinQuality.Bone4;
                var bounds=skin.localBounds;
                bounds.Expand(.65f);
                skin.localBounds=bounds;
                foreach(var animator in rig.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                foreach(var collider in rig.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                float hipsOffset=Vector3.Dot(pelvis.position-actor.position,actor.up)-.86f*driverScale;
                Transform Driver(string name)=>source.TryGetValue(name,out var entry)?entry.bone:null;
                Transform Publisher(string name)=>target.TryGetValue(Key(name),out var entry)?entry:null;
                Palm SourcePalm(string side)=>new Palm(Driver("DEF-hand."+side),Driver("DEF-f_index.01."+side),
                    Driver("DEF-f_middle.01."+side),Driver("DEF-f_pinky.01."+side));
                Palm TargetPalm(string side)=>new Palm(Publisher(side+" Hand"),Publisher(side+" Finger1"),
                    Publisher(side+" Finger2"),Publisher(side+" Finger4"));
                LapSide Lap(string side)
                {
                    var fingers=new Transform[15];
                    for(int digit=0;digit<5;digit++)for(int joint=0;joint<3;joint++)
                        fingers[digit*3+joint]=Publisher(side+" Finger"+digit+(joint==0?"":joint.ToString()));
                    return new LapSide {thigh=Publisher(side+" Thigh"),knee=Publisher(side+" Calf"),
                        upper=Publisher(side+" UpperArm"),elbow=Publisher(side+" Forearm"),palm=TargetPalm(side),fingers=fingers};
                }
                return new MountedAvatar(joints.ToArray(),rig,hips.bone,pelvis,index,skin,touches.ToArray(),actor,Mathf.Clamp(hipsOffset,-.19f,.19f),
                    SourcePalm("R"),TargetPalm("R"),SourcePalm("L"),
                    TargetPalm("L"),Publisher("R Finger02")) {lap=new[]{Lap("L"),Lap("R")}};
            }

            internal void Apply(float sitting,bool alignHands,bool holdingPhone,float relaxedLeft,float relaxedRight,float leftWristWeight,float rightWristWeight,bool initialPose=false,float lapWeight=0)
            {
                RestoreIdleWrists();
                if(!initialPose&&!Skin.isVisible&&!Skin.updateWhenOffscreen)return;
                targetHips.position=sourceHips.position+actor.up*(standingPelvisOffset*(1-sitting));
                foreach(var joint in joints){
                    var desired=joint.source.rotation*joint.rotationOffset;
                    if(joint.finger&&!alignHands)
                        joint.target.localRotation=Quaternion.Slerp(joint.restLocal,Quaternion.Inverse(joint.target.parent.rotation)*desired,.22f);
                    else joint.target.rotation=desired;
                }
                foreach(var limb in contacts){
                    bool foot=limb.toe!=null;
                    if(!foot) {
                        bool left=limb.end==publisherLeftPalm.Hand;
                        float relaxed=left?relaxedLeft:relaxedRight;
                        if(relaxed>.001f||!alignHands) {
                            Quaternion rotation=Metro3DPassengerContacts.LimitWristRotation(limb.lower,limb.end,limb.middle,31);
                            if(relaxed>.001f) {
                                var palm=left?publisherLeftPalm:publisherPalm;
                                rotation=Quaternion.Slerp(rotation,palm.RelaxedRotation(limb.lower,actor),relaxed);
                            }
                            limb.end.rotation=rotation;
                            continue;
                        }
                    }
                    Vector3 root=limb.upper.position,end=limb.end.position;
                    Vector3 wanted=limb.driverEnd.position+actor.up*limb.floorOffset;
                    float error=(wanted-end).sqrMagnitude;
                    if(error>.00015f&&(foot||error<.24f*.24f)) {
                        Reach(limb.upper,limb.lower,limb.end,wanted,foot?actor.forward:limb.lower.position-root);
                    }
                    if(foot&&limb.driverToe!=null&&Vector3.Dot(limb.driverEnd.position-actor.position,actor.up)<.115f){
                        Vector3 line=Vector3.ProjectOnPlane(limb.driverToe.position-limb.driverEnd.position,actor.up).normalized;
                        Vector3 slope=line+actor.up*(limb.toeSlope/Mathf.Sqrt(1-limb.toeSlope*limb.toeSlope));
                        if(line.sqrMagnitude>.1f)limb.end.rotation=Quaternion.FromToRotation(limb.toe.position-limb.end.position,slope)*limb.end.rotation;
                    }
                    if(!foot)
                        limb.end.rotation=Metro3DPassengerContacts.LimitWristRotation(limb.lower,limb.end,limb.middle,holdingPhone?32:42);
                }
                // The next retarget pass rewrites both wrists from the driver.
                // Apply the idle adjustment after all normal hand and finger work.
                if (leftWristWeight > 0) publisherLeftPalm.AlignTo(driverLeftPalm,leftWristWeight*(1-relaxedLeft));
                if (rightWristWeight > 0) publisherPalm.AlignTo(driverPalm,rightWristWeight*(1-relaxedRight));
                if(lapWeight>.001f)RestPalmsOnLap(sitting,lapWeight);
            }

            static void Reach(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 bendHint)
            {
                Vector3 root=upper.position,delta=target-root,v=delta.normalized;
                Vector3 bend=Vector3.ProjectOnPlane(bendHint,v).normalized;
                if(bend.sqrMagnitude<=.1f)return;
                float a=Vector3.Distance(root,lower.position),b=Vector3.Distance(lower.position,end.position);
                float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.004f,a+b-.006f);
                float along=(a*a-b*b+distance*distance)/(2*distance);
                Vector3 joint=root+v*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
                Quaternion rotation=end.rotation;
                upper.rotation=Quaternion.FromToRotation(lower.position-root,joint-root)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(end.position-lower.position,root+v*distance-lower.position)*lower.rotation;
                end.rotation=rotation;
            }

            void RestPalmsOnLap(float sitting,float weight)
            {
                var mesh=Skin.sharedMesh;
                lapFits.TryGetValue(mesh,out var fit);
                for(int i=0;i<lap.Length;i++) {
                    var side=lap[i];
                    if(!side.palm.TryPose(out _,out var basis))continue;
                    var hand=side.palm.Hand;
                    Vector3 along=(side.knee.position-side.thigh.position).normalized;
                    Vector3 normal=Vector3.ProjectOnPlane(actor.up,along).normalized;
                    Vector3 palmUp=Vector3.Dot(basis*Vector3.up,normal)<0?-normal:normal;
                    var flat=Quaternion.LookRotation(along,palmUp)*Quaternion.Inverse(basis);
                    hand.rotation=Quaternion.Slerp(Quaternion.identity,flat,weight)*hand.rotation;
                    for(int j=0;j<side.fingers.Length;j++) {
                        var bone=side.fingers[j];
                        if(!bone||bone.childCount==0)continue;
                        Vector3 segment=bone.GetChild(0).position-bone.position;
                        if(Vector3.Dot(segment,normal)>=0)continue;
                        var tangent=Vector3.ProjectOnPlane(segment,normal);
                        if(tangent.sqrMagnitude<.000001f)continue;
                        var straighten=Quaternion.FromToRotation(segment,tangent);
                        bone.rotation=Quaternion.Slerp(Quaternion.identity,straighten,weight)*bone.rotation;
                    }
                    PlaceLapHand(side,fit==null?new LapFit(.26f):fit[i],weight);
                }
                if(fit==null&&sitting>.99f&&weight>.99f) {
                    fit=MeasureLap();
                    lapFits.Add(mesh,fit);
                    for(int i=0;i<lap.Length;i++)PlaceLapHand(lap[i],fit[i],1);
                }
            }

            void PlaceLapHand(LapSide side,LapFit fit,float weight)
            {
                var hand=side.palm.Hand;
                Vector3 forward=(side.knee.position-side.thigh.position).normalized;
                hand.rotation=LapTilt(forward,actor.up,fit.pitch*weight,fit.roll*weight)*hand.rotation;
                if(!side.palm.TryPose(out Vector3 palm,out _))return;
                Vector3 contact=side.ThighPoint+actor.up*(side.Length*fit.height);
                Reach(side.upper,side.elbow,hand,hand.position+(contact-palm)*weight,side.elbow.position-side.upper.position);
            }

            static Quaternion LapTilt(Vector3 forward,Vector3 up,float pitch,float roll)
                =>Quaternion.AngleAxis(pitch,Vector3.Cross(up,forward).normalized)*Quaternion.AngleAxis(roll,forward);

            LapFit[] MeasureLap()
            {
                // Isolate hand and thigh geometry even on imports with mesh readback disabled.
                var left=lap[0].palm.Hand;var right=lap[1].palm.Hand;
                var l=left.localPosition;var r=right.localPosition;
                var baked=new Mesh();
                try {
                    Skin.BakeMesh(baked);
                    var posed=baked.vertices;var faces=baked.triangles;
                    left.position-=actor.right*.5f;right.position+=actor.right*.5f;
                    Skin.BakeMesh(baked);
                    var bare=baked.vertices;
                    left.localPosition=l;right.localPosition=r;
                    var leftThigh=lap[0].thigh;var rightThigh=lap[1].thigh;
                    var leftRest=leftThigh.localPosition;var rightRest=rightThigh.localPosition;
                    Vector3[] legs;
                    try {
                        leftThigh.position-=actor.right*.5f;rightThigh.position+=actor.right*.5f;
                        Skin.BakeMesh(baked);legs=baked.vertices;
                    } finally {leftThigh.localPosition=leftRest;rightThigh.localPosition=rightRest;}
                    var local=Matrix4x4.TRS(actor.position,actor.rotation,Vector3.one).inverse;
                    var frame=local*Skin.transform.localToWorldMatrix;
                    for(int i=0;i<posed.Length;i++) {
                        posed[i]=frame.MultiplyPoint3x4(posed[i]);
                        bare[i]=frame.MultiplyPoint3x4(bare[i]);
                        legs[i]=frame.MultiplyPoint3x4(legs[i]);
                    }
                    var fit=new LapFit[lap.Length];
                    for(int i=0;i<lap.Length;i++) {
                        var side=lap[i];
                        if(!side.palm.TryPose(out var palm,out _)){fit[i]=new LapFit(.26f);continue;}
                        fit[i]=FitLapContact(posed,bare,legs,faces,local.MultiplyPoint3x4(side.ThighPoint),
                            local.MultiplyPoint3x4(palm),local.MultiplyVector((side.knee.position-side.thigh.position).normalized),
                            side.Length,i==0?-1:1);
                    }
                    return fit;
                } finally {
                    left.localPosition=l;right.localPosition=r;
                    Object.Destroy(baked);
                }
            }

            static LapFit FitLapContact(Vector3[] hands,Vector3[] bare,Vector3[] legs,int[] faces,
                Vector3 thigh,Vector3 palm,Vector3 forward,float length,int side)
            {
                var points=new List<Vector3>();
                var footprint=new Bounds(palm,Vector3.zero);
                for(int i=0;i<hands.Length;i++) {
                    if((bare[i].x-hands[i].x)*side<.24f)continue;
                    footprint.Encapsulate(hands[i]);
                    points.Add(hands[i]);
                }
                footprint.Expand(.10f);
                var surface=new List<(Vector3 origin,Vector3 ab,Vector3 ac,Vector3 min,Vector3 max,float inverseDet)>();
                for(int i=0;i<faces.Length;i+=3) {
                    int ia=faces[i],ib=faces[i+1],ic=faces[i+2];
                    // The moving thigh identifies its own clothes, even when a sleeve
                    // or the other leg overlaps the hand in this projection.
                    float legShift=(legs[ia].x-hands[ia].x)+(legs[ib].x-hands[ib].x)+(legs[ic].x-hands[ic].x);
                    if(legShift*side<.69f)continue;
                    var a=bare[ia];var b=bare[ib];var c=bare[ic];
                    var min=Vector3.Min(a,Vector3.Min(b,c));var max=Vector3.Max(a,Vector3.Max(b,c));
                    if(min.x>footprint.max.x||max.x<footprint.min.x||min.z>footprint.max.z||max.z<footprint.min.z||
                        min.y>thigh.y+length*.55f||max.y<thigh.y-length*.35f)continue;
                    var ab=b-a;var ac=c-a;
                    float det=ab.x*ac.z-ab.z*ac.x;
                    if(Mathf.Abs(det)>1e-10f)surface.Add((a,ab,ac,min,max,1/det));
                }
                const float startingHeight=.26f;
                var fit=new LapFit(startingHeight);
                var gaps=new List<float>(points.Count);
                float best=float.PositiveInfinity;
                for(int pitch=-8;pitch<=24;pitch+=8)for(int roll=-12;roll<=12;roll+=12) {
                    gaps.Clear();
                    var tilt=LapTilt(forward,Vector3.up,pitch,roll);
                    foreach(var vertex in points) {
                        Vector3 point=palm+tilt*(vertex-palm);
                        float gap=float.NegativeInfinity;
                        foreach(var face in surface) {
                            if(point.x<face.min.x||point.x>face.max.x||point.z<face.min.z||point.z>face.max.z)continue;
                            var p=point-face.origin;
                            float u=(p.x*face.ac.z-p.z*face.ac.x)*face.inverseDet;
                            float v=(face.ab.x*p.z-face.ab.z*p.x)*face.inverseDet;
                            if(u>=0&&v>=0&&u+v<=1)gap=Mathf.Max(gap,face.ab.y*u+face.ac.y*v-p.y);
                        }
                        if(float.IsFinite(gap))gaps.Add(gap);
                    }
                    if(gaps.Count<points.Count*.35f||gaps.Count<3)continue;
                    gaps.Sort();
                    float At(float fraction)=>gaps[Mathf.RoundToInt((gaps.Count-1)*fraction)];
                    // Fit a patch of the hand to the cloth. Isolated skinning
                    // outliers are allowed to overlap slightly at the contact.
                    float lift=Mathf.Max(At(.86f)-.003f,At(.98f)-.012f);
                    float cost=lift-At(.55f)+Mathf.Abs(roll)*.00025f+Mathf.Abs(pitch-8)*.00018f;
                    if(cost>=best)continue;
                    best=cost;fit=new LapFit(startingHeight+lift/length,pitch,roll);
                }
                return fit;
            }

            internal void RestoreIdleWrists()
            {
                publisherLeftPalm.RestoreIdleInput();
                publisherPalm.RestoreIdleInput();
            }

            internal void Dispose()
            {
                if(model==null)return;
                model.SetActive(false);
                Object.Destroy(model);
            }

            internal void MatchPhonePalm(Transform phone)
            {
                if(!Skin.isVisible&&!Skin.updateWhenOffscreen||
                    !driverPalm.TryPose(out var source,out var from)||!publisherPalm.TryPose(out var destination,out var to))return;
                Quaternion match=to*Quaternion.Inverse(from);
                Quaternion rotation=match*phone.rotation;
                Vector3 position=destination+match*(phone.position-source);
                if(publisherThumb!=null) {
                    // Keep the authored grip orientation. Fit the handset body to
                    // the visible thumb when the two rigs have different reach.
                    Vector3 offset=Quaternion.Inverse(rotation)*(publisherThumb.position-position);
                    Vector3 scale=phone.lossyScale;
                    Vector3 edge=new Vector3(Mathf.Clamp(offset.x,-.0365f*scale.x,.0365f*scale.x),
                        Mathf.Clamp(offset.y,-.0725f*scale.y,.0725f*scale.y),
                        Mathf.Clamp(offset.z,-.005f*scale.z,.005f*scale.z));
                    position+=rotation*Vector3.ClampMagnitude(offset-edge,.027f);
                }
                phone.SetPositionAndRotation(position,rotation);
            }
        }
    }

}
