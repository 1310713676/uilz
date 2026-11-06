using System;
using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// 战斗行为块。数组按等级索引：index = level - 1，长度必须等于 BuildingConfig.maxLevel。
    /// 同一建筑只启用与 buildingType 匹配的块，其余 enabled = false。
    /// </summary>
    [Serializable]
    public struct CombatBlock
    {
        public bool enabled;

        public DamageType damageType;

        [Tooltip("每级单发伤害")]
        public float[] damage;

        [Tooltip("每级攻击间隔（秒）")]
        public float[] interval;

        [Tooltip("每级射程（米）")]
        public float[] range;

        [Tooltip("每级溅射半径（米），0 = 单体")]
        public float[] splashRadius;

        [Tooltip("是否可攻击空中单位（第一阶段无空中敌人，预留）")]
        public bool canTargetFlying;

        [Tooltip("目标优先级：0 = 最近，1 = 血量最高，2 = 建筑优先")]
        [Range(0, 2)]
        public int targetPriority;

        public float DamageAt(int level)
        {
            return Sample(damage, level);
        }

        public float IntervalAt(int level)
        {
            return Mathf.Max(0.05f, Sample(interval, level));
        }

        public float RangeAt(int level)
        {
            return Sample(range, level);
        }

        public float SplashAt(int level)
        {
            return Sample(splashRadius, level);
        }

        /// <summary>DPS，供 AI 与平衡表使用。</summary>
        public float DpsAt(int level)
        {
            return DamageAt(level) / IntervalAt(level);
        }

        private static float Sample(float[] arr, int level)
        {
            if (arr == null || arr.Length == 0)
            {
                return 0f;
            }

            int i = Mathf.Clamp(level - 1, 0, arr.Length - 1);
            return arr[i];
        }
    }

    /// <summary>支援行为块（护盾、修复、干扰）。</summary>
    [Serializable]
    public struct SupportBlock
    {
        public bool enabled;

        [Tooltip("影响半径（米）")]
        public float[] auraRadius;

        [Tooltip("每级提供的护盾值，0 = 不提供护盾")]
        public float[] shieldAmount;

        [Tooltip("每级每秒修复量，0 = 不修复")]
        public float[] repairPerSecond;

        [Tooltip("效果冷却（秒）")]
        public float[] cooldown;

        public float RadiusAt(int level)
        {
            return Sample(auraRadius, level);
        }

        public float ShieldAt(int level)
        {
            return Sample(shieldAmount, level);
        }

        public float RepairAt(int level)
        {
            return Sample(repairPerSecond, level);
        }

        public float CooldownAt(int level)
        {
            return Sample(cooldown, level);
        }

        private static float Sample(float[] arr, int level)
        {
            if (arr == null || arr.Length == 0)
            {
                return 0f;
            }

            int i = Mathf.Clamp(level - 1, 0, arr.Length - 1);
            return arr[i];
        }
    }

    /// <summary>出兵行为块（解锁兵种 + 提供出兵槽位）。</summary>
    [Serializable]
    public struct ProductionBlock
    {
        public bool enabled;

        [Tooltip("该建筑解锁的 UnitConfig.id 列表")]
        public string[] unlockedUnitIds;

        [Tooltip("每级额外提供的出兵槽位")]
        public int[] extraUnitSlots;

        public int ExtraSlotsAt(int level)
        {
            if (extraUnitSlots == null || extraUnitSlots.Length == 0)
            {
                return 0;
            }

            int i = Mathf.Clamp(level - 1, 0, extraUnitSlots.Length - 1);
            return extraUnitSlots[i];
        }
    }
}