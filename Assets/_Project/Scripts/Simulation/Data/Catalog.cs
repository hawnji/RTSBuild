using System;
using System.Collections.Generic;
using R = RTS.Sim.BuildingRole;

namespace RTS.Sim
{
    /// <summary>
    /// The authoritative, data-driven content database for both factions: warheads, weapons, units, buildings,
    /// research and support powers. Definitions are plain serializable classes so they can be exported to
    /// ScriptableObjects in the Unity editor (RTS ▸ Export Definition Assets) and overridden from there.
    /// </summary>
    public static class Catalog
    {
        public static readonly Dictionary<string, WarheadDef> Warheads = new Dictionary<string, WarheadDef>();
        public static readonly Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>();
        public static readonly Dictionary<string, UnitDef> Units = new Dictionary<string, UnitDef>();
        public static readonly Dictionary<string, BuildingDef> Buildings = new Dictionary<string, BuildingDef>();
        public static readonly Dictionary<string, TechDef> Techs = new Dictionary<string, TechDef>();
        public static readonly Dictionary<SupportPowerKind, SupportPowerDef> Powers = new Dictionary<SupportPowerKind, SupportPowerDef>();
        public static readonly Dictionary<FactionId, FactionDef> Factions = new Dictionary<FactionId, FactionDef>();

        static bool _built;

        public static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            BuildWarheads();
            BuildWeapons();
            BuildAllianceUnits();
            BuildDominionUnits();
            BuildBuildings();
            BuildNeutral();
            BuildTechs();
            BuildPowers();
            BuildFactions();
        }

        public static UnitDef Unit(string id) { EnsureBuilt(); return Units.TryGetValue(id, out var d) ? d : null; }
        public static BuildingDef Building(string id) { EnsureBuilt(); return Buildings.TryGetValue(id, out var d) ? d : null; }
        public static WeaponDef Weapon(string id) { EnsureBuilt(); return Weapons.TryGetValue(id, out var d) ? d : null; }
        public static WarheadDef Warhead(string id) { EnsureBuilt(); return Warheads.TryGetValue(id, out var d) ? d : Warheads["Bullet"]; }
        public static TechDef Tech(string id) { EnsureBuilt(); return Techs.TryGetValue(id, out var d) ? d : null; }
        public static FactionDef Faction(FactionId id) { EnsureBuilt(); return Factions[id]; }

        // ---------------------------------------------------------------- Warheads
        static void BuildWarheads()
        {
            //                                   inf  hInf light med  heavy air  naval bld  wall
            AddW(new WarheadDef("Bullet",        1.0f, 0.6f, 0.35f,0.2f, 0.1f, 0.5f, 0.15f,0.15f,0.05f));
            AddW(new WarheadDef("HeavyBullet",   1.0f, 0.8f, 0.7f, 0.4f, 0.25f,0.7f, 0.3f, 0.25f,0.1f));
            AddW(new WarheadDef("Sniper",        3.0f, 1.6f, 0.1f, 0.05f,0.02f,0.0f, 0.05f,0.02f,0.0f));
            AddW(new WarheadDef("AP",            0.3f, 0.45f,1.0f, 1.0f, 0.9f, 0.5f, 0.8f, 0.55f,0.4f));
            AddW(new WarheadDef("HE",            1.0f, 0.9f, 0.7f, 0.5f, 0.4f, 0.0f, 0.5f, 0.9f, 0.8f));
            AddW(new WarheadDef("Flame",         1.5f, 1.1f, 0.6f, 0.35f,0.2f, 0.0f, 0.2f, 0.7f, 0.2f));
            AddW(new WarheadDef("Flak",          0.3f, 0.2f, 0.2f, 0.1f, 0.05f,1.0f, 0.1f, 0.05f,0.0f));
            AddW(new WarheadDef("AAMissile",     0.0f, 0.0f, 0.1f, 0.1f, 0.05f,1.25f,0.1f, 0.0f, 0.0f));
            AddW(new WarheadDef("Torpedo",       0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.5f, 0.7f, 0.0f));
            AddW(new WarheadDef("NavalShell",    0.9f, 0.9f, 0.8f, 0.7f, 0.6f, 0.0f, 0.8f, 1.1f, 1.0f));
            AddW(new WarheadDef("Rail",          0.5f, 0.6f, 1.1f, 1.2f, 1.25f,0.3f, 1.0f, 0.7f, 0.6f));
            AddW(new WarheadDef("Siege",         0.6f, 0.6f, 0.6f, 0.7f, 0.8f, 0.0f, 0.8f, 1.6f, 1.5f));
            AddW(new WarheadDef("Bomb",          0.8f, 0.8f, 0.8f, 0.8f, 0.7f, 0.0f, 0.7f, 1.25f,1.0f));
            AddW(new WarheadDef("Demolition",    0.0f, 0.0f, 2.0f, 2.0f, 2.0f, 0.0f, 2.0f, 10f,  5.0f));
            AddW(new WarheadDef("Arc",           0.6f, 0.6f, 1.0f, 0.9f, 0.8f, 0.0f, 0.8f, 0.4f, 0.2f));
            AddW(new WarheadDef("Missile",       0.4f, 0.5f, 0.9f, 0.9f, 0.8f, 0.6f, 0.9f, 0.8f, 0.5f));
            AddW(new WarheadDef("Super",         1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f));
        }

        static void AddW(WarheadDef w) => Warheads[w.Id] = w;

        // ---------------------------------------------------------------- Weapons
        static WeaponDef Wp(string id, string name, float dmg, float range, float cooldown, ProjectileKind proj, string warhead,
            TargetLayer targets, string sound, float splash = 0, float speed = 30, int burst = 1, float inacc = 0,
            int clip = 0, float reload = 0, float minRange = 0, bool ff = false, float suppression = 0)
        {
            var w = new WeaponDef
            {
                Id = id, Name = name, Damage = dmg, Range = range, Cooldown = cooldown, Projectile = proj, Warhead = warhead,
                Targets = targets, Sound = sound, Splash = splash, ProjectileSpeed = speed, Burst = burst, Inaccuracy = inacc,
                Clip = clip, ReloadTime = reload, MinRange = minRange, FriendlyFire = ff, Suppression = suppression
            };
            Weapons[id] = w;
            return w;
        }

        const TargetLayer G = TargetLayer.Ground | TargetLayer.Structure;
        const TargetLayer GN = TargetLayer.Ground | TargetLayer.Structure | TargetLayer.Naval;
        const TargetLayer A = TargetLayer.Air;
        const TargetLayer GA = TargetLayer.Ground | TargetLayer.Structure | TargetLayer.Air;
        const TargetLayer GNA = TargetLayer.All & ~TargetLayer.Submerged;
        const TargetLayer N = TargetLayer.Naval | TargetLayer.Submerged;

