Shader "SubwayCarry/3D/WorldSignText"
{
    Properties {
        _MainTex ("Font atlas", 2D) = "white" {}
        _Color ("Label color", Color) = (1,1,1,1)
        _FaceColor ("Face color", Color) = (1,1,1,1)
        _FaceDilate ("Face weight", Range(-1,1)) = .08
        _OutlineWidth ("Outline width", Float) = 0
        _OutlineSoftness ("Outline softness", Float) = 0
        _GradientScale ("SDF padding", Float) = 9
        _TextureWidth ("Atlas width", Float) = 2048
        _TextureHeight ("Atlas height", Float) = 2048
        _WeightNormal ("Normal weight", Float) = 0
        _WeightBold ("Bold weight", Float) = .5
        _ScaleRatioA ("Scale ratio", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FaceColor;
                float _FaceDilate;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color*_FaceColor; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float distance=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                float edge=max(fwidth(distance),.001);
                half4 c=i.color; c.a*=saturate((distance-.5+_FaceDilate*.1)/edge+.5);
                clip(c.a-.005h); return c;
            }
            ENDHLSL
        }
    }
}
