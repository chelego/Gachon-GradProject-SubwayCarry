Shader "SubwayCarry/Gameplay/PackageDamageSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _DamageTex ("Damage Marks", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DamageColor ("Damage Color", Color) = (0.18,0.08,0.035,0.95)
        _DamageTiling ("Damage Tiling", Float) = 1.35
        [PerRendererData] _DamageStage ("Damage Stage", Float) = 0
        [PerRendererData] _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DamageTex);
            SAMPLER(sampler_DamageTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _DamageColor;
                float4 _SpriteUVRect;
                float _DamageTiling;
                float _DamageStage;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv) * input.color;

                if (_DamageStage < 0.5 || baseColor.a <= 0.001)
                {
                    return baseColor;
                }

                float2 rectSize = max(_SpriteUVRect.zw, float2(0.00001, 0.00001));
                float2 localUv = saturate(
                    (input.uv - _SpriteUVRect.xy) / rectSize);
                float2 damageUv = frac(localUv * max(0.1, _DamageTiling));
                half damageAlpha = SAMPLE_TEXTURE2D(
                    _DamageTex,
                    sampler_DamageTex,
                    damageUv).a;

                // The package occupies only a small part of each animation cell.
                // Offset samples make sure readable damage marks reach that opaque
                // area even when the sprite is displayed at gameplay scale.
                damageAlpha = max(
                    damageAlpha,
                    SAMPLE_TEXTURE2D(
                        _DamageTex,
                        sampler_DamageTex,
                        frac(damageUv + float2(0.37, 0.19))).a);
                if (_DamageStage >= 1.5)
                {
                    damageAlpha = max(
                        damageAlpha,
                        SAMPLE_TEXTURE2D(
                            _DamageTex,
                            sampler_DamageTex,
                            frac(damageUv + float2(0.13, 0.61))).a);
                    damageAlpha = max(
                        damageAlpha,
                        SAMPLE_TEXTURE2D(
                            _DamageTex,
                            sampler_DamageTex,
                            frac(damageUv + float2(0.71, 0.43))).a);
                }
                if (_DamageStage >= 2.5)
                {
                    damageAlpha = max(
                        damageAlpha,
                        SAMPLE_TEXTURE2D(
                            _DamageTex,
                            sampler_DamageTex,
                            frac(damageUv + float2(0.53, 0.79))).a);
                    damageAlpha = max(
                        damageAlpha,
                        SAMPLE_TEXTURE2D(
                            _DamageTex,
                            sampler_DamageTex,
                            frac(damageUv + float2(0.89, 0.07))).a);
                }

                float markThreshold = _DamageStage < 1.5
                    ? 0.14
                    : (_DamageStage < 2.5 ? 0.08 : 0.035);
                half mark = smoothstep(
                    markThreshold,
                    min(1.0, markThreshold + 0.16),
                    damageAlpha);

                float crushAmount = _DamageStage < 1.5
                    ? 0.18
                    : (_DamageStage < 2.5 ? 0.42 : 0.68);
                half3 crushedColor = baseColor.rgb * half3(0.62, 0.48, 0.38);
                baseColor.rgb = lerp(
                    baseColor.rgb,
                    crushedColor,
                    crushAmount);

                float markStrength = _DamageStage < 1.5
                    ? 0.58
                    : (_DamageStage < 2.5 ? 0.82 : 0.98);
                baseColor.rgb = lerp(
                    baseColor.rgb,
                    _DamageColor.rgb,
                    mark * _DamageColor.a * markStrength);
                return baseColor;
            }
            ENDHLSL
        }
    }
}