        static void BuildWeapons()
        {
            var P = ProjectileKind.Hitscan;
            // ---- Infantry small arms
            Wp("rifle_ca", "M9 Carbine", 14, 4.5f, 0.9f, P, "Bullet", GN, "rifle", suppression: 0.4f);
            Wp("rifle_id", "K-71 Battle Rifle", 13, 4.2f, 1.0f, P, "Bullet", GN, "rifle_heavy", suppression: 0.4f);
            Wp("hmg_ca", "Bulwark Repeater", 9, 5f, 0.25f, P, "HeavyBullet", GNA, "mg", clip: 24, reload: 2.2f, suppression: 0.8f);
            Wp("hmg_id", "Ironclad Chaingun", 10, 4.8f, 0.22f, P, "HeavyBullet", GNA, "mg_heavy", clip: 30, reload: 2.6f, suppression: 1.0f);
            Wp("atrocket_ca", "Lancer Guided Rocket", 70, 5.6f, 2.6f, ProjectileKind.Missile, "AP", GNA, "rocket", speed: 14, splash: 0.4f);
            Wp("atrocket_id", "Breacher RPG", 80, 5.0f, 3.0f, ProjectileKind.Missile, "AP", GN, "rocket", speed: 12, splash: 0.6f, inacc: 0.4f);
            Wp("aamissile_inf_ca", "Skyguard Launcher", 55, 7f, 2.2f, ProjectileKind.Missile, "AAMissile", A, "missile_aa", speed: 20);
            Wp("flak_inf_id", "Skyburst Flak Rifle", 22, 6.2f, 0.7f, ProjectileKind.Bullet, "Flak", A, "flak", speed: 35, splash: 0.6f);
            Wp("flamer_ca", "Scorcher Projector", 22, 3.0f, 0.5f, ProjectileKind.Flame, "Flame", G, "flame", splash: 0.8f, speed: 10, ff: true);
            Wp("flamer_id", "Pyre Torch", 26, 3.2f, 0.55f, ProjectileKind.Flame, "Flame", G, "flame", splash: 1.0f, speed: 10, ff: true);
            Wp("sniper_ca", "Marksman Rifle", 120, 9.5f, 3.2f, P, "Sniper", G, "sniper");
            Wp("sniper_id", "Hunter Longrifle", 130, 9f, 3.6f, P, "Sniper", G, "sniper");
            Wp("elite_ca", "Phantom SMG", 30, 5.5f, 0.35f, P, "HeavyBullet", GN, "smg", suppression: 0.6f);
            Wp("elite_id", "Black Guard Shotgun", 45, 4.2f, 0.7f, P, "HeavyBullet", GN, "shotgun", splash: 0.5f, suppression: 1f);
            Wp("c4", "Demolition Charge", 1000, 1.2f, 2.0f, P, "Demolition", TargetLayer.Structure | TargetLayer.Ground | TargetLayer.Naval, "c4");
            Wp("pathfinder_pistol", "Sidearm", 9, 4f, 1.0f, P, "Bullet", G, "pistol");
            Wp("arc_rifle", "Arc Rifle", 45, 4.5f, 1.8f, ProjectileKind.Beam, "Arc", GN, "arc", splash: 0.3f).EmpSeconds = 1.5f;
            Wp("pistol_id", "Service Pistol", 8, 4f, 1.0f, P, "Bullet", G, "pistol");

            // ---- Vehicle weapons
            Wp("scout_mg_ca", "Wasp Twin MG", 8, 5f, 0.3f, P, "HeavyBullet", GNA, "mg", suppression: 0.5f);
            Wp("scout_mg_id", "Jackal HMG", 9, 4.8f, 0.35f, P, "HeavyBullet", GN, "mg_heavy", suppression: 0.6f);
            Wp("mbt_ca", "105mm Smoothbore", 60, 5.5f, 1.7f, ProjectileKind.Shell, "AP", GN, "cannon", speed: 40, inacc: 0.2f);
            Wp("mbt_id", "115mm Cannon", 72, 5.2f, 2.1f, ProjectileKind.Shell, "AP", GN, "cannon_heavy", speed: 36, inacc: 0.35f);
            Wp("heavy_ca", "Twin 120mm Rail-Assisted", 75, 6f, 2.0f, ProjectileKind.Shell, "AP", GN, "cannon_heavy", burst: 2, speed: 45, inacc: 0.15f);
            Wp("heavy_id", "Twin 152mm Cannon", 90, 5.6f, 2.6f, ProjectileKind.Shell, "AP", GN, "cannon_huge", burst: 2, speed: 34, inacc: 0.35f);
            Wp("heavy_id_aa", "Behemoth Missile Pod", 45, 6.5f, 2.5f, ProjectileKind.Missile, "Missile", A, "missile_aa", speed: 18, burst: 2);
            Wp("td_ca", "Lancet Railgun", 210, 7f, 3.6f, ProjectileKind.Rail, "Rail", GN, "railgun");
            Wp("td_id", "Spike 125mm AP Gun", 170, 6.5f, 3.4f, ProjectileKind.Shell, "AP", GN, "cannon_heavy", speed: 55);
            Wp("mlrs_ca", "Thunderhead Rockets", 45, 11f, 6.5f, ProjectileKind.Missile, "HE", G, "rocket_salvo", burst: 6, splash: 1.0f, speed: 12, inacc: 1.2f, minRange: 3, ff: true);
            Wp("mlrs_id", "Hailstorm Rockets", 40, 10.5f, 7f, ProjectileKind.Missile, "HE", G, "rocket_salvo", burst: 8, splash: 1.2f, speed: 11, inacc: 1.8f, minRange: 3, ff: true);
            Wp("arty_ca", "Arbiter 155mm Howitzer", 120, 13f, 4.2f, ProjectileKind.Ballistic, "HE", GN, "artillery", splash: 1.2f, speed: 11, inacc: 0.7f, minRange: 3.5f, ff: true);
            Wp("arty_id", "Anvil 203mm Gun", 150, 14f, 5.2f, ProjectileKind.Ballistic, "HE", GN, "artillery_heavy", splash: 1.6f, speed: 10, inacc: 1.1f, minRange: 4, ff: true);
            Wp("aa_ca", "Sentinel SAM Rack", 60, 8.5f, 1.6f, ProjectileKind.Missile, "AAMissile", A, "missile_aa", burst: 2, speed: 24);
            Wp("aa_id", "Thresher Quad Flak", 16, 7.5f, 0.25f, ProjectileKind.Bullet, "Flak", A, "flak", speed: 40, splash: 0.7f);
            Wp("apc_mg_ca", "Stallion Autocannon", 18, 5f, 0.6f, P, "HeavyBullet", GNA, "autocannon");
            Wp("apc_mg_id", "Bulldog MG", 10, 4.8f, 0.3f, P, "HeavyBullet", GN, "mg_heavy", suppression: 0.6f);
            Wp("flametank_ca", "Thermite Lance", 40, 3.8f, 0.6f, ProjectileKind.Flame, "Flame", G, "thermal", splash: 1.0f, speed: 14, ff: true);
            Wp("flametank_id", "Hellbrand Twin Flamers", 24, 3.6f, 0.55f, ProjectileKind.Flame, "Flame", G, "flame", burst: 2, splash: 1.2f, speed: 11, ff: true).BurnsGround = true;
            Wp("siege_ca", "Citadel Mass Driver", 400, 9f, 7f, ProjectileKind.Rail, "Siege", G, "railgun_heavy", splash: 1.0f, minRange: 2);
            Wp("siege_id", "Earthbreaker 420mm Mortar", 420, 11f, 8f, ProjectileKind.Ballistic, "Siege", G, "mortar_huge", splash: 2.2f, speed: 8, inacc: 0.8f, minRange: 4, ff: true);
            Wp("ifv_mg_ca", "Mule Carbine Port", 9, 4f, 0.5f, P, "Bullet", G, "rifle");

            // ---- Aircraft weapons
            Wp("fighter_ca", "Falcon Air-to-Air Missiles", 80, 7f, 0.8f, ProjectileKind.Missile, "AAMissile", A, "missile_aa", speed: 30).AmmoCost = 1;
            Wp("fighter_ca_ground", "Falcon Strike Rockets", 55, 6f, 0.6f, ProjectileKind.Missile, "Missile", GN, "rocket", speed: 26).AmmoCost = 1;
            Wp("fighter_id", "Vulture Cannon Pods", 24, 6f, 0.25f, ProjectileKind.Bullet, "Flak", GA, "autocannon", speed: 50).AmmoCost = 1;
            Wp("heli_ca", "Hornet Hydra Rockets", 40, 6f, 0.7f, ProjectileKind.Missile, "Missile", GN, "rocket", speed: 22, burst: 2).AmmoCost = 1;
            Wp("heli_id", "Harrow Chin Cannon", 22, 5.5f, 0.3f, ProjectileKind.Bullet, "HeavyBullet", GN, "autocannon", speed: 45).AmmoCost = 1;
            Wp("bomber_ca", "Condor Guided Bombs", 260, 1.5f, 0.5f, ProjectileKind.Bomb, "Bomb", GN, "bomb", splash: 1.4f, speed: 8).AmmoCost = 1;
            Wp("bomber_id", "Leviathan Carpet Bombs", 180, 1.5f, 0.25f, ProjectileKind.Bomb, "Bomb", GN, "bomb", splash: 1.8f, speed: 8, ff: true).AmmoCost = 1;
            Wp("stealth_ca", "Specter Penetrator", 380, 2f, 1.0f, ProjectileKind.Bomb, "Bomb", GN, "bomb", splash: 1.2f, speed: 10).AmmoCost = 1;
            Wp("gorgon_rockets", "Gorgon Rocket Barrage", 50, 6.5f, 0.35f, ProjectileKind.Missile, "Missile", GN, "rocket_salvo", speed: 20, splash: 0.7f, inacc: 0.6f).AmmoCost = 1;
            Wp("gorgon_aa", "Gorgon Flak Turret", 18, 5.5f, 0.4f, ProjectileKind.Bullet, "Flak", A, "flak", speed: 40);

            // ---- Naval weapons
            Wp("patrol_ca", "Swift 30mm", 16, 6f, 0.4f, P, "HeavyBullet", GNA, "autocannon");
            Wp("patrol_id", "Pike Twin HMG", 11, 5.5f, 0.25f, P, "HeavyBullet", GNA, "mg_heavy");
            Wp("torpedo_ca", "Mk.4 Homing Torpedo", 160, 7f, 3.2f, ProjectileKind.Torpedo, "Torpedo", N | TargetLayer.Structure, "torpedo", speed: 8);
            Wp("torpedo_id", "Heavy Torpedo", 190, 6.5f, 3.8f, ProjectileKind.Torpedo, "Torpedo", N | TargetLayer.Structure, "torpedo", speed: 7, burst: 2);
            Wp("destroyer_gun_ca", "Vigilant 127mm", 55, 8f, 1.8f, ProjectileKind.Shell, "NavalShell", GN, "naval_gun", speed: 34);
            Wp("destroyer_asw_ca", "ASW Rocket", 90, 6f, 3.0f, ProjectileKind.Torpedo, "Torpedo", N, "torpedo", speed: 10);
            Wp("destroyer_aa_ca", "Vigilant Point Defense", 30, 7f, 0.6f, ProjectileKind.Missile, "AAMissile", A, "missile_aa", speed: 26);
            Wp("destroyer_gun_id", "Bastion 130mm", 65, 7.5f, 2.1f, ProjectileKind.Shell, "NavalShell", GN, "naval_gun", speed: 32);
            Wp("destroyer_asw_id", "Depth Charges", 110, 4f, 3.5f, ProjectileKind.Torpedo, "Torpedo", N, "depthcharge", speed: 6, splash: 1.0f);
            Wp("destroyer_aa_id", "Bastion Flak", 14, 6.5f, 0.3f, ProjectileKind.Bullet, "Flak", A, "flak", speed: 40, splash: 0.6f);
            Wp("battleship_ca", "Sovereign 16-inch Battery", 220, 18f, 6f, ProjectileKind.Ballistic, "NavalShell", GN, "naval_huge", splash: 1.6f, burst: 3, speed: 13, inacc: 1.4f, minRange: 5, ff: true);
            Wp("battleship_id", "Iron Monarch Triple 18-inch", 260, 19f, 7.5f, ProjectileKind.Ballistic, "NavalShell", GN, "naval_huge", splash: 2.0f, burst: 3, speed: 12, inacc: 1.8f, minRange: 5, ff: true);
            Wp("cruiser_ca", "Tempest Cruise Missiles", 140, 16f, 5f, ProjectileKind.Missile, "Missile", GN, "cruise_missile", splash: 1.3f, burst: 2, speed: 14, minRange: 4);
            Wp("cruiser_ca_aa", "Tempest SAM", 70, 9f, 1.4f, ProjectileKind.Missile, "AAMissile", A, "missile_aa", speed: 28);
            Wp("cruiser_id", "Scourge Rocket Battery", 70, 15f, 6f, ProjectileKind.Missile, "Missile", GN, "rocket_salvo", splash: 1.4f, burst: 5, speed: 12, inacc: 1.5f, minRange: 4, ff: true);
            Wp("cruiser_id_aa", "Scourge Flak Array", 18, 7.5f, 0.3f, ProjectileKind.Bullet, "Flak", A, "flak", speed: 40, splash: 0.7f);

            // ---- Structure weapons
            Wp("bunker_mg_ca", "Pillbox Twin MG", 14, 6f, 0.35f, P, "HeavyBullet", GN, "mg", suppression: 0.8f);
            Wp("bunker_mg_id", "Nest HMG", 15, 5.5f, 0.35f, P, "HeavyBullet", GN, "mg_heavy", suppression: 1.0f);
            Wp("sentry_ca", "Sentry Gatling", 12, 6.5f, 0.15f, P, "HeavyBullet", GN, "minigun", suppression: 1.0f).RequiresPower = true;
            Wp("incinerator_id", "Incinerator Jet", 38, 4.5f, 0.6f, ProjectileKind.Flame, "Flame", G, "flame", splash: 1.2f, speed: 12, ff: true).RequiresPower = true;
            Wp("railturret_ca", "Rail Turret", 160, 7f, 2.8f, ProjectileKind.Rail, "Rail", GN, "railgun").RequiresPower = true;
            Wp("cannon_turret_id", "Bulwark 130mm", 110, 6.5f, 2.4f, ProjectileKind.Shell, "AP", GN, "cannon_heavy", speed: 40).RequiresPower = true;
            Wp("skyshield_ca", "Skyshield SAM", 85, 10f, 1.6f, ProjectileKind.Missile, "AAMissile", A, "missile_aa", burst: 2, speed: 28).RequiresPower = true;
            Wp("flak_battery_id", "Flak Battery", 20, 9f, 0.25f, ProjectileKind.Bullet, "Flak", A, "flak", speed: 45, splash: 0.8f).RequiresPower = true;
            Wp("citadel_arty_ca", "Citadel 175mm", 160, 16f, 5f, ProjectileKind.Ballistic, "HE", GN, "artillery_heavy", splash: 1.3f, speed: 12, inacc: 0.6f, minRange: 4, ff: true).RequiresPower = true;
            Wp("mortar_pit_id", "Grand Mortar", 180, 15f, 5.8f, ProjectileKind.Ballistic, "HE", GN, "mortar_huge", splash: 1.8f, speed: 10, inacc: 1.0f, minRange: 4, ff: true).RequiresPower = true;
            Wp("civ_militia", "Militia Rifle", 8, 4f, 1.2f, P, "Bullet", G, "rifle");
        }

