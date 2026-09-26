// Destructible terrain: dirt front face shaded by depth below the surface,
// grassy top walls, rocky side walls. Colors come from noise, not textures.
Shader "Worms/Terrain"
{
    Properties
    {
        _GrassColor ("Grass", Color) = (0.36, 0.62, 0.22, 1)
        _DirtColor ("Dirt", Color) = (0.55, 0.38, 0.22, 1)
        _DeepColor ("Deep", Color) = (0.28, 0.19, 0.12, 1)
        _RockColor ("Rock", Color) = (0.46, 0.44, 0.41, 1)
        _NoiseScale ("Noise Scale", Float) = 1.6
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    // Scorch marks around recent explosions (xy = world center, z = radius, w = strength), set by Vfx.cs.
    float4 _WormsScorch[32];
    float _WormsScorchCount;
    CBUFFER_START(UnityPerMaterial)
        half4 _GrassColor;
        half4 _DirtColor;
        half4 _DeepColor;
        half4 _RockColor;
        float _NoiseScale;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WormsCommon.hlsl"

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
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 ws = input.positionWS;
                float3 n = normalize(input.normalWS);
                float noise = WormsFbm(ws.xy * _NoiseScale + ws.z * 0.7);
                half3 albedo;

                if (input.uv.y > 0.5)
                {
                    // Side wall: grass where it faces up, rock elsewhere; darker toward the back.
                    half3 rock = lerp(_RockColor.rgb, _RockColor.rgb * 0.65, noise);
                    half3 grass = _GrassColor.rgb * (0.85 + 0.3 * noise);
                    albedo = lerp(rock, grass, smoothstep(0.45, 0.75, n.y));
                    albedo *= lerp(1.0, 0.7, saturate(ws.z * 0.5));
                }
                else
                {
                    // Front face: topsoil, dirt, then deep rock, with scattered pebbles.
                    float d = input.uv.x + (noise - 0.5) * 2.0;
                    half3 dirt = lerp(_DirtColor.rgb, _DirtColor.rgb * 0.72, noise);
                    albedo = lerp(_GrassColor.rgb * (0.9 + 0.2 * noise), dirt, smoothstep(0.8, 2.2, d));
                    albedo = lerp(albedo, _DeepColor.rgb, smoothstep(6.0, 22.0, d));
                    float pebble = step(0.78, WormsValueNoise(ws.xy * _NoiseScale * 7.0));
                    albedo = lerp(albedo, _RockColor.rgb, pebble * 0.35 * smoothstep(2.0, 4.0, d));
                }

                half burn = 0;
                int scorches = (int)_WormsScorchCount;
                for (int k = 0; k < 32; k++)
                {
                    if (k >= scorches) break;
                    float4 sc = _WormsScorch[k];
                    float dist = distance(ws.xy, sc.xy);
                    burn = max(burn, (1.0 - smoothstep(sc.z * 0.85, sc.z * 1.35, dist)) * sc.w);
                }
                albedo = lerp(albedo, albedo * 0.22 + half3(0.03, 0.025, 0.02), burn * (0.75 + 0.25 * noise));

                Light light = GetMainLight(TransformWorldToShadowCoord(ws));
                half ndl = saturate(dot(n, light.direction));
                half3 color = albedo * (light.color * ndl * light.shadowAttenuation + SampleSH(n));
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
            Cull Back

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
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
