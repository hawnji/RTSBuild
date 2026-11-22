using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// Per-unit order execution: move, attack, attack-move, guard, patrol, stances, transports, engineers,
    /// MCV deployment, aircraft ammunition / rearm / return-to-base, passive healing abilities and veterancy regen.
    /// </summary>
    public class UnitBehaviorSystem
    {
        readonly World _w;
        readonly List<Unit> _near = new List<Unit>();

        public UnitBehaviorSystem(World w) { _w = w; }

        public void UpdateAll()
        {
            var list = _w.Units;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                if (u.Dead) continue;
                if (u.IsInside)
                {
                    var c = _w.GetUnit(u.CarrierId);
                    if (c == null) { u.CarrierId = 0; continue; }
                    u.Pos = c.Pos; u.PrevPos = c.Pos;
                    continue;
                }
                Timers(u);
                _w.Combat.TickWeapons(u, World.Dt);
                if ((_w.Tick + u.Id) % 8 == 0) Passive(u);
                Think(u);
            }
        }

        void Timers(Unit u)
        {
            float dt = World.Dt;
            if (u.SuppressedTimer > 0) u.SuppressedTimer -= dt;
            if (u.EmpTimer > 0) u.EmpTimer -= dt;
            if (u.SurfaceTimer > 0) { u.SurfaceTimer -= dt; if (u.SurfaceTimer <= 0) u.Submerged = true; }
            if (u.Rank >= 2 && u.Hp < u.MaxHp && _w.Time - u.LastDamagedTime > 5f) u.Hp = Math.Min(u.MaxHp, u.Hp + u.MaxHp * 0.01f * dt);
        }

        // ------------------------------------------------------------------ Orders
        public void SetOrder(Unit u, Order o, bool queue)
        {
            if (queue && (u.CurrentOrder.Type != OrderType.None || u.Moving))
            {
                if (u.OrderQueue.Count < 16) u.OrderQueue.Add(o);
                return;
            }
            u.OrderQueue.Clear();
            Begin(u, o);
        }

        void Begin(Unit u, Order o)
        {
            u.CurrentOrder = o;
            u.TargetId = 0;
            u.ChaseTimer = 0;
            if (u.Def.Has(UnitAbility.Harvester) && o.Type != OrderType.Harvest && o.Type != OrderType.None && o.Type != OrderType.ReturnCargo)
                u.HState = HarvestState.Idle;
            switch (o.Type)
            {
                case OrderType.Move:
                case OrderType.AttackMove:
                    if (u.IsAir && u.AState == AirState.Docked) { _w.ReleasePad(u); u.AState = AirState.TakingOff; }
                    _w.Movement.RequestMove(u, o.Pos);
                    u.HasGuardPos = false;
                    break;
                case OrderType.Patrol:
                    _w.Movement.RequestMove(u, o.Pos);
                    break;
                case OrderType.Stop:
                    _w.Movement.Stop(u);
                    u.CurrentOrder = Order.None;
                    u.GuardPos = u.Pos; u.HasGuardPos = true;
                    break;
                case OrderType.Guard:
                    u.GuardPos = o.Pos; u.HasGuardPos = true;
                    _w.Movement.RequestMove(u, o.Pos);
                    break;
                case OrderType.Harvest:
                    u.HState = HarvestState.Seeking;
                    if (o.Pos != Vec2.Zero) { u.LastHarvestPos = o.Pos; u.HasLastHarvest = true; }
                    u.CurrentOrder = Order.None;
                    break;
                case OrderType.ReturnCargo:
                    u.HState = HarvestState.Returning;
                    u.CurrentOrder = Order.None;
                    break;
                case OrderType.Attack:
                    u.TargetId = o.TargetId;
                    if (u.IsAir && u.AState == AirState.Docked) { _w.ReleasePad(u); u.AState = AirState.TakingOff; }
                    break;
            }
        }

        void Complete(Unit u)
        {
            u.CurrentOrder = Order.None;
            if (u.OrderQueue.Count > 0)
            {
                var next = u.OrderQueue[0];
                u.OrderQueue.RemoveAt(0);
                Begin(u, next);
            }
            else if (!u.IsAir)
            {
                u.GuardPos = u.Pos; u.HasGuardPos = true;
            }
        }

        // ------------------------------------------------------------------ Thinking
        void Think(Unit u)
        {
            if (u.Disabled && u.Def.Category != UnitCategory.Infantry) return;
            if (u.IsAir) { ThinkAir(u); return; }
            var o = u.CurrentOrder;
            switch (o.Type)
            {
                case OrderType.None: Idle(u); break;
                case OrderType.Move:
                    if (!u.Moving) Complete(u);
                    else OpportunityFire(u);
                    break;
                case OrderType.AttackMove: AttackMove(u, o.Pos, false); break;
                case OrderType.Patrol: Patrol(u); break;
                case OrderType.Attack: AttackTarget(u, o.TargetId, true); break;
                case OrderType.ForceFireGround:
                    if (_w.Combat.EngageGround(u, o.Pos)) _w.Movement.Stop(u);
                    else if (!u.Moving) _w.Movement.RequestMove(u, o.Pos);
                    break;
                case OrderType.Guard: Guard(u); break;
                case OrderType.Deploy: Deploy(u); break;
                case OrderType.Enter: Enter(u, o.TargetId); break;
                case OrderType.Unload: Unload(u); break;
                case OrderType.Capture: Capture(u, o.TargetId); break;
                case OrderType.RepairAtDepot: RepairAtDepot(u, o.TargetId); break;
                case OrderType.Follow: Follow(u, o.TargetId); break;
                case OrderType.Demolish: AttackTarget(u, o.TargetId, true); break;
                default: Complete(u); break;
            }
        }

        void Idle(Unit u)
        {
            if (u.Def.Has(UnitAbility.Harvester)) { _w.Economy.UpdateHarvester(u); if (!u.IsArmed) return; }
            if (!u.IsArmed || u.Stance == Stance.HoldFire) return;
            var target = _w.Get(u.TargetId);
            bool valid = target != null && _w.IsEnemy(u.Owner, target.Owner) && _w.Combat.CanTarget(u, target) && _w.Combat.VisibleTo(target, _w.TeamOf(u.Owner));
            u.AcquireTimer -= World.Dt;
            if (!valid || u.AcquireTimer <= 0)
            {
                u.AcquireTimer = 0.55f;
                float radius = u.Stance == Stance.HoldPosition ? _w.Combat.MaxRangeAny(u) : Math.Max(u.SightRange, _w.Combat.MaxRangeAny(u));
                var best = _w.Combat.FindTarget(u, u.Pos, radius);
                if (best != null && (!valid || best.Id != u.TargetId))
                {
                    // Keep current target if it is still attackable and in range.
                    if (!(valid && _w.Combat.InRange(u, target))) { target = best; u.TargetId = best.Id; valid = true; }
                }
            }
            if (!valid) { u.TargetId = 0; ReturnToGuard(u); return; }
            if (_w.Combat.Engage(u, target)) { if (u.Moving) _w.Movement.Stop(u); return; }
            if (u.Stance == Stance.HoldPosition) { u.TargetId = 0; return; }
            // Chase limited by stance
            float leash = u.Stance == Stance.Guard ? 5f : 12f;
            if (u.HasGuardPos && Vec2.Distance(u.Pos, u.GuardPos) > leash) { u.TargetId = 0; _w.Movement.RequestMove(u, u.GuardPos); return; }
            Chase(u, target);
        }

        void ReturnToGuard(Unit u)
        {
            if (!u.HasGuardPos || u.Moving) return;
            if (Vec2.Distance(u.Pos, u.GuardPos) > 2.5f && u.Stance != Stance.Aggressive) _w.Movement.RequestMove(u, u.GuardPos);
        }

        void Chase(Unit u, Entity target)
        {
            if (_w.Combat.TooClose(u, target))
            {
                // Artillery backs away from targets inside its minimum range.
                if (!u.Moving) { var away = (u.Pos - target.Pos).Normalized * 3f; _w.Movement.RequestMove(u, u.Pos + away); }
                return;
            }
            u.ChaseTimer -= World.Dt;
            if (!u.Moving || u.ChaseTimer <= 0)
            {
                u.ChaseTimer = 1.2f;
                Vec2 dest = target is Building b ? NearestPointOn(b, u.Pos) : target.Pos;
                _w.Movement.RequestMove(u, dest);
            }
        }

        static Vec2 NearestPointOn(Building b, Vec2 p)
        {
            float x = SMath.Clamp(p.X, b.Origin.X - 0.5f, b.Origin.X + b.W + 0.5f);
            float y = SMath.Clamp(p.Y, b.Origin.Y - 0.5f, b.Origin.Y + b.H + 0.5f);
            return new Vec2(x, y);
        }

        void OpportunityFire(Unit u)
        {
            if (!u.IsArmed || u.Stance == Stance.HoldFire) return;
            u.AcquireTimer -= World.Dt;
            var t = _w.Get(u.TargetId);
            if (t == null || u.AcquireTimer <= 0)
            {
                u.AcquireTimer = 0.6f;
                t = _w.Combat.FindTarget(u, u.Pos, _w.Combat.MaxRangeAny(u));
                u.TargetId = t?.Id ?? 0;
            }
            if (t != null && _w.Combat.InRange(u, t)) _w.Combat.Engage(u, t, false);
        }

        void AttackMove(Unit u, Vec2 dest, bool patrol)
        {
            var target = _w.Get(u.TargetId);
            bool valid = target != null && _w.IsEnemy(u.Owner, target.Owner) && _w.Combat.CanTarget(u, target) && _w.Combat.VisibleTo(target, _w.TeamOf(u.Owner));
            u.AcquireTimer -= World.Dt;
            if (u.AcquireTimer <= 0 || !valid)
            {
                u.AcquireTimer = 0.5f;
                var t = _w.Combat.FindTarget(u, u.Pos, Math.Max(u.SightRange, _w.Combat.MaxRangeAny(u)));
                if (t != null && (!valid || !_w.Combat.InRange(u, target))) { target = t; u.TargetId = t.Id; valid = true; }
            }
            if (valid)
            {
                if (_w.Combat.Engage(u, target)) { if (u.Moving) _w.Movement.Stop(u); }
                else Chase(u, target);
                return;
            }
            u.TargetId = 0;
            if (Vec2.Distance(u.Pos, dest) < 1.5f || (!u.Moving && Vec2.Distance(u.Pos, dest) < 3.5f))
            {
                if (!patrol) { _w.Movement.Stop(u); Complete(u); }
                return;
            }
            if (!u.Moving) _w.Movement.RequestMove(u, dest);
        }

        void Patrol(Unit u)
        {
            var o = u.CurrentOrder;
            AttackMove(u, o.Pos, true);
            if (u.TargetId == 0 && Vec2.Distance(u.Pos, o.Pos) < 1.6f)
            {
                var swapped = new Order(OrderType.Patrol, o.Pos2) { Pos2 = o.Pos };
                u.CurrentOrder = swapped;
                _w.Movement.RequestMove(u, swapped.Pos);
            }
        }

        void AttackTarget(Unit u, int targetId, bool forced)
        {
            var t = _w.Get(targetId);
            if (t == null || !(_w.Combat.CanTarget(u, t) || u.CurrentOrder.Type == OrderType.Demolish))
            { _w.Movement.Stop(u); Complete(u); return; }
            if (!_w.Combat.VisibleTo(t, _w.TeamOf(u.Owner)) && !(t is Building))
            {
                // Lost sight: move to last known position then give up.
                if (!u.Moving) { Complete(u); }
                return;
            }
            u.TargetId = t.Id;
            if (_w.Combat.Engage(u, t)) { if (u.Moving) _w.Movement.Stop(u); }
            else Chase(u, t);
        }

        void Guard(Unit u)
        {
            var o = u.CurrentOrder;
            // Guarding a friendly unit: follow it.
            if (o.TargetId != 0)
            {
                var g = _w.Get(o.TargetId);
                if (g == null || !_w.IsAlly(g.Owner, u.Owner)) { Complete(u); return; }
                u.GuardPos = g.Pos;
            }
            var t = _w.Get(u.TargetId);
            bool valid = t != null && _w.IsEnemy(u.Owner, t.Owner) && _w.Combat.VisibleTo(t, _w.TeamOf(u.Owner)) && Vec2.Distance(t.Pos, u.GuardPos) < u.SightRange + 4;
            u.AcquireTimer -= World.Dt;
            if (!valid && u.AcquireTimer <= 0)
            {
                u.AcquireTimer = 0.5f;
                var nt = _w.Combat.FindTarget(u, u.GuardPos, u.SightRange + 2f);
                if (nt != null) { t = nt; u.TargetId = nt.Id; valid = true; }
            }
            if (valid)
            {
                if (_w.Combat.Engage(u, t)) { if (u.Moving) _w.Movement.Stop(u); }
                else Chase(u, t);
                return;
            }
            u.TargetId = 0;
            if (Vec2.Distance(u.Pos, u.GuardPos) > 2.2f && !u.Moving) _w.Movement.RequestMove(u, u.GuardPos);
        }

        void Follow(Unit u, int targetId)
        {
            var t = _w.Get(targetId);
            if (t == null) { Complete(u); return; }
            if (Vec2.Distance(u.Pos, t.Pos) > 3f && (!u.Moving || (_w.Tick + u.Id) % 15 == 0)) _w.Movement.RequestMove(u, t.Pos);
            OpportunityFire(u);
        }

        // ------------------------------------------------------------------ Special orders
        void Deploy(Unit u)
        {
            if (string.IsNullOrEmpty(u.Def.DeploysInto)) { Complete(u); return; }
            var def = Catalog.Building(u.DeploysIntoDef());
            var origin = new Int2((int)Math.Floor(u.Pos.X) - def.Width / 2, (int)Math.Floor(u.Pos.Y) - def.Height / 2);
            if (_w.Production.CanPlaceIgnoringUnit(u.Owner, def, origin, false, u.Id, requireAdjacency: false))
            {
                var b = _w.SpawnBuilding(def.Id, u.Owner, origin, false);
                b.Hp = b.MaxHp * u.HpRatio;
                _w.Emit(new SimEvent { Type = SimEventType.Deployed, Player = u.Owner, EntityId = b.Id, Pos = b.Center, Text = def.Id });
                u.Dead = true; // silently removed (not a loss)
                var p = _w.GetPlayer(u.Owner); if (p != null) p.Stats.UnitsBuilt--;
            }
            else
            {
                _w.Emit(new SimEvent { Type = SimEventType.CannotPlace, Player = u.Owner, EntityId = u.Id, Pos = u.Pos });
                Complete(u);
            }
        }

        void Enter(Unit u, int transportId)
        {
            var t = _w.GetUnit(transportId);
            if (t == null || !_w.IsAlly(t.Owner, u.Owner) || !CanBoard(t, u)) { _w.Movement.Stop(u); Complete(u); return; }
            float reach = t.IsAir ? 1.4f : 1.3f + t.Radius;
            if (Vec2.Distance(u.Pos, t.Pos) <= reach && (!t.IsAir || t.Height < 1.5f || t.Def.Locomotor == Locomotor.Helicopter))
            {
                t.Passengers.Add(u.Id);
                u.CarrierId = t.Id;
                _w.Movement.Stop(u);
                u.CurrentOrder = Order.None; u.OrderQueue.Clear();
                _w.Emit(new SimEvent { Type = SimEventType.Loaded, Player = u.Owner, EntityId = t.Id, OtherId = u.Id, Pos = t.Pos });
                return;
            }
            if (!u.Moving || (_w.Tick + u.Id) % 20 == 0)
            {
                var dest = t.MoveClass == MoveClass.Naval ? ShorePointNear(t.Pos, u.MoveClass) : t.Pos;
                _w.Movement.RequestMove(u, dest);
            }
        }

        Vec2 ShorePointNear(Vec2 p, MoveClass mc)
        {
            var c = _w.FindFreeCellNear(p, mc, 4);
            return c.HasValue ? c.Value.Center : p;
        }

        public bool CanBoard(Unit transport, Unit passenger)
        {
            if (!transport.Def.Has(UnitAbility.Transport) || transport.Dead) return false;
            if (passenger.IsAir || passenger.MoveClass == MoveClass.Naval) return false;
            if (passenger.Def.Category == UnitCategory.Vehicle && !transport.Def.TransportVehicles) return false;
            if (transport.Def.Id == "ca_mule" || transport.Def.Id == "id_mole")
                if (!(passenger.Def.Has(UnitAbility.CaptureRepair) || passenger.Def.Has(UnitAbility.HealInfantry))) return false;
            int used = 0;
            foreach (var pid in transport.Passengers)
            {
                var p = _w.GetUnit(pid);
                if (p != null) used += p.Def.Category == UnitCategory.Vehicle ? 3 : 1;
            }
            int need = passenger.Def.Category == UnitCategory.Vehicle ? 3 : 1;
            return used + need <= transport.Def.TransportCapacity;
        }

        void Unload(Unit u)
        {
            if (u.Passengers.Count == 0) { Complete(u); return; }
            if (u.Moving) return;
            if (u.IsAir && u.Height > 0.6f)
            {
                // Helicopters descend to unload.
                if (!_w.Map.PassableStatic((int)u.Pos.X, (int)u.Pos.Y, MoveClass.Foot)) { Complete(u); return; }
                u.Height = SMath.MoveTowards(u.Height, 0.5f, 3f * World.Dt);
                return;
            }
            int unloaded = 0;
            for (int i = u.Passengers.Count - 1; i >= 0; i--)
            {
                var p = _w.GetUnit(u.Passengers[i]);
                if (p == null) { u.Passengers.RemoveAt(i); continue; }
                var cell = _w.FindFreeCellNear(u.Pos, p.MoveClass, 4, _w.TeamOf(u.Owner));
                if (!cell.HasValue) break;
                p.CarrierId = 0;
                p.Pos = cell.Value.Center + _w.Rng.InsideCircle(0.25f); p.PrevPos = p.Pos;
                p.GuardPos = p.Pos; p.HasGuardPos = true;
                u.Passengers.RemoveAt(i);
                unloaded++;
            }
            if (unloaded > 0) _w.Emit(new SimEvent { Type = SimEventType.Unloaded, Player = u.Owner, EntityId = u.Id, Pos = u.Pos, Value = unloaded });
            if (u.Passengers.Count == 0 || unloaded == 0) Complete(u);
        }

        void Capture(Unit u, int buildingId)
        {
            var b = _w.GetBuilding(buildingId);
            if (b == null || !u.Def.Has(UnitAbility.CaptureRepair)) { Complete(u); return; }
            if (b.DistanceTo(u.Pos) <= 0.9f)
            {
                var me = _w.GetPlayer(u.Owner);
                if (_w.IsAlly(b.Owner, u.Owner))
                {
                    if (b.Hp < b.MaxHp)
                    {
                        b.Hp = b.MaxHp;
                        _w.Emit(new SimEvent { Type = SimEventType.Repairing, Player = u.Owner, EntityId = b.Id, Pos = b.Center });
                        u.Dead = true;
                    }
                    Complete(u);
                    return;
                }
                bool neutral = _w.GetPlayer(b.Owner)?.IsNeutral ?? true;
                if (b.Def.Capturable && (neutral || b.HpRatio <= 0.5f || b.Def.Role == BuildingRole.TechStructure))
                {
                    _w.ChangeOwner(b, u.Owner);
                    u.Dead = true;
                }
                else
                {
                    // Sabotage: engineers damage structures that are too healthy to capture.
                    _w.Combat.Damage(b, b.MaxHp * 0.25f, null, u.Id, u.Owner, 1f);
                    u.Dead = true;
                }
                if (me != null) me.Stats.UnitsBuilt--; // consumed, not lost
                Complete(u);
                return;
            }
            if (!u.Moving || (_w.Tick + u.Id) % 30 == 0) _w.Movement.RequestMove(u, NearestPointOn(b, u.Pos));
        }

        void RepairAtDepot(Unit u, int depotId)
        {
            var d = _w.GetBuilding(depotId);
            if (d == null || !_w.IsAlly(d.Owner, u.Owner)) { Complete(u); return; }
            if (Vec2.Distance(u.Pos, d.Center) < 1.4f)
            {
                _w.Movement.Stop(u);
                if (u.Hp >= u.MaxHp) Complete(u);
                return; // depot heals units on its pad (EconomySystem)
            }
            if (!u.Moving) _w.Movement.RequestMove(u, d.Center);
        }

        // ------------------------------------------------------------------ Aircraft
        void ThinkAir(Unit u)
        {
            var o = u.CurrentOrder;
            bool needsAmmo = u.Def.Ammo > 0 && u.Ammo <= 0;
            if (u.AState == AirState.Docked)
            {
                Rearm(u);
                if (o.Type == OrderType.Move || o.Type == OrderType.Attack || o.Type == OrderType.AttackMove || o.Type == OrderType.Patrol || o.Type == OrderType.Guard)
                {
                    if (!needsAmmo || o.Type == OrderType.Move) { _w.ReleasePad(u); u.AState = AirState.TakingOff; if (o.Type == OrderType.Move || o.Type == OrderType.AttackMove || o.Type == OrderType.Patrol) _w.Movement.RequestMove(u, o.Pos); }
                }
                else if (o.Type != OrderType.None && o.Type != OrderType.Rearm) Complete(u);
                return;
            }
            if (u.AState == AirState.TakingOff) return;
            if (needsAmmo && o.Type != OrderType.Move && o.Type != OrderType.Unload && o.Type != OrderType.Rearm)
            {
                ReturnToPad(u);
                return;
            }
            switch (o.Type)
            {
                case OrderType.Attack:
                    {
                        var t = _w.Get(o.TargetId);
                        if (t == null || !_w.Combat.CanTarget(u, t)) { Complete(u); break; }
                        if (!_w.Combat.VisibleTo(t, _w.TeamOf(u.Owner)) && !(t is Building)) { Complete(u); break; }
                        AirEngage(u, t);
                    }
                    break;
                case OrderType.Move:
                    if (!u.Moving) Complete(u);
                    else if (u.Def.Locomotor == Locomotor.Helicopter) OpportunityFire(u);
                    break;
                case OrderType.AttackMove:
                case OrderType.Patrol:
                case OrderType.Guard:
                    {
                        var center = o.Type == OrderType.Guard ? u.GuardPos : u.Pos;
                        var t = _w.Get(u.TargetId);
                        if (t == null || !_w.Combat.VisibleTo(t, _w.TeamOf(u.Owner)) || (_w.Tick + u.Id) % 10 == 0)
                        {
                            var nt = _w.Combat.FindTarget(u, center, u.SightRange + 1);
                            if (nt != null) t = nt;
                            u.TargetId = t?.Id ?? 0;
                        }
                        if (t != null && u.IsArmed) AirEngage(u, t);
                        else
                        {
                            if (o.Type == OrderType.Patrol && Vec2.Distance(u.Pos, o.Pos) < 1.5f)
                            {
                                u.CurrentOrder = new Order(OrderType.Patrol, o.Pos2) { Pos2 = o.Pos };
                                _w.Movement.RequestMove(u, o.Pos2);
                            }
                            else if (o.Type == OrderType.AttackMove && Vec2.Distance(u.Pos, o.Pos) < 1.5f) Complete(u);
                            else if (!u.Moving && o.Type != OrderType.Guard) _w.Movement.RequestMove(u, o.Pos);
                            else if (o.Type == OrderType.Guard && !u.Moving && Vec2.Distance(u.Pos, u.GuardPos) > 2) _w.Movement.RequestMove(u, u.GuardPos);
                        }
                    }
                    break;
                case OrderType.Unload: Unload(u); break;
                case OrderType.Rearm: ReturnToPad(u); break;
                case OrderType.None:
                    if (u.Def.Locomotor == Locomotor.Air)
                    {
                        // Jets return to base when idle.
                        if (u.Def.Has(UnitAbility.Recon) && u.Moving) break;
                        if (HasPadAvailable(u)) ReturnToPad(u);
                    }
                    else if (u.IsArmed && u.Stance != Stance.HoldFire)
                    {
                        var t = _w.Get(u.TargetId);
                        if (t == null || (_w.Tick + u.Id) % 10 == 0) { t = _w.Combat.FindTarget(u, u.Pos, u.SightRange); u.TargetId = t?.Id ?? 0; }
                        if (t != null && Vec2.Distance(u.Pos, t.Pos) < u.SightRange + 3) AirEngage(u, t);
                    }
                    break;
                default: Complete(u); break;
            }
        }

        void AirEngage(Unit u, Entity t)
        {
            u.TargetId = t.Id;
            bool jet = u.Def.Locomotor == Locomotor.Air;
            float d = Vec2.Distance(u.Pos, t.Pos);
            bool bomber = u.Weapons.Count > 0 && u.Weapons[0].Def != null && u.Weapons[0].Def.Projectile == ProjectileKind.Bomb;
            if (jet)
            {
                // Strafing runs: fly at the target; overshoot and loop back.
                _w.Movement.RequestMove(u, t.Pos + Vec2.FromAngle(u.Facing) * (bomber ? 0.5f : -1f));
                u.MoveGoal = t.Pos;
                u.Moving = true;
                _w.Combat.Engage(u, t, false);
            }
            else
            {
                float range = _w.Combat.MaxRange(u, t.Layer);
                if (d > range * 0.85f)
                {
                    if (!u.Moving || (_w.Tick + u.Id) % 15 == 0) _w.Movement.RequestMove(u, t.Pos);
                }
                else
                {
                    u.Moving = false;
                    u.Facing = SMath.RotateTowards(u.Facing, (t.Pos - u.Pos).Angle, u.Def.TurnRate * 1.5f * World.Dt);
                }
                _w.Combat.Engage(u, t, true);
            }
        }

        bool HasPadAvailable(Unit u)
        {
            if (u.PadBuildingId != 0 && _w.GetBuilding(u.PadBuildingId) != null) return true;
            return FindPad(u, out _, out _);
        }

        bool FindPad(Unit u, out Building pad, out int index)
        {
            pad = null; index = -1;
            float best = float.MaxValue;
            foreach (var b in _w.Buildings)
            {
                if (b.Dead || b.Owner != u.Owner || b.Pads.Length == 0 || b.BuildUp < 1) continue;
                for (int i = 0; i < b.Pads.Length; i++)
                {
                    if (b.Pads[i] != 0 && _w.GetUnit(b.Pads[i]) != null && b.Pads[i] != u.Id) continue;
                    float d = Vec2.Distance(u.Pos, b.Center);
                    if (d < best) { best = d; pad = b; index = i; }
                    break;
                }
            }
            return pad != null;
        }

        public Vec2 PadPosition(Building b, int index)
        {
            int cols = Math.Max(1, b.W - 1);
            float px = b.Origin.X + 0.75f + (index % 2) * (b.W - 1.5f);
            float py = b.Origin.Y + 0.6f + (index / 2) * Math.Max(0.8f, b.H - 1.2f);
            return new Vec2(px, py);
        }

        void ReturnToPad(Unit u)
        {
            var pad = _w.GetBuilding(u.PadBuildingId);
            if (pad == null || u.PadIndex < 0)
            {
                _w.ReleasePad(u);
                if (!FindPad(u, out pad, out int idx))
                {
                    // Nowhere to land: helicopters hover, jets orbit near home.
                    if (u.Def.Locomotor == Locomotor.Air)
                    {
                        var home = _w.GetPlayer(u.Owner)?.StartCell.Center ?? u.Pos;
                        if (!u.Moving && Vec2.Distance(u.Pos, home) > 4) _w.Movement.RequestMove(u, home);
                    }
                    if (u.CurrentOrder.Type == OrderType.Rearm) Complete(u);
                    return;
                }
                pad.Pads[idx] = u.Id; u.PadBuildingId = pad.Id; u.PadIndex = idx;
            }
            var pos = PadPosition(pad, u.PadIndex);
            float d = Vec2.Distance(u.Pos, pos);
            if (d < 2.5f || (u.Def.Locomotor == Locomotor.Helicopter && d < 4))
            {
                u.AState = AirState.Landing;
                u.MoveGoal = pos;
                u.Moving = true;
            }
            else if (!u.Moving || Vec2.Distance(u.MoveGoal, pos) > 0.5f) { _w.Movement.RequestMove(u, pos); }
        }

        void Rearm(Unit u)
        {
            var pad = _w.GetBuilding(u.PadBuildingId);
            if (pad == null) { u.AState = AirState.TakingOff; return; }
            u.RearmTimer += World.Dt;
            var p = _w.GetPlayer(u.Owner);
            float per = 1.2f * (p != null ? p.Mods.UnitMult(TechEffectKind.ReloadMult, u.Def) : 1f);
            if (pad.Owner == u.Owner && p != null && p.LowPower) per *= 2f;
            if (u.RearmTimer >= per)
            {
                u.RearmTimer = 0;
                if (u.Ammo < u.Def.Ammo) u.Ammo = Math.Min(u.Def.Ammo, u.Ammo + Math.Max(1, u.Def.Ammo / 6));
                if (u.Hp < u.MaxHp) u.Hp = Math.Min(u.MaxHp, u.Hp + u.MaxHp * 0.04f);
            }
            if (u.CurrentOrder.Type == OrderType.Rearm && u.Ammo >= u.Def.Ammo) Complete(u);
        }

        // ------------------------------------------------------------------ Passive abilities (every 8 ticks)
        void Passive(Unit u)
        {
            float dt = World.Dt * 8;
            if (u.Def.HealRate <= 0 || u.Disabled) return;
            bool inf = u.Def.Has(UnitAbility.HealInfantry);
            bool veh = u.Def.Has(UnitAbility.RepairVehicles);
            bool nav = u.Def.Has(UnitAbility.RepairNaval);
            _w.Spatial.UnitsNear(u.Pos, 3f, _near);
            Unit best = null; float worst = 1f;
            foreach (var o in _near)
            {
                if (o == u || !_w.IsAlly(o.Owner, u.Owner) || o.Hp >= o.MaxHp || o.IsInside) continue;
                bool ok = (inf && o.Def.Category == UnitCategory.Infantry) || (veh && o.Def.Category == UnitCategory.Vehicle) || (nav && o.Def.Category == UnitCategory.Naval);
                if (!ok) continue;
                if (o.HpRatio < worst) { worst = o.HpRatio; best = o; }
            }
            if (best != null)
            {
                best.Hp = Math.Min(best.MaxHp, best.Hp + u.Def.HealRate * dt);
                if ((_w.Tick / 8) % 4 == 0) _w.Emit(new SimEvent { Type = SimEventType.Healed, Player = u.Owner, EntityId = best.Id, OtherId = u.Id, Pos = best.Pos });
            }
        }
    }

    public static class UnitExtensions
    {
        public static string DeploysIntoDef(this Unit u) => u.Def.DeploysInto;
    }
}
