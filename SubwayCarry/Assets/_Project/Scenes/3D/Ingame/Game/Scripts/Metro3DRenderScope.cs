using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D
{
    // Override for this gameplay session only; never rewrite the shared 2D pipeline asset.
    public sealed class Metro3DRenderScope : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        RenderPipelineAsset previous;
        SphericalHarmonicsL2 previousAmbient;
        AmbientMode previousMode;
        bool applied;
        void Awake()
        {
            previous = QualitySettings.renderPipeline; previousAmbient = RenderSettings.ambientProbe; previousMode=RenderSettings.ambientMode;
            QualitySettings.renderPipeline = pipeline; ApplyIndoorAmbient(); applied=true;
        }
        // Unbaked indoor bounce approximation. It is not a substitute for a measured light bake.
        public static void ApplyIndoorAmbient()
        {
            var ambient=new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.26f,.275f,.29f));
            ambient.AddDirectionalLight(Vector3.up,new Color(.10f,.105f,.11f),1);
            ambient.AddDirectionalLight(Vector3.down,new Color(.22f,.225f,.23f),1);
            RenderSettings.ambientMode=AmbientMode.Custom;
            RenderSettings.ambientProbe=ambient;
        }
        // Unity's first environment update can replace the custom SH after Awake.
        void LateUpdate() {if(applied) ApplyIndoorAmbient();}
        void OnDestroy() { if(applied) {QualitySettings.renderPipeline = previous; RenderSettings.ambientMode=previousMode; RenderSettings.ambientProbe=previousAmbient;} }
    }
}
