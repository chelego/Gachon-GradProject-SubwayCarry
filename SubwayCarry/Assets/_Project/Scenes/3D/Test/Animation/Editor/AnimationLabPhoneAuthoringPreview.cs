#if UNITY_EDITOR
using System;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Editor-only props. The approved rig, bone paths and authoring keys are never modified by this preview.
    public sealed class AnimationLabPhoneAuthoringPreview : IDisposable
    {
        AnimationLabPhoneMotion data;AnimationLabPhoneMotion.ActionClip entry;AnimationLabAuthoredMotion source;
        GameObject phone,pocket,previewRoot;Transform hand,hips;
        public GameObject Phone=>phone;
        public string Description=>data.FindSqueezeSource(source.id)!=null?"사람 사이 통과 · 전신 / ROOT 이동은 Motor 소유":entry!=null?entry.displayName+" · "+(AnimationLabPhoneMotion.BothArms(entry.role)?"양팔":"오른팔/손")+" · 다음 Play 적용":"폰 보기 · 오른팔/손 · 다음 Play 적용";
        public static AnimationLabPhoneAuthoringPreview Create(AnimationLabAuthoredMotion source,GameObject rig)
        {
            var bindings=AnimationLabPhonePublication.Targets(source);if(bindings.Length!=1)return null;var data=bindings[0];if(data.phonePrefab==null)return null;
            var value=new AnimationLabPhoneAuthoringPreview{data=data,entry=data.FindSource(source.id),source=source,hand=rig.transform.Find(data.rightHandPath)};
            if(value.hand==null)return null;
            value.previewRoot=new GameObject("Phone props · editor only"){hideFlags=HideFlags.HideAndDontSave};value.hips=string.IsNullOrEmpty(data.hipsPath)?null:rig.transform.Find(data.hipsPath);
            if(data.pocketPrefab!=null){value.pocket=UnityEngine.Object.Instantiate(data.pocketPrefab,value.previewRoot.transform,false);value.pocket.hideFlags=HideFlags.DontSave;}
            value.phone=UnityEngine.Object.Instantiate(data.phonePrefab,value.previewRoot.transform,false);value.phone.hideFlags=HideFlags.DontSave;value.Apply(0);return value;
        }
        public void Apply(float frame)
        {
            if(phone==null)return;float n=frame/Mathf.Max(1,source.lastFrame),blend=0;bool inHand=data.FindSqueezeSource(source.id)==null;
            if(entry!=null)
            {
                if(entry.role==AnimationLabPhoneAction.TakeOut)inHand=frame>=entry.contactFrame;
                else if(entry.role==AnimationLabPhoneAction.PutAway)inHand=frame<entry.contactFrame;
                if(entry.role==AnimationLabPhoneAction.LandscapeEnter)blend=Mathf.SmoothStep(0,1,n);
                else if(entry.role==AnimationLabPhoneAction.LandscapeExit)blend=1-Mathf.SmoothStep(0,1,n);
                else if(entry.role==AnimationLabPhoneAction.LandscapeHold)blend=1;
            }
            if(pocket!=null&&hips!=null){pocket.transform.SetPositionAndRotation(hips.TransformPoint(data.pocketLocalPosition),hips.rotation*data.pocketLocalRotation);}
            var parent=!inHand&&pocket!=null?pocket.transform:hand;
            phone.transform.SetPositionAndRotation(parent.TransformPoint(inHand?Vector3.Lerp(data.phoneLocalPosition,data.landscapePhonePosition,blend):data.pocketPhonePosition),parent.rotation*(inHand?Quaternion.Slerp(data.phoneLocalRotation,data.landscapePhoneRotation,blend):data.pocketPhoneRotation));
        }
        public void Dispose(){if(previewRoot!=null)UnityEngine.Object.DestroyImmediate(previewRoot);phone=pocket=previewRoot=null;}
    }
}
#endif
