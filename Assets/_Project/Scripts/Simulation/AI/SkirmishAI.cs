using System;
using System.Collections.Generic;
using System.Linq;

namespace RTS.Sim
{
    /// <summary>
    /// Multi-layered computer opponent: intel (fog-limited), economy and base construction, research,
    /// counter-unit production, base defence, scouting, harassment, coordinated (and flanking) attacks,
    /// retreat and repair, support power usage, and 2v2 team coordination via <see cref="TeamBlackboard"/>.
    /// It acts only through <see cref="Commands"/>, exactly like a human player.
    /// </summary>
    public class SkirmishAI : IAIController
    {
        enum Role { Rally, Defense, Attack, Harass, Scout, Air, Naval, Retreat, Escort }

        readonly World _w;
        public int PlayerId { get; }
        public readonly AIProfile Profile;
        public readonly AIIntel Intel;
        readonly SimRandom _rng;
        readonly Dictionary<int, Role> _roles = new Dictionary<int, Role>();
        float _nextThink, _nextMilitary, _nextIntel, _nextScout, _nextAttack, _nextHarass, _nextPowers;
        Vec2 _baseCenter;
        bool _hasBase;
        Vec2 _rally;
        int _attackWaves;
        Vec2 _attackTarget;
        bool _attackActive;
        float _attackStarted;
        int _expansionMcvId;
        Vec2 _expansionSite;
        readonly List<Unit> _tmp = new List<Unit>();
        TeamBlackboard Board => TeamBlackboard.For(_w, Me.Team);

        Player Me => _w.GetPlayer(PlayerId);
        FactionId F => Me.Faction;

        public SkirmishAI(World w, int playerId)
        {
            _w = w;
            PlayerId = playerId;
            Profile = AIProfile.For(w.GetPlayer(playerId).Difficulty);
            Intel = new AIIntel(w, playerId);
            _rng = new SimRandom(w.Settings.Seed * 31 + playerId * 7919);
            float jitter = _rng.Range(0f, 1f);
            _nextThink = jitter; _nextMilitary = jitter + 0.5f;
            _nextAttack = Profile.FirstAttackTime * _rng.Range(0.9f, 1.15f);
            _nextScout = Profile.ScoutInterval > 0 ? 45f + _rng.Range(0, 30) : float.MaxValue;
            _nextHarass = Profile.FirstAttackTime * 0.7f;
        }

        /// <summary>One-line state summary for diagnostics (headless QA harness, debug overlay).</summary>
        public string DebugSummary()
        {
            var counts = new Dictionary<Role, int>();
            float rallyValue = 0;
            foreach (var kv in _roles)
            {
                counts.TryGetValue(kv.Value, out int n); counts[kv.Value] = n + 1;
                if (kv.Value == Role.Rally) { var u = _w.GetUnit(kv.Key); if (u != null) rallyValue += u.Def.Cost; }
            }
            float need = Profile.AttackMinValue + Profile.AttackValueGrowth * _attackWaves * Me.AIAggression;
            return $"waves {_attackWaves} active {_attackActive} next {_nextAttack:F0}s target {_attackTarget.X:F0},{_attackTarget.Y:F0} rally {rallyValue:F0}/{need:F0} credits {Me.Credits:F0} ref {Count(BuildingRole.Refinery)} ore18 {OreNearBase(18)} ore26 {OreNearBase(26)} roles " +
                   string.Join(" ", counts.Select(kv => kv.Key + ":" + kv.Value));
        }

        public void Update()
        {
            var me = Me;
            if (me == null || me.Defeated) return;
            float t = _w.Time;
            if (t >= _nextIntel) { _nextIntel = t + 1f; Intel.Update(); }
            if (t >= _nextThink)
            {
                _nextThink = t + Profile.ThinkInterval * _rng.Range(0.85f, 1.15f);
                if (!_rng.Chance(Profile.IdleChance))
                {
                    UpdateBase();
                    if (me.AIBuilds) BuildStructures();
                    PlaceReady();
                    Research();
                    ProduceUnits();
                    ManageEconomyUnits();
                }
            }
            if (t >= _nextMilitary)
            {
                _nextMilitary = t + Profile.MilitaryInterval;
                AssignNewUnits();
                Defend();
                if (me.AIAttacks) { Attack(); Harass(); Scout(); }
                ManageAir();
                ManageNaval();
                Retreat();
            }
            if (t >= _nextPowers) { _nextPowers = t + 3f; UsePowers(); }
        }

        // ================================================================== Base
        void UpdateBase()
        {
            Building yard = null;
            foreach (var b in _w.BuildingsOf(PlayerId))
                if (b.Def.Role == BuildingRole.ConstructionYard) { yard = b; break; }
            if (yard != null) { _baseCenter = yard.Center; _hasBase = true; }
            else
            {
                Building any = _w.BuildingsOf(PlayerId).FirstOrDefault(b => !b.IsWallLike);
                if (any != null) { _baseCenter = any.Center; _hasBase = true; }
                else { _baseCenter = Me.StartCell.Center; _hasBase = false; }
            }
            // Deploy MCVs
            foreach (var u in _w.UnitsOf(PlayerId).ToList())
            {
                if (!u.Def.Has(UnitAbility.DeployToHQ) || u.CurrentOrder.Type == OrderType.Deploy) continue;
                if (u.Id == _expansionMcvId)
                {
                    if (Vec2.Distance(u.Pos, _expansionSite) < 2.5f || !u.Moving) TryDeploy(u);
                    continue;
                }
                if (yard == null || _expansionMcvId == 0)
                {
                    if (yard != null && Profile.Expands && FindExpansionSite(out var site))
                    {
                        _expansionMcvId = u.Id; _expansionSite = site;
                        _w.Commands.Move(new[] { u.Id }, PlayerId, site);
                    }
                    else TryDeploy(u);
                }
            }
            var mcv = _w.GetUnit(_expansionMcvId);
            if (mcv == null) _expansionMcvId = 0;
            var dirToEnemy = (EnemyCenter() - _baseCenter).Normalized;
            _rally = _baseCenter + dirToEnemy * 7f;
            var rc = _w.FindFreeCellNear(_rally, MoveClass.Vehicle, 6, Me.Team);
            if (rc.HasValue) _rally = rc.Value.Center;
        }

        void TryDeploy(Unit u)
        {
            var def = Catalog.Building(u.Def.DeploysInto);
            var origin = new Int2((int)u.Pos.X - def.Width / 2, (int)u.Pos.Y - def.Height / 2);
            if (_w.Production.CanPlaceIgnoringUnit(PlayerId, def, origin, false, u.Id, false))
                _w.Commands.Deploy(new[] { u.Id }, PlayerId);
            else
            {
                var spot = FindDeploySpot(u.Pos, def, u.Id);
                if (spot.HasValue) _w.Commands.Move(new[] { u.Id }, PlayerId, spot.Value);
            }
        }

