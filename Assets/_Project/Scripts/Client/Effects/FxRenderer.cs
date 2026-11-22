using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// Batched particle and line effects. Every particle, tracer, beam and projectile is written into two dynamic
    /// meshes per frame (alpha-blended and additive), so the whole effects layer costs two draw calls.
    /// </summary>
    public class FxRenderer
    {
        struct Particle
        {
            public Vector3 Pos, Vel;
            public float Life, MaxLife, Size, Grow, Gravity, Drag;
            public Color C0, C1;
            public bool Add;
        }

        struct Line
        {
            public Vector3 A, B;
            public float Life, MaxLife, Width;
            public Color C;
            public bool Add;
        }

        readonly List<Particle> _p = new List<Particle>(4096);
        readonly List<Line> _l = new List<Line>(512);
        readonly MeshBuilder _alpha = new MeshBuilder(), _add = new MeshBuilder();
        readonly Mesh _alphaMesh, _addMesh;
        readonly GameObject _alphaGo, _addGo;
        readonly World _w;
        public int MaxParticles = 3500;
        public float Quality = 1f;     // 0.5 = low effects

        public FxRenderer(World w, Transform parent)
        {
            _w = w;
            _alphaMesh = new Mesh { name = "fx_alpha" }; _alphaMesh.MarkDynamic();
            _addMesh = new Mesh { name = "fx_add" }; _addMesh.MarkDynamic();
            _alphaGo = MakeGo("FX Alpha", _alphaMesh, Materials.Transparent, parent);
            _addGo = MakeGo("FX Additive", _addMesh, Materials.Additive, parent);
        }

        static GameObject MakeGo(string n, Mesh m, Material mat, Transform parent)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------------------------------------------------------------- emitters
        public void Emit(Vector3 pos, Vector3 vel, float life, float size, Color c0, Color c1, bool add, float grow = 0, float gravity = 0, float drag = 0)
        {
            if (_p.Count >= MaxParticles) return;
            _p.Add(new Particle { Pos = pos, Vel = vel, Life = life, MaxLife = life, Size = size, Grow = grow, C0 = c0, C1 = c1, Add = add, Gravity = gravity, Drag = drag });
        }

        public void LineFx(Vector3 a, Vector3 b, float life, float width, Color c, bool add = true)
        {
            if (_l.Count > 600) return;
            _l.Add(new Line { A = a, B = b, Life = life, MaxLife = life, Width = width, C = c, Add = add });
        }

        int N(int n) => Mathf.Max(1, Mathf.RoundToInt(n * Quality));

        public void Explosion(Vector3 p, float size)
        {
            size = Mathf.Max(0.3f, size);
            for (int i = 0; i < N((int)(10 + size * 10)); i++)
            {
                var d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) + 0.2f;
                Emit(p + d * 0.1f * size, d * (1.5f + size * 1.5f), 0.35f + Random.value * 0.3f, 0.25f * size + 0.15f, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.25f, 0.05f, 0f), true, size * 1.2f, 0, 3f);
            }
            for (int i = 0; i < N((int)(6 + size * 6)); i++)
            {
                var d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f + 0.3f;
                Emit(p + d * 0.2f * size, d * (0.6f + size * 0.5f), 1.2f + Random.value * 1.0f, 0.3f * size + 0.2f, new Color(0.25f, 0.22f, 0.2f, 0.75f), new Color(0.3f, 0.3f, 0.3f, 0f), false, size * 0.9f, -0.3f, 1.5f);
            }
            for (int i = 0; i < N((int)(4 + size * 4)); i++)
            {
                var d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) + 0.8f;
                Emit(p, d * (3f + size * 2f), 0.6f + Random.value * 0.4f, 0.06f, new Color(1f, 0.8f, 0.4f), new Color(0.6f, 0.2f, 0.05f, 0f), true, 0, 9.8f, 0.5f);
            }
            Emit(p + Vector3.up * 0.1f, Vector3.zero, 0.18f, size * 1.4f, new Color(1f, 0.95f, 0.8f, 0.9f), new Color(1f, 0.6f, 0.2f, 0f), true, size * 4f);
        }

        public void Smoke(Vector3 p, float size, Color c)
        {
            Emit(p, new Vector3(Random.Range(-0.15f, 0.15f), 0.5f + Random.value * 0.3f, Random.Range(-0.15f, 0.15f)), 1.5f + Random.value, size, c, new Color(c.r, c.g, c.b, 0f), false, size * 0.8f, -0.1f, 0.5f);
        }

        public void Fire(Vector3 p, float size)
        {
            Emit(p + Random.insideUnitSphere * 0.1f, new Vector3(0, 0.8f + Random.value * 0.6f, 0), 0.4f + Random.value * 0.3f, size, new Color(1f, 0.7f, 0.2f, 0.9f), new Color(0.8f, 0.15f, 0.05f, 0f), true, -size * 0.8f, -0.5f);
        }

        public void Muzzle(Vector3 p, Vector3 dir, float size, Color c)
        {
            Emit(p, dir * 0.5f, 0.07f, size, c, c.WithA(0f), true, size * 2f);
            Emit(p + dir * size * 0.6f, dir * 0.8f, 0.06f, size * 0.7f, c, c.WithA(0f), true, size);
        }

        public void Sparks(Vector3 p, int n, Color c)
        {
            for (int i = 0; i < N(n); i++)
            {
                var d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) + 0.3f;
                Emit(p, d * 2.5f, 0.25f + Random.value * 0.2f, 0.04f, c, c.WithA(0f), true, 0, 6f);
            }
        }

        public void Ring(Vector3 p, float radius, Color c, float life)
        {
            int seg = 28;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Emit(p + Vector3.up * 0.1f, d * radius / life, life, 0.35f, c, c.WithA(0f), true, 0.5f);
            }
        }

        public void Splash(Vector3 p, float size)
        {
            for (int i = 0; i < N(10); i++)
            {
                var d = Random.insideUnitCircle;
                Emit(p, new Vector3(d.x * 0.8f, 2.5f + Random.value * 1.5f, d.y * 0.8f) * size, 0.7f, 0.12f * size + 0.05f, new Color(0.85f, 0.92f, 1f, 0.8f), new Color(0.8f, 0.9f, 1f, 0f), false, 0.3f, 9f);
            }
        }

        // ---------------------------------------------------------------- per-frame
        public void Update(float dt, Camera cam, float alpha, ViewManager views)
        {
            // continuous effects: projectiles
            foreach (var pr in _w.Projectiles)
            {
                if (pr.Done) continue;
                var dir = (pr.Target - pr.Start).Normalized;
                var p2 = pr.Pos + dir * pr.Speed * World.Dt * alpha;
                if (views != null && !views.RevealAll && !_w.Fog.IsVisible(views.LocalTeam, p2)) continue;
                float gh = Conv.Ground(_w.Map, p2);
                var wp = new Vector3(p2.X, gh + 0.3f + pr.Height, p2.Y);
                var back = new Vector3(dir.X, 0, dir.Y);
                switch (pr.Kind)
                {
                    case ProjectileKind.Shell:
                    case ProjectileKind.Bullet:
                        PushLine(_add, cam, wp - back * 0.35f, wp, 0.05f, new Color(1f, 0.85f, 0.5f, 0.9f));
                        break;
                    case ProjectileKind.Ballistic:
                    case ProjectileKind.Bomb:
                        PushBillboard(_add, cam, wp, 0.12f, new Color(1f, 0.7f, 0.3f, 0.9f));
                        if (Random.value < 0.5f * Quality) Smoke(wp, 0.12f, new Color(0.5f, 0.5f, 0.5f, 0.35f));
                        break;
                    case ProjectileKind.Missile:
                        PushLine(_alpha, cam, wp - back * 0.3f, wp, 0.06f, new Color(0.85f, 0.85f, 0.85f, 1f));
                        PushBillboard(_add, cam, wp - back * 0.3f, 0.12f, new Color(1f, 0.6f, 0.2f, 1f));
                        if (Random.value < 0.8f * Quality) Emit(wp - back * 0.3f, Vector3.up * 0.1f, 0.8f, 0.08f, new Color(0.7f, 0.7f, 0.7f, 0.5f), new Color(0.6f, 0.6f, 0.6f, 0f), false, 0.25f);
                        break;
                    case ProjectileKind.Torpedo:
                        wp.y = Conv.Ground(_w.Map, p2) + 0.25f;
                        if (Random.value < 0.6f) Emit(wp, Vector3.up * 0.1f, 0.6f, 0.1f, new Color(0.85f, 0.95f, 1f, 0.6f), new Color(1, 1, 1, 0), false, 0.2f);
                        break;
                    case ProjectileKind.Flame:
                        Fire(wp, 0.18f);
                        break;
                    default:
                        PushBillboard(_add, cam, wp, 0.1f, new Color(1f, 0.9f, 0.6f, 1f));
                        break;
                }
            }

            // fires on the ground
            foreach (var f in _w.Fires)
            {
                if (Random.value < dt * 20f * f.Radius * Quality)
                {
                    var o = Random.insideUnitCircle * f.Radius;
                    var p = new Vector3(f.Pos.X + o.x, 0, f.Pos.Y + o.y);
                    p.y = Conv.Ground(_w.Map, p.x, p.z) + 0.05f;
                    Fire(p, 0.25f);
                    if (Random.value < 0.3f) Smoke(p + Vector3.up * 0.3f, 0.3f, new Color(0.15f, 0.13f, 0.12f, 0.5f));
                }
            }

            // simulate particles
            for (int i = _p.Count - 1; i >= 0; i--)
            {
                var p = _p[i];
                p.Life -= dt;
                if (p.Life <= 0) { _p[i] = _p[_p.Count - 1]; _p.RemoveAt(_p.Count - 1); continue; }
                p.Vel.y -= p.Gravity * dt;
                if (p.Drag > 0) p.Vel *= Mathf.Max(0, 1f - p.Drag * dt);
                p.Pos += p.Vel * dt;
                p.Size = Mathf.Max(0.01f, p.Size + p.Grow * dt);
                _p[i] = p;
                float t = 1f - p.Life / p.MaxLife;
                PushBillboard(p.Add ? _add : _alpha, cam, p.Pos, p.Size, Color.Lerp(p.C0, p.C1, t));
            }
            for (int i = _l.Count - 1; i >= 0; i--)
            {
                var l = _l[i];
                l.Life -= dt;
                if (l.Life <= 0) { _l.RemoveAt(i); continue; }
                _l[i] = l;
                PushLine(l.Add ? _add : _alpha, cam, l.A, l.B, l.Width, l.C.WithA(l.C.a * (l.Life / l.MaxLife)));
            }
            _alpha.Fill(_alphaMesh); _alpha.Clear();
            _add.Fill(_addMesh); _add.Clear();
            // Effects can be anywhere on the map; keep bounds large so they are never culled.
            var big = new Bounds(new Vector3(_w.Map.Width * 0.5f, 0, _w.Map.Height * 0.5f), new Vector3(_w.Map.Width + 50, 60, _w.Map.Height + 50));
            _alphaMesh.bounds = big; _addMesh.bounds = big;
        }

        static void PushBillboard(MeshBuilder mb, Camera cam, Vector3 p, float s, Color c)
        {
            var r = cam.transform.right * s; var u = cam.transform.up * s;
            Color32 cc = c;
            int i = mb.V.Count;
            mb.V.Add(p - r - u); mb.V.Add(p - r + u); mb.V.Add(p + r + u); mb.V.Add(p + r - u);
            var n = -cam.transform.forward;
            mb.N.Add(n); mb.N.Add(n); mb.N.Add(n); mb.N.Add(n);
            mb.C.Add(cc); mb.C.Add(cc); mb.C.Add(cc); mb.C.Add(cc);
            mb.T.Add(i); mb.T.Add(i + 1); mb.T.Add(i + 2); mb.T.Add(i); mb.T.Add(i + 2); mb.T.Add(i + 3);
        }

        static void PushLine(MeshBuilder mb, Camera cam, Vector3 a, Vector3 b, float w, Color c)
        {
            var dir = b - a;
            var side = Vector3.Cross(dir, cam.transform.forward).normalized * w * 0.5f;
            Color32 cc = c;
            int i = mb.V.Count;
            mb.V.Add(a - side); mb.V.Add(a + side); mb.V.Add(b + side); mb.V.Add(b - side);
            var n = -cam.transform.forward;
            mb.N.Add(n); mb.N.Add(n); mb.N.Add(n); mb.N.Add(n);
            mb.C.Add(cc); mb.C.Add(cc); mb.C.Add(cc); mb.C.Add(cc);
            mb.T.Add(i); mb.T.Add(i + 1); mb.T.Add(i + 2); mb.T.Add(i); mb.T.Add(i + 2); mb.T.Add(i + 3);
        }

        public void Clear() { _p.Clear(); _l.Clear(); }
        public int ParticleCount => _p.Count;
    }
}
