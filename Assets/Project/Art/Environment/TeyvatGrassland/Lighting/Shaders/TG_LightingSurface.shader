Shader "Teyvat/URP/LightingSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted albedo",2D)="white"{}
        [MainColor] _BaseColor("Base tint",Color)=(1,1,1,1)
        _UseColorField("World grass color",Range(0,1))=0
        _MossColor("Grass / moss tint",Color)=(.4,.61,.18,1)
        _MossAmount("Upward grass blend",Range(0,1))=0
        _MossSlope("Grass slope threshold",Range(0,1))=.65
        _WorldScale("World texture scale",Float)=.14
        [Enum(UV,0,WorldTriplanar,1)]_Mapping("Texture mapping",Float)=0
        _Wrap("Diffuse wrap",Range(0,1))=.08
        _TextureStrength("Painted detail strength",Range(0,1))=1
        _UseSharedRockStyle("Shared terrain / mesh rock style",Range(0,1))=0
        [ToggleUI] _ReferenceRock("Normalized painted cliff albedo",Float)=0
        _RockTintScale("Rock slot tint multiplier",Range(.5,1.5))=1
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "TG_Common.hlsl"
        #include "Terrain/TG_ReferenceRock.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;half4 _BaseColor;half4 _MossColor;
        float _UseColorField,_MossAmount,_MossSlope,_WorldScale,_Mapping,_Wrap,_TextureStrength,_UseSharedRockStyle,_ReferenceRock,_RockTintScale;
        CBUFFER_END
        struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
        V vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
        half4 frag(V i):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);half3 n=normalize(i.normal);half3 paint;
            if(_Mapping>.5){half3 w=pow(abs(n),_ReferenceRock>.5?8:4);w/=max(dot(w,half3(1,1,1)),.001);float3 p=i.world*lerp(_WorldScale,_TG_RockWorldScale,_UseSharedRockStyle);
                paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*w.z;}
            else paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
            half3 ordinaryPaint=lerp(half3(1,1,1),paint,_TextureStrength)*_BaseColor.rgb;
            half3 albedo=lerp(ordinaryPaint,TGStyleRock(paint),_UseSharedRockStyle)*lerp(half3(1,1,1),TGColorField(i.world),_UseColorField);
            if(_ReferenceRock>.5)albedo=TGReferenceRock(paint,_TextureStrength)*_RockTintScale;
            half moss=smoothstep(_MossSlope,min(1,_MossSlope+.25),n.y)*_MossAmount;
            albedo=lerp(albedo,_MossColor.rgb*TGColorField(i.world),moss);
            half3 color=TGShade(albedo,i.world,n,GetWorldSpaceNormalizeViewDir(i.world),_Wrap,0);
            if (_TG_DebugAlbedo <= .5) color=MixFog(color,i.fog);
            return half4(color,1);
        }
        float3 _LightDirection;float3 _LightPosition;
        V shadow(A i){V o=vert(i);half3 d=_LightDirection;
        #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            d=normalize(_LightPosition-o.world);
        #endif
            o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.world,normalize(o.normal),d)));return o;}
        half4 depth(V i):SV_Target{return 0;}
        ENDHLSL
        Pass{Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass{Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ColorMask 0 ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex shadow
            #pragma fragment depth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass{Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ColorMask R ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
