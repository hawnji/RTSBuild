using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// Per-frame gameplay overlays: selection rings, health/veterancy/ammo bars, rally and order lines, building
    /// placement ghost, support-power targeting circles, incoming-strike warnings and move markers.
    /// </summary>
    public class OverlayRenderer
    {
        readonly World _w;
        readonly ViewManager _views;
        readonly PlayerController _pc;
        readonly MeshBuilder _ground = new MeshBuilder(), _top = new MeshBuilder();
        readonly Mesh _groundMesh, _topMesh;
        readonly List<(Vector3 pos, float t)> _markers = new List<(Vector3, float)>();

        public OverlayRenderer(World w, ViewManager views, PlayerController pc, Transform parent)
        {
            _w = w; _views = views; _pc = pc;
            _groundMesh = new Mesh { name = "overlay_ground" }; _groundMesh.MarkDynamic();
            _topMesh = new Mesh { name = "overlay_top" }; _topMesh.MarkDynamic();
            Make("Overlay Ground", _groundMesh, Materials.Transparent, parent);
            Make("Overlay Top", _topMesh, Materials.Overlay, parent);
            pc.OnMoveMarker += p => _markers.Add((p, 0.6f));
        }

        static void Make(string n, Mesh m, Material mat, Transform parent)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float G(float x, float y) => Conv.Ground(_w.Map, x, y) + 0.04f;

        public void Update(float dt, Camera cam, float time)
        {
            var sel = new HashSet<int>(_pc.Selection);
            var right = cam.transform.right; var up = cam.transform.up;

            // ---- selection rings and bars
            foreach (var kv in _views.Views)
            {
                var v = kv.Value;
                if (!v.Visible) continue;
                var e = _w.Get(kv.Key);
                if (e == null) continue;
                bool selected = sel.Contains(e.Id);
                bool hovered = _pc.Hover == e;
                bool damaged = _w.Time - e.LastDamagedTime < 3f;
                bool showBar = selected || hovered || _pc.ShowAllHealth || ClientSettings.Current.AlwaysShowHealth || (damaged && e.Owner == _pc.Player);
                Color ringCol = e.Owner == _pc.Player ? new Color(0.35f, 1f, 0.35f, 0.9f) : _w.IsEnemy(_pc.Player, e.Owner) ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(1f, 0.9f, 0.3f, 0.9f);
                var pos = v.Go.transform.position;
                if (e is Building b)
                {
                    if (selected || hovered)
                    {
                        float x0 = b.Origin.X, y0 = b.Origin.Y, x1 = b.Origin.X + b.W, y1 = b.Origin.Y + b.H;
                        Corner(x0, y0, 1, 1, ringCol.WithA(selected ? 0.9f : 0.5f)); Corner(x1, y0, -1, 1, ringCol.WithA(selected ? 0.9f : 0.5f));
                        Corner(x0, y1, 1, -1, ringCol.WithA(selected ? 0.9f : 0.5f)); Corner(x1, y1, -1, -1, ringCol.WithA(selected ? 0.9f : 0.5f));
                    }
                    if (showBar && b.Def.Selectable)
                    {
                        var top = pos + Vector3.up * (v.Parts.Height + 0.4f);
                        Bar(top, right, up, b.W * 0.45f, e.HpRatio, 0.09f);
                        if (b.Repairing) Billboard(_top, top + up * 0.25f + right * (b.W * 0.45f + 0.2f), right, up, 0.1f, new Color(1f, 0.9f, 0.2f, Mathf.PingPong(time * 2, 1)));
                    }
                    if (selected && b.Owner == _pc.Player && b.HasRally)
                        GroundLine(b.Center, b.Rally, new Color(0.6f, 1f, 0.6f, 0.6f), 0.06f);
                    if (selected && b.Def.Weapons.Count > 0) RangeRing(b.Center, Catalog.Weapons[b.Def.Weapons[0]].Range, new Color(1f, 1f, 1f, 0.12f));
                }
                else
                {
                    var u = (Unit)e;
                    float r = Mathf.Max(0.25f, u.Radius + 0.08f);
                    if (selected || hovered)
                    {
                        var gp = u.IsAir ? new Vector3(pos.x, G(pos.x, pos.z), pos.z) : pos + Vector3.up * 0.04f;
                        RingAt(gp, r, r + (selected ? 0.06f : 0.03f), ringCol.WithA(selected ? 0.9f : 0.45f));
                        if (u.IsAir && selected) GroundLineW(gp, pos, ringCol.WithA(0.25f), 0.02f);
                    }
                    if (showBar)
                    {
                        var top = pos + Vector3.up * (v.Parts.Height + 0.25f);
                        float w = Mathf.Max(0.3f, r * 1.4f);
                        Bar(top, right, up, w, e.HpRatio, 0.06f);
                        // veterancy chevrons
                        for (int i = 0; i < e.Rank; i++) Billboard(_top, top + right * (w + 0.1f + i * 0.13f), right, up, 0.055f, e.Rank >= 2 ? new Color(1f, 0.85f, 0.2f) : new Color(0.85f, 0.85f, 0.9f));
                        if (u.Def.Ammo > 0) SubBar(top - up * 0.09f, right, up, w, u.Ammo / (float)u.Def.Ammo, new Color(0.9f, 0.85f, 0.4f));
                        if (u.Def.Has(UnitAbility.Harvester)) SubBar(top - up * 0.09f, right, up, w, u.Cargo / Mathf.Max(1f, u.Def.HarvestCapacity), new Color(0.95f, 0.75f, 0.2f));
                        if (u.Passengers.Count > 0 && u.Def.TransportCapacity > 0) SubBar(top - up * 0.09f, right, up, w, u.Passengers.Count / (float)u.Def.TransportCapacity, new Color(0.5f, 0.8f, 1f));
                    }
                    if (selected && u.Owner == _pc.Player)
                    {
                        if (u.Moving && (u.CurrentOrder.Type == OrderType.Move || u.CurrentOrder.Type == OrderType.AttackMove))
                            GroundLine(u.Pos, u.CurrentOrder.Pos, u.CurrentOrder.Type == OrderType.AttackMove ? new Color(1f, 0.45f, 0.3f, 0.35f) : new Color(0.5f, 1f, 0.5f, 0.3f), 0.03f);
                        var prev = u.CurrentOrder.Pos;
                        foreach (var o in u.OrderQueue) { GroundLine(prev, o.Pos, new Color(0.7f, 0.9f, 1f, 0.3f), 0.03f); prev = o.Pos; }
                        if (u.CurrentOrder.Type == OrderType.Patrol) GroundLine(u.CurrentOrder.Pos, u.CurrentOrder.Pos2, new Color(0.6f, 0.8f, 1f, 0.35f), 0.03f);
                        var tgt = _w.Get(u.TargetId);
                        if (tgt != null && _views.IsVisible(tgt)) GroundLine(u.Pos, tgt.Pos, new Color(1f, 0.3f, 0.25f, 0.35f), 0.025f);
                    }
                }
            }

            // ---- move markers
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                m.t -= dt;
                if (m.t <= 0) { _markers.RemoveAt(i); continue; }
                _markers[i] = m;
                float s = 0.15f + (0.6f - m.t) * 0.6f;
                RingAt(m.pos + Vector3.up * 0.05f, s, s + 0.06f, new Color(0.4f, 1f, 0.4f, m.t / 0.6f));
            }

            // ---- incoming strikes (superweapons are announced to everyone; minor powers only to their owner's team)
            foreach (var s in _w.Powers.Pending)
            {
                var def = SupportPowerSystem.Def(s.Kind);
                if (!def.IsSuperweapon && _w.TeamOf(s.Owner) != _views.LocalTeam) continue;
                float pulse = 0.4f + 0.4f * Mathf.Sin(time * 8f);
                var c = new Vector3(s.Target.X, G(s.Target.X, s.Target.Y), s.Target.Y);
                RingAt(c, def.Radius - 0.15f, def.Radius, new Color(1f, 0.2f, 0.1f, pulse));
                RingAt(c, 0.2f, 0.35f, new Color(1f, 0.2f, 0.1f, pulse));
            }

            // ---- placement ghost
            if (_pc.Mode == CursorMode.Place && _pc.HoverGround.HasValue)
            {
                var me = _w.GetPlayer(_pc.Player);
                var def = Catalog.Building(me.Ready[(int)_pc.PlaceQueue]);
                if (def != null)
                {
                    bool rot = _pc.PlaceRotated && def.Rotatable;
                    var o = _pc.PlacementOrigin(def);
                    int w = rot ? def.Height : def.Width, h = rot ? def.Width : def.Height;
                    bool okAll = _w.Production.CanPlace(_pc.Player, def, o, rot);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int cx = o.X + x, cy = o.Y + y;
                            bool ok = _w.Map.InBounds(cx, cy) && CellOk(def, cx, cy) && okAll;
                            CellQuad(cx, cy, ok ? new Color(0.3f, 1f, 0.3f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.45f));
                        }
                    var parts = ModelLibrary.ForBuilding(def, Palette.ForPlayer(me), me.ColorIndex % 7, w, h);
                    var center = new Vector3(o.X + w * 0.5f, G(o.X + w * 0.5f, o.Y + h * 0.5f), o.Y + h * 0.5f);
                    if (parts.Body != null) Graphics.DrawMesh(parts.Body, Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), Materials.Lit, 0);
                    if (def.Weapons.Count > 0) RangeRing(new Vec2(center.x, center.z), Catalog.Weapons[def.Weapons[0]].Range, new Color(1f, 1f, 1f, 0.15f));
                    if (def.CountermeasureRadius > 0) RangeRing(new Vec2(center.x, center.z), def.CountermeasureRadius, new Color(0.4f, 0.8f, 1f, 0.2f));
                }
            }

            // ---- power targeting
            if (_pc.Mode == CursorMode.Power && _pc.HoverGround.HasValue)
            {
                var def = SupportPowerSystem.Def(_pc.PowerKind);
                var g = _pc.HoverGround.Value;
                RingAt(g + Vector3.up * 0.05f, def.Radius - 0.1f, def.Radius, new Color(1f, 0.6f, 0.2f, 0.7f));
                RingAt(g + Vector3.up * 0.05f, 0.15f, 0.25f, new Color(1f, 0.6f, 0.2f, 0.9f));
            }

            _ground.Fill(_groundMesh); _ground.Clear();
            _top.Fill(_topMesh); _top.Clear();
            var big = new Bounds(new Vector3(_w.Map.Width * 0.5f, 0, _w.Map.Height * 0.5f), new Vector3(_w.Map.Width + 20, 60, _w.Map.Height + 20));
            _groundMesh.bounds = big; _topMesh.bounds = big;
        }

        bool CellOk(BuildingDef def, int x, int y)
        {
            if (_w.BuildingAtCell(x, y) != null) return false;
            var t = _w.Map.Get(x, y);
            if (def.OnWater) return t == Terrain.Water;
            return t != Terrain.Water && t != Terrain.Cliff && t != Terrain.Mountain && t != Terrain.Forest && t != Terrain.Rock && t != Terrain.Bridge && t != Terrain.Shallow;
        }

        void CellQuad(int x, int y, Color c)
        {
            Color32 cc = c;
            _ground.QuadRaw(new Vector3(x + 0.05f, G(x, y), y + 0.05f), new Vector3(x + 0.05f, G(x, y + 1), y + 0.95f), new Vector3(x + 0.95f, G(x + 1, y + 1), y + 0.95f), new Vector3(x + 0.95f, G(x + 1, y), y + 0.05f), cc, cc, cc, cc);
        }

        void Corner(float x, float y, int dx, int dy, Color c)
        {
            float l = 0.4f, t = 0.08f;
            float h = G(x, y) + 0.05f;
            Color32 cc = c;
            _ground.QuadRaw(new Vector3(x, h, y), new Vector3(x, h, y + t * dy), new Vector3(x + l * dx, h, y + t * dy), new Vector3(x + l * dx, h, y), cc, cc, cc, cc);
            _ground.QuadRaw(new Vector3(x, h, y), new Vector3(x, h, y + l * dy), new Vector3(x + t * dx, h, y + l * dy), new Vector3(x + t * dx, h, y), cc, cc, cc, cc);
        }

        void RingAt(Vector3 c, float r0, float r1, Color col)
        {
            int seg = Mathf.Clamp((int)(r1 * 24), 16, 64);
            Color32 cc = col;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                var p0 = c + d0 * r0; var p1 = c + d1 * r0; var p2 = c + d1 * r1; var p3 = c + d0 * r1;
                if (r1 > 1.5f) { p0.y = G(p0.x, p0.z); p1.y = G(p1.x, p1.z); p2.y = G(p2.x, p2.z); p3.y = G(p3.x, p3.z); }
                _ground.QuadRaw(p0, p1, p2, p3, cc, cc, cc, cc);
            }
        }

        void RangeRing(Vec2 c, float r, Color col) => RingAt(new Vector3(c.X, G(c.X, c.Y), c.Y), r - 0.05f, r, col);

        void GroundLine(Vec2 a, Vec2 b, Color c, float w)
        {
            var pa = new Vector3(a.X, G(a.X, a.Y), a.Y); var pb = new Vector3(b.X, G(b.X, b.Y), b.Y);
            float len = Vector3.Distance(pa, pb);
            int seg = Mathf.Clamp((int)(len / 2f), 1, 20);
            for (int i = 0; i < seg; i++)
            {
                var p0 = Vector3.Lerp(pa, pb, i / (float)seg); var p1 = Vector3.Lerp(pa, pb, (i + 1) / (float)seg);
                p0.y = G(p0.x, p0.z); p1.y = G(p1.x, p1.z);
                GroundLineW(p0, p1, c, w);
            }
        }

        void GroundLineW(Vector3 a, Vector3 b, Color c, float w)
        {
            var d = b - a; d.y = 0;
            var side = Vector3.Cross(Vector3.up, d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward) * w;
            if ((b - a).y > 0.5f && d.sqrMagnitude < 0.01f) side = Vector3.right * w;
            Color32 cc = c;
            _ground.QuadRaw(a - side, b - side, b + side, a + side, cc, cc, cc, cc);
        }

        void Bar(Vector3 center, Vector3 right, Vector3 up, float halfW, float frac, float h)
        {
            frac = Mathf.Clamp01(frac);
            var col = frac > 0.6f ? new Color(0.3f, 0.95f, 0.3f) : frac > 0.3f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.25f, 0.2f);
            Rect2(center, right, up, -halfW - 0.02f, halfW + 0.02f, -h * 0.5f - 0.02f, h * 0.5f + 0.02f, new Color(0, 0, 0, 0.75f));
            Rect2(center, right, up, -halfW, -halfW + 2 * halfW * frac, -h * 0.5f, h * 0.5f, col);
        }

        void SubBar(Vector3 center, Vector3 right, Vector3 up, float halfW, float frac, Color col)
        {
            frac = Mathf.Clamp01(frac);
            Rect2(center, right, up, -halfW - 0.02f, halfW + 0.02f, -0.035f, 0.035f, new Color(0, 0, 0, 0.7f));
            Rect2(center, right, up, -halfW, -halfW + 2 * halfW * frac, -0.02f, 0.02f, col);
        }

        void Rect2(Vector3 c, Vector3 right, Vector3 up, float x0, float x1, float y0, float y1, Color col)
        {
            Color32 cc = col;
            _top.QuadRaw(c + right * x0 + up * y0, c + right * x0 + up * y1, c + right * x1 + up * y1, c + right * x1 + up * y0, cc, cc, cc, cc);
        }

        static void Billboard(MeshBuilder mb, Vector3 c, Vector3 right, Vector3 up, float s, Color col)
        {
            Color32 cc = col;
            mb.QuadRaw(c - right * s - up * s, c - right * s + up * s, c + right * s + up * s, c + right * s - up * s, cc, cc, cc, cc);
        }
    }
}
