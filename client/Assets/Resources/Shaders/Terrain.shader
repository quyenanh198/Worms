// Destructible terrain: dirt front face shaded by depth below the surface,
// grassy top walls, rocky side walls, and optional painted soil detail.
Shader "Worms/Terrain"
{
    Properties
    {
        _GrassColor ("Grass", Color) = (0.36, 0.62, 0.22, 1)
        _DirtColor ("Dirt", Color) = (0.55, 0.38, 0.22, 1)
        _DeepColor ("Deep", Color) = (0.28, 0.19, 0.12, 1)
        _RockColor ("Rock", Color) = (0.46, 0.44, 0.41, 1)
        _NoiseScale ("Noise Scale", Float) = 1.6
        _Field ("Edge Field (R edge, G land above)", 2D) = "black" {}
        _PaintedSoil ("Painted soil", 2D) = "white" {}
        _PaintStrength ("Paint strength", Range(0, 1)) = 0
        _RockStrength ("Procedural rock strength", Range(0, 1)) = 1
        _PaintTint ("Paint tint", Color) = (0.72, 0.78, 1.15, 1)
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
        float _PaintStrength;
        float _RockStrength;
        half4 _PaintTint;
        float4 _FieldSize; // cells wide, cells high, cells per world unit
    CBUFFER_END
    TEXTURE2D(_Field);
    SAMPLER(sampler_Field);
    TEXTURE2D(_PaintedSoil);
    SAMPLER(sampler_PaintedSoil);
    float4 _PaintedSoil_TexelSize;
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
                // Per-cell field: distance to open air, and land straight above (both in cells).
                float2 cell = float2(ws.x, -ws.y) * _FieldSize.z;
                half2 field = SAMPLE_TEXTURE2D(_Field, sampler_Field, cell / _FieldSize.xy).rg;
                float edge = field.r * 8.0;
                float above = field.g * 24.0;
                bool front = input.uv.y < 0.5;

                if (!front)
                {
                    // The thin side of the slab: reads as a dark border, grass-dark where it faces up.
                    half3 soil = _DirtColor.rgb * 0.42;
                    half3 turf = _GrassColor.rgb * 0.55;
                    albedo = lerp(soil, turf, smoothstep(0.4, 0.7, n.y));
                }
                else
                {
                    // Painted soil in the manner of Worms: broad two-tone blotches, a few
                    // embedded stones, darker far underground.
                    float blotch = WormsFbm(ws.xy * 0.8 + 3.1);
                    half3 soil = lerp(_DirtColor.rgb * 1.05, _DirtColor.rgb * 0.8, smoothstep(0.38, 0.62, blotch));
                    soil *= 0.94 + 0.12 * noise;
                    float stone = WormsValueNoise(ws.xy * 2.2 + 11.3);
                    float stoneMask = smoothstep(0.81, 0.86, stone);
                    half3 stoneColor = lerp(_RockColor.rgb * 0.76, _RockColor.rgb * 1.13,
                        saturate((stone - 0.81) * 14.0));
                    soil = lerp(soil, stoneColor, stoneMask * 0.85 * (1.0 - _PaintStrength) * _RockStrength);
                    soil = lerp(soil, _DeepColor.rgb, 0.45 * smoothstep(10.0, 24.0, above));
                    if (_PaintStrength > 0.001)
                    {
                        float2 soilUv = ws.xy * 0.05 + float2(0.13, 0.27);
                        // Mirror alternate tiles so neighboring cliffs meet at identical
                        // texels even when the source PNG's opposite edges differ.
                        soilUv = 1.0 - abs(frac(soilUv * 0.5) * 2.0 - 1.0);
                        soilUv = clamp(soilUv, _PaintedSoil_TexelSize.xy * 0.5,
                            1.0 - _PaintedSoil_TexelSize.xy * 0.5);
                        half3 painted = SAMPLE_TEXTURE2D(_PaintedSoil, sampler_PaintedSoil, soilUv).rgb;
                        // Pull the generated orange swatch toward the reference's
                        // deeper, less saturated clay. Broad value changes keep a
                        // large cliff from reading as one flat tiled plane.
                        painted *= _PaintTint.rgb;
                        float strata = WormsValueNoise(ws.xy * 0.10 + 29.7);
                        painted *= 0.66 + 0.58 * strata;
                        soil = lerp(soil, painted, _PaintStrength * 0.85);
                    }

                    // Grass on top surfaces only, in uneven tufts.
                    float tuft = 3.4 + 3.0 * WormsValueNoise(float2(ws.x * 30.0, 1.7));
                    float isGrass = 1.0 - smoothstep(tuft - 0.4, tuft + 0.4, above);
                    half3 grass = _GrassColor.rgb * (0.95 + 0.15 * WormsValueNoise(float2(ws.x * 60.0, ws.y * 4.0)));
                    grass = lerp(grass * 1.18, grass, smoothstep(0.0, tuft, above)); // lit tips
                    albedo = lerp(soil, grass, isGrass);

                    // Edge treatment along every border, crater rims included:
                    // a dark outline, a bright crust just inside it, then a soft shade inward.
                    float outline = 1.0 - smoothstep(0.55, 1.05, edge);
                    float crust = smoothstep(0.9, 1.3, edge) * (1.0 - smoothstep(1.6, 2.8, edge));
                    float inward = smoothstep(2.0, 8.0, edge);
                    albedo *= lerp(1.0, 1.18, crust * (1.0 - isGrass));
                    albedo *= lerp(1.06, 0.9, inward);
                    half3 ink = lerp(_DeepColor.rgb * 0.78, _GrassColor.rgb * 0.52, isGrass);
                    albedo = lerp(albedo, ink, outline * 0.78);
                    // Shadow of the turf on the soil just below it.
                    float under = smoothstep(tuft, tuft + 0.5, above) * (1.0 - smoothstep(tuft + 0.5, tuft + 3.5, above));
                    albedo *= 1.0 - 0.28 * under * (1.0 - isGrass);
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
                // Flat, painted lighting: the front face is not shaded by angle, only darkened
                // a little where worms and props cast shadows.
                half ndl = front ? 0.72 : saturate(dot(n, light.direction) * 0.6 + 0.4);
                half shade = lerp(0.72, 1.0, light.shadowAttenuation);
                half3 color = albedo * (light.color * ndl * shade + SampleSH(float3(0, 0, -1)) * 0.75);
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
