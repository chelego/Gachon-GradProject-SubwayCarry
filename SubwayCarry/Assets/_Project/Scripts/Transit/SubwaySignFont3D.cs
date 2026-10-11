using UnityEngine;

namespace SubwayCarry.Transit
{
    /// <summary>Keeps world signs on the dynamic Korean font atlas with normal depth testing.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SubwaySignFont3D : MonoBehaviour
    {
        public Font font;
        public Material signMaterial;
        private void OnEnable()
        {
            Font.textureRebuilt += UpdateAtlas;
            if (!font || !signMaterial) return;
            string characters = "";
            foreach (var sign in GetComponentsInChildren<TextMesh>()) characters += sign.text;
            font.RequestCharactersInTexture(characters, 64, FontStyle.Normal);
            UpdateAtlas(font);
        }
        private void OnDisable() => Font.textureRebuilt -= UpdateAtlas;
        private void UpdateAtlas(Font rebuilt)
        {
            if (rebuilt == font && signMaterial && font.material)
                signMaterial.SetTexture("_BaseMap", font.material.mainTexture);
        }
    }
}

