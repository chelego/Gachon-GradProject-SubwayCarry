using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Transit
{
    /// <summary>Uses the 3D renderer in both Scene and Game views while this car is loaded.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SubwaySceneRenderPipeline3D : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        public RenderPipelineAsset originalPipeline;
        private RenderPipelineAsset previous;
        private bool applied;
        public void Apply()
        {
            if (!pipeline || applied) return;
            previous = QualitySettings.renderPipeline;
            if (previous == pipeline) previous = originalPipeline;
            QualitySettings.renderPipeline = pipeline;
            applied = true;
        }
        private void OnEnable() => Apply();
        private void OnDisable()
        {
            if (applied && QualitySettings.renderPipeline == pipeline)
                QualitySettings.renderPipeline = previous;
            applied = false;
        }
    }
}

