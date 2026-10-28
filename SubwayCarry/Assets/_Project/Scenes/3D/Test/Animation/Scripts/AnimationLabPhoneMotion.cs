using System;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public enum AnimationLabPhoneMode {Stored,Read,Landscape,Carry}
    public enum AnimationLabPhoneAction {TakeOut,PutAway,Raise,Lower,LandscapeEnter,LandscapeExit,LandscapeHold,CarryLoop}
    // One opt-in review binding. The phone clip is an upper-body pose, not a recorded autonomous journey.
    public sealed class AnimationLabPhoneMotion : ScriptableObject
    {
        public const string ResourceName="PassengerPhoneReview";
        public bool situationsEnabled=true,normalEnabled=true,upperLoop=true;
        public string displayName="걷기 · 폰 보기 · 오른팔 유지 (상체)",sourceId,publishedRevision;
        public AnimationLabAuthoredMotion authored;
        public GameObject rig,phonePrefab;
        public AnimationClip idle,walk,upperBody;
        public string animatorPath,rightHandPath,neckPath,headPath;
        public string[] upperPaths;
        public Vector3 phoneLocalPosition;
        public Quaternion phoneLocalRotation=Quaternion.identity;
        public float referenceSpeed=1.0220767f;
        public Font font;
        public Vector2 start=new Vector2(0,-6),destination=new Vector2(0,7);
        public Vector2 peerStart=new Vector2(.1f,6),peerDestination=new Vector2(.1f,-6);
        public Rect bounds=new Rect(-7,-9,14,18);
        public Rect pillar;
        [Serializable] public sealed class ActionClip
        {
            public AnimationLabPhoneAction role;
            public string sourceId,displayName,revision;
            public AnimationLabAuthoredMotion authored;
            public AnimationClip clip;
            public bool loop;
            public int contactFrame=-1,carryFrame=-1;
            public float contactStart,contactEnd;
        }
        public ActionClip[] actions=Array.Empty<ActionClip>();
        [Serializable] public sealed class SqueezeClip
        {
            public int phase,side=1;
            public string sourceId,revision;
            public AnimationLabAuthoredMotion authored;
            public AnimationClip clip;
        }
        public SqueezeClip[] squeeze=Array.Empty<SqueezeClip>();
        public float squeezeStride=.28f,squeezeSpeed=.28f;
        public SqueezeClip FindSqueeze(int phase,int side){foreach(var e in squeeze)if(e!=null&&e.phase==phase&&e.side==side)return e;return null;}
        public SqueezeClip FindSqueezeSource(string id){foreach(var e in squeeze)if(e!=null&&e.sourceId==id)return e;return null;}
        public bool HasSqueezeSet {get{for(int s=-1;s<=1;s+=2)for(int p=0;p<3;p++){var e=FindSqueeze(p,s);if(e==null||e.clip==null||e.authored==null)return false;}return true;}}
        public string[] bothArmPaths=Array.Empty<string>();
        public string hipsPath,leftUpperPath,leftLowerPath,leftHandPath,rightUpperPath,rightLowerPath;
        public GameObject pocketPrefab;
        public Vector3 pocketLocalPosition,pocketPhonePosition=new Vector3(0,-.085f,0);
        public Quaternion pocketLocalRotation=Quaternion.identity,pocketPhoneRotation=Quaternion.Euler(0,0,180);
        public Vector3 landscapePhonePosition,landscapeLeftPosition;
        public Quaternion landscapePhoneRotation=Quaternion.identity,landscapeLeftRotation=Quaternion.identity;
        public ActionClip Find(AnimationLabPhoneAction role){foreach(var e in actions)if(e!=null&&e.role==role)return e;return null;}
        public ActionClip FindSource(string id){foreach(var e in actions)if(e!=null&&e.sourceId==id)return e;return null;}
        public bool HasActionSet {get{if(pocketPrefab==null||bothArmPaths.Length<upperPaths.Length||actions.Length!=8)return false;for(int i=0;i<8;i++){var e=Find((AnimationLabPhoneAction)i);if(e==null||e.clip==null||e.authored==null)return false;}return true;}}
        public static bool BothArms(AnimationLabPhoneAction role)=>role==AnimationLabPhoneAction.LandscapeEnter||role==AnimationLabPhoneAction.LandscapeExit||role==AnimationLabPhoneAction.LandscapeHold;
        public bool Valid=>rig!=null&&phonePrefab!=null&&idle!=null&&walk!=null&&upperBody!=null&&upperPaths!=null&&upperPaths.Length>0&&!string.IsNullOrEmpty(sourceId);
    }
}
