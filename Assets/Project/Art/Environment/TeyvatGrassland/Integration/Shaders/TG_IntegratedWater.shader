Shader "Teyvat/URP/IntegratedWater"
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
        _FlowLength("Fall arclength metres", Float) = 30
        _FlowWidth("Fall width metres", Float) = 8
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
                float _DepthRange, _FoamDistance, _FlowSpeed, _FlowStrength, _FlowLength, _FlowWidth;
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
                    // V is source-to-impact arclength. Metre-space cells hold
                    // sparse narrow white trails rather than broad white clouds.
                    float along=uv.y*max(1,_FlowLength)-time*4.0;
                    float across=uv.x*max(.25,_FlowWidth);
                    float waviness=(TGWaterNoise(float2(along*.09,across*.18+7))-.5)*.17;
                    float2 streakGrid=float2((across+waviness)*1.15,along*.19);
                    float2 streakCell=floor(streakGrid);
                    float2 within=frac(streakGrid);
                    float seed=TGWaterHash(streakCell+float2(19.7,3.2));
                    float center=lerp(.18,.82,TGWaterHash(streakCell+float2(2.3,9.1)));
                    center+=sin(within.y*3.14159+seed*6.28318)*.035;
                    float thin=lerp(.06,.14,TGWaterHash(streakCell+float2(8.2,4.7)));
                    float deviation=abs(within.x-center);
                    float aa=max(fwidth(deviation),.003);
                    half thread=1-smoothstep(thin,thin+aa,deviation);
                    half lengthMask=smoothstep(.02,.13,within.y)*(1-smoothstep(.48,.93,within.y));
                    half selected=step(.62,seed);
                    half ribbons=thread*lengthMask*selected;
                    if (_Quality>.5)
                    {
                        float2 secondGrid=float2((across-waviness*.7)*.69+3.1,along*.115+5.7);
                        float2 secondCell=floor(secondGrid), secondWithin=frac(secondGrid);
                        float secondary=TGWaterHash(secondCell+31.4);
                        float secondaryCenter=lerp(.25,.75,TGWaterHash(secondCell+12.6));
                        float secondaryDeviation=abs(secondWithin.x-secondaryCenter);
                        float secondaryWidth=lerp(.018,.04,secondary);
                        half narrow=1-smoothstep(secondaryWidth,secondaryWidth+max(fwidth(secondaryDeviation),.002),secondaryDeviation);
                        ribbons=max(ribbons,narrow*smoothstep(.03,.2,secondWithin.y)*(1-smoothstep(.42,.86,secondWithin.y))*step(.79,secondary)*.7);
                    }
                    float body=TGWaterNoise(float2(across*.27,along*.075));
                    // The physical course includes an approximately two-metre
                    // horizontal source apron. Carry crest whitewater across
                    // that apron and over the actual drop, with ragged teeth.
                    float sourceMetres=uv.y*max(1,_FlowLength);
                    float crestTeeth=TGWaterNoise(float2(across*1.45, time*.43+4.1));
                    half crest=1-smoothstep(2.9+crestTeeth*.8,3.7+crestTeeth,sourceMetres);
                    float impactMetres=(1-uv.y)*max(1,_FlowLength);
                    float churn=TGWaterCloud(float2(across*1.6+waviness,along*.95-time*.7),_Quality);
                    half plunge=(1-smoothstep(.18,1.2,impactMetres))*smoothstep(.27,.66,churn);
                    plunge=max(plunge,(1-smoothstep(.03,.35,impactMetres))*.42);
                    half rimBreak=lerp(.68,1,TGWaterNoise(float2(across*1.9,along*.5)));
                    half white=saturate((ribbons*.97+crest*.92*rimBreak+plunge*.94*rimBreak)*_FoamStrength);
                    half3 teal=lerp(_DeepColor.rgb,_ShallowColor.rgb,.48);
                    color=lerp(teal*(.96+(body-.5)*.075),_FoamColor.rgb,white);
                    float edgeNoise=(TGWaterNoise(float2(along*.35-time*.17,2.7))-.5)*.027;
                    float edge=min(uv.x,1-uv.x)+edgeNoise;
                    half side=smoothstep(0,max(.005,_EdgeSoftness),edge);
                    half endFade=smoothstep(0,.006,uv.y)*(1-smoothstep(.992,1,uv.y));
                    alpha=side*endFade*lerp(.96,1.0,saturate(ribbons+crest+plunge))*_Opacity;
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
