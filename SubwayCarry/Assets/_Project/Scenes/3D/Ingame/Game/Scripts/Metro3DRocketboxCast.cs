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
        readonly Variant[] variants=new Variant[Names.Length];
        readonly bool[] lookedUp=new bool[Names.Length];
        sealed class Variant
        {
            internal GameObject model;
            internal Material[] materials;
        }

        internal bool TryMount(Metro3DPassenger actor,SkinnedMeshRenderer driver,Mesh sourceMesh,Transform[] driverBones,
            Metro3DRiderLook look,int seed,ref MountedAvatar current)
        {
            int index=2*(int)look+(seed&1);
            var data=Load(index);
            if(data==null)return false;
            if(current!=null&&current.Index==index)return true;
            float driverScale=look==Metro3DRiderLook.Child?.73f:look==Metro3DRiderLook.Senior?.94f:1f;
            var created=MountedAvatar.TryCreate(actor.transform,driver,sourceMesh,driverBones,data.model,data.materials,index,
                look==Metro3DRiderLook.Child?1.33f:look==Metro3DRiderLook.Senior?1.67f:1.76f,driverScale);
            if(created==null)return false;
            current?.Dispose();
            current=created;
            return true;
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
                if(data!=null)foreach(var material in data.materials)Object.Destroy(material);
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
            readonly (Transform upper,Transform lower,Transform end,Transform driverEnd,Transform toe,Transform driverToe,float floorOffset,float toeSlope)[] contacts;
            readonly Transform actor;
            readonly float standingPelvisOffset;
            readonly Palm driverLeftPalm, driverPalm;
            Palm publisherLeftPalm, publisherPalm;
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
            internal SkinnedMeshRenderer Skin {get;}
            MountedAvatar(Joint[] joints,GameObject model,Transform sourceHips,Transform targetHips,int index,
                SkinnedMeshRenderer skin,(Transform,Transform,Transform,Transform,Transform,Transform,float,float)[] contacts,Transform actor,float pelvisOffset,
                Palm driverLeftPalm,Palm publisherLeftPalm,Palm driverPalm,Palm publisherPalm)
            {
                this.joints=joints;this.model=model;this.sourceHips=sourceHips;this.targetHips=targetHips;
                Index=index;Skin=skin;this.contacts=contacts;
                this.actor=actor;standingPelvisOffset=pelvisOffset;
                this.driverLeftPalm=driverLeftPalm;this.publisherLeftPalm=publisherLeftPalm;
                this.driverPalm=driverPalm;this.publisherPalm=publisherPalm;
            }

            static Dictionary<string,Transform> ByName(Transform root)
            {
                var result=new Dictionary<string,Transform>();
                foreach(var bone in root.GetComponentsInChildren<Transform>(true))result.TryAdd(bone.name,bone);
                return result;
            }

            internal static MountedAvatar TryCreate(Transform actor,SkinnedMeshRenderer driver,Mesh sourceMesh,Transform[] driverBones,
                GameObject prefab,Material[] materials,int index,float height,float driverScale)
            {
                if(sourceMesh==null||driverBones==null)return null;
                var rig=Object.Instantiate(prefab,actor,false);
                rig.name="Rocketbox "+prefab.name;
                var skin=rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var target=ByName(rig.transform);
                string prefix=target.ContainsKey("Bip01 Pelvis")?"Bip01":"Bip02";
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
                var touches=new List<(Transform,Transform,Transform,Transform,Transform,Transform,float,float)>();
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
                        touches.Add((target[Key(p+"Thigh")],target[Key(p+"Calf")],ankle,foot.bone,toe,driverToe.bone,
                            heightToAnkle-.1005f*driverScale,Mathf.Clamp(slope,-.65f,.2f)));
                    }
                    if(source.TryGetValue("DEF-hand"+s,out var hand))
                        touches.Add((target[Key(p+"UpperArm")],target[Key(p+"Forearm")],target[Key(p+"Hand")],hand.bone,null,null,0,0));
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
                return new MountedAvatar(joints.ToArray(),rig,hips.bone,pelvis,index,skin,touches.ToArray(),actor,Mathf.Clamp(hipsOffset,-.19f,.19f),
                    SourcePalm("L"),TargetPalm("L"),SourcePalm("R"),TargetPalm("R"));
            }

            internal void Apply(float sitting,bool alignHands,float leftWristWeight,float rightWristWeight)
            {
                RestoreIdleWrists();
                if(!Skin.isVisible&&!Skin.updateWhenOffscreen)return;
                targetHips.position=sourceHips.position+actor.up*(standingPelvisOffset*(1-sitting));
                foreach(var joint in joints){
                    var desired=joint.source.rotation*joint.rotationOffset;
                    if(joint.finger&&!alignHands)
                        joint.target.localRotation=Quaternion.Slerp(joint.restLocal,Quaternion.Inverse(joint.target.parent.rotation)*desired,.22f);
                    else joint.target.rotation=desired;
                }
                foreach(var limb in contacts){
                    bool foot=limb.toe!=null;
                    if(!foot&&!alignHands)continue;
                    Vector3 root=limb.upper.position,end=limb.end.position;
                    Vector3 wanted=limb.driverEnd.position+actor.up*limb.floorOffset;
                    float error=(wanted-end).sqrMagnitude;
                    if(error>.00015f&&(foot||error<.24f*.24f)) {
                        Vector3 delta=wanted-root,v=delta.normalized;
                        float a=Vector3.Distance(root,limb.lower.position),b=Vector3.Distance(limb.lower.position,end);
                        float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.004f,a+b-.006f);
                        Vector3 bend=Vector3.ProjectOnPlane(foot?actor.forward:limb.lower.position-root,v).normalized;
                        if(bend.sqrMagnitude>.1f) {
                            float along=(a*a-b*b+distance*distance)/(2*distance);
                            Vector3 knee=root+v*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
                            Quaternion endRotation=limb.end.rotation;
                            limb.upper.rotation=Quaternion.FromToRotation(limb.lower.position-root,knee-root)*limb.upper.rotation;
                            limb.lower.rotation=Quaternion.FromToRotation(limb.end.position-limb.lower.position,root+v*distance-limb.lower.position)*limb.lower.rotation;
                            limb.end.rotation=endRotation;
                        }
                    }
                    if(foot&&limb.driverToe!=null&&Vector3.Dot(limb.driverEnd.position-actor.position,actor.up)<.115f){
                        Vector3 line=Vector3.ProjectOnPlane(limb.driverToe.position-limb.driverEnd.position,actor.up).normalized;
                        Vector3 slope=line+actor.up*(limb.toeSlope/Mathf.Sqrt(1-limb.toeSlope*limb.toeSlope));
                        if(line.sqrMagnitude>.1f)limb.end.rotation=Quaternion.FromToRotation(limb.toe.position-limb.end.position,slope)*limb.end.rotation;
                    }
                }
                // The next retarget pass rewrites both wrists from the driver.
                // Apply the idle adjustment after all normal hand and finger work.
                if (leftWristWeight > 0) publisherLeftPalm.AlignTo(driverLeftPalm,leftWristWeight);
                if (rightWristWeight > 0) publisherPalm.AlignTo(driverPalm,rightWristWeight);
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
                var match=to*Quaternion.Inverse(from);
                // Move the rendered prop to the visible grip, retaining the
                // authored orientation and avoiding a new contact solver.
                Vector3 position=destination+match*(phone.position-source);
                phone.SetPositionAndRotation(Vector3.MoveTowards(phone.position,position,.105f),
                    Quaternion.RotateTowards(phone.rotation,match*phone.rotation,38f));
            }
        }
    }

}
