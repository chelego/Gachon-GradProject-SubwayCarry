Shader "SubwayCarry/ArtMapSlice/ClosedSurface"
{
    Properties { _Color ("Opaque backing", Color) = (0.028,0.035,0.04,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(float3 positionOS : POSITION) { Varyings o; o.positionCS=TransformObjectToHClip(positionOS); return o; }
            half4 frag(Varyings i) : SV_Target { return half4(_Color.rgb,1); }
            ENDHLSL
        }
    }
}
