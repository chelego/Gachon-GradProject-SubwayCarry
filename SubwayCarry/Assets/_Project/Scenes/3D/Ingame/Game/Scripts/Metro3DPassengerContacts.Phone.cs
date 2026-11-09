using SubwayCarry.Gameplay3D.AnimationLab;
using UnityEngine;
using Unity.Profiling;

namespace SubwayCarry.Gameplay3D
{
    internal sealed class Metro3DPhonePoseLibrary
    {
        internal readonly AnimationLabPhoneMotion binding;
        internal readonly Sample enter, read, exit;
        internal bool Ready => enter != null && read != null && exit != null;

        internal sealed class Sample
        {
            internal readonly Metro3DSeatedActionLibrary.PoseSamples pose;
            internal readonly int[] bones;
            internal readonly float contact;

            internal Sample(AnimationLabAuthoredMotion source, int[] bones, int contactFrame)
            {
                pose = new Metro3DSeatedActionLibrary.PoseSamples(source);
                this.bones = bones;
                contact = contactFrame / (float)source.sampleRate;
            }
        }

        internal Metro3DPhonePoseLibrary(AnimationLabPhoneMotion binding)
        {
            this.binding = binding;
            if (binding == null || binding.phonePrefab == null || binding.upperPaths == null || binding.upperPaths.Length == 0) return;
            var take = binding.Find(AnimationLabPhoneAction.TakeOut);
            var put = binding.Find(AnimationLabPhoneAction.PutAway);
            enter = Build(take?.authored, take?.contactFrame ?? -1, false);
            read = Build(binding.authored, -1, true);
            exit = Build(put?.authored, put?.contactFrame ?? -1, false);
        }

        Sample Build(AnimationLabAuthoredMotion source, int contact, bool loop)
        {
            if (source == null || source.keys.Count == 0 || source.loop != loop ||
                !source.Validate(out _) || !loop && (contact < 0 || contact > source.lastFrame)) return null;
            var indices = new int[binding.upperPaths.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = System.Array.IndexOf(source.paths, binding.upperPaths[i]);
                if (indices[i] < 0) return null;
            }
            return new Sample(source, indices, contact);
        }
    }

    public sealed partial class Metro3DPassengerContacts
    {
        enum PhonePhase { Off, Enter, Read, Exit }
        static readonly ProfilerMarker PhoneMarker = new("SubwayCarry.Passenger.PhonePose");
        Metro3DPhonePoseLibrary phoneLibrary;
        PhonePhase phonePhase;
        Transform[] phoneBones;
        Transform phoneHand, phoneNeck, phoneHead;
        Quaternion neutralPhoneHead;
        GameObject phoneProp;
        bool phoneBindingFailed, phoneWalking;
        int phoneChoices;
        float nextPhoneChoice, phoneClock, lookClock, readDuration, exitFrom, transitGlance;

        internal bool HasPhoneInUse => phonePhase!=PhonePhase.Off;
        internal bool PutPhoneAwayForSeat()
        {
            if(phonePhase==PhonePhase.Off)return false;
            if(phonePhase==PhonePhase.Read)BeginPhoneExit();
            return true;
        }

        void BeginPhoneExit()
        {
            exitFrom=Mathf.Repeat(phoneClock,phoneLibrary.read.pose.duration);
            phonePhase=PhonePhase.Exit;
            phoneClock=0;
        }

        internal string PhoneLabel => phonePhase == PhonePhase.Off ? "" :
            actor.PhoneTransitLook ? "폰 든 채 승하차 확인" : phoneWalking ? "걷기 · 폰 보기" : "대기 · 폰 보기";

        void ResetPhone()
        {
            ClearPhone();
            phoneChoices = 0;
            transitGlance = 0;
            nextPhoneChoice = Time.time + .25f + (actor.index % 11) * .13f;
        }

        void ShowPhone(bool visible)
        {
            if (phoneProp != null && phoneProp.activeSelf != visible) phoneProp.SetActive(visible);
        }

        void ClearPhone()
        {
            phonePhase = PhonePhase.Off;
            ShowPhone(false);
        }

        float AvailablePhoneTime(bool walking)
        {
            if (!walking) return actor.inTrain ? actor.Riding?.MovementRemaining ?? 0 : actor.world.Transit.TimeToBoarding(actor);
            var ahead = actor.MovementIntent;
            ahead.y = 0;
            return ahead.magnitude / Mathf.Max(.75f, actor.Velocity.magnitude) - .7f;
        }

