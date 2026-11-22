using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>What an AI player knows about its enemies — gathered strictly through its team's fog of war.</summary>
    public class AIIntel
    {
        public class KnownBuilding
        {
            public int Id;
            public int Owner;
            public string DefId;
            public Vec2 Pos;
            public float LastSeen;
            public BuildingRole Role;
            public bool IsDefense;
            public int Cost;
        }

        readonly World _w;
        readonly int _player;
        public readonly Dictionary<int, KnownBuilding> Buildings = new Dictionary<int, KnownBuilding>();
        public float EnemyInfantry, EnemyVehicles, EnemyHeavy, EnemyAir, EnemyNaval, EnemyDefenses, EnemyStealth;
        public float EnemyArmyValue;
        public readonly List<Unit> VisibleEnemies = new List<Unit>();
        public readonly HashSet<int> ScoutedStarts = new HashSet<int>();
        public float LastUpdate;

        public AIIntel(World w, int player) { _w = w; _player = player; }

        int Team => _w.TeamOf(_player);

        public void Update()
        {
            LastUpdate = _w.Time;
            int team = Team;
            VisibleEnemies.Clear();
            float inf = 0, veh = 0, heavy = 0, air = 0, nav = 0, value = 0, stealth = 0;
            foreach (var u in _w.Units)
            {
                if (u.Dead || u.IsInside || !_w.IsEnemy(_player, u.Owner)) continue;
                if (!_w.Combat.VisibleTo(u, team)) continue;
                VisibleEnemies.Add(u);
                if (!u.IsArmed) continue;
                value += u.Def.Cost;
                switch (u.Def.Category)
                {
                    case UnitCategory.Infantry: inf += u.Def.Cost; break;
                    case UnitCategory.Vehicle: veh += u.Def.Cost; if (u.Def.Armor >= ArmorType.Heavy) heavy += u.Def.Cost; break;
                    case UnitCategory.Aircraft: air += u.Def.Cost; break;
                    case UnitCategory.Naval: nav += u.Def.Cost; break;
                }
                if (u.Def.Has(UnitAbility.Stealth)) stealth += u.Def.Cost;
            }
            // Exponential memory of enemy composition so production does not flip every second.
            const float k = 0.25f;
            EnemyInfantry = SMath.Lerp(EnemyInfantry, inf, k);
            EnemyVehicles = SMath.Lerp(EnemyVehicles, veh, k);
            EnemyHeavy = SMath.Lerp(EnemyHeavy, heavy, k);
            EnemyAir = SMath.Lerp(EnemyAir, air, k);
            EnemyNaval = SMath.Lerp(EnemyNaval, nav, k);
            EnemyStealth = SMath.Lerp(EnemyStealth, stealth, k);
            EnemyArmyValue = Math.Max(EnemyArmyValue * 0.97f, value);

            foreach (var b in _w.Buildings)
            {
                if (b.Dead || !_w.IsEnemy(_player, b.Owner)) continue;
                if (!_w.Fog.IsBuildingVisible(team, b)) continue;
                if (!Buildings.TryGetValue(b.Id, out var kb))
                {
                    kb = new KnownBuilding { Id = b.Id };
                    Buildings[b.Id] = kb;
                }
                kb.Owner = b.Owner; kb.DefId = b.DefId; kb.Pos = b.Center; kb.LastSeen = _w.Time; kb.Role = b.Def.Role; kb.IsDefense = b.IsDefense; kb.Cost = b.Def.Cost;
            }
            // Forget buildings whose location is visible but which no longer exist there.
            var remove = new List<int>();
            float defs = 0;
            foreach (var kb in Buildings.Values)
            {
                var real = _w.GetBuilding(kb.Id);
                if (real == null || real.Owner != kb.Owner)
                {
                    if (_w.Fog.IsVisible(team, kb.Pos) || real == null && _w.Time - kb.LastSeen > 240) remove.Add(kb.Id);
                    continue;
                }
                if (kb.IsDefense) defs += kb.Cost;
            }
            foreach (var id in remove) Buildings.Remove(id);
            EnemyDefenses = defs;

            for (int i = 0; i < _w.Map.StartPositions.Count; i++)
                if (_w.Fog.IsVisible(team, _w.Map.StartPositions[i].Center)) ScoutedStarts.Add(i);
        }

        public float Total => EnemyInfantry + EnemyVehicles + EnemyAir + EnemyNaval + 1f;

        public KnownBuilding BestTarget(Vec2 from, bool preferEconomy, int targetOwner = -1)
        {
            KnownBuilding best = null; float bs = float.MinValue;
            foreach (var kb in Buildings.Values)
            {
                if (targetOwner >= 0 && kb.Owner != targetOwner) continue;
                if (kb.Role == BuildingRole.Wall || kb.Role == BuildingRole.Gate) continue;
                var owner = _w.GetPlayer(kb.Owner);
                if (owner == null || owner.IsNeutral) continue;
                float d = Vec2.Distance(from, kb.Pos);
                float score = -d;
                if (preferEconomy && (kb.Role == BuildingRole.Refinery || kb.Role == BuildingRole.Power)) score += 30;
                if (kb.Role == BuildingRole.ConstructionYard) score += 15;
                if (kb.Role == BuildingRole.Superweapon) score += 40;
                if (kb.IsDefense) score -= 10;
                if (score > bs) { bs = score; best = kb; }
            }
            return best;
        }

        /// <summary>Best location for an area attack: maximizes known enemy structure value within radius.</summary>
        public Vec2? BestStrikeLocation(float radius, out float value)
        {
            value = 0;
            Vec2? best = null;
            foreach (var a in Buildings.Values)
            {
                float v = 0;
                foreach (var b in Buildings.Values)
                    if (Vec2.Distance(a.Pos, b.Pos) <= radius) v += b.Cost * (b.Role == BuildingRole.Superweapon || b.Role == BuildingRole.ConstructionYard ? 2f : 1f);
                if (v > value) { value = v; best = a.Pos; }
            }
            return best;
        }
    }

    /// <summary>Shared coordination state for AI teammates (2v2): joint targets, threats and resource claims.</summary>
    public class TeamBlackboard
    {
        static readonly Dictionary<World, Dictionary<int, TeamBlackboard>> _boards = new Dictionary<World, Dictionary<int, TeamBlackboard>>();

        public int TargetOwner = -1;
        public Vec2 TargetPos;
        public float TargetSetTime = -999;
        public float AttackLaunchTime = -999;
        public readonly Dictionary<int, Vec2> ThreatenedBases = new Dictionary<int, Vec2>();   // player -> threat position
        public readonly Dictionary<int, float> ThreatTime = new Dictionary<int, float>();
        public readonly HashSet<long> ClaimedFields = new HashSet<long>();

        public static TeamBlackboard For(World w, int team)
        {
            if (!_boards.TryGetValue(w, out var d))
            {
                // Avoid unbounded growth across matches.
                if (_boards.Count > 8) _boards.Clear();
                d = new Dictionary<int, TeamBlackboard>();
                _boards[w] = d;
            }
            if (!d.TryGetValue(team, out var b)) { b = new TeamBlackboard(); d[team] = b; }
            return b;
        }

        public void ReportThreat(int player, Vec2 pos, float time)
        {
            ThreatenedBases[player] = pos;
            ThreatTime[player] = time;
        }

        public bool AnyThreat(int exceptPlayer, float now, out int player, out Vec2 pos)
        {
            foreach (var kv in ThreatenedBases)
            {
                if (kv.Key == exceptPlayer) continue;
                if (ThreatTime.TryGetValue(kv.Key, out var t) && now - t < 15f) { player = kv.Key; pos = kv.Value; return true; }
            }
            player = -1; pos = Vec2.Zero;
            return false;
        }
    }
}
