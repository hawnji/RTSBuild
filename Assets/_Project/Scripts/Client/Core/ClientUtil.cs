using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>Conversions between simulation space (x, y cells) and Unity world space (x, height, z).</summary>
    public static class Conv
    {
        public const float LevelHeight = 0.75f;

        public static Vector3 ToWorld(Vec2 p, float h = 0f) => new Vector3(p.X, h, p.Y);
        public static Vec2 ToSim(Vector3 p) => new Vec2(p.x, p.z);
        public static Vector2 V2(Vec2 p) => new Vector2(p.X, p.Y);

        /// <summary>Visual ground height at a sim position (bilinear across cell levels for smooth slopes on ramps).</summary>
        public static float Ground(MapData m, float x, float y)
        {
            var th = TerrainHeights.Current;
            if (th != null) return th.Sample(x, y);
            int cx = Mathf.Clamp(Mathf.FloorToInt(x), 0, m.Width - 1), cy = Mathf.Clamp(Mathf.FloorToInt(y), 0, m.Height - 1);
            return m.Level[cy * m.Width + cx] * LevelHeight;
        }

        public static float Ground(MapData m, Vec2 p) => Ground(m, p.X, p.Y);

        /// <summary>Converts a sim facing (radians, 0 = +X, counter-clockwise toward +Y) to a Unity yaw rotation.</summary>
        public static Quaternion Facing(float radians) => Quaternion.Euler(0f, 90f - radians * Mathf.Rad2Deg, 0f);

        public static Color Col(float[] rgb) => rgb == null || rgb.Length < 3 ? Color.white : new Color(rgb[0], rgb[1], rgb[2]);
    }

    /// <summary>Team colors and theme palettes.</summary>
    public static class Palette
    {
        public static readonly Color[] Team =
        {
            new Color(0.22f, 0.48f, 0.92f),   // blue
            new Color(0.86f, 0.18f, 0.14f),   // red
            new Color(0.25f, 0.72f, 0.30f),   // green
            new Color(0.95f, 0.65f, 0.12f),   // orange
            new Color(0.62f, 0.32f, 0.82f),   // purple
            new Color(0.15f, 0.78f, 0.78f),   // teal
            new Color(0.92f, 0.88f, 0.25f),   // yellow
            new Color(0.75f, 0.75f, 0.78f),   // grey (neutral)
        };
        public static readonly string[] TeamNames = { "Blue", "Red", "Green", "Orange", "Purple", "Teal", "Yellow", "Grey" };

        public static Color ForPlayer(Player p)
        {
            if (p == null || p.IsNeutral) return Team[7];
            return Team[Mathf.Abs(p.ColorIndex) % 7];
        }

        public static Color TerrainColor(Terrain t, MapTheme theme)
        {
            switch (t)
            {
                case Terrain.Grass:
                    switch (theme)
                    {
                        case MapTheme.Desert: return new Color(0.70f, 0.62f, 0.40f);
                        case MapTheme.Winter: return new Color(0.86f, 0.89f, 0.93f);
                        case MapTheme.Wasteland: return new Color(0.42f, 0.40f, 0.30f);
                        default: return new Color(0.34f, 0.50f, 0.24f);
                    }
                case Terrain.Dirt: return theme == MapTheme.Winter ? new Color(0.62f, 0.60f, 0.58f) : new Color(0.50f, 0.40f, 0.27f);
                case Terrain.Sand: return new Color(0.82f, 0.74f, 0.52f);
                case Terrain.Snow: return new Color(0.93f, 0.95f, 0.98f);
                case Terrain.Rock: return new Color(0.48f, 0.46f, 0.44f);
                case Terrain.Road: return new Color(0.36f, 0.35f, 0.34f);
                case Terrain.Water: return new Color(0.12f, 0.30f, 0.45f);
                case Terrain.Shallow: return new Color(0.30f, 0.52f, 0.58f);
                case Terrain.Cliff: return new Color(0.40f, 0.36f, 0.32f);
                case Terrain.Mountain: return new Color(0.44f, 0.42f, 0.40f);
                case Terrain.Forest: return theme == MapTheme.Winter ? new Color(0.80f, 0.84f, 0.86f) : theme == MapTheme.Desert ? new Color(0.62f, 0.56f, 0.36f) : new Color(0.24f, 0.38f, 0.18f);
                case Terrain.Bridge: return new Color(0.45f, 0.38f, 0.30f);
                case Terrain.Rubble: return new Color(0.34f, 0.31f, 0.28f);
            }
            return Color.magenta;
        }

        public static Color Minimap(Terrain t, MapTheme theme)
        {
            var c = TerrainColor(t, theme);
            if (t == Terrain.Forest) c *= 0.75f;
            if (t == Terrain.Cliff || t == Terrain.Mountain) c *= 0.7f;
            c.a = 1;
            return c;
        }
    }

    public static class ColorExt
    {
        public static Color WithA(this Color c, float a) { c.a = a; return c; }
        public static Color Mul(this Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
    }
}
