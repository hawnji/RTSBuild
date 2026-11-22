using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// Radar minimap: terrain, ore, fog, unit/structure blips, alert pings and the camera view box. Left click/drag
    /// moves the camera, right click orders the selection. Requires an operational radar for the local player.
    /// </summary>
    public class Minimap
    {
        readonly World _w;
        readonly GameSession _s;
        readonly Texture2D _tex;
        readonly Color32[] _base, _px;
        float _timer;
        readonly List<(Vec2 p, Color c, float t)> _pings = new List<(Vec2, Color, float)>();
        bool _dragging;

        public Minimap(World w, GameSession s)
        {
            _w = w; _s = s;
            int W = w.Map.Width, H = w.Map.Height;
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Minimap" };
            _base = new Color32[W * H]; _px = new Color32[W * H];
            RebuildBase();
            Refresh();
        }

        public Texture Texture => _tex;

        void RebuildBase()
        {
            int W = _w.Map.Width;
            for (int y = 0; y < _w.Map.Height; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    var c = Palette.Minimap(_w.Map.Cells[i], _w.Map.Theme).Mul(1f + _w.Map.Level[i] * 0.06f);
                    _base[i] = c;
                }
        }

        public void Ping(Vec2 p, Color c) => _pings.Add((p, c, 3f));

        public bool RadarOnline
        {
            get
            {
                if (_s.Spectator) return true;
                var me = _s.Me;
                return me != null && (me.HasRadar || me.Revealed || _w.Mission != null && !_w.HasRole(me.Id, BuildingRole.ConstructionYard) && _w.Mission.Def.NoBase);
            }
        }

        public void Update(float dt)
        {
            for (int i = _pings.Count - 1; i >= 0; i--) { var p = _pings[i]; p.t -= dt; if (p.t <= 0) _pings.RemoveAt(i); else _pings[i] = p; }
            _timer -= dt;
            if (_timer > 0) return;
            _timer = 0.25f;
            Refresh();
        }

        int _baseHash;
        void Refresh()
        {
            int W = _w.Map.Width, H = _w.Map.Height;
            // terrain can change (rubble, destroyed bridges): cheap hash check
            int hsh = 0; for (int i = 0; i < _w.Map.Cells.Length; i += 7) hsh = hsh * 31 + (int)_w.Map.Cells[i];
            if (hsh != _baseHash) { _baseHash = hsh; RebuildBase(); }
            int team = _w.TeamOf(_s.LocalPlayer);
            bool fog = _w.Fog.Enabled && !_s.Spectator;
            for (int i = 0; i < _px.Length; i++)
            {
                var c = _base[i];
                if (_w.Map.Ore[i] > 0) c = _w.Map.OreKind[i] == 1 ? new Color32(90, 200, 230, 255) : new Color32(200, 160, 60, 255);
                if (fog)
                {
                    byte st = _w.Fog.CellState(team, i % W, i / W);
                    if (st == 0) c = new Color32(0, 0, 0, 255);
                    else if (st == 1) c = new Color32((byte)(c.r / 2), (byte)(c.g / 2), (byte)(c.b / 2), 255);
                }
                _px[i] = c;
            }
            foreach (var b in _w.Buildings)
            {
                if (b.Dead) continue;
                var v = _s.Views.Get(b.Id);
                bool vis = v != null ? v.Visible : _s.Views.IsVisible(b);
                if (!vis) continue;
                Color32 c = Palette.ForPlayer(_w.GetPlayer(b.Owner));
                for (int y = 0; y < b.H; y++) for (int x = 0; x < b.W; x++) Set(b.Origin.X + x, b.Origin.Y + y, c);
            }
            foreach (var u in _w.Units)
            {
                if (u.Dead || u.IsInside || !_s.Views.IsVisible(u)) continue;
                Color32 c = Palette.ForPlayer(_w.GetPlayer(u.Owner));
                if (u.Owner == _s.LocalPlayer) c = Color.Lerp(c, Color.white, 0.25f);
                int x = (int)u.Pos.X, y = (int)u.Pos.Y;
                Set(x, y, c);
                if (u.Def.Category != UnitCategory.Infantry) { Set(x + 1, y, c); Set(x, y + 1, c); Set(x + 1, y + 1, c); }
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < _w.Map.Width && y < _w.Map.Height) _px[y * _w.Map.Width + x] = c; }

        /// <summary>Draws the minimap into rect r (GUI space, possibly scaled) and handles mouse interaction.</summary>
        public void Draw(Rect r, float uiScale)
        {
            int W = _w.Map.Width, H = _w.Map.Height;
            float aspect = W / (float)H;
            var inner = r;
            if (aspect > r.width / r.height) { float h = r.width / aspect; inner = new Rect(r.x, r.y + (r.height - h) * 0.5f, r.width, h); }
            else { float w = r.height * aspect; inner = new Rect(r.x + (r.width - w) * 0.5f, r.y, w, r.height); }
            GUI.color = Color.black; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
            if (!RadarOnline)
            {
                GUI.color = new Color(0.1f, 0.12f, 0.1f); GUI.DrawTexture(inner, Texture2D.whiteTexture); GUI.color = Color.white;
                // static noise stripes
                for (int i = 0; i < 18; i++)
                {
                    float y = inner.y + ((Time.unscaledTime * 40f + i * 23f) % inner.height);
                    GUI.color = new Color(0.3f, 0.4f, 0.3f, 0.25f); GUI.DrawTexture(new Rect(inner.x, y, inner.width, 1), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                GUI.Label(inner, "RADAR OFFLINE\n<size=11>Build a radar and keep it powered</size>", UIKit.CenterLabel);
                return;
            }
            // flip vertically: texture row 0 = map y 0 should appear at the top of the minimap
            GUI.DrawTextureWithTexCoords(inner, _tex, new Rect(0, 1, 1, -1));

            Vector2 ToMini(Vec2 p) => new Vector2(inner.x + p.X / W * inner.width, inner.y + p.Y / H * inner.height);
            // camera box
            var corners = _s.Cam.ViewCorners();
            for (int i = 0; i < 4; i++)
            {
                var a = ToMini(new Vec2(corners[i].x, corners[i].z)); var b = ToMini(new Vec2(corners[(i + 1) % 4].x, corners[(i + 1) % 4].z));
                UIKit.Line(a, b, new Color(1, 1, 1, 0.85f), 1.2f, inner);
            }
            foreach (var p in _pings)
            {
                var c = ToMini(p.p); float s = 6 + (3f - p.t) * 8f % 12f;
                GUI.color = p.c.WithA(Mathf.Clamp01(p.t)); UIKit.Box(new Rect(c.x - s, c.y - s, s * 2, s * 2), 1.5f); GUI.color = Color.white;
            }

            // interaction (rect is in scaled GUI space, mouse in raw pixels)
            var mouse = InputState.MouseGui / uiScale;
            if (inner.Contains(mouse) || _dragging)
            {
                var mp = new Vec2(Mathf.Clamp01((mouse.x - inner.x) / inner.width) * W, Mathf.Clamp01((mouse.y - inner.y) / inner.height) * H);
                if (InputState.MouseDown(0) && inner.Contains(mouse)) { _dragging = true; InputState.EatMouse(0); }
                if (_dragging && InputState.MouseHeld(0)) _s.Cam.LookAt(mp);
                if (!InputState.MouseHeld(0)) _dragging = false;
                if (InputState.MouseDown(1) && inner.Contains(mouse)) { _s.Controller.OrderToMinimap(mp, InputState.Ctrl); InputState.EatMouse(1); }
            }
        }

        public void Destroy() => Object.Destroy(_tex);
    }
}
