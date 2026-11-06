using System.Collections.Generic;
using DefenseCounter.Data;

namespace DefenseCounter.Logic.Skill
{
    /// <summary>技能的纯数据快照，由 Core 层从 SkillConfig 转换。</summary>
    public struct SkillSpec
    {
        public string Id;
        public int EnergyCost;
        public float Cooldown;
        public string RequiredBuildingId;
        public SkillCastTarget CastTarget;
        public float Radius;
        public float Delay;
        public SkillEffect Effect;
        public DamageType DamageType;
        public float Damage;
        public float Duration;
        public string SummonUnitId;
        public int SummonCount;

        public bool RequiresBuilding => !string.IsNullOrEmpty(RequiredBuildingId);
    }

    public interface ISkillCatalog
    {
        bool TryGet(string id, out SkillSpec spec);
    }

    public sealed class DictionarySkillCatalog : ISkillCatalog
    {
        private readonly Dictionary<string, SkillSpec> _map = new Dictionary<string, SkillSpec>();

        public int Count => _map.Count;

        public void Add(SkillSpec spec)
        {
            _map[spec.Id] = spec;
        }

        public bool TryGet(string id, out SkillSpec spec)
        {
            if (string.IsNullOrEmpty(id))
            {
                spec = default;
                return false;
            }

            return _map.TryGetValue(id, out spec);
        }
    }

    /// <summary>待生效的技能（有 Delay 的先进这里）。</summary>
    public struct PendingSkill
    {
        public string SkillId;
        public byte CasterId;
        public float X;
        public float Z;
        public float ResolveTime;
    }
}