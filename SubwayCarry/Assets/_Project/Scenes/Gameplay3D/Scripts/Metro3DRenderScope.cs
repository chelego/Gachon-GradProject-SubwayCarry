using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D
{
    // Override for this gameplay session only; never rewrite the shared 2D pipeline asset.
    public sealed class Metro3DRenderScope : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        RenderPipelineAsset previous;
        void Awake() { previous = QualitySettings.renderPipeline; QualitySettings.renderPipeline = pipeline; }
        void OnDestroy() { QualitySettings.renderPipeline = previous; }
    }
}
