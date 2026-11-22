using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTS.Client
{
    /// <summary>
    /// Accumulates flat-shaded, vertex-coloured geometry. All procedural models, terrain chunks and effects are built
    /// through this class so that no external art assets are required.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Color32> C = new List<Color32>();
        public readonly List<int> T = new List<int>();
        public Matrix4x4 M = Matrix4x4.identity;
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public int Count => V.Count;
        public void Clear() { V.Clear(); N.Clear(); C.Clear(); T.Clear(); M = Matrix4x4.identity; _stack.Clear(); }

        public void Push(Vector3 pos, Quaternion rot, Vector3 scale) { _stack.Push(M); M = M * Matrix4x4.TRS(pos, rot, scale); }
        public void Push(Vector3 pos) => Push(pos, Quaternion.identity, Vector3.one);
        public void Push(Vector3 pos, float yawDeg) => Push(pos, Quaternion.Euler(0, yawDeg, 0), Vector3.one);
        public void Pop() { M = _stack.Count > 0 ? _stack.Pop() : Matrix4x4.identity; }

        Vector3 P(Vector3 p) => M.MultiplyPoint3x4(p);

        /// <summary>Adds a flat triangle (positions in local space).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            a = P(a); b = P(b); c = P(c);
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            N.Add(n); N.Add(n); N.Add(n);
            Color32 cc = col; C.Add(cc); C.Add(cc); C.Add(cc);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        /// <summary>Adds a flat quad a-b-c-d (clockwise when viewed from the front, Unity convention).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            a = P(a); b = P(b); c = P(c); d = P(d);
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (n.sqrMagnitude < 0.5f) n = Vector3.Cross(c - a, d - a).normalized;
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            N.Add(n); N.Add(n); N.Add(n); N.Add(n);
            Color32 cc = col; C.Add(cc); C.Add(cc); C.Add(cc); C.Add(cc);
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        /// <summary>Quad with per-vertex colours and an explicit normal (used by terrain).</summary>
        public void QuadRaw(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            N.Add(n); N.Add(n); N.Add(n); N.Add(n);
            C.Add(ca); C.Add(cb); C.Add(cc); C.Add(cd);
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        /// <summary>Axis-aligned box centred at c (local), size s. Optional top colour for a two-tone look.</summary>
        public void Box(Vector3 c, Vector3 s, Color col, Color? top = null, bool bottom = false)
        {
            var h = s * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z), p010 = c + new Vector3(-h.x, h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z), p011 = c + new Vector3(-h.x, h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z);
            Quad(p010, p011, p111, p110, top ?? col.Mul(1.08f));       // top
            Quad(p000, p010, p110, p100, col);                          // front (-z)
            Quad(p101, p111, p011, p001, col.Mul(0.92f));               // back (+z)
            Quad(p001, p011, p010, p000, col.Mul(0.86f));               // left
            Quad(p100, p110, p111, p101, col.Mul(0.86f));               // right
            if (bottom) Quad(p001, p000, p100, p101, col.Mul(0.6f));
        }

        /// <summary>Box with a narrower top (truncated pyramid) — hulls, turrets, bunkers.</summary>
        public void Taper(Vector3 c, Vector3 bottomSize, Vector3 topSize, float height, Color col, Color? top = null)
        {
            float bx = bottomSize.x * 0.5f, bz = bottomSize.z * 0.5f, tx = topSize.x * 0.5f, tz = topSize.z * 0.5f;
            float y0 = c.y, y1 = c.y + height;
            Vector3 b0 = new Vector3(c.x - bx, y0, c.z - bz), b1 = new Vector3(c.x + bx, y0, c.z - bz), b2 = new Vector3(c.x + bx, y0, c.z + bz), b3 = new Vector3(c.x - bx, y0, c.z + bz);
            Vector3 t0 = new Vector3(c.x - tx, y1, c.z - tz), t1 = new Vector3(c.x + tx, y1, c.z - tz), t2 = new Vector3(c.x + tx, y1, c.z + tz), t3 = new Vector3(c.x - tx, y1, c.z + tz);
            Quad(t0, t3, t2, t1, top ?? col.Mul(1.08f));
            Quad(b0, t0, t1, b1, col);
            Quad(b2, t2, t3, b3, col.Mul(0.92f));
            Quad(b3, t3, t0, b0, col.Mul(0.86f));
            Quad(b1, t1, t2, b2, col.Mul(0.86f));
        }

        /// <summary>Cylinder along local Y from base centre c.</summary>
        public void Cylinder(Vector3 c, float r, float h, int seg, Color col, Color? top = null, float rTop = -1, bool cap = true)
        {
            if (rTop < 0) rTop = r;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                float shade = 0.82f + 0.18f * Mathf.Cos((a0 + a1) * 0.5f - 0.8f);
                Quad(c + d0 * r, c + d0 * rTop + Vector3.up * h, c + d1 * rTop + Vector3.up * h, c + d1 * r, col.Mul(shade));
                if (cap && rTop > 0.001f) Tri(c + Vector3.up * h, c + d1 * rTop + Vector3.up * h, c + d0 * rTop + Vector3.up * h, top ?? col.Mul(1.08f));
            }
        }

        /// <summary>Cylinder along local Z (barrels, tubes) from start point.</summary>
        public void Barrel(Vector3 start, float r, float len, Color col, int seg = 6)
        {
            Push(start, Quaternion.Euler(90, 0, 0), Vector3.one);
            Cylinder(Vector3.zero, r, len, seg, col, col.Mul(0.5f));
            Pop();
        }

        public void Cone(Vector3 c, float r, float h, int seg, Color col) => Cylinder(c, r, h, seg, col, null, 0f, false);

        /// <summary>Low-poly sphere (icosahedron-ish via lat/long bands).</summary>
        public void Sphere(Vector3 c, float r, Color col, int bands = 4, int seg = 6)
        {
            for (int b = 0; b < bands; b++)
            {
                float t0 = Mathf.PI * b / bands - Mathf.PI / 2, t1 = Mathf.PI * (b + 1) / bands - Mathf.PI / 2;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector3 p00 = c + Sph(a0, t0) * r, p10 = c + Sph(a1, t0) * r, p01 = c + Sph(a0, t1) * r, p11 = c + Sph(a1, t1) * r;
                    Quad(p00, p01, p11, p10, col.Mul(0.85f + 0.15f * (b + 1) / bands));
                }
            }
        }

        static Vector3 Sph(float a, float t) => new Vector3(Mathf.Cos(a) * Mathf.Cos(t), Mathf.Sin(t), Mathf.Sin(a) * Mathf.Cos(t));

        /// <summary>Flat ring on the XZ plane (selection circles, range indicators).</summary>
        public void Ring(Vector3 c, float r0, float r1, int seg, Color col)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Quad(c + d0 * r0, c + d1 * r0, c + d1 * r1, c + d0 * r1, col);
            }
        }

        /// <summary>Wedge/prism for sloped armour, roofs and wings: a triangular cross-section extruded along X.</summary>
        public void Wedge(Vector3 c, Vector3 s, Color col, float peakZ = 0f)
        {
            var h = s * 0.5f;
            Vector3 a0 = c + new Vector3(-h.x, -h.y, -h.z), b0 = c + new Vector3(-h.x, -h.y, h.z), p0 = c + new Vector3(-h.x, h.y, peakZ * h.z);
            Vector3 a1 = c + new Vector3(h.x, -h.y, -h.z), b1 = c + new Vector3(h.x, -h.y, h.z), p1 = c + new Vector3(h.x, h.y, peakZ * h.z);
            Quad(a0, p0, p1, a1, col);
            Quad(b1, p1, p0, b0, col.Mul(0.9f));
            Tri(a0, b0, p0, col.Mul(0.85f));
            Tri(a1, p1, b1, col.Mul(0.85f));
        }

        public Mesh ToMesh(string name = "proc", bool dynamic = false)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            if (dynamic) m.MarkDynamic();
            m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }

        public void Fill(Mesh m)
        {
            m.Clear();
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
            m.RecalculateBounds();
        }
    }

    /// <summary>Shared materials created from the bundled shaders, with a safe fallback.</summary>
    public static class Materials
    {
        static Material _lit, _trans, _transTop, _add, _water;

        static Shader Load(string res, string fallback)
        {
            var s = Resources.Load<Shader>("Shaders/" + res);
            if (s == null) s = Shader.Find(fallback);
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        public static Material Lit { get { if (_lit == null) { _lit = new Material(Load("RTSVertexLit", "RTS/VertexLit")) { name = "RTS Lit", enableInstancing = true }; } return _lit; } }
        public static Material Transparent { get { if (_trans == null) _trans = new Material(Load("RTSTransparent", "RTS/Transparent")) { name = "RTS Transparent" }; return _trans; } }
        /// <summary>Transparent overlay drawn on top of geometry (health bars, placement grid).</summary>
        public static Material Overlay
        {
            get
            {
                if (_transTop == null)
                {
                    _transTop = new Material(Load("RTSTransparent", "RTS/Transparent")) { name = "RTS Overlay", renderQueue = 3500 };
                    _transTop.SetFloat("_ZTest", (float)CompareFunction.Always);
                }
                return _transTop;
            }
        }
        public static Material Additive { get { if (_add == null) _add = new Material(Load("RTSAdditive", "RTS/Additive")) { name = "RTS Additive" }; return _add; } }
        public static Material Water { get { if (_water == null) _water = new Material(Load("RTSWater", "RTS/Water")) { name = "RTS Water" }; return _water; } }

        static Material _litNoFog;
        public static Material LitNoFog => LitNoFogShared;
        public static Material LitNoFogShared { get { if (_litNoFog == null) { _litNoFog = new Material(Lit) { name = "RTS Lit (no fog)" }; _litNoFog.SetFloat("_FogAmount", 0f); } return _litNoFog; } }
    }
}
