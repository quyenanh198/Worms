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
        _GrassBand ("Grass Band", Float) = 2.35
        _MottleScale ("Broad Detail Scale", Float) = 0.9
        _MottleStrength ("Broad Detail Strength", Range(0, 0.4)) = 0.18
        _PebbleStrength ("Pebble Strength", Range(0, 0.5)) = 0.26
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
        float _GrassBand;
        float _MottleScale;
        float _MottleStrength;
        float _PebbleStrength;
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
                // Large color patches survive Low tier's 0.7 render scale.
                float broad = WormsFbm(ws.xy * _MottleScale + ws.z * 0.2);
                float mottle = (broad - 0.5) * _MottleStrength;
                half3 albedo;

                if (input.uv.y > 0.5)
                {
                    // Side wall: grass where it faces up, rock elsewhere; darker toward the back.
                    half3 rock = lerp(_RockColor.rgb, _DirtColor.rgb, 0.4) * (0.9 + mottle);
                    half3 grass = _GrassColor.rgb * (0.95 + mottle);
                    // Keep exposed walls warm and continuous. The bright grass
                    // lip is drawn by the front face's depth band below.
                    albedo = lerp(rock, grass, 0.18);
                    albedo *= lerp(1.0, 0.7, saturate(ws.z * 0.5));
                }
                else
                {
                    // Front face: topsoil, dirt, then deep rock, with scattered pebbles.
                    float d = input.uv.x + (broad - 0.5) * 1.2;
                    half3 dirt = _DirtColor.rgb * (0.98 + mottle);
                    half3 turf = _GrassColor.rgb * (1.08 - 0.24 * smoothstep(1.0, _GrassBand, d) + mottle);
                    albedo = lerp(turf, dirt,
                                  smoothstep(_GrassBand - 1.2, _GrassBand + 0.8, d));
                    albedo = lerp(albedo, _DeepColor.rgb * (1.0 + mottle), smoothstep(7.0, 23.0, d));
                    // A few broad stones read as texture without fine noise shimmer.
                    float pebble = step(0.84, WormsValueNoise(ws.xy * 2.8));
                    albedo = lerp(albedo, _RockColor.rgb, pebble * _PebbleStrength * smoothstep(2.0, 4.0, d));
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
                albedo = lerp(albedo, albedo * 0.22 + half3(0.03, 0.025, 0.02), burn * (0.75 + 0.25 * broad));

                Light light = GetMainLight(TransformWorldToShadowCoord(ws));
                half ndl = saturate(dot(n, light.direction));
                half shadow = light.shadowAttenuation;
                if (input.uv.y > 0.5)
                {
                    // Exposed cut walls should read as one continuous band,
                    // including vertical faces and self-shadowed cell edges.
                    ndl = 0.62;
                    shadow = 1.0;
                }
                half3 ambient = input.uv.y > 0.5 ? SampleSH(float3(0, 1, 0)) : SampleSH(n);
                half3 color = albedo * (light.color * ndl * shadow + ambient);
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
