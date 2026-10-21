Shader "Hidden/OsakaNight/URP/Glow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X(_OsakaBloom);
        float4 _OsakaGlowDirection;
        float _OsakaGlowThreshold;
        float _OsakaGlowStrength;
        half4 Extract(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            half peak = max(color.r, max(color.g, color.b));
            return half4(color.rgb * max(0, peak - _OsakaGlowThreshold) / max(peak, .001), 1);
        }
        half4 Blur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord) * .227027;
            color += (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + _OsakaGlowDirection.xy * 1.384615)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - _OsakaGlowDirection.xy * 1.384615)) * .316216;
            color += (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + _OsakaGlowDirection.xy * 3.230769)
                    + SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - _OsakaGlowDirection.xy * 3.230769)) * .070270;
            return color;
        }
        half4 Combine(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            color.rgb += SAMPLE_TEXTURE2D_X(_OsakaBloom, sampler_LinearClamp, input.texcoord).rgb * _OsakaGlowStrength;
            // Identical saturation-preserving shoulder to OsakaNightAtmosphere.
            color.rgb = color.rgb / (1 + max(0, color.rgb - 1) * .65);
            return color;
        }
        ENDHLSL
        Pass
        {
            Name "Extract"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Extract
            ENDHLSL
        }
        Pass
        {
            Name "Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur
            ENDHLSL
        }
        Pass
        {
            Name "Combine"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Combine
            ENDHLSL
        }
    }
    Fallback Off
}
