using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;

namespace RTS.Client
{
    /// <summary>
    /// Unity side of the terrain look: the procedural detail texture array (built once), the per-cell terrain index
    /// texture for the current map, and the terrain material with the theme's colours.
    /// </summary>
    public static class TerrainTextures
    {
        const int Size = 256;
        static Texture2DArray _layers;
        static Material _mat;

        public static Texture2DArray Layers
        {
            get
            {
                if (_layers != null) return _layers;
                var data = TerrainTextureGen.GenerateAll(Size);
                _layers = new Texture2DArray(Size, Size, TerrainTextureGen.Layers, TextureFormat.RGBA32, true, true)
                {
                    name = "RTS Terrain Layers", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
                };
                for (int i = 0; i < data.Length; i++) _layers.SetPixelData(data[i], 0, i);
                _layers.Apply(true, true);
                return _layers;
            }
        }

        /// <summary>Terrain material for a map theme, or null if the shader is unavailable (falls back to vertex colours).</summary>
        public static Material MaterialFor(MapTheme theme)
        {
            if (_mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/RTSTerrain");
                if (sh == null) sh = Shader.Find("RTS/Terrain");
                if (sh == null || !sh.isSupported || !SystemInfo.supports2DArrayTextures) return null;
                _mat = new Material(sh) { name = "RTS Terrain" };
                _mat.SetTexture("_Layers", Layers);
            }
            var cols = new Vector4[16];
            for (int i = 0; i < TerrainTextureGen.Layers; i++)
            {
                var c = Palette.TerrainColor((Terrain)i, theme);
                if ((Terrain)i == Terrain.Water) c = c.Mul(0.55f);
                cols[i] = c;
            }
            _mat.SetVectorArray("_LayerColors", cols);
            return _mat;
        }

        /// <summary>Creates the one-texel-per-cell terrain index texture and makes it global for the shader.</summary>
        public static Texture2D CreateCellTexture(MapData m)
        {
            var t = new Texture2D(m.Width, m.Height, TextureFormat.RGBA32, false, true)
            {
                name = "RTS Cell Types", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            UpdateCells(t, m, 0, 0, m.Width, m.Height);
            Shader.SetGlobalTexture("_RTSCellTex", t);
            Shader.SetGlobalVector("_RTSCellParams", new Vector4(1f / m.Width, 1f / m.Height, 0, 0));
            return t;
        }

        static Color32[] _buf;

        public static void UpdateCells(Texture2D t, MapData m, int x0, int y0, int x1, int y1)
        {
            if (_buf == null || _buf.Length != m.Width * m.Height) _buf = t.GetPixels32();
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var c = (byte)m.Cells[y * m.Width + x];
                    // Under a bridge the ground is the river bed.
                    if (c == (byte)Terrain.Bridge) c = (byte)Terrain.Shallow;
                    // Forest floor under the trees, mountain rock under the peaks.
                    _buf[y * m.Width + x] = new Color32(c, 0, 0, 255);
                }
            t.SetPixels32(_buf);
            t.Apply(false);
        }
    }
}
