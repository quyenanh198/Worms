// Sea at the bottom of the map: vertex waves, view-dependent tint, sun glint, crest foam.
Shader "Worms/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.18, 0.55, 0.62, 0.78)
        _DeepColor ("Deep", Color) = (0.05, 0.18, 0.32, 0.92)
        _FoamColor ("Foam", Color) = (0.9, 0.97, 1, 1)
        _WaveHeight ("Wave Height", Float) = 0.18
        _WaveSpeed ("Wave Speed", Float) = 1.2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _ShallowColor;
        half4 _DeepColor;
        half4 _FoamColor;
        float _WaveHeight;
        float _WaveSpeed;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WormsCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float crest : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            float Waves(float2 p, float t, out float2 slope)
            {
                float w1 = sin(p.x * 0.9 + t) * 0.6;
                float w2 = sin(p.x * 1.7 - p.y * 0.8 + t * 1.3) * 0.3;
                float w3 = sin(p.y * 1.3 + p.x * 0.4 + t * 0.7) * 0.2;
                slope = float2(cos(p.x * 0.9 + t) * 0.54 + cos(p.x * 1.7 - p.y * 0.8 + t * 1.3) * 0.51 + cos(p.y * 1.3 + p.x * 0.4 + t * 0.7) * 0.08,
                               -cos(p.x * 1.7 - p.y * 0.8 + t * 1.3) * 0.24 + cos(p.y * 1.3 + p.x * 0.4 + t * 0.7) * 0.26);
                return w1 + w2 + w3;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                float2 slope;
                float h = Waves(ws.xz, _Time.y * _WaveSpeed, slope);
                ws.y += h * _WaveHeight;
                o.positionWS = ws;
                o.normalWS = normalize(float3(-slope.x * _WaveHeight, 1, -slope.y * _WaveHeight));
                o.crest = h;
                o.positionCS = TransformWorldToHClip(ws);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0 - saturate(dot(n, v)), 4.0);
                half4 baseColor = lerp(_DeepColor, _ShallowColor, saturate(n.y * 0.5 + fresnel));
                Light light = GetMainLight();
                float3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), 90.0) * 1.5;
                float foamNoise = WormsValueNoise(input.positionWS.xz * 3.0 + _Time.y);
                half foam = smoothstep(0.55, 0.9, input.crest + foamNoise * 0.4);
                half3 color = baseColor.rgb * (light.color * 0.6 + SampleSH(n) * 0.6) + light.color * spec;
                color = lerp(color, _FoamColor.rgb, foam * 0.6);
                color = MixFog(color, input.fogFactor);
                return half4(color, saturate(baseColor.a + fresnel * 0.2 + foam * 0.3));
            }
            ENDHLSL
        }
    }
}
