using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SubwayCarry.Gameplay3D
{
    // Signs are real world geometry, never the legacy always-on-top GUI/font shader.
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class Metro3DWorldSign : MonoBehaviour
    {
        public float maximumWidth = 3f;
        public float maximumHeight = .24f;
        static readonly Dictionary<Font, Material> materials = new Dictionary<Font, Material>();
        static bool subscribed;
        void Start() => Apply();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMaterials()
        {
            Font.textureRebuilt -= FontAtlasChanged;
            subscribed = false;
            foreach (var material in materials.Values) if (material != null) Destroy(material);
            materials.Clear();
        }

        static void FontAtlasChanged(Font font)
        {
            if (font != null && materials.TryGetValue(font, out var material) && material != null)
            {
                material.mainTexture = font.material.mainTexture;
                foreach(var sign in Resources.FindObjectsOfTypeAll<Metro3DWorldSign>()) {
                    var text=sign.GetComponent<TextMesh>(); if(text.font!=font) continue;
                    var renderer=sign.GetComponent<MeshRenderer>(); var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    block.SetTexture("_MainTex",material.mainTexture); renderer.SetPropertyBlock(block);
                }
            }
        }

        public void Configure(float width, float height)
        {
            maximumWidth = Mathf.Max(.05f, width);
            maximumHeight = Mathf.Max(.03f, height);
            Apply();
        }

        public void Apply()
        {
            var text = GetComponent<TextMesh>();
            var renderer = GetComponent<MeshRenderer>();
            if (text.font == null) return;
            if (!subscribed) { Font.textureRebuilt += FontAtlasChanged; subscribed = true; }
            if (!materials.TryGetValue(text.font, out var material) || material == null)
            {
                var shader = Resources.Load<Shader>("WorldSignText");
                if (shader == null) throw new System.InvalidOperationException("Missing depth-tested WorldSignText shader.");
                material = new Material(shader) { name = "3D sign font: " + text.font.name, hideFlags = HideFlags.HideAndDontSave };
                materials[text.font] = material;
            }
            // Existing redistributable Nanum Gothic; only 3D signs use the heavier style.
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 64;
            text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            material.mainTexture = text.font.material.mainTexture;
            renderer.sharedMaterial = material;
            var color=new MaterialPropertyBlock(); renderer.GetPropertyBlock(color);
            color.SetColor("_Color",text.color); color.SetTexture("_MainTex",material.mainTexture); renderer.SetPropertyBlock(color);
            var bounds = renderer.localBounds;
            float width = bounds.size.x * Mathf.Abs(transform.localScale.x);
            float height = bounds.size.y * Mathf.Abs(transform.localScale.y);
            float factor = Mathf.Min(1f, maximumWidth / Mathf.Max(.0001f, width), maximumHeight / Mathf.Max(.0001f, height));
            text.characterSize *= factor;
            // Native dynamic TextMesh UVs can become stale after atlas repacking. A
            // persistent SDF atlas supplies visible lettering; TextMesh stays metadata.
            renderer.enabled = false;
            var sdf = transform.Find("Depth-tested lettering")?.GetComponent<TextMeshPro>();
            if (sdf == null)
            {
                var child = new GameObject("Depth-tested lettering");
                child.transform.SetParent(transform, false);
                sdf = child.AddComponent<TextMeshPro>();
            }
            sdf.font = Resources.Load<TMP_FontAsset>("Metro3DSignFont");
            if (sdf.font == null) throw new System.InvalidOperationException("Missing station SDF font asset.");
            // This licensed Korean face has no circled exit-number glyphs. Keep the
            // actual numbers readable instead of allowing missing-glyph squares.
            sdf.text = text.text.Replace('①','1').Replace('②','2').Replace('③','3').Replace('④','4').Replace('⑤','5');
            sdf.color = text.color;
            sdf.richText = false;
            sdf.alignment = TextAlignmentOptions.Center;
            sdf.textWrappingMode = TextWrappingModes.NoWrap;
            sdf.overflowMode = TextOverflowModes.Overflow;
            sdf.enableAutoSizing = true;
            sdf.fontSizeMin = .01f;
            sdf.fontSizeMax = 100f;
            sdf.rectTransform.sizeDelta = new Vector2(maximumWidth, maximumHeight);
            sdf.ForceMeshUpdate(true, true);
            var signBounds = sdf.textBounds;
            float fit = Mathf.Min(1f, maximumWidth / Mathf.Max(.0001f, signBounds.size.x), maximumHeight / Mathf.Max(.0001f, signBounds.size.y));
            sdf.transform.localScale = Vector3.one * fit;
        }

        // Repair labels in the already-saved scene without regenerating/saving that scene.
        public static void PrepareExisting(Transform root)
        {
            // Reserve one stable atlas before fitting individual labels. Repacking it label-by-label
            // can leave TextMesh geometry referring to earlier glyph UVs after a scene reload.
            var labels=root.GetComponentsInChildren<TextMesh>(true);
            var characters=new Dictionary<Font,HashSet<char>>();
            foreach(var label in labels) if(label.font!=null) {
                if(!characters.TryGetValue(label.font,out var glyphs)) characters[label.font]=glyphs=new HashSet<char>();
                foreach(char c in label.text) glyphs.Add(c);
            }
            foreach(var group in characters) group.Key.RequestCharactersInTexture(new string(new List<char>(group.Value).ToArray()),64,FontStyle.Bold);
            foreach (var text in labels)
            {
                string content=text.text; text.text=string.Empty; text.text=content;
                var existing = text.GetComponent<Metro3DWorldSign>();
                if (existing != null) { existing.Apply(); continue; }
                float width = 3f, height = .24f;
                foreach (Transform sibling in text.transform.parent)
                {
                    if (sibling.name == "Wayfinding face")
                    {
                        var renderer = sibling.GetComponentInChildren<MeshRenderer>();
                        if (renderer == null) continue;
                        Vector3 delta = text.transform.position - renderer.bounds.center;
                        if (new Vector2(delta.x, delta.z).magnitude > .16f || Mathf.Abs(delta.y) > .26f) continue;
                        width = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) - .22f;
                        height = renderer.bounds.size.y - .14f;
                        break;
                    }
                    if (sibling.name == "White station name plate")
                    {
                        var renderer = sibling.GetComponentInChildren<MeshRenderer>();
                        if (renderer == null) continue;
                        Vector3 delta = text.transform.position - renderer.bounds.center;
                        if (new Vector2(delta.x, delta.z).magnitude > .16f || Mathf.Abs(delta.y) > .30f) continue;
                        width = 2.42f; height = text.text.StartsWith("K", System.StringComparison.Ordinal) ? .23f : .12f;
                        break;
                    }
                    if (sibling.name == "NoticeBoard")
                    {
                        Vector3 local = sibling.InverseTransformPoint(text.transform.position);
                        if (Mathf.Abs(local.x) > .12f || Mathf.Abs(local.z) > .18f || local.y < 1f || local.y > 2.2f) continue;
                        width = .87f; height = text.text.Contains("\n") ? .29f : .13f;
                        break;
                    }
                }
                text.gameObject.AddComponent<Metro3DWorldSign>().Configure(width, height);
            }
        }
    }
}