        // ---------------------------------------------------------------- Units
        static UnitDef U(string id, string name, FactionId f, UnitCategory cat, Locomotor loco, int cost, float time, float hp, ArmorType armor,
            float speed, float sight, BuildingRole at, int tier, string model, string desc, params string[] weapons)
        {
            var u = new UnitDef
            {
                Id = id, Name = name, Faction = f, Category = cat, Locomotor = loco, Cost = cost, BuildTime = time, Health = hp,
                Armor = armor, Speed = speed, Sight = sight, ProducedAt = at, Tier = tier, Model = model, Description = desc
            };
            u.Weapons.AddRange(weapons);
            u.Radius = cat == UnitCategory.Infantry ? 0.22f : cat == UnitCategory.Naval ? 0.8f : cat == UnitCategory.Aircraft ? 0.5f : 0.5f;
            u.TurnRate = cat == UnitCategory.Infantry ? 10f : cat == UnitCategory.Naval ? 1.4f : cat == UnitCategory.Aircraft ? 2.5f : 4f;
            u.Crushable = cat == UnitCategory.Infantry;
            u.Voice = cat == UnitCategory.Infantry ? "infantry" : cat == UnitCategory.Vehicle ? "vehicle" : cat == UnitCategory.Aircraft ? "pilot" : "naval";
            u.Prerequisites.Add(at);
            Units[id] = u;
            return u;
        }

        static UnitDef Req(this UnitDef u, params BuildingRole[] roles) { foreach (var r in roles) if (!u.Prerequisites.Contains(r)) u.Prerequisites.Add(r); return u; }
        static UnitDef Ab(this UnitDef u, params UnitAbility[] a) { u.Abilities.AddRange(a); return u; }
        static UnitDef Tech(this UnitDef u, string tech) { u.RequiredTech.Add(tech); return u; }
        static UnitDef Prio(this UnitDef u, float p) { u.TargetPriority = p; return u; }

