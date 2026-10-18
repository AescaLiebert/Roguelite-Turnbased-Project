Shader "Teyvat/URP/ImpactFoam"
{
    Properties
    {
        [MainColor] _BaseColor("Foam / mist color", Color) = (.86,.97,.95,1)
        _Opacity("Effect opacity", Range(0,1)) = .78
        _FlowSpeed("Shared-clock flow speed", Float) = .32
        _DepthFade("Intersection fade metres", Float) = .25
        _UseSceneDepth("Use opaque intersection depth fade", Range(0,1)) = 1
        _NoiseScale("Irregular patch scale", Float) = 3
        _Softness("Patch edge softness", Range(.02,.8)) = .20
        [Enum(Fallback,0,Full,1)] _Quality("Procedural quality", Float) = 1
        [Enum(Impact,0,Downstream,1,Mist,2,Fringe,3)] _EffectMode("Effect family", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            Name "ForwardImpactFoam"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FoamVertex
            #pragma fragment FoamFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "TG_WaterProcedural.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity, _FlowSpeed, _DepthFade, _UseSceneDepth;
                float _NoiseScale, _Softness, _Quality, _EffectMode;
            CBUFFER_END

            // UV0 is the actual normalized patch, not the fall's arc-length UV.
            // +V is the impact/back edge; advected fragments travel toward -V.
            // Fringe mode instead uses a vertical fan: V0 foot, V1 upper edge.
            // Vertex R decorrelates patch phase, A controls authored opacity.
            struct FoamAttributes
            {
                float4 positionOS:POSITION;
                float2 uv:TEXCOORD0;
                half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct FoamVaryings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float2 uv:TEXCOORD1;
                half2 phaseOpacity:TEXCOORD2;
                half fog:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            FoamVaryings FoamVertex(FoamAttributes input)
            {
                FoamVaryings output=(FoamVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv;
                output.phaseOpacity=input.color.ra;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 FoamFragment(FoamVaryings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.uv;
                float phase=input.phaseOpacity.x*11.73;
                float time=_TG_Wind.w*_FlowSpeed;
                float scale=max(.25,_NoiseScale);
                float softness=clamp(_Softness,.02,.8);
                float2 offset=float2(phase,phase*.37);

                // Advection is continuous: no frac-reset of the entire patch,
                // no concentric radius/ring wave and no extra animation clock.
                float2 flow=float2((uv.x-.5)*scale*1.7,
                                  (uv.y+time*.48)*scale*2.1)+offset;
                float broad=TGWaterCloud(flow,_Quality);
                float detail=broad;
                if (_Quality>.5)
                    detail=TGWaterNoise(flow*2.37+float2(7.1,-time*.21));

                float x=uv.x*2-1;
                // Broader toward the downstream/front edge, with unequal sides.
                float drift=(TGWaterNoise(float2(uv.y*3.1+phase,time*.23))-.5)*.21;
                float halfWidth=lerp(.98,.70,saturate(uv.y));
                half lateral=1-smoothstep(max(.05,halfWidth-softness*.65),
                                         halfWidth,abs(x+drift));
                half ends=smoothstep(0,max(.025,softness*.35),uv.y)
                         *(1-smoothstep(1-max(.025,softness*.35),1,uv.y));
                half patchEdge=lateral*ends;
                half alpha=0;
                if (_EffectMode<.5)
                {
                    // A broad churning strip at the landing, not a bright rim.
                    // Its meandering center and fragments break the symmetry.
                    float backCenter=.69+(TGWaterNoise(float2(uv.x*4.7+phase,time*.31))-.5)*.12;
                    float crossDistance=abs(uv.y-backCenter+(broad-.5)*.12);
                    half impactBand=1-smoothstep(.10,.28+softness*.10,crossDistance);
                    half churn=smoothstep(.24,.68,broad)*lerp(.64,1,detail);
                    half concentrated=smoothstep(.39,.58,uv.y);
                    // White turbulent mass stays near +V; sparse torn tails
                    // lead out into the river toward the front of the patch.
                    half fragments=smoothstep(.56,.74,broad)
                                  *smoothstep(.30,.64,detail)
                                  *(1-smoothstep(.43,.76,uv.y));
                    alpha=patchEdge*saturate(impactBand*concentrated*(.30+.78*churn)
                                           +fragments*.54);
                }
                else if (_EffectMode<1.5)
                {
                    // Narrow irregular islands of foam, elongated downstream.
                    float shear=(TGWaterNoise(float2(uv.y*2.8+phase,time*.19))-.5)*.35;
                    float2 stream=float2((uv.x+shear)*scale*3.8,
                                         (uv.y+time*.58)*scale*1.6)+offset;
                    float fragments=TGWaterCloud(stream,_Quality);
                    float holes=detail;
                    if (_Quality>.5)
                        holes=TGWaterNoise(stream*1.91+float2(-2.4,time*.17));
                    half pieces=smoothstep(.58,.74,fragments)*smoothstep(.34,.69,holes);
                    half downstreamFade=lerp(.30,1,smoothstep(.06,.72,uv.y));
                    alpha=patchEdge*pieces*downstreamFade;
                }
                else if (_EffectMode<2.5)
                {
                    float2 centered=(uv-.5)*2;
                    float distortedRadius=length(centered*float2(1,.88))+(broad-.5)*.12;
                    half cloudEdge=1-smoothstep(max(.08,1-softness),1,distortedRadius);
                    alpha=cloudEdge*exp(-dot(centered,centered)*1.15)
                         *lerp(.25,.62,broad);
                }
                else
                {
                    // A vertical broken foot of white water. Do not use the
                    // disk's front/back edge fade: V0 must join the impact mass.
                    float fringeTime=time*1.7;
                    float topNoise=TGWaterNoise(float2(uv.x*scale*4.3+phase,
                                                      fringeTime*.47));
                    float toothHeight=.32+.62*smoothstep(.22,.78,topNoise);
                    float2 turbulentUV=float2(uv.x*scale*3.1,
                                             uv.y*scale*1.5-fringeTime)+offset;
                    float turbulent=TGWaterCloud(turbulentUV,_Quality);
                    float small=turbulent;
                    if (_Quality>.5)
                        small=TGWaterNoise(turbulentUV*2.21+float2(3.4,fringeTime*.23));
                    half side=smoothstep(0,max(.025,softness*.30),min(uv.x,1-uv.x));
                    half top=1-smoothstep(toothHeight-.10,toothHeight+.055,uv.y);
                    half body=1-smoothstep(.15,.95,uv.y);
                    half churn=lerp(.72,1,smoothstep(.24,.70,turbulent));
                    // Irregular upward tips are strongest above the dense foot;
                    // the two noise scales prevent a row of identical teeth.
                    half tips=smoothstep(.48,.73,turbulent)*smoothstep(.28,.62,small)
                             *smoothstep(.16,.43,uv.y)*.48;
                    alpha=side*top*(1-smoothstep(.92,1,uv.y))
                         *saturate(body*churn+tips);
                }

                if (_UseSceneDepth>.001)
                {
                    float rawDepth=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    float sceneEye=TGWaterEyeDepth(rawDepth);
                    float surfaceEye=-TransformWorldToView(input.positionWS).z;
                    half fade=saturate((sceneEye-surfaceEye)/max(.01,_DepthFade));
                    alpha*=lerp(1,fade,saturate(_UseSceneDepth));
                }
                alpha*=saturate(input.phaseOpacity.y)*_BaseColor.a*_Opacity;
                return half4(MixFog(_BaseColor.rgb,input.fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
