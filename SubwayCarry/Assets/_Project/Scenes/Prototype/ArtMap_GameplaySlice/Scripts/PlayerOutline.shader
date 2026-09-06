Shader "SubwayCarry/ArtMapSlice/PlayerOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline", Color) = (0.2,1,1,1)
        _Width ("Width in texels", Float) = 1.5
        _UvRect ("Sprite UV rectangle", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor; float4 _UvRect; float _Width;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; o.color=v.color; return o; }
            float sampleAlpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a * step(_UvRect.x,uv.x)*step(_UvRect.y,uv.y)*step(uv.x,_UvRect.z)*step(uv.y,_UvRect.w); }
            half4 frag(Varyings i) : SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
                float2 d=_MainTex_TexelSize.xy*_Width;
                float a=max(max(sampleAlpha(i.uv+float2(d.x,0)),sampleAlpha(i.uv-float2(d.x,0))),max(sampleAlpha(i.uv+float2(0,d.y)),sampleAlpha(i.uv-float2(0,d.y))));
                a=max(a,max(max(sampleAlpha(i.uv+d),sampleAlpha(i.uv-d)),max(sampleAlpha(i.uv+float2(d.x,-d.y)),sampleAlpha(i.uv+float2(-d.x,d.y)))));
                return half4(lerp(_OutlineColor.rgb,c.rgb,saturate(c.a*5)),max(c.a,a*_OutlineColor.a*i.color.a));
            }
            ENDHLSL
        }
    }
}
