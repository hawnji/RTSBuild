using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public enum AIDifficulty { Easy = 0, Normal = 1, Hard = 2, Brutal = 3 }

    public enum FogMode { Shroud = 0, Explored = 1, Off = 2 }

    public enum VictoryCondition { DestroyStructures = 0, Annihilation = 1, Commander = 2 }

    public enum StartingUnits { MCVOnly = 0, Light = 1, Heavy = 2 }

    public enum StartingBase { MCV = 0, ConstructionYard = 1, SmallBase = 2 }

    [Serializable]
    public class GameSettings
    {
        public int StartingCredits = 10000;
        public FogMode Fog = FogMode.Shroud;
        public bool Superweapons = true;
        public VictoryCondition Victory = VictoryCondition.DestroyStructures;
        public StartingUnits StartUnits = StartingUnits.Light;
        public StartingBase StartBase = StartingBase.MCV;
        public float GameSpeed = 1f;
        public bool OreRegrowth = true;
        public int Seed = 12345;
    }

    [Serializable]
    public class PlayerStats
    {
        public int UnitsBuilt, UnitsLost, UnitsKilled, BuildingsBuilt, BuildingsLost, BuildingsKilled;
        public float CreditsHarvested, CreditsSpent;
    }

    [Serializable]
    public class Player
    {
        public int Id;
        public string Name;
        public int Team;
        public FactionId Faction;
        public int ColorIndex;
        public bool IsHuman;
        public bool IsAI;
        public bool IsNeutral;
        public AIDifficulty Difficulty = AIDifficulty.Normal;
        public float Credits;
        public int StorageCap;
        public int PowerProduced;
        public int PowerConsumed;
        public bool Defeated;
        public Int2 StartCell;
        public List<ProductionItem>[] Queues = new List<ProductionItem>[7];
        public string[] Ready = new string[7];          // completed structures waiting for placement
        public List<string> Researched = new List<string>();
        public float[] PowerCharge = new float[6];      // indexed by SupportPowerKind
        public bool[] PowerOwned = new bool[6];
        public bool[] PowerReady = new bool[6];
        public PlayerStats Stats = new PlayerStats();
        public float LowPowerCooldown, FundsCooldown, AttackCooldown, SiloCooldown;
        public bool HasRadar;
        public bool AIBuilds = true;        // mission scripts can restrict AI behaviour
        public bool AIAttacks = true;
        public bool AIHarvests = true;
        public float AIAggression = 1f;
        public bool Revealed;               // full map visibility (spectator/cheat/script)
        public int MaxTechTier = 99;        // campaign progression limits available tech
        public bool PlayerControlled => IsHuman;

        [NonSerialized] public TechModifiers Mods = new TechModifiers();

        public Player()
        {
            for (int i = 0; i < Queues.Length; i++) Queues[i] = new List<ProductionItem>();
        }

        public float PowerRatio => PowerConsumed <= 0 ? 1f : Math.Min(1f, PowerProduced / (float)PowerConsumed);
        public bool LowPower => PowerConsumed > PowerProduced;
        public bool HasTech(string id) => Researched.Contains(id);
    }

    /// <summary>Aggregated multipliers from completed research. Recomputed when research completes.</summary>
    public class TechModifiers
    {
        readonly Dictionary<string, float> _mult = new Dictionary<string, float>();
        public float ProductionSpeed = 1f;
        public float HarvestRate = 1f;

        public void Recompute(Player p)
        {
            _mult.Clear(); ProductionSpeed = 1f; HarvestRate = 1f;
            foreach (var id in p.Researched)
            {
                var t = Catalog.Tech(id);
                if (t == null) continue;
                foreach (var fx in t.Effects)
                {
                    switch (fx.Kind)
                    {
                        case TechEffectKind.ProductionSpeed: ProductionSpeed *= fx.Value; break;
                        case TechEffectKind.HarvestRate: HarvestRate *= fx.Value; break;
                        case TechEffectKind.UnlockUnit: break;
                        default:
                            string key = fx.Kind + ":" + (fx.Category.HasValue ? fx.Category.Value.ToString() : "*") + ":" + (fx.Target ?? "*");
                            _mult[key] = (_mult.TryGetValue(key, out var v) ? v : 1f) * fx.Value;
                            break;
                    }
                }
            }
        }

        float Get(TechEffectKind k, string cat, string target)
        {
            float r = 1f;
            if (_mult.Count == 0) return r;
            if (_mult.TryGetValue(k + ":" + cat + ":*", out var a)) r *= a;
            if (_mult.TryGetValue(k + ":*:*", out var b)) r *= b;
            if (target != null && _mult.TryGetValue(k + ":*:" + target, out var c)) r *= c;
            return r;
        }

        public float UnitMult(TechEffectKind k, UnitDef u, string target = null) => Get(k, u.Category.ToString(), target);
        public float BuildingHealthMult(BuildingDef b) => Get(TechEffectKind.HealthMult, "Building", b.Role == BuildingRole.Defense ? "defense" : null);
        public float WarheadDamage(string warhead) => Get(TechEffectKind.DamageMult, "None", warhead);
    }

    public enum SimEventType
    {
        Fire, Impact, Explosion, UnitDied, BuildingDestroyed, BuildingPlaced, ConstructionComplete, UnitReady, ResearchComplete,
        InsufficientFunds, LowPower, PowerRestored, UnderAttack, HarvesterUnderAttack, SupportPowerReady, SupportPowerLaunched, SupportPowerImpact,
        Captured, Sold, Promoted, OreDelivered, SilosNeeded, MissionMessage, ObjectiveUpdated, Reinforcements, PlayerDefeated,
        GameOver, CannotPlace, UnitLost, BuildingLost, SuperweaponDetected, Healed, Repairing, Paradrop, EmpHit, Crushed, Deployed,
        CameraPan, CinematicStart, CinematicEnd, TimerStarted, Unloaded, Loaded, BridgeDestroyed, TreeDestroyed, Intercepted, Hint
    }

    [Serializable]
    public struct SimEvent
    {
        public SimEventType Type;
        public int Player;      // player concerned (-1 = everyone)
        public int EntityId;
        public int OtherId;
        public Vec2 Pos;
        public Vec2 Pos2;
        public string Text;     // def id, weapon id, message text, etc.
        public string Text2;    // speaker etc.
        public float Value;
    }
}
