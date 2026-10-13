Shader "OsakaNight/Neon"
{
    Properties { _MainTex("Sign / lens",2D)="white"{} _Color("Color",Color)=(1,1,1,1) _Glow("Luminance",Float)=1.6 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_ST,_Color;float _Glow;
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1) };
            v2f vert(appdata v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);UNITY_TRANSFER_FOG(o,o.pos);return o; }
            half4 frag(v2f i):SV_Target { half4 c=tex2D(_MainTex,i.uv)*_Color;c.rgb*=_Glow;UNITY_APPLY_FOG(i.fogCoord,c);return c; }
            ENDCG
        }
    }
    Fallback Off
}
