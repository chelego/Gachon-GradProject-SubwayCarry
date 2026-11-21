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
        float supportWeight, standingWeight, doorBrace, doorRecoil, doorGazeYaw;
        Vector3 doorBraceDirection, doorRecoilDirection;
        float doorChestYaw, doorStanceYaw, watchingStillFor, rearGlanceSide;
        Vector3 flankLean;
        Transform gazeHead,leanFrame;
        Vector3 leanTwist,leanShift;
        float leanRelease;
        Vector3 gazeForward;
        bool posePlanted, platformMoving, platformCrossing, platformWatching;
        bool plantRequested;
        float plantRequestSince,doorStepUntil;
        Metro3DWaitGestures platformMotions;
        Transform[] platformBones;
        int activePlatformCue=-1, previousPlatformCue=-1;
        float platformAge,previousPlatformAge,platformBlend,platformStillTime;
        internal float RelaxedHandsWeight {get;private set;}
        Arm gripArm;
        Metro3DStationTraffic.SupportSpot activeGripSpot;
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
        readonly float[] soleSlope=new float[2];
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
        void Update() {RestoreContactInputs();TickLifeAction();TickPhone();}
        void OnDisable() {RestoreContactInputs();ResetLifeAction();ClearPhone();gripArm=null;activeGripSpot=null;actor?.RestoreIdleWrists();if(gateProp!=null)gateProp.SetActive(false);}
        public void Initialize(Metro3DPassenger passenger)
        {
            RelaxedHandsWeight=doorBrace=doorRecoil=doorGazeYaw=0;
            leanFrame=null;leanRelease=0;
            doorBraceDirection=doorRecoilDirection=flankLean=Vector3.zero;doorChestYaw=doorStanceYaw=watchingStillFor=rearGlanceSide=0;
            posePlanted=plantRequested=platformMoving=platformCrossing=platformWatching=false;
            plantRequestSince=doorStepUntil=0;activePlatformCue=previousPlatformCue=-1;platformBones=null;
            platformAge=previousPlatformAge=platformBlend=platformStillTime=0;
            gateMotionGate=null;gateHand=-1;gateMotion=oldGateMotion=-1;
            gateMotionAge=oldGateMotionAge=gateMotionBlend=0;
            cardDraw=cardReach=travelLean=travelChestYaw=travelPitch=0;
            cardFacing=previousTravelHeading=cardPalm=Vector3.zero;
            hasCardPalm=false;
            previousGatePhase=Metro3DPassengerJourney.GatePhase.None;
            actor=passenger;ResetLifeAction();ResetWaitGesture();ResetTurnFeet();bones.Clear();supportWeight=standingWeight=0;gripArm=null;activeGripSpot=null;wasSeated=false;ResetPhone();
            skin=GetComponentInChildren<SkinnedMeshRenderer>(true);
            foreach(var t in GetComponentsInChildren<Transform>(true)) if(!bones.ContainsKey(t.name)) bones.Add(t.name,t);
            mannequin=bones.ContainsKey("DEF-hips");
            var head=bones.TryGetValue("DEF-head",out var genericHead)?genericHead:Bone("Head");
            if(head!=null&&head!=gazeHead)gazeForward=head.InverseTransformDirection(actor.transform.forward);
            gazeHead=head;
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
                var toe=Bone(side+"ToeBase");
                if(legs[i].upper==null||legs[i].lower==null||legs[i].end==null||toe==null) rigReady=false;
                else if(previous==null||previous.chain.end!=arm.chain.end) {
                    Vector3 neutral=toe.position-legs[i].end.position;
                    soleSlope[i]=Mathf.Clamp(neutral.y/Mathf.Max(.01f,Vector3.ProjectOnPlane(neutral,actor.transform.up).magnitude),-.9f,-.06f);
                }
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
            ApplyContacts();
            if (actor == null) return;

            // Blend with the pose that moves each hand onto its contact.
            // A change of AI state or Animator clip does not release a wrist on its own.
            float freeWeight = actor.world.RelaxIdlePassengerWrists && actor.CanCorrectVisualWrists &&
                !Leaning && !LifeActive && actor.NarrowPoseWeight < .05f
                ? 1 - actor.SeatContactWeight : 0;
            float left = freeWeight;
            float right = freeWeight * PhoneRightWristFreedom;
            if(gripArm!=null) {
                if(gripArm.left)left*=1-supportWeight;
                else right*=1-supportWeight;
            }
            if(gateHand>=0&&cardDraw>.001f) {
                float freedom=1-Mathf.SmoothStep(0,1,cardDraw);
                if(arms[gateHand].left)left*=freedom;
                else right*=freedom;
            }
            if(PoseFrame==Time.frameCount&&rigReady) {
                float motion=Mathf.InverseLerp(.05f,1.1f,actor.Velocity.magnitude);
                float angle=Mathf.Lerp(7,10,motion);
                float freedom=1-Mathf.Clamp01(doorBrace+doorRecoil);
                LowerFreeUpperArm(arms[0],angle,left*freedom);
                LowerFreeUpperArm(arms[1],angle,right*freedom);
            }
            actor.RetargetPassengerVisual(posePlanted?0:left,posePlanted?0:right);
        }

        void LowerFreeUpperArm(Arm arm,float limit,float weight)
        {
            if(weight<=0)return;
            var frame=actor.transform;
            var upper=arm.chain.upper;
            Vector3 direction=arm.chain.lower.position-upper.position;
            float down=-Vector3.Dot(direction.normalized,frame.up);
            float hang=Mathf.InverseLerp(.50f,.82f,down);
            float side=arm.left?-1:1;
            float angle=Mathf.Atan2(side*Vector3.Dot(direction,frame.right),-Vector3.Dot(direction,frame.up))*Mathf.Rad2Deg;
            if(hang<=0||angle<=limit)return;
            Remember(upper);
            // Rotate in the frontal plane so the authored forward/backward swing survives.
            upper.rotation=Quaternion.AngleAxis(-side*(angle-limit)*weight*hang,frame.forward)*upper.rotation;
        }

        void ApplyContacts()
        {
            Holding=Leaning=false;GripError=LeanError=-1;
            var visibleSkin=actor?.ExternalPassengerSkin??skin;
            if(!rigReady||actor==null||visibleSkin==null||(!visibleSkin.isVisible&&!ReviewWhileCulled)) {ShowPhone(false);if(gateProp!=null)gateProp.SetActive(false);return;}
            var hips=Bone("Hips");if(hips==null) return;
            if(actor.world.session.SimulationPaused) {
                if(RelaxedHandsWeight>.001f)RelaxStandingArms(hips,RelaxedHandsWeight);
                ApplyPhonePose();return;
            }
            RelaxedHandsWeight=0;
            PoseFrame=Time.frameCount;SeatFootError=-1;
            ApplyGateMotion();
            if(ApplyGateCard(hips))return;
            if(gateProp!=null&&gateProp.activeSelf)gateProp.SetActive(false);
            float sitting=actor.SeatContactWeight;
            actor.ApplyRiderPosture(sitting,Remember);
            if(actor.Seat!=null&&(sitting>0||actor.RisingFromSeat||actor.LoweringToSeat)) {
                ResetTurnFeet();leanFrame=null;leanRelease=0;
                if(!wasSeated) {leftPlant=actor.transform.parent.InverseTransformPoint(legs[0].end.position);rightPlant=actor.transform.parent.InverseTransformPoint(legs[1].end.position);wasSeated=true;}
                var seat=actor.Seat.anchor;
                if((seat.position-actor.transform.position).sqrMagnitude>1.1f*1.1f) return;
                // Fit the pelvis to the existing cushion using this rig's authored
                // seated height, then solve its legs without moving the AI root.
                float phase=actor.SeatTransitionPhase;
                float forwardLoad=actor.RisingFromSeat
                    ?Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.29f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.80f,phase)))
                    :actor.LoweringToSeat?Mathf.Sin(Mathf.PI*phase)*.48f:0;
                float hipHeight=mannequin?Mathf.Lerp(.501f,.527f,actor.HeightRatio):.585f;
                Vector3 seatedHip=seat.position+Vector3.up*hipHeight+
                    seat.forward*(-.036f+forwardLoad*.15f);
                Remember(hips);hips.position=Vector3.Lerp(hips.position,seatedHip,sitting);
                var spine=Bone("Spine");
                if(spine!=null&&forwardLoad>.001f){
                    Remember(spine);
                    spine.rotation=Quaternion.AngleAxis(16*forwardLoad,seat.right)*spine.rotation;
                }
                for(int i=0;i<2;i++) {bool left=i==0;
                    float sign=left?-1:1;
                    float height=actor.HeightRatio;
                    float offset=(actor.index%3-1)*.022f*sign;
                    float ankleHeight=(mannequin?.1005f:.095f)*height;
                    Vector3 foot=seat.position+seat.forward*((.39f+offset)*height)+
                        seat.right*(sign*.116f*height)+Vector3.up*ankleHeight;
                    Vector3 plant=actor.transform.parent.TransformPoint(left?leftPlant:rightPlant);
                    float legWeight=1;
                    if(actor.RisingFromSeat){
                        float step=Mathf.InverseLerp(left?.74f:.86f,left?.91f:1,phase);
                        Vector3 landing=actor.StandLanding+seat.right*(sign*.116f*height)+Vector3.up*ankleHeight;
                        foot=Vector3.Lerp(foot,landing,Mathf.SmoothStep(0,1,step))+Vector3.up*(Mathf.Sin(Mathf.PI*step)*.038f);
                        legWeight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,1,phase));
                    }else if(actor.LoweringToSeat){
                        float step=Mathf.InverseLerp(left?.13f:.28f,left?.37f:.52f,phase);
                        foot=Vector3.Lerp(plant,foot,Mathf.SmoothStep(0,1,step))+Vector3.up*(Mathf.Sin(Mathf.PI*step)*.035f);
                    }
                    Limb(legs[i],foot,seat.forward,legWeight);
                    if(sitting>.99f) SeatFootError=Mathf.Max(SeatFootError,(legs[i].end.position-foot).magnitude);
                    var ankle=legs[i].end;var toe=Bone(left?"LeftToeBase":"RightToeBase");
                    AlignPlantedFoot(ankle,toe,seat.forward,soleSlope[i],legWeight);
                    // Place relaxed hands on each actual thigh. The elbows
                    // hang below the shoulders and the wrist follows the forearm.
                    var thigh=legs[i];
                    Vector3 palm=Vector3.Lerp(thigh.upper.position,thigh.lower.position,.67f)
                        +seat.right*sign*.022f+Vector3.up*.075f;
                    Hand(arms[i],palm,seat.forward,Vector3.down,sitting,false,relaxedSeated:true);
                }
                actor.ApplySeatedPastimePose();
                float portrait=actor.SeatedPortraitWeight;
                if(portrait>.001f)ApplySeatedPortrait(seat,portrait);
                actor.ApplySeatedPastimeProp();
                supportWeight=standingWeight=0;gripArm=null;activeGripSpot=null;return;
            }
            wasSeated=false;
            if(actor.NarrowPoseWeight>.001f)ApplyNarrowPassage(hips);
            var spot=actor.world.Traffic.SupportFor(actor);
            bool valid=actor.IsResting&&spot!=null&&spot.frame==actor.transform.parent&&
                (spot.frame.TransformPoint(spot.feet)-actor.transform.position).sqrMagnitude<.34f*.34f;
            if(valid) {
                valid=Vector3.Dot(actor.transform.forward,spot.frame.TransformDirection(spot.Facing))>=.94f;
                if(spot.lean)valid=valid&&spot.surface!=null&&spot.surface.enabled&&
                    (spot.frame.TransformPoint(spot.feet)-actor.transform.position).sqrMagnitude<.18f*.18f;
            }
            if(activeGripSpot!=null&&(!valid||activeGripSpot!=spot)) {
                activeGripSpot=null;gripArm=null;ResetTurnFeet();
            }
            supportWeight=Mathf.MoveTowards(supportWeight,valid?1:0,Time.deltaTime*2.8f);
            if (supportWeight == 0) gripArm = null;
            if(!valid||supportWeight<=0) {
                ReleaseLeanPose(hips);
                bool settled=actor.SettledStanding;
                UpdateDoorContactPose();
                // A displaced stance takes a short recovery step using the same
                // frame-relative planted feet as a standing turn.
                bool displaced=doorRecoil>.12f&&actor.DesiredVelocity.sqrMagnitude<.18f*.18f&&
                    actor.Velocity.sqrMagnitude<.41f*.41f;
                bool watchingStill=UpdateDoorStance(dt:Time.deltaTime);
                float speedSquared=actor.Velocity.sqrMagnitude;
                bool takingDoorStep=actor.DoorCourtesyStep||actor.RetreatingDoor||displaced;
                if(takingDoorStep&&speedSquared<.83f*.83f)doorStepUntil=Time.time+.16f;
                // A one-tick contact rejection must not switch a planted
                // recovery step back onto the forward-walking pose.
                bool shortDoorStep=Time.time<doorStepUntil&&speedSquared<.92f*.92f;
                bool planted=settled||shortDoorStep||watchingStill||actor.SteppingTurn&&speedSquared<.023f;
                if(planted!=plantRequested) {plantRequested=planted;plantRequestSince=Time.time;}
                if(!shortDoorStep&&(actor.DoorCrossing||actor.Velocity.sqrMagnitude>.29f*.29f))posePlanted=false;
                else if(planted!=posePlanted&&Time.time-plantRequestSince>=(planted?(shortDoorStep?.045f:.09f):.13f))
                    posePlanted=planted;
                standingWeight=Mathf.MoveTowards(standingWeight,posePlanted?1:0,Time.deltaTime*(posePlanted?6f:7f));
                if(standingWeight>.001f)ApplyTurnFeet(hips,standingWeight,shortDoorStep?0:doorStanceYaw,shortDoorStep);
                else if(stanceFrame!=null)ResetTurnFeet();
                if(!ApplyLifeAction()) {
                    ApplyWaitGesture(settled&&!actor.QueueActive&&!actor.Journey.OnStairs&&!HasPhoneInUse);
                    if(!LifeActive&&actor.NarrowPoseWeight<=.001f&&standingWeight>.001f) {
                        if(settled)BreatheThroughChest(standingWeight);
                        RelaxedHandsWeight=standingWeight;
                        RelaxStandingArms(hips,standingWeight);
                    }
                    ApplyPhonePose();
                    ApplyPlatformMotion();
                    ApplyTravelTransition();
                    ApplyDoorContactPose(hips);
                }
                return;
            }
            standingWeight=0;
            var root=spot.frame;
            if(spot.lean) {
                ResetTurnFeet();
                gripArm = null;
                Vector3 normal=root.TransformDirection(spot.normal).normalized;
                bool sideLean=spot.sideLean;
                // Use the support plane in the moving station or carriage frame.
                Vector3 wall=root.TransformPoint(spot.grip);
                var lower=Bone("Spine");
                float sway=Mathf.Sin(Time.time*.78f+actor.index*1.17f)*2.2f;
                Vector3 axis=sideLean?Vector3.Cross(Vector3.up,-normal):actor.transform.right;
                float tilt=sideLean?10+sway*.3f:-8;
                Remember(lower);Remember(hips);
                lower.rotation=Quaternion.AngleAxis(tilt*supportWeight,axis)*lower.rotation;
                var upper=Bone("Spine2");
                Vector3 back=upper.position+Vector3.up*(.12f*actor.HeightRatio);
                float clearance=(sideLean?.18f:.13f)*(actor.BodyRadius/Metro3DPassenger.StandingRadius);
                float gap=Vector3.Dot(back-wall,normal)-clearance;
                Vector3 displacement=-normal*Mathf.Clamp(gap,-.06f,.34f)*supportWeight;
                hips.position+=displacement;
                leanTwist=root.InverseTransformDirection(axis*(tilt*supportWeight));
                leanShift=root.InverseTransformDirection(displacement);
                leanFrame=root;leanRelease=1;
                GroundSupportFeet(supportWeight,6f,true);
                // Keep the idle animation's relaxed arm chain.
                Leaning=true;ContactPoint=wall;
                LeanError=Mathf.Abs(Vector3.Dot(upper.position+Vector3.up*(.12f*actor.HeightRatio)-wall,normal)-clearance);
                if(!ApplyLifeAction())ApplyPhonePose();return;
            }
            leanFrame=null;leanRelease=0;
            Vector3 grip=root.TransformPoint(spot.grip);
            if(activeGripSpot!=spot||gripArm==null) {
                ResetTurnFeet();
                activeGripSpot=spot;
                gripArm=(arms[0].chain.upper.position-grip).sqrMagnitude<(arms[1].chain.upper.position-grip).sqrMagnitude?arms[0]:arms[1];
            }
            var holding=gripArm;
            float yield=actor.GripYieldWeight*supportWeight;
            ApplyTurnFeet(hips,supportWeight,0,true,gripping:true);
            if(yield>.001f) {
                var spine=Bone("Spine");
                if(spine!=null) {
                    Remember(spine);
                    spine.rotation=Quaternion.AngleAxis(7f*yield,actor.transform.right)*spine.rotation;
                }
            }
            // Crossbar is a real .024m tube. Aim the palm centre at it and solve
            // the shoulder/elbow triangle without exceeding anatomical reach.
            var arm=holding.chain;
            float reach=Vector3.Distance(arm.upper.position,arm.lower.position)+Vector3.Distance(arm.lower.position,arm.end.position);
            float palmReach=Vector3.Distance(arm.end.position,PalmCentre(holding));
            if((arm.upper.position-grip).magnitude>reach+palmReach-.025f) return;
            Hand(holding,grip,Vector3.up,root.TransformDirection(spot.Facing),supportWeight,true);
            Holding=true;ContactPoint=grip;
            GripError=(PalmCentre(holding)-grip).magnitude;
        }
        void ReleaseLeanPose(Transform hips)
        {
            if(leanRelease<=0)return;
            if(leanFrame==null||leanFrame!=actor.transform.parent||actor.DoorCrossing) {
                leanFrame=null;leanRelease=0;return;
            }
            // Release the support through the torso while the Motor and
            // Animator are already taking the first step. Leave the feet free.
            leanRelease=Mathf.MoveTowards(leanRelease,0,Time.deltaTime*4.3f);
            if(leanRelease<=0){leanFrame=null;return;}
            float weight=Mathf.SmoothStep(0,1,leanRelease);
            var spine=Bone("Spine");
            Remember(spine);Remember(hips);
            spine.rotation=Quaternion.AngleAxis(leanTwist.magnitude*weight,
                leanFrame.TransformDirection(leanTwist.normalized))*spine.rotation;
            hips.position+=leanFrame.TransformDirection(leanShift)*weight;
        }
        GameObject gateProp;
        MeshRenderer gatePropRenderer;
        static Mesh tagMesh;
        static Material fareCard,paperSlip;
        Metro3DFacility gateMotionGate;
        Metro3DPassengerJourney.GatePhase previousGatePhase;
        int gateHand=-1, gateMotion=-1, oldGateMotion=-1;
        float gateMotionAge, oldGateMotionAge, gateMotionBlend;
        float cardDraw, cardReach, travelLean, travelChestYaw, travelPitch;
        Vector3 cardFacing, previousTravelHeading, cardPalm;
        bool hasCardPalm;

        int GateHand(Vector3 reader)
            => (arms[0].chain.upper.position-reader).sqrMagnitude<
               (arms[1].chain.upper.position-reader).sqrMagnitude?0:1;

        void ApplyGateMotion()
        {
            var phase=actor.Journey.GateAction;
            var gate=actor.Journey.ReservedGate;
            if(gate!=gateMotionGate) {
                gateMotionGate=gate;
                if(gate!=null||cardDraw<=.001f) {gateHand=-1;cardFacing=Vector3.zero;}
                previousGatePhase=Metro3DPassengerJourney.GatePhase.None;
            }
            if(phase!=previousGatePhase) {
                int next=-1;
                if(gate!=null&&gate.anchor!=null&&!HasPhoneInUse) {
                    if(gateHand<0)gateHand=GateHand(gate.anchor.position);
                    if(phase==Metro3DPassengerJourney.GatePhase.Align)next=gateHand==0?1:0;
                    if(phase==Metro3DPassengerJourney.GatePhase.Tag)next=2;
                    if(previousGatePhase==Metro3DPassengerJourney.GatePhase.Tag&&
                       (phase==Metro3DPassengerJourney.GatePhase.DoorWait||phase==Metro3DPassengerJourney.GatePhase.Cross))
                        next=gateHand==0?4:3;
                }
                if(next>=0) {
                    oldGateMotion=gateMotion;oldGateMotionAge=gateMotionAge;
                    gateMotion=next;gateMotionAge=gateMotionBlend=0;
                }
                previousGatePhase=phase;
            }
            if(gateMotion<0&&oldGateMotion<0)return;
            platformMotions=platformMotions!=null?platformMotions:Metro3DWaitGestures.Load();
            var poses=platformMotions?.Gate;
            if(poses==null||poses.Length<5||HasPhoneInUse)return;
            BindPlatformBones();
            float dt=Time.deltaTime;
            gateMotionBlend=Mathf.MoveTowards(gateMotionBlend,1,dt*9f);
            gateMotionAge+=dt;
            var clip=gateMotion<0?null:poses[gateMotion];
            var previous=oldGateMotion<0?null:poses[oldGateMotion];
            bool holds=gateMotion<2&&phase==Metro3DPassengerJourney.GatePhase.Align;
            if(clip==null||!holds&&gateMotionAge>=clip.Length)gateMotion=-1;
            if(gateMotionBlend>=1)oldGateMotion=-1;
            for(int i=0;i<platformBones.Length;i++) {
                var bone=platformBones[i];
                if(bone==null||i>=5&&cardDraw>.05f)continue;
                Quaternion from=oldGateMotion<0||previous==null?Quaternion.identity:previous.Delta(i,Mathf.Min(oldGateMotionAge,previous.Length));
                Quaternion to=gateMotion<0?Quaternion.identity:clip.Delta(i,Mathf.Min(gateMotionAge,clip.Length));
                Quaternion delta=Quaternion.Slerp(from,to,gateMotionBlend);
                if(Quaternion.Dot(delta,Quaternion.identity)>.9999998f)continue;
                Remember(bone);bone.localRotation*=delta;
            }
        }

        bool ApplyGateCard(Transform hips)
        {
            var journey=actor.Journey;
            var phase=journey.GateAction;
            var gate=journey.ReservedGate;
            bool hasReader=gate!=null&&gate.anchor!=null;
            if(!hasReader&&cardDraw<=.001f)return false;
            var up=hasReader?gate.transform.parent.up:actor.transform.up;
            var bodyForward=actor.transform.forward;
            var towardCase=hasReader?Vector3.ProjectOnPlane(gate.anchor.position-gate.transform.position,up).normalized:Vector3.zero;
            if(hasReader&&(cardFacing.sqrMagnitude<.1f||cardDraw<.01f&&
                (phase==Metro3DPassengerJourney.GatePhase.Approach||
                 phase==Metro3DPassengerJourney.GatePhase.Queue||
                 phase==Metro3DPassengerJourney.GatePhase.Align))) {
                Vector3 lane=gate.transform.parent.forward;
                cardFacing=lane*(Vector3.Dot(gate.transform.position-actor.transform.position,lane)<0?-1:1);
            }
            Vector3 reader=hasReader?gate.anchor.position+towardCase*.13f+up*.185f-cardFacing*.435f:Vector3.zero;
            bool aligning=phase==Metro3DPassengerJourney.GatePhase.Align;
            bool tagging=phase==Metro3DPassengerJourney.GatePhase.Tag;
            bool holding=phase==Metro3DPassengerJourney.GatePhase.DoorWait;
            bool pocketEarly=Mathf.Repeat(actor.index*.75487766f,1)<.52f;
            Vector3 toGate=hasReader?gate.transform.position-actor.transform.position:Vector3.zero;
            float depth=Vector3.Dot(toGate,cardFacing);
            float width=Vector3.ProjectOnPlane(toGate-cardFacing*depth,up).magnitude;
            bool approaching=hasReader&&depth>.43f&&depth<(pocketEarly?2.7f:1.85f)&&width<.64f&&
                Vector3.Dot(bodyForward,cardFacing)>.64f&&
                (phase==Metro3DPassengerJourney.GatePhase.Approach||
                 phase==Metro3DPassengerJourney.GatePhase.Queue&&journey.GateQueueSlot<=1);
            bool keepCard=hasReader&&(approaching||aligning||tagging||holding);
            if(HasPhoneInUse) {cardDraw=cardReach=0;hasCardPalm=false;return false;}
            float dt=Time.deltaTime;
            cardDraw=Mathf.MoveTowards(cardDraw,keepCard?1:0,dt*(keepCard?3.3f:2.15f));
            if(cardDraw<=.001f) {cardReach=0;hasCardPalm=false;return false;}
            if(gateHand<0)gateHand=hasReader?GateHand(reader):1;
            var arm=arms[gateHand];
            Vector3 side=actor.transform.right*(arm.left?-1:1);
            float scale=actor.HeightRatio;
            Vector3 pocket=hips.position+side*(.185f*scale)-up*(.17f*scale)-bodyForward*(.045f*scale);
            Vector3 ready=hips.position+side*(.20f*scale)+up*(.085f*scale)+bodyForward*(.19f*scale);
            Vector3 palm=Vector3.Lerp(pocket,ready,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.94f,cardDraw)));
            float progress=tagging?journey.TagProgress:1;
            bool checking=tagging&&journey.CardMixup&&progress<.46f;
            float reachGoal=0;
            if(aligning||approaching) {
                float nearReader=Mathf.InverseLerp(.83f,.53f,(reader-arm.chain.upper.position).magnitude);
                float approachToStop=aligning?Mathf.InverseLerp(1.40f,.91f,depth)*.90f:0;
                reachGoal=.88f*Mathf.Max(nearReader,approachToStop);
            }
            if(tagging)
                reachGoal=journey.CardMixup?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.64f,progress)):1;
            if(holding)reachGoal=.28f;
            float reachRate=reachGoal>cardReach?(tagging?4.4f:3.15f):2.8f;
            cardReach=Mathf.MoveTowards(cardReach,reachGoal,dt*reachRate);
            if(hasReader&&cardReach>.001f) {
                float extension=Mathf.SmoothStep(0,1,cardReach);
                Vector3 hover=reader+up*(.05f*(1-cardReach))-cardFacing*(.085f*(1-cardReach));
                palm=Vector3.Lerp(palm,hover,extension);
            }
            // Bound the hand path through the state boundary as well
            // as the blend. The body can keep walking while the card retracts.
            if(!hasCardPalm){cardPalm=PalmCentre(arm);hasCardPalm=true;}
            cardPalm=Vector3.MoveTowards(cardPalm,palm,dt*(1.28f+actor.Velocity.magnitude*.70f));
            palm=cardPalm;
            float weight=Mathf.SmoothStep(0,1,cardDraw);
            if(tagging&&cardReach>.55f&&actor.Velocity.sqrMagnitude<.035f) {
                Vector3 shift=Vector3.ProjectOnPlane(palm-arm.chain.upper.position,up)*.14f;
                Remember(hips);hips.position+=Vector3.ClampMagnitude(shift,.092f)*weight*cardReach;
                GroundSupportFeet(weight*cardReach*.86f,relaxedStance:true);
            }
            float reach=(arm.chain.upper.position-arm.chain.lower.position).magnitude+
                (arm.chain.lower.position-arm.chain.end.position).magnitude+.09f*scale;
            palm=arm.chain.upper.position+Vector3.ClampMagnitude(palm-arm.chain.upper.position,reach);
            Vector3 fingers=Vector3.ProjectOnPlane(palm-arm.chain.lower.position,up).normalized;
            if(fingers.sqrMagnitude<.05f)fingers=bodyForward;
            Vector3 normal=Vector3.Slerp(-side,-up,Mathf.SmoothStep(.22f,1,cardReach));
            fingers=Vector3.ProjectOnPlane(fingers,normal).normalized;
            if(fingers.sqrMagnitude<.04f)fingers=bodyForward;
            Hand(arm,palm,fingers,normal,weight,true);
            if(cardDraw<.50f) {if(gateProp!=null&&gateProp.activeSelf)gateProp.SetActive(false);return true;}
            if(gateProp==null) {
                if(tagMesh==null) {
                    tagMesh=new Mesh {name="Transit card quad"};
                    tagMesh.vertices=new[]{new Vector3(-.043f,0,-.027f),new Vector3(-.043f,0,.027f),new Vector3(.043f,0,.027f),new Vector3(.043f,0,-.027f)};
                    tagMesh.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};
                    tagMesh.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up};tagMesh.RecalculateBounds();
                    var shader=Shader.Find("Universal Render Pipeline/Lit");
                    fareCard=new Material(shader){color=new Color(.14f,.52f,.46f)};
                    paperSlip=new Material(shader){color=new Color(.84f,.74f,.54f)};
                }
                gateProp=new GameObject("Passenger fare card");
                gateProp.transform.SetParent(transform,false);
                gateProp.AddComponent<MeshFilter>().sharedMesh=tagMesh;
                gatePropRenderer=gateProp.AddComponent<MeshRenderer>();
                gatePropRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var surface=checking?paperSlip:fareCard;
            if(gatePropRenderer.sharedMaterial!=surface)gatePropRenderer.sharedMaterial=surface;
            gateProp.transform.SetPositionAndRotation(PalmCentre(arm)-normal*.009f,Quaternion.LookRotation(fingers,-normal));
            gateProp.transform.localScale=checking?new Vector3(.82f,1,1.18f):Vector3.one;
            gateProp.SetActive(true);
            if(checking&&bones.TryGetValue("DEF-head",out var head)) {
                Remember(head);head.rotation=Quaternion.AngleAxis(13*weight,actor.transform.right)*head.rotation;
            }
            return true;
        }
        void ApplyTravelTransition()
        {
            Vector3 forward=actor.transform.forward,up=actor.transform.up;
            float dt=Time.deltaTime;
            float measured=previousTravelHeading.sqrMagnitude>.1f
                ?Vector3.SignedAngle(previousTravelHeading,forward,up)/Mathf.Max(.01f,dt):0;
            previousTravelHeading=forward;
            float speed=actor.Velocity.magnitude;
            bool moving=speed>.13f&&!posePlanted&&!actor.DoorCourtesyStep&&!actor.RetreatingDoor;
            float active=moving?Mathf.InverseLerp(.13f,.94f,speed):0;
            float desiredYaw=actor.AvoidanceVelocity.sqrMagnitude>.035f?
                Vector3.SignedAngle(forward,actor.AvoidanceVelocity,up):0;
            float turn=Mathf.Clamp(measured/125,-1,1);
            float look=Mathf.Clamp(desiredYaw*.15f,-8,8);
            float tilt=actor.Journey.OnStairs?4:actor.DoorCrossing?2.7f:0;
            float amount=1-Mathf.Exp(-dt*8);
            travelLean=Mathf.Lerp(travelLean,-turn*active*4.2f,amount);
            travelChestYaw=Mathf.Lerp(travelChestYaw,look*active,amount);
            travelPitch=Mathf.Lerp(travelPitch,tilt*active,amount);
            if(Mathf.Abs(travelLean)+Mathf.Abs(travelChestYaw)+Mathf.Abs(travelPitch)<.04f||
                actor.NarrowPoseWeight>.08f||HasPhoneInUse)return;
            var spine=Bone("Spine2");
            if(spine==null)return;
            Remember(spine);
            spine.rotation=Quaternion.AngleAxis(travelChestYaw,up)*Quaternion.AngleAxis(travelPitch,actor.transform.right)*
                Quaternion.AngleAxis(travelLean,forward)*spine.rotation;
        }

        Vector3 PalmCentre(Arm arm)=>Vector3.Lerp(arm.chain.end.position,arm.middle.position,.72f);
        void UpdateDoorContactPose()
        {
            float dt=Time.deltaTime;
            float brace=Mathf.Max(actor.BoardingPushEffort,actor.DoorContactPressure);
            doorBrace=Mathf.Lerp(doorBrace,brace,1-Mathf.Exp(-dt*9f));
            Vector3 displacement=actor.ActualContactStep;
            float recoil=Mathf.InverseLerp(.045f,.25f,displacement.magnitude/Time.fixedDeltaTime);
            if(actor.DoorCourtesyStep&&actor.Velocity.sqrMagnitude>.12f*.12f) {
                recoil=Mathf.Max(recoil,.36f);
                if(displacement.sqrMagnitude<.000003f)displacement=actor.Velocity*Time.fixedDeltaTime;
            }
            doorRecoil=Mathf.Lerp(doorRecoil,recoil,1-Mathf.Exp(-dt*(recoil>doorRecoil?11f:5f)));
            Vector3 wanted=actor.DesiredVelocity;
            if(wanted.sqrMagnitude<.02f&&actor.BoardingDoorLook(out var doorway))wanted=doorway;
            if(wanted.sqrMagnitude<.02f&&brace>.05f)wanted=actor.transform.forward;
            if(brace>.05f&&wanted.sqrMagnitude>.02f)
                doorBraceDirection=Vector3.Lerp(doorBraceDirection,wanted.normalized,Mathf.Clamp01(dt*13));
            if(recoil>0)
                doorRecoilDirection=Vector3.Lerp(doorRecoilDirection,displacement.normalized,Mathf.Clamp01(dt*14));
        }

        bool UpdateDoorStance(float dt)
        {
            Vector3 opening=Vector3.zero;
            bool held=actor.QueueActive&&!actor.DoorCrossing&&actor.Velocity.sqrMagnitude<.11f*.11f&&
                actor.RotationDegreesPerSecond<25&&
                actor.BoardingAttention(out opening);
            watchingStillFor=held?watchingStillFor+dt:0;
            bool planted=watchingStillFor>.24f;
            float angle=planted?Vector3.SignedAngle(actor.transform.forward,opening,Vector3.up):0;
            if(Mathf.Abs(angle)>155f&&rearGlanceSide!=0)angle=Mathf.Abs(angle)*rearGlanceSide;
            float yaw=angle*Mathf.InverseLerp(18f,48f,Mathf.Abs(angle));
            float desiredYaw=actor.PlatformFlankDirection.sqrMagnitude>.1f?0:Mathf.Clamp(yaw*.58f,-32f,32f);
            doorStanceYaw=Mathf.MoveTowards(doorStanceYaw,desiredYaw,dt*(held?100f:185f));
            return planted;
        }

        void ApplyDoorContactPose(Transform hips)
        {
            Vector3 up=actor.transform.up;
            var chest=Bone("Spine2");
            flankLean=Vector3.Lerp(flankLean,actor.PlatformFlankDirection,1-Mathf.Exp(-Time.deltaTime*5.3f));
            if(chest!=null&&flankLean.sqrMagnitude>.001f) {
                Vector3 tilt=Vector3.Cross(up,flankLean);
                Remember(chest);
                chest.rotation=Quaternion.AngleAxis(8f*tilt.magnitude,tilt.normalized)*chest.rotation;
            }
            if(chest!=null&&doorBrace+doorRecoil>.025f) {
                Vector3 load=doorBraceDirection*(doorBrace*10f)-doorRecoilDirection*(doorRecoil*12f);
                load=Vector3.ClampMagnitude(load,12f);
                if(load.sqrMagnitude>.002f) {
                    Remember(chest);
                    chest.rotation=Quaternion.AngleAxis(load.magnitude,Vector3.Cross(up,load).normalized)*chest.rotation;
                }
                if(!HasPhoneInUse&&actor.NarrowPoseWeight<.55f) {
                    for(int i=0;i<2;i++) {
                        var arm=arms[i];
                        Vector3 side=actor.transform.right*(arm.left?-1:1);
                        float catching=Mathf.Max(.28f,Vector3.Dot(side,doorRecoilDirection));
                        Vector3 palm=hips.position+side*(.22f+.045f*doorRecoil)-up*.11f+
                            actor.transform.forward*(.12f+.10f*doorBrace)+
                            (up*.16f+doorRecoilDirection*.08f)*(doorRecoil*catching);
                        float weight=Mathf.Max(doorBrace*.46f,doorRecoil*.61f)*Mathf.Lerp(1,.55f,actor.NarrowPoseWeight);
                        Limb(arm.chain,palm,side*.38f-up,weight);
                    }
                }
            }

            Vector3 entrance=Vector3.zero;
            bool looking=gazeHead!=null&&!HasPhoneInUse&&actor.BoardingAttention(out entrance);
            float angle=looking?Vector3.SignedAngle(actor.transform.forward,entrance,up):0;
            if(!looking||Mathf.Abs(angle)<130f)rearGlanceSide=0;
            else if(rearGlanceSide==0)rearGlanceSide=Mathf.Sign(angle);
            float relaxed=1-Mathf.InverseLerp(.12f,.55f,actor.Velocity.magnitude);
            float chestAim=angle-doorStanceYaw*standingWeight;
            if(rearGlanceSide!=0)chestAim=Mathf.Abs(chestAim)*rearGlanceSide;
            float chestYaw=looking?Mathf.Clamp(chestAim*.15f,-9f,9f)*relaxed*(1-flankLean.magnitude):0;
            float dt=Time.deltaTime;
            doorChestYaw=Mathf.LerpAngle(doorChestYaw,chestYaw,1-Mathf.Exp(-dt*5f));
            if(chest!=null&&Mathf.Abs(doorChestYaw)>.05f) {
                Remember(chest);chest.rotation=Quaternion.AngleAxis(doorChestYaw,up)*chest.rotation;
            }
            float yaw=0;
            if(looking) {
                Vector3 eyes=Vector3.ProjectOnPlane(gazeHead.TransformDirection(gazeForward),up);
                float error=Vector3.SignedAngle(eyes,entrance,up);
                if(rearGlanceSide!=0&&Mathf.Abs(error)>90f)error=Mathf.Abs(error)*rearGlanceSide;
                float limit=Mathf.Lerp(34f,54f,relaxed);
                yaw=Mathf.Clamp(error,-limit,limit);
            }
            doorGazeYaw=Mathf.LerpAngle(doorGazeYaw,yaw,1-Mathf.Exp(-dt*10f));
            if(gazeHead==null||Mathf.Abs(doorGazeYaw)<.05f)return;
            Remember(gazeHead);
            gazeHead.rotation=Quaternion.AngleAxis(doorGazeYaw,up)*gazeHead.rotation;
        }

        void ApplyPlatformMotion()
        {
            float dt=Time.deltaTime;
            bool watches=actor.BoardingAttention(out var opening);
            var train=actor.CrossingTrain;
            bool crossing=false;
            if(train!=null) {
                const float platformEdge=Metro3DWorld.ScreenDoorX-Metro3DWorld.TrackCenterX;
                Vector3 outward=-train.transform.right*train.side;
                float depth=Vector3.Dot(actor.transform.position-train.transform.position,outward);
                crossing=depth>=platformEdge-.63f&&depth<=platformEdge+.36f;
            }
            float speed=actor.Velocity.magnitude;
            // Short foot holds in the door footage belong to the same transfer.
            // Only sustained quiet should restart the authored queue gesture.
            platformStillTime=speed<=.115f?platformStillTime+dt:0;
            bool moving=platformMoving?platformStillTime<0.200f:speed>.20f;
            if(train==null)platformCrossing=false;
            int next=-1;
            if(crossing&&speed>.16f&&!platformCrossing) {
                platformCrossing=true;
                next=actor.IsAlighting?6:5;
            }
            else if(!crossing&&activePlatformCue<5&&watches) {
                if(!platformWatching&&!moving)next=0;
                else if(moving&&!platformMoving) {
                    Vector3 lateral=Vector3.Cross(actor.transform.up,opening.normalized);
                    float across=Vector3.Dot(actor.Velocity,lateral);
                    next=Mathf.Abs(across)>.145f?(across<0?3:4):1;
                }
                else if(!moving&&platformMoving)next=2;
            }
            platformMoving=moving;platformWatching=watches;
            if(next>=0&&next!=activePlatformCue&&!HasPhoneInUse) {
                previousPlatformCue=activePlatformCue;previousPlatformAge=platformAge;
                activePlatformCue=next;platformAge=0;platformBlend=0;
            }
            if(activePlatformCue<0&&previousPlatformCue<0)return;
            platformMotions=platformMotions!=null?platformMotions:Metro3DWaitGestures.Load();
            var clips=platformMotions?.Platform;
            if(clips==null||clips.Length<7)return;
            BindPlatformBones();
            platformAge+=dt;previousPlatformAge+=dt;
            platformBlend=Mathf.MoveTowards(platformBlend,1,dt*8f);
            float mix=Mathf.SmoothStep(0,1,platformBlend);
            var active=activePlatformCue<0?null:clips[activePlatformCue];
            var previous=previousPlatformCue<0?null:clips[previousPlatformCue];
            if(active==null||platformAge>=active.Length)activePlatformCue=-1;
            if(previous==null||previousPlatformAge>=previous.Length||platformBlend>=1)previousPlatformCue=-1;
            for(int i=0;i<platformBones.Length;i++) {
                var bone=platformBones[i];
                if(bone==null||i>=5&&(HasPhoneInUse||actor.NarrowPoseWeight>.08f))continue;
                Quaternion from=previousPlatformCue<0?Quaternion.identity:PlatformCueDelta(previous,i,previousPlatformAge);
                Quaternion to=activePlatformCue<0?Quaternion.identity:PlatformCueDelta(active,i,platformAge);
                Quaternion delta=Quaternion.Slerp(from,to,mix);
                if(Quaternion.Dot(delta,Quaternion.identity)>.9999998f)continue;
                Remember(bone);bone.localRotation=bone.localRotation*delta;
            }
        }

        static Quaternion PlatformCueDelta(Metro3DWaitGestures.Pose cue,int bone,float age)
        {
            // Release the additive pose into its underlying walk or stance,
            // even if the authored end key is still supporting the last step.
            float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01((cue.Length-age)*8f));
            return Quaternion.Slerp(Quaternion.identity,cue.Delta(bone,age),weight);
        }

        void BindPlatformBones()
        {
            if(platformBones!=null)return;
            platformBones=new Transform[Metro3DWaitGestures.PlatformBones.Length];
            for(int i=0;i<platformBones.Length;i++)
                bones.TryGetValue(Metro3DWaitGestures.PlatformBones[i],out platformBones[i]);
        }

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
        internal static Quaternion LimitWristRotation(Transform elbow,Transform hand,Transform middle,float degrees)
        {
            if(elbow==null||hand==null||middle==null)return hand!=null?hand.rotation:Quaternion.identity;
            Vector3 forearm=hand.position-elbow.position;
            Vector3 palm=middle.position-hand.position;
            if(forearm.sqrMagnitude<.0001f||palm.sqrMagnitude<.00001f)return hand.rotation;
            Vector3 direction=Vector3.RotateTowards(forearm.normalized,palm.normalized,degrees*Mathf.Deg2Rad,0);
            return Quaternion.FromToRotation(palm,direction)*hand.rotation;
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
            hand.rotation=Quaternion.Slerp(hand.rotation,LimitWristRotation(arm.chain.lower,hand,arm.middle,curl?46:31),weight);
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
        void GroundSupportFeet(float weight,float toeOut=0,bool relaxedStance=false)
        {
            for(int i=0;i<2;i++) {
                float stance=relaxedStance?.105f+(actor.index%4)*.009f:.13f;
                float stagger=relaxedStance?(actor.index%3-1)*(i==0?-.026f:.026f):0;
                Vector3 point=actor.transform.position+actor.transform.right*((i==0?-stance:stance)*actor.HeightRatio)+actor.transform.forward*((.025f+stagger)*actor.HeightRatio)+Vector3.up*((mannequin?.1005f:.095f)*actor.HeightRatio);
                Limb(legs[i],point,actor.transform.forward,weight);
                var foot=legs[i].end;var toe=Bone(i==0?"LeftToeBase":"RightToeBase");
                Vector3 forward=Quaternion.AngleAxis(i==0?-toeOut:toeOut,Vector3.up)*actor.transform.forward;
                AlignFoot(foot,toe,forward,weight);
            }
        }
        readonly Vector3[] stanceFeet=new Vector3[2],stanceHeadings=new Vector3[2];
        Transform stanceFrame;
        int swingFoot=-1,previousSwing=-1;
        float swingTime,swingDuration;
        bool recoverySwing;
        Vector3 swingFrom,swingTo,swingHeading;

        void ResetTurnFeet()
        {
            stanceFrame=null;swingFoot=previousSwing=-1;swingTime=0;
            ClearStandingLoad();
        }

        void ApplyTurnFeet(Transform hips,float weight,float stanceYaw,bool shortStep,bool gripping=false)
        {
            Transform frame=actor.transform.parent;
            if(frame==null)return;
            float scale=actor.HeightRatio;
            float width=(gripping?Mathf.Lerp(.102f,.070f,actor.GripYieldWeight):.105f+(actor.index%4)*.009f)*scale;
            Vector3 heading=Quaternion.AngleAxis(stanceYaw,Vector3.up)*actor.transform.forward;
            Vector3 side=Vector3.Cross(Vector3.up,heading);
            Vector3 localHeading=frame.InverseTransformDirection(heading);
            if(Mathf.Abs(stanceYaw)>.05f) {
                Remember(hips);
                hips.rotation=Quaternion.AngleAxis(stanceYaw*weight,Vector3.up)*hips.rotation;
            }
            Vector3 center=actor.transform.position+heading*.025f*scale
                +Vector3.up*((mannequin?.1005f:.095f)*scale);
            float stagger=(Mathf.Repeat(actor.index*.618034f+.21f,1)-.5f)*.108f*scale;
            Vector3 left=frame.InverseTransformPoint(center-side*width-heading*stagger);
            Vector3 right=frame.InverseTransformPoint(center+side*width+heading*stagger);
            if(stanceFrame!=frame) {
                stanceFrame=frame;
                stanceFeet[0]=left;stanceFeet[1]=right;
                stanceHeadings[0]=stanceHeadings[1]=localHeading;
            }
            Vector3 stepMotion=shortStep?actor.Velocity:Vector3.zero;
            float lateralSpeed=Vector3.Dot(stepMotion,side);
            if(swingFoot<0) {
                float dl=(left-stanceFeet[0]).sqrMagnitude,dr=(right-stanceFeet[1]).sqrMagnitude;
                bool turnLeft=Vector3.Dot(stanceHeadings[0],localHeading)<.951f;
                bool turnRight=Vector3.Dot(stanceHeadings[1],localHeading)<.951f;
                bool stepLeft=dl>.038f*.038f||turnLeft;
                bool stepRight=dr>.038f*.038f||turnRight;
                if(shortStep) {
                    float trigger=stepMotion.sqrMagnitude>.14f*.14f?.030f:.046f;
                    stepLeft=dl>trigger*trigger;stepRight=dr>trigger*trigger;
                }
                if(stepLeft||stepRight) {
                    int lead=lateralSpeed<0?0:1;
                    if(shortStep&&previousSwing<0&&Mathf.Abs(lateralSpeed)>.12f)
                        swingFoot=lead;
                    else swingFoot=stepLeft&&stepRight?(previousSwing<0?(dl>=dr?0:1):1-previousSwing):stepLeft?0:1;
                    swingFrom=stanceFeet[swingFoot];swingTo=swingFoot==0?left:right;
                    Vector3 toeHeading=heading;
                    if(shortStep&&stepMotion.sqrMagnitude>.10f*.10f) {
                        if(!gripping)swingTo+=frame.InverseTransformDirection(Vector3.ClampMagnitude(stepMotion*.14f,.09f*scale));
                        if(swingFoot==lead&&Mathf.Abs(lateralSpeed)>.10f)
                            toeHeading=Quaternion.AngleAxis(lateralSpeed<0?-13:13,Vector3.up)*heading;
                    }
                    swingHeading=frame.InverseTransformDirection(toeHeading);swingTime=0;
                    recoverySwing=shortStep;
                    swingDuration=shortStep?Mathf.Lerp(.27f,.19f,Mathf.InverseLerp(.12f,.72f,stepMotion.magnitude)):.21f;
                }
            }
            float phase=0;
            if(swingFoot>=0) {
                swingTime+=Time.deltaTime;phase=Mathf.Clamp01(swingTime/swingDuration);
                Vector3 toward=frame.TransformPoint(stanceFeet[1-swingFoot])-hips.position;toward.y=0;
                float load=recoverySwing?.24f*Mathf.Sin(Mathf.PI*Mathf.Pow(phase,.66f)):.10f*Mathf.Sin(Mathf.PI*phase);
                Remember(hips);hips.position+=Vector3.ClampMagnitude(toward,.24f)*(load*weight);
            }
            bool stable=!gripping&&swingFoot<0&&actor.SettledStanding&&!actor.QueueActive&&actor.RotationDegreesPerSecond<17;
            ApplyStandingLoad(hips,frame.TransformPoint((stanceFeet[0]+stanceFeet[1])*.5f),width,weight,stable);
            for(int i=0;i<2;i++) {
                bool swinging=i==swingFoot;
                float blend=swinging?Mathf.SmoothStep(0,1,phase):0;
                Vector3 position=swinging?Vector3.Lerp(swingFrom,swingTo,blend):stanceFeet[i];
                Vector3 head=swinging?Vector3.Slerp(stanceHeadings[i],swingHeading,blend):stanceHeadings[i];
                Vector3 foot=frame.TransformPoint(position)+Vector3.up*(swinging?Mathf.Sin(Mathf.PI*phase)*(recoverySwing?.044f:.058f)*scale:0);
                Limb(legs[i],foot,heading,weight);
                AlignFoot(legs[i].end,Bone(i==0?"LeftToeBase":"RightToeBase"),
                    Quaternion.AngleAxis(i==0?-6:6,Vector3.up)*frame.TransformDirection(head),weight);
            }
            if(swingFoot>=0&&phase>=1) {
                stanceFeet[swingFoot]=swingTo;stanceHeadings[swingFoot]=swingHeading;
                previousSwing=swingFoot;swingFoot=-1;
            }
        }
        void ApplySeatedPortrait(Transform seat,float weight)
        {
            var source=actor.SeatedPhoneSource;
            var head=bones.TryGetValue("DEF-head",out var genericHead)?genericHead:Bone("Head");
            if(source==null||head==null)return;

            var arm=arms[1].chain;
            var hand=arm.end;
            Vector3 eye=head.position+(seat.forward*.075f+seat.up*.105f)*actor.HeightRatio;
            Vector3 wrist=hand.position+(seat.up*.064f+seat.forward*.024f)*actor.HeightRatio;
            Quaternion originalGrip=hand.rotation;
            Limb(arm,wrist,seat.right*.22f-seat.up,.65f*weight);
            Vector3 device=hand.TransformPoint(source.phoneHandPosition);
            Quaternion screen=Quaternion.LookRotation(eye-device,seat.up);
            Quaternion readingGrip=screen*Quaternion.Inverse(source.phoneHandRotation);
            Remember(hand);
            hand.rotation=Quaternion.Slerp(originalGrip,Quaternion.RotateTowards(originalGrip,readingGrip,24f),weight*.56f);
            hand.rotation=Quaternion.Slerp(hand.rotation,LimitWristRotation(arm.lower,hand,arms[1].middle,29),weight);

            var freeArm=arms[0];
            Vector3 palm=Vector3.Lerp(arm.lower.position,hand.position,.33f)+seat.forward*.025f-seat.up*.046f;
            float reach=Vector3.Distance(freeArm.chain.upper.position,freeArm.chain.lower.position)+
                Vector3.Distance(freeArm.chain.lower.position,freeArm.chain.end.position);
            if(Vector3.Distance(freeArm.chain.upper.position,palm)>reach*.89f) {
                var thigh=legs[0];
                palm=Vector3.Lerp(thigh.upper.position,thigh.lower.position,.64f)+seat.up*.071f;
            }
            bool supportedByThigh=Vector3.Dot(palm-hand.position,seat.up)<-.19f;
            Hand(freeArm,palm,supportedByThigh?seat.forward:seat.right,supportedByThigh?-seat.up:seat.up,
                .67f*weight,false,relaxedSeated:true);

            Vector3 sight=device-eye;
            Vector3 gaze=(Quaternion.AngleAxis(18f,seat.right)*seat.forward).normalized;
            Quaternion adjust=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(gaze,sight),12f);
            Remember(head);
            head.rotation=Quaternion.Slerp(head.rotation,adjust*head.rotation,weight);
        }

        void AlignPlantedFoot(Transform foot,Transform toe,Vector3 forward,float slope,float weight)
        {
            Vector3 current=toe.position-foot.position;
            if(current.sqrMagnitude<.0001f)return;
            Vector3 aligned=forward+actor.transform.up*slope;
            Remember(foot);
            foot.rotation=Quaternion.Slerp(foot.rotation,Quaternion.FromToRotation(current,aligned)*foot.rotation,weight);
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
