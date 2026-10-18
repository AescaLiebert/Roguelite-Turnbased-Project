#ifndef TEYVAT_REFERENCE_ROCK_INCLUDED
#define TEYVAT_REFERENCE_ROCK_INCLUDED

// Original TG_RockPainted.png, sampled as sRGB albedo. Its measured linear RGB
// mean is (.17163,.18891,.21022). Normalize this quiet mid-grey texture around
// one so the authored rock tint controls average albedo, while painted strata
// remain visible. This is surface color only: no baked AO or invented normals.
half3 TGReferenceRock(half3 paint, half localStrength)
{
    half3 relativePaint = paint / half3(.17163h, .18891h, .21022h);
    half3 variation = clamp(1.0h + (relativePaint - 1.0h) * 1.20h, .50h, 1.65h);
    half amount = saturate(_TG_RockStyle.a) * saturate(localStrength);
    return _TG_RockStyle.rgb * lerp(half3(1,1,1), variation, amount);
}

#endif
