Shader "Teyvat/URP/WaterProduction"
{
    Properties
    {
        _ShallowColor("Shallow turquoise", Color) = (.15,.66,.63,.72)
        _DeepColor("Deep azure", Color) = (.045,.27,.43,.90)
        _FoamColor("Painted foam", Color) = (.85,.96,.91,1)
        _DepthRange("Deep color depth metres", Float) = 4
        _FoamDistance("Contact foam depth metres", Float) = .35
        _FlowDirection("Flow direction", Vector) = (0,1,0,0)
        _FlowSpeed("Flow speed", Float) = .24
        _FlowStrength("Surface pattern strength", Range(0,1)) = .3
        _UseSceneDepth("Scene depth / fallback", Range(0,1)) = 1
        _FallbackDepth("Fallback depth metres", Float) = 2
        [Enum(River,0,Waterfall,1,Pool,2)] _Mode("Water family", Float) = 0
        _Opacity("Opacity scale", Range(0,1)) = 1
        _SkyColor("Artist sky reflection approximation", Color) = (.43,.74,.88,1)
        _ReflectionStrength("Sky Fresnel strength", Range(0,1)) = .22
        _FoamStrength("Foam strength", Range(0,1)) = .75
        _RippleScale("World ripple scale", Float) = .35
        _EdgeSoftness("Water edge softness", Range(.005,.4)) = .08
        [Enum(Fallback,0,Full,1)] _Quality("Quality", Float) = 1
        [HideInInspector] _DebugDepth("0 normal, 1 fraction, 2 raw, 3 scene eye/40, 4 surface eye/40", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "TG_WaterProcedural.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor, _SkyColor;
                float4 _FlowDirection;
                float _DepthRange, _FoamDistance, _FlowSpeed, _FlowStrength;
                float _UseSceneDepth, _FallbackDepth, _Mode, _Opacity, _DebugDepth;
                float _ReflectionStrength, _FoamStrength, _RippleScale, _EdgeSoftness, _Quality;
            CBUFFER_END
            struct WaterAttributes
            {
                float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct WaterVaryings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1;
                half3 normalWS:TEXCOORD2; half fog:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            WaterVaryings WaterVertex(WaterAttributes input)
            {
                WaterVaryings output=(WaterVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv; output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 WaterFragment(WaterVaryings input, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float surfaceEye=-TransformWorldToView(input.positionWS).z;
                float rawDepth=0, sceneEye=surfaceEye+max(0,_FallbackDepth);
                // Falling curtains use the artist fallback: the cliff directly
                // behind a curtain is not its water thickness. Diagnostic modes
                // still expose the real camera sample for independent checks.
                if (((_Mode<.5 || _Mode>1.5) && _UseSceneDepth>.001) || _DebugDepth>1.5)
                {
                    rawDepth=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    sceneEye=TGWaterEyeDepth(rawDepth);
                }
                if (_DebugDepth>3.5) return half4(surfaceEye/40,surfaceEye/40,surfaceEye/40,1);
                if (_DebugDepth>2.5) return half4(sceneEye/40,sceneEye/40,sceneEye/40,1);
                if (_DebugDepth>1.5) return half4(rawDepth,rawDepth,rawDepth,1);
                float thickness=max(0,_FallbackDepth);
                if (_Mode<.5 || _Mode>1.5) thickness=lerp(thickness,max(0,sceneEye-surfaceEye),saturate(_UseSceneDepth));
                half depth=saturate(thickness/max(.05,_DepthRange));
                if (_DebugDepth>.5) return half4(depth,depth,depth,1);

                float time=_TG_Wind.w*_FlowSpeed;
                float2 direction=dot(_FlowDirection.xy,_FlowDirection.xy)>.0001 ? normalize(_FlowDirection.xy) : float2(0,1);
                half3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(face,1.0h,-1.0h);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 color; half alpha;
                if (_Mode<.5 || _Mode>1.5)
                {
                    float2 flow=(input.positionWS.xz-direction*time)*max(.02,_RippleScale);
                    float broad=TGWaterCloud(flow,_Quality);
                    float detail=TGWaterNoise(flow*2.9+float2(4.1,-2.8));
                    half4 water=lerp(_ShallowColor,_DeepColor,depth);
                    color=water.rgb*(.94h+(broad-.5h)*_FlowStrength*.20h);
                    half shore=0, highlight=0;
                    if (_Quality>.5)
                    {
                        half contact=1-smoothstep(max(.01,_FoamDistance*.12),max(.02,_FoamDistance),thickness);
                        half breakup=smoothstep(.26,.67,TGWaterNoise(flow*4.4+detail*.8));
                        shore=contact*breakup*_UseSceneDepth;
                        // Broad broken accents, without regular sine spots or a
                        // view-dependent opaque grid of specular white circles.
                        highlight=smoothstep(.56,.76,broad)*smoothstep(.40,.75,detail)*_FlowStrength*.45;
                    }
                    half fresnel=pow(1-saturate(dot(normal,view)),3);
                    color=lerp(color,_SkyColor.rgb,fresnel*_ReflectionStrength);
                    half foam=saturate((shore+highlight)*_FoamStrength);
                    color=lerp(color,_FoamColor.rgb,foam);
                    alpha=lerp(water.a,.95h,foam)*_Opacity;
                    if (_UseSceneDepth>.001) alpha*=lerp(1,smoothstep(0,max(.005,_EdgeSoftness),thickness),_UseSceneDepth);
                    if (_Mode>1.5) alpha*=1-smoothstep(.55,1,length((input.uv-.5)*2));
                }
                else
                {
                    float2 uv=input.uv;
                    float warp=(TGWaterNoise(float2(uv.y*3.4-time*.7,uv.x*2.7+2))-.5)*1.05;
                    float2 flow=float2(uv.x*7.8+warp,uv.y*6.0)-direction*time*2.0;
                    float cloud=TGWaterCloud(flow,_Quality);
                    float broken=TGWaterNoise(float2(uv.x*11.2+warp*.7,uv.y*14.5-time*3.8));
                    float threads=TGWaterNoise(float2(uv.x*23+warp*2.1,uv.y*3.7-time*2.9));
                    half ribbons=smoothstep(.33,.65,cloud)*lerp(.18,1,smoothstep(.27,.66,broken));
                    ribbons=saturate(ribbons+smoothstep(.60,.78,threads)*.26);
                    half crest=(1-smoothstep(0,.14,uv.y))*(.55+.45*broken);
                    half plunge=smoothstep(.78,1,uv.y)*(.3+.7*cloud);
                    half white=saturate((.10+ribbons*.95+crest*.38+plunge*.38)*_FoamStrength);
                    color=lerp(lerp(_DeepColor.rgb,_ShallowColor.rgb,.81),_FoamColor.rgb,white);
                    float edgeNoise=(TGWaterNoise(float2(uv.y*9.3-time*1.1,2.7))-.5)*.035;
                    float edge=min(uv.x,1-uv.x)+edgeNoise;
                    half side=smoothstep(0,max(.005,_EdgeSoftness),edge);
                    half endFade=smoothstep(0,.018,uv.y)*(1-smoothstep(.975,1,uv.y));
                    alpha=side*endFade*lerp(.56,.96,saturate(ribbons+crest*.4+plunge*.3))*_Opacity;
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normal,sun.direction));
                half illumination=(.87h+.13h*diffuse)*lerp(.74h,1.0h,sun.shadowAttenuation);
                color*=illumination;
                return half4(MixFog(color,input.fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