        static void BuildAllianceUnits()
        {
            var F = FactionId.Alliance;
            var I = UnitCategory.Infantry; var V = UnitCategory.Vehicle; var Ai = UnitCategory.Aircraft; var Nv = UnitCategory.Naval;
            // Infantry
            U("ca_rifleman", "Rifleman", F, I, Locomotor.Foot, 130, 6, 110, ArmorType.Infantry, 1.45f, 6f, R.Barracks, 1, "inf_rifle", "Versatile line infantry with a precise carbine. Effective against infantry.", "rifle_ca").Hotkey = "R";
            U("ca_bulwark", "Bulwark Trooper", F, I, Locomotor.Foot, 270, 9, 200, ArmorType.HeavyInfantry, 1.15f, 6f, R.Barracks, 1, "inf_heavy", "Armored heavy infantry with a suppressive repeater. Can engage aircraft.", "hmg_ca").Hotkey = "B";
            U("ca_lancer", "Lancer", F, I, Locomotor.Foot, 270, 9, 105, ArmorType.Infantry, 1.3f, 6.5f, R.Barracks, 1, "inf_at", "Anti-armor specialist firing guided rockets at vehicles and aircraft.", "atrocket_ca").Hotkey = "L";
            U("ca_skyguard", "Skyguard", F, I, Locomotor.Foot, 350, 10, 100, ArmorType.Infantry, 1.3f, 8f, R.Barracks, 2, "inf_aa", "Shoulder-launched surface-to-air missiles.", "aamissile_inf_ca").Req(R.Radar).Hotkey = "K";
            U("ca_engineer", "Field Engineer", F, I, Locomotor.Foot, 500, 12, 80, ArmorType.Infantry, 1.3f, 5f, R.Barracks, 1, "inf_engineer", "Captures damaged enemy and neutral structures, repairs friendly structures and bridges.").Ab(UnitAbility.CaptureRepair).Hotkey = "E";
            U("ca_corpsman", "Corpsman", F, I, Locomotor.Foot, 400, 10, 100, ArmorType.Infantry, 1.4f, 6f, R.Barracks, 1, "inf_medic", "Heals nearby friendly infantry.").Ab(UnitAbility.HealInfantry).HealRate = 12;
            U("ca_scorcher", "Scorcher", F, I, Locomotor.Foot, 320, 10, 130, ArmorType.HeavyInfantry, 1.2f, 5f, R.AdvancedBarracks, 2, "inf_flame", "Close-range incendiary projector that clears entrenched infantry.", "flamer_ca");
            U("ca_marksman", "Marksman", F, I, Locomotor.Foot, 650, 14, 90, ArmorType.Infantry, 1.25f, 10f, R.AdvancedBarracks, 2, "inf_sniper", "Long-range precision rifle eliminates infantry in a single shot. Hidden while stationary.", "sniper_ca").Ab(UnitAbility.Stealth).Hotkey = "M";
            U("ca_phantom", "Phantom Operative", F, I, Locomotor.Foot, 1500, 25, 220, ArmorType.HeavyInfantry, 1.7f, 8f, R.AdvancedBarracks, 3, "inf_elite", "Cloaked special forces operative. Plants demolition charges that level structures.", "elite_ca", "c4")
                .Ab(UnitAbility.Stealth, UnitAbility.Demolition).Tech("ca_phantom_program").Req(R.ResearchLab).Prio(2f);
            U("ca_pathfinder", "Pathfinder", F, I, Locomotor.Foot, 450, 10, 100, ArmorType.Infantry, 1.8f, 11f, R.AdvancedBarracks, 2, "inf_specialist", "Forward observer with long sight. Detects cloaked units and hides when stationary.", "pathfinder_pistol")
                .Ab(UnitAbility.Detector, UnitAbility.Stealth, UnitAbility.Recon);
            // Vehicles
            U("ca_wasp", "Wasp Scout Buggy", F, V, Locomotor.Wheeled, 400, 8, 220, ArmorType.Light, 4.2f, 9f, R.Factory, 1, "scout", "Fast reconnaissance buggy with twin machine guns.", "scout_mg_ca").Hotkey = "W";
            U("ca_sabre", "Sabre MBT", F, V, Locomotor.Tracked, 850, 14, 520, ArmorType.Medium, 2.6f, 7f, R.Factory, 1, "tank_mbt", "Fast, accurate main battle tank.", "mbt_ca").Hotkey = "T";
            U("ca_warden", "Warden Heavy Tank", F, V, Locomotor.Tracked, 1600, 22, 950, ArmorType.Heavy, 2.0f, 7f, R.AdvancedFactory, 3, "tank_heavy", "Heavily armored tank with twin rail-assisted cannons.", "heavy_ca").Hotkey = "H";
            U("ca_lancet", "Lancet Tank Destroyer", F, V, Locomotor.Tracked, 1300, 18, 420, ArmorType.Medium, 2.5f, 8f, R.AdvancedFactory, 3, "td", "Turretless railgun destroyer. Devastating against heavy armor.", "td_ca").Hotkey = "D";
            U("ca_thunderhead", "Thunderhead MLRS", F, V, Locomotor.Wheeled, 1100, 16, 260, ArmorType.Light, 2.6f, 7f, R.Factory, 2, "mlrs", "Rocket artillery that saturates an area with guided rockets.", "mlrs_ca").Req(R.Radar).Hotkey = "Y";
            U("ca_arbiter", "Arbiter Howitzer", F, V, Locomotor.Tracked, 1000, 16, 300, ArmorType.Light, 2.0f, 7f, R.Factory, 2, "howitzer", "Self-propelled long-range howitzer.", "arty_ca").Req(R.Radar).Hotkey = "U";
            U("ca_sentinel", "Sentinel AA Track", F, V, Locomotor.Tracked, 800, 12, 340, ArmorType.Light, 2.6f, 9f, R.Factory, 2, "aa_track", "Mobile surface-to-air missile platform.", "aa_ca").Req(R.Radar).Hotkey = "A";
            U("ca_stallion", "Stallion APC", F, V, Locomotor.Wheeled, 620, 10, 420, ArmorType.Medium, 3.4f, 7f, R.Factory, 1, "apc", "Armored personnel carrier with an autocannon. Carries 5 infantry.", "apc_mg_ca").Ab(UnitAbility.Transport).TransportCapacity = 5;
            U("ca_thermite", "Thermite Carrier", F, V, Locomotor.Tracked, 900, 15, 520, ArmorType.Medium, 2.3f, 6f, R.AdvancedFactory, 2, "flame_tank", "Projects a superheated thermite lance that melts infantry and structures.", "flametank_ca");
            U("ca_mule", "Mule Engineer Carrier", F, V, Locomotor.Wheeled, 650, 10, 380, ArmorType.Medium, 3.2f, 6f, R.Factory, 1, "eng_carrier", "Light armored carrier for up to 3 engineers or medics. Repairs nearby vehicles slowly.", "ifv_mg_ca").Ab(UnitAbility.Transport, UnitAbility.RepairVehicles).TransportCapacity = 3;
            Units["ca_mule"].HealRate = 4;
            U("ca_citadel", "Citadel Siege Platform", F, V, Locomotor.Tracked, 2200, 30, 900, ArmorType.Heavy, 1.3f, 8f, R.AdvancedFactory, 4, "siege", "Mass-driver siege platform that shatters fortifications from range.", "siege_ca").Req(R.ResearchLab);
            U("ca_mhq", "Mobile HQ Vehicle", F, V, Locomotor.Tracked, 3000, 40, 1000, ArmorType.Heavy, 1.4f, 6f, R.Factory, 2, "mcv", "Deploys into a Command Center.").Ab(UnitAbility.DeployToHQ).Req(R.RepairDepot).DeploysInto = "ca_hq";
            U("ca_prospector", "Prospector Harvester", F, V, Locomotor.Tracked, 1400, 18, 750, ArmorType.Heavy, 1.8f, 5f, R.Factory, 1, "harvester", "Automated ore harvester.").Ab(UnitAbility.Harvester).Req(R.Refinery);
            Units["ca_prospector"].HarvestCapacity = 700; Units["ca_prospector"].HarvestRate = 70;
            U("ca_mender", "Mender Repair Vehicle", F, V, Locomotor.Wheeled, 800, 12, 300, ArmorType.Light, 3.0f, 6f, R.Factory, 2, "repair_truck", "Field-repairs nearby friendly vehicles.").Ab(UnitAbility.RepairVehicles).Req(R.RepairDepot).HealRate = 18;
            // Aircraft
            U("ca_falcon", "Falcon Interceptor", F, Ai, Locomotor.Air, 1200, 16, 260, ArmorType.Aircraft, 9f, 10f, R.Hangar, 2, "fighter", "Air-superiority jet with air-to-air missiles and strike rockets.", "fighter_ca", "fighter_ca_ground").Ammo = 6;
            U("ca_hornet", "Hornet Gunship", F, Ai, Locomotor.Helicopter, 1200, 16, 380, ArmorType.Aircraft, 5f, 9f, R.Hangar, 2, "heli_attack", "Attack helicopter armed with rocket pods.", "heli_ca").Ammo = 12;
            U("ca_condor", "Condor Precision Bomber", F, Ai, Locomotor.Air, 2000, 25, 500, ArmorType.Aircraft, 6.5f, 8f, R.AdvancedHangar, 3, "bomber", "Delivers guided bombs on structures and armor.", "bomber_ca").Ammo = 4;
            U("ca_owl", "Owl Recon Jet", F, Ai, Locomotor.Air, 800, 10, 200, ArmorType.Aircraft, 11f, 16f, R.Hangar, 2, "recon_air", "Unarmed high-speed reconnaissance jet. Detects cloaked units.").Ab(UnitAbility.Recon, UnitAbility.Detector).Req(R.Radar);
            U("ca_albatross", "Albatross Tiltrotor", F, Ai, Locomotor.Helicopter, 900, 12, 420, ArmorType.Aircraft, 6f, 7f, R.Hangar, 2, "transport_air", "Transport tiltrotor carrying 6 infantry.").Ab(UnitAbility.Transport).TransportCapacity = 6;
            U("ca_specter", "Specter Stealth Striker", F, Ai, Locomotor.Air, 2600, 30, 420, ArmorType.Aircraft, 8f, 9f, R.AdvancedHangar, 4, "stealth_air", "Cloaked strike aircraft with penetrator bombs.", "stealth_ca").Ab(UnitAbility.Stealth).Tech("ca_specter_program").Req(R.ResearchLab).Ammo = 2;
            // Naval
            U("ca_swift", "Swift Patrol Boat", F, Nv, Locomotor.Naval, 560, 10, 380, ArmorType.Naval, 4.5f, 9f, R.Shipyard, 1, "patrol", "Fast patrol boat with a 30mm gun.", "patrol_ca");
            U("ca_barracuda", "Barracuda Attack Sub", F, Nv, Locomotor.Submarine, 1100, 16, 600, ArmorType.Naval, 3.0f, 7f, R.Shipyard, 2, "sub", "Submerged hunter-killer submarine.", "torpedo_ca").Ab(UnitAbility.Submerge, UnitAbility.Stealth);
            U("ca_vigilant", "Vigilant Destroyer", F, Nv, Locomotor.Naval, 1400, 20, 950, ArmorType.Naval, 3.2f, 9f, R.Shipyard, 2, "destroyer", "Multi-role destroyer with sonar, ASW rockets and point defense.", "destroyer_gun_ca", "destroyer_asw_ca", "destroyer_aa_ca").Ab(UnitAbility.Detector).Req(R.Radar);
            U("ca_sovereign", "Sovereign Battleship", F, Nv, Locomotor.Naval, 3000, 40, 2200, ArmorType.Naval, 1.8f, 9f, R.AdvancedShipyard, 4, "battleship", "Capital ship with long-range heavy guns.", "battleship_ca").Req(R.TechCenter);
            U("ca_tempest", "Tempest Missile Cruiser", F, Nv, Locomotor.Naval, 2200, 30, 1300, ArmorType.Naval, 2.4f, 10f, R.AdvancedShipyard, 3, "cruiser", "Cruise missiles for land attack and SAMs for fleet defense.", "cruiser_ca", "cruiser_ca_aa");
            U("ca_pelican", "Pelican Landing Ship", F, Nv, Locomotor.Naval, 900, 12, 800, ArmorType.Naval, 3.0f, 6f, R.Shipyard, 1, "lst", "Carries up to 6 infantry or vehicles to shore.").Ab(UnitAbility.Transport).TransportCapacity = 6;
            Units["ca_pelican"].TransportVehicles = true;
            U("ca_haven", "Haven Tender", F, Nv, Locomotor.Naval, 900, 12, 700, ArmorType.Naval, 3.0f, 7f, R.Shipyard, 2, "tender", "Naval support vessel that repairs nearby ships.").Ab(UnitAbility.RepairNaval).HealRate = 20;
        }

