#ifndef TEYVAT_SHARED_MATERIALS_INCLUDED
#define TEYVAT_SHARED_MATERIALS_INCLUDED
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
    half shadow=lerp(.48h,1.0h,sun.shadowAttenuation);
    half3 ambient=max(SampleSH(normalWS),half3(.12,.15,.15));
    half3 key=sun.color*(diffuse*.72h+.08h)*shadow;
    half back=pow(saturate(dot(-sun.direction,viewDir)),3)*transmission*saturate(1-ndl)*sun.shadowAttenuation;
    return albedo*(ambient*.8h+key)+albedo*sun.color*back*.35h;
}
#endif
