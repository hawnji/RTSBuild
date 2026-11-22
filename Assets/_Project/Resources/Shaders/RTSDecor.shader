// Scenery (trees, rocks, ore, bridges). Like RTS/VertexLit, plus: vertex alpha from 0.5 to 1 is a wind-sway weight
// (foliage moves, trunks do not) and alpha below 0.5 glows (aurite crystals, ore vents).
Shader "RTS/Decor"
{
    Properties
    {
        _Wind ("Wind Strength", Range(0, 3)) = 1
        _FogAmount ("Fog Of War Amount", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
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
            float4 _RTSLightDir;
            float4 _RTSAmbient;
            float4 _RTSSun;
            float _Wind;
            float _FogAmount;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float3 wpos : TEXCOORD0; float3 n : TEXCOORD1; float glow : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                float a = v.color.a;
                float sway = a >= 0.5 ? (1 - a) * 2 : 0;
                float t = _Time.y;
                float gust = 0.6 + 0.4 * sin(t * 0.35 + wp.x * 0.05);
                wp.x += (sin(t * 1.9 + wp.x * 0.7 + wp.z * 0.3) * 0.6 + sin(t * 3.7 + wp.z * 1.3) * 0.25) * sway * 0.04 * _Wind * gust;
                wp.z += cos(t * 1.6 + wp.z * 0.6 + wp.x * 0.2) * sway * 0.03 * _Wind * gust;
                o.pos = mul(UNITY_MATRIX_VP, wp);
                o.wpos = wp.xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.color = float4(ToLin(v.color.rgb), 1);
                o.glow = a < 0.5 ? (0.5 - a) * 8 * (0.8 + 0.2 * sin(t * 2.5 + wp.x * 1.7 + wp.z)) : 0;
                return o;
            }

            float4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.n) * (facing > 0 ? 1 : -1);
                float3 L = normalize(_RTSLightDir.xyz + float3(0.0001, 0, 0));
                float lam = saturate(dot(n, L));
                float3 amb = _RTSAmbient.rgb + max(0, n.y) * 0.08;
                float3 c = i.color.rgb;
                float3 lit = c * (amb + _RTSSun.rgb * lam) + c * i.glow;
                float fog = 1;
                if (_RTSFogParams.z > 0.5) fog = tex2D(_RTSFogTex, i.wpos.xz * _RTSFogParams.xy).r;
                lit *= lerp(1, fog, _FogAmount);
                return float4(lit, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
