using UnityEngine;
using Unity.Profiling;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Transform mask for an owned Generic upper-body clip; root, pelvis and legs are never in the mask.
    public sealed partial class AnimationLabPhoneLayer : MonoBehaviour
    {
        static readonly ProfilerMarker Marker=new ProfilerMarker("SubwayCarry.PhoneCase.UpperBody");
        AnimationLabPhoneMotion data;GameObject sample,phone;Transform[] targets,samples;Quaternion[] baseRotations;
        Transform neck,head,hand;Quaternion baseNeck,baseHead,neutralHeadInActor;bool applied;float time,yaw,pitch,yawVelocity,pitchVelocity;
        public bool WantsPhone {get;set;}
        public bool PresentationEnabled {get;set;}=true;
        public Vector3 LookTarget {get;set;}
        public float Weight {get;private set;}
        public Transform Phone=>phone!=null?phone.transform:null;
        public string AppliedRevision=>actionSetReady?(action!=null?action.revision:hold!=null?hold.revision:data.publishedRevision):data!=null?data.publishedRevision:"";
        public float ClipTime=>actionSetReady?ActionClipTime:data==null?0:data.upperLoop?Mathf.Repeat(time,Mathf.Max(.001f,data.upperBody.length)):Mathf.Min(time,data.upperBody.length);
        public void Initialize(AnimationLabPhoneMotion binding)
        {
            data=binding;sample=Instantiate(data.rig);sample.name="Phone upper clip sampler · bones only";sample.hideFlags=HideFlags.HideAndDontSave;
            foreach(var component in sample.GetComponentsInChildren<Component>(true))if(component!=null&&!(component is Transform))DestroyImmediate(component);
            var paths=data.HasActionSet?data.bothArmPaths:data.upperPaths;targets=new Transform[paths.Length];samples=new Transform[targets.Length];baseRotations=new Quaternion[targets.Length];
            for(int i=0;i<targets.Length;i++){targets[i]=transform.Find(paths[i]);samples[i]=sample.transform.Find(paths[i]);if(targets[i]==null||samples[i]==null)throw new System.InvalidOperationException("Phone mask bone missing: "+paths[i]);}
            hand=transform.Find(data.rightHandPath);neck=transform.Find(data.neckPath);head=transform.Find(data.headPath);if(hand==null||neck==null||head==null)throw new System.InvalidOperationException("Phone/gaze bones missing");
            neutralHeadInActor=Quaternion.Inverse(transform.rotation)*head.rotation;
            phone=Instantiate(data.phonePrefab,hand,false);phone.transform.localPosition=data.phoneLocalPosition;phone.transform.localRotation=data.phoneLocalRotation;LookTarget=head.position+transform.forward*5;InitializeActions();
        }
        public void RestoreBase()
        {if(!applied)return;for(int i=0;i<targets.Length;i++)targets[i].localRotation=baseRotations[i];neck.localRotation=baseNeck;head.localRotation=baseHead;applied=false;}
        public void Apply(float dt)
        {
            if(data==null)return;using var marker=Marker.Auto();RestoreBase();if(!PresentationEnabled)return;
            if(actionSetReady){ApplyActions(dt);return;}
            for(int i=0;i<targets.Length;i++)baseRotations[i]=targets[i].localRotation;baseNeck=neck.localRotation;baseHead=head.localRotation;
            Weight=Mathf.MoveTowards(Weight,WantsPhone?1:0,dt*2.1f);time+=dt;
            data.upperBody.SampleAnimation(sample,ClipTime);
            for(int i=0;i<targets.Length;i++)
            {
                // Fingers keep holding the phone while the arm is lowered for awareness/avoidance.
                float weight=data.upperPaths[i].Contains("thumb")||data.upperPaths[i].Contains("f_")?1:Weight;
                targets[i].localRotation=Quaternion.Slerp(baseRotations[i],samples[i].localRotation,weight);
            }
            var forward=head.position+transform.forward*5;var target=Vector3.Lerp(LookTarget.sqrMagnitude>.001f?LookTarget:forward,phone.transform.position,Weight);
            var local=transform.InverseTransformDirection(target-head.position);float wantedYaw=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-45,45),wantedPitch=Mathf.Clamp(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,-15,22);
            yaw=Mathf.SmoothDampAngle(yaw,wantedYaw,ref yawVelocity,.22f,95,dt);pitch=Mathf.SmoothDampAngle(pitch,wantedPitch,ref pitchVelocity,.22f,75,dt);
            Quaternion actor=transform.rotation,turn=(actor*Quaternion.Euler(pitch,yaw,0)*neutralHeadInActor)*Quaternion.Inverse(head.rotation);var headBefore=head.rotation;
            neck.rotation=Quaternion.Slerp(Quaternion.identity,turn,.55f)*neck.rotation;head.rotation=turn*headBefore;applied=true;
        }
        void OnDisable(){RestoreBase();CleanupActions();if(sample!=null)Destroy(sample);if(phone!=null)Destroy(phone);}
    }
}
