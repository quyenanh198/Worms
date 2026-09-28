// Tint a particle cutout by vertex color. Procedural particles can keep a
// radial fade; painted rock debris uses its own alpha silhouette.
Shader "Worms/Particle"
{
    Properties
    {
        _MainTex ("Particle cutout", 2D) = "white" {}
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.6
        _RadialFade ("Radial Fade", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Softness;
                half _RadialFade;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 radialUv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.radialUv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 cutout = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float d = length(input.radialUv - 0.5) * 2.0;
                half a = lerp(1.0h, saturate((1.0 - d) / _Softness), _RadialFade);
                return half4(input.color.rgb * cutout.rgb, input.color.a * a * cutout.a);
            }
            ENDHLSL
        }
    }
}
