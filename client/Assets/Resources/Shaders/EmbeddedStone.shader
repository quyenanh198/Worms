Shader "Worms/EmbeddedStone"
{
    Properties { _BaseColor ("Stone", Color) = (0.58, 0.55, 0.48, 1) }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+1" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                // Three broad facets, with a narrow dark edge against the earth.
                half shade = uv.y > 0.49 + uv.x * 0.24 ? 1.13 : (uv.x < 0.43 ? 0.92 : 0.72);
                half edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                shade *= lerp(0.67, 1.0, smoothstep(0.0, 0.08, edge));
                half3 color = MixFog(_BaseColor.rgb * shade, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
