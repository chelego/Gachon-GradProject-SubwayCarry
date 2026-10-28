#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabAuthoringField
    {
        public const string Example=AnimationLabMotionAuthoring.Authored+"/WeightShift_Example.asset";
        public static void Prepare()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            try{Build();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void Build()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            if(!System.IO.File.Exists(AnimationLabMotionAuthoring.ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SceneManager.SetActiveScene(scene);
                var root=new GameObject("MotionAuthoring");var scope=root.AddComponent<Metro3DRenderScope>();scope.pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/_Project/Scenes/3D/Ingame/Game/Data/Pipeline3D.asset");
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Authoring stage";floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(8,.1f,8);floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(AnimationLabMotionAuthoring.Root+"/Materials/Floor.mat");
                var key=new GameObject("Authoring key").AddComponent<Light>();key.transform.SetParent(root.transform);key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(45,-35,0);key.shadows=LightShadows.Soft;
                var fill=new GameObject("Authoring fill").AddComponent<Light>();fill.transform.SetParent(root.transform);fill.type=LightType.Directional;fill.intensity=.5f;fill.transform.rotation=Quaternion.Euler(25,145,0);
                var camera=new GameObject("Authoring camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.SetParent(root.transform);camera.transform.position=new Vector3(2.5f,1.5f,-3.5f);camera.transform.LookAt(Vector3.up*.9f);camera.fieldOfView=35;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.17f,.20f);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.57f,.6f);RenderSettings.fog=false;
                EditorSceneManager.SaveScene(scene,AnimationLabMotionAuthoring.ScenePath);
            }
            var rig=AnimationLabMotionAuthoring.CreateRig();var data=AnimationLabMotionAuthoring.CreateSource(rig);data.clipName="WeightShift_Example";data.author="SubwayCarry · example";
            try
            {
                var bones=AnimationLabMotionAuthoring.Bind(data,rig);
                Transform H(HumanBodyBones h)=>rig.transform.Find(data.humanPaths[(int)h]);
                var lf=H(HumanBodyBones.LeftFoot);var rf=H(HumanBodyBones.RightFoot);var left=lf.position;var right=rf.position;var lrot=lf.rotation;var rrot=rf.rotation;
                var pelvis=bones[data.hipsIndex];pelvis.position+=new Vector3(.035f,-.008f,0);
                SolveLeg(H(HumanBodyBones.LeftUpperLeg),H(HumanBodyBones.LeftLowerLeg),lf,left,lrot);
                SolveLeg(H(HumanBodyBones.RightUpperLeg),H(HumanBodyBones.RightLowerLeg),rf,right,rrot);
                var mid=AnimationLabMotionAuthoring.Capture(data,rig,30);data.StoreKey(mid);data.StoreKey(data.keys[0].Copy(60));
                float contact=0;for(int f=0;f<=60;f++){AnimationLabMotionAuthoring.Apply(data,bones,data.Evaluate(f,false));contact=Mathf.Max(contact,Vector3.Distance(lf.position,left),Vector3.Distance(rf.position,right));}
                if(contact>.003f)throw new InvalidOperationException("Example foot support drift "+contact);
                Motion source=AssetDatabase.LoadAssetAtPath<Motion>(Example);string revision=source!=null?AnimationLabMotionAuthoring.Revision(source):"";
                if(source!=null){data.id=source.id;data.generatedClip=source.generatedClip;data.generatedFbx=source.generatedFbx;}
                string path=AnimationLabMotionAuthoring.SaveBundle(data,ref source,revision);
                Debug.Log("AUTHORING_FIELD_READY "+path+" supportDrift="+contact+" skeleton="+data.paths.Length);
            }
            finally{UnityEngine.Object.DestroyImmediate(rig);UnityEngine.Object.DestroyImmediate(data);}
        }
        static void SolveLeg(Transform upper,Transform knee,Transform foot,Vector3 target,Quaternion rotation)
        {
            // Bake three supported FK example keys, not a second live pose/IK owner.
            Vector3 origin=upper.position,oldKnee=knee.position;float a=Vector3.Distance(origin,oldKnee),b=Vector3.Distance(oldKnee,foot.position);Vector3 axis=(target-origin).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(target,origin),Mathf.Abs(a-b)+.00001f,a+b-.00001f);var pole=Vector3.ProjectOnPlane(oldKnee-origin,axis).normalized;if(pole.sqrMagnitude<.5f)throw new InvalidOperationException("Ambiguous knee plane");
            float along=(a*a+distance*distance-b*b)/(2*distance);var desired=origin+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(oldKnee-origin,desired-origin)*upper.rotation;knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,target-knee.position)*knee.rotation;foot.rotation=rotation;
        }
    }
}
#endif
