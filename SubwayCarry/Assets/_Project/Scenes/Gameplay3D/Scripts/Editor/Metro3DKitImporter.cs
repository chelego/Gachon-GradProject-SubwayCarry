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
            foreach(var suffix in new[]{"White","Green","Wood","Blue","Seat","Brown","Luminous","Ballast","Floor","Cardboard"}) {
                string name="Metro_"+suffix,textureRoot=root+"/Art/Textures/Metro/";
                foreach(var channel in new[]{"BaseColor","Normal","Roughness","Metallic","AO","MetallicSmoothness"}) {
                    string path=textureRoot+name+"_"+channel+".png";
                    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                    if(importer==null) throw new InvalidOperationException("Missing Blender map: "+path);
                    bool normal=channel=="Normal",srgb=channel=="BaseColor";
                    if(importer.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default)||importer.sRGBTexture!=srgb) {
                        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                        importer.sRGBTexture=srgb; importer.mipmapEnabled=true; importer.wrapMode=TextureWrapMode.Repeat; importer.anisoLevel=4;
                        importer.SaveAndReimport();
                    }
                }
                string matPath=root+"/Art/Materials/Metro/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mat==null) {mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,matPath);}
                mat.SetColor("_BaseColor",Color.white); mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_BaseColor.png"));
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_Normal.png")); mat.SetFloat("_BumpScale",.22f); mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_MetallicSmoothness.png")); mat.SetFloat("_Smoothness",1); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+name+"_AO.png"));
                if(suffix=="Luminous") {mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",new Color(.87f,.93f,1)*2);}
                mat.enableInstancing=true; EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat); result.Add(name,mat);
            }
            foreach(string path in Directory.GetFiles(root+"/Art/Materials/Circulation","*.mat")) {
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\','/')); result.Add(mat.name,mat);
            }
            foreach(var mapping in new[]{"Tile:Circulation_CreamTile","Stone:Metro_Floor","Metal:Circulation_Steel","Glass:Circulation_SafetyGlass","Yellow:Circulation_Yellow","Rubber:Circulation_Rubber","Red:Circulation_StopRed","Seat:Metro_Seat","Ceiling:Metro_White","Luminous:Metro_Luminous","Box:Metro_Cardboard","Wood:Metro_Wood","Green:Metro_Green","Brown:Metro_Brown","Blue:Metro_Blue","White:Metro_White"}) {
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
