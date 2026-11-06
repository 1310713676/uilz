using System;
using System.Collections.Generic;
using DefenseCounter.Data;
using DefenseCounter.Logic;
using DefenseCounter.Logic.AI;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.Skill;
using UnityEngine;

namespace DefenseCounter.Core
{
    /// <summary>转换结果：Logic 层需要的两个目录 + id 清单。</summary>
    public sealed class GameCatalogs
    {
        public readonly DictionaryBuildingCatalog Buildings = new DictionaryBuildingCatalog();
        public readonly DictionaryUnitCatalog Units = new DictionaryUnitCatalog();
        public readonly DictionarySkillCatalog Skills = new DictionarySkillCatalog();
        public readonly List<string> BuildingIds = new List<string>();
        public readonly List<string> UnitIds = new List<string>();
        public readonly List<string> SkillIds = new List<string>();

        public int BuildingCount => Buildings.Count;

        public int UnitCount => Units.Count;

        public int SkillCount => Skills.Count;
    }

    /// <summary>
    /// ScriptableObject 配置 -> Logic 层纯数据。
    ///
    /// 这一层是必需的边界：Logic 不允许出现 UnityEngine 类型，
    /// 而配置资产全是 ScriptableObject。转换必须显式、无副作用、可审查。
    ///
    /// 三条规则：
    /// 1. 数组缺失时按 BuildingConfig.Sample 的规则取最后一个有效值，不抛异常；
    /// 2. 未填写的数值一律给"安全默认值"（如 AllowedTerrain 缺省为 Ground），
    ///    让配置疏漏表现为"能跑但可能不平衡"，而不是运行时崩；
    /// 3. 任何 Debug.LogError 只用于指出配置问题，不参与控制流。
    /// </summary>
    public static class ConfigBridge
    {
        public static BuildingSpec ToSpec(BuildingConfig c)
        {
            if (c == null)
            {
                throw new ArgumentNullException(nameof(c));
            }

            return new BuildingSpec
            {
                Id = c.id,
                MaxLevel = Mathf.Clamp(c.maxLevel, 1, 3),
                FootprintX = Mathf.Max(1, c.footprint.x),
                FootprintZ = Mathf.Max(1, c.footprint.y),
                BuildCost = c.buildCost,
                UpgradeCostToL2 = c.upgradeCostToL2,
                UpgradeCostToL3 = c.upgradeCostToL3,
                BuildTimeL1 = BuildingConfig.Sample(c.buildTimeByLevel, 1),
                BuildTimeL2 = BuildingConfig.Sample(c.buildTimeByLevel, 2),
                BuildTimeL3 = BuildingConfig.Sample(c.buildTimeByLevel, 3),
                HealthL1 = BuildingConfig.Sample(c.maxHealthByLevel, 1),
                HealthL2 = BuildingConfig.Sample(c.maxHealthByLevel, 2),
                HealthL3 = BuildingConfig.Sample(c.maxHealthByLevel, 3),
                SellRefundRate = c.sellRefundRate,
                BuildRadiusContribution = c.buildRadiusContribution,
                AllowedTerrain = c.allowedTerrain == TerrainMask.None
                    ? TerrainMask.Ground
                    : c.allowedTerrain,
                CombatEnabled = c.combat.enabled,
                DamageType = c.combat.damageType,
                DamageL1 = c.combat.DamageAt(1),
                DamageL2 = c.combat.DamageAt(2),
                DamageL3 = c.combat.DamageAt(3),
                AttackIntervalL1 = c.combat.IntervalAt(1),
                AttackIntervalL2 = c.combat.IntervalAt(2),
                AttackIntervalL3 = c.combat.IntervalAt(3),
                AttackRangeL1 = c.combat.RangeAt(1),
                AttackRangeL2 = c.combat.RangeAt(2),
                AttackRangeL3 = c.combat.RangeAt(3),
                SplashL1 = c.combat.SplashAt(1),
                SplashL2 = c.combat.SplashAt(2),
                SplashL3 = c.combat.SplashAt(3),
                TargetPriority = c.combat.targetPriority,
                SupportEnabled = c.support.enabled,
                SupportRadiusL1 = c.support.RadiusAt(1),
                SupportRadiusL2 = c.support.RadiusAt(2),
                SupportRadiusL3 = c.support.RadiusAt(3),
                ShieldL1 = c.support.ShieldAt(1),
                ShieldL2 = c.support.ShieldAt(2),
                ShieldL3 = c.support.ShieldAt(3),
                RepairL1 = c.support.RepairAt(1),
                RepairL2 = c.support.RepairAt(2),
                RepairL3 = c.support.RepairAt(3),
                SupportCooldownL1 = c.support.CooldownAt(1),
                SupportCooldownL2 = c.support.CooldownAt(2),
                SupportCooldownL3 = c.support.CooldownAt(3)
            };
        }

        public static UnitSpec ToSpec(UnitConfig u)
        {
            if (u == null)
            {
                throw new ArgumentNullException(nameof(u));
            }

            return new UnitSpec
            {
                Id = u.id,
                DeployCost = u.deployCost,
                SupplyCost = Mathf.Max(1, u.supplyCost),
                MaxHealth = u.maxHealth,
                MoveSpeed = u.moveSpeed,
                BodyRadius = u.bodyRadius,
                Armor = u.armorType,
                DamageType = u.damageType,
                Damage = u.damage,
                AttackInterval = u.attackInterval,
                Range = u.range,
                SplashRadius = u.splashRadius,
                AcquireRadius = Mathf.Max(u.acquireRadius, u.range),
                StructureDamageMultiplier = u.structureDamageMultiplier,
                TargetPriority = u.targetPriority,
                MaxAlive = u.maxAlive
            };
        }

