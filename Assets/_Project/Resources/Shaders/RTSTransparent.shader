// Unlit alpha-blended vertex-colour shader for selection rings, health bars, placement previews,
// smoke and other transparent effects. Pipeline-agnostic (works in URP and built-in).
Shader "RTS/Transparent"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _FogAmount ("Fog Of War Amount", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
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
            float4 _Tint;
            float _FogAmount;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float3 wpos : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = float4(ToLin(v.color.rgb), v.color.a) * _Tint;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float4 c = i.color;
                if (_RTSFogParams.z > 0.5 && _FogAmount > 0) c.rgb *= lerp(1, tex2D(_RTSFogTex, i.wpos.xz * _RTSFogParams.xy).r, _FogAmount);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
