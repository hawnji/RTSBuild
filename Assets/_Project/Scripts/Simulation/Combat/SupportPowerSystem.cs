using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    [Serializable]
    public class PendingStrike
    {
        public SupportPowerKind Kind;
        public Vec2 Target;
        public float TimeLeft;
        public int Owner;
        public int ShellsLeft;
        public float ShellTimer;
        public float Reduction;
    }

    [Serializable]
    public class RevealZone
    {
        public int Team;
        public Vec2 Pos;
        public float Radius;
        public float TimeLeft;
    }

    /// <summary>Superweapons and support powers: charging, readiness, warnings, countermeasures, and effects.</summary>
    public class SupportPowerSystem
    {
        readonly World _w;
        public readonly List<PendingStrike> Pending = new List<PendingStrike>();
        readonly List<Unit> _units = new List<Unit>();
        readonly List<Building> _blds = new List<Building>();
        readonly HashSet<int> _announced = new HashSet<int>();

        public SupportPowerSystem(World w) { _w = w; }

        public static SupportPowerDef Def(SupportPowerKind k) => Catalog.Powers[k];

        public void OnBuildingAdded(Building b)
        {
            if (!b.Def.Power_Kind.HasValue || b.BuildUp < 1f) return;
            var p = _w.GetPlayer(b.Owner);
            if (p == null) return;
            int k = (int)b.Def.Power_Kind.Value;
            p.PowerOwned[k] = true;
            if (Def(b.Def.Power_Kind.Value).IsSuperweapon && !_announced.Contains(b.Id))
            {
                _announced.Add(b.Id);
                _w.Emit(new SimEvent { Type = SimEventType.SuperweaponDetected, Player = -1, OtherId = b.Owner, EntityId = b.Id, Pos = b.Center, Text = b.Def.Power_Kind.Value.ToString() });
            }
        }

        public void RefreshOwnership(int player)
        {
            var p = _w.GetPlayer(player);
            if (p == null) return;
            var owned = new bool[p.PowerOwned.Length];
            foreach (var b in _w.BuildingsOf(player))
                if (b.Def.Power_Kind.HasValue && b.BuildUp >= 1f) owned[(int)b.Def.Power_Kind.Value] = true;
            for (int i = 0; i < owned.Length; i++)
            {
                if (!owned[i] && p.PowerOwned[i]) { p.PowerCharge[i] = 0; p.PowerReady[i] = false; }
                p.PowerOwned[i] = owned[i];
            }
        }

        public float ChargeFraction(Player p, SupportPowerKind k) => p.PowerOwned[(int)k] ? SMath.Clamp01(p.PowerCharge[(int)k] / Def(k).ChargeTime) : 0;

        public void Update()
        {
            float dt = World.Dt;
            foreach (var p in _w.Players)
            {
                if (p.Defeated || p.IsNeutral) continue;
                for (int i = 0; i < p.PowerOwned.Length; i++)
                {
                    if (!p.PowerOwned[i] || p.PowerReady[i]) continue;
                    var kind = (SupportPowerKind)i;
                    if (!_w.Settings.Superweapons && Def(kind).IsSuperweapon) continue;
                    if (p.LowPower) continue; // charging pauses on low power
                    if (!HasOperational(p.Id, kind)) continue;
                    p.PowerCharge[i] += dt;
                    if (p.PowerCharge[i] >= Def(kind).ChargeTime)
                    {
                        p.PowerReady[i] = true;
                        _w.Emit(new SimEvent { Type = SimEventType.SupportPowerReady, Player = p.Id, Text = kind.ToString() });
                    }
                }
            }
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                var s = Pending[i];
                s.TimeLeft -= dt;
                if (s.Kind == SupportPowerKind.Bombardment && s.TimeLeft <= 0)
                {
                    s.ShellTimer -= dt;
                    if (s.ShellTimer <= 0 && s.ShellsLeft > 0)
                    {
                        s.ShellTimer = 0.45f;
                        s.ShellsLeft--;
                        var pt = s.Target + _w.Rng.InsideCircle(Def(s.Kind).Radius);
                        AreaDamage(pt, 1.6f, Def(s.Kind).Damage * (1 - s.Reduction), "HE", s.Owner, 0, false);
                        _w.Emit(new SimEvent { Type = SimEventType.Explosion, Player = s.Owner, Pos = pt, Value = 1.4f, Text = "bombard" });
                    }
                    if (s.ShellsLeft <= 0) Pending.RemoveAt(i);
                    continue;
                }
                if (s.TimeLeft <= 0) { Resolve(s); Pending.RemoveAt(i); }
            }
            var zones = _w.Fog.RevealZones;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                zones[i].TimeLeft -= dt;
                if (zones[i].TimeLeft <= 0) zones.RemoveAt(i);
            }
        }

        bool HasOperational(int player, SupportPowerKind k)
        {
            foreach (var b in _w.BuildingsOf(player))
                if (b.Def.Power_Kind == k && b.Operational) return true;
            return false;
        }

        public bool Launch(int player, SupportPowerKind kind, Vec2 target)
        {
            var p = _w.GetPlayer(player);
            if (p == null || !p.PowerReady[(int)kind]) return false;
            var def = Def(kind);
            if (kind == SupportPowerKind.Paradrop && _w.Fog.Enabled && !_w.Fog.IsExplored(p.Team, Int2.Floor(target))) return false;
            p.PowerReady[(int)kind] = false;
            p.PowerCharge[(int)kind] = 0;
            var s = new PendingStrike { Kind = kind, Target = target, TimeLeft = def.WarningTime, Owner = player };
            s.Reduction = CountermeasureReduction(kind, target, player);
            if (kind == SupportPowerKind.Bombardment) { s.ShellsLeft = (int)(16 * (1 - s.Reduction * 0.5f)); s.ShellTimer = 0; }
            Pending.Add(s);
            _w.Emit(new SimEvent { Type = SimEventType.SupportPowerLaunched, Player = def.IsSuperweapon ? -1 : player, OtherId = player, Pos = target, Text = kind.ToString(), Value = def.WarningTime });
            if (kind == SupportPowerKind.SkyeyeReveal) { Resolve(s); Pending.Remove(s); }
            return true;
        }

        float CountermeasureReduction(SupportPowerKind kind, Vec2 target, int attacker)
        {
            float best = 0;
            foreach (var b in _w.Buildings)
            {
                if (b.Dead || b.Def.Role != BuildingRole.Countermeasure || !b.Operational || !_w.IsEnemy(b.Owner, attacker)) continue;
                var op = _w.GetPlayer(b.Owner);
                if (op != null && op.LowPower) continue;
                bool applies = b.Def.Faction == FactionId.Alliance
                    ? (kind == SupportPowerKind.HammerfallMissile || kind == SupportPowerKind.Bombardment)
                    : (kind == SupportPowerKind.OrbitalLance || kind == SupportPowerKind.EmpPulse);
                if (!applies) continue;
                if (b.DistanceTo(target) <= b.Def.CountermeasureRadius) best = Math.Max(best, b.Def.AntiSuperweapon);
            }
            return best;
        }

        void Resolve(PendingStrike s)
        {
            var def = Def(s.Kind);
            int team = _w.TeamOf(s.Owner);
            if (s.Reduction > 0)
                _w.Emit(new SimEvent { Type = SimEventType.Intercepted, Player = -1, Pos = s.Target, Text = s.Kind.ToString(), Value = s.Reduction });
            switch (s.Kind)
            {
                case SupportPowerKind.OrbitalLance:
                case SupportPowerKind.HammerfallMissile:
                    AreaDamage(s.Target, def.Radius, def.Damage * (1 - s.Reduction), "Super", s.Owner, 0, true);
                    if (s.Kind == SupportPowerKind.HammerfallMissile)
                        _w.Fires.Add(new FireZone { Pos = s.Target, Radius = def.Radius * 0.7f, TimeLeft = 18f, Dps = 12f * (1 - s.Reduction), Owner = s.Owner });
                    _w.Emit(new SimEvent { Type = SimEventType.SupportPowerImpact, Player = -1, Pos = s.Target, Text = s.Kind.ToString(), Value = def.Radius });
                    break;
                case SupportPowerKind.EmpPulse:
                    {
                        float secs = 20f * (1 - s.Reduction);
                        _w.Spatial.UnitsNear(s.Target, def.Radius, _units);
                        foreach (var u in _units)
                            if (u.Def.Category == UnitCategory.Vehicle || u.Def.Category == UnitCategory.Naval) u.EmpTimer = Math.Max(u.EmpTimer, secs);
                        _w.Spatial.BuildingsNear(s.Target, def.Radius, _blds);
                        foreach (var b in _blds) { b.EmpTimer = Math.Max(b.EmpTimer, secs); }
                        foreach (var p in _w.Players) _w.Economy.RecalculatePlayer(p.Id);
                        _w.Emit(new SimEvent { Type = SimEventType.SupportPowerImpact, Player = -1, Pos = s.Target, Text = s.Kind.ToString(), Value = def.Radius });
                    }
                    break;
                case SupportPowerKind.SkyeyeReveal:
                    _w.Fog.RevealZones.Add(new RevealZone { Team = team, Pos = s.Target, Radius = def.Radius, TimeLeft = 25f });
                    _w.Fog.Update();
                    break;
                case SupportPowerKind.Paradrop:
                    {
                        var p = _w.GetPlayer(s.Owner);
                        string a = "id_levy", b = "id_breacher";
                        if (p != null && p.Faction == FactionId.Alliance) { a = "ca_rifleman"; b = "ca_lancer"; }
                        for (int i = 0; i < 8; i++)
                        {
                            var c = _w.FindFreeCellNear(s.Target + _w.Rng.InsideCircle(1.5f), MoveClass.Foot, 4);
                            if (!c.HasValue) continue;
                            var u = _w.SpawnUnit(i < 6 ? a : b, s.Owner, c.Value.Center);
                            u.Paradropped = true;
                            u.GuardPos = u.Pos; u.HasGuardPos = true;
                        }
                        _w.Emit(new SimEvent { Type = SimEventType.Paradrop, Player = s.Owner, Pos = s.Target });
                    }
                    break;
            }
        }

        public void AreaDamage(Vec2 pos, float radius, float damage, string warhead, int owner, int sourceId, bool friendly)
        {
            var wd = new WeaponDef { Id = "area_" + warhead, Warhead = warhead, Damage = damage, Splash = radius, FriendlyFire = friendly, Targets = TargetLayer.All };
            _w.Spatial.UnitsNear(pos, radius, _units);
            foreach (var u in _units)
            {
                if (u.IsInside) continue;
                if (u.IsAir && u.AState == AirState.Flying && warhead != "Super") continue;
                if (!friendly && !_w.IsEnemy(owner, u.Owner)) continue;
                float d = Vec2.Distance(u.Pos, pos);
                float f = SMath.Clamp01(1f - d / radius) * 0.7f + 0.3f;
                _w.Combat.Damage(u, damage * f, wd, sourceId, owner, 1f);
            }
            _w.Spatial.BuildingsNear(pos, radius, _blds);
            foreach (var b in _blds)
            {
                if (!friendly && !_w.IsEnemy(owner, b.Owner)) continue;
                float d = b.DistanceTo(pos);
                float f = SMath.Clamp01(1f - d / radius) * 0.7f + 0.3f;
                _w.Combat.Damage(b, damage * f, wd, sourceId, owner, 1f);
            }
            int ir = (int)Math.Ceiling(radius);
            for (int dy = -ir; dy <= ir; dy++)
                for (int dx = -ir; dx <= ir; dx++)
                {
                    int x = (int)pos.X + dx, y = (int)pos.Y + dy;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    if (_w.Map.Get(x, y) == Terrain.Forest && _w.Rng.Chance(0.5f))
                    {
                        _w.Map.Set(x, y, Terrain.Dirt);
                        _w.Emit(new SimEvent { Type = SimEventType.TreeDestroyed, Player = -1, Pos = new Vec2(x + 0.5f, y + 0.5f) });
                    }
                }
            _w.Pathfinder.Version++;
        }
    }
}
