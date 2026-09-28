// Transparent painted scenery behind the terrain, shared by every map theme.
Shader "Worms/BackdropSprite"
{
    Properties
    {
        _MainTex ("Scenery", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 0.88
        _EdgeFade ("Horizontal edge fade", Range(0, 0.25)) = 0
        _AlphaThreshold ("Soft alpha threshold", Range(0, 1)) = 0
        _Flash ("Hit flash", Range(0, 1)) = 0
        _TeamColor ("Worm body color", Color) = (1, 0, 0, 1)
        _RecolorStrength ("Recolor red body", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Opacity;
                half _EdgeFade;
                half _AlphaThreshold;
                half _Flash;
                half4 _TeamColor;
                half _RecolorStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 cloud = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_AlphaThreshold > 0.001h)
                    cloud.a = smoothstep(_AlphaThreshold, _AlphaThreshold + 0.06h, cloud.a);
                // Red body pixels are warm and saturated. Leave white eyes,
                // dark outlines and mouth details in their painted colors.
                half redMask = smoothstep(0.045h, 0.19h, cloud.r - max(cloud.g, cloud.b))
                    * smoothstep(0.2h, 0.38h, cloud.r);
                half brightness = max(cloud.r, 0.001h);
                half3 recolored = _TeamColor.rgb * brightness;
                cloud.rgb = lerp(cloud.rgb, recolored, redMask * _RecolorStrength);
                cloud.rgb = lerp(cloud.rgb, half3(1.0h, 1.0h, 1.0h), _Flash * 0.7h);
                cloud.rgb = MixFog(cloud.rgb, input.fogFactor);
                half fadeWidth = max(_EdgeFade, 0.0001h);
                half edgeAlpha = smoothstep(0.0h, fadeWidth, input.uv.x)
                    * smoothstep(0.0h, fadeWidth, 1.0h - input.uv.x);
                cloud.a *= _Opacity * edgeAlpha;
                return cloud;
            }
            ENDHLSL
        }
    }
}
