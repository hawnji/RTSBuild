using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public interface IAIController
    {
        int PlayerId { get; }
        void Update();
    }

    /// <summary>Evaluates defeat per player and team victory for skirmish rules. Missions add their own conditions.</summary>
    public class VictorySystem
    {
        readonly World _w;
        public VictorySystem(World w) { _w = w; }

        public bool HasLost(Player p)
        {
            if (p.IsNeutral) return false;
            var cond = _w.Mission != null ? VictoryCondition.DestroyStructures : _w.Settings.Victory;
            bool anyBuilding = false, anyYard = false, anyUnit = false, anyMcv = false;
            foreach (var b in _w.Buildings)
            {
                if (b.Dead || b.Owner != p.Id) continue;
                if (b.IsWallLike) continue;
                anyBuilding = true;
                if (b.Def.Role == BuildingRole.ConstructionYard) anyYard = true;
            }
            foreach (var u in _w.Units)
            {
                if (u.Dead || u.Owner != p.Id) continue;
                anyUnit = true;
                if (u.Def.Has(UnitAbility.DeployToHQ)) anyMcv = true;
            }
            switch (cond)
            {
                case VictoryCondition.Annihilation: return !anyBuilding && !anyUnit;
                case VictoryCondition.Commander: return !anyYard && !anyMcv;
                default: return !anyBuilding && !anyMcv;
            }
        }

        public void Update()
        {
            if (_w.GameOver || _w.Tick % World.TicksPerSecond != 0) return;
            if (_w.Time < 3f) return;
            foreach (var p in _w.Players)
            {
                if (p.Defeated || p.IsNeutral) continue;
                if (_w.Mission != null && !_w.Mission.UsesSkirmishDefeat(p)) continue;
                if (HasLost(p))
                {
                    p.Defeated = true;
                    foreach (var q in p.Queues) q.Clear();
                    _w.Emit(new SimEvent { Type = SimEventType.PlayerDefeated, Player = -1, OtherId = p.Id, Text = p.Name });
                }
            }
            if (_w.Mission != null) return; // missions decide the outcome
            var aliveTeams = new HashSet<int>();
            foreach (var p in _w.Players) if (!p.Defeated && !p.IsNeutral) aliveTeams.Add(p.Team);
            if (aliveTeams.Count <= 1)
            {
                _w.GameOver = true;
                foreach (var t in aliveTeams) _w.WinningTeam = t;
                _w.Emit(new SimEvent { Type = SimEventType.GameOver, Player = -1, Value = _w.WinningTeam });
            }
        }
    }
}
