Shader "Teyvat/URP/PaintedSky"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Cloud-shape panorama",2D)="white"{}
        _Rotation("Panorama rotation degrees",Range(0,360))=0
        _Exposure("Sky exposure",Range(0,4))=1
        _ZenithColor("Zenith blue",Color)=(.16,.40,.74,1)
        _HorizonColor("Horizon haze",Color)=(.65,.77,.85,1)
        _CloudShadeColor("Cloud shaded side",Color)=(.66,.78,.90,1)
        _CloudSunColor("Cloud sunlit side",Color)=(1,.94,.82,1)
        _CloudAmount("Cloud amount",Range(0,1))=1
        _SunSize("Sun angular radius degrees",Range(.05,3))=.7
        _SunGlow("Sun glow strength",Range(0,1))=.12
        _CloudDrift("Cloud rotation degrees per second",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            Name "PaintedSky"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            // The lighting owner publishes the direction TOWARD the sun, matching
            // the main light direction in shading. The optional drift uses the
            // same shared clock as foliage and water; no independent animation.
            float4 _TG_SunDirection;
            float4 _TG_Wind;
            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor,_HorizonColor,_CloudShadeColor,_CloudSunColor;
                float _Rotation,_Exposure,_CloudAmount,_SunSize,_SunGlow,_CloudDrift;
            CBUFFER_END
            struct SkyAttributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SkyVaryings { float4 positionCS:SV_POSITION; float3 directionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            SkyVaryings SkyVertex(SkyAttributes input)
            {
                SkyVaryings output=(SkyVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.directionWS=TransformObjectToWorldDir(input.positionOS.xyz,false);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                // Keep sky at the far plane for both ordinary and reversed Z.
                #if UNITY_REVERSED_Z
                    output.positionCS.z=0;
                #else
                    output.positionCS.z=output.positionCS.w;
                #endif
                return output;
            }
            half4 SkyFragment(SkyVaryings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction=normalize(input.directionWS);
                float angle=radians(_Rotation+_TG_Wind.w*_CloudDrift);
                float sine,cosine; sincos(angle,sine,cosine);
                float3 sampleDirection=float3(cosine*direction.x-sine*direction.z,direction.y,sine*direction.x+cosine*direction.z);
                // Matches Unity's panoramic skybox radial convention. U repeats
                // at the seam and V clamps, as configured by the source texture.
                float2 uv=float2(.5-atan2(sampleDirection.z,sampleDirection.x)*(1.0/(2*PI)),1-acos(clamp(sampleDirection.y,-1,1))/PI);
                half3 panorama=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb;
                half whiteMask=min(panorama.r,min(panorama.g,panorama.b));
                // Separate cloud whites from the panorama's bright cyan sky;
                // brightness alone mistakes its pale horizon for solid cloud.
                half cloudChroma=panorama.r/max(panorama.b,.001h);
                half clouds=smoothstep(.18h,.70h,cloudChroma)*saturate(_CloudAmount);
                half height=saturate(direction.y);
                half3 sky=lerp(_HorizonColor.rgb,_ZenithColor.rgb,pow(height,.48h));
                float sunLength=dot(_TG_SunDirection.xyz,_TG_SunDirection.xyz);
                float3 sun=sunLength>.0001 ? _TG_SunDirection.xyz*rsqrt(sunLength) : normalize(float3(.3,.6,.7));
                half sunward=smoothstep(-.35h,.85h,dot(direction,sun));
                // Keep the source cloud's full color/value modelling rather
                // than replacing it with a thresholded flat cloud color.
                half luminance=dot(panorama,half3(.2126h,.7152h,.0722h));
                half cloudLight=saturate(smoothstep(.16h,.95h,luminance)*.88h+sunward*.12h);
                half3 cloudTint=lerp(_CloudShadeColor.rgb,_CloudSunColor.rgb,cloudLight);
                half3 cloud=panorama*lerp(half3(1,1,1),cloudTint,.12h);
                half3 color=lerp(sky,cloud,clouds);
                float alignment=clamp(dot(direction,sun),-1,1);
                float radius=radians(max(.05,_SunSize));
                float disk=smoothstep(cos(radius*1.15),cos(radius*.80),alignment);
                float glow=pow(saturate(alignment),72)*_SunGlow;
                color+=_CloudSunColor.rgb*(disk*.72+glow)*(1-clouds*.82);
                return half4(max(half3(0,0,0),color)*max(0,_Exposure),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
