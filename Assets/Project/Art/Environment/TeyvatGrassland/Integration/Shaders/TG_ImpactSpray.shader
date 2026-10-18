Shader "Teyvat/URP/ImpactSpray"
{
    Properties
    {
        [MainColor] _BaseColor("Droplet color",Color)=(.86,.98,.98,1)
        _Opacity("Opacity",Range(0,1))=.72
        _Gravity("Local downward gravity",Float)=9.81
        _Size("UV2 size multiplier",Float)=1
        _Stretch("Vertical droplet elongation",Range(.5,4))=1.6
        _Speed("Shared-clock speed multiplier",Float)=1
        _DepthFade("Intersection fade metres",Float)=.25
        _UseSceneDepth("Use depth fade",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+15" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            Name "ForwardImpactSpray"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SprayVertex
            #pragma fragment SprayFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "TG_WaterProcedural.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity,_Gravity,_Size,_Stretch,_Speed,_DepthFade,_UseSceneDepth;
            CBUFFER_END
            // Four vertices share emitter POSITION, velocity/lifetime TEXCOORD1,
            // and phase/size TEXCOORD2. Only signed billboard UV0 corners differ.
            // Velocity is object-local metres/second. Phase is normalized 0..1;
            // UV2.y is the world half-size in metres (starting values .05-.13).
            struct SprayAttributes
            {
                float4 positionOS:POSITION; float2 corner:TEXCOORD0;
                float4 velocityLife:TEXCOORD1; float2 phaseSize:TEXCOORD2;
                half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct SprayVaryings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
                float2 corner:TEXCOORD1; half4 color:TEXCOORD2; half lifeFade:TEXCOORD3;
                half fog:TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            SprayVaryings SprayVertex(SprayAttributes input)
            {
                SprayVaryings output=(SprayVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float lifetime=max(.08,input.velocityLife.w);
                float age=frac(_TG_Wind.w*_Speed/lifetime+input.phaseSize.x)*lifetime;
                float normalizedAge=age/lifetime;
                float3 offset=input.velocityLife.xyz*age+float3(0,-.5*_Gravity*age*age,0);
                float3 centerWS=TransformObjectToWorld(input.positionOS.xyz+offset);
                // Inverse-view columns face this camera without a camera object
                // per emitter. Size contracts before the looping emission resets.
                float3 cameraRight=UNITY_MATRIX_I_V._m00_m10_m20;
                float3 cameraUp=UNITY_MATRIX_I_V._m01_m11_m21;
                float halfSize=max(.001,input.phaseSize.y)*_Size*lerp(1,.62,normalizedAge);
                output.positionWS=centerWS+cameraRight*input.corner.x*halfSize+cameraUp*input.corner.y*halfSize*_Stretch;
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.corner=input.corner; output.color=input.color;
                output.lifeFade=smoothstep(0,.10,normalizedAge)*(1-smoothstep(.53,1,normalizedAge));
                output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 SprayFragment(SprayVaryings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius=length(input.corner);
                half shape=1-smoothstep(.48,1,radius);
                half alpha=shape*input.lifeFade*input.color.a*_BaseColor.a*_Opacity;
                if (_UseSceneDepth>.001)
                {
                    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    float sceneEye=TGWaterEyeDepth(raw);
                    float surfaceEye=-TransformWorldToView(input.positionWS).z;
                    alpha*=lerp(1,saturate((sceneEye-surfaceEye)/max(.01,_DepthFade)),_UseSceneDepth);
                }
                return half4(MixFog(_BaseColor.rgb*input.color.rgb,input.fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
