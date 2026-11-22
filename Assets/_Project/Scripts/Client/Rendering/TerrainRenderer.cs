using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>Smooth visual height field derived from the simulation's discrete elevation levels.</summary>
    public class TerrainHeights
    {
        public static TerrainHeights Current;
        public readonly int W, H;
        readonly float[] _v;   // (W+1)*(H+1) vertex heights

        public TerrainHeights(MapData m)
        {
            W = m.Width; H = m.Height;
            _v = new float[(W + 1) * (H + 1)];
            Rebuild(m);
        }

        static float CellH(MapData m, int x, int y)
        {
            x = Mathf.Clamp(x, 0, m.Width - 1); y = Mathf.Clamp(y, 0, m.Height - 1);
            int i = y * m.Width + x;
            float h = m.Level[i] * Conv.LevelHeight;
            var t = m.Cells[i];
            if (t == Terrain.Water) h -= 0.45f;
            else if (t == Terrain.Shallow) h -= 0.12f;
            else if (t == Terrain.Mountain) h += 0.9f;
            return h;
        }

        public void Rebuild(MapData m)
        {
            for (int y = 0; y <= H; y++)
                for (int x = 0; x <= W; x++)
                {
                    float a = CellH(m, x - 1, y - 1), b = CellH(m, x, y - 1), c = CellH(m, x - 1, y), d = CellH(m, x, y);
                    float h = (a + b + c + d) * 0.25f;
                    // Keep plateau tops flat: a vertex touching only same-level land takes that level exactly.
                    _v[y * (W + 1) + x] = h;
                }
        }

        public float Vertex(int x, int y) => _v[Mathf.Clamp(y, 0, H) * (W + 1) + Mathf.Clamp(x, 0, W)];

        public float Sample(float x, float y)
        {
            x = Mathf.Clamp(x, 0, W - 0.001f); y = Mathf.Clamp(y, 0, H - 0.001f);
            int ix = (int)x, iy = (int)y; float fx = x - ix, fy = y - iy;
            float a = Vertex(ix, iy), b = Vertex(ix + 1, iy), c = Vertex(ix, iy + 1), d = Vertex(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }

    /// <summary>Builds and maintains chunked meshes for terrain, water, vegetation, rocks, bridges and ore.</summary>
    public class TerrainRenderer
    {
        const int Chunk = 32;
        readonly MapData _m;
        readonly Transform _root;
        readonly TerrainHeights _h;
        readonly int _cw, _ch;
        readonly ChunkData[] _chunks;
        readonly MeshBuilder _mb = new MeshBuilder();
        int _scan;

        class ChunkData
        {
            public int X0, Y0;
            public MeshFilter Ground, Decor, Ore, Water;
            public int TerrainHash, OreHash;
        }

        public TerrainRenderer(MapData m, Transform parent)
        {
            _m = m;
            _root = new GameObject("Terrain").transform;
            _root.SetParent(parent, false);
            _h = new TerrainHeights(m);
            TerrainHeights.Current = _h;
            _cw = (m.Width + Chunk - 1) / Chunk; _ch = (m.Height + Chunk - 1) / Chunk;
            _chunks = new ChunkData[_cw * _ch];
            for (int cy = 0; cy < _ch; cy++)
                for (int cx = 0; cx < _cw; cx++)
                {
                    var c = new ChunkData { X0 = cx * Chunk, Y0 = cy * Chunk };
                    c.Ground = MakeFilter("Ground_" + cx + "_" + cy, Materials.Lit);
                    c.Decor = MakeFilter("Decor_" + cx + "_" + cy, Materials.Lit);
                    c.Ore = MakeFilter("Ore_" + cx + "_" + cy, Materials.Lit);
                    c.Water = MakeFilter("Water_" + cx + "_" + cy, Materials.Water);
                    _chunks[cy * _cw + cx] = c;
                    BuildTerrain(c);
                    BuildOre(c);
                }
            BuildSkirt();
        }

        MeshFilter MakeFilter(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mf;
        }

        static float Hash(int x, int y, int s = 0)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        int TerrainHashOf(ChunkData c)
        {
            int h = 17;
            for (int y = c.Y0; y < Mathf.Min(c.Y0 + Chunk, _m.Height); y++)
                for (int x = c.X0; x < Mathf.Min(c.X0 + Chunk, _m.Width); x++)
                    h = h * 31 + (int)_m.Cells[y * _m.Width + x];
            return h;
        }

        int OreHashOf(ChunkData c)
        {
            int h = 17;
            for (int y = c.Y0; y < Mathf.Min(c.Y0 + Chunk, _m.Height); y++)
                for (int x = c.X0; x < Mathf.Min(c.X0 + Chunk, _m.Width); x++)
                    h = h * 31 + OreBucket(_m.Ore[y * _m.Width + x]);
            return h;
        }

        static int OreBucket(int ore) => ore <= 0 ? 0 : ore < 200 ? 1 : ore < 450 ? 2 : ore < 700 ? 3 : 4;

        /// <summary>Incrementally checks a couple of chunks per frame and rebuilds those whose cells changed.</summary>
        public void Update()
        {
            if (_chunks.Length == 0) return;
            for (int k = 0; k < 2; k++)
            {
                _scan = (_scan + 1) % _chunks.Length;
                var c = _chunks[_scan];
                if (TerrainHashOf(c) != c.TerrainHash)
                {
                    _h.Rebuild(_m);
                    // Neighbouring chunks share edge vertices; rebuild them too so seams stay closed.
                    int cx = c.X0 / Chunk, cy = c.Y0 / Chunk;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= _cw || ny >= _ch) continue;
                            BuildTerrain(_chunks[ny * _cw + nx]);
                        }
                }
                if (OreHashOf(c) != c.OreHash) BuildOre(c);
            }
        }

        public void ForceRefresh()
        {
            _h.Rebuild(_m);
            foreach (var c in _chunks) { BuildTerrain(c); BuildOre(c); }
        }

        Color CellColor(int x, int y, Terrain t)
        {
            var c = Palette.TerrainColor(t, _m.Theme);
            float n = Hash(x, y) * 0.10f - 0.05f;
            float n2 = Mathf.PerlinNoise(x * 0.11f + _m.Seed * 0.01f, y * 0.11f) * 0.12f - 0.06f;
            c = new Color(c.r + n + n2, c.g + n + n2 * 1.2f, c.b + n * 0.8f + n2 * 0.6f);
            int lvl = _m.LevelAt(x, y);
            c = c.Mul(1f + lvl * 0.04f);
            if (t == Terrain.Road && ((x + y) & 7) == 0) c = c.Mul(1.06f);
            return c;
        }

        void BuildTerrain(ChunkData c)
        {
            c.TerrainHash = TerrainHashOf(c);
            int x1 = Mathf.Min(c.X0 + Chunk, _m.Width), y1 = Mathf.Min(c.Y0 + Chunk, _m.Height);

            // ---- ground
            _mb.Clear();
            for (int y = c.Y0; y < y1; y++)
                for (int x = c.X0; x < x1; x++)
                {
                    var t = _m.Cells[y * _m.Width + x];
                    var col = CellColor(x, y, t);
                    if (t == Terrain.Water) col = Palette.TerrainColor(Terrain.Water, _m.Theme).Mul(0.55f);
                    Color32 cc = col;
                    var a = new Vector3(x, _h.Vertex(x, y), y);
                    var b = new Vector3(x, _h.Vertex(x, y + 1), y + 1);
                    var d = new Vector3(x + 1, _h.Vertex(x + 1, y), y);
                    var e = new Vector3(x + 1, _h.Vertex(x + 1, y + 1), y + 1);
                    // Split the quad along the flatter diagonal to avoid creases.
                    if (Mathf.Abs(a.y - e.y) <= Mathf.Abs(b.y - d.y)) { Tri(a, b, e, cc); Tri(a, e, d, cc); }
                    else { Tri(a, b, d, cc); Tri(b, e, d, cc); }
                }
            Assign(c.Ground, "ground");

            // ---- water surface
            _mb.Clear();
            var wcol = Palette.TerrainColor(Terrain.Water, _m.Theme);
            if (_m.Theme == MapTheme.Winter) wcol = new Color(0.25f, 0.40f, 0.52f);
            for (int y = c.Y0; y < y1; y++)
                for (int x = c.X0; x < x1; x++)
                {
                    var t = _m.Cells[y * _m.Width + x];
                    if (t != Terrain.Water && t != Terrain.Shallow && t != Terrain.Bridge) continue;
                    float wy = _m.LevelAt(x, y) * Conv.LevelHeight - 0.08f;
                    var col = t == Terrain.Shallow ? Color.Lerp(wcol, Palette.TerrainColor(Terrain.Sand, _m.Theme), 0.25f).WithA(0.55f) : wcol.WithA(0.85f);
                    Color32 cc = col;
                    _mb.QuadRaw(new Vector3(x, wy, y), new Vector3(x, wy, y + 1), new Vector3(x + 1, wy, y + 1), new Vector3(x + 1, wy, y), cc, cc, cc, cc);
                }
            Assign(c.Water, "water");

            // ---- decor: trees, rocks, mountains, bridges, rubble, cliff faces
            _mb.Clear();
            for (int y = c.Y0; y < y1; y++)
                for (int x = c.X0; x < x1; x++)
                {
                    var t = _m.Cells[y * _m.Width + x];
                    float gx = x + 0.5f, gy = y + 0.5f;
                    float gh = _h.Sample(gx, gy);
                    switch (t)
                    {
                        case Terrain.Forest:
                        {
                            int n = 2 + (int)(Hash(x, y, 1) * 2.5f);
                            for (int i = 0; i < n; i++)
                            {
                                float ox = Hash(x, y, 10 + i) * 0.8f + 0.1f, oy = Hash(x, y, 20 + i) * 0.8f + 0.1f;
                                float s = 0.7f + Hash(x, y, 30 + i) * 0.6f;
                                Tree(new Vector3(x + ox, _h.Sample(x + ox, y + oy), y + oy), s, Hash(x, y, 40 + i));
                            }
                            break;
                        }
                        case Terrain.Rock:
                        {
                            var rc = Palette.TerrainColor(Terrain.Rock, _m.Theme);
                            int n = 1 + (int)(Hash(x, y, 2) * 2);
                            for (int i = 0; i < n; i++)
                            {
                                float ox = Hash(x, y, 50 + i) * 0.6f + 0.2f, oy = Hash(x, y, 60 + i) * 0.6f + 0.2f, s = 0.3f + Hash(x, y, 70 + i) * 0.35f;
                                _mb.Push(new Vector3(x + ox, _h.Sample(x + ox, y + oy) - 0.05f, y + oy), Hash(x, y, 80 + i) * 360f);
                                _mb.Taper(Vector3.zero, new Vector3(s * 1.6f, 0, s * 1.3f), new Vector3(s * 0.7f, 0, s * 0.6f), s, rc.Mul(0.9f + Hash(x, y, 90 + i) * 0.2f));
                                _mb.Pop();
                            }
                            break;
                        }
                        case Terrain.Mountain:
                        {
                            var mc = Palette.TerrainColor(Terrain.Mountain, _m.Theme);
                            float s = 0.9f + Hash(x, y, 3) * 0.5f;
                            _mb.Push(new Vector3(gx, gh - 0.1f, gy), Hash(x, y, 4) * 360f);
                            _mb.Taper(Vector3.zero, new Vector3(1.2f, 0, 1.2f), new Vector3(0.35f * s, 0, 0.3f * s), 0.9f * s, mc.Mul(0.85f + Hash(x, y, 5) * 0.2f),
                                _m.Theme == MapTheme.Winter || _m.Theme == MapTheme.Temperate && Hash(x, y, 6) > 0.6f ? new Color(0.93f, 0.95f, 0.97f) : (Color?)null);
                            _mb.Pop();
                            break;
                        }
                        case Terrain.Cliff:
                        {
                            var cc = Palette.TerrainColor(Terrain.Cliff, _m.Theme);
                            if (Hash(x, y, 7) > 0.5f)
                            {
                                _mb.Push(new Vector3(gx, gh - 0.15f, gy), Hash(x, y, 8) * 360f);
                                _mb.Taper(Vector3.zero, new Vector3(0.9f, 0, 0.8f), new Vector3(0.5f, 0, 0.4f), 0.45f, cc.Mul(0.85f));
                                _mb.Pop();
                            }
                            break;
                        }
                        case Terrain.Bridge:
                        {
                            float deck = Mathf.Max(_m.LevelAt(x, y) * Conv.LevelHeight + 0.1f, gh + 0.05f);
                            var wood = Palette.TerrainColor(Terrain.Bridge, _m.Theme);
                            bool horiz = IsBridge(x - 1, y) || IsBridge(x + 1, y) || !(IsBridge(x, y - 1) || IsBridge(x, y + 1));
                            _mb.Box(new Vector3(gx, deck, gy), new Vector3(1.02f, 0.12f, 1.02f), wood, wood.Mul(1.1f));
                            if (horiz)
                            {
                                _mb.Box(new Vector3(gx, deck + 0.14f, y + 0.05f), new Vector3(1.02f, 0.16f, 0.08f), wood.Mul(0.7f));
                                _mb.Box(new Vector3(gx, deck + 0.14f, y + 0.95f), new Vector3(1.02f, 0.16f, 0.08f), wood.Mul(0.7f));
                                if ((x & 1) == 0) _mb.Box(new Vector3(gx, deck - 0.5f, gy), new Vector3(0.2f, 1f, 0.6f), new Color(0.4f, 0.38f, 0.36f));
                            }
                            else
                            {
                                _mb.Box(new Vector3(x + 0.05f, deck + 0.14f, gy), new Vector3(0.08f, 0.16f, 1.02f), wood.Mul(0.7f));
                                _mb.Box(new Vector3(x + 0.95f, deck + 0.14f, gy), new Vector3(0.08f, 0.16f, 1.02f), wood.Mul(0.7f));
                                if ((y & 1) == 0) _mb.Box(new Vector3(gx, deck - 0.5f, gy), new Vector3(0.6f, 1f, 0.2f), new Color(0.4f, 0.38f, 0.36f));
                            }
                            break;
                        }
                        case Terrain.Rubble:
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                float ox = Hash(x, y, 100 + i) * 0.8f + 0.1f, oy = Hash(x, y, 110 + i) * 0.8f + 0.1f, s = 0.12f + Hash(x, y, 120 + i) * 0.2f;
                                _mb.Push(new Vector3(x + ox, gh, y + oy), Hash(x, y, 130 + i) * 360f);
                                _mb.Box(Vector3.zero, new Vector3(s * 1.5f, s, s), new Color(0.32f, 0.30f, 0.28f).Mul(0.8f + Hash(x, y, 140 + i) * 0.4f));
                                _mb.Pop();
                            }
                            break;
                        }
                        case Terrain.Grass:
                        {
                            if (_m.Theme == MapTheme.Temperate && Hash(x, y, 9) > 0.93f)
                            {
                                var bush = new Color(0.22f, 0.40f, 0.16f);
                                _mb.Sphere(new Vector3(x + Hash(x, y, 11), gh + 0.08f, y + Hash(x, y, 12)), 0.18f, bush, 2, 5);
                            }
                            else if (_m.Theme == MapTheme.Desert && Hash(x, y, 9) > 0.96f)
                            {
                                _mb.Cylinder(new Vector3(x + 0.5f, gh, y + 0.5f), 0.06f, 0.5f, 5, new Color(0.35f, 0.5f, 0.25f));
                            }
                            break;
                        }
                    }
                }
            Assign(c.Decor, "decor");
        }

        bool IsBridge(int x, int y) => _m.Get(x, y) == Terrain.Bridge;

        void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 col)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = _mb.V.Count;
            _mb.V.Add(a); _mb.V.Add(b); _mb.V.Add(c);
            _mb.N.Add(n); _mb.N.Add(n); _mb.N.Add(n);
            _mb.C.Add(col); _mb.C.Add(col); _mb.C.Add(col);
            _mb.T.Add(i); _mb.T.Add(i + 1); _mb.T.Add(i + 2);
        }

        void Tree(Vector3 p, float s, float variant)
        {
            Color leaf, trunk = new Color(0.36f, 0.25f, 0.15f);
            switch (_m.Theme)
            {
                case MapTheme.Winter: leaf = Color.Lerp(new Color(0.18f, 0.32f, 0.22f), new Color(0.85f, 0.9f, 0.92f), 0.35f + variant * 0.3f); break;
                case MapTheme.Desert: leaf = new Color(0.40f, 0.45f, 0.20f); break;
                case MapTheme.Wasteland: leaf = new Color(0.30f, 0.28f, 0.18f); trunk = new Color(0.25f, 0.20f, 0.15f); break;
                default: leaf = Color.Lerp(new Color(0.14f, 0.34f, 0.12f), new Color(0.26f, 0.44f, 0.16f), variant); break;
            }
            _mb.Cylinder(p, 0.05f * s, 0.25f * s, 5, trunk);
            if (_m.Theme == MapTheme.Wasteland && variant > 0.5f)
            {
                _mb.Cylinder(p + Vector3.up * 0.25f * s, 0.04f * s, 0.35f * s, 4, trunk, null, 0.01f);
                return;
            }
            if (variant > 0.45f || _m.Theme == MapTheme.Winter)
            {
                _mb.Cone(p + Vector3.up * 0.18f * s, 0.32f * s, 0.45f * s, 6, leaf);
                _mb.Cone(p + Vector3.up * 0.42f * s, 0.24f * s, 0.4f * s, 6, leaf.Mul(1.08f));
            }
            else
            {
                _mb.Sphere(p + Vector3.up * 0.45f * s, 0.3f * s, leaf, 3, 6);
            }
        }

        void BuildOre(ChunkData c)
        {
            c.OreHash = OreHashOf(c);
            int x1 = Mathf.Min(c.X0 + Chunk, _m.Width), y1 = Mathf.Min(c.Y0 + Chunk, _m.Height);
            _mb.Clear();
            for (int y = c.Y0; y < y1; y++)
                for (int x = c.X0; x < x1; x++)
                {
                    int i = y * _m.Width + x;
                    int b = OreBucket(_m.Ore[i]);
                    if (b == 0) continue;
                    bool aurite = _m.OreKind[i] == 1;
                    var col = aurite ? new Color(0.35f, 0.85f, 0.95f) : new Color(0.80f, 0.62f, 0.22f);
                    for (int k = 0; k < b; k++)
                    {
                        float ox = Hash(x, y, 200 + k) * 0.8f + 0.1f, oy = Hash(x, y, 210 + k) * 0.8f + 0.1f;
                        float s = 0.12f + Hash(x, y, 220 + k) * 0.1f + b * 0.02f;
                        var p = new Vector3(x + ox, _h.Sample(x + ox, y + oy) - 0.02f, y + oy);
                        _mb.Push(p, Quaternion.Euler(Hash(x, y, 230 + k) * 30 - 15, Hash(x, y, 240 + k) * 360, Hash(x, y, 250 + k) * 30 - 15), Vector3.one);
                        if (aurite) { _mb.Cylinder(Vector3.zero, s * 0.6f, s * 2.6f, 5, col.Mul(0.9f + Hash(x, y, 260 + k) * 0.25f), null, 0f, false); }
                        else _mb.Taper(Vector3.zero, new Vector3(s * 1.8f, 0, s * 1.6f), new Vector3(s * 0.6f, 0, s * 0.5f), s * 1.1f, col.Mul(0.85f + Hash(x, y, 260 + k) * 0.3f));
                        _mb.Pop();
                    }
                }
            foreach (var v in _m.OreVents)
            {
                if (v.X < c.X0 || v.Y < c.Y0 || v.X >= x1 || v.Y >= y1) continue;
                var p = new Vector3(v.X + 0.5f, _h.Sample(v.X + 0.5f, v.Y + 0.5f), v.Y + 0.5f);
                _mb.Cylinder(p, 0.35f, 0.25f, 8, new Color(0.3f, 0.28f, 0.26f), new Color(0.15f, 0.12f, 0.1f), 0.22f);
            }
            Assign(c.Ore, "ore");
        }

        void Assign(MeshFilter f, string name)
        {
            if (f.sharedMesh == null) f.sharedMesh = new Mesh { name = name };
            _mb.Fill(f.sharedMesh);
        }

        /// <summary>A dark skirt around the playable area so the map edge reads clearly.</summary>
        void BuildSkirt()
        {
            _mb.Clear();
            var col = new Color(0.05f, 0.05f, 0.06f);
            float W = _m.Width, H = _m.Height, e = 40f, y = -0.6f;
            _mb.Quad(new Vector3(-e, y, -e), new Vector3(-e, y, H + e), new Vector3(0, y, H + e), new Vector3(0, y, -e), col);
            _mb.Quad(new Vector3(W, y, -e), new Vector3(W, y, H + e), new Vector3(W + e, y, H + e), new Vector3(W + e, y, -e), col);
            _mb.Quad(new Vector3(0, y, -e), new Vector3(0, y, 0), new Vector3(W, y, 0), new Vector3(W, y, -e), col);
            _mb.Quad(new Vector3(0, y, H), new Vector3(0, y, H + e), new Vector3(W, y, H + e), new Vector3(W, y, H), col);
            var f = MakeFilter("Skirt", Materials.Lit);
            f.GetComponent<MeshRenderer>().sharedMaterial = Materials.LitNoFog;
            f.sharedMesh = _mb.ToMesh("skirt");
        }
    }
}
