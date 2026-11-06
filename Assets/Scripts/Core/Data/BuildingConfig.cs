using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// 建筑配置。所有数值走配置表，代码不改。
    /// 创建：Assets/Data/Buildings/ 右键 Create/DefenseCounter/Building
    /// </summary>
    [CreateAssetMenu(fileName = "Bld_New", menuName = "DefenseCounter/Building", order = 10)]
    public sealed class BuildingConfig : ScriptableObject
    {
        [Header("身份")]
        [Tooltip("唯一 ID，snake_case，联机同步只传这个")]
        public string id = "bld_new";

        public string displayName = "未命名建筑";

        [TextArea(2, 4)]
        public string description;

        public BuildingType buildingType = BuildingType.Defense;

        public BuildingTag tags = BuildingTag.None;

        [Header("占地与放置")]
        [Tooltip("占地格数（1 格 = 2m）。轻型 1x1，重型 2x2")]
        public Vector2Int footprint = Vector2Int.one;

        [Tooltip("可建造的地形层。通常只用 Ground")]
        public TerrainMask allowedTerrain = TerrainMask.Ground;

        [Tooltip("建筑自身的建造半径贡献（米）。用于扩展可建造区域，0 = 不贡献")]
        public float buildRadiusContribution;

        [Header("等级与经济")]
        [Range(1, 3)]
        public int maxLevel = 3;

        [Tooltip("V1 建造费用")]
        public int buildCost = 100;

        [Tooltip("V1 -> V2 升级费用")]
        public int upgradeCostToL2 = 150;

        [Tooltip("V2 -> V3 升级费用")]
        public int upgradeCostToL3 = 220;

        [Tooltip("拆除返还比例，0.5 = 返还一半总投入")]
        [Range(0f, 1f)]
        public float sellRefundRate = 0.5f;

        [Header("等级数值（长度必须 = maxLevel）")]
        [Tooltip("每级最大血量")]
        public float[] maxHealthByLevel = new float[3];

        [Tooltip("每级建造/升级耗时（秒）")]
        public float[] buildTimeByLevel = new float[3];

        [Header("行为块")]
        public CombatBlock combat;
        public SupportBlock support;
        public ProductionBlock production;

        [Header("外观")]
        [Tooltip("模块化拼装引用，仅编辑期预览用")]
        public ModuleParts parts;

        [Tooltip("每级对应的预制体。运行时直接换 sharedMesh，不拼装")]
        public GameObject[] prefabsByLevel = new GameObject[3];

        // ---------- 查询 ----------

        public float MaxHealthAt(int level)
        {
            return Sample(maxHealthByLevel, level);
        }

        public float BuildTimeAt(int level)
        {
            return Sample(buildTimeByLevel, level);
        }

        public int UpgradeCostFrom(int currentLevel)
        {
            if (currentLevel == 1)
            {
                return upgradeCostToL2;
            }

            if (currentLevel == 2)
            {
                return upgradeCostToL3;
            }

            return 0;
        }

        /// <summary>截至 currentLevel 的总投入，用于计算拆除返还。</summary>
        public int TotalInvestedAt(int currentLevel)
        {
            int total = buildCost;
            if (currentLevel >= 2)
            {
                total += upgradeCostToL2;
            }

            if (currentLevel >= 3)
            {
                total += upgradeCostToL3;
            }

            return total;
        }

        public static float Sample(float[] arr, int level)
        {
            if (arr == null || arr.Length == 0)
            {
                return 0f;
            }

            int i = Mathf.Clamp(level - 1, 0, arr.Length - 1);
            return arr[i];
        }
    }
}