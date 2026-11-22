// Ground shader. Each cell's terrain type is read from a small point-sampled index texture (_RTSCellTex, one texel
// per cell) and the four nearest cells are blended with a height-based blend, so borders between ground types are
// soft and irregular instead of square. Detail comes from the procedural texture array built by TerrainTextures.cs;
// the colour comes from the map theme (_LayerColors). Lighting and fog of war match RTS/VertexLit.
Shader "RTS/Terrain"
{
    Properties
    {
        _Layers ("Layers", 2DArray) = "" {}
        _TexScale ("Detail Scale", Float) = 0.33
        _BlendDepth ("Blend Depth", Range(0.02, 1)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma require 2darray
            #include "UnityCG.cginc"

            float3 ToLin(float3 c)
            {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }

            UNITY_DECLARE_TEX2DARRAY(_Layers);
            sampler2D _RTSCellTex;
            float4 _RTSCellParams;   // x = 1/width, y = 1/height
            float4 _LayerColors[16]; // sRGB theme colour per terrain type
            float _TexScale;
            float _BlendDepth;

            sampler2D _RTSFogTex;
            float4 _RTSFogParams;
            float4 _RTSLightDir;
            float4 _RTSAmbient;
            float4 _RTSSun;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float3 wpos : TEXCOORD0; float3 n : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }

            float LayerAt(float2 cell)
            {
                float r = tex2Dlod(_RTSCellTex, float4((cell + 0.5) * _RTSCellParams.xy, 0, 0)).r;
                return floor(r * 255.0 + 0.5);
            }

            float3 Albedo(float layer, float2 uv, float2 uvMacro, out float h)
            {
                float4 t = UNITY_SAMPLE_TEX2DARRAY(_Layers, float3(uv, layer));
                float3 macro = UNITY_SAMPLE_TEX2DARRAY(_Layers, float3(uvMacro, layer)).rgb * 2.0;
                h = t.a;
                int li = (int)layer;
                return t.rgb * 2.0 * lerp(1.0, macro, 0.35) * ToLin(_LayerColors[li].rgb);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 cell = i.wpos.xz - 0.5;
                float2 b = floor(cell);
                float2 f = cell - b;
                float l00 = LayerAt(b), l10 = LayerAt(b + float2(1, 0)), l01 = LayerAt(b + float2(0, 1)), l11 = LayerAt(b + float2(1, 1));
                float2 uv = i.wpos.xz * _TexScale;
                float2 uvM = i.wpos.xz * _TexScale * 0.17 + 0.37;
                float3 albedo;
                if (l00 == l10 && l00 == l01 && l00 == l11)
                {
                    float h;
                    albedo = Albedo(l00, uv, uvM, h);
                }
                else
                {
                    float h00, h10, h01, h11;
                    float3 a00 = Albedo(l00, uv, uvM, h00), a10 = Albedo(l10, uv, uvM, h10);
                    float3 a01 = Albedo(l01, uv, uvM, h01), a11 = Albedo(l11, uv, uvM, h11);
                    float4 w = float4((1 - f.x) * (1 - f.y), f.x * (1 - f.y), (1 - f.x) * f.y, f.x * f.y);
                    // height blend: the higher (bilinear weight + texture height) wins, with a soft edge
                    float4 s = w + float4(h00, h10, h01, h11) * 0.5;
                    float m = max(max(s.x, s.y), max(s.z, s.w)) - _BlendDepth;
                    float4 bw = max(s - m, 0);
                    albedo = (a00 * bw.x + a10 * bw.y + a01 * bw.z + a11 * bw.w) / (bw.x + bw.y + bw.z + bw.w + 1e-5);
                }
                albedo *= i.color.rgb * 2.0;   // per-vertex brightness variation (0.5 = none)

                float3 n = normalize(i.n);
                float3 L = normalize(_RTSLightDir.xyz + float3(0.0001, 0, 0));
                float lam = saturate(dot(n, L));
                float3 amb = _RTSAmbient.rgb + max(0, n.y) * 0.08;
                float3 lit = albedo * (amb + _RTSSun.rgb * lam);
                if (_RTSFogParams.z > 0.5) lit *= tex2D(_RTSFogTex, i.wpos.xz * _RTSFogParams.xy).r;
                return float4(lit, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
