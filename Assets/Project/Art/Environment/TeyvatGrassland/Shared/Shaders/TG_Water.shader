Shader "Teyvat/URP/Water"
{
    Properties
    {
        _ShallowColor("Shallow turquoise",Color)=(.15,.66,.63,.72)
        _DeepColor("Deep blue",Color)=(.045,.27,.43,.9)
        _FoamColor("Foam",Color)=(.85,.96,.91,1)
        _DepthRange("Deep color depth metres",Float)=4
        _FoamDistance("Contact foam depth metres",Float)=.35
        _FlowSpeed("UV flow per second",Float)=.24
        _FlowDirection("UV flow direction",Vector)=(0,1,0,0)
        _FlowStrength("Surface pattern strength",Range(0,1))=.15
        _UseSceneDepth("Scene depth / fallback",Range(0,1))=1
        _FallbackDepth("Fallback depth metres",Float)=2
        [Enum(River,0,Waterfall,1)]_Mode("Water family",Float)=0
        _Opacity("Opacity scale",Range(0,1))=1
        [HideInInspector]_DebugDepth("Depth diagnostic",Float)=0
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            Name "ForwardWater" Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "TG_Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor,_DeepColor,_FoamColor;float4 _FlowDirection;
            float _DepthRange,_FoamDistance,_FlowSpeed,_FlowStrength,_UseSceneDepth,_FallbackDepth,_Mode,_Opacity,_DebugDepth;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;half fog:TEXCOORD2;half3 normal:TEXCOORD3;};
            V vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(V i):SV_Target
            {
                float2 screen=i.positionCS.xy/_ScaledScreenParams.xy;
                float rawDepth=SampleSceneDepth(screen);
                float sceneEye=LinearEyeDepth(rawDepth,_ZBufferParams);
                float surfaceEye=-TransformWorldToView(i.world).z;
                if(_DebugDepth>3.5)return half4(surfaceEye/40,surfaceEye/40,surfaceEye/40,1);
                if(_DebugDepth>2.5)return half4(sceneEye/40,sceneEye/40,sceneEye/40,1);
                if(_DebugDepth>1.5)return half4(rawDepth,rawDepth,rawDepth,1);
                float thickness=lerp(_FallbackDepth,max(0,sceneEye-surfaceEye),_UseSceneDepth);
                half depth=saturate(thickness/max(.05,_DepthRange));
                if(_DebugDepth>.5)return half4(depth,depth,depth,1);
                float2 flow=i.uv-_FlowDirection.xy*(_TG_Wind.w*_FlowSpeed);
                float2 p=i.world.xz-_FlowDirection.xy*(_TG_Wind.w*_FlowSpeed);
                half ripple=sin(p.x*1.7+p.y*2.3)*.5+sin(p.x*.65-p.y*1.1)*.3;
                half sparkle=pow(saturate(ripple+.25),15)*.4;
                half shore=(1-smoothstep(.03,max(.04,_FoamDistance),thickness))*(.65+.35*sin(p.x*6+p.y*4));
                half streak=pow(saturate(sin(flow.x*98+sin(flow.y*21)*.7)),7)*.6+pow(saturate(sin(flow.x*39+flow.y*25)),9)*.22;
                half impact=smoothstep(.85,1,i.uv.y)*.32;
                half4 base=lerp(_ShallowColor,_DeepColor,depth);
                base.rgb+=ripple*_FlowStrength*.09;
                half foam=lerp(saturate(shore+sparkle),saturate(streak+impact),_Mode);
                base.rgb=lerp(base.rgb,_FoamColor.rgb,foam);
                Light sun=GetMainLight();half light=.78+.22*saturate(dot(normalize(i.normal),sun.direction));
                base.rgb*=light;base.a=lerp(base.a,.94,foam)*_Opacity;
                return half4(MixFog(base.rgb,i.fog),base.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