        static void BuildDominionUnits()
        {
            var F = FactionId.Dominion;
            var I = UnitCategory.Infantry; var V = UnitCategory.Vehicle; var Ai = UnitCategory.Aircraft; var Nv = UnitCategory.Naval;
            U("id_levy", "Levy Rifleman", F, I, Locomotor.Foot, 120, 4, 100, ArmorType.Infantry, 1.4f, 5.5f, R.Barracks, 1, "inf_rifle", "Cheap mass-produced line infantry.", "rifle_id").Hotkey = "R";
            U("id_ironclad", "Ironclad Gunner", F, I, Locomotor.Foot, 320, 8, 240, ArmorType.HeavyInfantry, 1.0f, 5.5f, R.Barracks, 1, "inf_heavy", "Plate-armored gunner with a chaingun. Slow but tough.", "hmg_id").Hotkey = "B";
            U("id_breacher", "Breacher", F, I, Locomotor.Foot, 280, 8, 110, ArmorType.Infantry, 1.3f, 6f, R.Barracks, 1, "inf_at", "RPG-armed anti-vehicle infantry.", "atrocket_id").Hotkey = "L";
            U("id_skyburst", "Skyburst Trooper", F, I, Locomotor.Foot, 280, 8, 110, ArmorType.Infantry, 1.3f, 7f, R.Barracks, 2, "inf_aa", "Fires airburst flak rounds at aircraft.", "flak_inf_id").Req(R.Radar).Hotkey = "K";
            U("id_sapper", "Combat Sapper", F, I, Locomotor.Foot, 450, 11, 90, ArmorType.Infantry, 1.3f, 5f, R.Barracks, 1, "inf_engineer", "Captures damaged enemy and neutral structures, repairs friendly structures and bridges.").Ab(UnitAbility.CaptureRepair).Hotkey = "E";
            U("id_surgeon", "Field Surgeon", F, I, Locomotor.Foot, 350, 9, 110, ArmorType.Infantry, 1.35f, 6f, R.Barracks, 1, "inf_medic", "Heals nearby friendly infantry.").Ab(UnitAbility.HealInfantry).HealRate = 10;
            U("id_pyre", "Pyre Trooper", F, I, Locomotor.Foot, 350, 9, 150, ArmorType.HeavyInfantry, 1.15f, 5f, R.AdvancedBarracks, 2, "inf_flame", "Flamethrower trooper in a heat suit.", "flamer_id");
            U("id_hunter", "Hunter", F, I, Locomotor.Foot, 600, 13, 95, ArmorType.Infantry, 1.25f, 9.5f, R.AdvancedBarracks, 2, "inf_sniper", "Camouflaged sniper. Hidden while stationary.", "sniper_id").Ab(UnitAbility.Stealth).Hotkey = "M";
            U("id_blackguard", "Black Guard Commando", F, I, Locomotor.Foot, 1400, 24, 280, ArmorType.HeavyInfantry, 1.6f, 7.5f, R.AdvancedBarracks, 3, "inf_elite", "Elite shock commando with a combat shotgun and demolition charges.", "elite_id", "c4")
                .Ab(UnitAbility.Demolition).Tech("id_blackguard_program").Req(R.ResearchLab).Prio(2f);
            U("id_arc", "Arc Trooper", F, I, Locomotor.Foot, 500, 12, 150, ArmorType.HeavyInfantry, 1.2f, 6f, R.AdvancedBarracks, 2, "inf_specialist", "Electrical arc rifle damages and briefly disables vehicles.", "arc_rifle").Ab(UnitAbility.EmpShot);
            U("id_jackal", "Jackal Armored Car", F, V, Locomotor.Wheeled, 430, 7, 260, ArmorType.Light, 3.8f, 8f, R.Factory, 1, "scout", "Rugged armored car with a heavy machine gun.", "scout_mg_id").Hotkey = "W";
            U("id_ravager", "Ravager MBT", F, V, Locomotor.Tracked, 900, 13, 600, ArmorType.Medium, 2.2f, 6.5f, R.Factory, 1, "tank_mbt", "Rugged mass-produced main battle tank with a heavy cannon.", "mbt_id").Hotkey = "T";
            U("id_behemoth", "Behemoth Heavy Tank", F, V, Locomotor.Tracked, 1800, 24, 1300, ArmorType.Heavy, 1.6f, 6.5f, R.AdvancedFactory, 3, "tank_heavy", "Colossal twin-cannon tank with an anti-air missile pod. Crushes infantry.", "heavy_id", "heavy_id_aa").Hotkey = "H";
            U("id_spike", "Spike Tank Destroyer", F, V, Locomotor.Tracked, 1100, 16, 500, ArmorType.Medium, 2.2f, 7f, R.AdvancedFactory, 3, "td", "Low-profile casemate destroyer with a high-velocity gun.", "td_id").Hotkey = "D";
            U("id_hailstorm", "Hailstorm Rocket Truck", F, V, Locomotor.Wheeled, 900, 14, 260, ArmorType.Light, 2.5f, 6.5f, R.Factory, 2, "mlrs", "Saturation rocket artillery.", "mlrs_id").Req(R.Radar).Hotkey = "Y";
            U("id_anvil", "Anvil Self-Propelled Gun", F, V, Locomotor.Tracked, 1100, 18, 380, ArmorType.Light, 1.7f, 6.5f, R.Factory, 2, "howitzer", "Heavy 203mm self-propelled gun with extreme range.", "arty_id").Req(R.Radar).Hotkey = "U";
            U("id_thresher", "Thresher Flak Track", F, V, Locomotor.Tracked, 700, 11, 380, ArmorType.Light, 2.4f, 8f, R.Factory, 2, "aa_track", "Quad-barrel flak cannon vehicle.", "aa_id").Req(R.Radar).Hotkey = "A";
            U("id_bulldog", "Bulldog APC", F, V, Locomotor.Tracked, 700, 10, 500, ArmorType.Medium, 2.8f, 6.5f, R.Factory, 1, "apc", "Tracked troop carrier. Carries 6 infantry.", "apc_mg_id").Ab(UnitAbility.Transport).TransportCapacity = 6;
            U("id_hellbrand", "Hellbrand Flame Tank", F, V, Locomotor.Tracked, 1000, 15, 620, ArmorType.Medium, 2.1f, 6f, R.AdvancedFactory, 2, "flame_tank", "Twin flamethrower tank that sets the ground ablaze.", "flametank_id");
            U("id_mole", "Mole Sapper Carrier", F, V, Locomotor.Tracked, 550, 9, 450, ArmorType.Medium, 2.6f, 6f, R.Factory, 1, "eng_carrier", "Armored carrier for 4 sappers or surgeons.").Ab(UnitAbility.Transport).TransportCapacity = 4;
            U("id_earthbreaker", "Earthbreaker Siege Crawler", F, V, Locomotor.Tracked, 2000, 30, 1100, ArmorType.Heavy, 1.1f, 7f, R.AdvancedFactory, 4, "siege", "Enormous siege mortar that pulverizes bases.", "siege_id").Req(R.ResearchLab);
            U("id_foundry", "Foundry Crawler", F, V, Locomotor.Tracked, 2800, 38, 1200, ArmorType.Heavy, 1.2f, 6f, R.Factory, 2, "mcv", "Deploys into a Command Bunker.").Ab(UnitAbility.DeployToHQ).Req(R.RepairDepot).DeploysInto = "id_hq";
            U("id_hauler", "Ore Hauler", F, V, Locomotor.Tracked, 1200, 16, 900, ArmorType.Heavy, 1.6f, 5f, R.Factory, 1, "harvester", "Heavily armored ore hauler.").Ab(UnitAbility.Harvester).Req(R.Refinery);
            Units["id_hauler"].HarvestCapacity = 800; Units["id_hauler"].HarvestRate = 60;
            U("id_wrench", "Wrench Repair Truck", F, V, Locomotor.Wheeled, 700, 11, 340, ArmorType.Light, 2.8f, 6f, R.Factory, 2, "repair_truck", "Field repair truck.").Ab(UnitAbility.RepairVehicles).Req(R.RepairDepot).HealRate = 15;
            U("id_vulture", "Vulture Interceptor", F, Ai, Locomotor.Air, 1000, 14, 300, ArmorType.Aircraft, 8f, 9f, R.Hangar, 2, "fighter", "Rugged cannon-armed jet that engages air and ground targets.", "fighter_id").Ammo = 20;
            U("id_harrow", "Harrow Gunship", F, Ai, Locomotor.Helicopter, 1100, 15, 480, ArmorType.Aircraft, 4.3f, 8f, R.Hangar, 2, "heli_attack", "Armored gunship with a chin autocannon.", "heli_id").Ammo = 30;
            U("id_leviathan", "Leviathan Heavy Bomber", F, Ai, Locomotor.Air, 2500, 28, 700, ArmorType.Aircraft, 5.5f, 8f, R.AdvancedHangar, 3, "bomber", "Strategic bomber that carpet-bombs everything on its path.", "bomber_id").Ammo = 8;
            U("id_raven", "Raven Recon Plane", F, Ai, Locomotor.Air, 700, 9, 240, ArmorType.Aircraft, 10f, 15f, R.Hangar, 2, "recon_air", "Unarmed reconnaissance aircraft. Detects cloaked units.").Ab(UnitAbility.Recon, UnitAbility.Detector).Req(R.Radar);
            U("id_ox", "Ox Heavy Transport", F, Ai, Locomotor.Helicopter, 1000, 14, 600, ArmorType.Aircraft, 5f, 7f, R.Hangar, 2, "transport_air", "Twin-rotor heavy transport carrying 8 infantry.").Ab(UnitAbility.Transport).TransportCapacity = 8;
            U("id_gorgon", "Gorgon Assault Gunship", F, Ai, Locomotor.Helicopter, 2400, 30, 1000, ArmorType.Aircraft, 3.6f, 9f, R.AdvancedHangar, 4, "adv_heli", "Flying fortress gunship with rocket barrages and a flak turret.", "gorgon_rockets", "gorgon_aa").Tech("id_gorgon_program").Req(R.ResearchLab).Ammo = 24;
            U("id_pike", "Pike Patrol Boat", F, Nv, Locomotor.Naval, 560, 9, 420, ArmorType.Naval, 4.0f, 8f, R.Shipyard, 1, "patrol", "Patrol boat with twin heavy machine guns.", "patrol_id");
            U("id_hydra", "Hydra Attack Sub", F, Nv, Locomotor.Submarine, 1000, 15, 700, ArmorType.Naval, 2.6f, 6.5f, R.Shipyard, 2, "sub", "Heavy torpedo submarine.", "torpedo_id").Ab(UnitAbility.Submerge, UnitAbility.Stealth);
            U("id_bastion", "Bastion Destroyer", F, Nv, Locomotor.Naval, 1300, 19, 1100, ArmorType.Naval, 2.8f, 8f, R.Shipyard, 2, "destroyer", "Gun destroyer with depth charges, sonar and flak.", "destroyer_gun_id", "destroyer_asw_id", "destroyer_aa_id").Ab(UnitAbility.Detector).Req(R.Radar);
            U("id_monarch", "Iron Monarch Battleship", F, Nv, Locomotor.Naval, 3200, 44, 2800, ArmorType.Naval, 1.5f, 8f, R.AdvancedShipyard, 4, "battleship", "Armored behemoth with triple 18-inch turrets.", "battleship_id").Req(R.TechCenter);
            U("id_scourge", "Scourge Missile Cruiser", F, Nv, Locomotor.Naval, 2000, 28, 1500, ArmorType.Naval, 2.2f, 9f, R.AdvancedShipyard, 3, "cruiser", "Rocket battery cruiser for shore bombardment.", "cruiser_id", "cruiser_id_aa");
            U("id_barge", "Hauler Barge", F, Nv, Locomotor.Naval, 800, 11, 900, ArmorType.Naval, 2.6f, 6f, R.Shipyard, 1, "lst", "Armored landing barge for 8 infantry or vehicles.").Ab(UnitAbility.Transport).TransportCapacity = 8;
            Units["id_barge"].TransportVehicles = true;
            U("id_tender", "Repair Barge", F, Nv, Locomotor.Naval, 800, 11, 800, ArmorType.Naval, 2.6f, 7f, R.Shipyard, 2, "tender", "Floating workshop that repairs nearby vessels.").Ab(UnitAbility.RepairNaval).HealRate = 18;

            // Crush rules
            foreach (var u in Units.Values)
                if (u.Category == UnitCategory.Vehicle && (u.Locomotor == Locomotor.Tracked) && u.Armor >= ArmorType.Medium) u.CanCrush = true;
            foreach (var u in Units.Values)
                if (u.Category == UnitCategory.Aircraft) u.CruiseHeight = u.Locomotor == Locomotor.Helicopter ? 3.5f : 6f;
        }

