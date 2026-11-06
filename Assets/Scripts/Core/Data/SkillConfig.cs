using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// 反击技能配置。
    /// 注意：SkillCastTarget / SkillEffect 两个枚举定义在 Logic 层（Enums.cs），
    /// 因为技能执行器在 Logic 侧，且 Logic 不能反向依赖 Core。
    ///
    /// 创建：Assets/Data/Skills/ 右键 Create/DefenseCounter/Skill
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_New", menuName = "DefenseCounter/Skill", order = 30)]
    public sealed class SkillConfig : ScriptableObject
    {
        [Header("身份")]
        public string id = "skill_new";

        public string displayName = "未命名反击";

        [TextArea(2, 4)]
        public string description;

        [Header("消耗与冷却")]
        [Tooltip("能量消耗")]
        public int energyCost = 80;

        [Tooltip("冷却时间（秒）")]
        public float cooldown = 45f;

        [Tooltip("是否需要指定反击建筑才能使用，空 = 无条件")]
        public string requiredBuildingId;

        [Header("施放")]
        public SkillCastTarget castTarget = SkillCastTarget.GroundPoint;

        [Tooltip("作用半径（米）")]
        public float radius = 8f;

        [Tooltip("生效延迟（秒）。0 = 立即")]
        public float delay;

        [Header("效果")]
        public SkillEffect effect = SkillEffect.Damage;

        public DamageType damageType = DamageType.Explosive;

        public float damage = 400f;

        [Tooltip("持续时间（秒）。EMP/减速/黑客使用")]
        public float duration;

        [Header("召唤类专用")]
        [Tooltip("召唤的 UnitConfig.id")]
        public string summonUnitId;

        [Tooltip("召唤数量")]
        public int summonCount;

        [Header("表现")]
        public GameObject vfxPrefab;

        public Sprite icon;
    }
}