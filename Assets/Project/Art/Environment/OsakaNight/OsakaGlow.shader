Shader "Hidden/OsakaNight/Glow"
{
    Properties { _MainTex("Source",2D)="white"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _Bloom;
        float4 _MainTex_TexelSize, _Direction;
        float _Threshold, _Strength;
        half4 extract(v2f_img i):SV_Target
        {
            half4 c=tex2D(_MainTex,i.uv);
            half peak=max(c.r,max(c.g,c.b));
            return half4(c.rgb*max(0,peak-_Threshold)/max(peak,.001),1);
        }
        half4 blur(v2f_img i):SV_Target
        {
            half4 c=tex2D(_MainTex,i.uv)*.227027;
            c+=(tex2D(_MainTex,i.uv+_Direction.xy*1.384615)+tex2D(_MainTex,i.uv-_Direction.xy*1.384615))*.316216;
            c+=(tex2D(_MainTex,i.uv+_Direction.xy*3.230769)+tex2D(_MainTex,i.uv-_Direction.xy*3.230769))*.070270;
            return c;
        }
        half4 combine(v2f_img i):SV_Target
        {
            half4 c=tex2D(_MainTex,i.uv);
            c.rgb+=tex2D(_Bloom,i.uv).rgb*_Strength;
            // Soft shoulder retains saturated lettering; only very bright highlights compress.
            c.rgb=c.rgb/(1+max(0,c.rgb-1)*.65);
            return c;
        }
        ENDCG
        Pass { CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment extract
        ENDCG }
        Pass { CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment blur
        ENDCG }
        Pass { CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment combine
        ENDCG }
    }
    Fallback Off
}
