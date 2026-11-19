using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DPassengerContacts
    {
        static Metro3DWaitGestures waitGestures;
        static bool waitLibraryLoaded;
        float nextWaitGesture, waitGestureStart, waitGestureWeight;
        int waitGestureCount;
        Metro3DWaitGestures.Pose waitGesture;
        Transform[] waitBones;
        float nextStandingTransfer, standingTransferElapsed, previousLoad, desiredLoad, standingLoad;
        bool relaxedStanding;
        int standingTransferCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWaitLibrary()
        {
            waitLibraryLoaded = false;
            waitGestures = null;
        }

        void ResetWaitGesture()
        {
            waitGesture = null;
            waitGestureWeight = 0;
            waitGestureCount = 0;
            nextWaitGesture = Time.time + 2.4f + WaitChoice(0) * 4.3f;
            ClearStandingLoad();
        }

        float WaitChoice(uint salt)
        {
            uint value = unchecked((uint)actor.index * 747796405u + (uint)waitGestureCount * 2891336453u + salt);
            value = unchecked(((value >> (int)((value >> 28) + 4)) ^ value) * 277803737u);
            return ((value >> 22) ^ value) % 4096 / 4096f;
        }

        void ApplyWaitGesture(bool allowed)
        {
            if (waitGesture == null && (!allowed || Time.time < nextWaitGesture)) return;
            EnsureWaitLibrary();
            if (waitGestures == null) return;
            if (waitGesture == null)
            {
                bool look = !actor.inTrain && WaitChoice(33) < Mathf.Lerp(.27f, .68f, actor.Journey.Personality.Mobility);
                waitGesture = look ? waitGestures.Look : waitGestures.Weight;
                waitGestureStart = Time.time;
                nextWaitGesture = Time.time + 7f + WaitChoice(211) * 12f;
                waitGestureCount++;
                if (waitGesture == null) return;
            }

            ApplyWaitGesturePose(allowed);
        }

        static void EnsureWaitLibrary()
        {
            if (!waitLibraryLoaded)
            {
                waitGestures = Metro3DWaitGestures.Load();
                waitLibraryLoaded = true;
            }
        }

        void ApplyWaitGesturePose(bool allowed)
        {
            float age = Time.time - waitGestureStart;
            waitGestureWeight = Mathf.MoveTowards(waitGestureWeight, allowed && age < waitGesture.Length ? 1 : 0, Time.deltaTime * 5);
            if (waitGestureWeight <= 0 && (!allowed || age >= waitGesture.Length))
            {
                waitGesture = null;
                return;
            }
            if (waitBones == null)
            {
                waitBones = new Transform[Metro3DWaitGestures.Bones.Length];
                for (int i = 0; i < waitBones.Length; i++) bones.TryGetValue(Metro3DWaitGestures.Bones[i], out waitBones[i]);
            }
            for (int i = 0; i < waitBones.Length; i++)
            {
                var bone = waitBones[i];
                if (bone == null) continue;
                Remember(bone);
                bone.localRotation *= Quaternion.Slerp(Quaternion.identity, waitGesture.Delta(i, age), waitGestureWeight);
            }
        }

        void ClearStandingLoad()
        {
            relaxedStanding=false;
            standingTransferCount=0;
            standingTransferElapsed=0;
            previousLoad=desiredLoad=standingLoad=0;
        }

        void ApplyStandingLoad(Transform hips,Vector3 footMidpoint,float halfWidth,float weight,bool stable)
        {
            EnsureWaitLibrary();
            if(stable!=relaxedStanding) {
                relaxedStanding=stable;
                previousLoad=standingLoad;
                desiredLoad=0;
                standingTransferElapsed=0;
                nextStandingTransfer=Time.time+.45f+Mathf.Repeat(actor.index*.381966f,1)*1.20f;
            }
            if(stable&&Time.time>=nextStandingTransfer) {
                float selection=Mathf.Repeat(actor.index*.618034f+standingTransferCount*.381966f,1);
                previousLoad=standingLoad;
                desiredLoad=selection<.16f?0:selection<.57f?-1:1;
                standingTransferElapsed=0;
                nextStandingTransfer=Time.time+5.8f+Mathf.Repeat(actor.index*.754878f+standingTransferCount*.236068f,1)*6.2f;
                standingTransferCount++;
            }
            standingTransferElapsed+=Time.deltaTime;
            float progress=Mathf.Clamp01(standingTransferElapsed/(stable?.96f:.24f));
            var transfer=waitGestures?.Transfer;
            float eased=stable&&transfer!=null?transfer.WeightAt(progress):Mathf.SmoothStep(0,1,progress);
            standingLoad=Mathf.Lerp(previousLoad,desiredLoad,eased);
            if(!stable&&Mathf.Abs(standingLoad)<.01f)return;

            Vector3 right=actor.transform.right,forward=actor.transform.forward,up=actor.transform.up;
            float authoredRange=transfer==null||transfer.Range<.001f?.036f:transfer.Range;
            float maximum=Mathf.Clamp(authoredRange*2.3f*actor.HeightRatio,halfWidth*.58f,halfWidth*.81f);
            Vector3 relative=hips.position-footMidpoint;
            float sideways=maximum*standingLoad-Vector3.Dot(relative,right);
            float sagittal=-.022f*actor.HeightRatio-Vector3.Dot(relative,forward);
            Remember(hips);
            hips.position+=(right*sideways+forward*Mathf.Clamp(sagittal,-.12f,.12f)
                -up*(.013f*Mathf.Abs(standingLoad)*actor.HeightRatio))*weight;
            hips.rotation=Quaternion.AngleAxis(5.3f*standingLoad*weight,forward)*hips.rotation;
            var spine=Bone("Spine");
            if(spine!=null) {
                Remember(spine);
                spine.rotation=Quaternion.AngleAxis(-3.1f*standingLoad*weight,forward)*spine.rotation;
            }
        }

        void BreatheThroughChest(float weight)
        {
            var chest=Bone("Spine2");
            if(chest==null)return;
            var neck=Bone("DEF-neck")??Bone("Neck");
            Quaternion neckFacing=neck!=null?neck.rotation:Quaternion.identity;
            float cycle=3.8f+(actor.index&3)*.25f;
            float pitch=Mathf.Sin(Time.time*(2*Mathf.PI/cycle)+actor.index*.41f)*(.24f*weight);
            Remember(chest);
            if(neck!=null)Remember(neck);
            chest.rotation=Quaternion.AngleAxis(pitch,actor.transform.right)*chest.rotation;
            if(neck!=null)neck.rotation=neckFacing;
        }

        void RelaxStandingArms(Transform hips,float weight)
        {
            Vector3 forward=actor.transform.forward,up=actor.transform.up;
            foreach(var arm in arms) {
                // The in-game single-hand phone layer owns the right arm.
                if(!arm.left&&HasPhoneInUse)continue;
                var chain=arm.chain;
                Vector3 outward=actor.transform.right*(arm.left?-1:1);
                Vector3 shoulder=chain.upper.position;
                float reach=Vector3.Distance(shoulder,chain.lower.position)+Vector3.Distance(chain.lower.position,chain.end.position);
                float scale=actor.HeightRatio;
                float shoulderWidth=Vector3.Dot(shoulder-hips.position,outward);
                float wristWidth=Mathf.Max(.211f*scale,shoulderWidth+.021f*scale);
                Vector3 hang=-up*reach+outward*(wristWidth-shoulderWidth)+forward*(.051f*scale);
                Vector3 target=shoulder+hang.normalized*(.989f*reach);
                Limb(chain,target,outward*.30f-forward,weight*.96f);

                Remember(chain.end);
                var relaxed=RelaxedWristRotation(chain.lower,chain.end,arm.middle,arm.index,arm.pinky,forward,up);
                chain.end.rotation=Quaternion.Slerp(chain.end.rotation,relaxed,weight);
            }
        }

        internal static Quaternion RelaxedWristRotation(Transform elbow,Transform hand,Transform middle,
            Transform index,Transform pinky,Vector3 forward,Vector3 up)
        {
            if(index==null||pinky==null||middle==null)return hand.rotation;
            Vector3 handAxis=middle.position-hand.position,forearm=hand.position-elbow.position;
            if(handAxis.sqrMagnitude<.00001f||forearm.sqrMagnitude<.0001f)return hand.rotation;
            Vector3 fingers=Vector3.RotateTowards(forearm.normalized,-up,5*Mathf.Deg2Rad,0);
            Quaternion swing=Quaternion.FromToRotation(handAxis,fingers);
            // Point the thumb side forward to let the palm face the thigh.
            Vector3 knuckles=Vector3.ProjectOnPlane(swing*(index.position-pinky.position),fingers);
            Vector3 toward=Vector3.ProjectOnPlane(forward,fingers);
            float roll=Vector3.SignedAngle(knuckles,toward,fingers);
            return Quaternion.AngleAxis(roll,fingers)*swing*hand.rotation;
        }
    }
}
