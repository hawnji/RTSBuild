// Animated translucent water surface. Vertex colours carry depth tint; waves are a cheap
// procedural ripple in the fragment stage. Fog of war darkens water like terrain.
Shader "RTS/Water"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,0.82) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float3 ToLin(float3 c)
            {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }
            sampler2D _RTSFogTex;
            float4 _RTSFogParams;
            float4 _RTSSun;
            float4 _Tint;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float3 wpos : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                wp.y += sin(wp.x * 1.3 + _Time.y * 1.4) * 0.03 + cos(wp.z * 1.1 + _Time.y) * 0.03;
                o.pos = mul(UNITY_MATRIX_VP, wp);
                o.wpos = wp.xyz;
                o.color = float4(ToLin(v.color.rgb), v.color.a) * _Tint;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float r = sin(i.wpos.x * 3.1 + i.wpos.z * 1.7 + _Time.y * 2.2) * sin(i.wpos.z * 2.3 - _Time.y * 1.6);
                float3 c = i.color.rgb * (0.85 + _RTSSun.rgb * 0.15) + saturate(r - 0.6) * 0.35;
                if (_RTSFogParams.z > 0.5) c *= tex2D(_RTSFogTex, i.wpos.xz * _RTSFogParams.xy).r;
                return float4(c, i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
