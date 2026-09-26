// Distant scenery layers: flat color with a vertical gradient, faded by fog.
Shader "Worms/Backdrop"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.35, 0.55, 0.35, 1)
        _BottomColor ("Bottom", Color) = (0.25, 0.4, 0.3, 1)
        _Height ("Gradient Height", Float) = 20
        _Haze ("Haze", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _TopColor;
        half4 _BottomColor;
        float _Height;
        half _Haze;
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
            #include "WormsCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // uv.y: 0 at the foot of the hill, 1 on its ridge line.
                float t = saturate(input.positionWS.y / _Height + 0.5);
                half3 color = lerp(_BottomColor.rgb, _TopColor.rgb, t);
                // Broad, soft patches (fields and woods) rather than fine blocky noise.
                color *= 0.9 + 0.2 * WormsFbm(input.positionWS.xy * 0.08);
                // A sunlit rim along the ridge gives each layer a crisp silhouette.
                color = lerp(color, color * 1.25 + 0.04, smoothstep(0.93, 1.0, input.uv.y));
                // Aerial perspective: farther layers melt into the scene's haze color.
                color = lerp(color, unity_FogColor.rgb, _Haze);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
