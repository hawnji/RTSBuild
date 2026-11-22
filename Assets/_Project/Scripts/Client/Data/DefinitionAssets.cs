using System;
using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>Designer-tunable overrides for one unit. Only non-zero/non-empty fields override the catalog.</summary>
    [Serializable]
    public class UnitTuning
    {
        public string Id;
        public string DisplayName;
        public int Cost;
        public float BuildTime;
        public float Health;
        public float Speed;
        public float Sight;
        [TextArea] public string Description;
    }

    [Serializable]
    public class BuildingTuning
    {
        public string Id;
        public string DisplayName;
        public int Cost;
        public float BuildTime;
        public float Health;
        public int Power;
        public bool OverridePower;
        [TextArea] public string Description;
    }

    [Serializable]
    public class WeaponTuning
    {
        public string Id;
        public float Damage;
        public float Range;
        public float Cooldown;
        public float Splash;
    }

    public static class DefinitionOverrides
    {
        public static int Applied;

        public static void ApplyFromResources()
        {
            var db = Resources.Load<DefinitionDatabase>("RTSDefinitions");
            if (db != null && db.Enabled) Apply(db);
        }

        public static void Apply(DefinitionDatabase db)
        {
            Catalog.EnsureBuilt();
            Applied = 0;
            foreach (var t in db.Units)
            {
                var u = Catalog.Unit(t.Id); if (u == null) continue;
                if (!string.IsNullOrEmpty(t.DisplayName)) u.Name = t.DisplayName;
                if (t.Cost > 0) u.Cost = t.Cost;
                if (t.BuildTime > 0) u.BuildTime = t.BuildTime;
                if (t.Health > 0) u.Health = t.Health;
                if (t.Speed > 0) u.Speed = t.Speed;
                if (t.Sight > 0) u.Sight = t.Sight;
                if (!string.IsNullOrEmpty(t.Description)) u.Description = t.Description;
                Applied++;
            }
            foreach (var t in db.Buildings)
            {
                var b = Catalog.Building(t.Id); if (b == null) continue;
                if (!string.IsNullOrEmpty(t.DisplayName)) b.Name = t.DisplayName;
                if (t.Cost > 0) b.Cost = t.Cost;
                if (t.BuildTime > 0) b.BuildTime = t.BuildTime;
                if (t.Health > 0) b.Health = t.Health;
                if (t.OverridePower) b.Power = t.Power;
                if (!string.IsNullOrEmpty(t.Description)) b.Description = t.Description;
                Applied++;
            }
            foreach (var t in db.Weapons)
            {
                var w = Catalog.Weapon(t.Id); if (w == null) continue;
                if (t.Damage > 0) w.Damage = t.Damage;
                if (t.Range > 0) w.Range = t.Range;
                if (t.Cooldown > 0) w.Cooldown = t.Cooldown;
                if (t.Splash > 0) w.Splash = t.Splash;
                Applied++;
            }
            if (Applied > 0) Debug.Log("Applied " + Applied + " definition overrides from RTSDefinitions asset.");
        }

        /// <summary>Fills a database with the current catalog values (used by the editor exporter).</summary>
        public static void Fill(DefinitionDatabase db)
        {
            Catalog.EnsureBuilt();
            db.Units.Clear(); db.Buildings.Clear(); db.Weapons.Clear();
            foreach (var u in Catalog.Units.Values) db.Units.Add(new UnitTuning { Id = u.Id, DisplayName = u.Name, Cost = u.Cost, BuildTime = u.BuildTime, Health = u.Health, Speed = u.Speed, Sight = u.Sight, Description = u.Description });
            foreach (var b in Catalog.Buildings.Values) db.Buildings.Add(new BuildingTuning { Id = b.Id, DisplayName = b.Name, Cost = b.Cost, BuildTime = b.BuildTime, Health = b.Health, Power = b.Power, OverridePower = true, Description = b.Description });
            foreach (var w in Catalog.Weapons.Values) db.Weapons.Add(new WeaponTuning { Id = w.Id, Damage = w.Damage, Range = w.Range, Cooldown = w.Cooldown, Splash = w.Splash });
        }
    }
}
