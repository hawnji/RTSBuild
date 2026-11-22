using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    public enum OrderType
    {
        None, Move, AttackMove, Attack, ForceFireGround, Guard, Patrol, Stop, Harvest, ReturnCargo, Deploy,
        Enter, Unload, Capture, RepairAtDepot, Rearm, Follow, RepairStructure, Demolish
    }

    public enum Stance { Aggressive, Guard, HoldPosition, HoldFire }

    public enum HarvestState { Idle, Seeking, Moving, Harvesting, Returning, Docking, Unloading }

    public enum AirState { Flying, Landing, Docked, TakingOff }

    [Serializable]
    public struct Order
    {
        public OrderType Type;
        public Vec2 Pos;
        public Vec2 Pos2;      // patrol second point
        public int TargetId;

        public Order(OrderType t, Vec2 p, int target = 0) { Type = t; Pos = p; Pos2 = p; TargetId = target; }
        public static Order None => new Order(OrderType.None, Vec2.Zero);
    }

    [Serializable]
    public class WeaponState
    {
        public string WeaponId;
        public float Cooldown;
        public int ClipLeft;
        public float Reload;
        [NonSerialized] public WeaponDef Def;
    }

    [Serializable]
    public abstract class Entity
    {
        public int Id;
        public int Owner;
        public string DefId;
        public Vec2 Pos;
        public float Facing;          // radians, chassis
        public float TurretFacing;
        public float Hp;
        public float MaxHp;
        public bool Dead;
        public string Tag;            // mission scripting tag
        public float Xp;
        public int Rank;              // 0 rookie, 1 veteran, 2 elite
        public int TargetId;
        public float EmpTimer;
        public int LastAttackerId;
        public float LastDamagedTime = -100;
        public float SinceFired = 100;
        public List<WeaponState> Weapons = new List<WeaponState>();
        public bool Invulnerable;

        public abstract bool IsBuilding { get; }
        public abstract float Radius { get; }
        public abstract ArmorType Armor { get; }
        public abstract TargetLayer Layer { get; }
        public abstract int Cost { get; }
        public abstract float SightRange { get; }
        public abstract string Name { get; }
        public float HpRatio => MaxHp > 0 ? Hp / MaxHp : 0;
        public bool Alive => !Dead;
        public bool Disabled => EmpTimer > 0;
    }

    [Serializable]
    public class Unit : Entity
    {
        [NonSerialized] public UnitDef Def;
        public Vec2 PrevPos;
        public float Height;          // aircraft altitude
        public Order CurrentOrder;
        public List<Order> OrderQueue = new List<Order>();
        public Stance Stance = Stance.Aggressive;
        public Vec2 GuardPos;
        public bool HasGuardPos;
        public List<Int2> Path = new List<Int2>();
        public int PathIndex;
        public Vec2 MoveGoal;
        public bool Moving;
        public bool PathPending;
        public float StuckTimer;
        public int Repaths;
        public float LastProgressDist = float.MaxValue;
        public float RepathTimer;
        public Vec2 FormationOffset;
        public float StillTime;       // seconds since last movement
        public float SuppressedTimer;
        public float Speed;           // current speed
        // Harvester
        public HarvestState HState;
        public float Cargo;
        public float CargoValue;
        public Vec2 LastHarvestPos;
        public bool HasLastHarvest;
        public int DockId;
        public float UnloadTimer;
        // Air
        public AirState AState;
        public int Ammo;
        public int PadBuildingId;
        public int PadIndex = -1;
        public float RearmTimer;
        public float OrbitAngle;
        // Transport
        public List<int> Passengers = new List<int>();
        public int CarrierId;
        // Submarine
        public bool Submerged = true;
        public float SurfaceTimer;
        public float ChaseTimer;
        public float AcquireTimer;
        public bool Selected;          // not used by sim logic; for save convenience only
        public bool Paradropped;
        public int GroupId;

        public override bool IsBuilding => false;
        public override float Radius => Def.Radius;
        public override ArmorType Armor => Def.Armor;
        public override TargetLayer Layer
        {
            get
            {
                if (Def.Locomotor == Locomotor.Submarine) return Submerged ? TargetLayer.Submerged : TargetLayer.Naval;
                if ((Def.Locomotor == Locomotor.Air || Def.Locomotor == Locomotor.Helicopter) && AState == AirState.Docked) return TargetLayer.Ground;
                return Def.Layer;
            }
        }
        public override int Cost => Def.Cost;
        public override float SightRange => Def.Sight;
        public override string Name => Def.Name;
        public bool IsAir => Def.Locomotor == Locomotor.Air || Def.Locomotor == Locomotor.Helicopter;
        public bool IsInside => CarrierId != 0;
        public MoveClass MoveClass => Def.Locomotor == Locomotor.Foot ? MoveClass.Foot :
            (Def.Locomotor == Locomotor.Naval || Def.Locomotor == Locomotor.Submarine) ? MoveClass.Naval :
            IsAir ? MoveClass.Air : MoveClass.Vehicle;
        public bool IsArmed => Weapons.Count > 0;
        public bool IsIdle => CurrentOrder.Type == OrderType.None && !Moving;
    }

    [Serializable]
    public class ProductionItem
    {
        public string DefId;
        public bool IsTech;
        public float Progress;    // 0..1
        public float Paid;        // credits paid so far
        public bool OnHold;
        public int Count = 1;     // stacked repeats of the same item
    }

    [Serializable]
    public class Building : Entity
    {
        [NonSerialized] public BuildingDef Def;
        public Int2 Origin;
        public int W, H;
        public float BuildUp = 1f;        // 0..1 construction animation after placement
        public bool Repairing;
        public bool Selling;
        public float SellTimer;
        public Vec2 Rally;
        public bool HasRally;
        public bool Powered = true;
        public int[] Pads = new int[0];   // aircraft ids per pad
        public float ProduceFlash;        // presentation hint: time since produced
        public float AcquireTimer;
        public int OriginalOwner;
        public float IncomeTimer;
        public int UnloadingHarvester;
        public bool GateOpen;
        public float GateTimer;

        public override bool IsBuilding => true;
        public override float Radius => Math.Max(W, H) * 0.5f;
        public override ArmorType Armor => Def.Armor;
        public override TargetLayer Layer => TargetLayer.Structure;
        public override int Cost => Def.Cost;
        public override float SightRange => Def.Sight;
        public override string Name => Def.Name;
        public Vec2 Center => new Vec2(Origin.X + W * 0.5f, Origin.Y + H * 0.5f);
        public bool Operational => BuildUp >= 1f && !Disabled && !Selling;
        public bool IsDefense => Def.Role == BuildingRole.Defense;
        public bool IsWallLike => Def.Role == BuildingRole.Wall || Def.Role == BuildingRole.Gate;

        /// <summary>Cell just outside the building where produced units appear / harvesters dock.</summary>
        public Int2 ExitCell => new Int2(Origin.X + W / 2, Origin.Y - 1);

        public bool Covers(int x, int y) => x >= Origin.X && y >= Origin.Y && x < Origin.X + W && y < Origin.Y + H;

        public float DistanceTo(Vec2 p)
        {
            float dx = Math.Max(Math.Max(Origin.X - p.X, 0), p.X - (Origin.X + W));
            float dy = Math.Max(Math.Max(Origin.Y - p.Y, 0), p.Y - (Origin.Y + H));
            return SMath.Sqrt(dx * dx + dy * dy);
        }
    }

    [Serializable]
    public class Projectile
    {
        public ProjectileKind Kind;
        public Vec2 Pos;
        public Vec2 Start;
        public Vec2 Target;
        public int TargetId;
        public float Speed;
        public float Travelled;
        public float TotalDist;
        public string WeaponId;
        public int Owner;
        public int Team;
        public int SourceId;
        public float DamageMult = 1f;
        public float Height;          // visual altitude for ballistic arcs
        public float StartHeight;
        public float TargetHeight;
        public bool Done;
        public int Id;
        [NonSerialized] public WeaponDef Weapon;
    }

    /// <summary>Burning ground left by flame weapons and thermobaric strikes.</summary>
    [Serializable]
    public class FireZone
    {
        public Vec2 Pos;
        public float Radius;
        public float TimeLeft;
        public float Dps;
        public int Owner;
    }
}
