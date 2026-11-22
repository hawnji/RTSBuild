using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// Writes the local team's fog-of-war state into a small texture (one texel per cell) that every shader samples.
    /// Values fade smoothly: unexplored 0, explored 0.42, visible 1.
    /// </summary>
    public class FogTexture
    {
        readonly World _w;
        readonly Texture2D _tex;
        readonly float[] _cur;
        readonly Color32[] _px;
        public int Team;
        public bool RevealAll;

        public FogTexture(World w, int team)
        {
            _w = w; Team = team;
            int W = w.Map.Width, H = w.Map.Height;
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "FogOfWar" };
            _cur = new float[W * H];
            _px = new Color32[W * H];
            Update(1f, true);
            Shader.SetGlobalTexture("_RTSFogTex", _tex);
        }

        public void Update(float dt, bool snap = false)
        {
            int W = _w.Map.Width, H = _w.Map.Height;
            bool enabled = _w.Fog.Enabled && !RevealAll;
            Shader.SetGlobalVector("_RTSFogParams", new Vector4(1f / W, 1f / H, enabled ? 1 : 0, 0));
            if (!enabled) return;
            float k = snap ? 1f : 1f - Mathf.Exp(-dt * 8f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    byte s = _w.Fog.CellState(Team, x, y);
                    float target = s == 2 ? 1f : s == 1 ? 0.42f : 0f;
                    _cur[i] += (target - _cur[i]) * k;
                    byte b = (byte)(Mathf.Clamp01(_cur[i]) * 255);
                    _px[i] = new Color32(b, b, b, 255);
                }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        public void Destroy() { Object.Destroy(_tex); }
    }

    /// <summary>Global lighting parameters for the custom shaders, chosen per map theme.</summary>
    public static class SceneLighting
    {
        public static void Apply(MapTheme theme)
        {
            Vector3 dir = new Vector3(-0.45f, 0.8f, -0.35f).normalized;
            Color amb, sun;
            switch (theme)
            {
                case MapTheme.Desert: amb = new Color(0.46f, 0.42f, 0.38f); sun = new Color(0.75f, 0.68f, 0.55f); break;
                case MapTheme.Winter: amb = new Color(0.48f, 0.52f, 0.6f); sun = new Color(0.62f, 0.64f, 0.7f); break;
                case MapTheme.Wasteland: amb = new Color(0.4f, 0.36f, 0.34f); sun = new Color(0.7f, 0.58f, 0.46f); break;
                default: amb = new Color(0.42f, 0.44f, 0.48f); sun = new Color(0.72f, 0.68f, 0.6f); break;
            }
            Shader.SetGlobalVector("_RTSLightDir", dir);
            Shader.SetGlobalColor("_RTSAmbient", amb);
            Shader.SetGlobalColor("_RTSSun", sun);
            RenderSettings.ambientLight = amb;
        }
    }
}
