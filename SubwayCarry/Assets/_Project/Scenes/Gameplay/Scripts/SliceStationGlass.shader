Shader "SubwayCarry/Gameplay/StationGlass"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _GlassAlpha("Glass opacity", Range(0,1)) = .22
        _SpriteRect("Sprite UV bounds", Vector) = (0,0,1,1)
        _PaneA("Dark glass region A", Vector) = (2,3,2,3)
        _PaneB("Dark glass region B", Vector) = (2,3,2,3)
        _PaneSlope("Pane projection slope", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _SpriteRect, _PaneA, _PaneB; float _GlassAlpha, _PaneSlope;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varyings vert(Attributes a) { Varyings o; o.positionCS=TransformObjectToHClip(a.positionOS); o.uv=a.uv; o.color=a.color*_Color; return o; }
            float InPane(float2 uv, float4 pane)
            {
                float y=uv.y-uv.x*_PaneSlope;
                return step(pane.x,uv.x)*step(uv.x,pane.y)*step(pane.z,y)*step(y,pane.w);
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                // Existing art's blue-green glass only; silver rails, black seals and signs stay opaque.
                float glass=step(.2,(c.b-c.r)/max(.01,c.b))*step(.2,(c.g-c.r)/max(.01,c.g))*step(.012,c.g)*step(c.g,.5);
                float2 localUv=(i.uv-_SpriteRect.xy)/max(float2(.0001,.0001),_SpriteRect.zw-_SpriteRect.xy);
                // Near-black PSD panes need an explicit region; never key out every black seal in the sprite.
                float dark=step(max(c.r,max(c.g,c.b)),.025);
                glass=max(glass,dark*max(InPane(localUv,_PaneA),InPane(localUv,_PaneB)));
                c.a*=lerp(1,_GlassAlpha,glass);
                return c*i.color;
            }
            ENDHLSL
        }
    }
}
