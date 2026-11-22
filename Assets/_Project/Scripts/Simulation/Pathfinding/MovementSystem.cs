using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// Physical movement: path following, turning, local separation/avoidance, terrain speed, stuck recovery,
    /// infantry crushing, aircraft flight models (fixed-wing and rotary) and naval movement.
    /// </summary>
    public class MovementSystem
    {
        readonly World _w;
        readonly List<Unit> _near = new List<Unit>();

        public MovementSystem(World w) { _w = w; }

        public void RequestMove(Unit u, Vec2 goal)
        {
            goal = new Vec2(SMath.Clamp(goal.X, 1, _w.Map.Width - 1.01f), SMath.Clamp(goal.Y, 1, _w.Map.Height - 1.01f));
            u.MoveGoal = goal;
            u.Moving = true;
            u.PathPending = !u.IsAir;
            u.Path.Clear();
            u.PathIndex = 0;
            u.StuckTimer = 0;
            u.Repaths = 0;
            u.LastProgressDist = float.MaxValue;
        }

        public void Stop(Unit u)
        {
            u.Moving = false;
            u.PathPending = false;
            u.Path.Clear();
        }

        public void UpdateAll()
        {
            var units = _w.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.IsInside) continue;
                u.PrevPos = u.Pos;
                if (u.IsAir) UpdateAir(u);
                else UpdateSurface(u);
                if (Vec2.SqrDistance(u.Pos, u.PrevPos) > 0.0001f) u.StillTime = 0; else u.StillTime += World.Dt;
            }
        }

        float SpeedOf(Unit u)
        {
            var p = _w.GetPlayer(u.Owner);
            float s = u.Def.Speed;
            if (p != null) s *= p.Mods.UnitMult(TechEffectKind.SpeedMult, u.Def);
            if (u.SuppressedTimer > 0) s *= 0.55f;
            if (u.Rank >= 2) s *= 1.1f;
            return s;
        }

        // ------------------------------------------------------------------ Ground & naval
        void UpdateSurface(Unit u)
        {
            if (u.Disabled && u.Def.Category != UnitCategory.Infantry) { u.Speed = 0; return; }
            var mc = u.MoveClass;
            int team = _w.TeamOf(u.Owner);
            if (!u.Moving) { Separate(u, mc, team, false); u.Speed = 0; return; }

            if (u.PathPending)
            {
                var path = _w.Pathfinder.FindPath(u.Pos, u.MoveGoal, mc, team);
                if (path == null) { Separate(u, mc, team, false); return; }
                u.Path = path; u.PathIndex = 0; u.PathPending = false;
            }
            if (u.PathIndex >= u.Path.Count) { Arrive(u); return; }

            bool last = u.PathIndex == u.Path.Count - 1;
            var wp = u.Path[u.PathIndex];
            Vec2 target = last && _w.Passable((int)u.MoveGoal.X, (int)u.MoveGoal.Y, mc, team) && Int2.Floor(u.MoveGoal) == wp ? u.MoveGoal : wp.Center;
            var to = target - u.Pos;
            float dist = to.Length;
            float arriveR = last ? 0.25f : 0.45f;
            if (dist < arriveR)
            {
                u.PathIndex++;
                if (u.PathIndex >= u.Path.Count) { Arrive(u); return; }
                return;
            }

            // Group arrival: stop near a crowded goal rather than jostling forever.
            float goalDist = Vec2.Distance(u.Pos, u.MoveGoal);
            if (goalDist < 2.2f && u.StuckTimer > 0.6f) { Arrive(u); return; }

            var dir = to / dist;
            float desiredAngle = dir.Angle;
            float turn = u.Def.TurnRate * World.Dt;
            u.Facing = SMath.RotateTowards(u.Facing, desiredAngle, turn);
            float angleDiff = SMath.Abs(SMath.WrapAngle(desiredAngle - u.Facing));
            float speedFactor = u.Def.Category == UnitCategory.Infantry ? 1f : SMath.Clamp01((float)Math.Cos(angleDiff) * 1.2f);
            if (angleDiff > 1.2f && u.Def.Category != UnitCategory.Infantry) speedFactor = 0f;
            var cell = Int2.Floor(u.Pos);
            float speed = SpeedOf(u) * _w.Map.SpeedMultiplier(cell.X, cell.Y, mc) * speedFactor;
            // Accelerate smoothly
            u.Speed = SMath.MoveTowards(u.Speed, speed, SpeedOf(u) * 3f * World.Dt);
            var heading = u.Def.Category == UnitCategory.Infantry ? dir : Vec2.FromAngle(u.Facing);
            var step = heading * Math.Min(u.Speed * World.Dt, dist);
            step += Separation(u, mc, team) * World.Dt;
            TryMove(u, step, mc, team);

            // Progress / stuck detection
            if (goalDist < u.LastProgressDist - 0.05f) { u.LastProgressDist = goalDist; u.StuckTimer = 0; }
            else u.StuckTimer += World.Dt;
            if (u.StuckTimer > 2.0f)
            {
                u.Repaths++;
                u.StuckTimer = 0;
                u.LastProgressDist = float.MaxValue;
                if (u.Repaths > 4 || goalDist < 1.5f) { Arrive(u); return; }
                // Small random nudge, then re-plan from the current cell.
                var nudge = _w.Rng.InsideCircle(0.5f);
                TryMove(u, nudge, mc, team);
                u.PathPending = true;
            }

            if (u.Def.CanCrush) Crush(u);
        }

        void Arrive(Unit u)
        {
            u.Moving = false;
            u.Path.Clear();
            u.PathPending = false;
            u.Speed = 0;
        }

        bool TryMove(Unit u, Vec2 step, MoveClass mc, int team)
        {
            var np = u.Pos + step;
            if (CanStand(np, mc, team)) { u.Pos = np; return true; }
            var npx = new Vec2(u.Pos.X + step.X, u.Pos.Y);
            if (Math.Abs(step.X) > 1e-4f && CanStand(npx, mc, team)) { u.Pos = npx; return true; }
            var npy = new Vec2(u.Pos.X, u.Pos.Y + step.Y);
            if (Math.Abs(step.Y) > 1e-4f && CanStand(npy, mc, team)) { u.Pos = npy; return true; }
            // If the unit is currently inside an obstacle (structure placed on top), let it walk out.
            if (!CanStand(u.Pos, mc, team)) { u.Pos = np; return true; }
            return false;
        }

        bool CanStand(Vec2 p, MoveClass mc, int team)
        {
            int x = (int)Math.Floor(p.X), y = (int)Math.Floor(p.Y);
            return _w.Passable(x, y, mc, team);
        }

        Vec2 Separation(Unit u, MoveClass mc, int team)
        {
            _w.Spatial.UnitsNear(u.Pos, u.Radius + 0.9f, _near);
            var push = Vec2.Zero;
            for (int i = 0; i < _near.Count; i++)
            {
                var o = _near[i];
                if (o == u || o.IsAir || o.MoveClass == MoveClass.Air) continue;
                if ((o.MoveClass == MoveClass.Naval) != (mc == MoveClass.Naval)) continue;
                var d = u.Pos - o.Pos;
                float dist = d.Length;
                float min = u.Radius + o.Radius + 0.05f;
                if (dist >= min) continue;
                if (dist < 0.001f) { d = Vec2.FromAngle((u.Id % 16) * 0.39f); dist = 0.001f; }
                float strength = (min - dist) / min;
                // Moving units push idle friendly units aside rather than stopping.
                if (!o.Moving && _w.IsAlly(o.Owner, u.Owner) && o.Stance != Stance.HoldPosition && u.Moving)
                {
                    var nudge = -d / dist * strength * 0.08f;
                    if (CanStand(o.Pos + nudge, o.MoveClass, team)) o.Pos += nudge;
                }
                push += d / dist * strength * 2.2f;
            }
            return push;
        }

        void Separate(Unit u, MoveClass mc, int team, bool moving)
        {
            var s = Separation(u, mc, team);
            if (s.SqrLength > 0.0001f) TryMove(u, s * World.Dt * 0.5f, mc, team);
        }

        void Crush(Unit u)
        {
            _w.Spatial.UnitsNear(u.Pos, 0.45f, _near);
            for (int i = 0; i < _near.Count; i++)
            {
                var o = _near[i];
                if (o.Def.Crushable && _w.IsEnemy(u.Owner, o.Owner) && !o.Dead && u.Speed > 0.3f)
                {
                    _w.Emit(new SimEvent { Type = SimEventType.Crushed, Player = o.Owner, EntityId = o.Id, Pos = o.Pos });
                    _w.Kill(o, u.Id);
                }
            }
        }

        // ------------------------------------------------------------------ Air
        void UpdateAir(Unit u)
        {
            float cruise = u.Def.CruiseHeight;
            bool heli = u.Def.Locomotor == Locomotor.Helicopter;
            if (u.AState == AirState.Docked)
            {
                u.Height = SMath.MoveTowards(u.Height, 0, 3f * World.Dt);
                u.Speed = 0;
                if (u.Moving) u.AState = AirState.TakingOff;
                return;
            }
            if (u.AState == AirState.TakingOff)
            {
                u.Height = SMath.MoveTowards(u.Height, cruise, 3f * World.Dt);
                if (u.Height >= cruise - 0.05f) u.AState = AirState.Flying;
                if (!heli) { u.Pos += Vec2.FromAngle(u.Facing) * SpeedOf(u) * 0.4f * World.Dt; ClampToMap(u); }
                return;
            }
            float speed = SpeedOf(u);
            if (u.AState == AirState.Landing)
            {
                var to = u.MoveGoal - u.Pos;
                float d = to.Length;
                float s = Math.Min(speed * (heli ? 1f : 0.6f), Math.Max(1f, d * 2f));
                if (d > 0.05f)
                {
                    u.Facing = SMath.RotateTowards(u.Facing, to.Angle, u.Def.TurnRate * 2f * World.Dt);
                    u.Pos += to / d * Math.Min(s * World.Dt, d);
                }
                if (d < 1.5f) u.Height = SMath.MoveTowards(u.Height, 0, 3f * World.Dt);
                if (d < 0.1f && u.Height <= 0.05f) { u.AState = AirState.Docked; u.Moving = false; }
                return;
            }
            u.Height = SMath.MoveTowards(u.Height, cruise, 2f * World.Dt);
            if (heli)
            {
                if (u.Moving)
                {
                    var to = u.MoveGoal - u.Pos;
                    float d = to.Length;
                    if (d < 0.2f) { u.Moving = false; u.Speed = 0; }
                    else
                    {
                        u.Facing = SMath.RotateTowards(u.Facing, to.Angle, u.Def.TurnRate * 1.5f * World.Dt);
                        u.Speed = SMath.MoveTowards(u.Speed, speed, speed * 2f * World.Dt);
                        u.Pos += to / d * Math.Min(u.Speed * World.Dt, d);
                    }
                }
                else u.Speed = SMath.MoveTowards(u.Speed, 0, speed * 2f * World.Dt);
                AirSeparation(u);
            }
            else
            {
                // Fixed wing: always flying forward; steer toward the goal or orbit around it.
                Vec2 goal = u.Moving ? u.MoveGoal : u.MoveGoal;
                var to = goal - u.Pos;
                float d = to.Length;
                float desired;
                if (u.Moving && d > 1.5f) desired = to.Angle;
                else
                {
                    if (u.Moving && d <= 1.5f) u.Moving = false;
                    // Orbit: aim tangentially at radius 3 around goal.
                    u.OrbitAngle += World.Dt * speed / 3.5f;
                    var orbitPt = goal + Vec2.FromAngle(u.OrbitAngle) * 3.5f;
                    desired = (orbitPt - u.Pos).Angle;
                }
                u.Facing = SMath.RotateTowards(u.Facing, desired, u.Def.TurnRate * World.Dt);
                u.Speed = speed;
                u.Pos += Vec2.FromAngle(u.Facing) * speed * World.Dt;
            }
            ClampToMap(u);
        }

        void AirSeparation(Unit u)
        {
            _w.Spatial.UnitsNear(u.Pos, 1.2f, _near);
            var push = Vec2.Zero;
            for (int i = 0; i < _near.Count; i++)
            {
                var o = _near[i];
                if (o == u || !o.IsAir || o.AState != AirState.Flying) continue;
                var d = u.Pos - o.Pos;
                float dist = d.Length;
                if (dist < 0.001f) d = Vec2.FromAngle(u.Id);
                if (dist < 1.1f) push += d.Normalized * (1.1f - dist);
            }
            u.Pos += push * World.Dt * 1.5f;
        }

        void ClampToMap(Unit u)
        {
            u.Pos = new Vec2(SMath.Clamp(u.Pos.X, 0.5f, _w.Map.Width - 0.5f), SMath.Clamp(u.Pos.Y, 0.5f, _w.Map.Height - 0.5f));
        }
    }
}
