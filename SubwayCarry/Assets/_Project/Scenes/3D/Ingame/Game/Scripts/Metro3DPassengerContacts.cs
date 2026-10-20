using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Contact correction after the preserved generic Animator. No Humanoid
    // avatar/retargeting contract or team-wide quality setting is replaced.
    [DefaultExecutionOrder(100)]
    public sealed class Metro3DPassengerContacts : MonoBehaviour
    {
        public float GripError {get;private set;}=-1;
        public bool Holding {get;private set;}
        public bool Leaning {get;private set;}
        public float LeanError {get;private set;}=-1;
        public Vector3 ContactPoint {get;private set;}
        public float SeatFootError {get;private set;}=-1;
        public int PoseFrame {get;private set;}
        public bool ReviewWhileCulled {get;set;}
        Metro3DPassenger actor;
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
        SkinnedMeshRenderer skin;
        float supportWeight;
        Vector3 leftPlant,rightPlant;
        bool wasSeated;
        bool mannequin,rigReady;
        static readonly string[] Digits={"Thumb","Index","Middle","Ring","Pinky"};
        sealed class Chain {public Transform upper,lower,end;}
        sealed class Arm {
            public Chain chain;public Transform middle,index,pinky;public Transform[] fingers;
            public bool left;public float palmSign;
        }
        readonly Arm[] arms=new Arm[2];
        readonly Chain[] legs=new Chain[2];
        public void Initialize(Metro3DPassenger passenger)
        {
            actor=passenger;bones.Clear();supportWeight=0;wasSeated=false;
            skin=GetComponentInChildren<SkinnedMeshRenderer>(true);
            foreach(var t in GetComponentsInChildren<Transform>(true)) if(!bones.ContainsKey(t.name)) bones.Add(t.name,t);
            mannequin=bones.ContainsKey("DEF-hips");
            rigReady=true;
            for(int i=0;i<2;i++) {
                string side=i==0?"Left":"Right";
                var arm=new Arm {left=i==0,chain=new Chain {upper=Bone(side+"Arm"),lower=Bone(side+"ForeArm"),end=Bone(side+"Hand")},middle=Bone(side+"HandMiddle1"),index=Bone(side+"HandIndex1"),pinky=Bone(side+"HandPinky1"),fingers=new Transform[15]};
                for(int digit=0;digit<5;digit++) for(int segment=0;segment<3;segment++) arm.fingers[digit*3+segment]=Bone(side+"Hand"+Digits[digit]+(segment+1));
                if(arm.chain.upper==null||arm.chain.lower==null||arm.chain.end==null||arm.middle==null||arm.index==null||arm.pinky==null) {rigReady=false;continue;}
                Vector3 palm=Vector3.Cross(arm.middle.position-arm.chain.end.position,arm.index.position-arm.pinky.position);
                // Calibrate each imported hand's winding from its actual neutral
                // pose: the mannequin rests palms inward, the legacy A-pose down.
                Vector3 inward=Vector3.ProjectOnPlane(actor.transform.position-arm.chain.end.position,actor.transform.up);
                var previous=arms[i];
                // Pooled passengers may still be gripping when re-initialized.
                // Keep the same rig's neutral-pose calibration across reuse.
                arm.palmSign=mannequin&&previous!=null&&previous.chain.end==arm.chain.end
                    ?previous.palmSign:Vector3.Dot(palm,mannequin?inward:-actor.transform.up)>=0?1:-1;
                arms[i]=arm;legs[i]=new Chain {upper=Bone(side+"UpLeg"),lower=Bone(side+"Leg"),end=Bone(side+"Foot")};
                if(legs[i].upper==null||legs[i].lower==null||legs[i].end==null||Bone(side+"ToeBase")==null) rigReady=false;
            }
            if(!rigReady) Debug.LogError("Passenger contact rig is incomplete: "+name,this);
        }
        Transform Bone(string name)
        {
            if(bones.TryGetValue(name,out var original)) return original;
            if(!mannequin) return null;
            string mapped=name=="Hips"?"DEF-hips":name=="Spine"?"DEF-spine.001":name=="Spine2"?"DEF-spine.003":null;
            if(mapped==null&&(name.StartsWith("Left")||name.StartsWith("Right"))) {
                bool left=name.StartsWith("Left");string part=name.Substring(left?4:5),side=left?".L":".R";
                switch(part) {
                    case "Arm":mapped="DEF-upper_arm"+side;break;
                    case "ForeArm":mapped="DEF-forearm"+side;break;
                    case "Hand":mapped="DEF-hand"+side;break;
                    case "UpLeg":mapped="DEF-thigh"+side;break;
                    case "Leg":mapped="DEF-shin"+side;break;
                    case "Foot":mapped="DEF-foot"+side;break;
                    case "ToeBase":mapped="DEF-toe"+side;break;
                    default:
                        for(int digit=0;digit<Digits.Length;digit++) if(part.StartsWith("Hand"+Digits[digit]))
                            mapped="DEF-"+(digit==0?"thumb":"f_"+Digits[digit].ToLowerInvariant())+".0"+part[part.Length-1]+side;
                        break;
                }
            }
            if(mapped==null||!bones.TryGetValue(mapped,out var bone)) return null;
            bones[name]=bone;return bone;
        }
        void LateUpdate()
        {
            Holding=Leaning=false;GripError=LeanError=-1;
            if(!rigReady||actor==null||skin==null||(!skin.isVisible&&!ReviewWhileCulled)||actor.world.session.SimulationPaused) return;
            var hips=Bone("Hips");if(hips==null) return;
            PoseFrame=Time.frameCount;SeatFootError=-1;
            float sitting=actor.SittingWeight;
            if(sitting>0&&actor.Seat!=null) {
                if(!wasSeated) {leftPlant=actor.transform.parent.InverseTransformPoint(legs[0].end.position);rightPlant=actor.transform.parent.InverseTransformPoint(legs[1].end.position);wasSeated=true;}
                var seat=actor.Seat.anchor;
                if((seat.position-actor.transform.position).sqrMagnitude>1.1f*1.1f) return;
                // Fit the pelvis to the existing cushion using this rig's authored
                // seated height, then solve its legs without moving the AI root.
                Vector3 pelvis=hips.position;
                if(mannequin) pelvis=Vector3.Lerp(pelvis,seat.position+Vector3.up*.527f,sitting);
                else pelvis.y=Mathf.Lerp(pelvis.y,seat.position.y+.585f,sitting);
                hips.position=pelvis;
                for(int i=0;i<2;i++) {bool left=i==0;
                    float sign=left?-1:1;
                    Vector3 foot=seat.position+seat.forward*.40f+seat.right*sign*.13f+Vector3.up*(mannequin?.1005f:.095f);
                    Vector3 plant=actor.transform.parent.TransformPoint(left?leftPlant:rightPlant);
                    Limb(legs[i],Vector3.Lerp(plant,foot,sitting),seat.forward,1);
                    if(sitting>.99f) SeatFootError=Mathf.Max(SeatFootError,(legs[i].end.position-foot).magnitude);
                    var ankle=legs[i].end;var toe=Bone(left?"LeftToeBase":"RightToeBase");
                    AlignFoot(ankle,toe,seat.forward,sitting);
                    Vector3 palm=seat.position+seat.forward*.29f+seat.right*sign*.16f+Vector3.up*.62f;
                    Hand(arms[i],palm,seat.forward,Vector3.down,sitting,false);
                }
                supportWeight=0;return;
            }
            wasSeated=false;
            var spot=actor.world.Traffic.SupportFor(actor);
            bool valid=actor.inTrain&&actor.IsResting&&spot!=null&&spot.train==actor.Riding;
            supportWeight=Mathf.MoveTowards(supportWeight,valid?1:0,Time.deltaTime*2.8f);
            if(!valid||supportWeight<=0) return;
            var root=spot.train.transform;
            if(spot.lean) {
                Vector3 normal=root.TransformDirection(spot.normal).normalized;
                // Re-sample the actual support normal at shoulder height. Do not
                // lean against a trigger, through an opening, or a removed wall.
                if(spot.surface==null||!spot.surface.enabled||!spot.surface.Raycast(new Ray(hips.position+Vector3.up*.43f,-normal),out var hit,.65f)) return;
                normal=hit.normal;
                var lower=Bone("Spine");lower.rotation=Quaternion.AngleAxis(-8*supportWeight,actor.transform.right)*lower.rotation;
                Vector3 back=Bone("Spine2").position+Vector3.up*.12f;
                float gap=Vector3.Dot(back-hit.point,normal)-.13f;
                hips.position-=normal*Mathf.Clamp(gap,-.06f,.24f)*supportWeight;
                GroundSupportFeet(supportWeight);
                // Natural bent elbows and hands resting together at the waist.
                Hand(arms[0],hips.position+actor.transform.forward*.18f-actor.transform.right*.06f+Vector3.up*.10f,actor.transform.right,actor.transform.forward,supportWeight,false);
                Hand(arms[1],hips.position+actor.transform.forward*.21f+actor.transform.right*.05f+Vector3.up*.10f,-actor.transform.right,actor.transform.forward,supportWeight,false);
                Leaning=true;ContactPoint=hit.point;LeanError=Mathf.Abs(Vector3.Dot(Bone("Spine2").position+Vector3.up*.12f-hit.point,normal)-.13f);return;
            }
            Vector3 grip=root.TransformPoint(spot.grip);
            var holding=(arms[0].chain.upper.position-grip).sqrMagnitude<(arms[1].chain.upper.position-grip).sqrMagnitude?arms[0]:arms[1];
            // Crossbar is a real .024m tube. Aim the palm centre at it and solve
            // the shoulder/elbow triangle without exceeding anatomical reach.
            if((holding.chain.upper.position-grip).magnitude>.68f) return;
            Hand(holding,grip,Vector3.up,actor.transform.forward,supportWeight,true);
            GroundSupportFeet(supportWeight);
            Holding=true;ContactPoint=grip;
            GripError=(PalmCentre(holding)-grip).magnitude;
        }
        Vector3 PalmCentre(Arm arm)=>Vector3.Lerp(arm.chain.end.position,arm.middle.position,.72f);
        void Hand(Arm arm,Vector3 palm,Vector3 fingers,Vector3 normal,float weight,bool curl)
        {
            var hand=arm.chain.end;var middle=arm.middle;var index=arm.index;var pinky=arm.pinky;
            Vector3 direction=(middle.position-hand.position).normalized;
            Quaternion swing=Quaternion.FromToRotation(direction,fingers.normalized);
            Vector3 across=swing*(index.position-pinky.position);
            Vector3 currentNormal=Vector3.Cross(fingers,across).normalized;
            currentNormal*=arm.palmSign;
            Quaternion turn=Quaternion.AngleAxis(Vector3.SignedAngle(currentNormal,normal,fingers),fingers.normalized);
            Quaternion orientation=turn*swing*hand.rotation;
            Vector3 offset=Quaternion.Inverse(hand.rotation)*(PalmCentre(arm)-hand.position);
            Vector3 wrist=palm-orientation*offset;
            Vector3 elbowHint=actor.transform.forward*.6f+actor.transform.right*(arm.left?-.35f:.35f);
            Limb(arm.chain,wrist,elbowHint,weight);
            hand.rotation=Quaternion.Slerp(hand.rotation,orientation,weight);
            for(int digit=0;digit<5;digit++) for(int segment=1;segment<=3;segment++) {
                var finger=arm.fingers[digit*3+segment-1];if(finger==null) continue;
                // Mild curl at rest; MCP/PIP/DIP flexion closes around the tube.
                // The mannequin's authored clips already curl its fingers. Keep that
                // resting pose and add only a small closure instead of doubling it.
                float angle=mannequin?(curl?(digit==0?5:segment==2?15:10):0):curl?(digit==0?18:segment==1?55:segment==2?70:40):segment==2?12:6;
                Vector3 along=segment<3?arm.fingers[digit*3+segment].position-finger.position:finger.position-arm.fingers[digit*3+segment-2].position;
                Vector3 axis=Vector3.Cross(along,normal).normalized;
                finger.rotation=Quaternion.AngleAxis(angle*weight,axis)*finger.rotation;
            }
        }
        void GroundSupportFeet(float weight)
        {
            for(int i=0;i<2;i++) {
                Vector3 point=actor.transform.position+actor.transform.right*(i==0?-.13f:.13f)+actor.transform.forward*.025f+Vector3.up*(mannequin?.1005f:.095f);
                Limb(legs[i],point,actor.transform.forward,weight);
                var foot=legs[i].end;var toe=Bone(i==0?"LeftToeBase":"RightToeBase");
                AlignFoot(foot,toe,actor.transform.forward,weight);
            }
        }
        void AlignFoot(Transform foot,Transform toe,Vector3 forward,float weight)
        {
            Vector3 direction=toe.position-foot.position;
            // Preserve the mannequin's authored ankle pitch/sole height; yaw only.
            if(mannequin) direction=Vector3.ProjectOnPlane(direction,Vector3.up);
            if(direction.sqrMagnitude<.0001f) return;
            foot.rotation=Quaternion.Slerp(foot.rotation,Quaternion.FromToRotation(direction,forward)*foot.rotation,weight);
        }
        void Limb(Chain chain,Vector3 target,Vector3 hint,float weight)
        {
            var upper=chain.upper;var lower=chain.lower;var end=chain.end;
            if(upper==null||lower==null||end==null) return;
            Vector3 root=upper.position;float a=(lower.position-root).magnitude,b=(end.position-lower.position).magnitude;
            Vector3 delta=target-root;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.005f,a+b-.005f);Vector3 direction=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(hint,direction).normalized;if(bend.sqrMagnitude<.1f) bend=Vector3.ProjectOnPlane(actor.transform.right,direction).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 elbow=root+direction*along+bend*height;
            var q=Quaternion.FromToRotation(lower.position-root,elbow-root)*upper.rotation;
            upper.rotation=Quaternion.Slerp(upper.rotation,q,weight);
            target=root+direction*distance;
            q=Quaternion.FromToRotation(end.position-lower.position,target-lower.position)*lower.rotation;
            lower.rotation=Quaternion.Slerp(lower.rotation,q,weight);
        }
    }
}
