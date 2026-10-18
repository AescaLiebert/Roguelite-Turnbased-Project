Shader "Fighting Allstar/Actor Heal Particles"
{
    Properties
    {
        _Shape ("Shape (0=Aura Wisp, 1=Needle/Streak, 2=Heal Cross, 3=Sparkle Star, 4=Ground Ring)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float _Shape;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 q = i.uv * 2.0 - 1.0;
                float mask = 0.0;
                float highlight = 0.0;

                if (_Shape < 0.5) // Shape 0: Soft Vertical Aura Column / Luminous Wisp
                {
                    float r = length(q * float2(1.0, 0.45));
                    mask = exp(-r * r * 2.0) * saturate(1.1 - r * 0.8);
                    highlight = exp(-r * r * 5.2) * 0.85;
                }
                else if (_Shape < 1.5) // Shape 1: Ascending Energy Streaks / Needles
                {
                    float x = abs(q.x);
                    float y = abs(q.y);
                    float width = 0.32 * saturate(1.0 - y * y);
                    mask = (1.0 - smoothstep(width * 0.15, width, x)) * (1.0 - smoothstep(0.15, 0.98, y));
                    highlight = (1.0 - smoothstep(0.015, 0.12, x)) * (1.0 - smoothstep(0.05, 0.85, y));
                }
                else if (_Shape < 2.5) // Shape 2: Radiant Genshin-style Healing Cross (+) Symbol
                {
                    float armX = max(abs(q.x) - 0.22, abs(q.y) - 0.74);
                    float armY = max(abs(q.x) - 0.74, abs(q.y) - 0.22);
                    float d = min(armX, armY);
                    mask = 1.0 - smoothstep(-0.06, 0.04, d);

                    // Glowing hot lime core inside the cross arms
                    float coreX = max(abs(q.x) - 0.10, abs(q.y) - 0.60);
                    float coreY = max(abs(q.x) - 0.60, abs(q.y) - 0.10);
                    float coreD = min(coreX, coreY);
                    highlight = (1.0 - smoothstep(-0.04, 0.03, coreD)) * 0.85;

                    // Soft outer glow around the cross
                    float softGlow = exp(-max(0.0, d) * 10.0) * 0.4;
                    mask = max(mask, softGlow);
                }
                else if (_Shape < 3.5) // Shape 3: 4-Point Diamond Sparkle / Star
                {
                    float r = length(q);
                    float star = max(0.0, 1.0 - pow(abs(q.x * 1.8), 0.55) - pow(abs(q.y * 1.8), 0.55));
                    float coreCircle = exp(-r * r * 14.0);
                    mask = max(star, coreCircle * 0.85);
                    highlight = coreCircle * 0.95;
                }
                else // Shape 4: Ground Energy Ring & Radiant Base Pool
                {
                    float r = length(q);
                    float ring = exp(-pow((r - 0.70) * 5.2, 2.0));
                    float innerDisc = exp(-r * r * 3.2) * 0.55;
                    mask = max(ring, innerDisc);
                    highlight = exp(-pow((r - 0.70) * 9.0, 2.0)) * 0.65 + exp(-r * r * 7.5) * 0.7;
                }

                // Hot lime/pale-yellow core blend for luminous AAA anime aesthetic
                fixed3 coreColor = fixed3(0.92, 1.0, 0.75);
                fixed3 finalColor = lerp(i.color.rgb, coreColor, highlight);

                // Additive blend output: alpha modulates intensity
                float finalAlpha = saturate(i.color.a * mask);
                return fixed4(finalColor * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
}
