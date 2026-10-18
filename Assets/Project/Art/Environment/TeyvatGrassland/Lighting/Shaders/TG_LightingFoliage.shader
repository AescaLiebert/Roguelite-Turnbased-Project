Shader "Teyvat/URP/LightingFoliage"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo / opacity", 2D) = "white" {}
        [MainColor] _BaseColor ("Base color", Color) = (0.62,0.82,0.35,1)
        _RootColor ("Root / lower leaf tint", Color) = (1,1,1,1)
        _TipColor ("Tip / upper leaf tint", Color) = (1,1,1,1)
        _CanopyShadeColor ("Cool canopy side tint", Color) = (0.55,0.80,0.75,1)
        _UseColorField ("Grass roots use world color field", Range(0,1)) = 1
        _WindResponse ("Wind response", Range(0,2)) = 1
        _Wrap ("Diffuse wrap", Range(0,1)) = 0.35
        _Transmission ("Backlighting", Range(0,1)) = 0.25
        _Cutoff ("Opacity cutoff", Range(0,1)) = 0.45
        [Toggle] _AlphaClip ("Clip opacity", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(Grass,0,Leaf,1,Flower,2)] _Family ("Foliage family", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "AlphaTest" }
        LOD 200
        Cull [_Cull]
        ZWrite On
        AlphaToMask On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _RootColor;
            half4 _TipColor;
            half4 _CanopyShadeColor;
            float _UseColorField;
            float _WindResponse;
            float _Wrap;
            float _Transmission;
            float _Cutoff;
            float _AlphaClip;
            float _Cull;
            float _Family;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        #include "TG_Common.hlsl"

        struct TGAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct TGFoliageVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
            float3 undeformedPositionWS : TEXCOORD3;
            half4 color : TEXCOORD4;
            half fog : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct TGDepthVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        half TGOpacity(half textureAlpha)
        {
            half alpha = saturate(textureAlpha * _BaseColor.a);
            if (_AlphaClip > 0.5)
                clip(alpha - _Cutoff);
            return alpha;
        }

        float3 TGFoliagePositionWS(TGAttributes input)
        {
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            return TGDeform(positionWS, input.color, _WindResponse, _Family);
        }

        TGFoliageVaryings TGFoliageVertex(TGAttributes input)
        {
            TGFoliageVaryings output = (TGFoliageVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.undeformedPositionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = TGFoliagePositionWS(input);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.color = input.color;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 TGFoliageFragment(TGFoliageVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            half alpha = TGOpacity(texel.a);
            half3 baseColor = texel.rgb * _BaseColor.rgb;
            half3 albedo = baseColor;
            half bend = saturate(input.color.r);
            if (_Family < 0.5)
            {
                // At weight zero and field weight one the unlit root is exactly the ground's field.
                // Sample before deformation so wind cannot swim the broad ground-color pattern.
                half3 root = lerp(baseColor * _RootColor.rgb, TGColorField(input.undeformedPositionWS), saturate(_UseColorField));
                albedo = lerp(root, baseColor * _TipColor.rgb, bend);
            }
            else if (_Family < 1.5)
            {
                albedo *= lerp(_RootColor.rgb, _TipColor.rgb, bend);
                albedo *= lerp(_CanopyShadeColor.rgb,half3(1,1,1),smoothstep(.2h,.88h,abs(input.normalWS.y)));
            }
            // Family two preserves the authored flower-petal hue.
            half3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0h, -1.0h);
            // Stylized foliage keeps the authored upward canopy/blade bias on both faces.
            normalWS.y = abs(normalWS.y);
            normalWS = normalize(normalWS);
            half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            half3 color = TGShade(albedo, input.positionWS, normalWS, viewDirectionWS, _Wrap, _Transmission);
            // Exclude baked vertex shading and fog from the raw-albedo comparison.
            if (_TG_DebugAlbedo <= .5)
            {
                color *= saturate(input.color.a);
                color = MixFog(color, input.fog);
            }
            return half4(color, alpha);
        }

        TGDepthVaryings TGFoliageDepthVertex(TGAttributes input)
        {
            TGDepthVaryings output = (TGDepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformWorldToHClip(TGFoliagePositionWS(input));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        float3 _LightDirection;
        float3 _LightPosition;

        TGDepthVaryings TGFoliageShadowVertex(TGAttributes input)
        {
            TGDepthVaryings output = (TGDepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 positionWS = TGFoliagePositionWS(input);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        half4 TGFoliageDepthFragment(TGDepthVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half alpha = TGOpacity(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a);
            return half4(0, 0, 0, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TGFoliageVertex
            #pragma fragment TGFoliageFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TGFoliageShadowVertex
            #pragma fragment TGFoliageDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TGFoliageDepthVertex
            #pragma fragment TGFoliageDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
