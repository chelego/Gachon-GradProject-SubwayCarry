#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.Editor
{
    [InitializeOnLoad]
    public static class Metro3DPassengerAssetUpdate
    {
        const string Root="Assets/_Project/Scenes/3D/Ingame/Game";
        public const string MannequinModel="Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
        const string Template="Assets/_Project/Scenes/3D/Test/AI/Assets/Prefabs/TestMannequin3D.prefab";
        static double next;
        static Metro3DPassengerAssetUpdate() {EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
            next=EditorApplication.timeSinceStartup+2;
            string folder=Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild"),request=Path.Combine(folder,"passenger-assets.request");if(!File.Exists(request)) return;
            File.Delete(request);
            try {UpdateAsset();File.WriteAllText(Path.Combine(folder,"passenger-assets.result"),"PASS: in-game mannequin visuals updated; passenger prefab/controller GUIDs and scenes preserved.");}
            catch(Exception ex) {File.WriteAllText(Path.Combine(folder,"passenger-assets.result"),"FAILED: "+ex);Debug.LogException(ex);}
        }

        public static AnimationClip Clip(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(MannequinModel).OfType<AnimationClip>()
                .Single(c=>!c.name.StartsWith("__preview__")&&(c.name==name||c.name.EndsWith("|"+name)));
        }

        // Reuse only the test visual. Its AI, physics body and controller stay in the test fields.
        [MenuItem("SubwayCarry/3D/Apply Test Mannequin To Passengers")]
        public static void UpdateAsset()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before changing passenger assets.");
            const string prefabPath=Root+"/Prefabs/Passenger3D.prefab";
            var template=AssetDatabase.LoadAssetAtPath<GameObject>(Template);
            var source=template!=null?template.GetComponentInChildren<Animator>(true):null;
            if(source==null||source.transform.parent.name!="AuthoredAxisCorrection") throw new InvalidOperationException("Existing test mannequin visual source missing.");
            var settings=AssetDatabase.LoadAssetAtPath<Metro3DSettings>(Root+"/Data/Metro3DSettings.asset");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Data/Passenger3D.controller");
            if(settings==null||controller==null) throw new InvalidOperationException("Existing passenger settings/controller missing.");
            var idle=Clip("Idle_Loop");var walk=Clip("Walk_Loop");var seated=Clip("Sitting_Idle_Loop");
            var down=Clip("Sitting_Enter");var up=Clip("Sitting_Exit");
            var states=controller.layers[0].stateMachine.states.Select(s=>s.state).ToArray();
            foreach(string required in new[]{"Locomotion","SitDown","Seated","StandUp"})
                if(!states.Any(s=>s.name==required)) throw new InvalidOperationException("Missing preserved passenger state: "+required);
            var locomotion=states.Single(s=>s.name=="Locomotion");
            if(!(locomotion.motion is BlendTree tree)) throw new InvalidOperationException("Existing locomotion blend tree missing.");
            var children=tree.children;
            if(children.Length!=2) throw new InvalidOperationException("Unexpected passenger locomotion contract.");
            for(int i=0;i<children.Length;i++) {
                bool resting=children[i].threshold==0;
                children[i].motion=resting?idle:walk;
                children[i].threshold=resting?0:.12f;
            }
            tree.children=children;EditorUtility.SetDirty(tree);AssetDatabase.SaveAssetIfDirty(tree);
            foreach(var state in states) {
                if(state.name=="Seated") state.motion=seated;
                if(state.name=="SitDown") {state.motion=down;state.speed=down.length/.9f;}
                if(state.name=="StandUp") {state.motion=up;state.speed=up.length/.9f;}
                EditorUtility.SetDirty(state);AssetDatabase.SaveAssetIfDirty(state);
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                if(root.GetComponent<CharacterController>()==null) throw new InvalidOperationException("Existing passenger body missing.");
                var old=root.transform.Find("HumanModel");if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var visual=UnityEngine.Object.Instantiate(source.transform.parent.gameObject,root.transform,false);
                visual.name="HumanModel";
                // The generic FBX walks along authored -Z. Preserve its import
                // axes, correcting only the visual wrapper to motor +Z. Native
                // pelvis/stance sampling rules out the former 155.3291deg yaw.
                visual.transform.localRotation=Quaternion.Euler(0,180,0);
                var animator=visual.GetComponentInChildren<Animator>(true);
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    if(renderer.sharedMesh==null||renderer.sharedMaterials.Any(m=>m==null)||renderer.sharedMesh.subMeshCount!=renderer.sharedMaterials.Length)
                        throw new InvalidOperationException("Mannequin material/mesh references missing: "+renderer.name);
                    renderer.quality=SkinQuality.Bone4;renderer.updateWhenOffscreen=false;
                }
                if(visual.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||visual.GetComponentInChildren<Collider>(true)!=null||visual.GetComponentInChildren<Rigidbody>(true)!=null)
                    throw new InvalidOperationException("Test gameplay components leaked into the passenger visual.");
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            } finally {PrefabUtility.UnloadPrefabContents(root);}
            settings.idle=idle;settings.walk=walk;settings.sit=seated;
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
        }
    }
}
#endif
