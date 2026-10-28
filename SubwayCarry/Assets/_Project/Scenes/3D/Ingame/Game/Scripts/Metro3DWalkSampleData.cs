using System;
using MxM;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // One shared local licensed sample. Not a replacement for the old rejected DB.
    public sealed class Metro3DWalkSampleData : ScriptableObject
    {
        public MxMAnimData database;
        public Clip[] clips;
        public float sourceSeconds,loopSpeed;
        public string sourceHash,license,buildSignature;
        public GameObject sourcePreview;
        public Quaternion sourceAxis=Quaternion.identity;
        public float sourceScale=1;
        public string sourceRootPath;
        public string sourceName,coverage;
        public bool supportsTurns;
        public float minimumPace,maximumPace,maximumTurnRate;
        [Serializable] public sealed class Clip
        {
            public AnimationClip animation;
            public AnimationClip original;
            public GameObject sourcePreview;
            public Quaternion sourceAxis=Quaternion.identity;
            public float sourceScale=1,sourceTimeOffset;
            public string sourceRootPath;
            public Vector3 sourceRootPosition;
            public Quaternion sourceRootRotation=Quaternion.identity;
            public AnimationCurve leftContact,rightContact;
        }
        public float Contact(AnimationClip clip,float time,int foot)
        {
            if(clips==null)return 0;
            foreach(var data in clips)if(data.animation==clip){if(clip.isLooping)time=Mathf.Repeat(time,clip.length);return (foot==0?data.leftContact:data.rightContact).Evaluate(time);}
            return 0;
        }
    }
}
