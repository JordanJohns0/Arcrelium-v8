Shader "Arcrelium/OutlinePostProcess"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            float4 _MaskTex_TexelSize;
            float4 _OutlineColor;
            float _SampleDistance;
            float _OutlineStrength;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionHCS = float4(output.uv * 2.0 - 1.0, 0, 1);
                output.uv.y = 1.0 - output.uv.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float2 texelSize = _MaskTex_TexelSize.xy * _SampleDistance;
                float center = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).a;

                float maxNeighbor = 0;
                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(0, texelSize.y)).a);
                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv - float2(0, texelSize.y)).a);
                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(texelSize.x, 0)).a);
                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv - float2(texelSize.x, 0)).a);

                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(texelSize.x, texelSize.y)).a);
                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv - float2(texelSize.x, texelSize.y)).a);
                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(-texelSize.x, texelSize.y)).a);
                maxNeighbor = max(maxNeighbor, SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(texelSize.x, -texelSize.y)).a);

                float edge = saturate(maxNeighbor - center) * _OutlineStrength;

                return half4(lerp(scene.rgb, _OutlineColor.rgb, edge * _OutlineColor.a), scene.a);
            }
            ENDHLSL
        }
    }
}
