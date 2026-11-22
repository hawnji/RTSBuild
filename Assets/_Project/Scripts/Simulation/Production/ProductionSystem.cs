using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// Sidebar production queues (structures, defenses, infantry, vehicles, aircraft, naval, research), prerequisite
    /// and technology checks, pay-as-you-build costs, power-dependent speed, cancellation refunds, hold, structure
    /// placement validation and unit delivery to rally points.
    /// </summary>
    public class ProductionSystem
    {
        readonly World _w;
        public const int MaxQueue = 10;

        public ProductionSystem(World w) { _w = w; }

        public static BuildingRole[] Family(BuildingRole role)
        {
            switch (role)
            {
                case BuildingRole.Barracks: return new[] { BuildingRole.Barracks, BuildingRole.AdvancedBarracks };
                case BuildingRole.Factory: return new[] { BuildingRole.Factory, BuildingRole.AdvancedFactory };
                case BuildingRole.Hangar: return new[] { BuildingRole.Hangar, BuildingRole.AdvancedHangar };
                case BuildingRole.Shipyard: return new[] { BuildingRole.Shipyard, BuildingRole.AdvancedShipyard };
                default: return new[] { role };
            }
        }

        public static ProductionQueueType QueueOfBuilding(BuildingDef b) => b.Queue;

        public bool HasPrereqs(int player, List<BuildingRole> roles)
        {
            foreach (var r in roles)
            {
                bool ok = false;
                foreach (var fr in Family(r)) if (_w.HasRole(player, fr, true)) { ok = true; break; }
                // Advanced roles are only satisfied by the advanced building itself.
                if (r == BuildingRole.AdvancedBarracks || r == BuildingRole.AdvancedFactory || r == BuildingRole.AdvancedHangar || r == BuildingRole.AdvancedShipyard)
                    ok = _w.HasRole(player, r, true);
                if (!ok) return false;
            }
            return true;
        }

        public bool HasTechs(Player p, List<string> techs)
        {
            foreach (var t in techs) if (!p.HasTech(t)) return false;
            return true;
        }

        // ------------------------------------------------------------------ Availability
        public bool CanBuildUnit(Player p, UnitDef u)
        {
            if (u == null || u.Faction != p.Faction) return false;
            if (!HasPrereqs(p.Id, u.Prerequisites)) return false;
            if (!HasTechs(p, u.RequiredTech)) return false;
            return true;
        }

        public bool CanBuildStructure(Player p, BuildingDef b)
        {
            if (b == null || b.Faction != p.Faction || !b.Buildable) return false;
            if (!_w.Settings.Superweapons && (b.Role == BuildingRole.Superweapon || b.Role == BuildingRole.SpecialPower)) return false;
            if (!_w.HasRole(p.Id, BuildingRole.ConstructionYard, true)) return false;
            if (!HasPrereqs(p.Id, b.Prerequisites)) return false;
            if (!HasTechs(p, b.RequiredTech)) return false;
            if (b.OnWater && !MapHasWater()) return false;
            return true;
        }

        bool? _mapWater;
        bool MapHasWater()
        {
            if (_mapWater.HasValue) return _mapWater.Value;
            int n = 0;
            foreach (var c in _w.Map.Cells) if (c == Terrain.Water) n++;
            _mapWater = n > 60;
            return _mapWater.Value;
        }

        public bool CanResearch(Player p, TechDef t)
        {
            if (t == null || t.Faction != p.Faction || p.HasTech(t.Id)) return false;
            foreach (var it in p.Queues[(int)ProductionQueueType.Research]) if (it.DefId == t.Id) return false;
            if (!HasPrereqs(p.Id, t.Prerequisites)) return false;
            if (!HasTechs(p, t.RequiredTech)) return false;
            return true;
        }

        public int CostOf(Player p, string defId, bool tech)
        {
            if (tech) return Catalog.Tech(defId)?.Cost ?? 0;
            var u = Catalog.Unit(defId);
            if (u != null) return (int)(u.Cost * p.Mods.UnitMult(TechEffectKind.CostMult, u));
            return Catalog.Building(defId)?.Cost ?? 0;
        }

        float BuildTimeOf(string defId, bool tech)
        {
            if (tech) return Catalog.Tech(defId)?.ResearchTime ?? 30;
            var u = Catalog.Unit(defId);
            if (u != null) return u.BuildTime;
            return Catalog.Building(defId)?.BuildTime ?? 10;
        }

        public ProductionQueueType QueueFor(string defId, bool tech)
        {
            if (tech) return ProductionQueueType.Research;
            var u = Catalog.Unit(defId);
            if (u != null) return Catalog.QueueFor(u);
            return Catalog.Building(defId).Queue;
        }

        // ------------------------------------------------------------------ Queue manipulation
        public bool Enqueue(int playerId, string defId, bool tech = false, int count = 1)
        {
            var p = _w.GetPlayer(playerId);
            if (p == null || p.Defeated) return false;
            bool ok;
            if (tech) ok = CanResearch(p, Catalog.Tech(defId));
            else if (Catalog.Unit(defId) != null) ok = CanBuildUnit(p, Catalog.Unit(defId));
            else ok = CanBuildStructure(p, Catalog.Building(defId));
            if (!ok) return false;
            var q = p.Queues[(int)QueueFor(defId, tech)];
            int total = 0; foreach (var it in q) total += it.Count;
            if (total >= MaxQueue) return false;
            var last = q.Count > 0 ? q[q.Count - 1] : null;
            if (!tech && last != null && last.DefId == defId && Catalog.Unit(defId) != null && q.Count > 1)
            {
                last.Count = Math.Min(MaxQueue, last.Count + count);
                return true;
            }
            if (!tech && q.Count == 1 && q[0].DefId == defId && Catalog.Unit(defId) != null)
            {
                q[0].Count += count;
                return true;
            }
            q.Add(new ProductionItem { DefId = defId, IsTech = tech, Count = count });
            return true;
        }

        public void Cancel(int playerId, ProductionQueueType qt, string defId)
        {
            var p = _w.GetPlayer(playerId);
            if (p == null) return;
            if ((qt == ProductionQueueType.Structures || qt == ProductionQueueType.Defenses) && p.Ready[(int)qt] == defId)
            {
                // Cancelling a finished, unplaced structure refunds it fully.
                p.Ready[(int)qt] = null;
                p.Credits += CostOf(p, defId, false);
                var q0 = p.Queues[(int)qt];
                if (q0.Count > 0 && q0[0].DefId == defId && q0[0].Progress >= 1f) q0.RemoveAt(0);
                return;
            }
            var q = p.Queues[(int)qt];
            for (int i = q.Count - 1; i >= 0; i--)
            {
                if (q[i].DefId != defId) continue;
                if (q[i].Count > 1) { q[i].Count--; return; }
                p.Credits += q[i].Paid;
                p.Stats.CreditsSpent -= q[i].Paid;
                q.RemoveAt(i);
                return;
            }
        }

        public void ToggleHold(int playerId, ProductionQueueType qt)
        {
            var p = _w.GetPlayer(playerId);
            if (p == null) return;
            var q = p.Queues[(int)qt];
            if (q.Count > 0) q[0].OnHold = !q[0].OnHold;
        }

        // ------------------------------------------------------------------ Update
        public void Update()
        {
            foreach (var p in _w.Players)
            {
                if (p.Defeated || p.IsNeutral) continue;
                for (int qi = 0; qi < p.Queues.Length; qi++) UpdateQueue(p, (ProductionQueueType)qi);
            }
        }

        public float SpeedMultiplier(Player p, ProductionQueueType qt, string defId)
        {
            float m = p.Mods.ProductionSpeed;
            int producers = 0;
            switch (qt)
            {
                case ProductionQueueType.Structures:
                case ProductionQueueType.Defenses: producers = _w.CountRole(p.Id, BuildingRole.ConstructionYard); break;
                case ProductionQueueType.Infantry: producers = _w.CountRole(p.Id, BuildingRole.Barracks) + _w.CountRole(p.Id, BuildingRole.AdvancedBarracks); break;
                case ProductionQueueType.Vehicles: producers = _w.CountRole(p.Id, BuildingRole.Factory) + _w.CountRole(p.Id, BuildingRole.AdvancedFactory); break;
                case ProductionQueueType.Aircraft: producers = _w.CountRole(p.Id, BuildingRole.Hangar) + _w.CountRole(p.Id, BuildingRole.AdvancedHangar); break;
                case ProductionQueueType.Naval: producers = _w.CountRole(p.Id, BuildingRole.Shipyard) + _w.CountRole(p.Id, BuildingRole.AdvancedShipyard); break;
                case ProductionQueueType.Research: producers = 1; break;
            }
            if (producers > 1) m *= Math.Min(2.5f, 1f + 0.4f * (producers - 1));
            if (p.LowPower) m *= Math.Max(0.25f, p.PowerRatio);
            return m;
        }

        void UpdateQueue(Player p, ProductionQueueType qt)
        {
            var q = p.Queues[(int)qt];
            if (q.Count == 0) return;
            var item = q[0];
            if (item.OnHold) return;
            if ((qt == ProductionQueueType.Structures || qt == ProductionQueueType.Defenses) && p.Ready[(int)qt] != null) return;
            // Validate the producer still exists.
            if (!ProducerAvailable(p, item))
            {
                return; // paused until a producer exists again
            }
            int cost = CostOf(p, item.DefId, item.IsTech);
            float time = Math.Max(1f, BuildTimeOf(item.DefId, item.IsTech));
            float dp = World.Dt / time * SpeedMultiplier(p, qt, item.DefId);
            if (item.Progress + dp > 1f) dp = 1f - item.Progress;
            float pay = cost * dp;
            if (pay > p.Credits)
            {
                if (p.IsHuman && _w.Time - p.FundsCooldown > 10f) { p.FundsCooldown = _w.Time; _w.Emit(new SimEvent { Type = SimEventType.InsufficientFunds, Player = p.Id }); }
                return;
            }
            p.Credits -= pay;
            p.Stats.CreditsSpent += pay;
            item.Paid += pay;
            item.Progress += dp;
            if (item.Progress < 0.9999f) return;
            // Completed
            if (item.IsTech)
            {
                p.Researched.Add(item.DefId);
                ApplyResearch(p, item.DefId);
                q.RemoveAt(0);
                _w.Emit(new SimEvent { Type = SimEventType.ResearchComplete, Player = p.Id, Text = item.DefId });
                return;
            }
            var ud = Catalog.Unit(item.DefId);
            if (ud != null)
            {
                if (DeliverUnit(p, ud))
                {
                    item.Count--;
                    item.Progress = 0; item.Paid = 0;
                    if (item.Count <= 0) q.RemoveAt(0);
                }
                else item.Progress = 0.9999f; // wait for a free exit
                return;
            }
            p.Ready[(int)qt] = item.DefId;
            q.RemoveAt(0);
            _w.Emit(new SimEvent { Type = SimEventType.ConstructionComplete, Player = p.Id, Text = item.DefId });
        }

        void ApplyResearch(Player p, string techId)
        {
            var oldMods = new Dictionary<int, float>();
            foreach (var u in _w.UnitsOf(p.Id)) oldMods[u.Id] = p.Mods.UnitMult(TechEffectKind.HealthMult, u.Def);
            var oldB = new Dictionary<int, float>();
            foreach (var b in _w.BuildingsOf(p.Id)) oldB[b.Id] = p.Mods.BuildingHealthMult(b.Def);
            p.Mods.Recompute(p);
            foreach (var u in _w.UnitsOf(p.Id))
            {
                float nm = p.Mods.UnitMult(TechEffectKind.HealthMult, u.Def);
                if (oldMods.TryGetValue(u.Id, out var om) && Math.Abs(nm - om) > 0.001f) { float r = nm / om; u.MaxHp *= r; u.Hp *= r; }
            }
            foreach (var b in _w.BuildingsOf(p.Id))
            {
                float nm = p.Mods.BuildingHealthMult(b.Def);
                if (oldB.TryGetValue(b.Id, out var om) && Math.Abs(nm - om) > 0.001f) { float r = nm / om; b.MaxHp *= r; b.Hp *= r; }
            }
        }

        bool ProducerAvailable(Player p, ProductionItem item)
        {
            if (item.IsTech) return _w.HasRole(p.Id, Catalog.Tech(item.DefId).ResearchedAt, true);
            var ud = Catalog.Unit(item.DefId);
            if (ud != null) return FindProducer(p, ud) != null;
            return _w.HasRole(p.Id, BuildingRole.ConstructionYard, true);
        }

        Building FindProducer(Player p, UnitDef ud)
        {
            var roles = IsAdvancedRole(ud.ProducedAt) ? new[] { ud.ProducedAt } : Family(ud.ProducedAt);
            Building best = null;
            foreach (var b in _w.Buildings)
            {
                if (b.Dead || b.Owner != p.Id || !b.Operational) continue;
                if (Array.IndexOf(roles, b.Def.Role) < 0) continue;
                if (best == null || b.HasRally && !best.HasRally) best = b;
            }
            return best;
        }

        static bool IsAdvancedRole(BuildingRole r) =>
            r == BuildingRole.AdvancedBarracks || r == BuildingRole.AdvancedFactory || r == BuildingRole.AdvancedHangar || r == BuildingRole.AdvancedShipyard;

        bool DeliverUnit(Player p, UnitDef ud)
        {
            var b = FindProducer(p, ud);
            if (b == null) return false;
            var mc = ud.Locomotor == Locomotor.Foot ? MoveClass.Foot : (ud.Locomotor == Locomotor.Naval || ud.Locomotor == Locomotor.Submarine) ? MoveClass.Naval :
                (ud.Locomotor == Locomotor.Air || ud.Locomotor == Locomotor.Helicopter) ? MoveClass.Air : MoveClass.Vehicle;
            Vec2 spawn;
            Unit u;
            if (mc == MoveClass.Air)
            {
                int pad = -1;
                for (int i = 0; i < b.Pads.Length; i++) if (b.Pads[i] == 0 || _w.GetUnit(b.Pads[i]) == null) { pad = i; break; }
                spawn = pad >= 0 ? _w.Behavior.PadPosition(b, pad) : b.Center;
                u = _w.SpawnUnit(ud.Id, p.Id, spawn, SMath.PI / 2);
                if (pad >= 0) { b.Pads[pad] = u.Id; u.PadBuildingId = b.Id; u.PadIndex = pad; u.AState = AirState.Docked; u.Height = 0; }
                else { u.AState = AirState.Flying; }
            }
            else
            {
                Int2? cell;
                if (mc == MoveClass.Naval) cell = _w.FindFreeCellNear(b.Center, MoveClass.Naval, Math.Max(b.W, b.H) + 3);
                else
                {
                    var exit = b.ExitCell;
                    cell = _w.Passable(exit.X, exit.Y, mc, p.Team) ? exit : _w.FindFreeCellNear(exit.Center, mc, 4, p.Team);
                }
                if (!cell.HasValue) return false;
                spawn = cell.Value.Center;
                u = _w.SpawnUnit(ud.Id, p.Id, spawn, -SMath.PI / 2);
            }
            b.ProduceFlash = 1.5f;
            _w.Emit(new SimEvent { Type = SimEventType.UnitReady, Player = p.Id, EntityId = u.Id, Pos = u.Pos, Text = ud.Id });
            if (ud.Has(UnitAbility.Harvester)) { _w.Commands.Harvest(new List<int> { u.Id }, p.Id, Vec2.Zero); }
            else if (b.HasRally && mc != MoveClass.Air)
                _w.Behavior.SetOrder(u, new Order(u.IsArmed ? OrderType.AttackMove : OrderType.Move, b.Rally), false);
            else if (mc != MoveClass.Air)
            {
                // Clear the exit
                var off = new Vec2(_w.Rng.Range(-1.5f, 1.5f), -1.8f);
                _w.Behavior.SetOrder(u, new Order(OrderType.Move, u.Pos + off), false);
            }
            return true;
        }

        // ------------------------------------------------------------------ Placement
        public bool CanPlace(int playerId, BuildingDef def, Int2 origin, bool rotated, bool requireAdjacency = true) =>
            CanPlaceIgnoringUnit(playerId, def, origin, rotated, 0, requireAdjacency);

        public bool CanPlaceIgnoringUnit(int playerId, BuildingDef def, Int2 origin, bool rotated, int ignoreUnit, bool requireAdjacency = true)
        {
            int w = rotated && def.Rotatable ? def.Height : def.Width;
            int h = rotated && def.Rotatable ? def.Width : def.Height;
            var m = _w.Map;
            int team = _w.TeamOf(playerId);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int cx = origin.X + x, cy = origin.Y + y;
                    if (!m.InBounds(cx, cy) || cx <= 0 || cy <= 0 || cx >= m.Width - 1 || cy >= m.Height - 1) return false;
                    var t = m.Get(cx, cy);
                    if (def.OnWater) { if (t != Terrain.Water) return false; }
                    else if (t == Terrain.Water || t == Terrain.Cliff || t == Terrain.Mountain || t == Terrain.Forest || t == Terrain.Bridge || t == Terrain.Shallow) return false;
                    if (_w.BuildingGrid[m.Index(cx, cy)] != 0 && _w.GetBuilding(_w.BuildingGrid[m.Index(cx, cy)]) != null) return false;
                    // Cannot place on top of enemy units; friendly units are pushed aside.
                    foreach (var u in _w.Spatial.UnitsNear(new Vec2(cx + 0.5f, cy + 0.5f), 0.7f, _w))
                        if (u.Id != ignoreUnit && !u.IsAir && !_w.IsAlly(u.Owner, playerId)) return false;
                }
            // Plateau consistency: footprint must be on one elevation level.
            if (!def.OnWater)
            {
                int lvl = m.LevelAt(origin.X, origin.Y);
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (m.LevelAt(origin.X + x, origin.Y + y) != lvl) return false;
            }
            if (!requireAdjacency) return true;
            // Must be near an existing friendly structure.
            float reach = def.Role == BuildingRole.Wall || def.Role == BuildingRole.Gate ? 6f : def.OnWater ? 9f : 5.5f;
            var center = new Vec2(origin.X + w * 0.5f, origin.Y + h * 0.5f);
            foreach (var b in _w.Buildings)
            {
                if (b.Dead || b.Owner != playerId) continue;
                if (b.IsWallLike && def.Role != BuildingRole.Wall && def.Role != BuildingRole.Gate) continue;
                if (b.DistanceTo(center) - Math.Max(w, h) * 0.5f <= reach) return true;
            }
            return false;
        }

        public Building Place(int playerId, ProductionQueueType qt, Int2 origin, bool rotated)
        {
            var p = _w.GetPlayer(playerId);
            if (p == null) return null;
            string defId = p.Ready[(int)qt];
            if (defId == null) return null;
            var def = Catalog.Building(defId);
            if (!CanPlace(playerId, def, origin, rotated))
            {
                _w.Emit(new SimEvent { Type = SimEventType.CannotPlace, Player = playerId, Pos = origin.Center });
                return null;
            }
            p.Ready[(int)qt] = null;
            var b = _w.SpawnBuilding(defId, playerId, origin, false, rotated && def.Rotatable);
            // Push friendly units out of the footprint
            foreach (var u in _w.Units)
            {
                if (u.Dead || u.IsAir || u.IsInside) continue;
                if (b.Covers((int)u.Pos.X, (int)u.Pos.Y))
                {
                    var c = _w.FindFreeCellNear(u.Pos, u.MoveClass, 4, p.Team);
                    if (c.HasValue) _w.Movement.RequestMove(u, c.Value.Center);
                }
            }
            if (def.Role == BuildingRole.Refinery)
            {
                var hv = Catalog.Faction(p.Faction).HarvesterUnit;
                var dock = _w.Economy.DockPosition(b);
                var cell = _w.FindFreeCellNear(dock, MoveClass.Vehicle, 4, p.Team);
                if (cell.HasValue)
                {
                    var h = _w.SpawnUnit(hv, playerId, cell.Value.Center);
                    _w.Commands.Harvest(new List<int> { h.Id }, playerId, Vec2.Zero);
                }
            }
            return b;
        }

        /// <summary>Finds a valid placement near a point (used by AI and by MCV auto-deploy).</summary>
        public Int2? FindPlacement(int playerId, BuildingDef def, Vec2 near, int maxRadius, SimRandom rng, int spacing = 1)
        {
            var c = Int2.Floor(near);
            for (int r = 0; r <= maxRadius; r++)
            {
                var candidates = new List<Int2>();
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var o = new Int2(c.X + dx - def.Width / 2, c.Y + dy - def.Height / 2);
                        if (!CanPlace(playerId, def, o, false)) continue;
                        if (spacing > 0 && !def.OnWater && !HasClearance(o, def.Width, def.Height, spacing)) continue;
                        candidates.Add(o);
                    }
                if (candidates.Count > 0) return candidates[rng.Range(0, candidates.Count)];
            }
            return null;
        }

        /// <summary>Keeps a walkable ring around AI structures so bases do not wall themselves in.</summary>
        bool HasClearance(Int2 o, int w, int h, int spacing)
        {
            for (int y = -spacing; y < h + spacing; y++)
                for (int x = -spacing; x < w + spacing; x++)
                {
                    if (x >= 0 && y >= 0 && x < w && y < h) continue;
                    int cx = o.X + x, cy = o.Y + y;
                    if (!_w.Map.InBounds(cx, cy)) return false;
                    if (_w.BuildingGrid[_w.Map.Index(cx, cy)] != 0) return false;
                }
            return true;
        }
    }
}
