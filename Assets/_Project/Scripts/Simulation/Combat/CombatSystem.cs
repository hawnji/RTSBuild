using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// Target acquisition, weapon aiming and firing, projectile simulation (hitscan, bullets, shells, ballistic arcs,
    /// homing missiles, torpedoes, bombs, flame, beams and rails), damage resolution with armor/warhead tables,
    /// splash and friendly fire, suppression, EMP, veterancy, burning ground and destructible trees.
    /// </summary>
    public class CombatSystem
    {
        readonly World _w;
        readonly List<Unit> _units = new List<Unit>();
        readonly List<Building> _blds = new List<Building>();
        int _nextProjectileId = 1;

        public CombatSystem(World w) { _w = w; }

        // ------------------------------------------------------------------ Modifiers
        public float RangeOf(Entity e, WeaponDef wd)
        {
            float r = wd.Range;
            if (e is Unit u)
            {
                var p = _w.GetPlayer(u.Owner);
                if (p != null) r *= p.Mods.UnitMult(TechEffectKind.RangeMult, u.Def);
                if (u.Rank >= 2) r *= 1.08f;
                // High ground bonus for artillery-type weapons.
                if (wd.Projectile == ProjectileKind.Ballistic && _w.TerrainHeight(u.Pos) >= 1) r *= 1.1f;
            }
            return r;
        }

        public float DamageMultOf(Entity e, WeaponDef wd)
        {
            float m = 1f;
            var p = _w.GetPlayer(e.Owner);
            if (e is Unit u)
            {
                if (p != null) m *= p.Mods.UnitMult(TechEffectKind.DamageMult, u.Def, wd.Warhead);
                if (u.Rank == 1) m *= 1.15f; else if (u.Rank >= 2) m *= 1.3f;
            }
            else if (p != null) m *= p.Mods.WarheadDamage(wd.Warhead);
            return m;
        }

        public float MaxRange(Entity e, TargetLayer layer)
        {
            float r = 0;
            foreach (var ws in e.Weapons)
            {
                var wd = ws.Def ?? (ws.Def = Catalog.Weapon(ws.WeaponId));
                if (wd != null && (wd.Targets & layer) != 0) r = Math.Max(r, RangeOf(e, wd));
            }
            return r;
        }

        public float MaxRangeAny(Entity e)
        {
            float r = 0;
            foreach (var ws in e.Weapons)
            {
                var wd = ws.Def ?? (ws.Def = Catalog.Weapon(ws.WeaponId));
                if (wd != null) r = Math.Max(r, RangeOf(e, wd));
            }
            return r;
        }

        public TargetLayer TargetMask(Entity e)
        {
            TargetLayer m = TargetLayer.None;
            foreach (var ws in e.Weapons)
            {
                var wd = ws.Def ?? (ws.Def = Catalog.Weapon(ws.WeaponId));
                if (wd != null) m |= wd.Targets;
            }
            return m;
        }

        public bool CanTarget(Entity shooter, Entity target)
        {
            if (target == null || target.Dead) return false;
            if (target is Unit tu && tu.IsInside) return false;
            var layer = target.Layer;
            var mask = TargetMask(shooter);
            if ((mask & layer) != 0) return true;
            // Weapons that hit ground units can hit docked aircraft and surfaced ships.
            return false;
        }

        public float Effectiveness(Entity shooter, Entity target)
        {
            float best = 0;
            foreach (var ws in shooter.Weapons)
            {
                var wd = ws.Def;
                if (wd == null || (wd.Targets & target.Layer) == 0) continue;
                float v = wd.Damage * wd.Burst * Catalog.Warhead(wd.Warhead).Get(target.Armor) / Math.Max(0.2f, wd.Cooldown);
                if (v > best) best = v;
            }
            return best;
        }

        // ------------------------------------------------------------------ Stealth
        public bool IsStealthed(Entity e)
        {
            if (!(e is Unit u)) return false;
            if (u.Def.Locomotor == Locomotor.Submarine) return u.Submerged;
            if (!u.Def.Has(UnitAbility.Stealth)) return false;
            if (u.Def.Model == "inf_sniper" || u.Def.Model == "inf_specialist")
                return u.StillTime > 2f && u.SinceFired > 3f;
            return u.SinceFired > 2f;
        }

        public bool VisibleTo(Entity e, int team)
        {
            if (e == null || e.Dead) return false;
            if (_w.TeamOf(e.Owner) == team) return true;
            if (!_w.Fog.Enabled) return !IsStealthed(e) || _w.Fog.Detected(team, e.Pos);
            if (e is Building b) return _w.Fog.IsBuildingVisible(team, b);
            if (!_w.Fog.IsVisible(team, e.Pos)) return false;
            if (IsStealthed(e) && !_w.Fog.Detected(team, e.Pos)) return false;
            return true;
        }

        // ------------------------------------------------------------------ Acquisition
        public Entity FindTarget(Entity shooter, Vec2 center, float radius, bool includeBuildings = true)
        {
            var mask = TargetMask(shooter);
            if (mask == TargetLayer.None) return null;
            int team = _w.TeamOf(shooter.Owner);
            Entity best = null; float bestScore = 0;
            _w.Spatial.UnitsNear(center, radius + 1f, _units);
            for (int i = 0; i < _units.Count; i++)
            {
                var t = _units[i];
                if (!_w.IsEnemy(shooter.Owner, t.Owner) || t.IsInside) continue;
                if ((mask & t.Layer) == 0) continue;
                if (!VisibleTo(t, team)) continue;
                float d = Vec2.Distance(shooter.Pos, t.Pos);
                float eff = Effectiveness(shooter, t);
                if (eff <= 0.01f) continue;
                float score = eff * t.Def.TargetPriority / (1f + d * 0.12f);
                if (t.IsArmed) score *= 1.6f;
                if (t.TargetId == shooter.Id) score *= 1.5f;
                if (t.Def.Has(UnitAbility.Harvester)) score *= 0.8f;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            if (includeBuildings && (mask & TargetLayer.Structure) != 0)
            {
                _w.Spatial.BuildingsNear(center, radius, _blds);
                for (int i = 0; i < _blds.Count; i++)
                {
                    var b = _blds[i];
                    if (!_w.IsEnemy(shooter.Owner, b.Owner)) continue;
                    if (!VisibleTo(b, team)) continue;
                    float d = b.DistanceTo(shooter.Pos);
                    float eff = Effectiveness(shooter, b);
                    if (eff <= 0.01f) continue;
                    // Structures are low priority for automatic targeting unless they shoot back.
                    float prio = b.Weapons.Count > 0 ? 1.4f : b.IsWallLike ? 0.05f : 0.25f;
                    float score = eff * prio / (1f + d * 0.12f);
                    if (score > bestScore) { bestScore = score; best = b; }
                }
            }
            return best;
        }

        public float DistanceTo(Entity a, Entity b)
        {
            if (b is Building bb) return bb.DistanceTo(a.Pos);
            if (a is Building ab) return ab.DistanceTo(b.Pos);
            return Math.Max(0, Vec2.Distance(a.Pos, b.Pos) - b.Radius * 0.5f);
        }

        /// <summary>Returns true if at least one weapon able to hit the target is within range.</summary>
        public bool InRange(Entity shooter, Entity target)
        {
            float d = DistanceTo(shooter, target);
            foreach (var ws in shooter.Weapons)
            {
                var wd = ws.Def;
                if (wd == null || (wd.Targets & target.Layer) == 0) continue;
                if (d <= RangeOf(shooter, wd) && d >= wd.MinRange) return true;
            }
            return false;
        }

        public bool TooClose(Entity shooter, Entity target)
        {
            float d = DistanceTo(shooter, target);
            bool anyOk = false, anyTooClose = false;
            foreach (var ws in shooter.Weapons)
            {
                var wd = ws.Def;
                if (wd == null || (wd.Targets & target.Layer) == 0) continue;
                if (d < wd.MinRange) anyTooClose = true; else anyOk = true;
            }
            return anyTooClose && !anyOk;
        }

        // ------------------------------------------------------------------ Weapons
        public void TickWeapons(Entity e, float dt)
        {
            float reloadMult = 1f;
            if (e is Unit u)
            {
                if (u.SuppressedTimer > 0) reloadMult = 0.6f;
                if (u.IsAir) { var p = _w.GetPlayer(u.Owner); if (p != null) reloadMult /= p.Mods.UnitMult(TechEffectKind.ReloadMult, u.Def); }
            }
            foreach (var ws in e.Weapons)
            {
                if (ws.Def == null) ws.Def = Catalog.Weapon(ws.WeaponId);
                if (ws.Cooldown > 0) ws.Cooldown -= dt * reloadMult;
                if (ws.Def != null && ws.Def.Clip > 0 && ws.ClipLeft <= 0)
                {
                    ws.Reload += dt * reloadMult;
                    if (ws.Reload >= ws.Def.ReloadTime) { ws.ClipLeft = ws.Def.Clip; ws.Reload = 0; }
                }
            }
            e.SinceFired += dt;
        }

        /// <summary>Aims and fires any ready weapon at the target. Returns true if a weapon is in range.</summary>
        public bool Engage(Entity shooter, Entity target, bool allowChassisTurn = true)
        {
            if (shooter.Disabled) return false;
            bool inRange = false;
            float d = DistanceTo(shooter, target);
            var aimDir = target.Pos - shooter.Pos;
            float desired = aimDir.Angle;
            var u = shooter as Unit;
            bool turreted = IsTurreted(shooter);
            if (turreted) shooter.TurretFacing = SMath.RotateTowards(shooter.TurretFacing, desired, 4.5f * World.Dt);
            else if (u != null && allowChassisTurn && !(u.Def.Locomotor == Locomotor.Air))
            {
                u.Facing = SMath.RotateTowards(u.Facing, desired, u.Def.TurnRate * World.Dt);
                u.TurretFacing = u.Facing;
            }
            float facing = turreted ? shooter.TurretFacing : shooter.Facing;
            float aimErr = SMath.Abs(SMath.WrapAngle(desired - facing));
            float tolerance = u != null && u.Def.Locomotor == Locomotor.Air ? 0.6f : 0.25f;
            if (u != null && u.Def.Category == UnitCategory.Infantry) tolerance = 0.5f;

            foreach (var ws in shooter.Weapons)
            {
                var wd = ws.Def;
                if (wd == null || (wd.Targets & target.Layer) == 0) continue;
                float range = RangeOf(shooter, wd);
                // Bombs: drop when nearly overhead
                if (d > range || d < wd.MinRange) continue;
                inRange = true;
                if (ws.Cooldown > 0) continue;
                if (wd.Clip > 0 && ws.ClipLeft <= 0) continue;
                if (aimErr > tolerance && wd.Projectile != ProjectileKind.Bomb) continue;
                if (u != null && u.Def.Ammo > 0 && wd.AmmoCost > 0 && u.Ammo < wd.AmmoCost) continue;
                if (shooter is Building b && wd.RequiresPower)
                {
                    var p = _w.GetPlayer(b.Owner);
                    if (p != null && p.PowerRatio < 0.5f) continue;   // offline
                }
                Fire(shooter, ws, target, target.Pos);
            }
            return inRange;
        }

        public bool EngageGround(Entity shooter, Vec2 pos)
        {
            bool inRange = false;
            float d = Vec2.Distance(shooter.Pos, pos);
            float desired = (pos - shooter.Pos).Angle;
            bool turreted = IsTurreted(shooter);
            if (turreted) shooter.TurretFacing = SMath.RotateTowards(shooter.TurretFacing, desired, 4.5f * World.Dt);
            else if (shooter is Unit u) { u.Facing = SMath.RotateTowards(u.Facing, desired, u.Def.TurnRate * World.Dt); u.TurretFacing = u.Facing; }
            float facing = turreted ? shooter.TurretFacing : shooter.Facing;
            foreach (var ws in shooter.Weapons)
            {
                var wd = ws.Def;
                if (wd == null || (wd.Targets & (TargetLayer.Ground | TargetLayer.Structure)) == 0) continue;
                if (d > RangeOf(shooter, wd) || d < wd.MinRange) continue;
                inRange = true;
                if (ws.Cooldown > 0 || (wd.Clip > 0 && ws.ClipLeft <= 0)) continue;
                if (SMath.Abs(SMath.WrapAngle(desired - facing)) > 0.3f) continue;
                Fire(shooter, ws, null, pos);
            }
            return inRange;
        }

        bool IsTurreted(Entity e)
        {
            if (e is Building) return true;
            var u = (Unit)e;
            if (u.Def.Category == UnitCategory.Infantry) return false;
            if (u.IsAir) return u.Def.Locomotor == Locomotor.Helicopter && u.Def.Model == "adv_heli";
            string m = u.Def.Model;
            return m != "td" && m != "siege" && m != "sub";
        }

        void Fire(Entity shooter, WeaponState ws, Entity target, Vec2 aimPos)
        {
            var wd = ws.Def;
            ws.Cooldown = wd.Cooldown;
            if (wd.Clip > 0) ws.ClipLeft--;
            shooter.SinceFired = 0;
            var su = shooter as Unit;
            if (su != null)
            {
                if (su.Def.Ammo > 0 && wd.AmmoCost > 0) su.Ammo -= wd.AmmoCost;
                if (su.Def.Locomotor == Locomotor.Submarine) { su.Submerged = false; su.SurfaceTimer = 4f; }
            }
            float mult = DamageMultOf(shooter, wd);
            int team = _w.TeamOf(shooter.Owner);
            float dist = Vec2.Distance(shooter.Pos, aimPos);
            float startH = su != null ? su.Height + (su.IsAir ? 0 : 0.5f) : 0.8f;
            float targetH = target is Unit tu ? tu.Height : 0.3f;
            for (int i = 0; i < wd.Burst; i++)
            {
                Vec2 impact = aimPos;
                if (wd.Inaccuracy > 0)
                {
                    float scatter = wd.Inaccuracy * SMath.Clamp01(dist / Math.Max(1f, wd.Range)) * (su != null && su.Rank >= 1 ? 0.7f : 1f);
                    impact += _w.Rng.InsideCircle(scatter);
                }
                _w.Emit(new SimEvent { Type = SimEventType.Fire, Player = shooter.Owner, EntityId = shooter.Id, OtherId = target?.Id ?? 0, Pos = shooter.Pos, Pos2 = impact, Text = wd.Id, Value = startH });
                switch (wd.Projectile)
                {
                    case ProjectileKind.Hitscan:
                    case ProjectileKind.Beam:
                    case ProjectileKind.Rail:
                        if (target != null && !target.Dead) ApplyDirect(target, wd, shooter.Id, shooter.Owner, mult);
                        if (wd.Splash > 0) Splash(impact, wd, shooter.Id, shooter.Owner, mult, target);
                        _w.Emit(new SimEvent { Type = SimEventType.Impact, Player = shooter.Owner, Pos = impact, Text = wd.Id, Value = wd.Splash });
                        break;
                    default:
                        var pr = new Projectile
                        {
                            Id = _nextProjectileId++, Kind = wd.Projectile, Pos = shooter.Pos, Start = shooter.Pos, Target = impact,
                            TargetId = (wd.Projectile == ProjectileKind.Missile || wd.Projectile == ProjectileKind.Torpedo) ? (target?.Id ?? 0) : (wd.Inaccuracy > 0 ? 0 : (target?.Id ?? 0)),
                            Speed = wd.ProjectileSpeed, WeaponId = wd.Id, Weapon = wd, Owner = shooter.Owner, Team = team, SourceId = shooter.Id, DamageMult = mult,
                            StartHeight = startH, Height = startH, TargetHeight = targetH
                        };
                        pr.TotalDist = Math.Max(0.1f, Vec2.Distance(pr.Start, pr.Target));
                        if (wd.Projectile == ProjectileKind.Bomb) pr.TotalDist = Math.Max(0.5f, startH * 0.6f);
                        // Stagger burst projectiles slightly
                        pr.Travelled = -i * 0.4f;
                        _w.Projectiles.Add(pr);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ Projectiles
        public void UpdateProjectiles()
        {
            var list = _w.Projectiles;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                if (p.Weapon == null) p.Weapon = Catalog.Weapon(p.WeaponId);
                float step = p.Speed * World.Dt;
                if (p.Travelled < 0) { p.Travelled += step; continue; }
                if (p.Kind == ProjectileKind.Missile || p.Kind == ProjectileKind.Torpedo)
                {
                    var t = _w.Get(p.TargetId);
                    if (t != null && !(t is Unit tu && tu.IsInside))
                    {
                        p.Target = t.Pos;
                        p.TargetHeight = t is Unit u2 ? u2.Height : 0.3f;
                    }
                    var to = p.Target - p.Pos;
                    float d = to.Length;
                    if (d <= step + 0.15f) { Impact(p); list.RemoveAt(i); continue; }
                    p.Pos += to / d * step;
                    p.Travelled += step;
                    p.Height = SMath.Lerp(p.Height, p.TargetHeight + (p.Kind == ProjectileKind.Missile ? 0.5f : 0), 0.15f);
                    if (p.Travelled > p.TotalDist * 2.5f + 10) { Impact(p); list.RemoveAt(i); }
                    continue;
                }
                if (p.Kind == ProjectileKind.Bomb)
                {
                    p.Travelled += step;
                    p.Height = SMath.Max(0, p.StartHeight * (1 - p.Travelled / p.TotalDist));
                    if (p.Travelled >= p.TotalDist) { Impact(p); list.RemoveAt(i); }
                    continue;
                }
                p.Travelled += step;
                float t01 = SMath.Clamp01(p.Travelled / p.TotalDist);
                p.Pos = Vec2.Lerp(p.Start, p.Target, t01);
                if (p.Kind == ProjectileKind.Ballistic) p.Height = SMath.Lerp(p.StartHeight, 0.2f, t01) + (float)Math.Sin(t01 * Math.PI) * Math.Max(2f, p.TotalDist * 0.35f);
                else p.Height = SMath.Lerp(p.StartHeight, p.TargetHeight, t01);
                if (t01 >= 1f) { Impact(p); list.RemoveAt(i); }
            }
        }

        void Impact(Projectile p)
        {
            var wd = p.Weapon;
            if (wd == null) return;
            if (p.Kind == ProjectileKind.Torpedo)
            {
                var c = Int2.Floor(p.Pos);
                if (!MapData.IsWater(_w.Map.Get(c)) && _w.Map.Get(c) != Terrain.Bridge && _w.BuildingAtCell(c.X, c.Y) == null)
                {
                    _w.Emit(new SimEvent { Type = SimEventType.Impact, Player = p.Owner, Pos = p.Pos, Text = wd.Id, Value = 0.3f });
                    return; // torpedo ran aground
                }
            }
            Entity direct = null;
            var t = _w.Get(p.TargetId);
            if (t != null && !(t is Unit tu && tu.IsInside))
            {
                float tol = t.Radius + 0.45f;
                if ((t is Building tb && tb.DistanceTo(p.Pos) < 0.5f) || Vec2.Distance(t.Pos, p.Pos) < tol) direct = t;
            }
            else if (wd.Splash <= 0)
            {
                // Unguided shells can still hit whatever is at the impact point.
                direct = EntityAt(p.Pos, p.Owner, wd);
            }
            if (direct != null) ApplyDirect(direct, wd, p.SourceId, p.Owner, p.DamageMult);
            if (wd.Splash > 0) Splash(p.Pos, wd, p.SourceId, p.Owner, p.DamageMult, direct);
            if (wd.BurnsGround) _w.Fires.Add(new FireZone { Pos = p.Pos, Radius = 0.9f, TimeLeft = 5f, Dps = 8f, Owner = p.Owner });
            _w.Emit(new SimEvent { Type = SimEventType.Impact, Player = p.Owner, Pos = p.Pos, Text = wd.Id, Value = wd.Splash, OtherId = direct?.Id ?? 0 });
        }

        Entity EntityAt(Vec2 pos, int owner, WeaponDef wd)
        {
            var b = _w.BuildingAtCell((int)pos.X, (int)pos.Y);
            if (b != null && b.Owner != owner && (wd.Targets & TargetLayer.Structure) != 0) return b;
            _w.Spatial.UnitsNear(pos, 0.5f, _units);
            foreach (var u in _units)
                if (u.Owner != owner && (wd.Targets & u.Layer) != 0 && !u.IsInside) return u;
            return null;
        }

        void ApplyDirect(Entity target, WeaponDef wd, int sourceId, int owner, float mult)
        {
            if (!_w.IsEnemy(owner, target.Owner) && !wd.FriendlyFire && target.Owner != owner && !IsForcedTarget(sourceId, target)) return;
            if ((wd.Targets & target.Layer) == 0 && wd.Splash <= 0) return;
            Damage(target, wd.Damage, wd, sourceId, owner, mult);
        }

        bool IsForcedTarget(int sourceId, Entity target)
        {
            var s = _w.Get(sourceId) as Unit;
            return s != null && s.CurrentOrder.Type == OrderType.Attack && s.CurrentOrder.TargetId == target.Id;
        }

        void Splash(Vec2 pos, WeaponDef wd, int sourceId, int owner, float mult, Entity skip)
        {
            float r = wd.Splash;
            _w.Spatial.UnitsNear(pos, r + 0.3f, _units);
            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                if (u == skip || u.IsInside || u.Id == sourceId) continue;
                if ((wd.Targets & u.Layer) == 0 && !(u.Layer == TargetLayer.Ground && (wd.Targets & TargetLayer.Structure) != 0)) continue;
                if (u.IsAir && u.AState == AirState.Flying && (wd.Targets & TargetLayer.Air) == 0) continue;
                if (!_w.IsEnemy(owner, u.Owner) && !wd.FriendlyFire) continue;
                float d = Vec2.Distance(u.Pos, pos);
                float fall = SMath.Clamp01(1f - d / (r + 0.3f)) * 0.75f + 0.25f;
                Damage(u, wd.Damage * fall * (_w.IsEnemy(owner, u.Owner) ? 1f : 0.5f), wd, sourceId, owner, mult);
            }
            if ((wd.Targets & TargetLayer.Structure) != 0)
            {
                _w.Spatial.BuildingsNear(pos, r, _blds);
                for (int i = 0; i < _blds.Count; i++)
                {
                    var b = _blds[i];
                    if (b == skip) continue;
                    if (!_w.IsEnemy(owner, b.Owner) && !wd.FriendlyFire) continue;
                    if (b.Owner == owner && !wd.FriendlyFire) continue;
                    float d = b.DistanceTo(pos);
                    float fall = SMath.Clamp01(1f - d / (r + 0.3f)) * 0.6f + 0.2f;
                    Damage(b, wd.Damage * fall * (_w.IsEnemy(owner, b.Owner) ? 1f : 0.3f), wd, sourceId, owner, mult);
                }
            }
            // Destructible trees
            if (r >= 0.9f && (wd.Warhead == "HE" || wd.Warhead == "Flame" || wd.Warhead == "Siege" || wd.Warhead == "Bomb" || wd.Warhead == "NavalShell" || wd.Warhead == "Super"))
            {
                int ir = (int)Math.Ceiling(r);
                for (int dy = -ir; dy <= ir; dy++)
                    for (int dx = -ir; dx <= ir; dx++)
                    {
                        int x = (int)pos.X + dx, y = (int)pos.Y + dy;
                        if (_w.Map.Get(x, y) == Terrain.Forest && _w.Rng.Chance(wd.Warhead == "Super" ? 0.9f : 0.12f))
                        {
                            _w.Map.Set(x, y, Terrain.Dirt);
                            _w.Pathfinder.Version++;
                            _w.Emit(new SimEvent { Type = SimEventType.TreeDestroyed, Player = -1, Pos = new Vec2(x + 0.5f, y + 0.5f) });
                        }
                    }
            }
        }

        public void Damage(Entity target, float baseDamage, WeaponDef wd, int sourceId, int owner, float mult)
        {
            if (target.Dead || target.Invulnerable) return;
            float dmg = baseDamage * mult * Catalog.Warhead(wd?.Warhead ?? "Super").Get(target.Armor);
            if (target is Unit u)
            {
                if (u.Def.Category == UnitCategory.Infantry)
                {
                    var c = Int2.Floor(u.Pos);
                    dmg *= _w.Map.CoverMultiplier(c.X, c.Y);
                    if (u.SuppressedTimer > 0) dmg *= 0.85f; // prone
                    if (wd != null && wd.Suppression > 0) u.SuppressedTimer = Math.Max(u.SuppressedTimer, wd.Suppression * 2.5f);
                }
                if (u.Rank == 1) dmg *= 0.9f; else if (u.Rank >= 2) dmg *= 0.8f;
                if (wd != null && wd.EmpSeconds > 0 && u.Def.Category == UnitCategory.Vehicle) u.EmpTimer = Math.Max(u.EmpTimer, wd.EmpSeconds);
                if (u.Def.Has(UnitAbility.Harvester) && _w.IsEnemy(owner, u.Owner)) NotifyAttack(u, true);
                else if (_w.IsEnemy(owner, u.Owner)) NotifyAttack(u, false);
            }
            else if (target is Building b)
            {
                if (b.BuildUp < 1f) dmg *= 1.25f;
                if (_w.IsEnemy(owner, b.Owner)) NotifyAttack(b, false);
            }
            if (dmg <= 0) return;
            target.Hp -= dmg;
            target.LastAttackerId = sourceId;
            target.LastDamagedTime = _w.Time;
            if (target.Hp <= 0) _w.Kill(target, sourceId);
        }

        void NotifyAttack(Entity victim, bool harvester)
        {
            var p = _w.GetPlayer(victim.Owner);
            if (p == null || p.IsNeutral) return;
            if (_w.Time - p.AttackCooldown < 12f) return;
            p.AttackCooldown = _w.Time;
            bool baseHit = victim is Building;
            _w.Emit(new SimEvent
            {
                Type = harvester ? SimEventType.HarvesterUnderAttack : SimEventType.UnderAttack, Player = victim.Owner, EntityId = victim.Id,
                Pos = victim.Pos, Value = baseHit ? 1 : 0
            });
        }

        public void GrantXp(Entity killer, Entity victim)
        {
            if (!(killer is Unit u) || u.Rank >= 2) return;
            u.Xp += victim.IsBuilding ? victim.Cost * 0.4f : victim.Cost;
            float basis = Math.Max(300, u.Def.Cost);
            int newRank = u.Xp >= basis * 2.5f ? 2 : u.Xp >= basis ? 1 : 0;
            if (newRank > u.Rank)
            {
                u.Rank = newRank;
                u.MaxHp *= 1.1f; u.Hp = Math.Min(u.MaxHp, u.Hp + u.MaxHp * 0.25f);
                _w.Emit(new SimEvent { Type = SimEventType.Promoted, Player = u.Owner, EntityId = u.Id, Pos = u.Pos, Value = newRank });
            }
        }

        // ------------------------------------------------------------------ Buildings
        public void UpdateBuildings()
        {
            var list = _w.Buildings;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.Dead) continue;
                if (b.EmpTimer > 0) b.EmpTimer -= World.Dt;
                if (b.Weapons.Count == 0 || !b.Operational) continue;
                TickWeapons(b, World.Dt);
                var p = _w.GetPlayer(b.Owner);
                if (b.Def.RequiresPowerToFire && p != null && p.PowerRatio < 0.5f) continue;
                var t = _w.Get(b.TargetId);
                b.AcquireTimer -= World.Dt;
                bool valid = t != null && _w.IsEnemy(b.Owner, t.Owner) && CanTarget(b, t) && VisibleTo(t, _w.TeamOf(b.Owner)) && InRange(b, t);
                if (!valid || b.AcquireTimer <= 0)
                {
                    b.AcquireTimer = 0.5f;
                    var nt = FindTarget(b, b.Center, MaxRangeAny(b) + Math.Max(b.W, b.H) * 0.5f);
                    if (nt != null && InRange(b, nt)) { b.TargetId = nt.Id; t = nt; valid = true; }
                    else if (!valid) { b.TargetId = 0; continue; }
                }
                if (valid) Engage(b, t);
            }
        }

        public void UpdateFires()
        {
            var fires = _w.Fires;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                var f = fires[i];
                f.TimeLeft -= World.Dt;
                if (f.TimeLeft <= 0) { fires.RemoveAt(i); continue; }
                if (_w.Tick % 5 != 0) continue;
                _w.Spatial.UnitsNear(f.Pos, f.Radius, _units);
                foreach (var u in _units)
                {
                    if (u.IsAir || u.MoveClass == MoveClass.Naval) continue;
                    float mult = u.Def.Category == UnitCategory.Infantry ? 1f : 0.25f;
                    u.Hp -= f.Dps * mult * World.Dt * 5;
                    if (u.Hp <= 0) _w.Kill(u, 0);
                }
                if (_w.Map.Get((int)f.Pos.X, (int)f.Pos.Y) == Terrain.Forest && _w.Rng.Chance(0.05f))
                {
                    _w.Map.Set((int)f.Pos.X, (int)f.Pos.Y, Terrain.Dirt);
                    _w.Emit(new SimEvent { Type = SimEventType.TreeDestroyed, Player = -1, Pos = f.Pos });
                }
            }
        }
    }
}