        // ---------------------------------------------------------------- Buildings
        static BuildingDef B(string id, string name, FactionId f, BuildingRole role, int cost, float time, float hp, int w, int h, int power, int tier,
            string desc, params BuildingRole[] prereq)
        {
            var b = new BuildingDef
            {
                Id = id, Name = name, Faction = f, Role = role, Cost = cost, BuildTime = time, Health = hp, Width = w, Height = h, Power = power,
                Tier = tier, Description = desc, Model = role.ToString()
            };
            b.Prerequisites.AddRange(prereq);
            b.Sight = Math.Max(4, Math.Max(w, h) + 3);
            if (role == R.Wall) b.Armor = ArmorType.Wall;
            Buildings[id] = b;
            return b;
        }

        static void BuildBuildings()
        {
            foreach (FactionId f in new[] { FactionId.Alliance, FactionId.Dominion })
            {
                bool ca = f == FactionId.Alliance;
                string p = ca ? "ca_" : "id_";
                Func<string, string, string> nm = (a, d) => ca ? a : d;

                var hq = B(p + "hq", nm("Command Center", "Command Bunker"), f, R.ConstructionYard, 3000, 40, ca ? 2200 : 2600, 3, 3, 0, 1, "Central construction facility. Builds all structures.");
                hq.Buildable = false; hq.Storage = 2000; hq.Sight = 8;
                B(p + "power", nm("Fusion Generator", "Combustion Plant"), f, R.Power, ca ? 350 : 300, 10, ca ? 700 : 800, 2, 2, 100, 1, "Generates electrical power.", R.ConstructionYard);
                B(p + "refinery", nm("Ore Processing Plant", "Ore Smelter"), f, R.Refinery, 2000, 22, 1100, 3, 3, -30, 1, "Processes ore into credits. Comes with a free harvester.", R.Power).Storage = 4000;
                B(p + "silo", nm("Ore Silo", "Ore Bunker"), f, R.Silo, 200, 6, 400, 1, 1, -5, 1, "Stores additional credits.", R.Refinery).Storage = 3000;
                B(p + "barracks", nm("Training Camp", "Conscription Hall"), f, R.Barracks, ca ? 500 : 400, 12, 800, 2, 2, -20, 1, "Trains infantry.", R.Power).Hotkey = "B";
                B(p + "factory", nm("Vehicle Assembly", "Tank Foundry"), f, R.Factory, 2000, 24, 1500, 3, 3, -30, 1, "Manufactures ground vehicles.", R.Refinery).Hotkey = "F";
                B(p + "radar", nm("Radar Array", "Radar Tower"), f, R.Radar, 1000, 16, 900, 2, 2, -40, 2, "Enables the minimap and unlocks advanced units.", R.Refinery).RevealRadius = 0;
                Buildings[p + "radar"].Sight = 10;
                B(p + "repair", nm("Service Depot", "Field Workshop"), f, R.RepairDepot, 1000, 16, 900, 3, 3, -30, 2, "Repairs vehicles parked on its pad.", R.Factory);
                B(p + "hangar", nm("Air Command Hangar", "Airstrip Hangar"), f, R.Hangar, 1200, 18, 1000, 3, 2, -30, 2, "Builds and rearms aircraft (4 pads).", R.Radar).Docks = 4;
                var yard = B(p + "shipyard", nm("Naval Yard", "Dockyard"), f, R.Shipyard, 1200, 18, 1200, 3, 3, -30, 2, "Builds and repairs naval units. Must be placed on water.", R.Refinery);
                yard.OnWater = true;
                B(p + "techcenter", nm("Technology Center", "Research Bureau"), f, R.TechCenter, 1500, 24, 900, 2, 2, -80, 3, "Unlocks tier-3 structures and researches upgrades.", R.Factory, R.Radar);
                B(p + "advbarracks", nm("Special Operations Academy", "Elite Guard Garrison"), f, R.AdvancedBarracks, 1200, 20, 1000, 2, 3, -40, 3, "Trains advanced infantry.", R.Barracks, R.TechCenter);
                B(p + "advfactory", nm("Advanced Armor Works", "Heavy Assembly Works"), f, R.AdvancedFactory, 2500, 30, 1800, 3, 4, -60, 3, "Manufactures heavy and advanced vehicles.", R.Factory, R.TechCenter);
                B(p + "advhangar", nm("Advanced Aviation Complex", "Strategic Aviation Command"), f, R.AdvancedHangar, 2200, 28, 1300, 3, 3, -60, 3, "Builds strategic aircraft (2 pads).", R.Hangar, R.TechCenter).Docks = 2;
                var ay = B(p + "advshipyard", nm("Advanced Naval Yard", "Heavy Dockyard"), f, R.AdvancedShipyard, 2200, 28, 1600, 4, 3, -60, 3, "Builds capital ships.", R.Shipyard, R.TechCenter);
                ay.OnWater = true;
                B(p + "advpower", nm("Advanced Fusion Plant", "Heavy Reactor"), f, R.AdvancedPower, ca ? 800 : 700, 18, ca ? 1100 : 1300, 3, 2, ca ? 250 : 220, 3, "High-output power plant.", R.TechCenter);
                B(p + "lab", nm("Advanced Research Laboratory", "Advanced Weapons Institute"), f, R.ResearchLab, 2500, 32, 1100, 3, 3, -100, 4, "Unlocks experimental units, superweapons and top-tier research.", R.TechCenter);
                var comm = B(p + "comm", nm("Communications Uplink", "Signal Command"), f, R.CommCenter, 1500, 24, 900, 2, 2, -60, 4, ca ? "Detects cloaked units and enables the Skyeye Reveal satellite scan." : "Detects cloaked units and enables Airborne Drop.", R.ResearchLab);
                comm.DetectRadius = 14; comm.Sight = 12;
                comm.Power_Kind = ca ? SupportPowerKind.SkyeyeReveal : SupportPowerKind.Paradrop;
                var sw = B(p + "superweapon", nm("Helios Orbital Lance Control", "Hammerfall Launch Complex"), f, R.Superweapon, 5000, 60, 1800, 3, 3, -150, 4,
                    ca ? "Calls down a kinetic strike from orbit." : "Launches a thermobaric Hammerfall ballistic missile.", R.ResearchLab);
                sw.Power_Kind = ca ? SupportPowerKind.OrbitalLance : SupportPowerKind.HammerfallMissile;
                var sp = B(p + "special", nm("Pulse Array", "Grand Battery"), f, R.SpecialPower, 2500, 36, 1300, 2, 2, -100, 4,
                    ca ? "Fires an electromagnetic pulse that disables vehicles and structures." : "Long-range bombardment of a target area.", R.CommCenter);
                sp.Power_Kind = ca ? SupportPowerKind.EmpPulse : SupportPowerKind.Bombardment;
                var cm = B(p + "countermeasure", nm("Interceptor Battery", "Scatter Field Generator"), f, R.Countermeasure, 1800, 28, 1200, 2, 2, -80, 3,
                    ca ? "Intercepts ballistic missiles and bombardment shells over the base, greatly reducing their damage." : "Projects a scattering field that diffuses orbital strikes and energy pulses over the base.", R.TechCenter);
                cm.AntiSuperweapon = ca ? 0.6f : 0.5f; cm.CountermeasureRadius = 14;

                // Defenses
                var mg = B(p + "mgbunker", nm("Pillbox", "MG Nest"), f, R.Defense, ca ? 500 : 400, 10, ca ? 700 : 650, 1, 1, 0, 1, "Machine-gun bunker effective against infantry.", R.Barracks);
                mg.Weapons.Add(ca ? "bunker_mg_ca" : "bunker_mg_id"); mg.Model = "mgbunker";
                var ai = B(p + "aiturret", nm("Sentry Gun", "Incinerator Turret"), f, R.Defense, ca ? 700 : 600, 12, ca ? 750 : 700, 1, 1, -15, 2, "Anti-infantry turret.", R.Barracks);
                ai.Weapons.Add(ca ? "sentry_ca" : "incinerator_id"); ai.Model = "aiturret"; ai.RequiresPowerToFire = true;
                var av = B(p + "avturret", nm("Rail Turret", "Bulwark Cannon"), f, R.Defense, ca ? 1100 : 900, 16, ca ? 1000 : 1100, 1, 1, -30, 2, "Anti-vehicle turret.", R.Factory);
                av.Weapons.Add(ca ? "railturret_ca" : "cannon_turret_id"); av.Model = "avturret"; av.RequiresPowerToFire = true;
                var aa = B(p + "aadefense", nm("Skyshield Battery", "Flak Battery"), f, R.Defense, ca ? 900 : 750, 14, 800, 1, 1, -30, 2, "Anti-aircraft defense.", R.Radar);
                aa.Weapons.Add(ca ? "skyshield_ca" : "flak_battery_id"); aa.Model = "aadefense"; aa.RequiresPowerToFire = true;
                var art = B(p + "artillery", nm("Citadel Artillery", "Grand Mortar Pit"), f, R.Defense, ca ? 1600 : 1400, 24, ca ? 1100 : 1200, 2, 2, -60, 3, "Long-range heavy artillery emplacement.", R.TechCenter);
                art.Weapons.Add(ca ? "citadel_arty_ca" : "mortar_pit_id"); art.Model = "artillery"; art.RequiresPowerToFire = true;
                var wall = B(p + "wall", nm("Composite Wall", "Iron Wall"), f, R.Wall, ca ? 60 : 50, 2, ca ? 600 : 700, 1, 1, 0, 1, "Blocks enemy movement.", R.ConstructionYard);
                wall.Rotatable = true; wall.Capturable = false; wall.Sight = 1; wall.Model = "wall";
                var gate = B(p + "gate", nm("Security Gate", "Iron Gate"), f, R.Gate, ca ? 200 : 180, 4, ca ? 900 : 1000, 1, 1, 0, 1, "Opens for friendly units only.", R.ConstructionYard);
                gate.Rotatable = true; gate.Capturable = false; gate.Sight = 2; gate.Model = "gate";
                foreach (var d in new[] { mg, ai, av, aa, art }) d.Capturable = false;
            }
            // Detection on defenses (CA precision): sentry guns detect stealth nearby.
            Buildings["ca_aiturret"].DetectRadius = 5;
            Buildings["id_mgbunker"].DetectRadius = 4;
        }

