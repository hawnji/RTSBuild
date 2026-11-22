using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    [Serializable]
    public class SlotConfig
    {
        public bool Enabled = true;
        public bool Human;
        public FactionId Faction = FactionId.Alliance;
        public int Team;
        public AIDifficulty Difficulty = AIDifficulty.Normal;
        public int Color;
        public int StartIndex = -1;
        public string Name;
    }

    [Serializable]
    public class SkirmishConfig
    {
        public string MapId = "twin_rivers";
        public List<SlotConfig> Slots = new List<SlotConfig>();
        public GameSettings Settings = new GameSettings();
        public bool TeamGame;          // 2v2
        public bool Spectator;         // no human slot: watch AI vs AI

        public static SkirmishConfig Default1v1()
        {
            var c = new SkirmishConfig();
            c.Slots.Add(new SlotConfig { Human = true, Faction = FactionId.Alliance, Team = 0, Color = 0, Name = "Commander" });
            c.Slots.Add(new SlotConfig { Human = false, Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = AIDifficulty.Normal, Name = "Iron Marshal" });
            return c;
        }

        public static SkirmishConfig Default2v2()
        {
            var c = new SkirmishConfig { MapId = "crossroads", TeamGame = true };
            c.Slots.Add(new SlotConfig { Human = true, Faction = FactionId.Alliance, Team = 0, Color = 0, Name = "Commander" });
            c.Slots.Add(new SlotConfig { Human = false, Faction = FactionId.Alliance, Team = 0, Color = 2, Difficulty = AIDifficulty.Normal, Name = "Allied Colonel" });
            c.Slots.Add(new SlotConfig { Human = false, Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = AIDifficulty.Normal, Name = "Iron Marshal" });
            c.Slots.Add(new SlotConfig { Human = false, Faction = FactionId.Dominion, Team = 1, Color = 3, Difficulty = AIDifficulty.Normal, Name = "Iron Warlord" });
            return c;
        }
    }

    /// <summary>Builds playable worlds for skirmish matches and campaign missions.</summary>
    public static class GameSetup
    {
        public static World CreateSkirmish(SkirmishConfig c)
        {
            Catalog.EnsureBuilt();
            var spec = MapLibrary.Find(c.MapId) ?? MapLibrary.Skirmish[0];
            var map = MapGenerator.Generate(spec);
            var w = new World(map, c.Settings);
            // Assign starting positions: team A starts on its designated side for team maps.
            var free = new List<int>();
            for (int i = 0; i < map.StartPositions.Count; i++) free.Add(i);
            var assigned = new Dictionary<SlotConfig, int>();
            var active = c.Slots.FindAll(s => s.Enabled);
            if (spec.TeamA.Count > 0 && active.Count > 2)
            {
                // Team 0 takes the TeamA starts, others take the rest.
                var teamAStarts = new List<int>(spec.TeamA);
                var teamBStarts = new List<int>();
                for (int i = 0; i < map.StartPositions.Count; i++) if (!spec.TeamA.Contains(i)) teamBStarts.Add(i);
                int firstTeam = active[0].Team;
                foreach (var s in active)
                {
                    var pool = s.Team == firstTeam ? teamAStarts : teamBStarts;
                    if (pool.Count == 0) pool = teamAStarts.Count > 0 ? teamAStarts : teamBStarts;
                    int idx = s.StartIndex >= 0 && pool.Contains(s.StartIndex) ? s.StartIndex : pool[0];
                    pool.Remove(idx);
                    assigned[s] = idx;
                }
            }
            else
            {
                foreach (var s in active)
                {
                    int idx = s.StartIndex >= 0 && free.Contains(s.StartIndex) ? s.StartIndex : free[0];
                    free.Remove(idx);
                    assigned[s] = idx;
                }
            }
            foreach (var s in active)
            {
                var p = w.AddPlayer(s.Name ?? (s.Human ? "Commander" : "AI"), s.Faction, s.Team, s.Human && !c.Spectator, s.Difficulty, s.Color);
                p.StartCell = map.StartPositions[assigned[s]];
            }
            PlaceNeutrals(w, spec);
            foreach (var p in w.Players)
            {
                if (p.IsNeutral) continue;
                PlaceStart(w, p, c.Settings);
                if (p.IsAI) w.AIs.Add(new SkirmishAI(w, p.Id));
            }
            if (c.Spectator) foreach (var p in w.Players) w.Fog.SetRevealAll(p.Team, true);
            w.Fog.Update();
            return w;
        }

        public static void PlaceNeutrals(World w, MapSpec spec)
        {
            if (spec.Neutrals.Count == 0) return;
            Player neutral = null;
            foreach (var p in w.Players) if (p.IsNeutral) neutral = p;
            if (neutral == null) neutral = w.AddPlayer("Civilians", FactionId.Neutral, -1, false);
            foreach (var n in spec.Neutrals)
            {
                var def = Catalog.Building(n.DefId);
                if (def == null) continue;
                if (!w.Production.CanPlace(neutral.Id, def, n.Cell, false, false))
                {
                    // Clear the spot (generator noise may have put trees there).
                    for (int y = 0; y < def.Height; y++)
                        for (int x = 0; x < def.Width; x++)
                        {
                            var t = w.Map.Get(n.Cell.X + x, n.Cell.Y + y);
                            if (t == Terrain.Forest || t == Terrain.Rock) w.Map.Set(n.Cell.X + x, n.Cell.Y + y, MapGenerator.BaseTerrain(w.Map.Theme));
                        }
                    if (!w.Production.CanPlace(neutral.Id, def, n.Cell, false, false)) continue;
                }
                var b = w.SpawnBuilding(n.DefId, neutral.Id, n.Cell, true);
                b.Tag = n.Tag;
            }
        }

        public static void PlaceStart(World w, Player p, GameSettings s)
        {
            var f = Catalog.Faction(p.Faction);
            var c = p.StartCell;
            bool ca = p.Faction == FactionId.Alliance;
            switch (s.StartBase)
            {
                case StartingBase.MCV:
                    w.SpawnUnit(f.StartingVehicle, p.Id, c.Center);
                    break;
                case StartingBase.ConstructionYard:
                    w.SpawnBuilding(ca ? "ca_hq" : "id_hq", p.Id, new Int2(c.X - 1, c.Y - 1));
                    break;
                case StartingBase.SmallBase:
                    w.SpawnBuilding(ca ? "ca_hq" : "id_hq", p.Id, new Int2(c.X - 1, c.Y - 1));
                    TryBuild(w, p, ca ? "ca_power" : "id_power", c, 3, -1);
                    TryBuild(w, p, ca ? "ca_barracks" : "id_barracks", c, -4, -1);
                    var r = TryBuild(w, p, ca ? "ca_refinery" : "id_refinery", c, -1, 3);
                    if (r == null) r = TryBuild(w, p, ca ? "ca_refinery" : "id_refinery", c, -1, -5);
                    if (r != null)
                    {
                        var dock = w.Economy.DockPosition(r);
                        var cell = w.FindFreeCellNear(dock, MoveClass.Vehicle, 4);
                        if (cell.HasValue) { var h = w.SpawnUnit(f.HarvesterUnit, p.Id, cell.Value.Center); w.Commands.Harvest(new[] { h.Id }, p.Id, Vec2.Zero); }
                    }
                    break;
            }
            var units = new List<string>();
            if (s.StartUnits >= StartingUnits.Light)
                units.AddRange(ca ? new[] { "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_wasp" } : new[] { "id_levy", "id_levy", "id_levy", "id_levy", "id_breacher", "id_jackal" });
            if (s.StartUnits >= StartingUnits.Heavy)
                units.AddRange(ca ? new[] { "ca_sabre", "ca_sabre", "ca_bulwark", "ca_lancer", "ca_sentinel", "ca_engineer" } : new[] { "id_ravager", "id_ravager", "id_ironclad", "id_breacher", "id_thresher", "id_sapper" });
            int i = 0;
            foreach (var id in units)
            {
                var ang = i * 0.7f;
                var pos = c.Center + Vec2.FromAngle(ang) * (3.5f + (i / 9) * 1.2f);
                var cell = w.FindFreeCellNear(pos, Catalog.Unit(id).Category == UnitCategory.Infantry ? MoveClass.Foot : MoveClass.Vehicle, 5);
                if (cell.HasValue) { var u = w.SpawnUnit(id, p.Id, cell.Value.Center); u.GuardPos = u.Pos; u.HasGuardPos = true; p.Stats.UnitsBuilt--; }
                i++;
            }
            p.Stats.UnitsBuilt = 0; p.Stats.BuildingsBuilt = 0;
        }

        static Building TryBuild(World w, Player p, string def, Int2 c, int dx, int dy)
        {
            var d = Catalog.Building(def);
            var o = new Int2(c.X + dx, c.Y + dy);
            if (w.Production.CanPlace(p.Id, d, o, false, false)) return w.SpawnBuilding(def, p.Id, o);
            var spot = w.Production.FindPlacement(p.Id, d, c.Center, 8, w.Rng, 1);
            return spot.HasValue ? w.SpawnBuilding(def, p.Id, spot.Value) : null;
        }

        /// <summary>Creates a campaign mission world. When restoring, initial entities are not spawned.</summary>
        public static World CreateMission(MissionDef def, AIDifficulty difficulty, bool restoring = false)
        {
            Catalog.EnsureBuilt();
            var map = MapGenerator.Generate(def.Map);
            var settings = new GameSettings { StartingCredits = def.StartingCredits, Fog = FogMode.Shroud, Superweapons = true, Seed = def.Map.Seed };
            var w = new World(map, settings);
            var m = new MissionRunner(w, def, difficulty);
            w.Mission = m;
            def.Players?.Invoke(m);
            if (m.Human == null) m.AddHuman();
            if (!restoring)
            {
                PlaceNeutrals(w, def.Map);
                def.Populate?.Invoke(m);
            }
            def.Script?.Invoke(m);
            foreach (var p in w.Players)
            {
                if (p.IsAI) w.AIs.Add(new SkirmishAI(w, p.Id));
                if (p.StartCell == default(Int2))
                {
                    // Derive a start cell from the first structure or unit for AI orientation.
                    foreach (var b in w.BuildingsOf(p.Id)) { p.StartCell = Int2.Floor(b.Center); break; }
                    if (p.StartCell == default(Int2)) foreach (var u in w.UnitsOf(p.Id)) { p.StartCell = Int2.Floor(u.Pos); break; }
                }
            }
            if (!map.StartPositions.Contains(m.Human.StartCell) && m.Human.StartCell != default(Int2)) { }
            w.Fog.Update();
            return w;
        }
    }
}
