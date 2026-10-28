#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabPhoneGripChecks
    {
        public static void Fit()
        {
            AnimationLabPhoneBuild.Guard();GameObject rig=null;
            try
            {
                var data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);rig=AnimationLabMotionAuthoring.CreateRig(data.rig);AnimationLabMotionAuthoring.StripAnimators(rig);var source=data.authored;
                AnimationLabMotionAuthoring.Apply(source,AnimationLabMotionAuthoring.Bind(source,rig),source.Evaluate(0,false));
                Transform Bone(HumanBodyBones human)=>rig.transform.Find(source.humanPaths[(int)human]);
                var hand=Bone(HumanBodyBones.RightHand);var index=Bone(HumanBodyBones.RightIndexProximal);var middle=Bone(HumanBodyBones.RightMiddleProximal);var little=Bone(HumanBodyBones.RightLittleProximal);
                Vector3 row=(index.position+middle.position+little.position)/3,up=(row-hand.position).normalized,across=(index.position-little.position).normalized;
                Vector3 normal=Vector3.Cross(across,up).normalized;if(Vector3.Dot(normal,Bone(HumanBodyBones.Head).position-row)<0)normal=-normal;
                // The palm frame is measured after joint constraints. A requested wrist orientation can be clamped.
                Vector3 center=Vector3.Lerp(hand.position,row,.65f)+up*.019f+normal*.016f;
                data.phoneLocalPosition=hand.InverseTransformPoint(center);data.phoneLocalRotation=Quaternion.Inverse(hand.rotation)*Quaternion.LookRotation(normal,up);
                EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
                Debug.Log("PHONE_GRIP_FIT palmLength="+Vector3.Distance(row,hand.position)+" offset="+data.phoneLocalPosition+" normalToHead="+Vector3.Dot(normal,(Bone(HumanBodyBones.Head).position-center).normalized));EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
            finally{if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);}
        }
    }
}
#endif