        static void BuildNeutral()
        {
            var F = FactionId.Neutral;
            var house = B("civ_house", "Farmhouse", F, R.Civilian, 0, 0, 400, 2, 2, 0, 0, "Civilian dwelling."); house.Buildable = false; house.Capturable = false; house.Model = "civ_house";
            var barn = B("civ_barn", "Barn", F, R.Civilian, 0, 0, 500, 2, 3, 0, 0, "Civilian barn."); barn.Buildable = false; barn.Capturable = false; barn.Model = "civ_barn";
            var chapel = B("civ_chapel", "Chapel", F, R.Civilian, 0, 0, 700, 2, 2, 0, 0, "Old chapel."); chapel.Buildable = false; chapel.Capturable = false; chapel.Model = "civ_chapel";
            var tower = B("civ_watertower", "Water Tower", F, R.Civilian, 0, 0, 300, 1, 1, 0, 0, "Water tower."); tower.Buildable = false; tower.Capturable = false; tower.Model = "civ_watertower";
            var derrick = B("tech_derrick", "Ore Derrick", F, R.TechStructure, 0, 0, 800, 2, 2, 0, 0, "Capture with an engineer to receive a steady credit income."); derrick.Buildable = false; derrick.Model = "tech_derrick";
            var hosp = B("tech_hospital", "Field Hospital", F, R.TechStructure, 0, 0, 900, 2, 2, 0, 0, "Capture to heal friendly infantry nearby."); hosp.Buildable = false; hosp.Model = "tech_hospital";
            var relay = B("tech_relay", "Comms Relay", F, R.TechStructure, 0, 0, 700, 2, 2, 0, 0, "Capture to reveal a wide area."); relay.Buildable = false; relay.RevealRadius = 12; relay.Model = "tech_relay";
            var depot = B("tech_depot", "Supply Depot", F, R.TechStructure, 0, 0, 1000, 3, 2, 0, 0, "Mission objective storage facility."); depot.Buildable = false; depot.Model = "tech_depot";
            var bunker = B("civ_bunker", "Old Fortification", F, R.Defense, 0, 0, 900, 1, 1, 0, 0, "Abandoned fortification manned by militia."); bunker.Buildable = false; bunker.Capturable = false; bunker.Weapons.Add("civ_militia"); bunker.Model = "mgbunker";
            // Civilians as units
            var civ = U("civ_militia", "Militia", F, UnitCategory.Infantry, Locomotor.Foot, 0, 0, 80, ArmorType.Infantry, 1.3f, 5f, R.Civilian, 0, "inf_rifle", "Local militia.", "civ_militia");
            civ.Prerequisites.Clear();
            var truck = U("civ_truck", "Supply Truck", F, UnitCategory.Vehicle, Locomotor.Wheeled, 0, 0, 300, ArmorType.Light, 2.6f, 5f, R.Civilian, 0, "convoy_truck", "Convoy supply truck.");
            truck.Prerequisites.Clear();
        }

        // ---------------------------------------------------------------- Technology
        static TechDef T(string id, string name, FactionId f, int cost, float time, BuildingRole at, int tier, string desc, params TechEffect[] fx)
        {
            var t = new TechDef { Id = id, Name = name, Faction = f, Cost = cost, ResearchTime = time, ResearchedAt = at, Tier = tier, Description = desc };
            t.Prerequisites.Add(at);
            t.Effects.AddRange(fx);
            Techs[id] = t;
            return t;
        }

