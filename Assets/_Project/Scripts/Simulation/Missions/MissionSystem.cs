using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public enum ObjectiveState { Hidden, Active, Completed, Failed }

    [Serializable]
    public class Objective
    {
        public string Id;
        public string Text;
        public bool Primary = true;
        public ObjectiveState State = ObjectiveState.Active;
    }

    [Serializable]
    public class MissionZone
    {
        public string Name;
        public Vec2 Center;
        public float Radius;
        public bool Contains(Vec2 p) => Vec2.Distance(p, Center) <= Radius;
    }

    public class MissionTrigger
    {
        public string Id;
        public Func<bool> Condition;
        public Action Action;
        public bool Repeat;
        public float Interval;   // seconds between repeat firings
        public float NextAllowed;
        public bool Fired;
    }

    [Serializable]
    public class MissionCountdown
    {
        public string Label;
        public float EndTime;
        public bool Visible = true;
    }

    /// <summary>Static description of a campaign mission. Setup delegates build the scenario through MissionRunner.</summary>
    public class MissionDef
    {
        public string Id;
        public FactionId Faction;
        public int Index;            // 1-based within its campaign
        public string Title;
        public string Location;
        public string Date;
        public string Briefing;
        public string VictoryText;
        public string DefeatText;
        public string Music = "battle";
        public bool IsCinematic;
        public bool NoBase;          // player starts without structures; defeat if all forces lost
        public MapSpec Map;
        public int StartingCredits = 5000;
        public FactionId EnemyFaction;
        /// <summary>Creates players. Always runs (also when loading a save).</summary>
        public Action<MissionRunner> Players;
        /// <summary>Spawns the initial entities. Skipped when restoring from a save.</summary>
        public Action<MissionRunner> Populate;
        /// <summary>Objectives, zones and triggers. Always runs.</summary>
        public Action<MissionRunner> Script;
        public List<string> Tags = new List<string>(); // mission type tags for documentation (e.g. "defend", "escort")
    }

    [Serializable]
    public class MissionSaveState
    {
        public string MissionId;
        public List<string> FiredTriggers = new List<string>();
        public List<string> FlagKeys = new List<string>();
        public List<int> FlagValues = new List<int>();
        public List<Objective> Objectives = new List<Objective>();
        public List<MissionCountdown> Countdowns = new List<MissionCountdown>();
        public float EndTimer;
        public int Result;
    }

    /// <summary>
    /// Runs a mission script: trigger evaluation, objectives, timers, reinforcements, scripted attacks, dialogue,
    /// cinematic camera cues, and mission success/failure.
    /// </summary>
    public class MissionRunner
    {
        public readonly World W;
        public readonly MissionDef Def;
        public Player Human;
        public readonly Dictionary<string, Player> PlayersByKey = new Dictionary<string, Player>();
        public readonly List<Objective> Objectives = new List<Objective>();
        public readonly Dictionary<string, MissionZone> Zones = new Dictionary<string, MissionZone>();
        public readonly List<MissionTrigger> Triggers = new List<MissionTrigger>();
        public readonly Dictionary<string, int> Flags = new Dictionary<string, int>();
        public readonly List<MissionCountdown> Countdowns = new List<MissionCountdown>();
        public int Result;           // 0 running, 1 victory, -1 defeat
        public string ResultReason;
        float _endTimer = -1;
        public bool AutoWinOnPrimaries = true;
        public AIDifficulty Difficulty;

        public MissionRunner(World w, MissionDef def, AIDifficulty difficulty)
        {
            W = w; Def = def; Difficulty = difficulty;
        }

        // ------------------------------------------------------------------ Lifecycle
        public void Update()
        {
            if (Result != 0)
            {
                if (_endTimer > 0)
                {
                    _endTimer -= World.Dt;
                    if (_endTimer <= 0)
                    {
                        W.GameOver = true;
                        W.WinningTeam = Result > 0 ? Human.Team : -2;
                        W.Emit(new SimEvent { Type = SimEventType.GameOver, Player = -1, Value = Result, Text = ResultReason });
                    }
                }
                return;
            }
            for (int i = 0; i < Triggers.Count; i++)
            {
                var t = Triggers[i];
                if (t.Fired && !t.Repeat) continue;
                if (t.Repeat && W.Time < t.NextAllowed) continue;
                bool ok;
                try { ok = t.Condition == null || t.Condition(); }
                catch (Exception ex) { W.Emit(new SimEvent { Type = SimEventType.Hint, Player = -1, Text = "Trigger " + t.Id + " error: " + ex.Message }); ok = false; t.Fired = true; }
                if (!ok) continue;
                t.Fired = true;
                t.NextAllowed = W.Time + Math.Max(0.1f, t.Interval);
                try { t.Action?.Invoke(); }
                catch (Exception ex) { W.Emit(new SimEvent { Type = SimEventType.Hint, Player = -1, Text = "Trigger " + t.Id + " action error: " + ex.Message }); }
                if (Result != 0) return;
            }
            // Default outcome rules
            if (Human != null)
            {
                bool hasAnything = false;
                foreach (var b in W.Buildings) if (!b.Dead && b.Owner == Human.Id && !b.IsWallLike) { hasAnything = true; break; }
                if (!hasAnything) foreach (var u in W.Units) if (!u.Dead && u.Owner == Human.Id) { hasAnything = true; break; }
                if (!hasAnything && W.Time > 5f && !Def.IsCinematic) { Lose("All forces lost."); return; }
            }
            if (AutoWinOnPrimaries && Objectives.Count > 0)
            {
                bool allDone = true, anyFailed = false;
                foreach (var o in Objectives)
                {
                    if (!o.Primary) continue;
                    if (o.State == ObjectiveState.Failed) anyFailed = true;
                    if (o.State != ObjectiveState.Completed) allDone = false;
                }
                if (anyFailed) Lose("A primary objective failed.");
                else if (allDone) Win();
            }
        }

        public bool UsesSkirmishDefeat(Player p) => p != Human;

        public void Win(string reason = "Mission accomplished.")
        {
            if (Result != 0) return;
            Result = 1; ResultReason = reason; _endTimer = 3f;
            W.Emit(new SimEvent { Type = SimEventType.MissionMessage, Player = -1, Text = Def.VictoryText ?? reason, Text2 = "Command" });
        }

        public void Lose(string reason = "Mission failed.")
        {
            if (Result != 0) return;
            Result = -1; ResultReason = reason; _endTimer = 3f;
            W.Emit(new SimEvent { Type = SimEventType.MissionMessage, Player = -1, Text = reason, Text2 = "Command" });
        }

        // ------------------------------------------------------------------ Players
        public Player AddHuman(string name = "Commander")
        {
            Human = W.AddPlayer(name, Def.Faction, 0, true);
            Human.Credits = Def.StartingCredits;
            PlayersByKey["player"] = Human;
            return Human;
        }

        public Player AddAI(string key, string name, FactionId f, int team, bool builds = true, bool attacks = true, float aggression = 1f)
        {
            var p = W.AddPlayer(name, f, team, false, Difficulty);
            p.AIBuilds = builds; p.AIAttacks = attacks; p.AIAggression = aggression;
            p.Credits = builds ? 8000 : 0;
            PlayersByKey[key] = p;
            return p;
        }

        public Player AddNeutral()
        {
            var p = W.AddPlayer("Civilians", FactionId.Neutral, -1, false);
            PlayersByKey["neutral"] = p;
            return p;
        }

        public Player P(string key) => PlayersByKey.TryGetValue(key, out var p) ? p : null;

        // ------------------------------------------------------------------ Objectives
        public Objective AddObjective(string id, string text, bool primary = true, bool hidden = false)
        {
            var o = new Objective { Id = id, Text = text, Primary = primary, State = hidden ? ObjectiveState.Hidden : ObjectiveState.Active };
            Objectives.Add(o);
            if (!hidden) W.Emit(new SimEvent { Type = SimEventType.ObjectiveUpdated, Player = -1, Text = id, Text2 = text, Value = (int)o.State });
            return o;
        }

        Objective FindObj(string id) { foreach (var o in Objectives) if (o.Id == id) return o; return null; }

        public void SetObjective(string id, ObjectiveState s)
        {
            var o = FindObj(id);
            if (o == null || o.State == s) return;
            if (o.State == ObjectiveState.Completed || o.State == ObjectiveState.Failed) return;
            o.State = s;
            W.Emit(new SimEvent { Type = SimEventType.ObjectiveUpdated, Player = -1, Text = id, Text2 = o.Text, Value = (int)s });
        }

        public void Complete(string id) => SetObjective(id, ObjectiveState.Completed);
        public void Fail(string id) => SetObjective(id, ObjectiveState.Failed);
        public void Reveal(string id) => SetObjective(id, ObjectiveState.Active);
        public bool IsComplete(string id) => FindObj(id)?.State == ObjectiveState.Completed;
        public bool IsActive(string id) => FindObj(id)?.State == ObjectiveState.Active;

        // ------------------------------------------------------------------ Triggers
        public MissionTrigger When(string id, Func<bool> cond, Action act, bool repeat = false, float interval = 1f)
        {
            var t = new MissionTrigger { Id = id, Condition = cond, Action = act, Repeat = repeat, Interval = interval };
            Triggers.Add(t);
            return t;
        }

        public MissionTrigger At(float seconds, string id, Action act) => When(id, () => W.Time >= seconds, act);

        public MissionTrigger Every(float seconds, string id, Func<bool> cond, Action act)
        {
            var t = When(id, cond, act, true, seconds);
            t.NextAllowed = seconds;
            return t;
        }

        public bool Fired(string id) { foreach (var t in Triggers) if (t.Id == id) return t.Fired; return false; }
        public int Flag(string k) => Flags.TryGetValue(k, out var v) ? v : 0;
        public void SetFlag(string k, int v = 1) => Flags[k] = v;

        public void Countdown(string label, float seconds, bool visible = true)
        {
            Countdowns.RemoveAll(c => c.Label == label);
            Countdowns.Add(new MissionCountdown { Label = label, EndTime = W.Time + seconds, Visible = visible });
            W.Emit(new SimEvent { Type = SimEventType.TimerStarted, Player = -1, Text = label, Value = seconds });
        }

        public float TimeLeft(string label)
        {
            foreach (var c in Countdowns) if (c.Label == label) return Math.Max(0, c.EndTime - W.Time);
            return -1;
        }

        public bool CountdownExpired(string label)
        {
            foreach (var c in Countdowns) if (c.Label == label) return W.Time >= c.EndTime;
            return false;
        }

        public void ClearCountdown(string label) => Countdowns.RemoveAll(c => c.Label == label);

        // ------------------------------------------------------------------ Zones
        public MissionZone Zone(string name, float x, float y, float r)
        {
            var z = new MissionZone { Name = name, Center = new Vec2(x, y), Radius = r };
            Zones[name] = z;
            return z;
        }

        public int CountInZone(string zone, Player p, Func<Unit, bool> filter = null)
        {
            if (!Zones.TryGetValue(zone, out var z)) return 0;
            int n = 0;
            foreach (var u in W.Units)
                if (!u.Dead && !u.IsInside && u.Owner == p.Id && z.Contains(u.Pos) && (filter == null || filter(u))) n++;
            return n;
        }

        public int CountTeamInZone(string zone, int team, bool includeBuildings = false)
        {
            if (!Zones.TryGetValue(zone, out var z)) return 0;
            int n = 0;
            foreach (var u in W.Units)
                if (!u.Dead && !u.IsInside && W.TeamOf(u.Owner) == team && z.Contains(u.Pos) && u.IsArmed) n++;
            if (includeBuildings)
                foreach (var b in W.Buildings)
                    if (!b.Dead && W.TeamOf(b.Owner) == team && z.Contains(b.Center) && !b.IsWallLike) n++;
            return n;
        }

        public int EnemiesInZone(string zone) => CountTeamInZoneEnemies(zone, Human.Team);

        int CountTeamInZoneEnemies(string zone, int team)
        {
            if (!Zones.TryGetValue(zone, out var z)) return 0;
            int n = 0;
            foreach (var u in W.Units)
                if (!u.Dead && !u.IsInside && W.IsEnemy(Human.Id, u.Owner) && z.Contains(u.Pos)) n++;
            return n;
        }

        // ------------------------------------------------------------------ Entities
        public Unit Spawn(Player p, string defId, float x, float y, string tag = null, float facing = -1.57f)
        {
            var c = new Vec2(x, y);
            var u = Catalog.Unit(defId);
            var mc = u.Locomotor == Locomotor.Foot ? MoveClass.Foot : (u.Locomotor == Locomotor.Naval || u.Locomotor == Locomotor.Submarine) ? MoveClass.Naval :
                (u.Locomotor == Locomotor.Air || u.Locomotor == Locomotor.Helicopter) ? MoveClass.Air : MoveClass.Vehicle;
            if (mc != MoveClass.Air && !W.Passable((int)x, (int)y, mc))
            {
                var cell = W.FindFreeCellNear(c, mc, 8);
                if (cell.HasValue) c = cell.Value.Center;
            }
            var unit = W.SpawnUnit(defId, p.Id, c, facing);
            unit.Tag = tag;
            unit.GuardPos = unit.Pos; unit.HasGuardPos = true;
            if (unit.IsAir) { unit.AState = AirState.Flying; unit.MoveGoal = unit.Pos; }
            if (p.IsHuman) p.Stats.UnitsBuilt--; // starting forces are not "built"
            return unit;
        }

        public List<Unit> SpawnGroup(Player p, float x, float y, string tag, params string[] defs)
        {
            var list = new List<Unit>();
            int i = 0;
            foreach (var d in defs)
            {
                float ox = (i % 4) * 1.1f - 1.6f, oy = (i / 4) * 1.1f;
                list.Add(Spawn(p, d, x + ox, y + oy, tag));
                i++;
            }
            return list;
        }

        public Building Build(Player p, string defId, int x, int y, string tag = null)
        {
            var def = Catalog.Building(defId);
            if (def == null) return null;
            // Clear blocking terrain for scripted placement.
            for (int dy = 0; dy < def.Height; dy++)
                for (int dx = 0; dx < def.Width; dx++)
                {
                    var t = W.Map.Get(x + dx, y + dy);
                    if (def.OnWater) { if (t != Terrain.Water) W.Map.Set(x + dx, y + dy, Terrain.Water); }
                    else if (t == Terrain.Forest || t == Terrain.Rock || t == Terrain.Mountain || t == Terrain.Cliff || t == Terrain.Water || t == Terrain.Shallow)
                        W.Map.Set(x + dx, y + dy, MapGenerator.BaseTerrain(W.Map.Theme));
                    if (W.Map.InBounds(x + dx, y + dy)) W.Map.Ore[W.Map.Index(x + dx, y + dy)] = 0;
                }
            if (W.BuildingAtCell(x, y) != null) return null;
            var b = W.SpawnBuilding(defId, p.Id, new Int2(x, y), true);
            b.Tag = tag;
            if (p.IsHuman) p.Stats.BuildingsBuilt--;
            return b;
        }

        /// <summary>Lays down a complete faction base around a center using a preset layout.</summary>
        public void BuildBase(Player p, int cx, int cy, BasePreset preset, string tagPrefix = null)
        {
            BaseLayouts.Build(this, p, cx, cy, preset, tagPrefix);
        }

        public List<Entity> Tagged(string tag)
        {
            var l = new List<Entity>();
            foreach (var u in W.Units) if (!u.Dead && u.Tag == tag) l.Add(u);
            foreach (var b in W.Buildings) if (!b.Dead && b.Tag == tag) l.Add(b);
            return l;
        }

        public int CountTagged(string tag)
        {
            int n = 0;
            foreach (var u in W.Units) if (!u.Dead && u.Tag == tag) n++;
            foreach (var b in W.Buildings) if (!b.Dead && b.Tag == tag) n++;
            return n;
        }

        public bool AllDead(string tag) => CountTagged(tag) == 0;

        public bool OwnedBy(string tag, Player p)
        {
            var l = Tagged(tag);
            if (l.Count == 0) return false;
            foreach (var e in l) if (e.Owner != p.Id) return false;
            return true;
        }

        public int CountBuildings(Player p, bool excludeWalls = true)
        {
            int n = 0;
            foreach (var b in W.Buildings) if (!b.Dead && b.Owner == p.Id && (!excludeWalls || !b.IsWallLike)) n++;
            return n;
        }

        public int CountUnits(Player p, Func<Unit, bool> filter = null)
        {
            int n = 0;
            foreach (var u in W.Units) if (!u.Dead && u.Owner == p.Id && (filter == null || filter(u))) n++;
            return n;
        }

        public bool HasBuilt(Player p, BuildingRole role) => W.HasRole(p.Id, role, true);

        public bool IsDestroyed(Player p) => CountBuildings(p) == 0;

        public bool BaseDestroyed(Player p) => CountBuildings(p) == 0 && CountUnits(p, u => u.Def.Has(UnitAbility.DeployToHQ)) == 0;

        // ------------------------------------------------------------------ Actions
        public void Say(string speaker, string text) =>
            W.Emit(new SimEvent { Type = SimEventType.MissionMessage, Player = -1, Text = text, Text2 = speaker });

        public void Hint(string text) => W.Emit(new SimEvent { Type = SimEventType.Hint, Player = -1, Text = text });

        public void CameraTo(float x, float y, float seconds = 1.5f) =>
            W.Emit(new SimEvent { Type = SimEventType.CameraPan, Player = -1, Pos = new Vec2(x, y), Value = seconds });

        public void Cinematic(bool on)
        {
            W.Cinematic = on;
            W.Emit(new SimEvent { Type = on ? SimEventType.CinematicStart : SimEventType.CinematicEnd, Player = -1 });
        }

        public void Reveal(float x, float y, float r, float seconds = 30)
        {
            W.Fog.RevealZones.Add(new RevealZone { Team = Human.Team, Pos = new Vec2(x, y), Radius = r, TimeLeft = seconds });
        }

        /// <summary>Spawns units at a map edge point and sends them to a destination (attack-move for combat units).</summary>
        public List<Unit> Reinforce(Player p, float fromX, float fromY, float toX, float toY, string tag, params string[] defs)
        {
            var list = SpawnGroup(p, fromX, fromY, tag, defs);
            var ids = new List<int>();
            foreach (var u in list) ids.Add(u.Id);
            if (p.IsHuman) W.Commands.Move(ids, p.Id, new Vec2(toX, toY));
            else W.Commands.AttackMove(ids, p.Id, new Vec2(toX, toY));
            W.Emit(new SimEvent { Type = SimEventType.Reinforcements, Player = p.Id, Pos = new Vec2(fromX, fromY), Value = list.Count });
            return list;
        }

        /// <summary>Scripted attack wave from an AI player.</summary>
        public List<Unit> Attack(Player p, float fromX, float fromY, float targetX, float targetY, string tag, params string[] defs)
        {
            var list = SpawnGroup(p, fromX, fromY, tag ?? "wave", defs);
            var ids = new List<int>();
            foreach (var u in list) ids.Add(u.Id);
            W.Commands.AttackMove(ids, p.Id, new Vec2(targetX, targetY));
            return list;
        }

        public void SendTagged(string tag, float x, float y, bool attack = true)
        {
            foreach (var e in Tagged(tag))
                if (e is Unit u)
                {
                    if (attack) W.Commands.AttackMove(new[] { u.Id }, u.Owner, new Vec2(x, y));
                    else W.Commands.Move(new[] { u.Id }, u.Owner, new Vec2(x, y), false, false);
                }
        }

        public void GiveTagged(string tag, Player p)
        {
            foreach (var e in Tagged(tag)) W.ChangeOwner(e, p.Id);
        }

        public void GiveCredits(Player p, int amount) { p.Credits += amount; }

        // ------------------------------------------------------------------ Save state
        public MissionSaveState Capture()
        {
            var s = new MissionSaveState { MissionId = Def.Id, EndTimer = _endTimer, Result = Result };
            foreach (var t in Triggers) if (t.Fired) s.FiredTriggers.Add(t.Id);
            foreach (var kv in Flags) { s.FlagKeys.Add(kv.Key); s.FlagValues.Add(kv.Value); }
            foreach (var o in Objectives) s.Objectives.Add(new Objective { Id = o.Id, Text = o.Text, Primary = o.Primary, State = o.State });
            foreach (var c in Countdowns) s.Countdowns.Add(new MissionCountdown { Label = c.Label, EndTime = c.EndTime, Visible = c.Visible });
            return s;
        }

        public void Restore(MissionSaveState s)
        {
            foreach (var t in Triggers)
            {
                t.Fired = s.FiredTriggers.Contains(t.Id);
                if (t.Fired && t.Repeat) t.NextAllowed = W.Time + t.Interval;
            }
            Flags.Clear();
            for (int i = 0; i < s.FlagKeys.Count; i++) Flags[s.FlagKeys[i]] = s.FlagValues[i];
            foreach (var so in s.Objectives)
            {
                var o = FindObj(so.Id);
                if (o != null) o.State = so.State;
                else Objectives.Add(so);
            }
            Countdowns.Clear();
            Countdowns.AddRange(s.Countdowns);
            Result = s.Result; _endTimer = s.EndTimer;
        }
    }
}
