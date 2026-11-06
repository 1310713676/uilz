using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// AI 难度配置。难度不是单纯加数值，而是资源、决策频率、失误率、
    /// 进攻欲望、操作精度的综合。
    /// 创建：Assets/Data/AI/ 右键 Create/DefenseCounter/AI Difficulty
    /// </summary>
    [CreateAssetMenu(fileName = "AIDiff_New", menuName = "DefenseCounter/AI Difficulty", order = 60)]
    public sealed class AIDifficultyConfig : ScriptableObject
    {
        [Header("身份")]
        public string id = "ai_normal";

        public string displayName = "普通";

        [Tooltip("给玩家看的档位描述")]
        [TextArea(2, 3)]
        public string description;

        [Header("经济")]
        [Tooltip("金币收入倍率")]
        [Range(0.5f, 3f)]
        public float incomeMultiplier = 1.2f;

        [Tooltip("初始金币倍率")]
        [Range(0.5f, 3f)]
        public float startGoldMultiplier = 1f;

        [Header("决策节奏")]
        [Tooltip("决策间隔（秒）。越小反应越快")]
        public float decisionInterval = 1.5f;

        [Tooltip("从感知到执行的反应延迟（秒）")]
        public float reactionDelay = 0.8f;

        [Tooltip("随机失误概率，0~1")]
        [Range(0f, 1f)]
        public float mistakeChance = 0.15f;

        [Header("行为权重")]
        [Range(0f, 1f)]
        public float buildBuildingWeight = 0.35f;

        [Range(0f, 1f)]
        public float upgradeBuildingWeight = 0.2f;

        [Range(0f, 1f)]
        public float deployUnitWeight = 0.3f;

        [Range(0f, 1f)]
        public float useSkillWeight = 0.15f;

        [Header("战术倾向")]
        [Tooltip("进攻欲望 0~1，越高越倾向主动出兵压线")]
        [Range(0f, 1f)]
        public float aggression = 0.5f;

        [Tooltip("同时在场的兵力上限")]
        public int maxConcurrentUnits = 12;

        [Tooltip("基地血量低于该比例时转入全力反击")]
        [Range(0f, 1f)]
        public float counterAttackThreshold = 0.6f;

        [Tooltip("能量足够时释放技能的概率")]
        [Range(0f, 1f)]
        public float skillUseChance = 0.6f;

        [Header("操作精度")]
        [Tooltip("建造位置随机偏移（米）。越大越容易放歪")]
        public float buildPosJitter = 2f;

        /// <summary>权重总和，用于加权随机。</summary>
        public float TotalActionWeight =>
            buildBuildingWeight + upgradeBuildingWeight + deployUnitWeight + useSkillWeight;
    }
}