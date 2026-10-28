#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabPhoneBuild
    {
        public const string BindingPath=AnimationLabMotionAuthoring.Root+"/Data/Resources/PassengerPhoneReview.asset";
        public static void Batch()
        {
            Guard();try{Build();Debug.Log("PHONE_CASE_ASSETS_OK commonGenericRig=true ownedUpperBodyPose=true originalCC0Unchanged=true");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void Guard(){if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");}
        public static AnimationLabPhoneMotion Build()
        {
            var existing=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(BindingPath);if(existing!=null&&existing.Valid)return existing;
            EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");
            var window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.Begin();window.ForkForAuthor();var source=window.Draft;source.author="AI";source.clipName="걷기_폰보기_오른팔유지_상체";source.loop=true;
            var bones=AnimationLabMotionAuthoring.Bind(source,window.Rig);
            int Joint(HumanBodyBones human)=>Array.IndexOf(source.paths,source.humanPaths[(int)human]);
            int handIndex=Joint(HumanBodyBones.RightHand);var hand=bones[handIndex];var actor=window.Rig.transform;
            Vector3 phonePosition=actor.TransformPoint(new Vector3(.19f,1.19f,.32f));var head=bones[Joint(HumanBodyBones.Head)];
            Quaternion phoneRotation=Quaternion.LookRotation((head.position-phonePosition).normalized,actor.up);var up=phoneRotation*Vector3.up;var right=phoneRotation*Vector3.right;var normal=phoneRotation*Vector3.forward;
            Vector3 handTarget=phonePosition-up*.047f+right*.026f-normal*.011f;
            if(!window.MoveJointWorld(handIndex,handTarget))throw new InvalidOperationException("Phone hand target rejected");window.ResetPositionDrag();window.MoveJointWorld(Joint(HumanBodyBones.RightLowerArm),actor.TransformPoint(new Vector3(.29f,1.02f,.075f)));
            var middle=bones[Joint(HumanBodyBones.RightMiddleProximal)];var index=bones[Joint(HumanBodyBones.RightIndexProximal)];var little=bones[Joint(HumanBodyBones.RightLittleProximal)];
            Vector3 finger=(middle.position-hand.position).normalized,palm=Vector3.Cross(index.position-little.position,finger).normalized;
            Quaternion currentFrame=Quaternion.LookRotation(palm,finger),wantedFrame=Quaternion.LookRotation(normal,up);Quaternion wrist=(wantedFrame*Quaternion.Inverse(currentFrame))*hand.rotation;
            window.SetRotation(handIndex,hand.parent!=null?Quaternion.Inverse(hand.parent.rotation)*wrist:wrist);
            foreach(var fingerName in new[]{"Index","Middle","Ring","Little"})foreach(var segment in new[]{"Proximal","Intermediate","Distal"})
            {
                var human=(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),"Right"+fingerName+segment);int joint=Joint(human);if(joint<0||bones[joint].childCount==0)continue;var bone=bones[joint];
                Vector3 direction=(bone.GetChild(0).position-bone.position).normalized;Vector3 axis=bone.InverseTransformDirection(Vector3.Cross(direction,normal)).normalized;
                float curl=segment=="Proximal"?22:segment=="Intermediate"?48:32;window.SetRotation(joint,bone.localRotation*Quaternion.AngleAxis(curl,axis));
            }
            var thumb=bones[Joint(HumanBodyBones.RightThumbProximal)];window.SetRotation(Joint(HumanBodyBones.RightThumbProximal),thumb.localRotation*Quaternion.AngleAxis(18,Vector3.forward));
            var localPosition=hand.InverseTransformPoint(phonePosition);var localRotation=Quaternion.Inverse(hand.rotation)*phoneRotation;
            foreach(int frame in new[]{0,30,60}){window.Seek(frame);if(frame==0){}window.SaveKeyAdvance();}
            // The first save is a real constrained arm/hand pose, not an empty placeholder clip.
            window.SaveBundle();if(!window.Status.StartsWith("원본 +"))throw new InvalidOperationException(window.Status);var authored=window.SavedSource;
            var binding=ScriptableObject.CreateInstance<AnimationLabPhoneMotion>();binding.authored=authored;binding.sourceId=authored.id;binding.publishedRevision=AnimationLabMotionAuthoring.Revision(authored);
            binding.rig=source.sourceRig;binding.upperBody=authored.generatedClip;binding.rightHandPath=source.humanPaths[(int)HumanBodyBones.RightHand];binding.neckPath=source.humanPaths[(int)HumanBodyBones.Neck];binding.headPath=source.humanPaths[(int)HumanBodyBones.Head];
            string shoulder=source.humanPaths[(int)HumanBodyBones.RightShoulder];binding.upperPaths=source.paths.Where(p=>p==shoulder||p.StartsWith(shoulder+"/",StringComparison.Ordinal)).ToArray();
            var animator=window.Rig.GetComponentInChildren<Animator>();binding.animatorPath=animator!=null?AnimationUtility.CalculateTransformPath(animator.transform,window.Rig.transform):FindAnimatorPath(binding.rig);
            var clips=AssetDatabase.LoadAllAssetsAtPath(AnimationLabMotionAuthoring.SharedMotion).OfType<AnimationClip>().ToArray();binding.idle=clips.First(c=>c.name.EndsWith("|Idle_Loop"));binding.walk=clips.First(c=>c.name.EndsWith("|Walk_Loop"));binding.phoneLocalPosition=localPosition;binding.phoneLocalRotation=localRotation;
            var library=AssetDatabase.LoadAssetAtPath<AnimationLabLibrary>(AnimationLabMotionAuthoring.Root+"/Data/Library.asset");binding.font=library!=null?library.font:null;
            binding.phonePrefab=BuildPhone();AssetDatabase.CreateAsset(binding,BindingPath);AnimationLabPhonePublication.Publish(authored,authored.generatedClip,new[]{binding});window.Close();SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");return binding;
        }
        static string FindAnimatorPath(GameObject rig)=>AnimationUtility.CalculateTransformPath(rig.GetComponentInChildren<Animator>(true).transform,rig.transform);
        static GameObject BuildPhone()
        {
            string path=AnimationLabMotionAuthoring.Root+"/Prefabs/Phone.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            Material Mat(string name,Color color,float smooth){string p=AnimationLabMotionAuthoring.Root+"/Materials/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(p);if(value!=null)return value;value=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};value.SetColor("_BaseColor",color);value.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(value,p);return value;}
            var body=Mat("PhoneBody",new Color(.035f,.041f,.049f),.5f);var screen=Mat("PhoneScreen",new Color(.32f,.51f,.59f),.25f);var line=Mat("PhoneScreenText",new Color(.82f,.9f,.91f),.15f);
            var root=new GameObject("Phone");
            void Part(string name,Vector3 position,Vector3 size,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localScale=size;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;}
            try{Part("Body",Vector3.zero,new Vector3(.073f,.145f,.008f),body);Part("Screen",new Vector3(0,.002f,.0045f),new Vector3(.064f,.125f,.001f),screen);for(int i=0;i<4;i++)Part("Message "+i,new Vector3(i%2==0?-.006f:.004f,.04f-i*.023f,.0052f),new Vector3(.042f,.010f,.0008f),line);Part("Speaker",new Vector3(0,.067f,.0047f),new Vector3(.016f,.002f,.001f),line);return PrefabUtility.SaveAsPrefabAsset(root,path);}finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
#endif
