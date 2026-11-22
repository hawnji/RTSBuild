using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public enum FactionId { Alliance = 0, Dominion = 1, Neutral = 2 }

    public enum ArmorType { Infantry, HeavyInfantry, Light, Medium, Heavy, Aircraft, Naval, Building, Wall }

    public enum UnitCategory { Infantry, Vehicle, Aircraft, Naval }

    public enum Locomotor { Foot, Wheeled, Tracked, Air, Helicopter, Naval, Submarine }

    public enum ProjectileKind { Hitscan, Bullet, Shell, Ballistic, Missile, Flame, Torpedo, Bomb, Beam, Rail }

    /// <summary>Which "layer" a target is on; weapons declare which layers they can engage.</summary>
    [Flags]
    public enum TargetLayer { None = 0, Ground = 1, Air = 2, Naval = 4, Submerged = 8, Structure = 16, All = 31 }

    public enum BuildingRole
    {
        ConstructionYard, Refinery, Silo, Power, AdvancedPower,
        Barracks, AdvancedBarracks, Factory, AdvancedFactory, RepairDepot,
        Hangar, AdvancedHangar, Shipyard, AdvancedShipyard,
        TechCenter, ResearchLab, Radar, CommCenter, Superweapon,
        Defense, Wall, Gate, SpecialPower, Countermeasure,
        Civilian, TechStructure
    }

    /// <summary>Production tabs on the sidebar.</summary>
    public enum ProductionQueueType { Structures = 0, Defenses = 1, Infantry = 2, Vehicles = 3, Aircraft = 4, Naval = 5, Research = 6 }

    public enum UnitAbility
    {
        None, Harvester, DeployToHQ, CaptureRepair, HealInfantry, RepairVehicles, Transport,
        Stealth, Detector, Demolition, Recon, Submerge, RepairNaval, Paradrop, EmpShot
    }

    public enum SupportPowerKind { OrbitalLance, HammerfallMissile, EmpPulse, Bombardment, SkyeyeReveal, Paradrop }

    public enum TechEffectKind
    {
        DamageMult, HealthMult, SpeedMult, RangeMult, ProductionSpeed, HarvestRate, CostMult, UnlockUnit, ReloadMult, SightMult
    }

    [Serializable]
    public class WarheadDef
    {
        public string Id;
        // Multipliers indexed by ArmorType.
        public float[] Vs = new float[9];
        public float Get(ArmorType a) => Vs[(int)a];

        public WarheadDef(string id, float inf, float hinf, float light, float med, float heavy, float air, float naval, float bld, float wall)
        {
            Id = id; Vs = new[] { inf, hinf, light, med, heavy, air, naval, bld, wall };
        }
    }

    [Serializable]
    public class WeaponDef
    {
        public string Id;
        public string Name;
        public float Damage;
        public float Range;
        public float MinRange;
        public float Cooldown = 1f;          // seconds between shots within a clip
        public int Clip = 0;                 // 0 = unlimited clip (no reload)
        public float ReloadTime = 0;         // seconds to refill clip
        public int Burst = 1;                // projectiles per shot
        public ProjectileKind Projectile = ProjectileKind.Bullet;
        public float ProjectileSpeed = 30f;  // cells per second
        public float Splash;                 // radius in cells
        public float Inaccuracy;             // scatter radius at max range (cells)
        public string Warhead = "Bullet";
        public TargetLayer Targets = TargetLayer.Ground | TargetLayer.Structure;
        public bool FriendlyFire;
        public float Suppression;            // seconds of suppression applied to infantry
        public bool BurnsGround;
        public float EmpSeconds;             // disables vehicles/buildings
        public string Sound = "rifle";
        public bool RequiresPower;           // structure weapons only
        public bool Turreted = true;         // can aim without turning the chassis
        public int AmmoCost = 0;             // ammunition used per shot (aircraft)
    }

    [Serializable]
    public class UnitDef
    {
        public string Id;
        public string Name;
        public FactionId Faction;
        public UnitCategory Category;
        public Locomotor Locomotor;
        public string Description;
        public int Cost;
        public float BuildTime;          // seconds at normal speed
        public float Health;
        public ArmorType Armor;
        public float Speed;              // cells per second
        public float TurnRate = 6f;      // radians per second
        public float Sight = 6f;
        public float Radius = 0.35f;
        public List<string> Weapons = new List<string>();
        public List<BuildingRole> Prerequisites = new List<BuildingRole>(); // satisfied by owning any building with each role
        public List<string> RequiredTech = new List<string>();
        public BuildingRole ProducedAt;
        public int Tier = 1;
        public List<UnitAbility> Abilities = new List<UnitAbility>();
        public int TransportCapacity;
        public bool TransportVehicles;
        public int Ammo;                 // aircraft ammunition (0 = unlimited)
        public float HarvestCapacity;    // ore units
        public float HarvestRate;        // ore per second
        public float HealRate;           // hp per second for medics/repair vehicles
        public float TargetPriority = 1f;
        public bool Crushable;
        public bool CanCrush;
        public string Model;             // archetype key for procedural model builder
        public string Voice = "infantry";
        public string Hotkey;
        public float CruiseHeight;
        public string DeploysInto;
        public string Role => Category.ToString();

        public bool Has(UnitAbility a) => Abilities.Contains(a);

        public TargetLayer Layer
        {
            get
            {
                switch (Locomotor)
                {
                    case Locomotor.Air:
                    case Locomotor.Helicopter: return TargetLayer.Air;
                    case Locomotor.Naval: return TargetLayer.Naval;
                    case Locomotor.Submarine: return TargetLayer.Naval;
                    default: return TargetLayer.Ground;
                }
            }
        }
    }

    [Serializable]
    public class BuildingDef
    {
        public string Id;
        public string Name;
        public FactionId Faction;
        public BuildingRole Role;
        public string Description;
        public int Cost;
        public float BuildTime;
        public float Health;
        public ArmorType Armor = ArmorType.Building;
        public int Width = 2;
        public int Height = 2;
        public int Power;                // positive = produces, negative = consumes
        public float Sight = 5f;
        public List<BuildingRole> Prerequisites = new List<BuildingRole>();
        public List<string> RequiredTech = new List<string>();
        public int Tier = 1;
        public List<string> Weapons = new List<string>();
        public int Storage;              // credits storage contributed
        public bool OnWater;             // shipyards
        public bool Rotatable;
        public bool RequiresPowerToFire;
        public string Model;
        public bool Capturable = true;
        public SupportPowerKind? Power_Kind;
        public float PowerCharge;        // seconds to charge a support power
        public float DetectRadius;       // stealth detection
        public float RevealRadius;
        public int Docks;                // aircraft pads
        public bool Selectable = true;
        public bool Buildable = true;    // false for civilian/tech structures
        public float AntiSuperweapon;    // countermeasure strength (0..1 damage reduction / intercept)
        public float CountermeasureRadius;
        public string Hotkey;
        public ProductionQueueType Queue => Role == BuildingRole.Defense || Role == BuildingRole.Wall || Role == BuildingRole.Gate
            ? ProductionQueueType.Defenses : ProductionQueueType.Structures;
    }

    [Serializable]
    public class TechEffect
    {
        public TechEffectKind Kind;
        public float Value;
        public UnitCategory? Category;    // null = all
        public string Target;             // unit id for UnlockUnit or weapon warhead filter
        public TechEffect(TechEffectKind k, float v, UnitCategory? cat = null, string target = null) { Kind = k; Value = v; Category = cat; Target = target; }
    }

    [Serializable]
    public class TechDef
    {
        public string Id;
        public string Name;
        public FactionId Faction;
        public string Description;
        public int Cost;
        public float ResearchTime;
        public BuildingRole ResearchedAt;
        public List<BuildingRole> Prerequisites = new List<BuildingRole>();
        public List<string> RequiredTech = new List<string>();
        public int Tier = 2;
        public List<TechEffect> Effects = new List<TechEffect>();
    }

    [Serializable]
    public class SupportPowerDef
    {
        public SupportPowerKind Kind;
        public string Name;
        public string Description;
        public float ChargeTime;
        public float Radius;
        public float Damage;
        public string Warhead = "Super";
        public float WarningTime;       // seconds between activation and impact
        public bool IsSuperweapon;
    }

    [Serializable]
    public class FactionDef
    {
        public FactionId Id;
        public string Name;
        public string ShortName;
        public string Motto;
        public string Description;
        public float[] PrimaryColor;   // rgb 0..1 used for faction branding
        public float[] SecondaryColor;
        public string StartingVehicle;  // construction vehicle id
        public string HarvesterUnit;
        public List<string> Units = new List<string>();
        public List<string> Buildings = new List<string>();
        public List<string> Techs = new List<string>();
        public List<SupportPowerKind> Powers = new List<SupportPowerKind>();
        public string MusicTheme;
        public string VoiceStyle;
    }
}
