// Distant scenery layers: flat color with a vertical gradient, faded by fog.
Shader "Worms/Backdrop"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.35, 0.55, 0.35, 1)
        _BottomColor ("Bottom", Color) = (0.25, 0.4, 0.3, 1)
        _Height ("Gradient Height", Float) = 20
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _TopColor;
        half4 _BottomColor;
        float _Height;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "WormsCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = saturate(input.positionWS.y / _Height + 0.5);
                half3 color = lerp(_BottomColor.rgb, _TopColor.rgb, t);
                color *= 0.92 + 0.16 * WormsValueNoise(input.positionWS.xy * 0.6);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
