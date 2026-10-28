using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Presentation
{
    // Applied only while the isolated graphics demonstration scene is running.
    public sealed class StationGraphicsScope : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        RenderPipelineAsset previous;
        void Awake() { previous = QualitySettings.renderPipeline; QualitySettings.renderPipeline = pipeline; }
        void OnDestroy() { QualitySettings.renderPipeline = previous; }
    }
}
