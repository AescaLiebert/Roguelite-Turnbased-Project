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
                else if (_Shape < 4.5) // Small cross spark.
                {
                    mask=pow(saturate(1-length(q)),2);
                    mask=max(mask,.5*(1-smoothstep(.045,.095,min(abs(q.x),abs(q.y))))*(1-smoothstep(.3,.9,max(abs(q.x),abs(q.y)))));
                }
                else if (_Shape < 5.5) // Vertical, curved 2D aura ribbon with a pale inner streak.
                {
                    float x=abs(q.x+.3*sin(q.y*3.4));
                    float width=.2+.18*(1-q.y*q.y);
                    mask=(1-smoothstep(width*.45,width,x))*pow(saturate(1-abs(q.y)),.4);
                    highlight=(1-smoothstep(.04,.13,x))*.65;
                }
                else if (_Shape < 6.5) // Card silhouette, pale rim, and an unmistakable crossed-card seal.
                {
                    float edge=max(abs(q.x)/.64,abs(q.y)/.86);
                    mask=1-smoothstep(.96,1,edge);
                    float cross=(1-smoothstep(.075,.11,min(abs(q.x-q.y*.65),abs(q.x+q.y*.65))))*step(abs(q.y),.58);
                    highlight=max(smoothstep(.8,.88,edge),cross);
                    i.color.rgb*=.5;
                }
                else if (_Shape < 7.5) // Solid healing cross.
                {
                    mask=(1-smoothstep(.2,.25,min(abs(q.x),abs(q.y))))*(1-smoothstep(.65,.73,max(abs(q.x),abs(q.y))));
                    highlight=.6;
                }
                else if (_Shape < 8.5) // Warm bomb marker: round dark body, bright rim, sparking fuse.
                {
                    float r=length(q-float2(0,-.19));
                    float body=1-smoothstep(.6,.64,r);
                    float fuse=(1-smoothstep(.06,.1,abs(q.x-.1*sin(q.y*5))))*step(.38,q.y)*step(q.y,.8);
                    float spark=(1-smoothstep(.04,.1,min(abs(q.x),abs(q.y-.8))))*(1-smoothstep(.12,.23,length(q-float2(0,.8))));
                    mask=max(body,max(fuse,spark));
                    highlight=max(smoothstep(.48,.56,r)*body,max(fuse,spark));
                    i.color.rgb*=.28;
                }
                else if (_Shape < 9.5) // Detonation shard/star, with a sharp luminous core.
                {
                    float r=length(q); float a=atan2(q.y,q.x);
                    float edge=.3+.5*pow(abs(cos(a*3)),5);
                    mask=1-smoothstep(edge-.05,edge,r);
                    highlight=1-smoothstep(.05,.28,r);
                }
                else // Paired chevrons. Direction reverses through particle rotation for Extort down.
                {
                    float d=min(abs(q.y-(.45-abs(q.x)*.8)),abs(q.y-(-.18-abs(q.x)*.8)));
                    mask=(1-smoothstep(.07,.13,d))*(1-smoothstep(.6,.7,abs(q.x)));
                    highlight=.3;
                }
                fixed3 color=lerp(i.color.rgb,fixed3(1,.97,.76),highlight);
                return fixed4(color, i.color.a*mask);
            }
            ENDCG
        }
    }
}
