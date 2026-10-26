#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public sealed class AnimationLabMotionWindow : EditorWindow
    {
        const string Recovery="SubwayCarry.MotionAuthoring.Draft";
        [SerializeField] int frame,selected,advance=1,rangeStart,rangeEnd=60;
        [SerializeField] bool active,ghostOn=true,loop;
        [SerializeField] float opacity=.18f,ghostSpacing;
        [SerializeField] string sourcePath="",baseline="",search="";
        Motion draft,source;
        GameObject rig,ghost;
        Transform[] bones,ghostBones;
        Material ghostMaterial;
        RenderPipelineAsset previousPipeline,authoringPipeline;
        bool ownsPipeline;
        bool playing,ownsTools,previousTools;
        double playStarted;float playFrame;
        Vector2 scroll,jointScroll;
        string status="F1으로 제작 시작. 조작은 이 작업창 또는 Scene 뷰에서 합니다.";
        AnimationClip startingClip;
        float startingTime;
        [SerializeField] bool moveTool=true;
        struct Limb {public int upper,mid,end;public bool leg;public bool Valid=>upper>=0&&mid>=0&&end>=0;}
        Limb[] limbs;
        int positionDragJoint=-1;
        Vector3 positionDragPlane;
        Quaternion positionDragTip;
        Quaternion positionDragParent;
        Motion.Pose positionDragStart;
        Quaternion[] jointRest;
        Vector3[] jointAxis;
        bool sceneNavigating;
        int dragGroup=-1;
        public Motion Draft=>draft;
        internal Motion SavedSource=>source;
        public GameObject Rig=>rig;
        public int Frame=>frame;
        public string Status=>status;
        internal GameObject Ghost=>ghost;
        internal bool Previewing=>playing;
        internal float PreviewFrame=>playFrame;
        internal bool PositionTool=>moveTool;
        string BoneLabel(int i){int human=Array.IndexOf(draft.humanPaths,draft.paths[i]);return i==0?"ROOT · 위치/회전":human>=0?((HumanBodyBones)human)+" · "+bones[i].name:bones[i].name;}
        bool Context=>!EditorApplication.isPlayingOrWillChangePlaymode&&SceneManager.GetActiveScene().path==AnimationLabMotionAuthoring.ScenePath;
        [MenuItem("SubwayCarry/Animation/동작 제작 · MotionAuthoring")]
        public static void Open()=>GetWindow<AnimationLabMotionWindow>("동작 제작").Show();
        [UnityEditor.Callbacks.OnOpenAsset]
        public static bool OpenSavedAsset(int instanceId,int line)
        {
            var value=EditorUtility.InstanceIDToObject(instanceId) as Motion;if(value==null)return false;
            var panel=GetWindow<AnimationLabMotionWindow>("동작 제작");panel.Show();
            if(EditorApplication.isPlayingOrWillChangePlaymode||!panel.Context&&Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            {panel.status="Play 또는 저장하지 않은 씬이 있어 원본 열기를 보류했습니다. 씬은 변경하지 않았습니다.";panel.Repaint();return true;}
            if(!File.Exists(AnimationLabMotionAuthoring.ScenePath)){panel.status="MotionAuthoring 제작 씬이 없습니다.";panel.Repaint();return true;}
            if(panel.draft!=null&&JsonUtility.ToJson(panel.draft)!=JsonUtility.ToJson(value)&&!EditorUtility.DisplayDialog("저장 원본 열기","현재 미저장 초안을 선택한 저장 원본으로 바꿀까요? 초안을 보존하려면 취소 후 F2로 먼저 저장하세요.","저장 원본 열기","현재 초안 유지"))return true;
            if(!panel.Context)EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);
            panel.LoadSource(value);return true;
        }
        internal void SelectSaved(UnityEngine.Object value){if(value==null)return;Selection.activeObject=value;EditorGUIUtility.PingObject(value);}
        void OnEnable()
        {
            minSize=new Vector2(420,620);RestoreDraft();
            SceneView.duringSceneGui+=SceneGUI;EditorApplication.update+=Tick;Undo.undoRedoPerformed+=UndoRedo;AssemblyReloadEvents.beforeAssemblyReload+=Remember;EditorApplication.playModeStateChanged+=PlayChanged;
        }
        void OnDisable()
        {
            Remember();Detach();if(draft!=null)DestroyImmediate(draft);
            SceneView.duringSceneGui-=SceneGUI;EditorApplication.update-=Tick;Undo.undoRedoPerformed-=UndoRedo;AssemblyReloadEvents.beforeAssemblyReload-=Remember;EditorApplication.playModeStateChanged-=PlayChanged;
        }
        void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode){Remember();Detach();}}
        void RestoreDraft()
        {
            string json=SessionState.GetString(Recovery,"");if(json.Length==0)return;
            draft=CreateInstance<Motion>();draft.hideFlags=HideFlags.HideAndDontSave;JsonUtility.FromJsonOverwrite(json,draft);
            if(draft.sourceRig==null)draft.sourceRig=AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabMotionAuthoring.RigPath);
            sourcePath=SessionState.GetString(Recovery+".Path",sourcePath);baseline=SessionState.GetString(Recovery+".Baseline",baseline);source=AssetDatabase.LoadAssetAtPath<Motion>(sourcePath);
            string guid=SessionState.GetString(Recovery+".SourceGuid","");if(guid.Length>0){string moved=AssetDatabase.GUIDToAssetPath(guid);if(moved.Length>0){sourcePath=moved;source=AssetDatabase.LoadAssetAtPath<Motion>(moved);}}
            frame=Mathf.Clamp(SessionState.GetInt(Recovery+".Frame",frame),0,draft.lastFrame);
        }
        internal void Remember()
        {
            if(draft==null)return;SessionState.SetString(Recovery,JsonUtility.ToJson(draft));SessionState.SetString(Recovery+".Path",sourcePath);SessionState.SetString(Recovery+".Baseline",baseline);SessionState.SetInt(Recovery+".Frame",frame);
            SessionState.SetString(Recovery+".SourceGuid",AssetDatabase.AssetPathToGUID(sourcePath));
        }
        void Attach()
        {
            if(rig!=null||draft==null||!Context)return;
            try
            {
            AnimationLabMotionAuthoring.ResolveSharedRig(draft);
            authoringPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/_Project/Scenes/3D/Ingame/Game/Data/Pipeline3D.asset");previousPipeline=QualitySettings.renderPipeline;QualitySettings.renderPipeline=authoringPipeline;ownsPipeline=true;
            rig=AnimationLabMotionAuthoring.CreateRig(draft.sourceRig);AnimationLabMotionAuthoring.StripAnimators(rig);SceneManager.MoveGameObjectToScene(rig,SceneManager.GetActiveScene());bones=AnimationLabMotionAuthoring.Bind(draft,rig);
            limbs=new[]{MakeLimb(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,false),MakeLimb(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,false),MakeLimb(HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,true),MakeLimb(HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,true)};
            jointRest=new Quaternion[bones.Length];jointAxis=new Vector3[bones.Length];
            for(int i=0;i<bones.Length;i++){var reference=draft.paths[i].Length==0?draft.sourceRig.transform:draft.sourceRig.transform.Find(draft.paths[i]);jointRest[i]=reference!=null?reference.localRotation:draft.bindRotations[i];jointAxis[i]=reference!=null&&reference.childCount>0&&reference.GetChild(0).localPosition.sqrMagnitude>.0000001f?reference.GetChild(0).localPosition.normalized:Vector3.right;}
            ghost=AnimationLabMotionAuthoring.CreateRig(draft.sourceRig);AnimationLabMotionAuthoring.StripAnimators(ghost);SceneManager.MoveGameObjectToScene(ghost,SceneManager.GetActiveScene());ghostBones=AnimationLabMotionAuthoring.Bind(draft,ghost);
            ghost.name="Previous key · editor only";
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("URP Unlit 셰이더가 없습니다. 공용 재질은 변경하지 않습니다.");
            ghostMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=(int)RenderQueue.Transparent};ghostMaterial.SetFloat("_Surface",1);ghostMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);ghostMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);ghostMaterial.SetFloat("_ZWrite",0);ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            foreach(var t in ghostBones)t.gameObject.hideFlags|=HideFlags.NotEditable;
            foreach(var renderer in ghost.GetComponentsInChildren<Renderer>(true)){renderer.sharedMaterials=Enumerable.Repeat(ghostMaterial,renderer.sharedMaterials.Length).ToArray();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
            if(!ownsTools){previousTools=Tools.hidden;Tools.hidden=true;ownsTools=true;}
            ApplyFrame();
            }
            catch{Detach();throw;}
        }
        void Detach()
        {
            playing=false;sceneNavigating=false;ResetPositionDrag();limbs=null;if(rig!=null)DestroyImmediate(rig);if(ghost!=null)DestroyImmediate(ghost);if(ghostMaterial!=null)DestroyImmediate(ghostMaterial);rig=ghost=null;bones=ghostBones=null;
            if(ownsTools){Tools.hidden=previousTools;ownsTools=false;}
            if(ownsPipeline){if(QualitySettings.renderPipeline==authoringPipeline)QualitySettings.renderPipeline=previousPipeline;ownsPipeline=false;}
        }
        internal void Begin()
        {
            if(!Context){status="MotionAuthoring 씬을 열고 Play를 끈 상태에서 제작하세요.";return;}
            playing=false;active=true;
            if(draft==null){rig=AnimationLabMotionAuthoring.CreateRig();draft=AnimationLabMotionAuthoring.CreateSource(rig,startingClip,startingTime);DestroyImmediate(rig);rig=null;frame=0;source=null;sourcePath=baseline="";}
            bool first=rig==null;Attach();ApplyFrame();if(first)FocusRig();Remember();status="제작 중 · W 위치 드래그 / E 회전. 손목·발목은 팔다리가 따라 움직이고, 팔꿈치·무릎은 굽힘 방향을 조절합니다.";
        }
        internal void LoadSource(Motion value)
        {
            if(value==null)return;Remember();Detach();if(draft!=null)DestroyImmediate(draft);source=value;sourcePath=AssetDatabase.GetAssetPath(value);baseline=AnimationLabMotionAuthoring.Revision(value);draft=Instantiate(value);draft.hideFlags=HideFlags.HideAndDontSave;frame=0;rangeStart=0;rangeEnd=draft.lastFrame;selected=draft.hipsIndex;active=true;Attach();FocusRig();Remember();Repaint();
        }
        internal void ForkForAuthor()
        {
            if(draft==null)return;playing=false;Undo.ClearUndo(draft);draft.id=Guid.NewGuid().ToString("N");draft.author="";draft.generatedClip=null;draft.generatedFbx=null;source=null;sourcePath=baseline="";Changed();status="독립 사본 · 제작자 이름/고유 별칭을 입력한 뒤 F2로 저장하세요. 기존 원본과 출력은 유지됩니다.";
        }
        internal void Seek(int value)
        {if(draft==null)return;playing=false;ResetPositionDrag();frame=Mathf.Clamp(value,0,draft.lastFrame);ApplyFrame();Remember();Repaint();}
        internal void FocusRig(){var view=SceneView.lastActiveSceneView;if(view==null||rig==null)return;view.in2DMode=false;view.drawGizmos=false;view.LookAt(rig.transform.position+Vector3.up*.88f,Quaternion.Euler(10,155,0),1.2f);}
        void ApplyFrame(float at=-1)
        {
            if(rig==null||draft==null)return;AnimationLabMotionAuthoring.Apply(draft,bones,draft.Evaluate(at<0?frame:at,at<0));
            var previous=draft.keys.Where(p=>p.frame<(at<0?frame:at)).OrderByDescending(p=>p.frame).FirstOrDefault();
            bool visible=ghostOn&&previous!=null;ghost.SetActive(visible);
            if(visible){AnimationLabMotionAuthoring.Apply(draft,ghostBones,previous);ghost.transform.position+=Vector3.right*ghostSpacing;ghostMaterial.SetColor("_BaseColor",new Color(.25f,.75f,1,opacity));}
            SceneView.RepaintAll();
        }
        void Changed(){EditorUtility.SetDirty(draft);Remember();ApplyFrame();Repaint();}
        void UndoRedo(){if(draft==null)return;ResetPositionDrag();frame=Mathf.Clamp(frame,0,draft.lastFrame);Changed();}
        internal void SetRotation(int joint,Quaternion local)
        {
            if(draft==null||rig==null)return;playing=false;ResetPositionDrag();Undo.RecordObject(draft,"관절 자세 수정");var pose=draft.Evaluate(frame);pose.rotations[joint]=local.normalized;draft.StoreDraft(pose);Changed();
        }
        internal void SetPosition(int joint,Vector3 local)
        {
            if(draft==null||joint!=0&&joint!=draft.hipsIndex)return;playing=false;ResetPositionDrag();Undo.RecordObject(draft,"루트/골반 위치 수정");var pose=draft.Evaluate(frame);if(joint==0)pose.root=local;else pose.hips=local;draft.StoreDraft(pose);Changed();
        }
        int Joint(HumanBodyBones human){int h=(int)human;return draft!=null&&h<draft.humanPaths.Length&&!string.IsNullOrEmpty(draft.humanPaths[h])?Array.IndexOf(draft.paths,draft.humanPaths[h]):-1;}
        Limb MakeLimb(HumanBodyBones upper,HumanBodyBones mid,HumanBodyBones end,bool leg)=>new Limb{upper=Joint(upper),mid=Joint(mid),end=Joint(end),leg=leg};
        bool TryLimb(int joint,out Limb limb){if(limbs!=null)foreach(var value in limbs)if(value.Valid&&(joint==value.end||joint==value.mid)){limb=value;return true;}limb=default;return false;}
        bool TryShoulder(int joint,out int tip,out int clavicle)
        {
            foreach(bool left in new[]{true,false}){int shoulder=Joint(left?HumanBodyBones.LeftShoulder:HumanBodyBones.RightShoulder),arm=Joint(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);if(shoulder>=0&&arm>=0&&(joint==shoulder||joint==arm)){tip=arm;clavicle=shoulder;return true;}}tip=clavicle=-1;return false;
        }
        bool CanMove(int joint)=>draft!=null&&(joint==0||joint==draft.hipsIndex||TryLimb(joint,out _)||TryShoulder(joint,out _,out _));
        internal Vector3 MoveHandlePosition(int joint)=>TryShoulder(joint,out int tip,out _)?bones[tip].position:bones[joint].position;
        internal void ResetPositionDrag(){positionDragJoint=-1;positionDragStart=null;}
        void SelectJoint(HumanBodyBones human){int index=Joint(human);if(index<0)return;selected=index;moveTool=true;ResetPositionDrag();Repaint();SceneView.RepaintAll();}
        static void SwingTwist(Quaternion delta,Vector3 axis,out Quaternion swing,out float twistDegrees)
        {
            delta=delta.normalized;if(delta.w<0)delta=new Quaternion(-delta.x,-delta.y,-delta.z,-delta.w);var vector=new Vector3(delta.x,delta.y,delta.z);var projected=axis*Vector3.Dot(vector,axis);var twist=new Quaternion(projected.x,projected.y,projected.z,delta.w);
            if(Quaternion.Dot(twist,twist)<.00000001f)twist=Quaternion.identity;else twist=twist.normalized;swing=delta*Quaternion.Inverse(twist);twistDegrees=Mathf.DeltaAngle(0,2*Mathf.Atan2(Vector3.Dot(new Vector3(twist.x,twist.y,twist.z),axis),twist.w)*Mathf.Rad2Deg);
        }
        Quaternion BoundJoint(int joint,Quaternion local,float swingLimit,float twistLimit)
        {
            var rest=jointRest[joint];var axis=jointAxis[joint];SwingTwist(Quaternion.Inverse(rest)*local,axis,out var swing,out float twist);
            // Preserve an existing FK pose outside the conservative preset; do not snap unrelated poses.
            SwingTwist(Quaternion.Inverse(rest)*positionDragStart.rotations[joint],axis,out var initialSwing,out float initialTwist);
            float allowedSwing=Mathf.Max(swingLimit,Quaternion.Angle(Quaternion.identity,initialSwing)),allowedTwist=Mathf.Max(twistLimit,Mathf.Abs(initialTwist));
            return (rest*Quaternion.RotateTowards(Quaternion.identity,swing,allowedSwing)*Quaternion.AngleAxis(Mathf.Clamp(twist,-allowedTwist,allowedTwist),axis)).normalized;
        }
        void SolveShoulder(int tip,int clavicle,Vector3 target)
        {
            var joints=new[]{clavicle,Joint(HumanBodyBones.UpperChest),Joint(HumanBodyBones.Chest),Joint(HumanBodyBones.Spine)};var weights=new[]{.75f,.28f,.18f,.12f};
            for(int pass=0;pass<10;pass++)
            {
                if(Vector3.Distance(bones[tip].position,target)<.0005f)break;
                for(int i=0;i<joints.Length;i++)
                {
                    int index=joints[i];if(index<0)continue;var joint=bones[index];var from=bones[tip].position-joint.position;var to=target-joint.position;if(from.sqrMagnitude<.0000001f||to.sqrMagnitude<.0000001f)continue;
                    var world=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(from,to),weights[i])*joint.rotation;var local=joint.parent!=null?Quaternion.Inverse(joint.parent.rotation)*world:world;
                    joint.localRotation=BoundJoint(index,local,i==0?30:10,15);
                }
            }
        }
        void StorePositionPose()
        {
            var pose=positionDragStart.Copy(frame);for(int i=0;i<bones.Length;i++)pose.rotations[i]=bones[i].localRotation.normalized;pose.root=bones[0].localPosition;pose.hips=bones[draft.hipsIndex].localPosition;draft.StoreDraft(pose);playing=false;Changed();
        }
        internal bool MoveJointWorld(int joint,Vector3 desired)
        {
            if(draft==null||rig==null||joint<0||joint>=bones.Length||!float.IsFinite(desired.x+desired.y+desired.z))return false;
            if(joint==0||joint==draft.hipsIndex){SetPosition(joint,bones[joint].parent!=null?bones[joint].parent.InverseTransformPoint(desired):desired);return true;}
            if(TryShoulder(joint,out int shoulderTip,out int shoulderBone))
            {
                if(positionDragJoint!=joint||positionDragStart==null){positionDragJoint=joint;positionDragStart=draft.Evaluate(frame);}
                Undo.RecordObject(draft,"어깨·쇄골·윗몸통 연동 이동");AnimationLabMotionAuthoring.Apply(draft,bones,positionDragStart);SolveShoulder(shoulderTip,shoulderBone,desired);StorePositionPose();status=Vector3.Distance(bones[shoulderTip].position,desired)>.002f?"어깨 연동 이동 · 현재 관절 범위에서 제한됨":"어깨 연동 이동 · 쇄골과 윗몸통에 분산 · 골반/발 위치 유지";return true;
            }
            if(!TryLimb(joint,out var chain))return false;
            var upper=bones[chain.upper];var middle=bones[chain.mid];var tip=bones[chain.end];
            if(positionDragJoint!=joint||positionDragStart==null)
            {
                positionDragJoint=joint;positionDragStart=draft.Evaluate(frame);positionDragTip=tip.rotation;positionDragParent=upper.parent!=null?upper.parent.rotation:Quaternion.identity;
                var initialAxis=(tip.position-upper.position).normalized;positionDragPlane=Vector3.ProjectOnPlane(middle.position-upper.position,initialAxis);
                if(positionDragPlane.sqrMagnitude<.0000001f)positionDragPlane=Vector3.ProjectOnPlane(chain.leg?rig.transform.forward:-rig.transform.forward,initialAxis);
                if(positionDragPlane.sqrMagnitude<.0000001f)positionDragPlane=Vector3.ProjectOnPlane(rig.transform.right,initialAxis);positionDragPlane.Normalize();
            }
            Undo.RecordObject(draft,joint==chain.mid?"팔꿈치/무릎 굽힘 방향 수정":"손발·어깨 연동 위치 이동");AnimationLabMotionAuthoring.Apply(draft,bones,positionDragStart);
            if(!chain.leg&&joint==chain.end)
            {
                float reach=Vector3.Distance(upper.position,middle.position)+Vector3.Distance(middle.position,tip.position),excess=Vector3.Distance(desired,upper.position)-reach*.985f;
                int clavicle=Joint(chain.upper==Joint(HumanBodyBones.LeftUpperArm)?HumanBodyBones.LeftShoulder:HumanBodyBones.RightShoulder);
                if(excess>.001f&&clavicle>=0)SolveShoulder(chain.upper,clavicle,upper.position+(desired-upper.position).normalized*Mathf.Min(.15f,excess));
            }
            var origin=upper.position;
            float a=Vector3.Distance(origin,middle.position),b=Vector3.Distance(middle.position,tip.position);if(a<.00001f||b<.00001f)return false;
            var oldAxis=(tip.position-origin).normalized;
            var hintPlane=(upper.parent!=null?upper.parent.rotation:Quaternion.identity)*Quaternion.Inverse(positionDragParent)*positionDragPlane;
            bool pole=joint==chain.mid;var requested=pole?tip.position:desired;var offset=requested-origin;
            var axis=offset.sqrMagnitude>.00000001f?offset.normalized:oldAxis;if(axis.sqrMagnitude<.5f)axis=Vector3.down;
            // Conservative editing limits avoid folded/straight singularities; not a full anatomical model.
            float minimum=Mathf.Sqrt(Mathf.Max(.00000001f,a*a+b*b+2*a*b*Mathf.Cos(140*Mathf.Deg2Rad)));
            float maximum=Mathf.Sqrt(a*a+b*b+2*a*b*Mathf.Cos(2*Mathf.Deg2Rad));
            float distance=Mathf.Clamp(offset.magnitude,minimum,maximum);var goal=origin+axis*distance;
            var bend=Vector3.ProjectOnPlane(pole?desired-origin:hintPlane,axis);
            if(bend.sqrMagnitude<.0000001f)bend=Vector3.ProjectOnPlane(hintPlane,axis);
            if(bend.sqrMagnitude<.0000001f)bend=Vector3.ProjectOnPlane(rig.transform.up,axis);
            if(bend.sqrMagnitude<.0000001f)bend=Vector3.ProjectOnPlane(rig.transform.right,axis);bend.Normalize();
            var reference=Vector3.ProjectOnPlane(hintPlane,axis).normalized;
            if(pole&&reference.sqrMagnitude>.5f)bend=Vector3.RotateTowards(reference,bend,(chain.leg?85:150)*Mathf.Deg2Rad,0).normalized;
            // Align the complete bend plane first; keep the middle joint's bend in that plane.
            var oldBend=Vector3.ProjectOnPlane(middle.position-origin,oldAxis).normalized;
            var align=Quaternion.FromToRotation(oldAxis,axis);var alignedBend=Vector3.ProjectOnPlane(align*oldBend,axis).normalized;
            if(alignedBend.sqrMagnitude>.5f)align=Quaternion.AngleAxis(Vector3.SignedAngle(alignedBend,bend,axis),axis)*align;
            upper.rotation=align*upper.rotation;
            float along=(a*a-b*b+distance*distance)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));var elbow=origin+axis*along+bend*height;
            upper.rotation=Quaternion.FromToRotation(middle.position-origin,elbow-origin)*upper.rotation;
            middle.rotation=Quaternion.FromToRotation(tip.position-middle.position,goal-middle.position)*middle.rotation;
            if(chain.leg)tip.rotation=positionDragTip;else{upper.localRotation=BoundJoint(chain.upper,upper.localRotation,110,85);tip.localRotation=positionDragStart.rotations[chain.end];}
            StorePositionPose();status=Vector3.Distance(tip.position,requested)>.001f?"관절/도달 범위에서 제한 · 뼈 길이 고정":pole?"굽힘 방향 수정 · 손/발 위치 유지":"연동 이동 · 손은 팔을 따라 회전 / 발은 방향 유지";return true;
        }
        internal void SaveKeyAdvance()
        {
            if(draft==null||rig==null)return;playing=false;Undo.RecordObject(draft,"현재 프레임 키 저장");var pose=AnimationLabMotionAuthoring.Capture(draft,rig,frame);var prior=draft.Exact(draft.keys,frame);if(prior!=null)pose.ease=prior.ease;draft.StoreKey(pose);
            int next=Mathf.Min(draft.lastFrame,frame+Mathf.Max(1,advance));
            if(next!=frame&&draft.Exact(draft.keys,next)==null&&draft.Exact(draft.drafts,next)==null)draft.StoreDraft(pose.Copy(next));
            frame=next;Changed();status=next==draft.lastFrame?"키 저장 · 마지막 프레임":"키 저장 · 다음 프레임은 직전 자세에서 시작";
        }
        internal void SaveBundle()
        {
            if(draft==null)return;
            try{sourcePath=AnimationLabMotionAuthoring.SaveBundle(draft,ref source,baseline);baseline=AnimationLabMotionAuthoring.Revision(source);Remember();SelectSaved(source);status="원본 + .anim + FBX 왕복 검증 완료\n"+sourcePath+"\nUnity: "+AssetDatabase.GetAssetPath(source.generatedClip)+"\nFBX: "+AssetDatabase.GetAssetPath(source.generatedFbx)+"\n초안은 원본에만 보관 · 출력은 저장한 키 기준";}
            catch(Exception e){status=e.Message;if(status.StartsWith("원본/.anim 저장됨")&&source!=null){sourcePath=AssetDatabase.GetAssetPath(source);baseline=AnimationLabMotionAuthoring.Revision(source);Remember();}Debug.LogWarning(status);}
            Repaint();
        }
        internal void UpdateSceneNavigation(Event e)
        {
            if(e.rawType==EventType.MouseDown&&e.button==1)sceneNavigating=true;
            else if(e.rawType==EventType.MouseUp&&e.button==1||e.rawType==EventType.MouseMove||e.rawType==EventType.Ignore)sceneNavigating=false;
        }
        internal void HandleKey(Event e,bool fromScene=false)
        {
            if(fromScene&&(sceneNavigating||Tools.viewToolActive||GUIUtility.hotControl!=0))return;
            if(e.type!=EventType.KeyDown||!Context||EditorGUIUtility.editingTextField||e.alt||e.control||e.command)return;
            if(e.keyCode==KeyCode.F1){Begin();e.Use();return;}if(!active||draft==null)return;
            if(e.keyCode==KeyCode.W||e.keyCode==KeyCode.E){moveTool=e.keyCode==KeyCode.W;ResetPositionDrag();Repaint();SceneView.RepaintAll();e.Use();return;}
            switch(e.keyCode){case KeyCode.F2:SaveBundle();break;case KeyCode.F3:SaveKeyAdvance();break;case KeyCode.LeftArrow:Seek(frame-1);break;case KeyCode.RightArrow:Seek(frame+1);break;case KeyCode.Space:TogglePreview();break;default:return;}e.Use();
        }
        internal void TogglePreview(bool fromStart=false)
        {
            if(draft==null||rig==null)return;if(playing&&!fromStart){playing=false;Seek(Mathf.RoundToInt(playFrame));return;}
            rangeStart=Mathf.Clamp(rangeStart,0,draft.lastFrame);rangeEnd=Mathf.Clamp(rangeEnd,rangeStart+1,draft.lastFrame);playFrame=fromStart||frame>=rangeEnd?rangeStart:Mathf.Max(rangeStart,frame);playStarted=EditorApplication.timeSinceStartup-playFrame/draft.sampleRate;playing=true;
        }
        void Tick()
        {
            if(!Context){if(rig!=null)Detach();return;}if(active&&draft!=null&&rig==null)Attach();if(!playing||draft==null)return;
            playFrame=(float)(EditorApplication.timeSinceStartup-playStarted)*draft.sampleRate;
            if(playFrame>rangeEnd){if(loop){playStarted+=(rangeEnd-rangeStart)/(double)draft.sampleRate;playFrame=rangeStart+Mathf.Repeat(playFrame-rangeStart,rangeEnd-rangeStart);}else{playing=false;Seek(rangeEnd);return;}}
            ApplyFrame(playFrame);Repaint();
        }
        void OnGUI()
        {
            HandleKey(Event.current);scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("동작 제작 · 관절 편집 / 정수 프레임",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play 없이 Scene 뷰에서 편집합니다. W 위치 드래그 / E 회전. 손발 이동은 뼈 길이를 유지해 팔·다리 회전으로 저장합니다.",MessageType.None);
            if(!Context)
            {
                if(GUILayout.Button("MotionAuthoring 씬 열기"))
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))status="Play/저장하지 않은 씬이 있습니다. 사용자가 먼저 정리한 뒤 열어주세요.";
                    else if(File.Exists(AnimationLabMotionAuthoring.ScenePath))EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);
                    else status="제작 씬 파일이 아직 없습니다.";
                }
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("F1 시작/복귀"))Begin();
                using(new EditorGUI.DisabledScope(draft==null))if(GUILayout.Button("F2 저장"))SaveBundle();
                if(GUILayout.Button("새 원본")&&(draft==null||EditorUtility.DisplayDialog("새 동작","현재 초안은 F2 저장 후 새로 시작하세요. 현재 초안을 새 동작으로 교체할까요?","새로 만들기","유지"))){Detach();if(draft!=null)DestroyImmediate(draft);draft=null;source=null;sourcePath=baseline="";Begin();}
            }
            EditorGUI.BeginChangeCheck();var opened=(Motion)EditorGUILayout.ObjectField("제작 원본 열기",source,typeof(Motion),false);if(EditorGUI.EndChangeCheck()&&opened!=null&&(draft==null||EditorUtility.DisplayDialog("원본 열기","현재 초안을 선택한 원본으로 교체할까요?","열기","유지")))LoadSource(opened);
            using(new EditorGUI.DisabledScope(draft==null))if(GUILayout.Button("내 사본 만들기 · 기존 원본 유지"))ForkForAuthor();
            if(source!=null)
            {
                EditorGUILayout.LabelField("다시 편집: .asset 원본 더블클릭 · .anim / FBX는 재생용 출력",EditorStyles.wordWrappedMiniLabel);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("저장 원본 선택"))SelectSaved(source);
                    if(GUILayout.Button("Unity 클립 선택"))SelectSaved(source.generatedClip);
                    if(GUILayout.Button("FBX 선택"))SelectSaved(source.generatedFbx);
                    if(GUILayout.Button("저장 폴더"))EditorUtility.RevealInFinder(Path.GetFullPath(sourcePath));
                }
                EditorGUILayout.SelectableLabel(sourcePath,EditorStyles.wordWrappedMiniLabel,GUILayout.Height(32));
            }
            var example=AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabMotionAuthoring.ExamplePath);if(example!=null&&GUILayout.Button("서기 → 가벼운 짝다리 → 서기 예제 열기")&&(draft==null||EditorUtility.DisplayDialog("예제 열기","현재 초안을 예제로 교체할까요?","열기","유지")))LoadSource(example);
            startingClip=(AnimationClip)EditorGUILayout.ObjectField("시작 클립 (CC0/직접 제작)",startingClip,typeof(AnimationClip),false);startingTime=EditorGUILayout.FloatField("시작 클립의 초",Mathf.Max(0,startingTime));
            if(draft==null){EditorGUILayout.HelpBox(status,MessageType.Info);EditorGUILayout.EndScrollView();return;}
            moveTool=GUILayout.Toolbar(moveTool?0:1,new[]{"W 위치 드래그","E 회전"})==0;
            using(new EditorGUILayout.HorizontalScope())
            {if(GUILayout.Button("왼손"))SelectJoint(HumanBodyBones.LeftHand);if(GUILayout.Button("오른손"))SelectJoint(HumanBodyBones.RightHand);if(GUILayout.Button("왼발"))SelectJoint(HumanBodyBones.LeftFoot);if(GUILayout.Button("오른발"))SelectJoint(HumanBodyBones.RightFoot);if(GUILayout.Button("골반"))SelectJoint(HumanBodyBones.Hips);}
            using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("왼 어깨 · 몸통 연동"))SelectJoint(HumanBodyBones.LeftShoulder);if(GUILayout.Button("오른 어깨 · 몸통 연동"))SelectJoint(HumanBodyBones.RightShoulder);}
            EditorGUILayout.LabelField("기본 설정은 그대로 사용 · 저장할 때 클립 이름/제작자 입력",EditorStyles.wordWrappedMiniLabel);
            EditorGUI.BeginChangeCheck();string name=EditorGUILayout.TextField("클립 이름",draft.clipName),author=EditorGUILayout.TextField("제작자",draft.author);int fps=EditorGUILayout.IntField("샘플 FPS",draft.sampleRate),end=EditorGUILayout.IntField("마지막 프레임",draft.lastFrame);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(draft,"동작 시간/이름");draft.clipName=name;draft.author=author;draft.sampleRate=Mathf.Clamp(fps,1,120);draft.lastFrame=Mathf.Max(Mathf.Max(draft.keys.Count>0?draft.keys.Max(p=>p.frame):1,draft.drafts.Count>0?draft.drafts.Max(p=>p.frame):1),Mathf.Clamp(end,1,18000));frame=Mathf.Min(frame,draft.lastFrame);rangeEnd=Mathf.Min(rangeEnd,draft.lastFrame);Changed();}
            EditorGUILayout.LabelField(source!=null?"재저장: 기존 경로와 GUID 유지":"새 저장: Authored/<제작자>_<클립 이름>.asset · Exports에 같은 이름의 .anim/.fbx",EditorStyles.wordWrappedMiniLabel);
            float displayFrame=playing?playFrame:frame;
            EditorGUILayout.LabelField($"{displayFrame:F1} 프레임 / {displayFrame/draft.sampleRate:F3}초    전체 {draft.lastFrame}F / {draft.Duration:F3}초");
            EditorGUILayout.LabelField(draft.Exact(draft.drafts,frame)!=null?"● 수정 초안 · F3 전에는 출력 키가 아님":draft.Exact(draft.keys,frame)!=null?"◆ 저장된 키":"○ 보간/직전 키 자세 · 키 없음",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();int shown=Mathf.RoundToInt(displayFrame),seek=EditorGUILayout.IntSlider("프레임 이동",shown,0,draft.lastFrame);float seconds=EditorGUILayout.FloatField("시간으로 이동 (초)",displayFrame/draft.sampleRate);if(EditorGUI.EndChangeCheck())Seek(seek!=shown?seek:Mathf.RoundToInt(seconds*draft.sampleRate));
            using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button("← 이전 프레임"))Seek(frame-1);if(GUILayout.Button("다음 프레임 →"))Seek(frame+1);}
            advance=Mathf.Clamp(EditorGUILayout.IntField("F3 저장 후 이동 간격",advance),1,draft.lastFrame);
            if(GUILayout.Button("F3 현재 키 저장/갱신 → 다음"))SaveKeyAdvance();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("현재 키 삭제")){Undo.RecordObject(draft,"키 삭제");draft.keys.RemoveAll(p=>p.frame==frame);Changed();}
                if(GUILayout.Button("현재 초안 버리기")){Undo.RecordObject(draft,"프레임 초안 버리기");draft.drafts.RemoveAll(p=>p.frame==frame);Changed();}
                if(GUILayout.Button("Undo"))Undo.PerformUndo();if(GUILayout.Button("Redo"))Undo.PerformRedo();
            }
            var key=draft.Exact(draft.keys,frame);if(key!=null){EditorGUI.BeginChangeCheck();float ease=EditorGUILayout.Slider("다음 키까지 Ease",key.ease,0,1);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(draft,"키 보간 조절");key.ease=ease;Changed();}}
            EditorGUILayout.LabelField("저장 키 ◆  "+string.Join(" / ",draft.keys.Select(p=>p.frame))+"    초안 ●  "+string.Join(" / ",draft.drafts.Select(p=>p.frame)),EditorStyles.wordWrappedMiniLabel);
            using(new EditorGUILayout.HorizontalScope()){if(GUILayout.Button(playing?"Space 정지":"Space 미리보기 (키 기준)"))TogglePreview();if(GUILayout.Button("구간 처음부터"))TogglePreview(true);if(GUILayout.Button("전체 구간")){rangeStart=0;rangeEnd=draft.lastFrame;}}
            rangeStart=Mathf.Clamp(EditorGUILayout.IntField("재생 시작 프레임",rangeStart),0,draft.lastFrame-1);rangeEnd=Mathf.Clamp(EditorGUILayout.IntField("재생 끝 프레임",rangeEnd),rangeStart+1,draft.lastFrame);loop=EditorGUILayout.Toggle("구간 반복",loop);
            EditorGUI.BeginChangeCheck();ghostOn=EditorGUILayout.Toggle("직전 키 고스트",ghostOn);opacity=EditorGUILayout.Slider("고스트 투명도",opacity,.03f,.5f);ghostSpacing=EditorGUILayout.Slider("고스트 옆 간격 (m)",ghostSpacing,-.6f,.6f);if(EditorGUI.EndChangeCheck())ApplyFrame();
            if(rig!=null)
            {
                search=EditorGUILayout.TextField("본 이름 검색 (L/R·손가락 포함)",search);selected=Mathf.Clamp(selected,0,bones.Length-1);EditorGUILayout.LabelField("선택: "+(selected==0?"ROOT":draft.paths[selected]),EditorStyles.wordWrappedLabel);
                jointScroll=EditorGUILayout.BeginScrollView(jointScroll,GUILayout.Height(135));for(int i=0;i<bones.Length;i++)if(search.Length==0||(BoneLabel(i)+draft.paths[i]).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)if(GUILayout.Toggle(selected==i,BoneLabel(i),"Button"))selected=i;EditorGUILayout.EndScrollView();
                EditorGUI.BeginChangeCheck();var euler=EditorGUILayout.Vector3Field("관절 로컬 회전 °",bones[selected].localEulerAngles);if(EditorGUI.EndChangeCheck())SetRotation(selected,Quaternion.Euler(euler));
                if(selected==0||selected==draft.hipsIndex){EditorGUI.BeginChangeCheck();var position=EditorGUILayout.Vector3Field("루트/골반 로컬 위치 m",bones[selected].localPosition);if(EditorGUI.EndChangeCheck())SetPosition(selected,position);}
                else if(TryLimb(selected,out var selectedLimb))EditorGUILayout.LabelField(selected==selectedLimb.mid?"위치 드래그: 손발은 유지하고 굽힘 방향 조절":"위치 드래그: 팔·다리가 따라 움직임 · 뼈 길이 고정",EditorStyles.wordWrappedMiniLabel);
                else if(TryShoulder(selected,out _,out _))EditorGUILayout.LabelField("어깨 위치 드래그: 쇄골·윗몸통에 분산 · 공통 더미 기준의 제한 적용",EditorStyles.wordWrappedMiniLabel);
                else if(moveTool)EditorGUILayout.LabelField("이 관절은 회전 편집 · 위치 편집은 손목/발목/팔꿈치/무릎 또는 골반 선택",EditorStyles.wordWrappedMiniLabel);
                if(GUILayout.Button("Scene 뷰에서 캐릭터 보기"))FocusRig();
            }
            if(GUILayout.Button("F2 원본 + Unity .anim + FBX 저장/검증"))SaveBundle();
            EditorGUILayout.HelpBox(status,MessageType.Info);EditorGUILayout.LabelField("일반 보간 · 발/손 접촉과 무게중심은 직접 검수 필요 · F2는 로컬 저장이며 Git 업로드 아님",EditorStyles.wordWrappedMiniLabel);EditorGUILayout.EndScrollView();
        }
        void SceneGUI(SceneView view)
        {
            UpdateSceneNavigation(Event.current);HandleKey(Event.current,true);if(!Context||!active||draft==null||rig==null||playing)return;
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0){ResetPositionDrag();Undo.IncrementCurrentGroup();dragGroup=Undo.GetCurrentGroup();}
            var color=Handles.color;var depth=Handles.zTest;Handles.zTest=CompareFunction.Always;
            try
            {
                for(int i=0;i<bones.Length;i++)
                {
                    var bone=bones[i];if(bone.parent!=null){Handles.color=new Color(.2f,.8f,1,.35f);Handles.DrawLine(bone.parent.position,bone.position);}
                    Handles.color=i==selected?Color.yellow:Color.cyan;float size=HandleUtility.GetHandleSize(bone.position)*(i==selected?.045f:.022f);
                    if(Handles.Button(bone.position,Quaternion.identity,size,size,Handles.SphereHandleCap)){selected=i;ResetPositionDrag();Repaint();}
                }
                var current=bones[selected];var handlePosition=moveTool&&CanMove(selected)?MoveHandlePosition(selected):current.position;Handles.Label(handlePosition,(selected==0?"ROOT":current.name)+"  ["+frame+"F]");
                if(Vector3.Distance(handlePosition,current.position)>.001f){Handles.color=Color.yellow;Handles.DrawLine(current.position,handlePosition);}
                EditorGUI.BeginChangeCheck();
                if(moveTool&&CanMove(selected)){var world=Handles.PositionHandle(handlePosition,Quaternion.identity);if(EditorGUI.EndChangeCheck())MoveJointWorld(selected,world);}
                else{var rotation=Handles.RotationHandle(current.rotation,current.position);if(EditorGUI.EndChangeCheck())SetRotation(selected,current.parent!=null?Quaternion.Inverse(current.parent.rotation)*rotation:rotation);}
            }
            finally{Handles.color=color;Handles.zTest=depth;}
            if(Event.current.rawType==EventType.MouseUp&&dragGroup>=0){Undo.CollapseUndoOperations(dragGroup);dragGroup=-1;ResetPositionDrag();}
        }
    }
    [CustomEditor(typeof(Motion))]
    public sealed class AnimationLabAuthoredMotionInspector : global::UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var value=(Motion)target;
            EditorGUILayout.LabelField("모션 제작 원본",EditorStyles.boldLabel);
            EditorGUILayout.LabelField(value.clipName+" · "+value.sampleRate+" FPS · "+value.Duration.ToString("F2")+"초");
            EditorGUILayout.LabelField("저장 키: "+string.Join(" / ",value.keys.Select(k=>k.frame)),EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("미확정 초안: "+value.drafts.Count+"개 · 출력에는 F3로 저장한 키만 포함",EditorStyles.wordWrappedMiniLabel);
            if(GUILayout.Button("제작 창에서 열기 / 다시 편집"))AnimationLabMotionWindow.OpenSavedAsset(value.GetInstanceID(),0);
            using(new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Unity 재생 클립",value.generatedClip,typeof(AnimationClip),false);
                EditorGUILayout.ObjectField("FBX 출력",value.generatedFbx,typeof(GameObject),false);
            }
            EditorGUILayout.HelpBox("관절·키·초안 편집은 제작 창에서 합니다. 원본 .asset과 .meta를 함께 보존하세요.",MessageType.None);
        }
    }
}
#endif