        Vec2? FindDeploySpot(Vec2 near, BuildingDef def, int unitId)
        {
            var c = Int2.Floor(near);
            for (int r = 1; r < 12; r++)
                for (int k = 0; k < 16; k++)
                {
                    var off = Vec2.FromAngle(k * SMath.TwoPI / 16) * r;
                    var p = near + off;
                    var o = new Int2((int)p.X - def.Width / 2, (int)p.Y - def.Height / 2);
                    if (_w.Production.CanPlaceIgnoringUnit(PlayerId, def, o, false, unitId, false)) return new Vec2(o.X + def.Width / 2 + 0.5f, o.Y + def.Height / 2 + 0.5f);
                }
            return null;
        }

        Vec2 EnemyCenter()
        {
            Vec2 best = new Vec2(_w.Map.Width / 2f, _w.Map.Height / 2f);
            float bd = float.MaxValue;
            for (int i = 0; i < _w.Map.StartPositions.Count; i++)
            {
                var sp = _w.Map.StartPositions[i].Center;
                bool enemy = false;
                foreach (var p in _w.Players)
                    if (!p.IsNeutral && !p.Defeated && _w.IsEnemy(PlayerId, p.Id) && Vec2.Distance(p.StartCell.Center, sp) < 4) enemy = true;
                if (!enemy) continue;
                float d = Vec2.Distance(sp, _baseCenter);
                if (d < bd) { bd = d; best = sp; }
            }
            var kb = Intel.BestTarget(_baseCenter, false);
            if (kb != null && Vec2.Distance(kb.Pos, _baseCenter) < bd) best = kb.Pos;
            return best;
        }

        // ================================================================== Construction
        int Count(BuildingRole r) => _w.CountRole(PlayerId, r);
        int CountQueuedOrReady(string defId)
        {
            int n = 0;
            foreach (var q in new[] { ProductionQueueType.Structures, ProductionQueueType.Defenses })
            {
                foreach (var it in Me.Queues[(int)q]) if (it.DefId == defId) n++;
                if (Me.Ready[(int)q] == defId) n++;
            }
            return n;
        }

        BuildingDef Def(BuildingRole r) => Catalog.BuildingForRole(F, r);

        void BuildStructures()
        {
            var me = Me;
            if (!_w.HasRole(PlayerId, BuildingRole.ConstructionYard, true)) return;
            if (me.Queues[(int)ProductionQueueType.Structures].Count == 0 && me.Ready[(int)ProductionQueueType.Structures] == null)
            {
                var next = NextStructure();
                if (next != null) _w.Commands.Enqueue(PlayerId, next.Id);
            }
            if (me.Queues[(int)ProductionQueueType.Defenses].Count == 0 && me.Ready[(int)ProductionQueueType.Defenses] == null)
            {
                var d = NextDefense();
                if (d != null) _w.Commands.Enqueue(PlayerId, d.Id);
            }
            // Repairs
            if (Profile.RepairsStructures && me.Credits > 400)
                foreach (var b in _w.BuildingsOf(PlayerId))
                    if (!b.Repairing && b.HpRatio < 0.75f && b.BuildUp >= 1f && _w.Time - b.LastDamagedTime > 3f) _w.Commands.ToggleRepair(b.Id, PlayerId);
        }

        bool CanBuild(BuildingDef d) => d != null && _w.Production.CanBuildStructure(Me, d) && d.Tier <= Profile.MaxTier;

        BuildingDef NextStructure()
        {
            var me = Me;
            int power = me.PowerProduced - me.PowerConsumed;
            int refineries = Count(BuildingRole.Refinery);
            int harvesters = _w.UnitsOf(PlayerId).Count(u => u.Def.Has(UnitAbility.Harvester));
            float minutes = _w.Time / 60f;
            // 1. Power first if needed (with margin for the next building)
            if (power < 40 || (power < 100 && minutes > 6))
            {
                var adv = Def(BuildingRole.AdvancedPower);
                if (CanBuild(adv) && minutes > 8) return adv;
                return Def(BuildingRole.Power);
            }
            if (refineries == 0) return Def(BuildingRole.Refinery);
            if (Count(BuildingRole.Barracks) == 0 && CanBuild(Def(BuildingRole.Barracks))) return Def(BuildingRole.Barracks);
            if (Count(BuildingRole.Factory) == 0 && CanBuild(Def(BuildingRole.Factory))) return Def(BuildingRole.Factory);
            // Economy before tech: a second refinery as soon as the basics stand.
            if (refineries < 2 && refineries < Profile.MaxRefineries && CanBuild(Def(BuildingRole.Refinery)) && (OreNearBase(18) > 4000 || OreNearBase(26) > 6000)) return Def(BuildingRole.Refinery);
            if (refineries < Profile.MaxRefineries && minutes > 4 && CanBuild(Def(BuildingRole.Refinery)) && OreNearBase(24) > 5000 && harvesters >= refineries * Profile.HarvestersPerRefinery) return Def(BuildingRole.Refinery);
            // Do not sink money into tech while the harvester fleet is short.
            if (harvesters < refineries * Profile.HarvestersPerRefinery && me.Credits < 1500) return null;
            if (Count(BuildingRole.Radar) == 0 && CanBuild(Def(BuildingRole.Radar))) return Def(BuildingRole.Radar);
            // Storage
            if (me.StorageCap > 0 && me.Credits > me.StorageCap * 0.85f && CanBuild(Def(BuildingRole.Silo))) return Def(BuildingRole.Silo);
            if (Count(BuildingRole.RepairDepot) == 0 && CanBuild(Def(BuildingRole.RepairDepot)) && minutes > 5 && Profile.Difficulty >= AIDifficulty.Normal) return Def(BuildingRole.RepairDepot);
            // Counter enemy superweapons
            if (EnemyHasSuperweapon() && Count(BuildingRole.Countermeasure) == 0 && CanBuild(Def(BuildingRole.Countermeasure))) return Def(BuildingRole.Countermeasure);
            if (Profile.UsesAir && Count(BuildingRole.Hangar) == 0 && CanBuild(Def(BuildingRole.Hangar)) && minutes > 6) return Def(BuildingRole.Hangar);
            if (Profile.UsesNaval && Count(BuildingRole.Shipyard) == 0 && CanBuild(Def(BuildingRole.Shipyard)) && NavalMap() && minutes > 5) return Def(BuildingRole.Shipyard);
            if (Count(BuildingRole.TechCenter) == 0 && CanBuild(Def(BuildingRole.TechCenter)) && minutes > 7) return Def(BuildingRole.TechCenter);
            if (refineries < Profile.MaxRefineries && CanBuild(Def(BuildingRole.Refinery)) && OreNearBase(22) > 3000 && minutes > 8) return Def(BuildingRole.Refinery);
            if (Count(BuildingRole.AdvancedFactory) == 0 && CanBuild(Def(BuildingRole.AdvancedFactory))) return Def(BuildingRole.AdvancedFactory);
            if (Count(BuildingRole.AdvancedBarracks) == 0 && CanBuild(Def(BuildingRole.AdvancedBarracks)) && minutes > 10) return Def(BuildingRole.AdvancedBarracks);
            if (Count(BuildingRole.Factory) < Profile.MaxProducers && me.Credits > 3000 && CanBuild(Def(BuildingRole.Factory))) return Def(BuildingRole.Factory);
            if (Count(BuildingRole.Barracks) < Math.Min(2, Profile.MaxProducers) && me.Credits > 2500 && CanBuild(Def(BuildingRole.Barracks))) return Def(BuildingRole.Barracks);
            if (Profile.UsesAir && Count(BuildingRole.AdvancedHangar) == 0 && CanBuild(Def(BuildingRole.AdvancedHangar)) && minutes > 13) return Def(BuildingRole.AdvancedHangar);
            if (Count(BuildingRole.ResearchLab) == 0 && CanBuild(Def(BuildingRole.ResearchLab)) && minutes > 12) return Def(BuildingRole.ResearchLab);
            if (Profile.UsesNaval && NavalMap() && Count(BuildingRole.AdvancedShipyard) == 0 && CanBuild(Def(BuildingRole.AdvancedShipyard)) && minutes > 14) return Def(BuildingRole.AdvancedShipyard);
            if (Profile.UsesSuperweapons && Count(BuildingRole.Superweapon) == 0 && CanBuild(Def(BuildingRole.Superweapon)) && minutes > 15) return Def(BuildingRole.Superweapon);
            if (Profile.UsesSuperweapons && Count(BuildingRole.CommCenter) == 0 && CanBuild(Def(BuildingRole.CommCenter)) && minutes > 16) return Def(BuildingRole.CommCenter);
            if (Profile.UsesSuperweapons && Count(BuildingRole.SpecialPower) == 0 && CanBuild(Def(BuildingRole.SpecialPower)) && minutes > 18) return Def(BuildingRole.SpecialPower);
            if (Count(BuildingRole.Countermeasure) == 0 && CanBuild(Def(BuildingRole.Countermeasure)) && minutes > 20 && Profile.Difficulty >= AIDifficulty.Hard) return Def(BuildingRole.Countermeasure);
            if (power < 150 && me.Credits > 2000) return CanBuild(Def(BuildingRole.AdvancedPower)) ? Def(BuildingRole.AdvancedPower) : Def(BuildingRole.Power);
            return null;
        }

