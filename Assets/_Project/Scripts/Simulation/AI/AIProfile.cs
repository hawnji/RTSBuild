using System;

namespace RTS.Sim
{
    /// <summary>
    /// Difficulty parameters. Difficulty changes decision quality, reaction time, economic efficiency and tactical
    /// sophistication — never free resources or combat bonuses.
    /// </summary>
    [Serializable]
    public class AIProfile
    {
        public AIDifficulty Difficulty;
        public float ThinkInterval;        // seconds between strategic decisions (reaction time)
        public float MilitaryInterval;
        public float IdleChance;           // chance to skip a decision cycle (simulates slower play)
        public int MaxRefineries;
        public int HarvestersPerRefinery;
        public bool Expands;
        public int MaxTier;
        public bool Researches;
        public int TargetDefenses;
        public float FirstAttackTime;      // seconds
        public float AttackInterval;
        public int AttackMinValue;         // minimum army value (credits) before attacking
        public float AttackValueGrowth;    // per minute
        public float ScoutInterval;        // 0 = never
        public bool Flanks;                // multi-direction attacks
        public bool Harasses;              // harvester harassment groups
        public float CounterWeight;        // 0..1 how strongly production reacts to enemy composition
        public float RetreatHp;            // 0 = never retreat
        public bool UsesAir;
        public bool UsesNaval;
        public bool UsesSuperweapons;
        public bool RepairsStructures;
        public bool FocusFire;
        public bool CapturesTech;
        public float CreditReserve;        // credits kept back before spending on army
        public int MaxProducers;           // per production type
        public int MaxArmy;                // combat units kept at once (Easy plays with a smaller army)
        public bool WeighsOdds;            // compares its army with the enemy army it has seen before attacking

        public static AIProfile For(AIDifficulty d)
        {
            switch (d)
            {
                case AIDifficulty.Easy:
                    return new AIProfile
                    {
                        Difficulty = d, ThinkInterval = 2.5f, MilitaryInterval = 4f, IdleChance = 0.35f, MaxRefineries = 1, HarvestersPerRefinery = 2,
                        Expands = false, MaxTier = 2, Researches = false, TargetDefenses = 1, FirstAttackTime = 720, AttackInterval = 300,
                        AttackMinValue = 3000, AttackValueGrowth = 300, ScoutInterval = 0, Flanks = false, Harasses = false, CounterWeight = 0f,
                        RetreatHp = 0, UsesAir = false, UsesNaval = false, UsesSuperweapons = false, RepairsStructures = false, FocusFire = false,
                        CapturesTech = false, CreditReserve = 0, MaxProducers = 1, MaxArmy = 16, WeighsOdds = false
                    };
                case AIDifficulty.Normal:
                    return new AIProfile
                    {
                        Difficulty = d, ThinkInterval = 1.2f, MilitaryInterval = 2f, IdleChance = 0.12f, MaxRefineries = 2, HarvestersPerRefinery = 2,
                        Expands = true, MaxTier = 3, Researches = true, TargetDefenses = 3, FirstAttackTime = 480, AttackInterval = 210,
                        AttackMinValue = 5000, AttackValueGrowth = 600, ScoutInterval = 200, Flanks = false, Harasses = false, CounterWeight = 0.5f,
                        RetreatHp = 0.2f, UsesAir = true, UsesNaval = true, UsesSuperweapons = true, RepairsStructures = true, FocusFire = false,
                        CapturesTech = false, CreditReserve = 300, MaxProducers = 1, MaxArmy = 40, WeighsOdds = false
                    };
                case AIDifficulty.Hard:
                    return new AIProfile
                    {
                        Difficulty = d, ThinkInterval = 0.7f, MilitaryInterval = 1.2f, IdleChance = 0.03f, MaxRefineries = 3, HarvestersPerRefinery = 2,
                        Expands = true, MaxTier = 4, Researches = true, TargetDefenses = 5, FirstAttackTime = 330, AttackInterval = 160,
                        AttackMinValue = 6500, AttackValueGrowth = 900, ScoutInterval = 110, Flanks = true, Harasses = true, CounterWeight = 0.8f,
                        RetreatHp = 0.3f, UsesAir = true, UsesNaval = true, UsesSuperweapons = true, RepairsStructures = true, FocusFire = true,
                        CapturesTech = true, CreditReserve = 200, MaxProducers = 2, MaxArmy = 70, WeighsOdds = true
                    };
                default:
                    return new AIProfile
                    {
                        Difficulty = AIDifficulty.Brutal, ThinkInterval = 0.4f, MilitaryInterval = 0.8f, IdleChance = 0f, MaxRefineries = 4, HarvestersPerRefinery = 3,
                        Expands = true, MaxTier = 4, Researches = true, TargetDefenses = 7, FirstAttackTime = 270, AttackInterval = 120,
                        AttackMinValue = 7000, AttackValueGrowth = 1100, ScoutInterval = 70, Flanks = true, Harasses = true, CounterWeight = 1f,
                        RetreatHp = 0.35f, UsesAir = true, UsesNaval = true, UsesSuperweapons = true, RepairsStructures = true, FocusFire = true,
                        CapturesTech = true, CreditReserve = 0, MaxProducers = 3, MaxArmy = 100, WeighsOdds = true
                    };
            }
        }
    }
}
