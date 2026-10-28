#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PoseClip=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabPoseProfile.Clip;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public sealed class AnimationLabPoseWindow : EditorWindow
    {
        public const string ProfilePath="Assets/_Project/Scenes/3D/Test/Animation/Data/WalkPoseProfile.asset";
        public const string NormalProfilePath="Assets/_Project/Scenes/3D/Test/Animation/Data/Resources/NormalPoseProfile.asset";
        public const string ScenePath="Assets/_Project/Scenes/3D/Test/Animation/Normal.unity";
        const string SessionKey="SubwayCarry.WalkPose.Draft";
        [SerializeField] PoseClip clip;
        [SerializeField] float normalizedTime;
        [SerializeField] int jointIndex=4;
        [SerializeField] bool paused=true,showSource;
        [SerializeField] bool normalMode;
        AnimationLabScenario normal;
        string ActivePath=>normalMode?NormalProfilePath:ProfilePath;
        string ActiveKey=>SessionKey+(normalMode?".Normal":"");
        AnimationLabPoseProfile asset,draft;
        AnimationLabWalkReview review;
        bool needsBinding=true,ownsTools,previousToolsHidden;
        double nextBinding,nextPaint;
        Vector2 scroll;
        internal AnimationLabPoseProfile Draft=>draft;
        internal AnimationLabWalkReview Review=>review;

        [MenuItem("SubwayCarry/Animation/Normal · 기존 Walk 자세 조절")]
        public static void Open(){var w=GetWindow<AnimationLabPoseWindow>("Walk 자세 조절");w.SelectMode(false);w.Show();}
        [MenuItem("SubwayCarry/Animation/Normal · 현재 보행 자세 조절")]
        public static void OpenNormal(){var w=GetWindow<AnimationLabPoseWindow>("Normal 자세 조절");w.SelectMode(true);w.Show();}
        void SelectMode(bool value)
        {
            if(normalMode==value)return;
            RememberDraft();Detach();if(draft!=null)DestroyImmediate(draft);
            normalMode=value;asset=AssetDatabase.LoadAssetAtPath<AnimationLabPoseProfile>(ActivePath);LoadDraft();needsBinding=true;
        }
        void OnEnable()
        {
            minSize=new Vector2(380,500);
            saveChangesMessage="Walk 자세 보정에 저장하지 않은 변경이 있습니다. 프로필에 저장할까요? 씬과 원본 애니메이션은 저장하지 않습니다.";
            asset=AssetDatabase.LoadAssetAtPath<AnimationLabPoseProfile>(ActivePath);
            LoadDraft();
            SceneView.duringSceneGui+=SceneGui;Undo.undoRedoPerformed+=OnUndoRedo;
            EditorApplication.update+=EditorTick;EditorApplication.playModeStateChanged+=PlayChanged;
            EditorApplication.hierarchyChanged+=HierarchyChanged;
        }
        void OnDisable()
        {
            PreserveDraftBeforeReload();Detach();
            SceneView.duringSceneGui-=SceneGui;Undo.undoRedoPerformed-=OnUndoRedo;
            EditorApplication.update-=EditorTick;EditorApplication.playModeStateChanged-=PlayChanged;
            EditorApplication.hierarchyChanged-=HierarchyChanged;
            if(draft!=null)DestroyImmediate(draft);
        }
        void LoadDraft()
        {
            if(asset==null)return;
            draft=CreateInstance<AnimationLabPoseProfile>();draft.hideFlags=HideFlags.HideAndDontSave;
            string json=SessionState.GetString(ActiveKey+".Json","");
            bool same=SessionState.GetString(ActiveKey+".Guid","")==AssetDatabase.AssetPathToGUID(ActivePath);
            JsonUtility.FromJsonOverwrite(same&&!string.IsNullOrEmpty(json)?json:JsonUtility.ToJson(asset),draft);
            UpdateDirty();
        }
        void RememberDraft()
        {
            if(asset==null||draft==null)return;
            SessionState.SetString(ActiveKey+".Guid",AssetDatabase.AssetPathToGUID(ActivePath));
            SessionState.SetString(ActiveKey+".Json",JsonUtility.ToJson(draft));
        }
        internal void PreserveDraftBeforeReload(){RememberDraft();hasUnsavedChanges=false;}
        void UpdateDirty(){hasUnsavedChanges=asset!=null&&draft!=null&&JsonUtility.ToJson(asset)!=JsonUtility.ToJson(draft);}
        void Changed(){EditorUtility.SetDirty(draft);RememberDraft();UpdateDirty();review?.EvaluatePoseNow();normal?.RefreshPose();Repaint();SceneView.RepaintAll();}
        void OnUndoRedo(){if(draft==null)return;Changed();}
        void HierarchyChanged(){needsBinding=true;}
        void PlayChanged(PlayModeStateChange state)
        {
            RememberDraft();
            if(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.EnteredEditMode)Detach();
            needsBinding=true;
        }
        internal void EditorTick()
        {
            if(!EditorApplication.isPlaying){if(review!=null)Detach();return;}
            if(normalMode)
            {
                if(normal==null&&EditorApplication.timeSinceStartup>=nextBinding)
                {
                    nextBinding=EditorApplication.timeSinceStartup+.5;
                    foreach(var candidate in FindObjectsByType<AnimationLabScenario>(FindObjectsSortMode.None))
                        if(candidate.gameObject.scene.path==ScenePath&&candidate.enabled){normal=candidate;normal.PoseDraft=draft;break;}
                }
                if(normal!=null&&EditorApplication.timeSinceStartup>=nextPaint){nextPaint=EditorApplication.timeSinceStartup+1.0/30;Repaint();}
                return;
            }
            if(review!=null&&!review.PoseReviewActive){Detach();needsBinding=true;}
            if((needsBinding||review==null)&&EditorApplication.timeSinceStartup>=nextBinding)
            {
                nextBinding=EditorApplication.timeSinceStartup+.5;needsBinding=false;
                foreach(var candidate in FindObjectsByType<AnimationLabWalkReview>(FindObjectsSortMode.None))
                {
                    if(candidate.gameObject.scene.path==ScenePath&&candidate.manual!=null&&candidate.manual.field=="Walk"&&candidate.PoseReviewActive){Attach(candidate);break;}
                }
            }
            if(review!=null&&review.PoseEditing)
            {
                normalizedTime=review.SelectedNormalizedTime;paused=review.manual.Frozen;
                if(EditorApplication.timeSinceStartup>=nextPaint){nextPaint=EditorApplication.timeSinceStartup+1.0/30;Repaint();SceneView.RepaintAll();}
            }
        }
        internal void Attach(AnimationLabWalkReview target)
        {
            if(target==review&&target.PoseEditing)return;
            Detach();if(target==null||draft==null||target.gameObject.scene.path!=ScenePath||!target.PoseReviewActive)return;
            review=target;review.BeginPoseEditing(draft,clip,normalizedTime,paused);review.ShowPoseSource(showSource);
            previousToolsHidden=Tools.hidden;ownsTools=true;Tools.hidden=true;
        }
        void Detach()
        {
            if(normal!=null){normal.PoseDraft=null;normal.RefreshPose();normal=null;}
            if(review!=null)review.EndPoseEditing();review=null;
            if(ownsTools){Tools.hidden=previousToolsHidden;ownsTools=false;}
        }
        public override void SaveChanges()
        {
            if(asset==null||draft==null)return;
            // Field JSON excludes the draft's transient HideFlags; save only this asset, never the scene.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(draft),asset);
            EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
            RememberDraft();UpdateDirty();base.SaveChanges();
        }
        public override void DiscardChanges()
        {
            if(asset!=null&&draft!=null){Undo.RecordObject(draft,"저장한 Walk 자세 복구");JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(asset),draft);Changed();}
            base.DiscardChanges();
        }
        internal void SelectClip(PoseClip value,float time=0)
        {
            clip=value;normalizedTime=Mathf.Clamp01(time);paused=true;
            review?.SeekPose(clip,normalizedTime);RememberDraft();Repaint();SceneView.RepaintAll();
        }
        internal void SetRotation(int index,Quaternion value)
        {
            if(draft==null||index<0||index>=AnimationLabPoseProfile.Bones.Length)return;
            Undo.RecordObject(draft,"Walk 관절 미세 회전");draft.For(clip).Set(index,value);Changed();
        }
        internal void SetStrength(float value)
        {Undo.RecordObject(draft,"Walk 보정 강도");draft.For(clip).strength=Mathf.Clamp01(value);Changed();}
        internal void SetCorrectionEnabled(bool value)
        {Undo.RecordObject(draft,"Walk 보정 비교");draft.For(clip).enabled=value;Changed();}
        internal void ResetJoint(int index){SetRotation(index,Quaternion.identity);}
        internal void ResetClip(){Undo.RecordObject(draft,"현재 클립 관절 원복");draft.For(clip).Reset();Changed();}
        internal void ApplyWorldRotation(int index,Quaternion world)
        {
            if(review==null&&normal==null||draft==null)return;var bone=Joint(index);var pose=draft.For(clip);
            if(bone==null||!pose.enabled||pose.strength<.001f)return;
            Quaternion local=bone.parent!=null?Quaternion.Inverse(bone.parent.rotation)*world:world;
            Quaternion applied=Quaternion.Inverse(normal!=null?normal.BaseJointRotation(index):review.BaseJointRotation(index))*local;
            applied.ToAngleAxis(out float angle,out Vector3 axis);if(angle>180)angle-=360;
            SetRotation(index,Mathf.Abs(angle)<.00001f?Quaternion.identity:Quaternion.AngleAxis(Mathf.Clamp(angle/pose.strength,-179.9f,179.9f),axis));
        }
        void OnGUI()
        {
            if(asset==null||draft==null){EditorGUILayout.HelpBox("WalkPoseProfile.asset을 찾을 수 없습니다. 다른 캐릭터나 프로젝트는 선택하지 않습니다.",MessageType.Info);return;}
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(normalMode?"Normal 모델 보정 · FK 회전":"Walk 상체 자세 · FK 회전",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField("저장 프로필",asset,typeof(AnimationLabPoseProfile),false);
            EditorGUILayout.LabelField(hasUnsavedChanges?"● 저장하지 않은 보정 (종료 시 확인)":"저장한 보정과 동일",EditorStyles.miniLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("프로필 저장"))SaveChanges();
                if(GUILayout.Button("저장한 값으로"))DiscardChanges();
                if(GUILayout.Button("Undo",GUILayout.Width(50)))Undo.PerformUndo();
                if(GUILayout.Button("Redo",GUILayout.Width(50)))Undo.PerformRedo();
            }
            EditorGUILayout.Space(8);
            if(normalMode)
            {
                if(normal==null)EditorGUILayout.HelpBox("Normal을 직접 Play하면 현재 Scenario 출력에 연결됩니다. 기존 WalkPoseProfile과 별도이며 원본 클립은 변경하지 않습니다.",MessageType.Info);
                int selected=GUILayout.Toolbar((int)clip,new[]{"서기 보정","보행 · 출발 · 전환 보정"});if(selected!=(int)clip){clip=(PoseClip)selected;Repaint();}
                var row=draft.For(clip);
                EditorGUI.BeginChangeCheck();bool active=EditorGUILayout.Toggle("보정 ON",row.enabled);if(EditorGUI.EndChangeCheck())SetCorrectionEnabled(active);
                EditorGUI.BeginChangeCheck();float strength=EditorGUILayout.Slider("보정 강도",row.strength,0,1);if(EditorGUI.EndChangeCheck())SetStrength(strength);
                if(normal!=null&&GUILayout.Button(normal.Actor.Frozen?"재생":"정지 후 관절 조절"))normal.Actor.SetFrozen(!normal.Actor.Frozen);
                EditorGUILayout.HelpBox("Scene 뷰에서 관절 선택 → 회전 고리. 원래 팔 스윙 위에 작은 로컬 회전만 적용합니다. 서기/보행 보정은 실제 전환 가중치로 연결됩니다.",MessageType.None);
                for(int i=0;i<AnimationLabPoseProfile.Bones.Length;i++)if(GUILayout.Toggle(jointIndex==i,AnimationLabPoseProfile.Labels[i],"Button"))jointIndex=i;
                if(GUILayout.Button("선택 관절 원복"))ResetJoint(jointIndex);
                if(GUILayout.Button("현재 보정 전체 원복"))ResetClip();
                EditorGUILayout.EndScrollView();return;
            }
            if(review==null||!review.PoseReviewActive)
            {
                EditorGUILayout.HelpBox("Normal 씬에서 직접 Play한 뒤 기존 Kubold Idle/Walk 보정 모드를 선택하세요. Mixamo에는 이 프로필을 적용하지 않습니다.\nPlay 종료 후에도 미저장 초안은 이 편집기 세션에 남습니다.",MessageType.Info);
                if(EditorApplication.isPlaying&&GUILayout.Button("기존 Idle/Walk FK 보정 모드"))
                    foreach(var candidate in FindObjectsByType<AnimationLabWalkReview>(FindObjectsSortMode.None))if(candidate.gameObject.scene.path==ScenePath){candidate.EnterLegacyPoseReview();needsBinding=true;break;}
                EditorGUILayout.EndScrollView();return;
            }
            int choice=GUILayout.Toolbar((int)clip,new[]{"서기 (Idle)","걷기 (Walk)"});if(choice!=(int)clip)SelectClip((PoseClip)choice);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(paused?"재생":"정지")){paused=!paused;review.manual.SetFrozen(paused);}
                if(GUILayout.Button("선택 자세 처음으로"))SelectClip(clip);
            }
            EditorGUI.BeginChangeCheck();float time=EditorGUILayout.Slider("재생 위치",normalizedTime,0,1);
            if(EditorGUI.EndChangeCheck())SelectClip(clip,time);
            EditorGUI.BeginChangeCheck();bool source=EditorGUILayout.Toggle("원본 A0 표시",showSource);
            if(EditorGUI.EndChangeCheck()){showSource=source;review.ShowPoseSource(source);}
            var pose=draft.For(clip);
            EditorGUI.BeginChangeCheck();bool on=EditorGUILayout.Toggle("현재 클립 보정 ON",pose.enabled);
            if(EditorGUI.EndChangeCheck())SetCorrectionEnabled(on);
            EditorGUI.BeginChangeCheck();float weight=EditorGUILayout.Slider("보정 강도",pose.strength,0,1);
            if(EditorGUI.EndChangeCheck())SetStrength(weight);
            EditorGUILayout.Space(8);EditorGUILayout.LabelField("Scene 뷰에서 관절 점 클릭 → 회전 고리 드래그",EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("먼저 정지 후 조절하세요. 현재 클립 전체에 같은 로컬 회전값을 적용하므로 재생하며 확인하세요. Idle/Walk는 별도이며 손 위치 이동·IK는 지원하지 않습니다.",MessageType.None);
            for(int i=0;i<AnimationLabPoseProfile.Bones.Length;i++)
            {
                bool exists=review.PoseJoint(i)!=null;
                using(new EditorGUI.DisabledScope(!exists))
                    if(GUILayout.Toggle(jointIndex==i,AnimationLabPoseProfile.Labels[i]+(exists?"":" (본 없음)"),"Button"))jointIndex=i;
            }
            EditorGUILayout.LabelField("회전 손잡이: 선택 관절의 현재 로컬 축",EditorStyles.miniLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("선택 관절 원복"))ResetJoint(jointIndex);
                if(GUILayout.Button("현재 클립 전체 원복"))ResetClip();
            }
            EditorGUILayout.EndScrollView();
        }
        void SceneGui(SceneView scene)
        {
            if((normal==null&&(review==null||!review.PoseEditing))||draft==null)return;
            var oldColor=Handles.color;var oldDepth=Handles.zTest;Handles.zTest=CompareFunction.Always;
            try
            {
                for(int i=0;i<AnimationLabPoseProfile.Bones.Length;i++)
                {
                    var bone=Joint(i);if(bone==null)continue;
                    Handles.color=i==jointIndex?Color.yellow:Color.cyan;
                    float size=HandleUtility.GetHandleSize(bone.position)*.065f;
                    if(Handles.Button(bone.position,Quaternion.identity,size,size,Handles.SphereHandleCap)){jointIndex=i;Repaint();}
                }
                var selected=Joint(jointIndex);if(selected==null)return;
                Handles.Label(selected.position,AnimationLabPoseProfile.Labels[jointIndex]);
                var pose=draft.For(clip);if(!pose.enabled||pose.strength<.001f)return;
                EditorGUI.BeginChangeCheck();var rotation=Handles.RotationHandle(selected.rotation,selected.position);
                if(EditorGUI.EndChangeCheck())ApplyWorldRotation(jointIndex,rotation);
            }
            finally{Handles.color=oldColor;Handles.zTest=oldDepth;}
        }
        Transform Joint(int index)=>normal!=null?normal.PoseJoint(index):review?.PoseJoint(index);
    }
}
#endif
