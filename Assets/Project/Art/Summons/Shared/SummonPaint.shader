Shader "FightingAllstar/SummonPaint"
{
    Properties
    {
        _BaseMap("Painted atlas",2D)="white"{}
        _MaskMap("R emission G opacity",2D)="black"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Emission("Emission strength",Float)=0
        _Visibility("Visibility",Range(0,1))=1
        _Pulse("Absorb pulse",Range(0,1))=0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=0
        [Toggle] _ZWrite("Depth write",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float4 _BaseMap_ST;
            float _Emission,_Visibility,_Pulse;
            CBUFFER_END
            struct A { float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct V { float4 p:SV_POSITION;float3 world:TEXCOORD1;float3 n:TEXCOORD2;float2 uv:TEXCOORD0;float4 color:COLOR; };
            V vert(A i) { V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);
                o.n=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.color=i.color;return o; }
            half4 frag(V i):SV_Target
            {
                float noise=frac(sin(dot(floor(i.world*90),float3(12.9898,78.233,37.719)))*43758.5453);
                clip(_Visibility-noise-.00001);
                half4 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                half emission=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv).r;
                Light light=GetMainLight();
                half shade=.68+.32*saturate(dot(normalize(i.n),light.direction));
                // Broad painted values remain readable under both sides' battle cameras.
                half3 color=paint.rgb*shade + paint.rgb*emission*_Emission + _Pulse*half3(.8,.60,.20);
                color*=i.color.rgb;
                return half4(color,paint.a*i.color.a);
            }
            ENDHLSL
        }
    }
}
