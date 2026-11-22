using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// Grid A* with octile heuristic, corner-cutting prevention, per-tick search budget, partial paths toward
    /// unreachable goals, and line-of-sight path smoothing. Reuses its buffers to avoid per-search allocations.
    /// </summary>
    public class Pathfinder
    {
        readonly World _w;
        float[] _g;
        int[] _parent;
        int[] _stamp;
        int[] _closedStamp;
        int _cur;
        readonly SimplePriorityQueue _open = new SimplePriorityQueue();
        public int SearchesThisTick;
        int _lastTick = -1;
        public int Version;              // increments whenever structures change
        public const int SearchesPerTick = 24;

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly float[] COST = { 1, 1, 1, 1, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

        public Pathfinder(World w)
        {
            _w = w;
            int n = w.Map.Width * w.Map.Height;
            _g = new float[n]; _parent = new int[n]; _stamp = new int[n]; _closedStamp = new int[n];
        }

        public void InvalidateArea(Int2 origin, int w, int h) => Version++;

        public bool HasBudget
        {
            get
            {
                if (_lastTick != _w.Tick) { _lastTick = _w.Tick; SearchesThisTick = 0; }
                return SearchesThisTick < SearchesPerTick;
            }
        }

        /// <summary>Nearest cell to target that the movement class can stand on.</summary>
        public Int2 NearestPassable(Int2 c, MoveClass mc, int team, int maxR = 12)
        {
            if (_w.Passable(c.X, c.Y, mc, team)) return c;
            for (int r = 1; r <= maxR; r++)
            {
                Int2 best = c; float bd = float.MaxValue; bool found = false;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = c.X + dx, y = c.Y + dy;
                        if (!_w.Passable(x, y, mc, team)) continue;
                        float d = dx * dx + dy * dy;
                        if (d < bd) { bd = d; best = new Int2(x, y); found = true; }
                    }
                if (found) return best;
            }
            return c;
        }

        /// <summary>Returns smoothed waypoints (cell coordinates) or null if the budget is exhausted this tick.</summary>
        public List<Int2> FindPath(Vec2 fromPos, Vec2 toPos, MoveClass mc, int team, int maxNodes = 9000)
        {
            if (!HasBudget) return null;
            SearchesThisTick++;
            var map = _w.Map;
            var start = Int2.Floor(fromPos);
            var goal = Int2.Floor(toPos);
            start = new Int2(SMath.Clamp(start.X, 0, map.Width - 1), SMath.Clamp(start.Y, 0, map.Height - 1));
            goal = new Int2(SMath.Clamp(goal.X, 0, map.Width - 1), SMath.Clamp(goal.Y, 0, map.Height - 1));
            var result = new List<Int2>();
            if (mc == MoveClass.Air) { result.Add(goal); return result; }
            goal = NearestPassable(goal, mc, team);
            if (start == goal) { result.Add(goal); return result; }
            if (!_w.Passable(start.X, start.Y, mc, team))
            {
                // Unit is standing somewhere it should not (e.g. a building was placed on it): step out first.
                start = NearestPassable(start, mc, team, 4);
            }

            _cur++;
            if (_cur == int.MaxValue) { Array.Clear(_stamp, 0, _stamp.Length); Array.Clear(_closedStamp, 0, _closedStamp.Length); _cur = 1; }
            _open.Clear();
            int W = map.Width;
            int s = start.Y * W + start.X, g = goal.Y * W + goal.X;
            _g[s] = 0; _parent[s] = -1; _stamp[s] = _cur;
            _open.Push(s, Heuristic(start.X, start.Y, goal.X, goal.Y));
            int best = s; float bestH = float.MaxValue;
            int expanded = 0;
            bool found = false;
            while (_open.Count > 0)
            {
                int c = _open.Pop();
                if (_closedStamp[c] == _cur) continue;
                _closedStamp[c] = _cur;
                if (c == g) { found = true; break; }
                int cx = c % W, cy = c / W;
                float h = Heuristic(cx, cy, goal.X, goal.Y);
                if (h < bestH) { bestH = h; best = c; }
                if (++expanded > maxNodes) break;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX[d], ny = cy + DY[d];
                    if (!map.InBounds(nx, ny)) continue;
                    if (!_w.Passable(nx, ny, mc, team)) continue;
                    if (d >= 4 && (!_w.Passable(cx + DX[d], cy, mc, team) || !_w.Passable(cx, cy + DY[d], mc, team))) continue;
                    int ni = ny * W + nx;
                    if (_closedStamp[ni] == _cur) continue;
                    float speed = map.SpeedMultiplier(nx, ny, mc);
                    float ng = _g[c] + COST[d] / speed;
                    if (_stamp[ni] != _cur || ng < _g[ni])
                    {
                        _stamp[ni] = _cur; _g[ni] = ng; _parent[ni] = c;
                        _open.Push(ni, ng + Heuristic(nx, ny, goal.X, goal.Y) * 0.95f);
                    }
                }
            }
            int end = found ? g : best;
            var raw = new List<Int2>();
            int cur = end;
            int guard = 0;
            while (cur != -1 && guard++ < 100000)
            {
                raw.Add(new Int2(cur % W, cur / W));
                if (cur == s) break;
                cur = _parent[cur];
            }
            raw.Reverse();
            return Smooth(raw, mc, team);
        }

        static float Heuristic(int x, int y, int gx, int gy)
        {
            int dx = Math.Abs(x - gx), dy = Math.Abs(y - gy);
            return (dx + dy) + (1.4142f - 2f) * Math.Min(dx, dy);
        }

        List<Int2> Smooth(List<Int2> raw, MoveClass mc, int team)
        {
            var outp = new List<Int2>();
            if (raw.Count == 0) return outp;
            int i = 0;
            while (i < raw.Count - 1)
            {
                int j = raw.Count - 1;
                // Look ahead up to 24 cells for the farthest directly reachable node.
                int limit = Math.Min(raw.Count - 1, i + 24);
                j = limit;
                while (j > i + 1 && !LineClear(raw[i], raw[j], mc, team)) j--;
                outp.Add(raw[j]);
                i = j;
            }
            if (outp.Count == 0) outp.Add(raw[raw.Count - 1]);
            return outp;
        }

        /// <summary>Supercover line test between two cell centers.</summary>
        public bool LineClear(Int2 a, Int2 b, MoveClass mc, int team)
        {
            float x0 = a.X + 0.5f, y0 = a.Y + 0.5f, x1 = b.X + 0.5f, y1 = b.Y + 0.5f;
            float dx = x1 - x0, dy = y1 - y0;
            float len = SMath.Sqrt(dx * dx + dy * dy);
            if (len < 0.01f) return true;
            int steps = (int)(len * 3) + 1;
            float r = 0.32f; // clearance
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                float px = x0 + dx * t, py = y0 + dy * t;
                if (!_w.Passable((int)(px - r), (int)(py - r), mc, team)) return false;
                if (!_w.Passable((int)(px + r), (int)(py - r), mc, team)) return false;
                if (!_w.Passable((int)(px - r), (int)(py + r), mc, team)) return false;
                if (!_w.Passable((int)(px + r), (int)(py + r), mc, team)) return false;
            }
            return true;
        }

        /// <summary>Flood-fill reachability check used by AI for expansion decisions.</summary>
        public bool Reachable(Int2 a, Int2 b, MoveClass mc, int team, int maxCells = 20000)
        {
            var path = FindPathUnbudgeted(a, b, mc, team, maxCells);
            if (path == null || path.Count == 0) return false;
            var last = path[path.Count - 1];
            return Math.Abs(last.X - b.X) <= 2 && Math.Abs(last.Y - b.Y) <= 2;
        }

        public List<Int2> FindPathUnbudgeted(Int2 a, Int2 b, MoveClass mc, int team, int maxNodes = 9000)
        {
            int saved = SearchesThisTick;
            SearchesThisTick = 0; _lastTick = _w.Tick;
            var r = FindPath(a.Center, b.Center, mc, team, maxNodes);
            SearchesThisTick = saved;
            return r;
        }
    }
}
