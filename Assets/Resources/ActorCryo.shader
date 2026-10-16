Shader "Fighting Allstar/Actor Cryo"
{
    Properties
    {
        _Tint ("Tint", Color) = (.4,.8,1,.5)
        _Mode ("Ice / Mist / Snow", Float) = 0
        _Phase ("Animation phase", Float) = 0
    }
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
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float3 view:TEXCOORD2; fixed4 color:COLOR; };
            fixed4 _Tint;
            float _Mode, _Phase;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.view=WorldSpaceViewDir(v.vertex); o.color=v.color*_Tint;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=i.color;
                if (_Mode<.5)
                {
                    float fresnel=pow(1-abs(dot(normalize(i.normal),normalize(i.view))),2);
                    float facet=saturate(dot(normalize(i.normal),normalize(float3(-.4,.8,-.6)))*.5+.5);
                    float edge=1-smoothstep(.01,.045,min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y)));
                    float vein=pow(saturate(1-abs(sin(i.uv.y*21+i.uv.x*10+noise(i.uv*8)*4))*.98),24);
                    c.rgb=lerp(c.rgb*.65,float3(.86,.98,1),facet*.48+fresnel*.4+edge*.3+vein*.3);
                    c.a*=saturate(.72+fresnel*.5+edge*.4+vein*.3);
                }
                else if (_Mode<1.5)
                {
                    float2 p=i.uv*2-1;
                    float cloud=noise(i.uv*4+float2(_Phase*.14,-_Phase*.09))*.65+noise(i.uv*9-_Phase*.1)*.35;
                    c.a*=smoothstep(1,.12,dot(p,p))*smoothstep(.2,.8,cloud);
                    c.rgb=lerp(c.rgb,float3(.93,.99,1),cloud*.7);
                }
                else
                {
                    float2 p=abs(i.uv*2-1);
                    float diamond=saturate(1-p.x-p.y);
                    c.a*=smoothstep(0,.3,diamond);
                    c.rgb=lerp(c.rgb,1,.6);
                }
                return c;
            }
            ENDCG
        }
    }
}
