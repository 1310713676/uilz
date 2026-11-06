using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// 阵容配置。玩家与 AI 共用，限制可建造建筑、可出兵种、可用技能。
    /// 创建：Assets/Data/Loadouts/ 右键 Create/DefenseCounter/Loadout
    /// </summary>
    [CreateAssetMenu(fileName = "Loadout_New", menuName = "DefenseCounter/Loadout", order = 40)]
    public sealed class LoadoutConfig : ScriptableObject
    {
        [Header("身份")]
        public string id = "loadout_new";

        public string displayName = "未命名阵容";

        [TextArea(2, 4)]
        public string description;

        [Header("可选内容（存 id，不存引用，便于联机同步）")]
        public string[] buildingIds;

        public string[] unitIds;

        public string[] skillIds;

        [Header("容量")]
        [Tooltip("同时在场兵力的槽位上限")]
        public int maxUnitSlots = 20;

        [Header("经济修正")]
        [Tooltip("初始金币倍率，1 = 标准")]
        public float startGoldMultiplier = 1f;

        [Tooltip("金币收入倍率，1 = 标准")]
        public float incomeMultiplier = 1f;

        [Header("AI 偏好权重（仅 AI 使用）")]
        [Tooltip("进攻权重 0~1，越高越倾向出兵压线")]
        [Range(0f, 1f)]
        public float aggressionBias = 0.5f;

        public bool AllowsBuilding(string buildingId)
        {
            return Contains(buildingIds, buildingId);
        }

        public bool AllowsUnit(string unitId)
        {
            return Contains(unitIds, unitId);
        }

        public bool AllowsSkill(string skillId)
        {
            return Contains(skillIds, skillId);
        }

        private static bool Contains(string[] arr, string value)
        {
            if (arr == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}