        BuildingDef NextDefense()
        {
            if (Profile.TargetDefenses <= 0) return null;
            int defenses = _w.BuildingsOf(PlayerId).Count(b => b.IsDefense);
            float minutes = _w.Time / 60f;
            int target = Math.Min(Profile.TargetDefenses, 1 + (int)(minutes / 3f));
            bool threatened = Board.ThreatTime.TryGetValue(PlayerId, out var tt) && _w.Time - tt < 20;
            if (threatened) target += 1;
            if (defenses >= target) return null;
            if (Me.Credits < 800 && !threatened) return null;
            var options = new List<(BuildingDef d, float w)>();
            foreach (var id in Catalog.Faction(F).Buildings)
            {
                var d = Catalog.Building(id);
                if (d.Role != BuildingRole.Defense || !CanBuild(d)) continue;
                float w = 1f;
                if (d.Weapons.Count > 0)
                {
                    var wd = Catalog.Weapon(d.Weapons[0]);
                    var wh = Catalog.Warhead(wd.Warhead);
                    float total = Intel.Total;
                    w += Profile.CounterWeight * 3f * (Intel.EnemyInfantry / total * wh.Get(ArmorType.Infantry) + Intel.EnemyVehicles / total * wh.Get(ArmorType.Medium) + Intel.EnemyAir / total * wh.Get(ArmorType.Aircraft) * 1.5f);
                    if ((wd.Targets & TargetLayer.Air) != 0 && Intel.EnemyAir < 200 && minutes < 10) w *= 0.3f;
                }
                options.Add((d, w));
            }
            return Pick(options);
        }

        T Pick<T>(List<(T item, float w)> options) where T : class
        {
            if (options.Count == 0) return null;
            float sum = 0; foreach (var o in options) sum += Math.Max(0.01f, o.w);
            float r = _rng.Range(0, sum);
            foreach (var o in options) { r -= Math.Max(0.01f, o.w); if (r <= 0) return o.item; }
            return options[options.Count - 1].item;
        }

        bool EnemyHasSuperweapon()
        {
            foreach (var kb in Intel.Buildings.Values) if (kb.Role == BuildingRole.Superweapon) return true;
            return false;
        }

        bool? _naval;
        bool NavalMap()
        {
            if (_naval.HasValue) return _naval.Value;
            int water = 0; foreach (var c in _w.Map.Cells) if (c == Terrain.Water) water++;
            // Water must be near our base for a shipyard to make sense.
            bool near = false;
            for (int dy = -14; dy <= 14 && !near; dy++)
                for (int dx = -14; dx <= 14; dx++)
                    if (_w.Map.Get((int)_baseCenter.X + dx, (int)_baseCenter.Y + dy) == Terrain.Water) { near = true; break; }
            _naval = water > _w.Map.Cells.Length / 12 && near;
            return _naval.Value;
        }

        int OreNearBase(float radius)
        {
            int total = 0;
            var m = _w.Map;
            int r = (int)radius;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = (int)_baseCenter.X + dx, y = (int)_baseCenter.Y + dy;
                    if (m.InBounds(x, y)) total += m.Ore[m.Index(x, y)];
                }
            return total;
        }

        void PlaceReady()
        {
            var me = Me;
            foreach (var qt in new[] { ProductionQueueType.Structures, ProductionQueueType.Defenses })
            {
                var id = me.Ready[(int)qt];
                if (id == null) continue;
                var def = Catalog.Building(id);
                Vec2 near = _baseCenter;
                int radius = 16, spacing = 1;
                if (def.Role == BuildingRole.Refinery)
                {
                    var ore = _w.Economy.FindOre(_baseCenter, PlayerId, 26);
                    if (ore.HasValue) near = Vec2.Lerp(_baseCenter, ore.Value.Center, 0.6f);
                }
                else if (def.Role == BuildingRole.Defense)
                {
                    var dir = (EnemyCenter() - _baseCenter).Normalized;
                    var perp = dir.Perp;
                    // Protect refineries sometimes
                    var refinery = _w.BuildingsOf(PlayerId).Where(b => b.Def.Role == BuildingRole.Refinery).OrderBy(b => _rng.NextFloat()).FirstOrDefault();
                    if (refinery != null && _rng.Chance(0.35f)) near = refinery.Center + dir * 3f;
                    else near = _baseCenter + dir * _rng.Range(6f, 10f) + perp * _rng.Range(-7f, 7f);
                    radius = 8; spacing = 0;
                }
                else if (def.OnWater) { radius = 24; spacing = 0; }
                else if (def.Role == BuildingRole.Silo || def.Role == BuildingRole.Power) near = _baseCenter - (EnemyCenter() - _baseCenter).Normalized * 4f;
                var spot = _w.Production.FindPlacement(PlayerId, def, near, radius, _rng, spacing);
                if (!spot.HasValue && spacing > 0) spot = _w.Production.FindPlacement(PlayerId, def, _baseCenter, 22, _rng, 0);
                if (spot.HasValue) _w.Commands.Place(PlayerId, qt, spot.Value, false);
                else _w.Commands.Cancel(PlayerId, qt, id); // impossible to place: refund and move on
            }
        }

