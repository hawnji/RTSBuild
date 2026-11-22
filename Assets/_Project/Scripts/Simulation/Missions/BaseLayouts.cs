using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public enum BasePreset { Outpost, Small, Medium, Large, Fortress, Airbase, NavalBase, SuperweaponSite }

    /// <summary>Prefabricated base layouts used by missions (and by skirmish "small base" starts).</summary>
    public static class BaseLayouts
    {
        struct Slot { public BuildingRole Role; public int X, Y; public Slot(BuildingRole r, int x, int y) { Role = r; X = x; Y = y; } }

        static readonly Dictionary<BasePreset, Slot[]> Layouts = new Dictionary<BasePreset, Slot[]>
        {
            [BasePreset.Outpost] = new[]
            {
                new Slot(BuildingRole.Barracks, -1, -1), new Slot(BuildingRole.Power, 2, -1), new Slot(BuildingRole.Defense, -3, 2), new Slot(BuildingRole.Defense, 4, 2),
            },
            [BasePreset.Small] = new[]
            {
                new Slot(BuildingRole.ConstructionYard, -1, -1), new Slot(BuildingRole.Power, 3, -1), new Slot(BuildingRole.Refinery, -1, -6),
                new Slot(BuildingRole.Barracks, 3, 2), new Slot(BuildingRole.Defense, -4, 3), new Slot(BuildingRole.Defense, 5, -4),
            },
            [BasePreset.Medium] = new[]
            {
                new Slot(BuildingRole.ConstructionYard, -1, -1), new Slot(BuildingRole.Power, 3, -1), new Slot(BuildingRole.Power, 3, 2),
                new Slot(BuildingRole.Refinery, -1, -6), new Slot(BuildingRole.Barracks, -4, 2), new Slot(BuildingRole.Factory, 6, -2),
                new Slot(BuildingRole.Radar, -5, -2), new Slot(BuildingRole.Defense, -6, 5), new Slot(BuildingRole.Defense, 8, 4), new Slot(BuildingRole.Defense, 2, 6),
            },
            [BasePreset.Large] = new[]
            {
                new Slot(BuildingRole.ConstructionYard, -1, -1), new Slot(BuildingRole.Power, 3, -1), new Slot(BuildingRole.Power, 3, 2), new Slot(BuildingRole.AdvancedPower, -5, 6),
                new Slot(BuildingRole.Refinery, -1, -6), new Slot(BuildingRole.Refinery, 4, -7), new Slot(BuildingRole.Barracks, -4, 2), new Slot(BuildingRole.Factory, 6, -2),
                new Slot(BuildingRole.Radar, -5, -2), new Slot(BuildingRole.TechCenter, 0, 3), new Slot(BuildingRole.Hangar, 6, 3), new Slot(BuildingRole.RepairDepot, -9, -2),
                new Slot(BuildingRole.Defense, -8, 7), new Slot(BuildingRole.Defense, 9, 6), new Slot(BuildingRole.Defense, 3, 8), new Slot(BuildingRole.Defense, -9, -6), new Slot(BuildingRole.Defense, 9, -6),
            },
            [BasePreset.Fortress] = new[]
            {
                new Slot(BuildingRole.ConstructionYard, -1, -1), new Slot(BuildingRole.Power, 3, -1), new Slot(BuildingRole.Power, 3, 2), new Slot(BuildingRole.AdvancedPower, -5, 6),
                new Slot(BuildingRole.AdvancedPower, 7, 7), new Slot(BuildingRole.Refinery, -1, -6), new Slot(BuildingRole.Barracks, -4, 2), new Slot(BuildingRole.Factory, 6, -2),
                new Slot(BuildingRole.Radar, -5, -2), new Slot(BuildingRole.TechCenter, 0, 3), new Slot(BuildingRole.AdvancedFactory, -10, 1), new Slot(BuildingRole.ResearchLab, 3, 6),
                new Slot(BuildingRole.Countermeasure, -2, 7),
                new Slot(BuildingRole.Defense, -8, 9), new Slot(BuildingRole.Defense, 10, 10), new Slot(BuildingRole.Defense, 3, 10), new Slot(BuildingRole.Defense, -11, -5),
                new Slot(BuildingRole.Defense, 10, -6), new Slot(BuildingRole.Defense, -4, -9), new Slot(BuildingRole.Defense, 5, -10), new Slot(BuildingRole.Defense, 12, 2),
            },
            [BasePreset.Airbase] = new[]
            {
                new Slot(BuildingRole.Power, -3, -1), new Slot(BuildingRole.Hangar, 0, -1), new Slot(BuildingRole.Hangar, 0, 2), new Slot(BuildingRole.Radar, 4, -1),
                new Slot(BuildingRole.Power, 4, 2), new Slot(BuildingRole.Defense, -4, 4), new Slot(BuildingRole.Defense, 5, 5), new Slot(BuildingRole.Defense, 1, -4),
            },
            [BasePreset.NavalBase] = new[]
            {
                new Slot(BuildingRole.Power, -3, -1), new Slot(BuildingRole.Barracks, 1, -1), new Slot(BuildingRole.Radar, 4, -1), new Slot(BuildingRole.Defense, -4, 3), new Slot(BuildingRole.Defense, 5, 3),
            },
            [BasePreset.SuperweaponSite] = new[]
            {
                new Slot(BuildingRole.Superweapon, -1, -1), new Slot(BuildingRole.AdvancedPower, 3, -1), new Slot(BuildingRole.AdvancedPower, -5, -1),
                new Slot(BuildingRole.Countermeasure, 3, 2), new Slot(BuildingRole.Defense, -4, 4), new Slot(BuildingRole.Defense, 4, 5), new Slot(BuildingRole.Defense, 0, 5),
                new Slot(BuildingRole.Defense, -5, -4), new Slot(BuildingRole.Defense, 5, -4),
            },
        };

        static readonly string[] DefenseCycleCA = { "ca_aiturret", "ca_avturret", "ca_mgbunker", "ca_aadefense", "ca_avturret", "ca_artillery" };
        static readonly string[] DefenseCycleID = { "id_mgbunker", "id_avturret", "id_aiturret", "id_aadefense", "id_avturret", "id_artillery" };

        public static void Build(MissionRunner m, Player p, int cx, int cy, BasePreset preset, string tagPrefix)
        {
            var slots = Layouts[preset];
            int di = 0;
            foreach (var s in slots)
            {
                string defId;
                if (s.Role == BuildingRole.Defense)
                {
                    var cyc = p.Faction == FactionId.Alliance ? DefenseCycleCA : DefenseCycleID;
                    defId = cyc[di++ % cyc.Length];
                    if (preset == BasePreset.Outpost || preset == BasePreset.Small) defId = cyc[di % 3];
                }
                else
                {
                    var def = Catalog.BuildingForRole(p.Faction, s.Role);
                    if (def == null) continue;
                    defId = def.Id;
                }
                var bd = Catalog.Building(defId);
                var b = m.Build(p, defId, cx + s.X, cy + s.Y, tagPrefix != null ? tagPrefix + "_" + s.Role.ToString().ToLower() : null);
                if (b == null)
                {
                    // Try nearby spots if the slot is blocked.
                    for (int k = 1; k < 4 && b == null; k++)
                        b = m.Build(p, defId, cx + s.X + k, cy + s.Y + k, tagPrefix != null ? tagPrefix + "_" + s.Role.ToString().ToLower() : null);
                }
                if (b != null && s.Role == BuildingRole.Refinery)
                {
                    var hv = Catalog.Faction(p.Faction).HarvesterUnit;
                    var dock = m.W.Economy.DockPosition(b);
                    var u = m.Spawn(p, hv, dock.X, dock.Y - 1);
                    m.W.Commands.Harvest(new[] { u.Id }, p.Id, Vec2.Zero);
                }
            }
        }
    }
}
