using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// Ore harvesting and processing, credit storage limits, electricity production/consumption and its effects,
    /// structure repair and selling, repair depots, ore regrowth and captured tech-structure bonuses.
    /// </summary>
    public class EconomySystem
    {
        readonly World _w;
        readonly List<Unit> _near = new List<Unit>();

        public EconomySystem(World w) { _w = w; }

        public void RecalculatePlayer(int playerId)
        {
            var p = _w.GetPlayer(playerId);
            if (p == null) return;
            bool wasLow = p.LowPower;
            int prod = 0, cons = 0, storage = 0;
            bool radar = false;
            foreach (var b in _w.Buildings)
            {
                if (b.Dead || b.Owner != playerId) continue;
                if (b.Def.Power > 0)
                {
                    if (b.BuildUp >= 1f && !b.Disabled) prod += (int)(b.Def.Power * SMath.Clamp(0.25f + b.HpRatio * 0.75f, 0, 1));
                }
                else cons -= b.Def.Power;
                storage += b.Def.Storage;
                if (b.Def.Role == BuildingRole.Radar && b.BuildUp >= 1f) radar = true;
            }
            p.PowerProduced = prod;
            p.PowerConsumed = cons;
            p.StorageCap = storage;
            p.HasRadar = radar && !p.LowPower;
            if (!p.IsNeutral && p.LowPower && !wasLow && _w.Time - p.LowPowerCooldown > 6f)
            {
                p.LowPowerCooldown = _w.Time;
                _w.Emit(new SimEvent { Type = SimEventType.LowPower, Player = playerId });
            }
            else if (!p.LowPower && wasLow)
                _w.Emit(new SimEvent { Type = SimEventType.PowerRestored, Player = playerId });
        }

        public void AddCredits(Player p, float amount, bool harvested)
        {
            if (amount <= 0) return;
            // Credits above storage capacity are lost: build silos.
            float cap = Math.Max(p.StorageCap, 0);
            if (p.IsHuman || p.IsAI)
            {
                if (harvested && p.Credits + amount > cap)
                {
                    amount = Math.Max(0, cap - p.Credits);
                    if (_w.Time - p.SiloCooldown > 20f)
                    {
                        p.SiloCooldown = _w.Time;
                        _w.Emit(new SimEvent { Type = SimEventType.SilosNeeded, Player = p.Id });
                    }
                }
            }
            p.Credits += amount;
            if (harvested) p.Stats.CreditsHarvested += amount;
        }

        public void Update()
        {
            // Power and storage are recomputed twice a second (damage changes plant output).
            if (_w.Tick % 8 == 0) foreach (var p in _w.Players) RecalculatePlayer(p.Id);

            float dt = World.Dt;
            foreach (var b in _w.Buildings)
            {
                if (b.Dead) continue;
                if (b.BuildUp < 1f)
                {
                    b.BuildUp = Math.Min(1f, b.BuildUp + dt / 2.5f);
                    if (b.BuildUp >= 1f) { RecalculatePlayer(b.Owner); _w.Powers.OnBuildingAdded(b); }
                    continue;
                }
                var owner = _w.GetPlayer(b.Owner);
                if (owner == null) continue;
                if (b.Repairing) RepairTick(b, owner, dt);
                if (b.Selling)
                {
                    b.SellTimer += dt;
                    if (b.SellTimer >= 1.2f) CompleteSale(b, owner);
                    continue;
                }
                if (b.ProduceFlash > 0) b.ProduceFlash -= dt;
                if (b.Def.Role == BuildingRole.Gate) UpdateGate(b);
                switch (b.Def.Role)
                {
                    case BuildingRole.RepairDepot: if (_w.Tick % 5 == 0) DepotTick(b, owner, dt * 5); break;
                    case BuildingRole.Shipyard:
                    case BuildingRole.AdvancedShipyard: if (_w.Tick % 10 == 0) ShipyardRepair(b, dt * 10); break;
                    case BuildingRole.TechStructure: TechStructureTick(b, owner, dt); break;
                }
            }

            if (_w.Settings.OreRegrowth && _w.Tick % (World.TicksPerSecond * 4) == 0) RegrowOre();
        }

        void UpdateGate(Building b)
        {
            if (_w.Tick % 5 != 0) return;
            int team = _w.TeamOf(b.Owner);
            bool friendNear = false;
            _w.Spatial.UnitsNear(b.Center, 1.6f, _near);
            foreach (var u in _near) if (_w.TeamOf(u.Owner) == team && !u.IsAir) { friendNear = true; break; }
            b.GateOpen = friendNear;
        }

        void RepairTick(Building b, Player p, float dt)
        {
            if (b.Hp >= b.MaxHp) { b.Repairing = false; return; }
            // Full repair costs 25% of the structure's price, over ~20 seconds per 1000 hp.
            float hpPerSec = Math.Max(40f, b.MaxHp / 20f);
            float hp = hpPerSec * dt;
            float cost = b.Def.Cost * 0.25f * (hp / b.MaxHp);
            if (p.Credits < cost) { if (p.IsHuman && _w.Time - p.FundsCooldown > 8) { p.FundsCooldown = _w.Time; _w.Emit(new SimEvent { Type = SimEventType.InsufficientFunds, Player = p.Id }); } return; }
            p.Credits -= cost;
            p.Stats.CreditsSpent += cost;
            b.Hp = Math.Min(b.MaxHp, b.Hp + hp);
        }

        public void StartSale(Building b)
        {
            if (b.Selling || b.Dead) return;
            b.Selling = true;
            b.SellTimer = 0;
            _w.Emit(new SimEvent { Type = SimEventType.Sold, Player = b.Owner, EntityId = b.Id, Pos = b.Center, Text = b.DefId });
        }

        void CompleteSale(Building b, Player p)
        {
            float refund = b.Def.Cost * 0.5f * b.HpRatio;
            p.Credits += refund; // refunds ignore silo cap
            bool military = b.Def.Cost >= 500 && !b.IsDefense && !b.IsWallLike;
            var center = b.Center;
            b.Dead = true;
            // Clear footprint (handled like destruction but without rubble or loss stats).
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                {
                    int cx = b.Origin.X + x, cy = b.Origin.Y + y;
                    if (_w.Map.InBounds(cx, cy) && _w.BuildingGrid[_w.Map.Index(cx, cy)] == b.Id) _w.BuildingGrid[_w.Map.Index(cx, cy)] = 0;
                }
            _w.Spatial.RemoveBuilding(b);
            _w.Pathfinder.InvalidateArea(b.Origin, b.W, b.H);
            foreach (var padId in b.Pads) { var a = _w.GetUnit(padId); if (a != null) { a.AState = AirState.TakingOff; a.PadBuildingId = 0; a.PadIndex = -1; } }
            if (military && !p.IsNeutral)
            {
                string inf = p.Faction == FactionId.Alliance ? "ca_rifleman" : "id_levy";
                int n = Math.Max(1, Math.Min(3, b.Def.Cost / 800));
                for (int i = 0; i < n; i++)
                {
                    var c = _w.FindFreeCellNear(center, MoveClass.Foot, 3);
                    if (c.HasValue) { _w.SpawnUnit(inf, p.Id, c.Value.Center); p.Stats.UnitsBuilt--; }
                }
            }
            RecalculatePlayer(p.Id);
            _w.Powers.RefreshOwnership(p.Id);
        }

        void DepotTick(Building b, Player p, float dt)
        {
            _w.Spatial.UnitsNear(b.Center, 1.6f, _near);
            foreach (var u in _near)
            {
                if (!_w.IsAlly(u.Owner, b.Owner) || u.Def.Category != UnitCategory.Vehicle || u.Hp >= u.MaxHp || u.Moving) continue;
                float hp = u.MaxHp * 0.06f * dt;
                float cost = u.Def.Cost * 0.2f * (hp / u.MaxHp);
                if (p.Credits < cost) continue;
                p.Credits -= cost; p.Stats.CreditsSpent += cost;
                u.Hp = Math.Min(u.MaxHp, u.Hp + hp);
                _w.Emit(new SimEvent { Type = SimEventType.Repairing, Player = u.Owner, EntityId = u.Id, Pos = u.Pos });
                break; // one at a time
            }
        }

        void ShipyardRepair(Building b, float dt)
        {
            _w.Spatial.UnitsNear(b.Center, 3.5f, _near);
            foreach (var u in _near)
                if (_w.IsAlly(u.Owner, b.Owner) && u.Def.Category == UnitCategory.Naval && u.Hp < u.MaxHp)
                    u.Hp = Math.Min(u.MaxHp, u.Hp + u.MaxHp * 0.02f * dt);
        }

        void TechStructureTick(Building b, Player p, float dt)
        {
            if (p.IsNeutral) return;
            switch (b.DefId)
            {
                case "tech_derrick":
                    b.IncomeTimer += dt;
                    if (b.IncomeTimer >= 5f) { b.IncomeTimer = 0; p.Credits += 75; p.Stats.CreditsHarvested += 75; }
                    break;
                case "tech_hospital":
                    if (_w.Tick % 15 == 0)
                    {
                        _w.Spatial.UnitsNear(b.Center, 6f, _near);
                        foreach (var u in _near)
                            if (_w.IsAlly(u.Owner, b.Owner) && u.Def.Category == UnitCategory.Infantry) u.Hp = Math.Min(u.MaxHp, u.Hp + 5);
                    }
                    break;
            }
        }

        void RegrowOre()
        {
            var m = _w.Map;
            foreach (var v in m.OreVents)
            {
                if (!m.InBounds(v.X, v.Y)) continue;
                for (int k = 0; k < 2; k++)
                {
                    int x = v.X + _w.Rng.Range(-3, 4), y = v.Y + _w.Rng.Range(-3, 4);
                    if (!m.InBounds(x, y)) continue;
                    var t = m.Get(x, y);
                    if (!MapData.IsLandPassable(t) || t == Terrain.Road || t == Terrain.Bridge || t == Terrain.Forest) continue;
                    if (_w.BuildingGrid[m.Index(x, y)] != 0) continue;
                    int i = m.Index(x, y);
                    if (m.Ore[i] < MapData.MaxOrePerCell) m.Ore[i] = Math.Min(MapData.MaxOrePerCell, m.Ore[i] + 60);
                    m.OreKind[i] = m.OreKind[m.Index(v.X, v.Y)];
                }
            }
        }

        // ------------------------------------------------------------------ Harvesters
        public void UpdateHarvester(Unit u)
        {
            var owner = _w.GetPlayer(u.Owner);
            if (owner == null) return;
            switch (u.HState)
            {
                case HarvestState.Idle:
                    return;
                case HarvestState.Seeking:
                    {
                        if (u.Cargo >= u.Def.HarvestCapacity) { u.HState = HarvestState.Returning; return; }
                        if ((_w.Tick + u.Id) % 5 != 0) return;
                        var from = u.HasLastHarvest ? u.LastHarvestPos : u.Pos;
                        var cell = FindOre(from, u.Owner, 14);
                        if (!cell.HasValue && u.HasLastHarvest) cell = FindOre(u.Pos, u.Owner, 14);
                        if (!cell.HasValue) cell = FindOre(u.Pos, u.Owner, 60);
                        if (!cell.HasValue)
                        {
                            if (u.Cargo > 0) u.HState = HarvestState.Returning;
                            else u.HState = HarvestState.Idle;
                            return;
                        }
                        u.LastHarvestPos = cell.Value.Center; u.HasLastHarvest = true;
                        _w.Movement.RequestMove(u, cell.Value.Center);
                        u.HState = HarvestState.Moving;
                    }
                    break;
                case HarvestState.Moving:
                    {
                        var c = Int2.Floor(u.Pos);
                        if (_w.Map.HasOre(c.X, c.Y) && (Vec2.Distance(u.Pos, u.LastHarvestPos) < 1.2f || !u.Moving))
                        { _w.Movement.Stop(u); u.HState = HarvestState.Harvesting; return; }
                        if (!u.Moving)
                        {
                            if (_w.Map.HasOre(c.X, c.Y)) u.HState = HarvestState.Harvesting;
                            else u.HState = HarvestState.Seeking;
                        }
                    }
                    break;
                case HarvestState.Harvesting:
                    {
                        var c = Int2.Floor(u.Pos);
                        int i = _w.Map.Index(c.X, c.Y);
                        if (_w.Map.Ore[i] <= 0)
                        {
                            // Move to an adjacent ore cell
                            var next = FindOre(u.Pos, u.Owner, 3);
                            if (next.HasValue && u.Cargo < u.Def.HarvestCapacity) { u.LastHarvestPos = next.Value.Center; _w.Movement.RequestMove(u, next.Value.Center); u.HState = HarvestState.Moving; }
                            else u.HState = u.Cargo > 0 ? HarvestState.Returning : HarvestState.Seeking;
                            return;
                        }
                        float rate = u.Def.HarvestRate * owner.Mods.HarvestRate * World.Dt;
                        int take = (int)Math.Ceiling(Math.Min(rate, Math.Min(_w.Map.Ore[i], u.Def.HarvestCapacity - u.Cargo)));
                        if (take <= 0) { u.HState = HarvestState.Returning; return; }
                        _w.Map.Ore[i] -= take;
                        u.Cargo += take;
                        u.CargoValue += take * (_w.Map.OreKind[i] == 1 ? 2f : 1f);
                        u.LastHarvestPos = u.Pos;
                        if (u.Cargo >= u.Def.HarvestCapacity) u.HState = HarvestState.Returning;
                    }
                    break;
                case HarvestState.Returning:
                    {
                        var r = NearestRefinery(u);
                        if (r == null) { if (!u.Moving) u.HState = HarvestState.Idle; return; }
                        u.DockId = r.Id;
                        var dock = DockPosition(r);
                        _w.Movement.RequestMove(u, dock);
                        u.HState = HarvestState.Docking;
                    }
                    break;
                case HarvestState.Docking:
                    {
                        var r = _w.GetBuilding(u.DockId);
                        if (r == null) { u.HState = HarvestState.Returning; return; }
                        var dock = DockPosition(r);
                        float d = Vec2.Distance(u.Pos, dock);
                        if (d < 0.9f || (!u.Moving && d < 2.2f))
                        {
                            var other = _w.GetUnit(r.UnloadingHarvester);
                            if (other != null && other != u && other.HState == HarvestState.Unloading) { _w.Movement.Stop(u); return; } // wait in line
                            r.UnloadingHarvester = u.Id;
                            _w.Movement.Stop(u);
                            u.HState = HarvestState.Unloading;
                            u.UnloadTimer = 0;
                            u.Facing = SMath.PI / 2;
                        }
                        else if (!u.Moving) _w.Movement.RequestMove(u, dock);
                    }
                    break;
                case HarvestState.Unloading:
                    {
                        var r = _w.GetBuilding(u.DockId);
                        if (r == null) { u.HState = HarvestState.Returning; return; }
                        u.UnloadTimer += World.Dt;
                        // Transfer cargo over ~3 seconds.
                        float portion = u.Def.HarvestCapacity / 3f * World.Dt;
                        float moved = Math.Min(portion, u.Cargo);
                        float value = u.Cargo > 0 ? u.CargoValue * (moved / u.Cargo) : 0;
                        u.Cargo -= moved; u.CargoValue -= value;
                        AddCredits(owner, value, true);
                        if (u.Cargo <= 0.01f)
                        {
                            u.Cargo = 0; u.CargoValue = 0;
                            r.UnloadingHarvester = 0;
                            _w.Emit(new SimEvent { Type = SimEventType.OreDelivered, Player = u.Owner, EntityId = r.Id, Pos = r.Center });
                            u.HState = HarvestState.Seeking;
                        }
                    }
                    break;
            }
        }

        public Vec2 DockPosition(Building refinery) => new Vec2(refinery.Origin.X + refinery.W * 0.5f, refinery.Origin.Y - 0.6f);

        public Building NearestRefinery(Unit u)
        {
            Building best = null; float bd = float.MaxValue;
            foreach (var b in _w.Buildings)
            {
                if (b.Dead || b.Owner != u.Owner || b.Def.Role != BuildingRole.Refinery || b.BuildUp < 1f) continue;
                float d = Vec2.Distance(b.Center, u.Pos);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        /// <summary>Nearest harvestable ore cell; avoids cells occupied by other harvesters when possible.</summary>
        public Int2? FindOre(Vec2 from, int owner, int maxRadius)
        {
            var m = _w.Map;
            var c = Int2.Floor(from);
            Int2? fallback = null;
            for (int r = 0; r <= maxRadius; r++)
            {
                Int2? best = null; float bd = float.MaxValue;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = c.X + dx, y = c.Y + dy;
                        if (!m.InBounds(x, y) || m.Ore[m.Index(x, y)] <= 0) continue;
                        if (!_w.Passable(x, y, MoveClass.Vehicle)) continue;
                        float d = dx * dx + dy * dy;
                        if (HarvesterAt(x, y, owner)) { if (!fallback.HasValue) fallback = new Int2(x, y); continue; }
                        if (d < bd) { bd = d; best = new Int2(x, y); }
                    }
                if (best.HasValue) return best;
            }
            return fallback;
        }

        bool HarvesterAt(int x, int y, int owner)
        {
            foreach (var u in _w.Spatial.UnitsNear(new Vec2(x + 0.5f, y + 0.5f), 0.7f, _w))
                if (u.Def.Has(UnitAbility.Harvester) && u.HState == HarvestState.Harvesting) return true;
            return false;
        }
    }
}
