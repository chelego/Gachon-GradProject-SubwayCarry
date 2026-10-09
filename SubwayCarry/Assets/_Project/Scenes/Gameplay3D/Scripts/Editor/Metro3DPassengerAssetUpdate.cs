#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.Editor
{
    [InitializeOnLoad]
    public static class Metro3DPassengerAssetUpdate
    {
        const string Root="Assets/_Project/Scenes/Gameplay3D";
        static double next;
        static Metro3DPassengerAssetUpdate() {EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
            next=EditorApplication.timeSinceStartup+2;
            string folder=Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild"),request=Path.Combine(folder,"passenger-assets.request");if(!File.Exists(request)) return;
            File.Delete(request);
            try {UpdateAsset();File.WriteAllText(Path.Combine(folder,"passenger-assets.result"),"PASS: passenger mesh/materials updated; prefab GUID, existing controller and scene preserved.");}
            catch(Exception ex) {File.WriteAllText(Path.Combine(folder,"passenger-assets.result"),"FAILED: "+ex);Debug.LogException(ex);}
        }
        static Material Material(string name,Color color,float smoothness)
        {
            string path=Root+"/Art/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smoothness);material.enableInstancing=true;
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }
        static void UpdateAsset()
        {
            const string modelPath=Root+"/Art/Models/PassengerHuman.fbx",prefabPath=Root+"/Prefabs/Passenger3D.prefab";
            // Keep the FBX material names available. Unity may reorder submeshes
            // during import; Blender's numeric slot order is not a Unity contract.
            var modelImporter=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            modelImporter.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            modelImporter.materialLocation=ModelImporterMaterialLocation.InPrefab;
            // Keep the authored cycle endpoint instead of clipping the new gait
            // to the old 30-frame take. All clip names and IDs remain unchanged.
            var clipSettings=modelImporter.clipAnimations;
            foreach(var clip in clipSettings) {
                if(clip.name.EndsWith("|Walk")||clip.name=="Walk") {clip.firstFrame=0;clip.lastFrame=37;}
                if(clip.name.EndsWith("|Idle")||clip.name=="Idle") {clip.firstFrame=0;clip.lastFrame=90;}
            }
            modelImporter.clipAnimations=clipSettings;
            modelImporter.SaveAndReimport();
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root+"/Data/Passenger3D.controller");
            var clips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            foreach(string name in new[]{"Idle","Walk","SitDown","Sit","StandUp"}) if(!clips.Any(c=>c.name.EndsWith("|"+name)||c.name==name)) throw new InvalidOperationException("Missing preserved clip: "+name);
            var materials=new[]{Material("CommuterSkin",new Color(.72f,.57f,.46f),.32f),Material("CommuterCoat",new Color(.19f,.25f,.32f),.18f),Material("CommuterTrousers",new Color(.10f,.13f,.18f),.12f),Material("CommuterHair",new Color(.035f,.027f,.021f),.16f),Material("CommuterLips",new Color(.47f,.24f,.20f),.28f)};
            var eye=Material("CommuterSclera",new Color(.82f,.82f,.77f),.55f);var iris=Material("CommuterIris",new Color(.04f,.026f,.018f),.45f);var shoe=Material("CommuterShoes",Color.white,.28f);
            var undershirt=Material("CommuterUnderShirt",new Color(.025f,.028f,.032f),.12f);
            SetMaps(materials[0],"CommuterSkin_BaseColor");SetMaps(materials[1],"CommuterSuit_BaseColor","CommuterSuit_Normal","CommuterSuit_AO");SetMaps(materials[2],"CommuterSuit_BaseColor","CommuterSuit_Normal","CommuterSuit_AO");
            SetMaps(materials[3],"CommuterHair_BaseColor");SetMaps(shoe,"CommuterShoes_BaseColor");
            // These garments are thin fabric shells. A bent sleeve/hem exposes
            // its inner faces; unlike skin, both sides must remain visible.
            foreach(var cloth in new[]{materials[1],materials[2]}) {cloth.SetFloat("_Cull",0);EditorUtility.SetDirty(cloth);AssetDatabase.SaveAssetIfDirty(cloth);}
            materials[3].SetFloat("_AlphaClip",1);materials[3].SetFloat("_Cutoff",.35f);materials[3].SetFloat("_Cull",0);materials[3].EnableKeyword("_ALPHATEST_ON");materials[3].renderQueue=2450;EditorUtility.SetDirty(materials[3]);AssetDatabase.SaveAssetIfDirty(materials[3]);
            var byName=new[]{materials[0],materials[1],materials[2],materials[3],eye,iris,shoe,undershirt}.ToDictionary(m=>m.name);
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                var old=root.transform.Find("HumanModel");if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(imported,root.transform);model.name="HumanModel";
                var animator=model.GetComponent<Animator>();if(animator==null) animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(source=> {
                        if(source==null||!byName.TryGetValue(source.name,out var mapped)) throw new InvalidOperationException("Unknown source passenger material: "+(source==null?"null":source.name));
                        return mapped;
                    }).ToArray();
                    Debug.Log("Passenger imported slots: "+string.Join(", ",renderer.sharedMaterials.Select(m=>m.name)));
                    // The shared project currently runs Very Low / one-bone
                    // skinning. Do not change team quality settings; preserve
                    // this character's authored four-influence joint weights.
                    renderer.quality=SkinQuality.Bone4;
                    renderer.updateWhenOffscreen=false;
                    if(renderer.sharedMesh==null||renderer.sharedMesh.subMeshCount!=renderer.sharedMaterials.Length) throw new InvalidOperationException("Passenger material-slot contract mismatch: "+renderer.name);
                }
                var cc=root.GetComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            } finally {PrefabUtility.UnloadPrefabContents(root);}
            // References resolve by action name after replacing geometry, not by a new controller.
            var settings=AssetDatabase.LoadAssetAtPath<Metro3DSettings>(Root+"/Data/Metro3DSettings.asset");settings.idle=clips.Single(c=>c.name.EndsWith("|Idle"));settings.walk=clips.Single(c=>c.name.EndsWith("|Walk"));settings.sit=clips.Single(c=>c.name.EndsWith("|Sit"));EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
            foreach(var state in ((UnityEditor.Animations.AnimatorController)controller).layers[0].stateMachine.states) {
                string name=state.state.name;
                if(name=="Seated") state.state.motion=settings.sit;
                else if(name=="SitDown"||name=="StandUp") state.state.motion=clips.Single(c=>c.name.EndsWith("|"+name));
                else if(state.state.motion is UnityEditor.Animations.BlendTree tree) {
                    var children=tree.children;
                    foreach(int i in Enumerable.Range(0,children.Length)) {
                        bool resting=children[i].threshold==0;
                        children[i].motion=resting?settings.idle:settings.walk;
                    }
                    tree.children=children;EditorUtility.SetDirty(tree);AssetDatabase.SaveAssetIfDirty(tree);
                }
                EditorUtility.SetDirty(state.state);AssetDatabase.SaveAssetIfDirty(state.state);
            }
        }
        static void SetMaps(Material material,string color,string normal=null,string ao=null)
        {
            material.SetColor("_BaseColor",Color.white);
            foreach(string map in new[]{color,normal,ao}) {
                if(map==null) continue;
                string path=Root+"/Art/Textures/"+map+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer==null) throw new InvalidOperationException("Missing commuter texture: "+path);
                importer.textureType=map==normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=map==color;importer.maxTextureSize=2048;importer.alphaIsTransparency=map==color&&color.Contains("Hair");importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);string property=map==color?"_BaseMap":map==normal?"_BumpMap":"_OcclusionMap";
                material.SetTexture(property,texture);material.SetTextureScale(property,Vector2.one);
            }
            material.SetTexture("_MetallicGlossMap",null);material.DisableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Metallic",0);
            if(normal!=null) {material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.65f);}
            if(ao!=null) material.SetFloat("_OcclusionStrength",.35f);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        }
    }
}
#endif
