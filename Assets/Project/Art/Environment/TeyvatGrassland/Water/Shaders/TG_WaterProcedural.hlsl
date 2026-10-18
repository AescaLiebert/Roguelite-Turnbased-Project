#ifndef TG_WATER_PROCEDURAL_INCLUDED
#define TG_WATER_PROCEDURAL_INCLUDED
#include "../../Shared/Shaders/TG_Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float TGWaterHash(float2 coordinate)
{
    float3 value = frac(float3(coordinate.xyx) * .1031);
    value += dot(value, value.yzx + 33.33);
    return frac((value.x + value.y) * value.z);
}

float TGWaterNoise(float2 coordinate)
{
    float2 cell = floor(coordinate), weight = frac(coordinate);
    weight = weight * weight * (3 - 2 * weight);
    return lerp(lerp(TGWaterHash(cell), TGWaterHash(cell + float2(1, 0)), weight.x),
        lerp(TGWaterHash(cell + float2(0, 1)), TGWaterHash(cell + 1), weight.x), weight.y);
}

float TGWaterCloud(float2 coordinate, float quality)
{
    float noise = TGWaterNoise(coordinate);
    if (quality > .5) noise = noise * .68 + TGWaterNoise(coordinate * 2.13 + float2(5.7, 2.4)) * .32;
    return noise;
}

float TGWaterEyeDepth(float rawDepth)
{
    // Sky depth is finite and monotonic even with reversed Z or orthographic
    // review cameras. Never let an empty-sky sample create a NaN foam edge.
    #if UNITY_REVERSED_Z
        if (rawDepth <= 0.000001) return _ProjectionParams.z;
    #else
        if (rawDepth >= 0.999999) return _ProjectionParams.z;
    #endif
    return unity_OrthoParams.w > .5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
}
#endif
