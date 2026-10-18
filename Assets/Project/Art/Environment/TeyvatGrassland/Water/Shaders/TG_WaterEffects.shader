Shader "Teyvat/URP/WaterEffects"
{
    Properties
    {
        [MainColor] _BaseColor("Effect color",Color)=(.84,.96,.94,1)
        _Opacity("Effect opacity",Range(0,1))=.65
        _FlowSpeed("Flow speed",Float)=.3
        _DepthFade("Intersection fade metres",Float)=.35
        _UseSceneDepth("Use intersection depth fade",Range(0,1))=1
        [Enum(Foam,0,Streak,1,Mist,2,ShoreStrip,3)] _EffectMode("Effect family",Float)=0
        _NoiseScale("Irregular patch scale",Float)=3
        _Softness("Soft edge width",Range(.02,.8))=.25
        [Enum(Fallback,0,Full,1)] _Quality("Quality",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            Name "ForwardWaterEffect"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EffectVertex
            #pragma fragment EffectFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "TG_WaterProcedural.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity,_FlowSpeed,_DepthFade,_UseSceneDepth,_EffectMode,_NoiseScale,_Softness,_Quality;
            CBUFFER_END
            struct EffectAttributes
            {
                float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct EffectVaryings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1;
                half4 color:TEXCOORD2; half fog:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            EffectVaryings EffectVertex(EffectAttributes input)
            {
                EffectVaryings output=(EffectVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz); output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv; output.color=input.color; output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 EffectFragment(EffectVaryings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time=_TG_Wind.w*_FlowSpeed+input.color.r*3.17;
                float2 uv=input.uv, centered=(uv-.5)*2;
                float radius=length(centered);
                float cloud=TGWaterCloud(uv*max(.1,_NoiseScale)+float2(-time*.31,time*.17),_Quality);
                half softEdge=1-smoothstep(max(.02,1-_Softness),1,radius);
                half alpha;
                if (_EffectMode<.5)
                {
                    float fine=TGWaterNoise(uv*_NoiseScale*3.1+float2(-time*.22,time*.3));
                    float ringRadius=radius+(cloud-.5)*.29;
                    half ring=1-smoothstep(.02,.055,abs(ringRadius-.59));
                    half inner=1-smoothstep(.12,.64,ringRadius);
                    half broken=smoothstep(.43,.58,cloud)*smoothstep(.30,.50,fine);
                    alpha=softEdge*(ring*broken+inner*broken*.45);
                }
                else if (_EffectMode<1.5)
                {
                    float cross=abs(centered.x+(cloud-.5)*.36);
                    half streak=1-smoothstep(.10,.35+_Softness*.2,cross);
                    half ends=smoothstep(0,.16,uv.y)*(1-smoothstep(.72,1,uv.y));
                    alpha=streak*ends*lerp(.35,1,smoothstep(.18,.76,cloud));
                }
                else if (_EffectMode<2.5)
                {
                    // Low-contrast layered haze with radial transparent edges;
                    // no opaque rectangular card silhouette or sharp noisy holes.
                    alpha=softEdge*exp(-radius*radius*1.3)*lerp(.28,.70,cloud);
                }
                else
                {
                    float2 flow=input.positionWS.xz*max(.1,_NoiseScale)+float2(-time*.15,time*.07);
                    alpha=smoothstep(.42,.72,TGWaterCloud(flow,_Quality))*.8;
                }
                if (_UseSceneDepth>.001)
                {
                    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    float sceneEye=TGWaterEyeDepth(raw);
                    float surfaceEye=-TransformWorldToView(input.positionWS).z;
                    alpha*=lerp(1,saturate((sceneEye-surfaceEye)/max(.01,_DepthFade)),_UseSceneDepth);
                }
                alpha*=saturate(input.color.a)*_Opacity*_BaseColor.a;
                return half4(MixFog(_BaseColor.rgb,input.fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
