Shader "Fighting Allstar/Actor Status Particles"
{
    Properties { _Shape ("Shape", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            float _Shape;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 q=i.uv*2-1;
                float mask=0, highlight=0;
                if (_Shape < .5) // Teardrop: rounded bulb, pointed tip, small wet highlight.
                {
                    float bulb=1-smoothstep(.45,.52,length(q-float2(0,-.34)));
                    float taper=(1-smoothstep(.02,.065,abs(q.x)-(.9-q.y)*.29))*step(-.2,q.y)*step(q.y,.9);
                    mask=max(bulb,taper);
                    highlight=(1-smoothstep(.07,.18,length(q-float2(-.15,-.28))))*.55;
                }
                else if (_Shape < 1.5) // Poison bubble, with a bright rim.
                {
                    float r=length(q); mask=1-smoothstep(.77,.9,r);
                    highlight=smoothstep(.5,.74,r)*.55;
                    mask*=lerp(.18,1,smoothstep(.52,.75,r));
                }
                else if (_Shape < 2.5) // Soft, irregular toxic wisp.
                {
                    float r=length(q*float2(.86,1.13));
                    mask=pow(saturate(1-r),1.4)*(.68+.32*sin(q.x*9+sin(q.y*7)));
                }
                else if (_Shape < 3.5) // Tapered flame with a pale hot core.
                {
                    float width=(1-q.y)*.4;
                    float x=abs(q.x+.13*sin(q.y*5));
                    mask=(1-smoothstep(width*.68,width+.055,x))*smoothstep(-1,-.65,q.y)*step(q.y,.96);
                    highlight=(1-smoothstep(width*.12,width*.55,x))*(1-smoothstep(.15,.8,q.y));
                }
                else // Small cross spark.
                {
                    mask=pow(saturate(1-length(q)),2);
                    mask=max(mask,.5*(1-smoothstep(.045,.095,min(abs(q.x),abs(q.y))))*(1-smoothstep(.3,.9,max(abs(q.x),abs(q.y)))));
                }
                fixed3 color=lerp(i.color.rgb,fixed3(1,.97,.76),highlight);
                return fixed4(color, i.color.a*mask);
            }
            ENDCG
        }
    }
}
