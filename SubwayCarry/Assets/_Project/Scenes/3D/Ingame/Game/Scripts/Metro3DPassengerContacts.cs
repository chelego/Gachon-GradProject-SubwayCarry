using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Contact correction after the preserved generic Animator. No Humanoid
    // avatar/retargeting contract or team-wide quality setting is replaced.
    [DefaultExecutionOrder(100)]
    public sealed partial class Metro3DPassengerContacts : MonoBehaviour
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
        float supportWeight, standingWeight;
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
        struct PoseInput {public Transform bone;public Quaternion rotation;public Vector3 position;}
        readonly PoseInput[] contactInputs=new PoseInput[48];
        int contactInputCount;

        void Remember(Transform bone)
        {
            for(int i=0;i<contactInputCount;i++)if(contactInputs[i].bone==bone)return;
            contactInputs[contactInputCount++]=new PoseInput {bone=bone,rotation=bone.localRotation,position=bone.localPosition};
        }

        void RestoreContactInputs()
        {
            for(int i=0;i<contactInputCount;i++) {
                var input=contactInputs[i];
                if(input.bone!=null) {input.bone.localRotation=input.rotation;input.bone.localPosition=input.position;}
            }
            contactInputCount=0;
        }

        // Contact is additive to this frame's Animator pose. Restore before
        // the next sampling pass so unkeyed or culled bones cannot accumulate it.
        void Update() {RestoreContactInputs();TickPhone();}
        void OnDisable() {RestoreContactInputs();ClearPhone();}
        public void Initialize(Metro3DPassenger passenger)
        {
            actor=passenger;bones.Clear();supportWeight=standingWeight=0;wasSeated=false;ResetPhone();
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
            if(!rigReady||actor==null||skin==null||(!skin.isVisible&&!ReviewWhileCulled)) {ShowPhone(false);return;}
            if(actor.world.session.SimulationPaused) {ApplyPhonePose();return;}
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
                Remember(hips);hips.position=pelvis;
                for(int i=0;i<2;i++) {bool left=i==0;
                    float sign=left?-1:1;
                    Vector3 foot=seat.position+seat.forward*.40f+seat.right*sign*.13f+Vector3.up*(mannequin?.1005f:.095f);
                    Vector3 plant=actor.transform.parent.TransformPoint(left?leftPlant:rightPlant);
                    Limb(legs[i],Vector3.Lerp(plant,foot,sitting),seat.forward,1);
                    if(sitting>.99f) SeatFootError=Mathf.Max(SeatFootError,(legs[i].end.position-foot).magnitude);
                    var ankle=legs[i].end;var toe=Bone(left?"LeftToeBase":"RightToeBase");
                    AlignFoot(ankle,toe,seat.forward,sitting);
                    // Place relaxed hands on each actual thigh. The elbows
                    // hang below the shoulders and the wrist follows the forearm.
                    var thigh=legs[i];
                    Vector3 palm=Vector3.Lerp(thigh.upper.position,thigh.lower.position,.67f)
                        +seat.right*sign*.022f+Vector3.up*.075f;
                    Hand(arms[i],palm,seat.forward,Vector3.down,sitting,false,relaxedSeated:true);
                }
                actor.ApplySeatedPastimePose();
                supportWeight=standingWeight=0;return;
            }
            wasSeated=false;
            if(actor.NarrowPoseWeight>.001f)ApplyNarrowPassage(hips);
            var spot=actor.world.Traffic.SupportFor(actor);
            bool valid=actor.IsResting&&spot!=null&&spot.frame==actor.transform.parent&&
                (spot.frame.TransformPoint(spot.feet)-actor.transform.position).sqrMagnitude<.34f*.34f;
            supportWeight=Mathf.MoveTowards(supportWeight,valid?1:0,Time.deltaTime*2.8f);
            if(!valid||supportWeight<=0) {
                bool settled=actor.SettledStanding;
                standingWeight=Mathf.MoveTowards(standingWeight,settled?1:0,Time.deltaTime*(settled?4.2f:9f));
                if(standingWeight>.001f)GroundSupportFeet(standingWeight,6f);
                ApplyPhonePose();return;
            }
            standingWeight=0;
            var root=spot.frame;
            if(spot.lean) {
                Vector3 normal=root.TransformDirection(spot.normal).normalized;
                bool sideLean=spot.sideLean;
                Vector3 facing=root.TransformDirection(spot.LeanFacing);
                if(Vector3.Dot(actor.transform.forward,facing)<.94f) {supportWeight=0;ApplyPhonePose();return;}
                // Both waist and shoulder hit this fixed wall when the spot was
                // built. It moves with the frame, so reuse its sampled plane.
                if(spot.surface==null||!spot.surface.enabled||
                    (root.TransformPoint(spot.feet)-actor.transform.position).sqrMagnitude>.18f*.18f) {ApplyPhonePose();return;}
                Vector3 wall=root.TransformPoint(spot.grip);
                var lower=Bone("Spine");
                float sway=Mathf.Sin(Time.time*.78f+actor.index*1.17f)*2.2f;
                Vector3 axis=sideLean?Vector3.Cross(Vector3.up,-normal):actor.transform.right;
                float tilt=sideLean?10+sway*.3f:-8;
                Remember(lower);Remember(hips);
                lower.rotation=Quaternion.AngleAxis(tilt*supportWeight,axis)*lower.rotation;
                var upper=Bone("Spine2");
                Vector3 back=upper.position+Vector3.up*.12f;
                float clearance=sideLean?.18f:.13f;
                float gap=Vector3.Dot(back-wall,normal)-clearance;
                hips.position-=normal*Mathf.Clamp(gap,-.06f,.34f)*supportWeight;
                GroundSupportFeet(supportWeight);
                // Keep the idle animation's relaxed arm chain.
                Leaning=true;ContactPoint=wall;
                LeanError=Mathf.Abs(Vector3.Dot(upper.position+Vector3.up*.12f-wall,normal)-clearance);
                ApplyPhonePose();return;
            }
            Vector3 grip=root.TransformPoint(spot.grip);
            var holding=(arms[0].chain.upper.position-grip).sqrMagnitude<(arms[1].chain.upper.position-grip).sqrMagnitude?arms[0]:arms[1];
            // Crossbar is a real .024m tube. Aim the palm centre at it and solve
            // the shoulder/elbow triangle without exceeding anatomical reach.
            var arm=holding.chain;
            float reach=Vector3.Distance(arm.upper.position,arm.lower.position)+Vector3.Distance(arm.lower.position,arm.end.position);
            float palmReach=Vector3.Distance(arm.end.position,PalmCentre(holding));
            if((arm.upper.position-grip).magnitude>reach+palmReach-.025f) return;
            Hand(holding,grip,Vector3.up,actor.transform.forward,supportWeight,true);
            GroundSupportFeet(supportWeight);
            Holding=true;ContactPoint=grip;
            GripError=(PalmCentre(holding)-grip).magnitude;
        }
        Vector3 PalmCentre(Arm arm)=>Vector3.Lerp(arm.chain.end.position,arm.middle.position,.72f);
        void ApplyNarrowPassage(Transform hips)
        {
            float weight=Mathf.SmoothStep(0,1,actor.NarrowPoseWeight);
            float side=actor.NarrowPoseSide;
            var spine=Bone("Spine2");
            if(spine==null)return;
            Vector3 forward=actor.transform.forward,right=actor.transform.right;
            Remember(spine);
            spine.rotation=Quaternion.AngleAxis(side*32*weight,actor.transform.up)*spine.rotation;
            for(int i=0;i<2;i++) {
                var arm=arms[i];
                float sign=arm.left?-1:1;
                var chain=arm.chain;
                Vector3 close=hips.position+right*sign*.155f+forward*(sign==side?.10f:-.065f)-actor.transform.up*.15f;
                Limb(chain,close,Vector3.down+right*sign*.28f-forward*.16f,weight*.7f);
            }
        }
        Quaternion PalmOrientation(Arm arm,Vector3 fingers,Vector3 normal)
        {
            var hand=arm.chain.end;var middle=arm.middle;var index=arm.index;var pinky=arm.pinky;
            Vector3 direction=(middle.position-hand.position).normalized;
            Quaternion swing=Quaternion.FromToRotation(direction,fingers.normalized);
            Vector3 across=swing*(index.position-pinky.position);
            Vector3 currentNormal=Vector3.Cross(fingers,across).normalized;
            currentNormal*=arm.palmSign;
            Vector3 palmNormal=Vector3.ProjectOnPlane(normal,fingers);
            Quaternion turn=Quaternion.AngleAxis(Vector3.SignedAngle(currentNormal,palmNormal,fingers),fingers.normalized);
            return turn*swing*hand.rotation;
        }
        void Hand(Arm arm,Vector3 palm,Vector3 fingers,Vector3 normal,float weight,bool curl,bool relaxedSeated=false)
        {
            var hand=arm.chain.end;
            Quaternion orientation=PalmOrientation(arm,fingers,normal);
            Vector3 offset=Quaternion.Inverse(hand.rotation)*(PalmCentre(arm)-hand.position);
            Vector3 wrist=palm-orientation*offset;
            Vector3 outward=actor.transform.right*(arm.left?-1:1);
            Vector3 elbowHint=relaxedSeated?Vector3.down+outward*.30f-actor.transform.forward*.18f:
                curl?Vector3.down+actor.transform.forward*.40f+outward*.26f:
                actor.transform.forward*.6f+outward*.35f;
            Limb(arm.chain,wrist,elbowHint,weight);
            if(relaxedSeated) {
                Vector3 forearm=(hand.position-arm.chain.lower.position).normalized;
                Vector3 relaxed=Vector3.RotateTowards(forearm,fingers,18*Mathf.Deg2Rad,0);
                orientation=PalmOrientation(arm,relaxed,normal);
            }
            Remember(hand);hand.rotation=Quaternion.Slerp(hand.rotation,orientation,weight);
            for(int digit=0;digit<5;digit++) for(int segment=1;segment<=3;segment++) {
                var finger=arm.fingers[digit*3+segment-1];if(finger==null) continue;
                // Mild curl at rest; MCP/PIP/DIP flexion closes around the tube.
                // The mannequin's authored clips already curl its fingers. Keep that
                // resting pose and add only a small closure instead of doubling it.
                float angle=mannequin?(curl?(digit==0?5:segment==2?15:10):0):curl?(digit==0?18:segment==1?55:segment==2?70:40):segment==2?12:6;
                Vector3 along=segment<3?arm.fingers[digit*3+segment].position-finger.position:finger.position-arm.fingers[digit*3+segment-2].position;
                Vector3 axis=Vector3.Cross(along,normal).normalized;
                if(angle!=0) {Remember(finger);finger.rotation=Quaternion.AngleAxis(angle*weight,axis)*finger.rotation;}
            }
        }
        void GroundSupportFeet(float weight,float toeOut=0)
        {
            for(int i=0;i<2;i++) {
                Vector3 point=actor.transform.position+actor.transform.right*(i==0?-.13f:.13f)+actor.transform.forward*.025f+Vector3.up*(mannequin?.1005f:.095f);
                Limb(legs[i],point,actor.transform.forward,weight);
                var foot=legs[i].end;var toe=Bone(i==0?"LeftToeBase":"RightToeBase");
                Vector3 forward=Quaternion.AngleAxis(i==0?-toeOut:toeOut,Vector3.up)*actor.transform.forward;
                AlignFoot(foot,toe,forward,weight);
            }
        }
        void AlignFoot(Transform foot,Transform toe,Vector3 forward,float weight)
        {
            Vector3 direction=toe.position-foot.position;
            // Preserve the mannequin's authored ankle pitch/sole height; yaw only.
            if(mannequin) direction=Vector3.ProjectOnPlane(direction,Vector3.up);
            if(direction.sqrMagnitude<.0001f) return;
            Remember(foot);foot.rotation=Quaternion.Slerp(foot.rotation,Quaternion.FromToRotation(direction,forward)*foot.rotation,weight);
        }
        void Limb(Chain chain,Vector3 target,Vector3 hint,float weight)
        {
            var upper=chain.upper;var lower=chain.lower;var end=chain.end;
            if(upper==null||lower==null||end==null) return;
            Remember(upper);Remember(lower);
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
