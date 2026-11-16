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
            if (!waitLibraryLoaded)
            {
                waitGestures = Metro3DWaitGestures.Load();
                waitLibraryLoaded = true;
            }
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
    }
}