        // ================================================================== Research
        void Research()
        {
            if (!Profile.Researches || Me.Queues[(int)ProductionQueueType.Research].Count > 0) return;
            if (Me.Credits < 2500) return;
            var opts = new List<(TechDef t, float w)>();
            foreach (var id in Catalog.Faction(F).Techs)
            {
                var t = Catalog.Tech(id);
                if (t.Tier > Profile.MaxTier || !_w.Production.CanResearch(Me, t)) continue;
                float w = 1f;
                foreach (var fx in t.Effects)
                {
                    if (fx.Kind == TechEffectKind.HarvestRate) w += 2f;
                    if (fx.Kind == TechEffectKind.HealthMult && fx.Category == UnitCategory.Vehicle) w += 1.5f;
                    if (fx.Kind == TechEffectKind.ProductionSpeed) w += 1.2f;
                    if (fx.Kind == TechEffectKind.UnlockUnit) w += 0.8f;
                }
                opts.Add((t, w));
            }
            var pick = Pick(opts);
            if (pick != null) _w.Commands.Enqueue(PlayerId, pick.Id, true);
        }

        // ================================================================== Units
        void ProduceUnits()
        {
            var me = Me;
            int harvesters = _w.UnitsOf(PlayerId).Count(u => u.Def.Has(UnitAbility.Harvester));
            int refineries = Count(BuildingRole.Refinery);
            var vq = me.Queues[(int)ProductionQueueType.Vehicles];
            var hvDef = Catalog.Unit(Catalog.Faction(F).HarvesterUnit);
            bool harvesterQueued = vq.Any(i => i.DefId == hvDef.Id);
            bool needHarvester = refineries > 0 && harvesters < refineries * Profile.HarvestersPerRefinery && me.AIHarvests;
            if (needHarvester && !harvesterQueued) _w.Commands.Enqueue(PlayerId, hvDef.Id);
            // Expansion MCV
            if (Profile.Expands && me.AIBuilds && _expansionMcvId == 0 && _w.Time > 600 && refineries >= 1 && OreNearBase(20) < 6000 && me.Credits > 4500
                && Count(BuildingRole.ConstructionYard) < 3 && !vq.Any(i => Catalog.Unit(i.DefId)?.Has(UnitAbility.DeployToHQ) == true)
                && !_w.UnitsOf(PlayerId).Any(u => u.Def.Has(UnitAbility.DeployToHQ)))
            {
                var mcv = Catalog.Unit(Catalog.Faction(F).StartingVehicle);
                if (_w.Production.CanBuildUnit(me, mcv)) _w.Commands.Enqueue(PlayerId, mcv.Id);
            }
            float reserve = Profile.CreditReserve;
            // Save up for the missing harvester instead of spending everything on the army.
            if (needHarvester && harvesters == 0) reserve += hvDef.Cost;
            else if (needHarvester) reserve += hvDef.Cost * 0.4f;
            foreach (var qt in new[] { ProductionQueueType.Infantry, ProductionQueueType.Vehicles, ProductionQueueType.Aircraft, ProductionQueueType.Naval })
            {
                var q = me.Queues[(int)qt];
                int queued = 0; foreach (var it in q) queued += it.Count;
                if (queued >= 2) continue;
                if (me.Credits < reserve + 200) continue;
                if (qt == ProductionQueueType.Aircraft && (!Profile.UsesAir || AircraftCount() >= MaxAircraft())) continue;
                if (qt == ProductionQueueType.Naval && (!Profile.UsesNaval || NavalCount() >= 6 + (int)Profile.Difficulty * 2)) continue;
                if (qt == ProductionQueueType.Infantry && InfantryCount() > 22) continue;
                var u = ChooseUnit(qt);
                if (u != null) _w.Commands.Enqueue(PlayerId, u.Id);
            }
        }

        int AircraftCount() => _w.UnitsOf(PlayerId).Count(u => u.IsAir);
        int NavalCount() => _w.UnitsOf(PlayerId).Count(u => u.MoveClass == MoveClass.Naval);
        int InfantryCount() => _w.UnitsOf(PlayerId).Count(u => u.Def.Category == UnitCategory.Infantry);
        int MaxAircraft()
        {
            int pads = 0;
            foreach (var b in _w.BuildingsOf(PlayerId)) pads += b.Pads.Length;
            return Math.Min(pads, 4 + (int)Profile.Difficulty * 2);
        }

        UnitDef ChooseUnit(ProductionQueueType qt)
        {
            var me = Me;
            var opts = new List<(UnitDef u, float w)>();
            float total = Intel.Total;
            float inf = Intel.EnemyInfantry / total, veh = Intel.EnemyVehicles / total, air = Intel.EnemyAir / total, nav = Intel.EnemyNaval / total;
            float defs = Intel.EnemyDefenses;
            int engineers = _w.UnitsOf(PlayerId).Count(x => x.Def.Has(UnitAbility.CaptureRepair));
            foreach (var id in Catalog.Faction(F).Units)
            {
                var u = Catalog.Unit(id);
                if (Catalog.QueueFor(u) != qt || !_w.Production.CanBuildUnit(me, u)) continue;
                if (u.Has(UnitAbility.Harvester) || u.Has(UnitAbility.DeployToHQ)) continue;
                if (u.Tier > Profile.MaxTier) continue;
                float w = 1f;
                if (!u.IsArmedDef())
                {
                    // Utility units in small numbers
                    if (u.Has(UnitAbility.CaptureRepair)) w = Profile.CapturesTech && engineers < 2 && TechStructuresKnown() ? 0.6f : 0.02f;
                    else if (u.Has(UnitAbility.HealInfantry)) w = InfantryCount() > 6 ? 0.3f : 0.02f;
                    else if (u.Has(UnitAbility.RepairVehicles) || u.Has(UnitAbility.RepairNaval)) w = 0.15f;
                    else if (u.Has(UnitAbility.Recon)) w = Profile.ScoutInterval > 0 && _w.UnitsOf(PlayerId).Count(x => x.Def.Has(UnitAbility.Recon)) == 0 ? 0.6f : 0.01f;
                    else if (u.Has(UnitAbility.Transport)) w = 0.05f;
                    else w = 0.05f;
                    opts.Add((u, w));
                    continue;
                }
                var c = Classify(u);
                float cw = Profile.CounterWeight;
                w += cw * (c.antiArmor * veh * 4f + c.antiInf * inf * 4f + c.antiAir * air * 6f + c.antiNaval * nav * 3f);
                if (c.antiAir > 0.5f && c.antiArmor < 0.3f && c.antiInf < 0.3f) w *= air > 0.05f ? 1.5f : 0.15f;
                if (c.artillery) w += Math.Min(2f, defs / 3000f) * (0.5f + cw);
                if (u.Has(UnitAbility.Transport)) w *= 0.3f;
                w *= 1f + (u.Tier - 1) * 0.35f;
                if (u.Cost > me.Credits) w *= 0.3f;
                if (u.Model == "inf_rifle") w *= 1.4f;
                if (u.Model == "tank_mbt") w *= 1.6f;
                opts.Add((u, w));
            }
            return Pick(opts);
        }

