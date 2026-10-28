using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Test-only Generic post-pose solver. Rotations preserve chain lengths;
    // no Humanoid IK, bone scaling or second CharacterController movement.
    [DefaultExecutionOrder(300)]
    public sealed class AnimationLabContacts : MonoBehaviour
    {
        static readonly Unity.Profiling.ProfilerMarker ContactProfile=new Unity.Profiling.ProfilerMarker("SubwayCarry.AnimationLab.Contacts");
        public AnimationLabCharacter character;
        public bool correction=true;
        Metro3DPassenger review;
        bool Frozen=>review!=null?review.WalkReviewFrozen:character.Frozen;
        Transform Body=>review!=null?review.transform:character.transform;
        public float MaxFootError {get;private set;}
        public float PelvisCorrection {get;private set;}
        public float BackError {get;private set;}=-1;
        public bool LeftPlanted=>legs!=null&&legs[0].planted;
        public bool RightPlanted=>legs!=null&&legs[1].planted;
        public bool Ready=>hips!=null;
        public Vector3 Hips=>hips!=null?hips.position:transform.position;
        public Vector3 Foot(int i)=>legs[i].end.position;
        public float Length(int i)=>Vector3.Distance(legs[i].upper.position,legs[i].lower.position)+Vector3.Distance(legs[i].lower.position,legs[i].end.position);
        public float[] ReferenceLengths {get;private set;}
        sealed class Leg {public Transform upper,lower,end,toe;public Transform support;public Vector3 localPlant,plantForward;public bool planted,releasedForTurn;public float weight;}
        Leg[] legs;
        Transform hips,spine,chest;
        readonly Vector3[] targets=new Vector3[2],normals=new Vector3[2];
        readonly RaycastHit[] hits=new RaycastHit[2];
        readonly Transform[] corrected=new Transform[8];
        readonly Vector3[] authoredPositions=new Vector3[8];
        readonly Quaternion[] authoredRotations=new Quaternion[8];
        bool hasAuthored;
        void Awake()
        {
            Bind(character.animator);
        }
        public void BindReview(Metro3DPassenger actor){Clear();review=actor;Bind(actor!=null?actor.GetComponentInChildren<Animator>(true):character.animator);}
        void Bind(Animator animator)
        {
            var bones=new Dictionary<string,Transform>();foreach(var t in animator.GetComponentsInChildren<Transform>(true))bones[t.name]=t;
            if(!bones.TryGetValue("DEF-hips",out hips))return;
            spine=bones["DEF-spine.001"];chest=bones["DEF-spine.003"];
            legs=new Leg[2];ReferenceLengths=new float[2];
            for(int i=0;i<2;i++){string s=i==0?".L":".R";legs[i]=new Leg{upper=bones["DEF-thigh"+s],lower=bones["DEF-shin"+s],end=bones["DEF-foot"+s],toe=bones["DEF-toe"+s]};ReferenceLengths[i]=Length(i);}
            corrected[0]=hips;corrected[1]=spine;for(int i=0;i<2;i++){corrected[2+i*3]=legs[i].upper;corrected[3+i*3]=legs[i].lower;corrected[4+i*3]=legs[i].end;}
        }
        // Generic constant/unkeyed channels are not guaranteed to be rewritten
        // each frame. Never feed last frame's IK/lean offset back into Animator.
        void RestoreAuthored(){if(!hasAuthored)return;for(int i=0;i<corrected.Length;i++)if(corrected[i]!=null){corrected[i].localPosition=authoredPositions[i];corrected[i].localRotation=authoredRotations[i];}hasAuthored=false;}
        public void RestoreForSearch()=>RestoreAuthored();
        void Update(){if(character!=null&&!Frozen)RestoreAuthored();}
        public void Clear(){RestoreAuthored();if(legs==null)return;foreach(var leg in legs){leg.planted=leg.releasedForTurn=false;leg.support=null;leg.weight=0;}}
        void LateUpdate()
        {
            using var profile=ContactProfile.Auto();
            if(!Ready||Frozen)return;
            MaxFootError=PelvisCorrection=0;BackError=-1;
            if(!correction){Clear();return;}
            for(int i=0;i<corrected.Length;i++){authoredPositions[i]=corrected[i].localPosition;authoredRotations[i]=corrected[i].localRotation;}hasAuthored=true;
            float contact=review!=null?0:character.ContactWeight;
            var anchor=review!=null?null:character.Anchor;
            bool seat=contact>0&&anchor!=null&&anchor.kind==AnimationLabAnchor.Kind.Seat;
            bool lean=contact>0&&anchor!=null&&!seat;
            Vector3 point=Vector3.zero,normal=Vector3.up;
            if((seat||lean)&&!anchor.Contact(out point,out normal)){Clear();return;}
            if(seat) {
                // .072m is the retained dummy's pelvis-to-cushion clearance,
                // calibrated from its authored seated pose (.527-.455m).
                Vector3 destination=point+Vector3.up*.072f;
                Vector3 delta=(destination-hips.position)*contact;
                if(delta.magnitude>.65f){Clear();return;}
                hips.position+=delta;PelvisCorrection=delta.magnitude;
            } else if(lean) {
                spine.rotation=Quaternion.AngleAxis(-8*contact,character.transform.right)*spine.rotation;
                var back=chest.position+Vector3.up*.12f;
                float gap=Vector3.Dot(back-point,normal)-.13f;
                var shift=-normal*Mathf.Clamp(gap,-.08f,.14f)*contact;
                hips.position+=shift;PelvisCorrection=shift.magnitude;
                BackError=Mathf.Abs(Vector3.Dot(chest.position+Vector3.up*.12f-point,normal)-.13f);
            }
            // Per-foot terrain probes; do not use a single root floor height.
            float lower=0;
            for(int i=0;i<2;i++){
                var leg=legs[i];hits[i]=default;Vector3 foot=leg.end.position;
                if(seat)foot=point+anchor.contact.forward*.40f+anchor.contact.right*(i==0?-.13f:.13f)+Vector3.up*.1005f;
                if(!Physics.Raycast(foot+Vector3.up*.42f,Vector3.down,out hits[i],.8f,character.environment,QueryTriggerInteraction.Ignore)||hits[i].normal.y<.6f){leg.planted=false;continue;}
                var hit=hits[i];float desired=seat||lean?1:review!=null?(review.MotionTrial!=null&&(review.MotionTrial.Matching||review.MotionTrial.DirectClipIndex>=0)?review.MotionTrial.Contact(i):0):character.Support(i);
                if(character.field=="Walk"&&!seat&&!lean){
                    if(desired<.35f)leg.releasedForTurn=false;
                    if(leg.planted&&Vector3.Angle(leg.plantForward,Body.forward)>12){leg.planted=false;leg.releasedForTurn=true;}
                    if(leg.releasedForTurn)desired=0;
                }
                float lift=hit.point.y+.1005f-leg.end.position.y;
                if(!seat&&!lean&&Mathf.Abs(lift)>.24f){desired=0;leg.planted=false;}
                if(desired>.65f&&!leg.planted){leg.support=hit.collider.transform;leg.localPlant=leg.support.InverseTransformPoint(hit.point);leg.plantForward=Body.forward;leg.planted=true;}
                if(desired<.35f)leg.planted=false;
                Vector3 ground=leg.planted&&leg.support!=null?leg.support.TransformPoint(leg.localPlant):hit.point;
                if((Vector3.ProjectOnPlane(ground-foot,Vector3.up)).magnitude>.20f){leg.planted=false;ground=hit.point;}
                leg.weight=Mathf.MoveTowards(leg.weight,desired,Time.deltaTime*8);
                targets[i]=seat?foot:new Vector3(ground.x,ground.y+.1005f,ground.z);normals[i]=hit.normal;
                if(!seat&&!lean)lower=Mathf.Min(lower,lift*leg.weight);
            }
            if(!seat&&!lean){float down=Mathf.Clamp(lower,-.10f,0);hips.position+=Vector3.up*down;PelvisCorrection=Mathf.Abs(down);}
            for(int i=0;i<2;i++){
                var leg=legs[i];if(hits[i].collider==null)continue;
                Quaternion authored=leg.end.rotation;
                Solve(leg.upper,leg.lower,leg.end,targets[i],Body.forward,leg.weight);
                leg.end.rotation=Quaternion.Slerp(authored,Quaternion.FromToRotation(Vector3.up,normals[i])*authored,leg.weight);
                MaxFootError=Mathf.Max(MaxFootError,Vector3.Distance(leg.end.position,targets[i])*leg.weight);
            }
        }
        void OnDisable(){RestoreAuthored();}
        public static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 hint,float weight)
        {
            Vector3 root=upper.position;float a=Vector3.Distance(root,lower.position),b=Vector3.Distance(lower.position,end.position);
            var delta=target-root;if(delta.sqrMagnitude<.000001f)return;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.008f,a+b-.008f);
            var direction=delta.normalized;var bend=Vector3.ProjectOnPlane(hint,direction).normalized;
            if(bend.sqrMagnitude<.1f)bend=Vector3.ProjectOnPlane(lower.position-root,direction).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var knee=root+direction*along+bend*height;
            var endRotation=end.rotation;
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-root,knee-root)*upper.rotation,weight);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(end.position-lower.position,target-lower.position)*lower.rotation,weight);
            end.rotation=endRotation;
        }
    }
}
