using System;

namespace RTS.Client
{
    /// <summary>
    /// Generates the tileable terrain detail textures in code (no Unity dependency, so it can be previewed outside the
    /// editor). One layer per <c>RTS.Sim.Terrain</c> value, in enum order. RGB is a detail multiplier stored at half
    /// scale (0.5 = no change), so the map theme's colour still decides the hue; alpha is a height used for blending
    /// between neighbouring ground types (higher wins, so grass tufts poke through sand edges, and so on).
    /// </summary>
    public static class TerrainTextureGen
    {
        public const int Layers = 13;

        public static byte[][] GenerateAll(int size, int seed = 1)
        {
            var all = new byte[Layers][];
            for (int i = 0; i < Layers; i++) all[i] = Generate(i, size, seed);
            return all;
        }

        // ------------------------------------------------------------------------------------------- noise

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        static int Wrap(int v, int p) => ((v % p) + p) % p;

        static float Smooth(float t) => t * t * (3 - 2 * t);

        /// <summary>Tileable value noise with period p cells over the unit square.</summary>
        static float Value(float u, float v, int p, int s)
        {
            float x = u * p, y = v * p;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            float a = Hash(Wrap(x0, p), Wrap(y0, p), s), b = Hash(Wrap(x0 + 1, p), Wrap(y0, p), s);
            float c = Hash(Wrap(x0, p), Wrap(y0 + 1, p), s), d = Hash(Wrap(x0 + 1, p), Wrap(y0 + 1, p), s);
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int p, int octaves, int s, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(u, v, p, s + o * 31) * amp;
                norm += amp; amp *= gain; p *= 2;
            }
            return sum / norm;
        }

