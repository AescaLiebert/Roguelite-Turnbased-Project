Shader "Teyvat/Foundation Water Preview"
{
    Properties
    {
        _Color ("Deep color", Color) = (0.05,0.5,0.65,1)
        _Shallow ("Shallow color", Color) = (0.2,0.76,0.76,1)
        _Foam ("Foam", Color) = (0.85,0.97,0.97,1)
        _Mode ("River 0 / waterfall 1", Float) = 0
        _MainTex ("Coordinate carrier", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _Shallow, _Foam;
        float _Mode;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        void vert(inout appdata_full v) { }
        void surf(Input IN, inout SurfaceOutput o)
        {
            float2 uv=IN.uv_MainTex;
            float2 p=IN.worldPos.xz;
            float waves=sin(p.x*1.1+p.y*2.1+_Time.y*1.8)*.5
                +sin(p.x*.73-p.y*1.24+sin(p.x*.3)*.5+_Time.y*.8)*.3
                +sin(p.x*4.1+p.y*.23+_Time.y*1.1)*.2;
            float glints=pow(saturate(waves),12)*.8;
            float edge=pow(saturate(abs(uv.x-.5)*2),14);
            float bankFoam=smoothstep(.64,.88,edge+waves*.13);
            float streak=pow(saturate(sin(uv.x*86+sin(uv.y*31-_Time.y*2)*.7)),6);
            float fallFoam=streak*.55+smoothstep(.82,1,uv.y)*.38;
            float blend=saturate(edge*.5+waves*.10+.12);
            fixed3 color=lerp(_Color.rgb,_Shallow.rgb,blend);
            color=lerp(color,_Foam.rgb,saturate(lerp(bankFoam*.75+glints*.5,fallFoam,_Mode)));
            o.Albedo=color*.82;o.Emission=color*.25;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
