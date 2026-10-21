Shader "OsakaNight/URP/Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (.018,.012,.07,1)
        _Horizon("Horizon", Color) = (.13,.04,.16,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Zenith;
                float4 _Horizon;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings SkyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 SkyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                float verticalBlend = pow(saturate(direction.y), .48);
                float3 color = lerp(_Horizon.rgb, _Zenith.rgb, verticalBlend);
                float2 cell = floor(direction.xz / (abs(direction.y) + .2) * 160);
                float star = step(.9978, frac(sin(dot(cell, float2(127.1,311.7))) * 43758.5453));
                color += star * smoothstep(.15,.7,direction.y) * .12;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