        /// <summary>Tileable cellular noise: distance to nearest and second-nearest feature point, plus a cell id.</summary>
        static void Voronoi(float u, float v, int p, int s, out float f1, out float f2, out float id)
        {
            float x = u * p, y = v * p;
            int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
            f1 = 9; f2 = 9; id = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    int wx = Wrap(gx, p), wy = Wrap(gy, p);
                    float px = gx + Hash(wx, wy, s), py = gy + Hash(wx, wy, s + 7);
                    float d = (float)Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; id = Hash(wx, wy, s + 13); }
                    else if (d < f2) f2 = d;
                }
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // ------------------------------------------------------------------------------------------- layers

        public static byte[] Generate(int layer, int size, int seed)
        {
            var px = new byte[size * size * 4];
            int s = seed * 101 + layer * 1009;
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float u = (i + 0.5f) / size, v = (j + 0.5f) / size;
                    Sample(layer, u, v, s, out float r, out float g, out float b, out float h);
                    int k = (j * size + i) * 4;
                    px[k] = (byte)(Clamp01(r * 0.5f) * 255);
                    px[k + 1] = (byte)(Clamp01(g * 0.5f) * 255);
                    px[k + 2] = (byte)(Clamp01(b * 0.5f) * 255);
                    px[k + 3] = (byte)(Clamp01(h) * 255);
                }
            return px;
        }

        /// <summary>r, g, b: multipliers around 1. h: blend height 0..1.</summary>
        static void Sample(int layer, float u, float v, int s, out float r, out float g, out float b, out float h)
        {
            float n = Fbm(u, v, 4, 5, s);
            float fine = Fbm(u, v, 32, 2, s + 5);
            switch (layer)
            {
                case 0: // grass: blades and clover patches with dry spots
                {
                    float blades = Fbm(u * 1f, v, 64, 2, s + 9);
                    float dry = Clamp01((Fbm(u, v, 3, 3, s + 3) - 0.55f) * 4f);
                    float k = 0.78f + n * 0.3f + (blades - 0.5f) * 0.35f;
                    r = k * (1 + dry * 0.25f); g = k; b = k * (0.9f - dry * 0.1f);
                    h = 0.45f + blades * 0.35f;
                    break;
                }
                case 1: // dirt: soil with pebbles
                {
                    Voronoi(u, v, 24, s + 2, out float f1, out _, out float id);
                    float pebble = f1 < 0.22f && id > 0.55f ? 1f : 0f;
                    float k = 0.8f + n * 0.3f + (fine - 0.5f) * 0.2f + pebble * 0.25f;
                    r = k; g = k * 0.97f; b = k * 0.93f;
                    h = 0.35f + n * 0.3f + pebble * 0.3f;
                    break;
                }
                case 2: // sand: wind ripples
                {
                    float warp = Fbm(u, v, 4, 3, s + 4) * 6f;
                    float rip = (float)Math.Sin((v * 40 + warp) * Math.PI * 2) * 0.5f + 0.5f;
                    float k = 0.88f + rip * 0.12f + (n - 0.5f) * 0.2f + (fine - 0.5f) * 0.08f;
                    r = k; g = k; b = k * 0.98f;
                    h = 0.2f + rip * 0.25f + n * 0.15f;
                    break;
                }
                case 3: // snow: soft drifts with sparkle
                {
                    float sparkle = Hash((int)(u * 256), (int)(v * 256), s) > 0.985f ? 0.15f : 0f;
                    float k = 0.9f + n * 0.15f + sparkle;
                    r = k * 0.98f; g = k * 0.99f; b = k * 1.03f;
                    h = 0.3f + n * 0.4f;
                    break;
                }
                case 4: // rock (bare ground rock)
                case 9: // mountain
                {
                    Voronoi(u, v, 8, s + 6, out float f1, out float f2, out float id);
                    float crack = Clamp01((f2 - f1) * 6f);
                    float k = 0.7f + n * 0.35f + id * 0.15f;
                    k *= 0.65f + 0.35f * crack;
                    r = k; g = k * 0.98f; b = k * 0.96f;
                    h = 0.5f + n * 0.3f + crack * 0.2f;
                    break;
                }
                case 5: // road: cobbles
                {
                    Voronoi(u, v, 16, s + 8, out float f1, out float f2, out float id);
                    float edge = Clamp01((f2 - f1) * 5f);
                    float k = (0.75f + id * 0.3f + (fine - 0.5f) * 0.15f) * (0.55f + 0.45f * edge);
                    r = k; g = k; b = k * 1.02f;
                    h = 0.3f + edge * 0.4f;
                    break;
                }
                case 6: // deep water bed
                case 7: // shallow bed: pebbles and silt
                {
                    Voronoi(u, v, 20, s + 10, out float f1, out _, out float id);
                    float k = 0.75f + n * 0.3f + (f1 < 0.3f ? id * 0.25f : 0f);
                    r = k * 0.95f; g = k; b = k * 1.05f;
                    h = 0.1f + n * 0.2f;
                    break;
                }
                case 8: // cliff: horizontal strata
                {
                    float warp = Fbm(u, v, 4, 3, s + 12) * 3f;
                    float strata = (float)Math.Sin((v * 12 + warp) * Math.PI * 2) * 0.5f + 0.5f;
                    float k = 0.7f + strata * 0.25f + (n - 0.5f) * 0.3f + (fine - 0.5f) * 0.12f;
                    r = k * 1.02f; g = k; b = k * 0.95f;
                    h = 0.6f + strata * 0.3f;
                    break;
                }
                case 10: // forest floor: leaf litter and needles
                {
                    Voronoi(u, v, 28, s + 14, out float f1, out _, out float id);
                    float leaf = f1 < 0.35f ? id : 0f;
                    float k = 0.72f + n * 0.3f + leaf * 0.25f;
                    r = k * (1 + leaf * 0.35f); g = k; b = k * (0.9f - leaf * 0.2f);
                    h = 0.55f + leaf * 0.3f;
                    break;
                }
                case 11: // bridge (under the deck): planks
                {
                    float plank = (float)Math.Floor(v * 8);
                    float grain = Fbm(u * 1f, v, 16, 3, s + (int)plank) * 0.3f;
                    float gap = (v * 8 - plank) < 0.06f ? 0.5f : 1f;
                    float k = (0.75f + Hash((int)plank, 0, s) * 0.3f + grain) * gap;
                    r = k; g = k * 0.95f; b = k * 0.9f;
                    h = 0.5f;
                    break;
                }
                default: // rubble: broken concrete and scorch marks
                {
                    Voronoi(u, v, 10, s + 16, out float f1, out float f2, out float id);
                    float edge = Clamp01((f2 - f1) * 4f);
                    float scorch = Clamp01((Fbm(u, v, 3, 3, s + 17) - 0.55f) * 3f);
                    float k = (0.7f + id * 0.35f) * (0.6f + 0.4f * edge) * (1 - scorch * 0.5f);
                    r = k; g = k * 0.97f; b = k * 0.94f;
                    h = 0.5f + id * 0.3f;
                    break;
                }
            }
        }
    }
}