        bool TechStructuresKnown()
        {
            foreach (var b in _w.Buildings)
                if (!b.Dead && b.Def.Role == BuildingRole.TechStructure && (_w.GetPlayer(b.Owner)?.IsNeutral ?? false) && _w.Fog.IsExplored(Me.Team, Int2.Floor(b.Center))) return true;
            return false;
        }

        public struct UnitClass { public float antiArmor, antiInf, antiAir, antiNaval; public bool artillery; }

        public static UnitClass Classify(UnitDef u)
        {
            var c = new UnitClass();
            foreach (var wid in u.Weapons)
            {
                var wd = Catalog.Weapon(wid);
                if (wd == null) continue;
                var wh = Catalog.Warhead(wd.Warhead);
                if ((wd.Targets & TargetLayer.Ground) != 0)
                {
                    c.antiArmor = Math.Max(c.antiArmor, wh.Get(ArmorType.Medium));
                    c.antiInf = Math.Max(c.antiInf, wh.Get(ArmorType.Infantry));
                }
                if ((wd.Targets & TargetLayer.Air) != 0) c.antiAir = Math.Max(c.antiAir, wh.Get(ArmorType.Aircraft));
                if ((wd.Targets & (TargetLayer.Naval | TargetLayer.Submerged)) != 0) c.antiNaval = Math.Max(c.antiNaval, wh.Get(ArmorType.Naval));
                if (wd.Projectile == ProjectileKind.Ballistic || wd.Range >= 9) c.artillery = true;
            }
            return c;
        }

        void ManageEconomyUnits()
        {
            // Idle harvesters get back to work; engineers capture visible tech structures.
            foreach (var u in _w.UnitsOf(PlayerId))
            {
                if (u.Def.Has(UnitAbility.Harvester) && u.HState == HarvestState.Idle && u.CurrentOrder.Type == OrderType.None && (_w.Tick + u.Id) % 3 == 0)
                    _w.Commands.Harvest(new[] { u.Id }, PlayerId, Vec2.Zero);
                if (u.Def.Has(UnitAbility.CaptureRepair) && u.IsIdle)
                {
                    Building best = null; float bd = float.MaxValue;
                    foreach (var b in _w.Buildings)
                    {
                        if (b.Dead || b.Def.Role != BuildingRole.TechStructure || _w.IsAlly(b.Owner, PlayerId)) continue;
                        if (!_w.Fog.IsExplored(Me.Team, Int2.Floor(b.Center))) continue;
                        float d = Vec2.Distance(b.Center, u.Pos);
                        if (d < bd) { bd = d; best = b; }
                    }
                    if (best != null) _w.Commands.Capture(new[] { u.Id }, PlayerId, best.Id);
                    else
                    {
                        // Repair the most damaged own structure
                        var dmg = _w.BuildingsOf(PlayerId).Where(b => b.HpRatio < 0.5f).OrderBy(b => b.HpRatio).FirstOrDefault();
                        if (dmg != null) _w.Commands.Capture(new[] { u.Id }, PlayerId, dmg.Id);
                    }
                }
            }
        }

