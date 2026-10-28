using System;
using MxM;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SubwayCarry.Gameplay3D
{
    // Pinned MxM 2.3.3 adapter. The DIY graph belongs to this component, not Motor.
    public sealed class Metro3DMotionPlayer : MxMAnimator
    {
        PlayableGraph graph;
        AnimationMixerPlayable mix;
        AnimationClipPlayable direct;
        double directClock;
        float directLength;
        bool directLoops;
#if UNITY_EDITOR
        System.Reflection.FieldInfo lastPoseCost,lastTrajectoryCost;
        public float LastSearchPoseCost=>lastPoseCost!=null?(float)lastPoseCost.GetValue(this):float.NaN;
        public float LastSearchTrajectoryCost=>lastTrajectoryCost!=null?(float)lastTrajectoryCost.GetValue(this):float.NaN;
#endif
        public AnimatorControllerPlayable Legacy {get;private set;}
        public bool Ready {get;private set;}
        bool disposed;
        protected override void Start() { }
        protected override void OnEnable(){if(Ready)base.OnEnable();}
        protected override void OnDisable(){if(Ready&&!disposed)base.OnDisable();}
        public void Boot(Animator target,MxMAnimData data)
        {
            p_DIYPlayableGraph=true;p_manualInitialize=false;
            AnimationRoot=target.transform;AnimData=new[]{data};RootMotion=EMxMRootMotion.Off;
            UpdateInterval=.1f;BlendTime=.18f;PriorityUpdate=true;BlendTrajectory=false;
            SetFavourCurrentPose(true,.85f);
            SetAngularErrorWarping(EAngularErrorWarp.Off);SetLongitudinalErrorWarping(ELongitudinalErrorWarp.None,new Vector2(.85f,1.15f));
            // MxM has no public setters for these Generic-inapplicable flags.
            // Validate the pinned fields once; no reflection in frame updates.
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
#if UNITY_EDITOR
            lastPoseCost=typeof(MxMAnimator).GetField("m_lastPoseCost",flags);lastTrajectoryCost=typeof(MxMAnimator).GetField("m_lastTrajectoryCost",flags);
#endif
            foreach(string name in new[]{"m_applyHumanoidFootIK","m_applyPlayableIK"}) {
                var field=typeof(MxMAnimator).GetField(name,flags);if(field==null)throw new InvalidOperationException("MxM API changed: "+name);field.SetValue(this,false);
            }
            // The query is the actually rendered pose/velocity. Use the basic
            // position+velocity distance, not an additional resultant-velocity
            // prediction derived from an already corrected/rendered pose.
            var method=typeof(MxMAnimator).GetField("m_poseMatchMethod",flags);
            if(method==null)throw new InvalidOperationException("MxM API changed: pose match method");
            method.SetValue(this,EPoseMatchMethod.Basic);
            graph=PlayableGraph.Create("Passenger one-person motion trial");graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var motion=CreateMotionMatchingPlayable(ref graph);
            Legacy=AnimatorControllerPlayable.Create(graph,target.runtimeAnimatorController);
            mix=AnimationMixerPlayable.Create(graph,3);graph.Connect(Legacy,0,mix,0);graph.Connect(motion,0,mix,1);
            mix.SetInputWeight(0,1);mix.SetInputWeight(1,0);
            var output=AnimationPlayableOutput.Create(graph,"Passenger pose",target);output.SetSourcePlayable(mix);
            target.applyRootMotion=false;graph.Play();Ready=true;
            MxMSearchManager.Instance.RegisterMxMAnimator(this);
        }
        public void SyncLegacy(float speed,float gait,bool seated){if(!Ready)return;Legacy.SetFloat("Speed",speed);Legacy.SetFloat("Gait",gait);Legacy.SetBool("Seated",seated);Legacy.SetSpeed(p_animator.speed);}
        public void SetWeight(float value){if(!Ready)return;mix.SetInputWeight(0,1-value);mix.SetInputWeight(1,value);}
        public void SetDirectClip(AnimationClip clip)
        {
            if(direct.IsValid()){graph.Disconnect(mix,2);graph.DestroyPlayable(direct);}
            directClock=0;
            if(clip!=null){directLength=clip.length;directLoops=clip.isLooping;direct=AnimationClipPlayable.Create(graph,clip);direct.SetApplyFootIK(false);direct.SetApplyPlayableIK(false);graph.Connect(direct,0,mix,2);direct.SetSpeed(0);direct.SetTime(0);direct.Play();}
            mix.SetInputWeight(2,clip!=null?1:0);
        }
        public float DirectTime=>direct.IsValid()?(float)direct.GetTime():0;
        public void SeekDirect(float time){if(!direct.IsValid())return;directClock=Mathf.Clamp(time,0,directLength);direct.SetTime(directClock);graph.Evaluate(0);}
        public void AdvanceDirect(float dt){if(!direct.IsValid())return;directClock+=dt;double time=directLoops?directClock%directLength:Math.Min(directClock,directLength);direct.SetTime(time);graph.Evaluate(0);}
        public void SetDirectWeight(bool value){if(value){mix.SetInputWeight(0,0);mix.SetInputWeight(1,0);}mix.SetInputWeight(2,value?1:0);}
        // Animation-lab local freeze: camera remains usable, Time.timeScale and
        // the game session are not changed. No effect unless explicitly called.
        public void SetGraphPaused(bool value){if(graph.IsValid())graph.SetTimeUpdateMode(value?DirectorUpdateMode.Manual:DirectorUpdateMode.GameTime);}
        public void PreparePose(){if(graph.IsValid())graph.Evaluate(0);}
        public void Shutdown()
        {
            if(disposed)return;disposed=true;
            if(Ready){base.OnDisable();base.OnDestroy();Ready=false;}
            if(graph.IsValid())graph.Destroy();
        }
        protected override void OnDestroy(){Shutdown();}
    }
}
