// Procedural skybox: vertical gradient plus a soft sun.
Shader "Worms/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.25, 0.5, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.75, 0.87, 0.95, 1)
        _BottomColor ("Bottom", Color) = (0.55, 0.7, 0.8, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.8, 1)
        _SunDirection ("Sun Direction", Vector) = (-0.4, 0.5, -0.7, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _SunColor;
                float4 _SunDirection;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                half3 color = d.y > 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(d.y), 0.6))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-d.y * 3.0));
                float sun = saturate(dot(d, normalize(_SunDirection.xyz)));
                color += _SunColor.rgb * (pow(sun, 400.0) * 2.0 + pow(sun, 12.0) * 0.25);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