        void TickPhone()
        {
            if (actor == null || actor.world == null || actor.world.session == null ||
                actor.world.session.SimulationPaused || phoneBindingFailed) return;
            float now = Time.time;
            if (phonePhase == PhonePhase.Off && now < nextPhoneChoice) return;
            var support=actor.world.Traffic.SupportFor(actor);
            bool leaning=support!=null&&support.lean&&actor.IsResting;
            bool usable=actor.PhoneSituation(HasPhoneInUse,out phoneWalking)&&(support==null||support.lean);
            if (!usable&&phonePhase==PhonePhase.Off) {
                nextPhoneChoice=now+.5f+(actor.index%7)*.14f;
                return;
            }
            if (!usable&&phonePhase==PhonePhase.Read)BeginPhoneExit();
            if (!usable&&phonePhase==PhonePhase.Enter&&phoneClock<phoneLibrary.enter.contact) {
                ClearPhone();return;
            }
            transitGlance=Mathf.MoveTowards(transitGlance,actor.PhoneTransitLook?1:0,Time.deltaTime*4.5f);
            if (phonePhase == PhonePhase.Off)
            {
                nextPhoneChoice = now + 1.8f + (actor.index % 9) * .19f;
                float pick = Mathf.Repeat(actor.index * .618034f + ++phoneChoices * .754878f, 1);
                float chance = (phoneWalking ? .43f : .68f) * Mathf.Lerp(.8f, 1.15f, actor.Journey.Personality.Patience);
                if(leaning) {
                    // Keep a stable preference for this rider while they wait at the wall.
                    pick=Mathf.Repeat(actor.index*.754878f+.29f,1f);
                    chance=.9f;
                }
                if (pick > chance || phoneWalking && actor.Velocity.sqrMagnitude < .18f) return;
                phoneLibrary = actor.world.PhonePoses();
                if (phoneLibrary == null || !phoneLibrary.Ready) return;
                float available = AvailablePhoneTime(phoneWalking);
                float room = available - phoneLibrary.enter.pose.duration - phoneLibrary.exit.pose.duration;
                if (room < 1.2f) return;
                readDuration = Mathf.Lerp(37f,72f,actor.Journey.Personality.Patience);
                phoneClock = lookClock = 0;
                phonePhase = PhonePhase.Enter;
            }

            phoneClock += Time.deltaTime;
            lookClock += Time.deltaTime;
            if (phonePhase == PhonePhase.Enter && phoneClock >= phoneLibrary.enter.pose.duration)
            {
                phoneClock -= phoneLibrary.enter.pose.duration;
                phonePhase = PhonePhase.Read;
            }
            if (phonePhase==PhonePhase.Read&&(!usable||phoneClock>=readDuration&&!actor.PhoneTransitLook))BeginPhoneExit();
            if (phonePhase == PhonePhase.Exit && phoneClock >= phoneLibrary.exit.pose.duration)
            {
                ClearPhone();
                nextPhoneChoice = now + 3.2f + (actor.index % 11) * .31f;
            }
        }

        bool BindPhone()
        {
            var data = phoneLibrary.binding;
            phoneHand = transform.Find(data.rightHandPath);
            phoneNeck = transform.Find(data.neckPath);
            phoneHead = transform.Find(data.headPath);
            if (phoneHand == null || phoneNeck == null || phoneHead == null) return false;
            var targets = new Transform[data.upperPaths.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = transform.Find(data.upperPaths[i]);
                if (targets[i] == null) return false;
            }
            phoneBones = targets;
            neutralPhoneHead = Quaternion.Inverse(transform.rotation) * phoneHead.rotation;
            return true;
        }

        void ApplyPhonePose()
        {
            if (phonePhase == PhonePhase.Off) return;
            using var sampleScope = PhoneMarker.Auto();
            if (phoneBones == null && !BindPhone()) {phoneBindingFailed = true;ClearPhone();return;}
            var sample = phonePhase == PhonePhase.Enter ? phoneLibrary.enter :
                phonePhase == PhonePhase.Exit ? phoneLibrary.exit : phoneLibrary.read;
            float duration = sample.pose.duration;
            float time = phonePhase == PhonePhase.Read ? Mathf.Repeat(phoneClock, duration) : phoneClock;
            float weight = phonePhase == PhonePhase.Enter ? Mathf.SmoothStep(0, 1, phoneClock / .22f) :
                phonePhase == PhonePhase.Exit ? Mathf.SmoothStep(0, 1, (duration - phoneClock) / .28f) : 1;
            float transition = Mathf.SmoothStep(0, 1, phoneClock / .2f);
            for (int i = 0; i < phoneBones.Length; i++)
            {
                var bone = phoneBones[i];
                Remember(bone);
                Quaternion target = sample.pose.Rotation(sample.bones[i], time);
                if (transition < 1 && phonePhase == PhonePhase.Read)
                {
                    var enter = phoneLibrary.enter;
                    target = Quaternion.Slerp(enter.pose.Rotation(enter.bones[i], enter.pose.duration), target, transition);
                }
                if (transition < 1 && phonePhase == PhonePhase.Exit)
                {
                    var read = phoneLibrary.read;
                    target = Quaternion.Slerp(read.pose.Rotation(read.bones[i], exitFrom), target, transition);
                }
                bone.localRotation = Quaternion.Slerp(bone.localRotation, target, weight);
            }

            bool inHand = phonePhase == PhonePhase.Read ||
                (phonePhase == PhonePhase.Enter ? time >= sample.contact : time < sample.contact);
            if (inHand && phoneProp == null)
            {
                var binding = phoneLibrary.binding;
                phoneProp = Instantiate(binding.phonePrefab, phoneHand, false);
                phoneProp.transform.SetLocalPositionAndRotation(binding.phoneLocalPosition, binding.phoneLocalRotation);
            }
            ShowPhone(inHand);
            if (!inHand) return;
            float gaze = phonePhase == PhonePhase.Enter ? Mathf.SmoothStep(0, 1, (time - sample.contact) / .7f) :
                phonePhase == PhonePhase.Exit ? 1 - Mathf.SmoothStep(0, 1, time / .45f) : 1;
            if (phoneWalking)
                gaze *= Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Cos((lookClock + actor.index * .27f) * 1.8f) + .72f));
            gaze*=1-transitGlance;
            if (gaze <= .001f) return;
            Vector3 local = transform.InverseTransformDirection(phoneProp.transform.position - phoneHead.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -32, 32);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, 0, 22);
            Remember(phoneNeck);Remember(phoneHead);
            Quaternion before = phoneHead.rotation;
            Quaternion desired = transform.rotation * Quaternion.Euler(pitch, yaw, 0) * neutralPhoneHead;
            Quaternion aimed = Quaternion.Slerp(before, desired, gaze);
            phoneNeck.rotation = Quaternion.Slerp(Quaternion.identity, aimed * Quaternion.Inverse(before), .55f) * phoneNeck.rotation;
            phoneHead.rotation = aimed;
        }
    }
}
