#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.Editor
{
    public static class Metro3DKitImporter
    {
        // Source-authored Blender maps only. Does not generate replacement textures in Unity.
        public static Dictionary<string,Material> Prepare()
        {
            Metro3DCirculationImporter.Prepare();
            string root=Metro3DBuilder.Root;
            Directory.CreateDirectory(root+"/Art/Materials/Metro");
            var result=new Dictionary<string,Material>();
            foreach(var suffix in new[]{"White","Green","Wood","Blue","Seat","Brown","Luminous","Ballast","Floor","Cardboard","ConcourseFloor","CeramicWall","ConcourseTile","CeramicColumn","AccentTile","BlackStone","LightDiffuser"}) {
                string name="Metro_"+suffix,textureRoot=root+"/Art/Textures/Metro/";
                foreach(var channel in new[]{"BaseColor","Normal","Roughness","Metallic","AO","MetallicSmoothness","Height"}) {
                    string path=textureRoot+name+"_"+channel+".png";
                    if(channel=="Height"&&!File.Exists(path)) continue;
                    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                    if(importer==null) throw new InvalidOperationException("Missing Blender map: "+path);
                    bool normal=channel=="Normal",srgb=channel=="BaseColor";
                    int resolution=suffix=="Floor"||suffix=="ConcourseFloor"?2048:1024;
                    if(importer.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default)||importer.sRGBTexture!=srgb||importer.maxTextureSize!=resolution||importer.anisoLevel!=8||!importer.mipmapEnabled) {
                        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                        importer.sRGBTexture=srgb; importer.mipmapEnabled=true; importer.wrapMode=TextureWrapMode.Repeat; importer.anisoLevel=8;
                        importer.maxTextureSize=resolution; importer.filterMode=FilterMode.Trilinear;
                        importer.SaveAndReimport();
                    }
                }
                string matPath=root+"/Art/Materials/Metro/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                // This material currently has user edits (emission disabled); do not overwrite them.
                if(suffix=="Luminous"&&mat!=null) {result.Add(name,mat);continue;}
                if(mat==null) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,matPath);}
                mat.SetColor("_BaseColor",Color.white); mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_BaseColor.png"));
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_Normal.png")); mat.SetFloat("_BumpScale",suffix=="Floor"||suffix=="ConcourseFloor"||suffix.StartsWith("Ceramic")||suffix=="ConcourseTile"||suffix=="AccentTile"?.7f:.22f); mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_MetallicSmoothness.png")); mat.SetFloat("_Smoothness",1); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_AO.png"));
                mat.EnableKeyword("_OCCLUSIONMAP");
                if(suffix=="Luminous"||suffix=="LightDiffuser") {
                    mat.SetColor("_EmissionColor",(suffix=="Luminous"?new Color(.87f,.93f,1):new Color(.84f,.89f,1))*2);
                    if(suffix=="Luminous") mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.EnableKeyword("_EMISSION");
                }
                mat.enableInstancing=true; EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat); result.Add(name,mat);
            }
            foreach(string path in Directory.GetFiles(root+"/Art/Materials/Circulation","*.mat")) {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\','/')); result.Add(mat.name,mat);
            }
            // Preserve the edited legacy glass; use a distinct, correctly blended 3D presentation material.
            string glassPath=root+"/Art/Materials/Metro/Metro_StationGlass.mat";
            var glass=AssetDatabase.LoadAssetAtPath<Material>(glassPath);
            if(glass==null) {glass=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(glass,glassPath);}
            glass.SetColor("_BaseColor",new Color(.88f,.93f,.94f,.065f)); glass.SetFloat("_Metallic",0); glass.SetFloat("_Smoothness",.94f);
            glass.SetFloat("_Surface",1); glass.SetFloat("_Blend",0); glass.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); glass.SetFloat("_ZWrite",0); glass.SetFloat("_Cull",0);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.SetOverrideTag("RenderType","Transparent"); glass.renderQueue=3000; glass.enableInstancing=true;
            EditorUtility.SetDirty(glass); AssetDatabase.SaveAssetIfDirty(glass);
            result["Circulation_SafetyGlass"]=glass;
            foreach(var mapping in new[]{"Tile:Metro_CeramicWall","Stone:Metro_Floor","Metal:Circulation_Steel","Glass:Circulation_SafetyGlass","Yellow:Circulation_Yellow","Rubber:Circulation_Rubber","Red:Circulation_StopRed","Seat:Metro_Seat","Ceiling:Metro_White","Luminous:Metro_Luminous","Box:Metro_Cardboard","Wood:Metro_Wood","Green:Metro_Green","Brown:Metro_Brown","Blue:Metro_Blue","White:Metro_White"}) {
                var pair=mapping.Split(':'); result.Add(pair[0],result[pair[1]]);
            }
            string hdrPath=root+"/Art/Textures/Metro/IndoorReflection.hdr";
            var hdr=AssetImporter.GetAtPath(hdrPath) as TextureImporter;
            if(hdr==null) throw new InvalidOperationException("Blender indoor reflection source missing.");
            if(hdr.textureShape!=TextureImporterShape.TextureCube) {
                hdr.textureShape=TextureImporterShape.TextureCube; hdr.generateCubemap=TextureImporterGenerateCubemap.AutoCubemap;
                hdr.sRGBTexture=false; hdr.mipmapEnabled=true; hdr.textureCompression=TextureImporterCompression.Uncompressed;
                hdr.maxTextureSize=512; hdr.SaveAndReimport();
            }
            return result;
        }
    }
}
#endif
