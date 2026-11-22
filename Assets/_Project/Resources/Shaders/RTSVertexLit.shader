// Opaque vertex-colour shader used for terrain, units and structures.
// Written as a plain CG pass without lighting-mode tags so it renders identically under the
// Universal Render Pipeline (as SRPDefaultUnlit) and the built-in pipeline. Lighting is a simple
// directional lambert term driven by global properties set from LightingRig.cs, and fog of war is
// sampled from a global texture written by FogTexture.cs.
Shader "RTS/VertexLit"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _FogAmount ("Fog Of War Amount", Range(0,1)) = 1
        _Emission ("Emission", Range(0,1)) = 0
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
            #pragma multi_compile_instancing
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
            float4 _RTSFogParams;   // x = 1/width, y = 1/height, z = enabled
            float4 _RTSLightDir;    // towards the light
            float4 _RTSAmbient;
            float4 _RTSSun;
            float _FogAmount;
            float _Emission;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float3 wpos : TEXCOORD0; float3 n : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.color = float4(ToLin(v.color.rgb), v.color.a);
                return o;
            }

            float4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
                float3 n = normalize(i.n) * (facing > 0 ? 1 : -1);
                float3 L = normalize(_RTSLightDir.xyz + float3(0.0001, 0, 0));
                float lam = saturate(dot(n, L));
                float3 amb = _RTSAmbient.rgb + max(0, n.y) * 0.08;
                float3 c = i.color.rgb * tint.rgb;
                float3 lit = c * (amb + _RTSSun.rgb * lam) + c * _Emission;
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
