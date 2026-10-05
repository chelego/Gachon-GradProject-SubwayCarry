#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D.Editor
{
    // Asset-only conversion of Blender's PBR sources. Does not add anything to a map/library.
    [InitializeOnLoad]
    public static class Metro3DCirculationImporter
    {
        static double nextPoll;
        static string Folder=>Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild");
        static string TextureRoot=>Metro3DBuilder.Root+"/Art/Textures/Circulation/";
        static Metro3DCirculationImporter() { EditorApplication.update-=Poll; EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll=EditorApplication.timeSinceStartup+3;
            string request=Path.Combine(Folder,"circulation-import.request");
            if(!File.Exists(request)) return;
            File.Delete(request);
            try { Prepare(); File.WriteAllText(Path.Combine(Folder,"circulation-import.result"),"PASS: Blender circulation PBR materials and two asset-only prefabs prepared; no gameplay scene or environment library changed."); }
            catch(Exception ex) { File.WriteAllText(Path.Combine(Folder,"circulation-import.result"),"FAILED: "+ex); Debug.LogException(ex); }
        }

        [MenuItem("SubwayCarry/3D/Prepare Blender Circulation Prefabs (No Map Changes)")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Metro3DBuilder.Root+"/Art/Materials/Circulation");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var materials=new Dictionary<string,Material>();
            foreach(var suffix in new[]{"Granite","CreamTile","Steel","Aluminium","Rubber","Yellow","StopRed"}) {
                string name="Circulation_"+suffix;
                foreach(var channel in new[]{"BaseColor","Normal","Roughness","Metallic","AO","MetallicSmoothness"}) {
                    string path=TextureRoot+name+"_"+channel+".png";
                    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                    if(importer==null) throw new InvalidOperationException("Missing Blender PBR source: "+path);
                    bool normal=channel=="Normal",srgb=channel=="BaseColor";
                    var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                    if(importer.textureType!=type||importer.sRGBTexture!=srgb||importer.wrapMode!=TextureWrapMode.Repeat) {
                        importer.textureType=type; importer.sRGBTexture=srgb; importer.wrapMode=TextureWrapMode.Repeat;
                        importer.mipmapEnabled=true; importer.anisoLevel=4; importer.SaveAndReimport();
                    }
                }
                string materialPath=Metro3DBuilder.Root+"/Art/Materials/Circulation/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(mat==null) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,materialPath);}
                mat.SetColor("_BaseColor",Color.white);
                mat.SetTexture("_BaseMap",Load(name,"BaseColor"));
                mat.SetTexture("_BumpMap",Load(name,"Normal")); mat.SetFloat("_BumpScale",.22f); mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap",Load(name,"MetallicSmoothness")); mat.SetFloat("_Smoothness",1f); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_OcclusionMap",Load(name,"AO")); mat.SetFloat("_OcclusionStrength",1f);
                mat.enableInstancing=true;
                EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat); materials.Add(name,mat);
            }
            string glassName="Circulation_SafetyGlass",glassPath=Metro3DBuilder.Root+"/Art/Materials/Circulation/"+glassName+".mat";
            var glass=AssetDatabase.LoadAssetAtPath<Material>(glassPath);
            if(glass==null) {glass=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(glass,glassPath);}
            glass.SetColor("_BaseColor",new Color(.9f,.96f,.96f,.10f)); glass.SetFloat("_Metallic",0); glass.SetFloat("_Smoothness",.94f);
            glass.SetFloat("_Surface",1); glass.SetFloat("_Blend",0); glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha); glass.SetFloat("_ZWrite",0); glass.SetFloat("_Cull",(float)CullMode.Off);
            glass.SetOverrideTag("RenderType","Transparent"); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue=(int)RenderQueue.Transparent;
            glass.enableInstancing=true;
            EditorUtility.SetDirty(glass); AssetDatabase.SaveAssetIfDirty(glass); materials.Add(glassName,glass);
            foreach(var name in new[]{"StationStaircase","StationEscalator"}) CreatePrefab(name,materials);
        }

        static Texture2D Load(string name,string channel)=>AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot+name+"_"+channel+".png");

        static void CreatePrefab(string name,Dictionary<string,Material> materials)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Metro3DBuilder.Root+"/Art/Models/"+name+".fbx");
            if(model==null) throw new InvalidOperationException("Missing Blender model: "+name);
            var preview=EditorSceneManager.NewPreviewScene();
            try {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(model,preview); go.name=name;
                foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>()) {
                    var mapped=renderer.sharedMaterials;
                    for(int i=0;i<mapped.Length;i++) {
                        string source=mapped[i]!=null?mapped[i].name:"";
                        if(!materials.TryGetValue(source,out var material)) throw new InvalidOperationException(name+" unmapped Blender material: "+source);
                        mapped[i]=material;
                    }
                    renderer.sharedMaterials=mapped;
                    if(mapped.Length==1&&mapped[0].name=="Circulation_SafetyGlass") renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                // Geometry/material prefab only. Colliders, moving band and map integration are later gates.
                if(PrefabUtility.SaveAsPrefabAsset(go,Metro3DBuilder.Root+"/Prefabs/"+name+".prefab")==null)
                    throw new IOException("Unable to save asset-only prefab: "+name);
            } finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
#endif