        public static AIDifficultySpec ToSpec(AIDifficultyConfig d)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }

            return new AIDifficultySpec
            {
                Id = d.id,
                IncomeMultiplier = d.incomeMultiplier,
                StartGoldMultiplier = d.startGoldMultiplier,
                DecisionInterval = Mathf.Max(0.1f, d.decisionInterval),
                ReactionDelay = Mathf.Max(0f, d.reactionDelay),
                MistakeChance = d.mistakeChance,
                BuildBuildingWeight = d.buildBuildingWeight,
                UpgradeBuildingWeight = d.upgradeBuildingWeight,
                DeployUnitWeight = d.deployUnitWeight,
                UseSkillWeight = d.useSkillWeight,
                Aggression = d.aggression,
                MaxConcurrentUnits = Mathf.Max(1, d.maxConcurrentUnits),
                CounterAttackThreshold = d.counterAttackThreshold,
                BuildPosJitter = Mathf.Max(0f, d.buildPosJitter),
                SkillUseChance = d.skillUseChance
            };
        }

        public static SkillSpec ToSpec(SkillConfig s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            return new SkillSpec
            {
                Id = s.id,
                EnergyCost = Mathf.Max(0, s.energyCost),
                Cooldown = Mathf.Max(0f, s.cooldown),
                RequiredBuildingId = s.requiredBuildingId,
                CastTarget = s.castTarget,
                Radius = Mathf.Max(0f, s.radius),
                Delay = Mathf.Max(0f, s.delay),
                Effect = s.effect,
                DamageType = s.damageType,
                Damage = Mathf.Max(0f, s.damage),
                Duration = Mathf.Max(0f, s.duration),
                SummonUnitId = s.summonUnitId,
                SummonCount = Mathf.Max(0, s.summonCount)
            };
        }

        /// <summary>把数据库里全部配置灌进两个目录。</summary>
        public static GameCatalogs BuildCatalogs(GameDatabase db)
        {
            var result = new GameCatalogs();

            if (db == null)
            {
                return result;
            }

            for (int i = 0; i < db.buildings.Count; i++)
            {
                BuildingConfig b = db.buildings[i];
                if (b == null || string.IsNullOrEmpty(b.id))
                {
                    continue;
                }

                result.Buildings.Add(ToSpec(b));
                result.BuildingIds.Add(b.id);
            }

            for (int i = 0; i < db.units.Count; i++)
            {
                UnitConfig u = db.units[i];
                if (u == null || string.IsNullOrEmpty(u.id))
                {
                    continue;
                }

                result.Units.Add(ToSpec(u));
                result.UnitIds.Add(u.id);
            }

            for (int i = 0; i < db.skills.Count; i++)
            {
                SkillConfig s = db.skills[i];
                if (s == null || string.IsNullOrEmpty(s.id))
                {
                    continue;
                }

                result.Skills.Add(ToSpec(s));
                result.SkillIds.Add(s.id);
            }

            return result;
        }

        /// <summary>
        /// 按阵容分类 AI 可用建筑：出兵建筑单独列表，其余算防御/支援。
        /// AI 需要知道"哪个建筑能出兵"，否则无法触发「先建出兵建筑」这条硬规则。
        /// </summary>
        public static AIArsenal BuildArsenal(GameDatabase db, LoadoutConfig loadout)
        {
            var defense = new List<string>();
            var support = new List<string>();
            var production = new List<string>();

            if (db != null && loadout != null && loadout.buildingIds != null)
            {
                for (int i = 0; i < loadout.buildingIds.Length; i++)
                {
                    string id = loadout.buildingIds[i];
                    BuildingConfig b = db.GetBuilding(id);
                    if (b == null)
                    {
                        continue;
                    }

                    if (b.buildingType == BuildingType.Production)
                    {
                        production.Add(id);
                    }
                    else if (b.buildingType == BuildingType.Support)
                    {
                        support.Add(id);
                    }
                    else
                    {
                        defense.Add(id);
                    }
                }
            }

            string[] units = loadout != null && loadout.unitIds != null
                ? loadout.unitIds
                : new string[0];

            string[] skillIds = loadout != null && loadout.skillIds != null
                ? loadout.skillIds
                : new string[0];

            // 能量门槛在这里算好，AI 不需要依赖技能目录
            int minSkillEnergy = int.MaxValue;
            if (db != null)
            {
                for (int i = 0; i < skillIds.Length; i++)
                {
                    SkillConfig s = db.GetSkill(skillIds[i]);
                    if (s != null && s.energyCost < minSkillEnergy)
                    {
                        minSkillEnergy = s.energyCost;
                    }
                }
            }

            return new AIArsenal
            {
                DefenseBuildingIds = defense.ToArray(),
                SupportBuildingIds = support.ToArray(),
                ProductionBuildingIds = production.ToArray(),
                UnitIds = units,
                SkillIds = skillIds,
                MinSkillEnergyCost = minSkillEnergy
            };
        }
    }
}