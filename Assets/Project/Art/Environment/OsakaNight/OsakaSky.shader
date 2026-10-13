Shader "OsakaNight/Sky"
{
    Properties
    {
        _Zenith("Zenith",Color)=(.018,.012,.07,1)
        _Horizon("Horizon",Color)=(.13,.04,.16,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Zenith,_Horizon;
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 pos:SV_POSITION; float3 dir:TEXCOORD0; };
            v2f vert(appdata v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o; }
            half4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.dir);
                float t=pow(saturate(d.y),.48);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,t);
                float2 cell=floor(d.xz/(abs(d.y)+.2)*160);
                float star=step(.9978,frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453));
                color+=star*smoothstep(.15,.7,d.y)*.12;
                return float4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
