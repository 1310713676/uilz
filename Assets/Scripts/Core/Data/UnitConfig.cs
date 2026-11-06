using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// 兵种配置。兵种无等级，靠数量、阵容、克制、技能差异。
    /// 创建：Assets/Data/Units/ 右键 Create/DefenseCounter/Unit
    /// </summary>
    [CreateAssetMenu(fileName = "Unit_New", menuName = "DefenseCounter/Unit", order = 20)]
    public sealed class UnitConfig : ScriptableObject
    {
        [Header("身份")]
        public string id = "unit_new";

        public string displayName = "未命名兵种";

        [TextArea(2, 4)]
        public string description;

        public UnitRole role = UnitRole.Assault;

        public UnitTag tags = UnitTag.None;

        [Header("出兵经济")]
        [Tooltip("单次出兵消耗金币")]
        public int deployCost = 40;

        [Tooltip("占用的出兵槽位数")]
        public int supplyCost = 1;

        [Tooltip("同屏该兵种最大数量，0 = 不限")]
        public int maxAlive = 0;

        [Header("生存与移动")]
        public float maxHealth = 220f;

        [Tooltip("米/秒")]
        public float moveSpeed = 3.5f;

        [Tooltip("度/秒")]
        public float turnSpeed = 540f;

        public ArmorType armorType = ArmorType.Light;

        [Tooltip("单位半径（米），用于碰撞与聚集")]
        public float bodyRadius = 0.35f;

        [Header("攻击")]
        public DamageType damageType = DamageType.Kinetic;

        public float damage = 18f;

        [Tooltip("秒")]
        public float attackInterval = 0.8f;

        [Tooltip("攻击距离（米）")]
        public float range = 6f;

        [Tooltip("溅射半径（米），0 = 单体")]
        public float splashRadius;

        [Tooltip("索敌距离（米）。通常 >= range，允许追击")]
        public float acquireRadius = 10f;

        [Tooltip("目标优先级：0 = 最近，1 = 血量最低，2 = 建筑优先")]
        [Range(0, 2)]
        public int targetPriority;

        [Header("结构伤害修正")]
        [Tooltip("对建筑（ArmorType.Structure）的额外倍率，1 = 无加成")]
        public float structureDamageMultiplier = 1f;

        [Header("外观")]
        [Tooltip("兵种预制体，含 Animator")]
        public GameObject prefab;

        // ---------- 查询 ----------

        public float Dps => damage / Mathf.Max(0.05f, attackInterval);

        /// <summary>对目标护甲的最终伤害倍率。克制矩阵的唯一真源，代码内不可另写一套。</summary>
        public float DamageMultiplierAgainst(ArmorType target)
        {
            float type = DamageMatrix.Multiplier(damageType, target);
            float structure = target == ArmorType.Structure ? structureDamageMultiplier : 1f;
            return type * structure;
        }
    }
}