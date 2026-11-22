using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public enum FeatureKind
    {
        Lake, River, Ocean, Plateau, Mountain, Forest, Road, OreField, CrystalField, Clearing, Ford, Patch, Ridge
    }

    public enum Symmetry { None, Rotational2, Mirror4 }

    [Serializable]
    public class MapFeature
    {
        public FeatureKind Kind;
        public List<Vec2> Points = new List<Vec2>();
        public float Radius = 4;
        public float Width = 2;
        public int Value;                      // plateau level / ore richness / patch terrain
        public List<int> BridgeAt = new List<int>(); // river: point indices at which to place bridges
        public List<Vec2> Ramps = new List<Vec2>();  // plateau ramps
        public Terrain PatchTerrain = Terrain.Dirt;

        public static MapFeature Make(FeatureKind k, float radius, params Vec2[] pts)
        {
            var f = new MapFeature { Kind = k, Radius = radius };
            f.Points.AddRange(pts);
            return f;
        }
    }

    [Serializable]
    public class NeutralPlacement
    {
        public string DefId;
        public Int2 Cell;
        public string Tag;
        public NeutralPlacement(string def, int x, int y, string tag = null) { DefId = def; Cell = new Int2(x, y); Tag = tag; }
    }

    /// <summary>Data-driven description of a battlefield. MapGenerator turns it into MapData deterministically.</summary>
    [Serializable]
    public class MapSpec
    {
        public string Id;
        public string Name;
        public string Description;
        public int Width = 96;
        public int Height = 96;
        public MapTheme Theme = MapTheme.Temperate;
        public int Seed = 1;
        public int MaxPlayers = 2;
        public Symmetry Symmetry = Symmetry.None;
        public float ForestDensity = 0.08f;
        public float Roughness = 0.05f;
        public List<MapFeature> Features = new List<MapFeature>();
        public List<Int2> Starts = new List<Int2>();
        public List<NeutralPlacement> Neutrals = new List<NeutralPlacement>();
        public bool Skirmish = true;
        /// <summary>Team layout hint for 2v2 maps: start indices of team A (others are team B).</summary>
        public List<int> TeamA = new List<int>();

        public MapSpec Add(MapFeature f) { Features.Add(f); return this; }
    }

    public static class MapGenerator
    {
        public static MapData Generate(MapSpec spec)
        {
            var m = new MapData(spec.Width, spec.Height) { Id = spec.Id, Name = spec.Name, Theme = spec.Theme, Seed = spec.Seed };
            var rng = new SimRandom(spec.Seed);
            Terrain baseT = BaseTerrain(spec.Theme);
            int W = m.Width, H = m.Height;
            var ramps = new HashSet<int>();

            // 1. Base terrain + noise detail
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Noise.Fractal(x * 0.08f, y * 0.08f, spec.Seed, 3);
                    float n2 = Noise.Fractal(x * 0.15f + 100, y * 0.15f, spec.Seed + 7, 2);
                    Terrain t = baseT;
                    if (n > 0.68f) t = SecondaryTerrain(spec.Theme);
                    if (n2 > 1f - spec.Roughness * 2.2f) t = Terrain.Rock;
                    if (n2 > 1f - spec.Roughness * 0.6f) t = Terrain.Mountain;
                    float f = Noise.Fractal(x * 0.11f + 50, y * 0.11f + 50, spec.Seed + 13, 3);
                    if (f > 1f - spec.ForestDensity * 2.4f && t != Terrain.Mountain) t = Terrain.Forest;
                    m.Cells[m.Index(x, y)] = t;
                }

            // 2. Features
            foreach (var feat in spec.Features) ApplyFeature(m, feat, rng, ramps);

            // 3. Symmetry
            if (spec.Symmetry == Symmetry.Rotational2) ApplyRotational(m, ramps, spec.Starts.Count == 0 || spec.Starts[0].Y * W + spec.Starts[0].X < W * H / 2);
            else if (spec.Symmetry == Symmetry.Mirror4) ApplyMirror4(m, ramps);

            // 4. Starting areas
            m.StartPositions.AddRange(spec.Starts);
            foreach (var s in spec.Starts) ClearArea(m, s, 7.5f, baseT, true);

            // 5. Cliffs from elevation
            BuildCliffs(m, ramps);

            // 6. Map border: impassable rim for land feel (keep water edges)
            for (int x = 0; x < W; x++) { Rim(m, x, 0); Rim(m, x, H - 1); }
            for (int y = 0; y < H; y++) { Rim(m, 0, y); Rim(m, W - 1, y); }

            // 7. Connectivity between all starts for vehicles
            for (int i = 1; i < spec.Starts.Count; i++) EnsureConnected(m, spec.Starts[0], spec.Starts[i]);

            // 8. Ore vents
            for (int i = 0; i < m.Ore.Length; i++)
                if (m.Ore[i] >= MapData.MaxOrePerCell && (i % 7 == 0)) m.OreVents.Add(new Int2(i % W, i / W));
            return m;
        }

        static void Rim(MapData m, int x, int y)
        {
            var t = m.Get(x, y);
            if (t != Terrain.Water) m.Set(x, y, Terrain.Mountain);
        }

        public static Terrain BaseTerrain(MapTheme t) =>
            t == MapTheme.Desert ? Terrain.Sand : t == MapTheme.Winter ? Terrain.Snow : t == MapTheme.Wasteland ? Terrain.Dirt : Terrain.Grass;

        static Terrain SecondaryTerrain(MapTheme t) =>
            t == MapTheme.Desert ? Terrain.Dirt : t == MapTheme.Winter ? Terrain.Rock : t == MapTheme.Wasteland ? Terrain.Rubble : Terrain.Dirt;

        static void ApplyFeature(MapData m, MapFeature f, SimRandom rng, HashSet<int> ramps)
        {
            switch (f.Kind)
            {
                case FeatureKind.Lake:
                    foreach (var p in f.Points) Blob(m, p, f.Radius, (x, y, d) =>
                    {
                        if (d < f.Radius - 1.2f) Set(m, x, y, Terrain.Water);
                        else if (m.Get(x, y) != Terrain.Water) Set(m, x, y, m.Theme == MapTheme.Winter ? Terrain.Snow : Terrain.Sand);
                    }, rng);
                    break;
                case FeatureKind.Ocean:
                    // Points[0] = a point on the shoreline, Points[1] = direction toward the sea.
                    {
                        var origin = f.Points[0];
                        var dir = f.Points.Count > 1 ? f.Points[1].Normalized : new Vec2(1, 0);
                        for (int y = 0; y < m.Height; y++)
                            for (int x = 0; x < m.Width; x++)
                            {
                                var rel = new Vec2(x + 0.5f, y + 0.5f) - origin;
                                float d = Vec2.Dot(rel, dir) + (Noise.Fractal(x * 0.1f, y * 0.1f, m.Seed + 99, 3) - 0.5f) * 6f;
                                if (d > 1.5f) Set(m, x, y, Terrain.Water);
                                else if (d > -1.0f) Set(m, x, y, m.Theme == MapTheme.Winter ? Terrain.Snow : Terrain.Sand);
                            }
                    }
                    break;
                case FeatureKind.River:
                    {
                        var cells = Polyline(f.Points, f.Width + 1.2f);
                        foreach (var c in cells)
                        {
                            if (!m.InBounds(c.X, c.Y)) continue;
                            if (c.D < f.Width * 0.5f + 0.3f) Set(m, c.X, c.Y, Terrain.Water);
                            else if (m.Get(c.X, c.Y) != Terrain.Water) Set(m, c.X, c.Y, Terrain.Dirt);
                        }
                        foreach (int bi in f.BridgeAt)
                        {
                            if (bi <= 0 || bi >= f.Points.Count) continue;
                            var a = f.Points[bi - 1]; var b = f.Points[bi];
                            var mid = Vec2.Lerp(a, b, 0.5f);
                            var perp = (b - a).Normalized.Perp;
                            float len = f.Width + 3f;
                            for (float t = -len; t <= len; t += 0.35f)
                                for (float s = -1f; s <= 1f; s += 0.5f)
                                {
                                    var p = mid + perp * t + (b - a).Normalized * s;
                                    int x = (int)p.X, y = (int)p.Y;
                                    if (!m.InBounds(x, y)) continue;
                                    if (m.Get(x, y) == Terrain.Water) Set(m, x, y, Terrain.Bridge);
                                    else if (Math.Abs(t) < len) Set(m, x, y, Terrain.Road);
                                }
                        }
                    }
                    break;
                case FeatureKind.Ford:
                    foreach (var c in Polyline(f.Points, f.Width))
                        if (m.InBounds(c.X, c.Y) && m.Get(c.X, c.Y) == Terrain.Water) Set(m, c.X, c.Y, Terrain.Shallow);
                    break;
                case FeatureKind.Plateau:
                    foreach (var p in f.Points) Blob(m, p, f.Radius, (x, y, d) =>
                    {
                        int i = m.Index(x, y);
                        if (m.Level[i] < f.Value) m.Level[i] = (byte)f.Value;
                    }, rng, 0.25f);
                    foreach (var r in f.Ramps)
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int x = (int)r.X + dx, y = (int)r.Y + dy;
                                if (m.InBounds(x, y)) ramps.Add(m.Index(x, y));
                            }
                    break;
                case FeatureKind.Ridge:
                    foreach (var c in Polyline(f.Points, f.Width))
                        if (m.InBounds(c.X, c.Y)) Set(m, c.X, c.Y, c.D < f.Width * 0.35f ? Terrain.Mountain : Terrain.Rock);
                    break;
                case FeatureKind.Mountain:
                    foreach (var p in f.Points) Blob(m, p, f.Radius, (x, y, d) =>
                    {
                        Set(m, x, y, d < f.Radius * 0.7f ? Terrain.Mountain : Terrain.Rock);
                    }, rng);
                    break;
                case FeatureKind.Forest:
                    foreach (var p in f.Points) Blob(m, p, f.Radius, (x, y, d) =>
                    {
                        var t = m.Get(x, y);
                        if (t != Terrain.Water && t != Terrain.Mountain && t != Terrain.Road && t != Terrain.Bridge && rng.Chance(0.82f))
                            Set(m, x, y, Terrain.Forest);
                    }, rng);
                    break;
                case FeatureKind.Road:
                    foreach (var c in Polyline(f.Points, f.Width))
                    {
                        if (!m.InBounds(c.X, c.Y)) continue;
                        var t = m.Get(c.X, c.Y);
                        if (t == Terrain.Water) Set(m, c.X, c.Y, Terrain.Bridge);
                        else if (t != Terrain.Bridge) Set(m, c.X, c.Y, Terrain.Road);
                        ramps.Add(m.Index(c.X, c.Y));
                    }
                    break;
                case FeatureKind.OreField:
                case FeatureKind.CrystalField:
                    foreach (var p in f.Points) Blob(m, p, f.Radius, (x, y, d) =>
                    {
                        var t = m.Get(x, y);
                        if (!MapData.IsLandPassable(t) || t == Terrain.Road || t == Terrain.Bridge) return;
                        if (t == Terrain.Forest) Set(m, x, y, Terrain.Dirt);
                        float density = 1f - d / f.Radius;
                        int amount = (int)(MapData.MaxOrePerCell * SMath.Clamp(0.35f + density * 0.9f, 0.2f, 1f));
                        if (f.Value > 0) amount = amount * f.Value / 100;
                        int i = m.Index(x, y);
                        m.Ore[i] = Math.Min(MapData.MaxOrePerCell, Math.Max(m.Ore[i], amount));
                        m.OreKind[i] = (byte)(f.Kind == FeatureKind.CrystalField ? 1 : 0);
                    }, rng, 0.35f);
                    foreach (var p in f.Points) m.OreVents.Add(Int2.Floor(p));
                    break;
                case FeatureKind.Clearing:
                    foreach (var p in f.Points) ClearArea(m, Int2.Floor(p), f.Radius, BaseTerrain(m.Theme), false);
                    break;
                case FeatureKind.Patch:
                    foreach (var p in f.Points) Blob(m, p, f.Radius, (x, y, d) =>
                    {
                        var t = m.Get(x, y);
                        if (t != Terrain.Water && t != Terrain.Road && t != Terrain.Bridge && t != Terrain.Mountain) Set(m, x, y, f.PatchTerrain);
                    }, rng);
                    break;
            }
        }

        static void Set(MapData m, int x, int y, Terrain t) => m.Set(x, y, t);

        static void Blob(MapData m, Vec2 c, float r, Action<int, int, float> act, SimRandom rng, float noiseAmt = 0.3f)
        {
            int x0 = (int)(c.X - r - 2), x1 = (int)(c.X + r + 2), y0 = (int)(c.Y - r - 2), y1 = (int)(c.Y + r + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!m.InBounds(x, y)) continue;
                    float d = Vec2.Distance(new Vec2(x + 0.5f, y + 0.5f), c);
                    float wobble = (Noise.Value(x * 0.35f, y * 0.35f, m.Seed + (int)(c.X * 13 + c.Y)) - 0.5f) * r * noiseAmt * 2f;
                    if (d <= r + wobble) act(x, y, Math.Max(0, d));
                }
        }

        struct PolyCell { public int X, Y; public float D; }

        static List<PolyCell> Polyline(List<Vec2> pts, float width)
        {
            var result = new Dictionary<int, PolyCell>();
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i];
                int x0 = (int)(Math.Min(a.X, b.X) - width - 1), x1 = (int)(Math.Max(a.X, b.X) + width + 1);
                int y0 = (int)(Math.Min(a.Y, b.Y) - width - 1), y1 = (int)(Math.Max(a.Y, b.Y) + width + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = DistToSegment(new Vec2(x + 0.5f, y + 0.5f), a, b);
                        if (d <= width * 0.5f + 0.5f)
                        {
                            int key = (y + 1000) * 4096 + x + 1000;
                            if (!result.TryGetValue(key, out var pc) || pc.D > d) result[key] = new PolyCell { X = x, Y = y, D = d };
                        }
                    }
            }
            return new List<PolyCell>(result.Values);
        }

        public static float DistToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            float l2 = ab.SqrLength;
            if (l2 < 1e-6f) return Vec2.Distance(p, a);
            float t = SMath.Clamp01(Vec2.Dot(p - a, ab) / l2);
            return Vec2.Distance(p, a + ab * t);
        }

        static void ClearArea(MapData m, Int2 c, float r, Terrain baseT, bool levelFlatten)
        {
            int lvl = m.LevelAt(c.X, c.Y);
            for (int y = (int)(c.Y - r); y <= c.Y + r; y++)
                for (int x = (int)(c.X - r); x <= c.X + r; x++)
                {
                    if (!m.InBounds(x, y)) continue;
                    if (Vec2.Distance(new Vec2(x, y), new Vec2(c.X, c.Y)) > r) continue;
                    int i = m.Index(x, y);
                    var t = m.Cells[i];
                    if (t == Terrain.Water || t == Terrain.Forest || t == Terrain.Mountain || t == Terrain.Rock || t == Terrain.Cliff || t == Terrain.Shallow)
                        m.Cells[i] = baseT;
                    m.Ore[i] = 0;
                    if (levelFlatten) m.Level[i] = (byte)lvl;
                }
        }

        /// <summary>Copies the authored half (the one holding the first start) onto the other half, rotated 180°.</summary>
        static void ApplyRotational(MapData m, HashSet<int> ramps, bool authoredLowHalf)
        {
            int n = m.Width * m.Height;
            var add = new List<int>();
            int from = authoredLowHalf ? n / 2 : 0, to = authoredLowHalf ? n : n / 2;
            for (int i = from; i < to; i++)
            {
                int x = i % m.Width, y = i / m.Width;
                int j = m.Index(m.Width - 1 - x, m.Height - 1 - y);
                CopyCell(m, j, i);
                if (ramps.Contains(j)) add.Add(i); else ramps.Remove(i);
            }
            foreach (var a in add) ramps.Add(a);
            MirrorVents(m, (v) => new Int2(m.Width - 1 - v.X, m.Height - 1 - v.Y), (v) => (v.Y * m.Width + v.X < n / 2) == authoredLowHalf);
        }

        static void ApplyMirror4(MapData m, HashSet<int> ramps)
        {
            int hw = (m.Width + 1) / 2, hh = (m.Height + 1) / 2;
            var add = new List<int>();
            for (int y = 0; y < m.Height; y++)
                for (int x = 0; x < m.Width; x++)
                {
                    if (x < hw && y < hh) continue;
                    int sx = x < hw ? x : m.Width - 1 - x;
                    int sy = y < hh ? y : m.Height - 1 - y;
                    int j = m.Index(sx, sy), i = m.Index(x, y);
                    CopyCell(m, j, i);
                    if (ramps.Contains(j)) add.Add(i); else ramps.Remove(i);
                }
            foreach (var a in add) ramps.Add(a);
            var src = new List<Int2>();
            foreach (var v in m.OreVents) if (v.X < hw && v.Y < hh) src.Add(v);
            m.OreVents.Clear();
            foreach (var v in src)
            {
                m.OreVents.Add(v);
                m.OreVents.Add(new Int2(m.Width - 1 - v.X, v.Y));
                m.OreVents.Add(new Int2(v.X, m.Height - 1 - v.Y));
                m.OreVents.Add(new Int2(m.Width - 1 - v.X, m.Height - 1 - v.Y));
            }
        }

        static void MirrorVents(MapData m, Func<Int2, Int2> mirror, Func<Int2, bool> keep)
        {
            var src = new List<Int2>();
            foreach (var v in m.OreVents) if (keep(v)) src.Add(v);
            m.OreVents.Clear();
            foreach (var v in src) { m.OreVents.Add(v); m.OreVents.Add(mirror(v)); }
        }

        static void CopyCell(MapData m, int from, int to)
        {
            m.Cells[to] = m.Cells[from];
            m.Level[to] = m.Level[from];
            m.Ore[to] = m.Ore[from];
            m.OreKind[to] = m.OreKind[from];
        }

        static void BuildCliffs(MapData m, HashSet<int> ramps)
        {
            var toCliff = new List<int>();
            for (int y = 0; y < m.Height; y++)
                for (int x = 0; x < m.Width; x++)
                {
                    int i = m.Index(x, y);
                    int lv = m.Level[i];
                    if (lv == 0) continue;
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!m.InBounds(nx, ny)) continue;
                            if (m.Level[m.Index(nx, ny)] < lv) { edge = true; break; }
                        }
                    if (edge && !ramps.Contains(i) && m.Cells[i] != Terrain.Water && m.Cells[i] != Terrain.Bridge) toCliff.Add(i);
                }
            foreach (var i in toCliff) { m.Cells[i] = Terrain.Cliff; m.Ore[i] = 0; }
            // Ramps become dirt so they read visually as slopes.
            foreach (var i in ramps)
                if (i >= 0 && i < m.Cells.Length && m.Cells[i] != Terrain.Water && m.Cells[i] != Terrain.Bridge && m.Cells[i] != Terrain.Road && m.Level[i] > 0)
                    m.Cells[i] = Terrain.Dirt;
        }

        /// <summary>Guarantees a vehicle route between two starts by carving through obstacles if needed.</summary>
        public static void EnsureConnected(MapData m, Int2 a, Int2 b)
        {
            if (Reachable(m, a, b)) return;
            // A* where impassable cells cost a lot; carve along the result.
            var path = CarvePath(m, a, b);
            foreach (var c in path)
            {
                for (int dy = 0; dy <= 1; dy++)
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int x = c.X + dx, y = c.Y + dy;
                        if (!m.InBounds(x, y) || x == 0 || y == 0 || x == m.Width - 1 || y == m.Height - 1) continue;
                        var t = m.Get(x, y);
                        if (t == Terrain.Water) m.Set(x, y, Terrain.Shallow);
                        else if (t == Terrain.Cliff || t == Terrain.Mountain || t == Terrain.Forest) m.Set(x, y, Terrain.Dirt);
                    }
            }
        }

        static bool Reachable(MapData m, Int2 a, Int2 b)
        {
            var seen = new bool[m.Width * m.Height];
            var q = new Queue<Int2>();
            q.Enqueue(a); seen[m.Index(a.X, a.Y)] = true;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (Math.Abs(c.X - b.X) <= 1 && Math.Abs(c.Y - b.Y) <= 1) return true;
                for (int d = 0; d < 4; d++)
                {
                    int nx = c.X + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = c.Y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!m.InBounds(nx, ny)) continue;
                    int i = m.Index(nx, ny);
                    if (seen[i] || !m.PassableStatic(nx, ny, MoveClass.Vehicle)) continue;
                    seen[i] = true; q.Enqueue(new Int2(nx, ny));
                }
            }
            return false;
        }

        static List<Int2> CarvePath(MapData m, Int2 a, Int2 b)
        {
            int n = m.Width * m.Height;
            var cost = new float[n];
            var prev = new int[n];
            for (int i = 0; i < n; i++) { cost[i] = float.MaxValue; prev[i] = -1; }
            var open = new SimplePriorityQueue();
            int s = m.Index(a.X, a.Y), g = m.Index(b.X, b.Y);
            cost[s] = 0; open.Push(s, 0);
            while (open.Count > 0)
            {
                int c = open.Pop();
                if (c == g) break;
                int cx = c % m.Width, cy = c / m.Width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!m.InBounds(nx, ny) || nx == 0 || ny == 0 || nx == m.Width - 1 || ny == m.Height - 1) continue;
                    int ni = m.Index(nx, ny);
                    float step = m.PassableStatic(nx, ny, MoveClass.Vehicle) ? 1f : 25f;
                    float nc = cost[c] + step;
                    if (nc < cost[ni]) { cost[ni] = nc; prev[ni] = c; open.Push(ni, nc + Math.Abs(nx - b.X) + Math.Abs(ny - b.Y)); }
                }
            }
            var path = new List<Int2>();
            int cur = g;
            while (cur != -1 && cur != s) { path.Add(new Int2(cur % m.Width, cur / m.Width)); cur = prev[cur]; }
            return path;
        }
    }

    /// <summary>Binary min-heap keyed by float priority (used by pathfinding and generation).</summary>
    public class SimplePriorityQueue
    {
        int[] _items = new int[256];
        float[] _prio = new float[256];
        public int Count;

        public void Clear() => Count = 0;

        public void Push(int item, float p)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, Count * 2);
                Array.Resize(ref _prio, Count * 2);
            }
            int i = Count++;
            _items[i] = item; _prio[i] = p;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_prio[parent] <= _prio[i]) break;
                Swap(i, parent); i = parent;
            }
        }

        public int Pop()
        {
            int top = _items[0];
            Count--;
            if (Count > 0)
            {
                _items[0] = _items[Count]; _prio[0] = _prio[Count];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < Count && _prio[l] < _prio[m]) m = l;
                    if (r < Count && _prio[r] < _prio[m]) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
            }
            return top;
        }

        void Swap(int a, int b)
        {
            int t = _items[a]; _items[a] = _items[b]; _items[b] = t;
            float p = _prio[a]; _prio[a] = _prio[b]; _prio[b] = p;
        }
    }
}
