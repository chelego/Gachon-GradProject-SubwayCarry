using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SubwayCarry.Transit
{
    /// <summary>Uses the 3D renderer in both Scene and Game views while this car is loaded.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-10000)]
    public sealed class SubwaySceneRenderPipeline3D : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        public RenderPipelineAsset originalPipeline;
        private RenderPipelineAsset previous;
        private bool applied;
        public void Apply()
        {
            if (!pipeline) return;
            if (!applied)
            {
                previous = QualitySettings.renderPipeline;
                if (previous == pipeline) previous = originalPipeline;
            }
            InitializeRendererCache(pipeline);
            QualitySettings.renderPipeline = pipeline;
            applied = true;
        }

        /// <summary>Initialize URP's renderer array before editor inspectors read the default slot.</summary>
        public static void InitializeRendererCache(RenderPipelineAsset asset)
        {
            if (asset is UniversalRenderPipelineAsset urp
                && urp.renderers.Length < urp.rendererDataList.Length)
                urp.GetRenderer(-1);
        }
        private void OnEnable() => Apply();
        private void OnDisable()
        {
            if (applied && QualitySettings.renderPipeline == pipeline)
            {
                InitializeRendererCache(previous);
                QualitySettings.renderPipeline = previous;
            }
            applied = false;
        }
    }
}