        bool FindExpansionSite(out Vec2 site)
        {
            site = Vec2.Zero;
            var board = Board;
            float best = float.MaxValue;
            bool found = false;
            foreach (var v in _w.Map.OreVents)
            {
                long key = v.X * 10000L + v.Y;
                if (board.ClaimedFields.Contains(key)) continue;
                var c = v.Center;
                float dBase = Vec2.Distance(c, _baseCenter);
                if (dBase < 16 || dBase > 60) continue;
                // Avoid fields near known enemy structures or other bases.
                bool unsafeSite = false;
                foreach (var kb in Intel.Buildings.Values) if (Vec2.Distance(kb.Pos, c) < 18) { unsafeSite = true; break; }
                foreach (var b in _w.Buildings) if (!b.Dead && b.Def.Role == BuildingRole.ConstructionYard && Vec2.Distance(b.Center, c) < 14) { unsafeSite = true; break; }
                if (unsafeSite) continue;
                int ore = 0;
                for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++) { int x = v.X + dx, y = v.Y + dy; if (_w.Map.InBounds(x, y)) ore += _w.Map.Ore[_w.Map.Index(x, y)]; }
                if (ore < 8000) continue;
                float score = dBase - ore / 2000f;
                if (score < best)
                {
                    var def = Catalog.Building(Catalog.Unit(Catalog.Faction(F).StartingVehicle).DeploysInto);
                    var spot = FindDeploySpot(c + (_baseCenter - c).Normalized * 5f, def, 0);
                    if (!spot.HasValue) continue;
                    best = score; site = spot.Value; found = true;
                    board.ClaimedFields.Add(key);
                }
            }
            return found;
        }

        // ================================================================== Military
        void AssignNewUnits()
        {
            var dead = _roles.Keys.Where(id => _w.GetUnit(id) == null).ToList();
            foreach (var id in dead) _roles.Remove(id);
            foreach (var u in _w.UnitsOf(PlayerId))
            {
                if (_roles.ContainsKey(u.Id)) continue;
                if (!u.IsArmed || u.Def.Has(UnitAbility.Harvester)) continue;
                // Scripted mission units (tagged) stay where the mission placed them; the script commands them.
                if (_w.Mission != null && u.Tag != null) continue;
                Role r = Role.Rally;
                if (u.IsAir) r = Role.Air;
                else if (u.MoveClass == MoveClass.Naval) r = Role.Naval;
                else if (CountRole(Role.Defense) < 3 + (int)Profile.Difficulty) r = Role.Defense;
                _roles[u.Id] = r;
                if (r == Role.Rally || r == Role.Defense)
                {
                    var dest = r == Role.Rally ? _rally : _baseCenter + _rng.InsideCircle(5f);
                    _w.Commands.AttackMove(new[] { u.Id }, PlayerId, dest);
                }
            }
            // Medics and repair vehicles follow the rally group.
            foreach (var u in _w.UnitsOf(PlayerId))
                if (!u.IsArmed && (u.Def.Has(UnitAbility.HealInfantry) || u.Def.Has(UnitAbility.RepairVehicles)) && u.IsIdle && Vec2.Distance(u.Pos, _rally) > 4)
                    _w.Commands.Move(new[] { u.Id }, PlayerId, _rally + _rng.InsideCircle(2));
        }

        int CountRole(Role r) { int n = 0; foreach (var v in _roles.Values) if (v == r) n++; return n; }

        List<Unit> UnitsWithRole(Role r)
        {
            var l = new List<Unit>();
            foreach (var kv in _roles)
                if (kv.Value == r) { var u = _w.GetUnit(kv.Key); if (u != null) l.Add(u); }
            return l;
        }

        void Defend()
        {
            // Threat detection around own buildings and harvesters (only visible enemies).
            Unit threat = null; float threatValue = 0; Vec2 threatPos = Vec2.Zero;
            foreach (var e in Intel.VisibleEnemies)
            {
                if (!e.IsArmed) continue;
                bool nearBase = Vec2.Distance(e.Pos, _baseCenter) < 20;
                if (!nearBase)
                {
                    foreach (var b in _w.Buildings)
                        if (!b.Dead && b.Owner == PlayerId && b.DistanceTo(e.Pos) < 9) { nearBase = true; break; }
                }
                if (!nearBase)
                    foreach (var h in _w.Units)
                        if (!h.Dead && h.Owner == PlayerId && h.Def.Has(UnitAbility.Harvester) && Vec2.Distance(h.Pos, e.Pos) < 7) { nearBase = true; break; }
                if (!nearBase) continue;
                threatValue += e.Def.Cost;
                if (threat == null || Vec2.Distance(e.Pos, _baseCenter) < Vec2.Distance(threat.Pos, _baseCenter)) threat = e;
            }
            var board = Board;
            if (threat != null)
            {
                threatPos = threat.Pos;
                board.ReportThreat(PlayerId, threatPos, _w.Time);
                var defenders = UnitsWithRole(Role.Defense);
                defenders.AddRange(UnitsWithRole(Role.Rally));
                float ourValue = defenders.Sum(u => (float)u.Def.Cost);
                if (ourValue < threatValue * 1.2f && _attackActive && Vec2.Distance(_attackTarget, _baseCenter) > 0)
                {
                    // Recall part of the attack force for big threats.
                    var attackers = UnitsWithRole(Role.Attack);
                    foreach (var a in attackers.Take(attackers.Count / 2)) { _roles[a.Id] = Role.Defense; defenders.Add(a); }
                }
                var ids = defenders.Where(u => !u.Moving || u.CurrentOrder.Type != OrderType.AttackMove || Vec2.Distance(u.CurrentOrder.Pos, threatPos) > 6).Select(u => u.Id).ToList();
                if (ids.Count > 0) _w.Commands.AttackMove(ids, PlayerId, threatPos);
                // Emergency production
                if (threatValue > ourValue * 1.5f && Me.Credits > 600)
                {
                    var u = ChooseUnit(ProductionQueueType.Infantry);
                    if (u != null) _w.Commands.Enqueue(PlayerId, u.Id);
                }
                return;
            }
            // Help a threatened teammate (AI or human) if our own base is calm.
            if (board.AnyThreat(PlayerId, _w.Time, out int ally, out var allyPos) && Profile.Difficulty >= AIDifficulty.Normal)
            {
                var helpers = UnitsWithRole(Role.Rally);
                if (helpers.Count >= 3)
                    _w.Commands.AttackMove(helpers.Select(u => u.Id), PlayerId, allyPos);
            }
            // Human teammates: watch their structures too
            foreach (var p in _w.Players)
            {
                if (p.Id == PlayerId || !p.IsHuman || !_w.IsAlly(p.Id, PlayerId)) continue;
                foreach (var e in Intel.VisibleEnemies)
                {
                    if (!e.IsArmed) continue;
                    bool near = false;
                    foreach (var b in _w.BuildingsOf(p.Id)) if (b.DistanceTo(e.Pos) < 8) { near = true; break; }
                    if (near) { board.ReportThreat(p.Id, e.Pos, _w.Time); break; }
                }
            }
            // Idle defenders return to base
            foreach (var d in UnitsWithRole(Role.Defense))
                if (d.IsIdle && Vec2.Distance(d.Pos, _baseCenter) > 14) _w.Commands.AttackMove(new[] { d.Id }, PlayerId, _baseCenter + _rng.InsideCircle(6));
        }

        void Attack()
        {
            var board = Board;
            var attackers = UnitsWithRole(Role.Attack);
            if (_attackActive)
            {
                if (attackers.Count == 0 || attackers.Sum(a => (float)a.Def.Cost) < 1000)
                {
                    _attackActive = false;
                    foreach (var a in attackers) { _roles[a.Id] = Role.Rally; _w.Commands.AttackMove(new[] { a.Id }, PlayerId, _rally); }
                    return;
                }
                // Re-target when the objective is destroyed.
                var kb = Intel.BestTarget(attackers[0].Pos, Profile.Harasses, board.TargetOwner);
                var idle = attackers.Where(a => a.IsIdle || a.CurrentOrder.Type == OrderType.None).Select(a => a.Id).ToList();
                if (idle.Count > 0)
                {
                    Vec2 dest = kb != null ? kb.Pos : EnemyCenter();
                    _attackTarget = dest;
                    _w.Commands.AttackMove(idle, PlayerId, dest);
                }
                if (Profile.FocusFire) FocusFire(attackers);
                return;
            }
            if (_w.Time < _nextAttack)
            {
                // Join a teammate's launched attack.
                if (!(board.AttackLaunchTime > _w.Time - 20 && board.TargetOwner >= 0 && Profile.Difficulty >= AIDifficulty.Normal)) return;
            }
            var rally = UnitsWithRole(Role.Rally).Where(u => !u.IsAir && u.MoveClass != MoveClass.Naval).ToList();
            // Defenders beyond the home guard join the strike.
            var extraDefense = UnitsWithRole(Role.Defense).Where(u => !u.IsAir && u.MoveClass != MoveClass.Naval).Skip(3).ToList();
            float value = rally.Sum(u => (float)u.Def.Cost) + extraDefense.Sum(u => (float)u.Def.Cost);
            float need = Profile.AttackMinValue + Profile.AttackValueGrowth * Math.Min(_attackWaves, 6) * Me.AIAggression;
            // An army that has waited long past its schedule attacks with what it has.
            if (_w.Time > _nextAttack + Profile.AttackInterval * 0.5f) need *= 0.6f;
            if (_w.Time > _nextAttack + Profile.AttackInterval * 1.5f) need *= 0.6f;
            if (value < need && !(board.AttackLaunchTime > _w.Time - 20 && value > need * 0.5f)) return;
            // Choose target (shared with team).
            int targetOwner = board.TargetOwner;
            if (targetOwner < 0 || _w.GetPlayer(targetOwner).Defeated || _w.Time - board.TargetSetTime > 300)
            {
                var enemies = _w.Players.Where(p => !p.IsNeutral && !p.Defeated && _w.IsEnemy(PlayerId, p.Id)).ToList();
                if (enemies.Count == 0) return;
                targetOwner = enemies.OrderBy(p => Vec2.Distance(p.StartCell.Center, _baseCenter)).First().Id;
                board.TargetOwner = targetOwner; board.TargetSetTime = _w.Time;
            }
            var target = Intel.BestTarget(_baseCenter, Profile.Harasses, targetOwner);
            Vec2 dest2 = target != null ? target.Pos : _w.GetPlayer(targetOwner).StartCell.Center;
            board.TargetPos = dest2;
            board.AttackLaunchTime = _w.Time;
            _attackTarget = dest2;
            _attackActive = true;
            _attackStarted = _w.Time;
            _attackWaves++;
            _nextAttack = _w.Time + Profile.AttackInterval * _rng.Range(0.85f, 1.2f);
            rally.AddRange(extraDefense);
            foreach (var u in rally) _roles[u.Id] = Role.Attack;
            if (Profile.Flanks && rally.Count >= 8)
            {
                // Split: main body straight, flank group via a perpendicular waypoint.
                var dir = (dest2 - _baseCenter).Normalized;
                var perp = dir.Perp * (_rng.Chance(0.5f) ? 1 : -1);
                var flankPoint = Vec2.Lerp(_baseCenter, dest2, 0.55f) + perp * 14f;
                flankPoint = new Vec2(SMath.Clamp(flankPoint.X, 3, _w.Map.Width - 3), SMath.Clamp(flankPoint.Y, 3, _w.Map.Height - 3));
                var flank = rally.Where((u, i) => i % 3 == 0).Select(u => u.Id).ToList();
                var main = rally.Where((u, i) => i % 3 != 0).Select(u => u.Id).ToList();
                _w.Commands.AttackMove(flank, PlayerId, flankPoint);
                _w.Commands.AttackMove(flank, PlayerId, dest2, true);
                _w.Commands.AttackMove(main, PlayerId, dest2);
            }
            else _w.Commands.AttackMove(rally.Select(u => u.Id), PlayerId, dest2);
            // Escort support units
            foreach (var u in _w.UnitsOf(PlayerId))
                if (!u.IsArmed && (u.Def.Has(UnitAbility.HealInfantry) || u.Def.Has(UnitAbility.RepairVehicles)) && rally.Count > 0)
                    _w.Commands.Follow(new[] { u.Id }, PlayerId, rally[0].Id);
        }

        void FocusFire(List<Unit> attackers)
        {
            // Concentrate fire on the weakest visible enemy near the group.
            foreach (var a in attackers)
            {
                if (a.TargetId == 0) continue;
                var t = _w.Get(a.TargetId);
                if (t == null) continue;
                _w.Spatial.UnitsNear(a.Pos, _w.Combat.MaxRangeAny(a), _tmp);
                Unit weakest = null;
                foreach (var e in _tmp)
                    if (_w.IsEnemy(PlayerId, e.Owner) && _w.Combat.CanTarget(a, e) && _w.Combat.VisibleTo(e, Me.Team) && _w.Combat.InRange(a, e) && (weakest == null || e.Hp < weakest.Hp)) weakest = e;
                if (weakest != null && weakest.Id != a.TargetId && weakest.Hp < (t.Hp * 0.6f)) a.TargetId = weakest.Id;
            }
        }

        void Harass()
        {
            if (!Profile.Harasses || _w.Time < _nextHarass) return;
            _nextHarass = _w.Time + 150 * _rng.Range(0.8f, 1.3f);
            // Fast units hit known refineries / harvesters.
            Vec2? target = null;
            foreach (var e in Intel.VisibleEnemies) if (e.Def.Has(UnitAbility.Harvester)) { target = e.Pos; break; }
            if (!target.HasValue)
                foreach (var kb in Intel.Buildings.Values) if (kb.Role == BuildingRole.Refinery) { target = kb.Pos; break; }
            if (!target.HasValue) return;
            var fast = UnitsWithRole(Role.Rally).Where(u => u.Def.Speed >= 3f && !u.IsAir).Take(4).ToList();
            if (fast.Count < 2)
            {
                var air = UnitsWithRole(Role.Air).Where(u => u.Def.Locomotor == Locomotor.Helicopter && u.Ammo > 0).Take(2).ToList();
                if (air.Count > 0) _w.Commands.AttackMove(air.Select(u => u.Id), PlayerId, target.Value);
                return;
            }
            foreach (var f in fast) _roles[f.Id] = Role.Harass;
            _w.Commands.AttackMove(fast.Select(u => u.Id), PlayerId, target.Value);
        }

        void Scout()
        {
            // Scouts that finished their trip rejoin the army.
            foreach (var sc in UnitsWithRole(Role.Scout))
                if (sc.IsIdle) { _roles[sc.Id] = Role.Rally; _w.Commands.AttackMove(new[] { sc.Id }, PlayerId, _rally); }
            if (Profile.ScoutInterval <= 0 || _w.Time < _nextScout) return;
            _nextScout = _w.Time + Profile.ScoutInterval;
            // Prefer recon aircraft, else a fast ground unit.
            Unit scout = _w.UnitsOf(PlayerId).FirstOrDefault(u => u.Def.Has(UnitAbility.Recon) && u.IsAir);
            if (scout == null) scout = UnitsWithRole(Role.Rally).Where(u => !u.IsAir).OrderByDescending(u => u.Def.Speed).FirstOrDefault();
            if (scout == null) return;
            Vec2 dest;
            var unscouted = new List<Vec2>();
            for (int i = 0; i < _w.Map.StartPositions.Count; i++)
                if (!Intel.ScoutedStarts.Contains(i)) unscouted.Add(_w.Map.StartPositions[i].Center);
            if (unscouted.Count > 0) dest = unscouted[_rng.Range(0, unscouted.Count)];
            else
            {
                // Check an ore field (possible enemy expansion)
                if (_w.Map.OreVents.Count == 0) return;
                dest = _w.Map.OreVents[_rng.Range(0, _w.Map.OreVents.Count)].Center;
            }
            if (scout.IsArmed) _roles[scout.Id] = Role.Scout;
            _w.Commands.Move(new[] { scout.Id }, PlayerId, dest);
        }

        void ManageAir()
        {
            var air = UnitsWithRole(Role.Air);
            if (air.Count == 0) return;
            // Interceptors defend against visible enemy aircraft near base.
            Unit enemyAir = Intel.VisibleEnemies.FirstOrDefault(e => e.IsAir && Vec2.Distance(e.Pos, _baseCenter) < 25);
            foreach (var a in air)
            {
                if (a.Def.Ammo > 0 && a.Ammo <= 0) continue;
                if (a.CurrentOrder.Type != OrderType.None) continue;
                var mask = _w.Combat.TargetMask(a);
                if (enemyAir != null && (mask & TargetLayer.Air) != 0) { _w.Commands.Attack(new[] { a.Id }, PlayerId, enemyAir.Id); continue; }
                if (_attackActive && (mask & TargetLayer.Ground) != 0 && (a.Ammo >= a.Def.Ammo || a.Def.Ammo == 0))
                {
                    // Strike a known structure / defense near the attack target.
                    var kb = Intel.Buildings.Values.Where(b => Vec2.Distance(b.Pos, _attackTarget) < 15).OrderByDescending(b => b.IsDefense ? 2 : 1).FirstOrDefault();
                    var real = kb != null ? _w.GetBuilding(kb.Id) : null;
                    if (real != null) _w.Commands.Attack(new[] { a.Id }, PlayerId, real.Id);
                    else _w.Commands.AttackMove(new[] { a.Id }, PlayerId, _attackTarget);
                }
            }
        }

        void ManageNaval()
        {
            var navy = UnitsWithRole(Role.Naval);
            if (navy.Count < 3) return;
            var idle = navy.Where(n => n.IsIdle).ToList();
            if (idle.Count < 3) return;
            // Attack the nearest known coastal structure reachable by water.
            Vec2? target = null; float bd = float.MaxValue;
            foreach (var kb in Intel.Buildings.Values)
            {
                bool coastal = false;
                for (int k = 0; k < 8 && !coastal; k++)
                {
                    var p = kb.Pos + Vec2.FromAngle(k * 0.785f) * 6f;
                    if (_w.Map.Get((int)p.X, (int)p.Y) == Terrain.Water) coastal = true;
                }
                if (!coastal) continue;
                float d = Vec2.Distance(kb.Pos, idle[0].Pos);
                if (d < bd) { bd = d; target = kb.Pos; }
            }
            foreach (var e in Intel.VisibleEnemies) if (e.MoveClass == MoveClass.Naval) { target = e.Pos; break; }
            if (target.HasValue) _w.Commands.AttackMove(idle.Select(n => n.Id), PlayerId, target.Value);
            else
            {
                // Patrol toward the enemy along the water.
                var dest = EnemyCenter();
                var water = _w.FindFreeCellNear(dest, MoveClass.Naval, 30);
                if (water.HasValue) _w.Commands.AttackMove(idle.Select(n => n.Id), PlayerId, water.Value.Center);
            }
        }

        void Retreat()
        {
            if (Profile.RetreatHp <= 0) return;
            var depot = _w.BuildingsOf(PlayerId).FirstOrDefault(b => b.Def.Role == BuildingRole.RepairDepot && b.Operational);
            foreach (var kv in _roles.ToList())
            {
                var u = _w.GetUnit(kv.Key);
                if (u == null) continue;
                if (kv.Value == Role.Retreat)
                {
                    if (u.HpRatio > 0.9f || (u.IsIdle && Vec2.Distance(u.Pos, _baseCenter) < 12) || (u.IsIdle && depot == null))
                    {
                        _roles[u.Id] = Role.Rally;
                        _w.Commands.AttackMove(new[] { u.Id }, PlayerId, _rally);
                    }
                    continue;
                }
                if ((kv.Value == Role.Attack || kv.Value == Role.Harass) && u.HpRatio < Profile.RetreatHp && u.Def.Cost >= 500 && !u.IsAir)
                {
                    _roles[u.Id] = Role.Retreat;
                    if (depot != null && u.Def.Category == UnitCategory.Vehicle) _w.Commands.RepairAtDepot(new[] { u.Id }, PlayerId, depot.Id);
                    else _w.Commands.Move(new[] { u.Id }, PlayerId, _baseCenter + _rng.InsideCircle(4));
                }
            }
        }

        // ================================================================== Support powers
        void UsePowers()
        {
            var me = Me;
            if (!Profile.UsesSuperweapons) return;
            for (int i = 0; i < me.PowerReady.Length; i++)
            {
                if (!me.PowerReady[i]) continue;
                var kind = (SupportPowerKind)i;
                var def = Catalog.Powers[kind];
                switch (kind)
                {
                    case SupportPowerKind.OrbitalLance:
                    case SupportPowerKind.HammerfallMissile:
                    case SupportPowerKind.Bombardment:
                        {
                            var loc = Intel.BestStrikeLocation(def.Radius, out float v);
                            if (loc.HasValue && v > 1500) _w.Commands.LaunchPower(PlayerId, kind, loc.Value);
                        }
                        break;
                    case SupportPowerKind.EmpPulse:
                        {
                            // Disable the biggest visible enemy vehicle cluster.
                            Vec2? best = null; float bv = 0;
                            foreach (var e in Intel.VisibleEnemies)
                            {
                                if (e.Def.Category != UnitCategory.Vehicle) continue;
                                float v = 0;
                                foreach (var o in Intel.VisibleEnemies) if (Vec2.Distance(o.Pos, e.Pos) < def.Radius && o.Def.Category == UnitCategory.Vehicle) v += o.Def.Cost;
                                if (v > bv) { bv = v; best = e.Pos; }
                            }
                            if (best.HasValue && bv > 2500) _w.Commands.LaunchPower(PlayerId, kind, best.Value);
                            else
                            {
                                var loc = Intel.BestStrikeLocation(def.Radius, out float v2);
                                if (loc.HasValue && v2 > 5000) _w.Commands.LaunchPower(PlayerId, kind, loc.Value);
                            }
                        }
                        break;
                    case SupportPowerKind.SkyeyeReveal:
                        {
                            Vec2 dest = EnemyCenter();
                            _w.Commands.LaunchPower(PlayerId, kind, dest);
                        }
                        break;
                    case SupportPowerKind.Paradrop:
                        {
                            var kb = Intel.Buildings.Values.Where(b => b.Role == BuildingRole.Refinery || b.Role == BuildingRole.Power).OrderBy(b => _rng.NextFloat()).FirstOrDefault();
                            if (kb != null) _w.Commands.LaunchPower(PlayerId, kind, kb.Pos + (_baseCenter - kb.Pos).Normalized * 4f);
                        }
                        break;
                }
            }
        }
    }

    public static class UnitDefExtensions
    {
        public static bool IsArmedDef(this UnitDef u) => u.Weapons.Count > 0;
    }
}