        static void BuildTechs()
        {
            var CA = FactionId.Alliance; var ID = FactionId.Dominion;
            T("ca_composite_armor", "Composite Armor", CA, 1500, 60, R.TechCenter, 3, "+20% health for all vehicles.", new TechEffect(TechEffectKind.HealthMult, 1.2f, UnitCategory.Vehicle));
            T("ca_precision_optics", "Precision Optics", CA, 1000, 45, R.TechCenter, 3, "+15% weapon range and +20% sight for infantry.", new TechEffect(TechEffectKind.RangeMult, 1.15f, UnitCategory.Infantry), new TechEffect(TechEffectKind.SightMult, 1.2f, UnitCategory.Infantry));
            T("ca_automated_logistics", "Automated Logistics", CA, 1500, 60, R.TechCenter, 3, "+20% production speed for all queues.", new TechEffect(TechEffectKind.ProductionSpeed, 1.2f));
            T("ca_refined_extraction", "Refined Extraction", CA, 1200, 50, R.TechCenter, 3, "+25% harvesting rate.", new TechEffect(TechEffectKind.HarvestRate, 1.25f));
            T("ca_advanced_avionics", "Advanced Avionics", CA, 1500, 60, R.TechCenter, 3, "+25% aircraft speed and 30% faster rearming.", new TechEffect(TechEffectKind.SpeedMult, 1.25f, UnitCategory.Aircraft), new TechEffect(TechEffectKind.ReloadMult, 0.7f, UnitCategory.Aircraft)).Prerequisites.Add(R.Hangar);
            T("ca_hardened_emplacements", "Hardened Emplacements", CA, 1200, 50, R.TechCenter, 3, "+30% health for defensive structures.", new TechEffect(TechEffectKind.HealthMult, 1.3f, null, "defense"));
            T("ca_smart_munitions", "Smart Munitions", CA, 2500, 80, R.ResearchLab, 4, "+15% damage for vehicles, aircraft and ships.", new TechEffect(TechEffectKind.DamageMult, 1.15f, UnitCategory.Vehicle), new TechEffect(TechEffectKind.DamageMult, 1.15f, UnitCategory.Aircraft), new TechEffect(TechEffectKind.DamageMult, 1.15f, UnitCategory.Naval));
            T("ca_phantom_program", "Phantom Program", CA, 2000, 70, R.ResearchLab, 4, "Unlocks the Phantom Operative.", new TechEffect(TechEffectKind.UnlockUnit, 1, null, "ca_phantom"));
            T("ca_specter_program", "Specter Program", CA, 3000, 90, R.ResearchLab, 4, "Unlocks the Specter Stealth Striker.", new TechEffect(TechEffectKind.UnlockUnit, 1, null, "ca_specter")).Prerequisites.Add(R.AdvancedHangar);

            T("id_reactive_plating", "Reactive Plating", ID, 1500, 60, R.TechCenter, 3, "+25% health for all vehicles.", new TechEffect(TechEffectKind.HealthMult, 1.25f, UnitCategory.Vehicle));
            T("id_high_explosives", "High-Explosive Shells", ID, 1200, 50, R.TechCenter, 3, "+20% damage for explosive (HE) weapons.", new TechEffect(TechEffectKind.DamageMult, 1.2f, null, "HE"));
            T("id_conscription", "Conscription Drive", ID, 1000, 45, R.TechCenter, 3, "Infantry cost 25% less.", new TechEffect(TechEffectKind.CostMult, 0.75f, UnitCategory.Infantry));
            T("id_mass_assembly", "Mass Assembly", ID, 1500, 60, R.TechCenter, 3, "+25% production speed.", new TechEffect(TechEffectKind.ProductionSpeed, 1.25f));
            T("id_deep_core_drills", "Deep-Core Drills", ID, 1200, 50, R.TechCenter, 3, "+20% harvesting rate.", new TechEffect(TechEffectKind.HarvestRate, 1.2f));
            T("id_fortified_bunkers", "Fortified Bunkers", ID, 1200, 50, R.TechCenter, 3, "+25% health for defensive structures.", new TechEffect(TechEffectKind.HealthMult, 1.25f, null, "defense"));
            T("id_incendiary_payloads", "Incendiary Payloads", ID, 2000, 70, R.ResearchLab, 4, "+30% damage for flame weapons.", new TechEffect(TechEffectKind.DamageMult, 1.3f, null, "Flame"));
            T("id_blackguard_program", "Black Guard Program", ID, 2000, 70, R.ResearchLab, 4, "Unlocks the Black Guard Commando.", new TechEffect(TechEffectKind.UnlockUnit, 1, null, "id_blackguard"));
            T("id_gorgon_program", "Gorgon Program", ID, 3000, 90, R.ResearchLab, 4, "Unlocks the Gorgon Assault Gunship.", new TechEffect(TechEffectKind.UnlockUnit, 1, null, "id_gorgon")).Prerequisites.Add(R.AdvancedHangar);
        }

        static void BuildPowers()
        {
            Powers[SupportPowerKind.OrbitalLance] = new SupportPowerDef { Kind = SupportPowerKind.OrbitalLance, Name = "Helios Orbital Lance", Description = "Kinetic strike from orbit. Massive damage in a focused area.", ChargeTime = 420, Radius = 4.5f, Damage = 2600, WarningTime = 6, IsSuperweapon = true };
            Powers[SupportPowerKind.HammerfallMissile] = new SupportPowerDef { Kind = SupportPowerKind.HammerfallMissile, Name = "Hammerfall Missile", Description = "Thermobaric ballistic missile. Huge blast that leaves the ground burning.", ChargeTime = 480, Radius = 6.5f, Damage = 2100, WarningTime = 10, IsSuperweapon = true };
            Powers[SupportPowerKind.EmpPulse] = new SupportPowerDef { Kind = SupportPowerKind.EmpPulse, Name = "EMP Pulse", Description = "Disables vehicles, aircraft and structures in the area for 20 seconds.", ChargeTime = 240, Radius = 6, Damage = 0, WarningTime = 2 };
            Powers[SupportPowerKind.Bombardment] = new SupportPowerDef { Kind = SupportPowerKind.Bombardment, Name = "Grand Battery Bombardment", Description = "A barrage of heavy shells across the target area.", ChargeTime = 240, Radius = 7, Damage = 160, Warhead = "HE", WarningTime = 3 };
            Powers[SupportPowerKind.SkyeyeReveal] = new SupportPowerDef { Kind = SupportPowerKind.SkyeyeReveal, Name = "Skyeye Reveal", Description = "Satellite scan reveals a large area for 25 seconds.", ChargeTime = 150, Radius = 18, Damage = 0, WarningTime = 0 };
            Powers[SupportPowerKind.Paradrop] = new SupportPowerDef { Kind = SupportPowerKind.Paradrop, Name = "Airborne Drop", Description = "Drops a squad of infantry anywhere you have explored.", ChargeTime = 180, Radius = 2, Damage = 0, WarningTime = 3 };
        }

        static void BuildFactions()
        {
            var ca = new FactionDef
            {
                Id = FactionId.Alliance, Name = "Continental Alliance", ShortName = "Alliance", Motto = "Precision. Unity. Resolve.",
                Description = "A technologically advanced coalition fielding precise, expensive high-performance forces, superior aircraft and formidable defenses.",
                PrimaryColor = new[] { 0.22f, 0.45f, 0.85f }, SecondaryColor = new[] { 0.85f, 0.88f, 0.92f },
                StartingVehicle = "ca_mhq", HarvesterUnit = "ca_prospector", MusicTheme = "alliance", VoiceStyle = "alliance",
                Powers = { SupportPowerKind.OrbitalLance, SupportPowerKind.EmpPulse, SupportPowerKind.SkyeyeReveal }
            };
            var id = new FactionDef
            {
                Id = FactionId.Dominion, Name = "Iron Dominion", ShortName = "Dominion", Motto = "Steel Without End.",
                Description = "An industrial war empire of heavy armor, massed infantry, crushing artillery and destructive siege weapons.",
                PrimaryColor = new[] { 0.80f, 0.16f, 0.12f }, SecondaryColor = new[] { 0.35f, 0.33f, 0.28f },
                StartingVehicle = "id_foundry", HarvesterUnit = "id_hauler", MusicTheme = "dominion", VoiceStyle = "dominion",
                Powers = { SupportPowerKind.HammerfallMissile, SupportPowerKind.Bombardment, SupportPowerKind.Paradrop }
            };
            var neutral = new FactionDef { Id = FactionId.Neutral, Name = "Civilians", ShortName = "Civilian", PrimaryColor = new[] { 0.7f, 0.7f, 0.6f }, SecondaryColor = new[] { 0.5f, 0.5f, 0.5f } };
            foreach (var u in Units.Values)
                (u.Faction == FactionId.Alliance ? ca : u.Faction == FactionId.Dominion ? id : neutral).Units.Add(u.Id);
            foreach (var b in Buildings.Values)
                (b.Faction == FactionId.Alliance ? ca : b.Faction == FactionId.Dominion ? id : neutral).Buildings.Add(b.Id);
            foreach (var t in Techs.Values)
                (t.Faction == FactionId.Alliance ? ca : id).Techs.Add(t.Id);
            Factions[FactionId.Alliance] = ca;
            Factions[FactionId.Dominion] = id;
            Factions[FactionId.Neutral] = neutral;
        }

        /// <summary>Find the faction's building with a given role (first match).</summary>
        public static BuildingDef BuildingForRole(FactionId f, BuildingRole role)
        {
            EnsureBuilt();
            foreach (var id in Faction(f).Buildings)
            {
                var b = Buildings[id];
                if (b.Role == role) return b;
            }
            return null;
        }

        public static UnitDef UnitByModel(FactionId f, string model)
        {
            EnsureBuilt();
            foreach (var id in Faction(f).Units) if (Units[id].Model == model) return Units[id];
            return null;
        }

        public static ProductionQueueType QueueFor(UnitDef u)
        {
            switch (u.Category)
            {
                case UnitCategory.Infantry: return ProductionQueueType.Infantry;
                case UnitCategory.Vehicle: return ProductionQueueType.Vehicles;
                case UnitCategory.Aircraft: return ProductionQueueType.Aircraft;
                default: return ProductionQueueType.Naval;
            }
        }
    }
}
