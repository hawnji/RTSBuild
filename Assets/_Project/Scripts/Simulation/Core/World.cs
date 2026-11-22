using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// The authoritative game state and fixed-step simulation loop. Systems (movement, combat, economy, production,
    /// fog of war, AI, missions) are separate classes that operate on the world; the Unity client only reads state
    /// and the event stream, and issues commands through <see cref="Commands"/>.
    /// </summary>
    public class World
    {
        public const int TicksPerSecond = 15;
        public const float Dt = 1f / TicksPerSecond;

        public MapData Map;
        public GameSettings Settings;
        public readonly List<Player> Players = new List<Player>();
        public readonly List<Unit> Units = new List<Unit>();
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<FireZone> Fires = new List<FireZone>();
        public readonly Dictionary<int, Entity> ById = new Dictionary<int, Entity>();
        public readonly List<SimEvent> Events = new List<SimEvent>();
        public int Tick;
        public float Time;
        public int NextId = 1;
        public SimRandom Rng;
        public int[] BuildingGrid;     // building id occupying each cell (0 = none)
        public bool NeutralHostile;
        public bool GameOver;
        public int WinningTeam = -1;
        public bool Cinematic;         // input locked
        public bool PlayerSuperweaponWarnings = true;

        public SpatialGrid Spatial;
        public Pathfinder Pathfinder;
        public FogOfWar Fog;
        public CombatSystem Combat;
        public MovementSystem Movement;
        public UnitBehaviorSystem Behavior;
        public EconomySystem Economy;
        public ProductionSystem Production;
        public SupportPowerSystem Powers;
        public VictorySystem Victory;
        public Commands Commands;
        public readonly List<IAIController> AIs = new List<IAIController>();
        public MissionRunner Mission;

        public World(MapData map, GameSettings settings)
        {
            Catalog.EnsureBuilt();
            Map = map;
            Settings = settings ?? new GameSettings();
            Rng = new SimRandom(Settings.Seed);
            BuildingGrid = new int[map.Width * map.Height];
            Spatial = new SpatialGrid(map.Width, map.Height, 4);
            Pathfinder = new Pathfinder(this);
            Fog = new FogOfWar(this);
            Combat = new CombatSystem(this);
            Movement = new MovementSystem(this);
            Behavior = new UnitBehaviorSystem(this);
            Economy = new EconomySystem(this);
            Production = new ProductionSystem(this);
            Powers = new SupportPowerSystem(this);
            Victory = new VictorySystem(this);
            Commands = new Commands(this);
        }

        // ------------------------------------------------------------------ Players
        public Player AddPlayer(string name, FactionId faction, int team, bool human, AIDifficulty diff = AIDifficulty.Normal, int color = -1)
        {
            var p = new Player
            {
                Id = Players.Count, Name = name, Faction = faction, Team = team, IsHuman = human, IsAI = !human && faction != FactionId.Neutral,
                IsNeutral = faction == FactionId.Neutral, Difficulty = diff, ColorIndex = color < 0 ? Players.Count : color, Credits = Settings.StartingCredits
            };
            if (p.IsNeutral) { p.Team = -1; p.Credits = 0; }
            Players.Add(p);
            Fog.EnsureTeam(p.Team);
            return p;
        }

        public Player GetPlayer(int id) => id >= 0 && id < Players.Count ? Players[id] : null;

        public bool IsEnemy(int a, int b)
        {
            if (a == b) return false;
            var pa = GetPlayer(a); var pb = GetPlayer(b);
            if (pa == null || pb == null) return false;
            if (pa.IsNeutral || pb.IsNeutral) return NeutralHostile && pa.IsNeutral != pb.IsNeutral;
            return pa.Team != pb.Team;
        }

        public bool IsAlly(int a, int b)
        {
            if (a == b) return true;
            var pa = GetPlayer(a); var pb = GetPlayer(b);
            if (pa == null || pb == null) return false;
            return pa.Team == pb.Team && !pa.IsNeutral;
        }

        public int TeamOf(int player) => GetPlayer(player)?.Team ?? -1;

        // ------------------------------------------------------------------ Entities
        public Entity Get(int id) => id != 0 && ById.TryGetValue(id, out var e) && !e.Dead ? e : null;
        public Unit GetUnit(int id) => Get(id) as Unit;
        public Building GetBuilding(int id) => Get(id) as Building;

        public Unit SpawnUnit(string defId, int owner, Vec2 pos, float facing = -SMath.PI / 2)
        {
            var def = Catalog.Unit(defId);
            if (def == null) throw new ArgumentException("Unknown unit " + defId);
            var u = new Unit { Id = NextId++, Owner = owner, DefId = defId, Def = def, Pos = pos, PrevPos = pos, Facing = facing, TurretFacing = facing };
            var p = GetPlayer(owner);
            float hpMult = p != null ? p.Mods.UnitMult(TechEffectKind.HealthMult, def) : 1f;
            u.MaxHp = def.Health * hpMult; u.Hp = u.MaxHp;
            u.Ammo = def.Ammo;
            u.Submerged = def.Locomotor == Locomotor.Submarine;
            u.Height = u.IsAir ? def.CruiseHeight : 0;
            foreach (var w in def.Weapons) u.Weapons.Add(MakeWeaponState(w));
            if (def.Has(UnitAbility.Harvester)) u.HState = HarvestState.Seeking;
            Units.Add(u); ById[u.Id] = u;
            u.AcquireTimer = (u.Id % 8) * Dt;
            if (p != null) p.Stats.UnitsBuilt++;
            return u;
        }

        public WeaponState MakeWeaponState(string id)
        {
            var wd = Catalog.Weapon(id);
            return new WeaponState { WeaponId = id, Def = wd, ClipLeft = wd != null ? wd.Clip : 0 };
        }

        public Building SpawnBuilding(string defId, int owner, Int2 origin, bool instant = true, bool rotated = false)
        {
            var def = Catalog.Building(defId);
            if (def == null) throw new ArgumentException("Unknown building " + defId);
            int w = rotated ? def.Height : def.Width, h = rotated ? def.Width : def.Height;
            var b = new Building
            {
                Id = NextId++, Owner = owner, OriginalOwner = owner, DefId = defId, Def = def, Origin = origin, W = w, H = h,
                BuildUp = instant ? 1f : 0f
            };
            b.Pos = b.Center;
            var p = GetPlayer(owner);
            float hpMult = p != null ? p.Mods.BuildingHealthMult(def) : 1f;
            b.MaxHp = def.Health * hpMult; b.Hp = b.MaxHp;
            foreach (var wpn in def.Weapons) b.Weapons.Add(MakeWeaponState(wpn));
            if (def.Docks > 0) b.Pads = new int[def.Docks];
            b.Facing = -SMath.PI / 2; b.TurretFacing = b.Facing;
            Buildings.Add(b); ById[b.Id] = b;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Map.InBounds(origin.X + x, origin.Y + y)) BuildingGrid[Map.Index(origin.X + x, origin.Y + y)] = b.Id;
            Spatial.AddBuilding(b);
            Pathfinder.InvalidateArea(origin, w, h);
            if (p != null) p.Stats.BuildingsBuilt++;
            Economy.RecalculatePlayer(owner);
            Powers.OnBuildingAdded(b);
            Emit(new SimEvent { Type = SimEventType.BuildingPlaced, Player = owner, EntityId = b.Id, Pos = b.Center, Text = defId });
            return b;
        }

        void RemoveBuildingFootprint(Building b)
        {
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                {
                    int cx = b.Origin.X + x, cy = b.Origin.Y + y;
                    if (Map.InBounds(cx, cy) && BuildingGrid[Map.Index(cx, cy)] == b.Id) BuildingGrid[Map.Index(cx, cy)] = 0;
                }
            Spatial.RemoveBuilding(b);
            Pathfinder.InvalidateArea(b.Origin, b.W, b.H);
        }

        /// <summary>Transfers a building to a new owner (engineer capture or scripted).</summary>
        public void ChangeOwner(Entity e, int newOwner)
        {
            int old = e.Owner;
            e.Owner = newOwner;
            if (e is Building b)
            {
                b.Repairing = false;
                Economy.RecalculatePlayer(old);
                Economy.RecalculatePlayer(newOwner);
                Powers.OnBuildingAdded(b);
                Powers.RefreshOwnership(old);
            }
            else if (e is Unit u)
            {
                u.CurrentOrder = Order.None; u.OrderQueue.Clear(); u.Moving = false; u.TargetId = 0;
            }
            Emit(new SimEvent { Type = SimEventType.Captured, Player = newOwner, OtherId = old, EntityId = e.Id, Pos = e.Pos, Text = e.DefId });
        }

        public Building BuildingAtCell(int x, int y)
        {
            if (!Map.InBounds(x, y)) return null;
            int id = BuildingGrid[Map.Index(x, y)];
            return id == 0 ? null : GetBuilding(id);
        }

        /// <summary>Dynamic passability: terrain + structures. Gates let their own team through.</summary>
        public bool Passable(int x, int y, MoveClass mc, int team = -99)
        {
            if (mc == MoveClass.Air) return Map.InBounds(x, y);
            if (!Map.PassableStatic(x, y, mc)) return false;
            int id = BuildingGrid[y * Map.Width + x];
            if (id == 0) return true;
            if (!ById.TryGetValue(id, out var e) || e.Dead) return true;
            var b = (Building)e;
            if (b.Def.Role == BuildingRole.Gate && team != -99 && TeamOf(b.Owner) == team) return true;
            if (b.Def.OnWater && mc == MoveClass.Naval) return false;
            return false;
        }

        public void Emit(SimEvent e) => Events.Add(e);

        public void Message(string speaker, string text, int player = -1, string voice = null)
        {
            Emit(new SimEvent { Type = SimEventType.MissionMessage, Player = player, Text = text, Text2 = speaker, Value = 0 });
        }

        // ------------------------------------------------------------------ Damage & death
        public void Kill(Entity e, int killerId)
        {
            if (e.Dead) return;
            e.Dead = true;
            e.Hp = 0;
            var killer = Get(killerId);
            var victimOwner = GetPlayer(e.Owner);
            if (killer != null)
            {
                var kp = GetPlayer(killer.Owner);
                if (kp != null && IsEnemy(killer.Owner, e.Owner))
                {
                    if (e.IsBuilding) kp.Stats.BuildingsKilled++; else kp.Stats.UnitsKilled++;
                    Combat.GrantXp(killer, e);
                }
            }
            if (e is Building b)
            {
                RemoveBuildingFootprint(b);
                if (victimOwner != null) victimOwner.Stats.BuildingsLost++;
                // Release docked aircraft
                foreach (var padId in b.Pads)
                {
                    var a = GetUnit(padId);
                    if (a != null) { a.AState = AirState.TakingOff; a.PadBuildingId = 0; a.PadIndex = -1; }
                }
                Emit(new SimEvent { Type = SimEventType.BuildingDestroyed, Player = e.Owner, EntityId = e.Id, Pos = b.Center, Text = b.DefId, Value = Math.Max(b.W, b.H) });
                Economy.RecalculatePlayer(e.Owner);
                Powers.RefreshOwnership(e.Owner);
                // Survivors from military buildings
                if (victimOwner != null && !victimOwner.IsNeutral && b.Def.Cost >= 500 && !b.IsDefense && !b.IsWallLike && b.Def.Role != BuildingRole.Silo)
                {
                    var f = Catalog.Faction(victimOwner.Faction);
                    string inf = victimOwner.Faction == FactionId.Alliance ? "ca_rifleman" : "id_levy";
                    int n = Math.Min(3, b.Def.Cost / 1000);
                    for (int i = 0; i < n; i++)
                    {
                        var spot = FindFreeCellNear(b.Center, MoveClass.Foot, 3);
                        if (spot.HasValue) { var s = SpawnUnit(inf, e.Owner, spot.Value.Center); s.Hp = s.MaxHp * 0.5f; victimOwner.Stats.UnitsBuilt--; }
                    }
                }
                // Rubble
                for (int y = 0; y < b.H; y++)
                    for (int x = 0; x < b.W; x++)
                    {
                        int cx = b.Origin.X + x, cy = b.Origin.Y + y;
                        var t = Map.Get(cx, cy);
                        if (t != Terrain.Water && t != Terrain.Bridge && !b.IsWallLike) Map.Set(cx, cy, Terrain.Rubble);
                    }
                Pathfinder.InvalidateArea(b.Origin, b.W, b.H);
            }
            else if (e is Unit u)
            {
                if (victimOwner != null) victimOwner.Stats.UnitsLost++;
                Emit(new SimEvent { Type = SimEventType.UnitDied, Player = e.Owner, EntityId = e.Id, Pos = u.Pos, Text = u.DefId, Value = u.Height, OtherId = killerId });
                ReleasePad(u);
                // Passengers die with a destroyed transport unless it is a ground vehicle (they bail out).
                foreach (var pid in u.Passengers)
                {
                    var p = GetUnit(pid);
                    if (p == null) continue;
                    if (u.Def.Category == UnitCategory.Vehicle)
                    {
                        p.CarrierId = 0;
                        var spot = FindFreeCellNear(u.Pos, p.MoveClass, 3);
                        p.Pos = spot.HasValue ? spot.Value.Center : u.Pos; p.PrevPos = p.Pos;
                        p.Hp *= 0.5f;
                    }
                    else Kill(p, killerId);
                }
                u.Passengers.Clear();
                if (u.CarrierId != 0)
                {
                    var c = GetUnit(u.CarrierId);
                    if (c != null) c.Passengers.Remove(u.Id);
                }
            }
        }

        public void ReleasePad(Unit u)
        {
            if (u.PadBuildingId == 0) return;
            var pad = GetBuilding(u.PadBuildingId);
            if (pad != null && u.PadIndex >= 0 && u.PadIndex < pad.Pads.Length && pad.Pads[u.PadIndex] == u.Id) pad.Pads[u.PadIndex] = 0;
            u.PadBuildingId = 0; u.PadIndex = -1;
        }

        public Int2? FindFreeCellNear(Vec2 p, MoveClass mc, int maxRadius, int team = -99)
        {
            var c = Int2.Floor(p);
            for (int r = 0; r <= maxRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = c.X + dx, y = c.Y + dy;
                        if (Passable(x, y, mc, team) && !CellCrowded(x, y)) return new Int2(x, y);
                    }
            for (int r = 0; r <= maxRadius + 4; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = c.X + dx, y = c.Y + dy;
                        if (Passable(x, y, mc, team)) return new Int2(x, y);
                    }
            return null;
        }

        bool CellCrowded(int x, int y)
        {
            int n = 0;
            foreach (var u in Spatial.UnitsNear(new Vec2(x + 0.5f, y + 0.5f), 0.6f, this))
                if (!u.IsAir) n++;
            return n >= 2;
        }

        // ------------------------------------------------------------------ Main loop
        public void Step()
        {
            if (GameOver && Mission == null) { Tick++; Time += Dt; FlushDead(); return; }
            Tick++;
            Time += Dt;
            Spatial.Rebuild(Units);

            Mission?.Update();
            for (int i = 0; i < AIs.Count; i++)
            {
                try { AIs[i].Update(); }
                catch (Exception ex) { Emit(new SimEvent { Type = SimEventType.Hint, Player = -1, Text = "AI error: " + ex.Message }); }
            }

            Economy.Update();
            Production.Update();
            Behavior.UpdateAll();
            Movement.UpdateAll();
            Combat.UpdateBuildings();
            Combat.UpdateProjectiles();
            Combat.UpdateFires();
            Powers.Update();
            if (Tick % 3 == 0) Fog.Update();
            FlushDead();
            Victory.Update();
        }

        void FlushDead()
        {
            for (int i = Units.Count - 1; i >= 0; i--)
                if (Units[i].Dead) { ById.Remove(Units[i].Id); Units.RemoveAt(i); }
            for (int i = Buildings.Count - 1; i >= 0; i--)
                if (Buildings[i].Dead) { ById.Remove(Buildings[i].Id); Buildings.RemoveAt(i); }
        }

        // ------------------------------------------------------------------ Queries
        public IEnumerable<Building> BuildingsOf(int player)
        {
            for (int i = 0; i < Buildings.Count; i++)
                if (!Buildings[i].Dead && Buildings[i].Owner == player) yield return Buildings[i];
        }

        public IEnumerable<Unit> UnitsOf(int player)
        {
            for (int i = 0; i < Units.Count; i++)
                if (!Units[i].Dead && Units[i].Owner == player) yield return Units[i];
        }

        public bool HasRole(int player, BuildingRole role, bool operational = false)
        {
            foreach (var b in Buildings)
                if (!b.Dead && b.Owner == player && b.Def.Role == role && (!operational || b.BuildUp >= 1f)) return true;
            return false;
        }

        public int CountRole(int player, BuildingRole role)
        {
            int n = 0;
            foreach (var b in Buildings) if (!b.Dead && b.Owner == player && b.Def.Role == role) n++;
            return n;
        }

        public int CountUnits(int player, string defId)
        {
            int n = 0;
            foreach (var u in Units) if (!u.Dead && u.Owner == player && u.DefId == defId) n++;
            return n;
        }

        public float TerrainHeight(Vec2 p)
        {
            var c = Int2.Floor(p);
            if (!Map.InBounds(c.X, c.Y)) return 0;
            return Map.Level[Map.Index(c.X, c.Y)] * 1.0f;
        }

        public int GetSeedRandom() => (int)(Rng.NextUInt() & 0x7FFFFFFF);
    }

    /// <summary>Uniform-grid spatial partition for fast proximity queries.</summary>
    public class SpatialGrid
    {
        readonly int _bw, _bh, _size;
        readonly List<Unit>[] _units;
        readonly List<Building>[] _buildings;

        public SpatialGrid(int w, int h, int bucketSize)
        {
            _size = bucketSize;
            _bw = (w + bucketSize - 1) / bucketSize;
            _bh = (h + bucketSize - 1) / bucketSize;
            _units = new List<Unit>[_bw * _bh];
            _buildings = new List<Building>[_bw * _bh];
            for (int i = 0; i < _units.Length; i++) { _units[i] = new List<Unit>(); _buildings[i] = new List<Building>(); }
        }

        int Bucket(Vec2 p)
        {
            int bx = SMath.Clamp((int)(p.X / _size), 0, _bw - 1), by = SMath.Clamp((int)(p.Y / _size), 0, _bh - 1);
            return by * _bw + bx;
        }

        public void Rebuild(List<Unit> units)
        {
            for (int i = 0; i < _units.Length; i++) _units[i].Clear();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.IsInside) continue;
                _units[Bucket(u.Pos)].Add(u);
            }
        }

        public void AddBuilding(Building b)
        {
            int x0 = SMath.Clamp(b.Origin.X / _size, 0, _bw - 1), x1 = SMath.Clamp((b.Origin.X + b.W) / _size, 0, _bw - 1);
            int y0 = SMath.Clamp(b.Origin.Y / _size, 0, _bh - 1), y1 = SMath.Clamp((b.Origin.Y + b.H) / _size, 0, _bh - 1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (!_buildings[y * _bw + x].Contains(b)) _buildings[y * _bw + x].Add(b);
        }

        public void RemoveBuilding(Building b)
        {
            foreach (var l in _buildings) l.Remove(b);
        }

        public IEnumerable<Unit> UnitsNear(Vec2 p, float r, World w)
        {
            int x0 = SMath.Clamp((int)((p.X - r) / _size), 0, _bw - 1), x1 = SMath.Clamp((int)((p.X + r) / _size), 0, _bw - 1);
            int y0 = SMath.Clamp((int)((p.Y - r) / _size), 0, _bh - 1), y1 = SMath.Clamp((int)((p.Y + r) / _size), 0, _bh - 1);
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var l = _units[y * _bw + x];
                    for (int i = 0; i < l.Count; i++)
                    {
                        var u = l[i];
                        if (u.Dead) continue;
                        if (Vec2.SqrDistance(u.Pos, p) <= r2) yield return u;
                    }
                }
        }

        /// <summary>Fills a list (no allocation of enumerators) with units within radius.</summary>
        public void UnitsNear(Vec2 p, float r, List<Unit> result)
        {
            result.Clear();
            int x0 = SMath.Clamp((int)((p.X - r) / _size), 0, _bw - 1), x1 = SMath.Clamp((int)((p.X + r) / _size), 0, _bw - 1);
            int y0 = SMath.Clamp((int)((p.Y - r) / _size), 0, _bh - 1), y1 = SMath.Clamp((int)((p.Y + r) / _size), 0, _bh - 1);
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var l = _units[y * _bw + x];
                    for (int i = 0; i < l.Count; i++)
                    {
                        var u = l[i];
                        if (!u.Dead && Vec2.SqrDistance(u.Pos, p) <= r2) result.Add(u);
                    }
                }
        }

        public void BuildingsNear(Vec2 p, float r, List<Building> result)
        {
            result.Clear();
            int x0 = SMath.Clamp((int)((p.X - r) / _size), 0, _bw - 1), x1 = SMath.Clamp((int)((p.X + r) / _size), 0, _bw - 1);
            int y0 = SMath.Clamp((int)((p.Y - r) / _size), 0, _bh - 1), y1 = SMath.Clamp((int)((p.Y + r) / _size), 0, _bh - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var l = _buildings[y * _bw + x];
                    for (int i = 0; i < l.Count; i++)
                    {
                        var b = l[i];
                        if (b.Dead || result.Contains(b)) continue;
                        if (b.DistanceTo(p) <= r) result.Add(b);
                    }
                }
        }
    }
}
