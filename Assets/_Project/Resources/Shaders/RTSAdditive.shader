// Unlit additive vertex-colour shader for muzzle flashes, explosions, beams and tracers.
Shader "RTS/Additive"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
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
            float4 _Tint;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = float4(ToLin(v.color.rgb), v.color.a) * _Tint; return o; }
            float4 frag (v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
    Fallback Off
}
