Shader "Fighting Allstar/Ultimate Ready Aura"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1.0, 0.78, 0.08, 1.0)
        _CoreColor ("Core Color", Color) = (1.3, 1.25, 0.9, 1.0)
        _RimColor ("Rim Color", Color) = (0.85, 1.0, 0.22, 1.0)
        _AccentColor ("Accent Color", Color) = (1.0, 0.45, 0.05, 1.0)
        _ScrollSpeed ("Scroll Speed", Float) = 2.4
        _Turbulence ("Turbulence", Float) = 1.0
        _Mode ("Mode (0=Ground, 1=Geyser, 2=Crest, 3=Streak)", Float) = 0
        _CustomTime ("Custom Time", Float) = -1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend One One
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _TintColor;
            float4 _CoreColor;
            float4 _RimColor;
            float4 _AccentColor;
            float _ScrollSpeed;
            float _Turbulence;
            float _Mode;
            float _CustomTime;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float mode = _Mode;
                float animTime = _CustomTime >= 0.0 ? _CustomTime : _Time.y;
                float t = animTime * _ScrollSpeed;

                // MODE 0: GROUND DISC / ENERGY POOL
                if (mode < 0.5)
                {
                    float2 p = i.uv * 2.0 - 1.0;
                    float r = length(p);
                    if (r > 1.0) discard;

                    float angle = atan2(p.y, p.x);

                    // Core intense white-gold glow directly under feet
                    float core = pow(saturate(1.0 - r * 1.5), 1.8) * 2.5;

                    // Rotating radiant sunburst energy rays
                    float rays1 = 0.5 + 0.5 * sin(angle * 12.0 + t * 2.4);
                    float rays2 = 0.5 + 0.5 * cos(angle * 8.0 - t * 1.8);
                    float rayPattern = (rays1 * 0.6 + rays2 * 0.4) * saturate(1.0 - r * 0.85);

                    // Concentric pulsing shockwave ripples
                    float ring1 = sin(r * 22.0 - t * 5.0) * 0.5 + 0.5;
                    float ring2 = sin(r * 36.0 - t * 8.0) * 0.5 + 0.5;
                    float shockwave = (ring1 * 0.6 + ring2 * 0.4) * smoothstep(0.12, 0.45, r) * (1.0 - smoothstep(0.72, 0.98, r));

                    // Sharp luminous outer perimeter ring
                    float perimeter = exp(-pow((r - 0.82) * 14.0, 2.0)) * 2.0;

                    // Overall base pool soft falloff
                    float basePool = smoothstep(0.98, 0.2, r);

                    float totalEnergy = (basePool * 0.7 + core + rayPattern * 0.5 + shockwave * 0.4 + perimeter) * i.color.a;

                    float3 col = lerp(_TintColor.rgb, _CoreColor.rgb, saturate(core * 0.5 + perimeter * 0.4));
                    col += _AccentColor.rgb * (shockwave * 0.4);
                    col += _RimColor.rgb * (rayPattern * 0.35);

                    return fixed4(col * totalEnergy, 1.0);
                }

                // MODE 1: FLAME SHEET / GEYSER (Backdrop arc and low base skirt)
                else if (mode < 1.5)
                {
                    float u = i.uv.x;
                    float v = i.uv.y;

                    // Smooth edge fade on sides of the arc
                    float edgeFade = saturate(u * 5.0) * saturate((1.0 - u) * 5.0);

                    // Multi-frequency sine wave turbulence for roaring anime flames
                    float w1 = sin(u * 6.28318 * 3.5 + t * 4.2 + v * 4.5);
                    float w2 = sin(u * 6.28318 * 6.5 - t * 6.2 + v * 7.5) * 0.5;
                    float w3 = sin(u * 6.28318 * 2.0 + t * 2.2 - v * 2.5) * 0.7;
                    float flameTurb = (w1 + w2 + w3) * _Turbulence * 0.28;

                    // Upward scrolling flame tongues
                    float tongue = (1.0 - v * 0.85) + flameTurb * (1.0 - v * 0.35);
                    float flameMask = smoothstep(0.18, 0.72, tongue) * edgeFade;
                    flameMask *= saturate(v * 6.5) * saturate((1.0 - v) * 3.2);

                    // Core hot streams
                    float core = pow(saturate(tongue - 0.45), 2.5) * 1.6;
                    float rim = smoothstep(0.30, 0.45, tongue) * (1.0 - smoothstep(0.45, 0.65, tongue));

                    float3 col = lerp(_TintColor.rgb, _CoreColor.rgb, saturate(core));
                    col += _RimColor.rgb * (rim * 1.2);
                    col = lerp(col, _AccentColor.rgb, saturate((1.0 - v) * 0.35));

                    float totalIntensity = (flameMask + core * 0.6) * i.color.a;
                    return fixed4(col * totalIntensity, 1.0);
                }

                // MODE 2: SWEEPING FLAME CRESTS / WINGS (Asymmetric roaring flame wings)
                else if (mode < 2.5)
                {
                    float u = i.uv.x; // 0..1 across width
                    float v = i.uv.y; // 0..1 from bottom to top
                    float x = (u - 0.5) * 2.0; // -1..1 centered

                    // Multi-frequency wave turbulence moving upwards
                    float w1 = sin(u * 5.0 + t * 4.5 + v * 6.0);
                    float w2 = sin(u * 9.0 - t * 6.5 + v * 10.0) * 0.5;
                    float w3 = sin(u * 3.0 + t * 2.5 - v * 3.0) * 0.8;
                    float flameTurb = (w1 + w2 + w3) * 0.28 * _Turbulence;

                    // Natural flame silhouette: wide in lower-mid, tapered at top
                    float edgeDist = abs(x) + flameTurb * saturate(v * 1.5);
                    float widthMask = 1.0 - smoothstep(0.45, 0.98, edgeDist);

                    // Upward flame erosion
                    float tongueErosion = (1.0 - v * 0.85) + flameTurb * 0.4;
                    float flameMask = widthMask * smoothstep(0.16, 0.62, tongueErosion);

                    // Fade smoothly at base & top
                    flameMask *= saturate(v * 6.0) * saturate((1.0 - v) * 3.5);

                    // White-hot core stream down the center of the flame wing
                    float coreStream = (1.0 - smoothstep(0.0, 0.32, abs(x + flameTurb * 0.5))) * saturate(v * 4.0) * (1.0 - v * 0.45);

                    // Outer flame rim fringe
                    float rimFringe = smoothstep(0.35, 0.65, edgeDist) * (1.0 - smoothstep(0.65, 0.95, edgeDist));

                    // Build dynamic color
                    float3 col = lerp(_TintColor.rgb, _CoreColor.rgb, saturate(coreStream * 1.4));
                    col += _RimColor.rgb * (rimFringe * 1.3 + i.color.g * 1.4);
                    col = lerp(col, _AccentColor.rgb, saturate(i.color.r * 0.9 + (1.0 - coreStream) * 0.35));

                    float totalIntensity = (flameMask + coreStream * 0.8) * i.color.a;
                    return fixed4(col * totalIntensity, 1.0);
                }

                // MODE 3: ASCENDING STREAKS & PARTICLES
                else
                {
                    float2 p = i.uv * 2.0 - 1.0;
                    float streak = (1.0 - smoothstep(0.04, 0.45, abs(p.x))) * saturate(1.0 - p.y * p.y);
                    float core = (1.0 - smoothstep(0.01, 0.10, abs(p.x))) * saturate(1.0 - abs(p.y));

                    float3 col = lerp(_TintColor.rgb, _CoreColor.rgb, core);
                    col += _RimColor.rgb * 0.4;

                    return fixed4(col * (streak + core * 0.8) * i.color.a, 1.0);
                }
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
