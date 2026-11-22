using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// Per-team visibility: unexplored / explored / currently visible cells, stealth detection, temporary reveal zones.
    /// The AI queries the same data, so computer players obey the same fog-of-war rules as the human.
    /// </summary>
    public class FogOfWar
    {
        public class TeamFog
        {
            public bool[] Explored;
            public int[] VisibleStamp;
            public int[] DetectStamp;
            public bool RevealAll;
        }

        readonly World _w;
        readonly Dictionary<int, TeamFog> _teams = new Dictionary<int, TeamFog>();
        public readonly List<RevealZone> RevealZones = new List<RevealZone>();
        int _stamp = 1;
        static readonly Dictionary<int, List<Int2>> _circles = new Dictionary<int, List<Int2>>();
        public int Version;     // increments every update (client uses this to refresh textures)

        public FogOfWar(World w) { _w = w; }

        public bool Enabled => _w.Settings.Fog != FogMode.Off;

        public TeamFog EnsureTeam(int team)
        {
            if (!_teams.TryGetValue(team, out var f))
            {
                int n = _w.Map.Width * _w.Map.Height;
                f = new TeamFog { Explored = new bool[n], VisibleStamp = new int[n], DetectStamp = new int[n] };
                if (_w.Settings.Fog == FogMode.Explored) for (int i = 0; i < n; i++) f.Explored[i] = true;
                _teams[team] = f;
            }
            return f;
        }

        public TeamFog GetTeam(int team) => EnsureTeam(team);

        public void RevealMap(int team)
        {
            var f = EnsureTeam(team);
            for (int i = 0; i < f.Explored.Length; i++) f.Explored[i] = true;
        }

        public void SetRevealAll(int team, bool on) => EnsureTeam(team).RevealAll = on;

        static List<Int2> Circle(int r)
        {
            if (_circles.TryGetValue(r, out var l)) return l;
            l = new List<Int2>();
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r + r) l.Add(new Int2(x, y));
            _circles[r] = l;
            return l;
        }

        void Stamp(TeamFog f, Vec2 p, float radius, bool detect)
        {
            var m = _w.Map;
            int cx = (int)p.X, cy = (int)p.Y;
            int r = Math.Max(1, (int)Math.Round(radius));
            var circ = Circle(r);
            for (int i = 0; i < circ.Count; i++)
            {
                int x = cx + circ[i].X, y = cy + circ[i].Y;
                if (x < 0 || y < 0 || x >= m.Width || y >= m.Height) continue;
                int idx = y * m.Width + x;
                if (detect) f.DetectStamp[idx] = _stamp;
                else { f.VisibleStamp[idx] = _stamp; f.Explored[idx] = true; }
            }
        }

        public void Update()
        {
            _stamp++;
            Version++;
            foreach (var p in _w.Players) EnsureTeam(p.Team);
            foreach (var u in _w.Units)
            {
                if (u.Dead || u.IsInside) continue;
                var p = _w.GetPlayer(u.Owner);
                if (p == null || p.IsNeutral) continue;
                var f = _teams[p.Team];
                float sight = u.Def.Sight * p.Mods.UnitMult(TechEffectKind.SightMult, u.Def);
                if (u.IsAir && u.AState == AirState.Flying) sight += 1;
                Stamp(f, u.Pos, sight, false);
                if (u.Def.Has(UnitAbility.Detector)) Stamp(f, u.Pos, sight * 0.8f, true);
            }
            foreach (var b in _w.Buildings)
            {
                if (b.Dead) continue;
                var p = _w.GetPlayer(b.Owner);
                if (p == null || p.IsNeutral) continue;
                var f = _teams[p.Team];
                Stamp(f, b.Center, b.Def.Sight + Math.Max(b.W, b.H) * 0.5f, false);
                if (b.Def.DetectRadius > 0 && b.Operational) Stamp(f, b.Center, b.Def.DetectRadius, true);
                if (b.Def.RevealRadius > 0 && b.Operational) Stamp(f, b.Center, b.Def.RevealRadius, false);
            }
            foreach (var z in RevealZones)
            {
                var f = EnsureTeam(z.Team);
                Stamp(f, z.Pos, z.Radius, false);
                Stamp(f, z.Pos, z.Radius, true);
            }
        }

        public bool IsVisible(int team, Vec2 p)
        {
            if (!Enabled) return true;
            if (!_teams.TryGetValue(team, out var f)) return false;
            if (f.RevealAll) return true;
            int x = (int)p.X, y = (int)p.Y;
            if (!_w.Map.InBounds(x, y)) return false;
            return f.VisibleStamp[y * _w.Map.Width + x] == _stamp;
        }

        public bool IsVisibleCell(int team, int x, int y)
        {
            if (!Enabled) return true;
            if (!_teams.TryGetValue(team, out var f)) return false;
            if (f.RevealAll) return true;
            if (!_w.Map.InBounds(x, y)) return false;
            return f.VisibleStamp[y * _w.Map.Width + x] == _stamp;
        }

        public bool IsExplored(int team, Int2 c)
        {
            if (!Enabled) return true;
            if (!_teams.TryGetValue(team, out var f)) return false;
            if (f.RevealAll) return true;
            if (!_w.Map.InBounds(c.X, c.Y)) return false;
            return f.Explored[c.Y * _w.Map.Width + c.X];
        }

        public bool Detected(int team, Vec2 p)
        {
            if (!_teams.TryGetValue(team, out var f)) return false;
            int x = (int)p.X, y = (int)p.Y;
            if (!_w.Map.InBounds(x, y)) return false;
            return f.DetectStamp[y * _w.Map.Width + x] == _stamp;
        }

        public bool IsBuildingVisible(int team, Building b)
        {
            if (!Enabled) return true;
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                    if (IsVisibleCell(team, b.Origin.X + x, b.Origin.Y + y)) return true;
            return false;
        }

        /// <summary>0 = unexplored, 1 = explored (fogged), 2 = visible. Used for minimap and fog textures.</summary>
        public byte CellState(int team, int x, int y)
        {
            if (!Enabled) return 2;
            if (!_teams.TryGetValue(team, out var f)) return 0;
            if (f.RevealAll) return 2;
            int i = y * _w.Map.Width + x;
            if (f.VisibleStamp[i] == _stamp) return 2;
            return f.Explored[i] ? (byte)1 : (byte)0;
        }

        public bool[] ExploredArray(int team) => EnsureTeam(team).Explored;
    }
}
