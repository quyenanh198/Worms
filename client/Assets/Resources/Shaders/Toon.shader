// Stylized lit shader for characters and props: stepped diffuse, rim light, shadows.
Shader "Worms/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.6, 0.6, 1)
        _RimColor ("Rim", Color) = (1, 1, 1, 0.5)
        _Flash ("Hit Flash", Range(0, 1)) = 0
        _Segments ("Worm Segments", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half4 _RimColor;
        half _Flash;
        half _Segments;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = dot(n, light.direction) * 0.5 + 0.5;
                // A soft two-tone ramp: round bodies read as round, still cartoony.
                half ramp = smoothstep(0.3, 0.75, ndl * light.shadowAttenuation) * 0.5 + 0.5;
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0) * _RimColor.a;
                half3 base = _BaseColor.rgb;
                // Worm bodies: faint rings every other spine ring (uv.y counts rings).
                half ring = abs(frac(input.uv.y * 0.5) - 0.5) * 2.0;
                base *= lerp(1.0, lerp(0.86, 1.04, smoothstep(0.15, 0.65, ring)), _Segments);
                half3 color = base * (light.color * ramp + SampleSH(n) * 0.45) + _RimColor.rgb * rim;
                // A small glossy highlight.
                float3 hv = normalize(light.direction + v);
                color += light.color * pow(saturate(dot(n, hv)), 48.0) * 0.28 * light.shadowAttenuation;
                color = lerp(color, half3(1, 0.25, 0.2), _Flash);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
