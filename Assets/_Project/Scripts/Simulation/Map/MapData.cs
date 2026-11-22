using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public enum Terrain : byte
    {
        Grass, Dirt, Sand, Snow, Rock, Road, Water, Shallow, Cliff, Mountain, Forest, Bridge, Rubble
    }

    public enum MapTheme { Temperate, Desert, Winter, Wasteland }

    public enum MoveClass { Foot = 0, Vehicle = 1, Naval = 2, Air = 3 }

    /// <summary>Runtime battlefield grid. One cell = one simulation unit.</summary>
    [Serializable]
    public class MapData
    {
        public string Id;
        public string Name;
        public int Width;
        public int Height;
        public MapTheme Theme;
        public Terrain[] Cells;
        public byte[] Level;          // elevation level 0..3
        public int[] Ore;             // remaining ore per cell
        public byte[] OreKind;        // 0 = ferrite ore, 1 = aurite crystal (double value)
        public List<Int2> OreVents = new List<Int2>();
        public List<Int2> StartPositions = new List<Int2>();
        public int Seed;

        public const int MaxOrePerCell = 900;

        public MapData(int w, int h)
        {
            Width = w; Height = h;
            Cells = new Terrain[w * h];
            Level = new byte[w * h];
            Ore = new int[w * h];
            OreKind = new byte[w * h];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Int2 c) => InBounds(c.X, c.Y);
        public int Index(int x, int y) => y * Width + x;
        public Terrain Get(int x, int y) => InBounds(x, y) ? Cells[y * Width + x] : Terrain.Mountain;
        public Terrain Get(Int2 c) => Get(c.X, c.Y);
        public void Set(int x, int y, Terrain t) { if (InBounds(x, y)) Cells[y * Width + x] = t; }
        public int LevelAt(int x, int y) => InBounds(x, y) ? Level[y * Width + x] : 0;

        public static bool IsWater(Terrain t) => t == Terrain.Water;
        public static bool IsLandPassable(Terrain t) =>
            t != Terrain.Water && t != Terrain.Cliff && t != Terrain.Mountain;

        /// <summary>Static terrain passability for a movement class (ignores buildings).</summary>
        public bool PassableStatic(int x, int y, MoveClass mc)
        {
            if (!InBounds(x, y)) return false;
            var t = Cells[y * Width + x];
            switch (mc)
            {
                case MoveClass.Foot: return t != Terrain.Water && t != Terrain.Cliff && t != Terrain.Mountain;
                case MoveClass.Vehicle: return t != Terrain.Water && t != Terrain.Cliff && t != Terrain.Mountain && t != Terrain.Forest;
                case MoveClass.Naval: return t == Terrain.Water || t == Terrain.Bridge && IsBridgeOverWater(x, y);
                default: return true;
            }
        }

        bool IsBridgeOverWater(int x, int y) => true;

        public float SpeedMultiplier(int x, int y, MoveClass mc)
        {
            if (mc == MoveClass.Air || mc == MoveClass.Naval) return 1f;
            switch (Get(x, y))
            {
                case Terrain.Road: return mc == MoveClass.Vehicle ? 1.3f : 1.1f;
                case Terrain.Bridge: return 1.15f;
                case Terrain.Sand: return 0.85f;
                case Terrain.Snow: return 0.85f;
                case Terrain.Rock: return 0.9f;
                case Terrain.Forest: return 0.6f;
                case Terrain.Shallow: return 0.6f;
                case Terrain.Rubble: return 0.75f;
                default: return 1f;
            }
        }

        /// <summary>Damage multiplier for infantry standing in this cell (cover).</summary>
        public float CoverMultiplier(int x, int y)
        {
            var t = Get(x, y);
            if (t == Terrain.Forest) return 0.7f;
            if (t == Terrain.Rubble) return 0.85f;
            return 1f;
        }

        public bool HasOre(int x, int y) => InBounds(x, y) && Ore[y * Width + x] > 0;

        public int TotalOre()
        {
            int s = 0;
            for (int i = 0; i < Ore.Length; i++) s += Ore[i];
            return s;
        }

        public MapData Clone()
        {
            var m = (MapData)MemberwiseClone();
            m.Cells = (Terrain[])Cells.Clone();
            m.Level = (byte[])Level.Clone();
            m.Ore = (int[])Ore.Clone();
            m.OreKind = (byte[])OreKind.Clone();
            m.OreVents = new List<Int2>(OreVents);
            m.StartPositions = new List<Int2>(StartPositions);
            return m;
        }
    }
}
