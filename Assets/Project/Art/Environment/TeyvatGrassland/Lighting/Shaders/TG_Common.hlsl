#ifndef TEYVAT_LIGHTING_MATERIALS_INCLUDED
#define TEYVAT_LIGHTING_MATERIALS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(_TG_ColorField); SAMPLER(sampler_TG_ColorField);
float4 _TG_ColorBounds;
float4 _TG_Wind;
float _TG_ColorFieldEnabled;
// Scene diagnostic only. Unlit albedo preserves every material's actual sampling
// and native terrain layer blending while removing lighting and fog differences.
float _TG_DebugAlbedo;
float4 _TG_RockStyle;
float _TG_RockWorldScale;
// WP08 scene-owned controls. Disabled mode retains the accepted earlier response
// exactly; the lighting owner publishes these globals before enabling the style.
float4 _TG_LightStyle; // ambient gain, key gain, shadow floor, transmission gain
float4 _TG_ShadowTint; // RGB multiplier and tint strength
float4 _TG_AmbientFloor; // linear RGB minimum; alpha reserved
float _TG_StyleEnabled;
float4 _TG_CloudShadow; // world scale, shared-clock drift, strength, enabled
float TGCloudHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float TGCloudNoise(float2 p)
{
    float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(TGCloudHash(a),TGCloudHash(a+float2(1,0)),f.x),lerp(TGCloudHash(a+float2(0,1)),TGCloudHash(a+1),f.x),f.y);
}

half3 TGStyleRock(half3 paint)
{
    return lerp(half3(1,1,1),paint,saturate(_TG_RockStyle.a))*_TG_RockStyle.rgb;
}

half3 TGColorField(float3 positionWS)
{
    float2 uv=saturate((positionWS.xz-_TG_ColorBounds.xy)*_TG_ColorBounds.zw);
    return lerp(half3(1,1,1),SAMPLE_TEXTURE2D_LOD(_TG_ColorField,sampler_TG_ColorField,uv,0).rgb,saturate(_TG_ColorFieldEnabled));
}

float3 TGDeform(float3 positionWS,float4 mask,float response,float family)
{
    float phase=dot(positionWS.xz,float2(.37,.23))+mask.b*6.283185;
    float slow=sin(_TG_Wind.w*1.3+phase);
    float flutter=sin(_TG_Wind.w*3.7+phase*2.1);
    float bend=mask.r*mask.r*(.65*slow+.15*flutter)+mask.g*.35*sin(_TG_Wind.w*.85+phase*.7);
    float distance=bend*_TG_Wind.z*response;
    positionWS.xz+=_TG_Wind.xy*distance;
    positionWS.y+=abs(distance)*.08*mask.r;
    return positionWS;
}

half3 TGShade(half3 albedo,float3 positionWS,half3 normalWS,half3 viewDir,half wrap,half transmission)
{
    if (_TG_DebugAlbedo > .5)
        return albedo;
    normalWS=normalize(normalWS);
    Light sun=GetMainLight(TransformWorldToShadowCoord(positionWS));
    half ndl=dot(normalWS,sun.direction);
    half diffuse=smoothstep(-.08h,.9h,saturate((ndl+wrap)/(1+wrap)));
    half back=pow(saturate(dot(-sun.direction,viewDir)),3)*transmission*saturate(1-ndl)*sun.shadowAttenuation;
    if (_TG_StyleEnabled > .5)
    {
        // Use the renderer's real SH input. This is diffuse ambient sampling,
        // without claiming APV, lightmap, reflection-probe or extra-light support.
        half3 ambient=max(SampleSH(normalWS),max(half3(0,0,0),_TG_AmbientFloor.rgb));
        float2 cloudUV=positionWS.xz*_TG_CloudShadow.x+float2(7.1,3.8)+_TG_Wind.w*_TG_CloudShadow.y*float2(.7,.3);
        half cloudShape=smoothstep(.32,.70,TGCloudNoise(cloudUV)*.7+TGCloudNoise(cloudUV*2.13+4.7)*.3);
        half cloudLight=1-cloudShape*saturate(_TG_CloudShadow.z)*saturate(_TG_CloudShadow.w);
        half attenuation=saturate(sun.shadowAttenuation)*cloudLight;
        half shadow=lerp(saturate(_TG_LightStyle.z),1.0h,attenuation);
        half3 coolShadow=lerp(half3(1,1,1),max(half3(0,0,0),_TG_ShadowTint.rgb),saturate(_TG_ShadowTint.a)*(1-attenuation));
        half3 key=sun.color*(diffuse*.72h+.08h)*shadow*coolShadow;
        return albedo*(ambient*.8h*max(0,_TG_LightStyle.x)+key*max(0,_TG_LightStyle.y))
            +albedo*sun.color*back*.35h*max(0,_TG_LightStyle.w);
    }
    half shadow=lerp(.48h,1.0h,sun.shadowAttenuation);
    half3 ambient=max(SampleSH(normalWS),half3(.12,.15,.15));
    half3 key=sun.color*(diffuse*.72h+.08h)*shadow;
    return albedo*(ambient*.8h+key)+albedo*sun.color*back*.35h;
}
#endif
