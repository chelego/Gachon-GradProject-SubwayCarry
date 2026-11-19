using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    internal enum Metro3DRiderLook { Office = 0, Senior = 1, Child = 2, Student = 3 }

    internal sealed class Metro3DRiderWardrobe
    {
        const string ResourceFolder = "PassengerWardrobe/";
        static readonly string[] Styles = { "Office", "Senior", "Child", "Student", "OfficeShirt", "StudentJacket" };
        static readonly Color[] ClothTints =
        {
            Color.white, new Color(.91f, .94f, 1f), new Color(1f, .95f, .89f), new Color(.87f, .93f, .91f)
        };

        sealed class Variant
        {
            internal readonly Mesh[] Meshes = new Mesh[3];
            internal readonly Material[][] Near = new Material[4][];
            internal readonly Material[][] Distant = new Material[4][];
            internal string[] Bones;
        }

        internal readonly Metro3DRocketboxCast PublisherCast=new Metro3DRocketboxCast();
        readonly Variant[] variants = new Variant[Styles.Length];
        readonly bool[] loaded = new bool[Styles.Length];
        readonly List<Material> ownedMaterials = new List<Material>();

        internal void Dispose()
        {
            foreach (var material in ownedMaterials)
                UnityEngine.Object.Destroy(material);
            ownedMaterials.Clear();
            PublisherCast.Dispose();
        }

        internal bool Apply(SkinnedMeshRenderer skin, Transform rig, Metro3DRiderLook look,
            Dictionary<string, Transform> rigBones, ref Transform[] mappedBones, int appearanceSeed)
        {
            var lod = skin.GetComponent<Metro3DWardrobeLod>();
            if (lod != null) lod.Clear();
            int index = (int)look;
            if (index < 0 || index >= 4) return false;

            int palette = appearanceSeed & 3;
            int style = index;
            if (palette == 3)
            {
                if (look == Metro3DRiderLook.Office) style = 4;
                if (look == Metro3DRiderLook.Student) style = 5;
            }
            var data = LoadVariant(style, look, skin);
            if (data == null && style != index)
                data = LoadVariant(index, look, skin);
            if (data == null) return false;

            if (rigBones.Count == 0)
                foreach (var bone in rig.GetComponentsInChildren<Transform>(true))
                    if (!rigBones.ContainsKey(bone.name)) rigBones.Add(bone.name, bone);
            if (mappedBones == null || mappedBones.Length != data.Bones.Length)
                mappedBones = new Transform[data.Bones.Length];
            for (int i = 0; i < mappedBones.Length; i++)
                if (!rigBones.TryGetValue(data.Bones[i], out mappedBones[i])) return false;

            skin.bones = mappedBones;
            if (lod == null) lod = skin.gameObject.AddComponent<Metro3DWardrobeLod>();
            lod.Configure(skin, data.Meshes, data.Near[palette], data.Distant[palette]);
            return true;
        }

        Variant LoadVariant(int index, Metro3DRiderLook look, SkinnedMeshRenderer skin)
        {
            if (loaded[index]) return variants[index];
            loaded[index] = true;
            string style = Styles[index];
            var prefab = Resources.Load<GameObject>(ResourceFolder + style + "Passenger");
            if (prefab == null) return null;
            var data = new Variant();
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int level = 0; level < data.Meshes.Length; level++)
            {
                var source = Array.Find(renderers, item => item.name.EndsWith("_LOD" + level, StringComparison.Ordinal));
                if (!CompatibleBinding(source, skin) || source.sharedMesh.subMeshCount != (level == 0 ? 5 : 2))
                {
                    Debug.LogError("Passenger wardrobe bind space or material slots differ from the preserved rig: " + style);
                    return null;
                }
                var bones = source.bones;
                if (data.Bones == null) data.Bones = Array.ConvertAll(bones, bone => bone.name);
                if (bones.Length != data.Bones.Length) return null;
                for (int bone = 0; bone < bones.Length; bone++)
                    if (bones[bone].name != data.Bones[bone]) return null;
                data.Meshes[level] = source.sharedMesh;
            }

            var cloth = Resources.Load<Material>(ResourceFolder + style + "Cloth");
            var face = Resources.Load<Material>(ResourceFolder + look + "Skin");
            var eyes = Resources.Load<Material>(ResourceFolder + "HumanEyes");
            var hair = Resources.Load<Material>(ResourceFolder + look + "Hair");
            var brows = Resources.Load<Material>(ResourceFolder + "HumanBrows");
            var crowd = Resources.Load<Material>(ResourceFolder + (look == Metro3DRiderLook.Office ? "YoungCrowd" : look + "Crowd"));
            if (cloth == null || face == null || eyes == null || hair == null || brows == null || crowd == null)
                return null;
            for (int palette = 0; palette < ClothTints.Length; palette++)
            {
                var material = cloth;
                if (palette != 0)
                {
                    material = new Material(cloth);
                    material.SetColor("_BaseColor", ClothTints[palette]);
                    ownedMaterials.Add(material);
                }
                data.Near[palette] = new[] { material, face, eyes, hair, brows };
                data.Distant[palette] = new[] { material, crowd };
            }
            return variants[index] = data;
        }

        internal static bool CompatibleBinding(SkinnedMeshRenderer source,SkinnedMeshRenderer target)
        {
            if(source==null||target==null||source.sharedMesh==null||target.sharedMesh==null)return false;
            var sourceBones=source.bones;var targetBones=target.bones;
            var sourceBind=source.sharedMesh.bindposes;var targetBind=target.sharedMesh.bindposes;
            if(sourceBones.Length==0||sourceBones.Length!=sourceBind.Length||targetBones.Length!=targetBind.Length)return false;
            for(int i=0;i<sourceBones.Length;i++) {
                if(sourceBones[i]==null)return false;
                int match=-1;
                for(int j=0;j<targetBones.Length;j++)
                    if(targetBones[j]!=null&&targetBones[j].name==sourceBones[i].name){match=j;break;}
                if(match<0)return false;
                for(int k=0;k<16;k++) {
                    float a=sourceBind[i][k],b=targetBind[match][k];
                    // Existing FBX round-tripping changes six index-finger bind
                    // entries by at most .023; it must not admit a unit-scale
                    // mismatch (the broken hips entry differed by .866).
                    if(float.IsNaN(a)||float.IsInfinity(a)||float.IsNaN(b)||float.IsInfinity(b)||Mathf.Abs(a-b)>.03f)return false;
                }
            }
            return true;
        }
    }
}
