using System.Collections.Generic;
using DefenseCounter.Data;

namespace DefenseCounter.Logic.Combat
{
    /// <summary>兵种的纯数据快照，由 Core 层从 UnitConfig 转换而来。</summary>
    public struct UnitSpec
    {
        public string Id;
        public int DeployCost;
        public int SupplyCost;
        public float MaxHealth;
        public float MoveSpeed;
        public float BodyRadius;
        public ArmorType Armor;
        public DamageType DamageType;
        public float Damage;
        public float AttackInterval;
        public float Range;
        public float SplashRadius;
        public float AcquireRadius;
        public float StructureDamageMultiplier;
        public int TargetPriority;
        public int MaxAlive;
    }

    public interface IUnitCatalog
    {
        bool TryGet(string id, out UnitSpec spec);
    }

    public sealed class DictionaryUnitCatalog : IUnitCatalog
    {
        private readonly Dictionary<string, UnitSpec> _map = new Dictionary<string, UnitSpec>();

        public int Count => _map.Count;

        public void Add(UnitSpec spec)
        {
            _map[spec.Id] = spec;
        }

        public bool TryGet(string id, out UnitSpec spec)
        {
            if (string.IsNullOrEmpty(id))
            {
                spec = default;
                return false;
            }

            return _map.TryGetValue(id, out spec);
        }
    }

    public enum TargetKind
    {
        None = 0,
        Building = 1,
        Unit = 2,
        Base = 3
    }

    /// <summary>
    /// 战斗目标引用。用 (Kind, Id) 而不是对象引用，
    /// 因为实体可能在目标解析之前就被销毁 —— 悬空引用是这类系统最常见的崩溃源。
    /// </summary>
    public struct TargetRef
    {
        public TargetKind Kind;
        public int Id;

        public bool IsNone => Kind == TargetKind.None;

        public static TargetRef None => default;

        public static TargetRef ForBuilding(int instanceId)
        {
            return new TargetRef { Kind = TargetKind.Building, Id = instanceId };
        }

        public static TargetRef ForUnit(int instanceId)
        {
            return new TargetRef { Kind = TargetKind.Unit, Id = instanceId };
        }

        public static TargetRef ForBase(int playerId)
        {
            return new TargetRef { Kind = TargetKind.Base, Id = playerId };
        }

        public override string ToString()
        {
            return Kind + "#" + Id;
        }
    }

    public sealed class UnitInstance
    {
        public int InstanceId;
        public string SpecId;
        public byte OwnerId;
        public float X;
        public float Z;
        public float Health;
        public float AttackCooldown;
        public bool Alive = true;
        public TargetRef Target = TargetRef.None;

        /// <summary>当前寻路结果（世界格）。null 表示走直线。</summary>
        public GridPos[] Path;

        public int PathIndex;

        /// <summary>重算路径的倒计时。建筑增减会改变网格，必须周期性重算。</summary>
        public float RepathTimer;

        /// <summary>瘫痪剩余时间（EMP）。&gt;0 时无法移动与攻击。</summary>
        public float StunTimer;

        /// <summary>减速剩余时间。&gt;0 时移动速度乘以 SlowFactor。</summary>
        public float SlowTimer;

        public float SlowFactor = 1f;

        public bool HasTarget => !Target.IsNone;

        public float DistanceTo(float x, float z)
        {
            float dx = X - x;
            float dz = Z - z;
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }
    }
}