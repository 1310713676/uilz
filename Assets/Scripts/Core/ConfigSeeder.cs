using DefenseCounter.Data;
using DefenseCounter.Logic;
using DefenseCounter.Logic.AI;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.Reference;
using DefenseCounter.Logic.Skill;
using UnityEngine;

namespace DefenseCounter.Core
{
    /// <summary>
    /// Spec → Config 的反向映射。
    ///
    /// 与 ConfigBridge（Config→Spec）成对。用途：在没有编辑器的场景下，
    /// 用 ReferenceDatabase 的数据把 20 个 ScriptableObject 资产「生成」出来。
    ///
    /// 本类**不引用 UnityEditor**，只做字段赋值 ——
    /// 所以能用 UnityEngine 参考程序集在本机编译验证。
    /// 真正的资产创建（AssetDatabase）在 ConfigSeederEditor，无法本机验证，但那一层很薄。
    /// </summary>
    public static class ConfigSeeder
    {
        // ---------- 兵种 ----------

        public static void Apply(UnitConfig cfg, UnitSpec s)
        {
            cfg.id = s.Id;
            cfg.displayName = DisplayNameOf(s.Id);
            cfg.role = ResolveRole(s.Id);
            cfg.tags = ResolveUnitTag(s.Id);
            cfg.deployCost = s.DeployCost;
            cfg.supplyCost = s.SupplyCost;
            cfg.maxAlive = s.MaxAlive;
            cfg.maxHealth = s.MaxHealth;
            cfg.moveSpeed = s.MoveSpeed;
            cfg.turnSpeed = 540f;
            cfg.armorType = s.Armor;
            cfg.bodyRadius = s.BodyRadius;
            cfg.damageType = s.DamageType;
            cfg.damage = s.Damage;
            cfg.attackInterval = s.AttackInterval;
            cfg.range = s.Range;
            cfg.splashRadius = s.SplashRadius;
            cfg.acquireRadius = s.AcquireRadius;
            cfg.targetPriority = s.TargetPriority;
            cfg.structureDamageMultiplier = s.StructureDamageMultiplier;
        }

        // ---------- 建筑 ----------

        public static void Apply(BuildingConfig cfg, BuildingSpec s)
        {
            cfg.id = s.Id;
            cfg.displayName = DisplayNameOf(s.Id);
            cfg.buildingType = TypeOf(s.Id);
            cfg.tags = ResolveBuildingTag(s.Id);
            cfg.footprint = new Vector2Int(s.FootprintX, s.FootprintZ);
            cfg.allowedTerrain = s.AllowedTerrain;
            cfg.buildRadiusContribution = s.BuildRadiusContribution;
            cfg.maxLevel = s.MaxLevel;
            cfg.buildCost = s.BuildCost;
            cfg.upgradeCostToL2 = s.UpgradeCostToL2;
            cfg.upgradeCostToL3 = s.UpgradeCostToL3;
            cfg.sellRefundRate = s.SellRefundRate;

            cfg.maxHealthByLevel = ToLevels(s.HealthL1, s.HealthL2, s.HealthL3);
            cfg.buildTimeByLevel = ToLevels(s.BuildTimeL1, s.BuildTimeL2, s.BuildTimeL3);

            cfg.combat = new CombatBlock
            {
                enabled = s.CombatEnabled,
                damageType = s.DamageType,
                damage = ToLevels(s.DamageL1, s.DamageL2, s.DamageL3),
                interval = ToLevels(s.AttackIntervalL1, s.AttackIntervalL2, s.AttackIntervalL3),
                range = ToLevels(s.AttackRangeL1, s.AttackRangeL2, s.AttackRangeL3),
                splashRadius = ToLevels(s.SplashL1, s.SplashL2, s.SplashL3),
                canTargetFlying = false,
                targetPriority = s.TargetPriority
            };

            cfg.support = new SupportBlock
            {
                enabled = s.SupportEnabled,
                auraRadius = ToLevels(s.SupportRadiusL1, s.SupportRadiusL2, s.SupportRadiusL3),
                shieldAmount = ToLevels(s.ShieldL1, s.ShieldL2, s.ShieldL3),
                repairPerSecond = ToLevels(s.RepairL1, s.RepairL2, s.RepairL3),
                cooldown = ToLevels(s.SupportCooldownL1, s.SupportCooldownL2, s.SupportCooldownL3)
            };

            bool isHive = s.Id == "bld_drone_hive";
            cfg.production = new ProductionBlock
            {
                enabled = isHive,
                unlockedUnitIds = isHive ? new[] { "unit_drone" } : new string[0],
                extraUnitSlots = isHive ? new[] { 2, 4, 6 } : new[] { 0, 0, 0 }
            };

            cfg.parts = new ModuleParts
            {
                baseId = "MD_Base_Tripod_01",
                bodyId = "MD_Body_Cylinder_02",
                barrelId = s.CombatEnabled ? "MD_Barrel_Rail_03" : "none",
                coreId = "MD_Core_Cyan",
                attachIds = new string[0]
            };
        }

        // ---------- 技能 ----------

        public static void Apply(SkillConfig cfg, SkillSpec s)
        {
            cfg.id = s.Id;
            cfg.displayName = DisplayNameOf(s.Id);
            cfg.energyCost = s.EnergyCost;
            cfg.cooldown = s.Cooldown;
            cfg.requiredBuildingId = s.RequiredBuildingId;
            cfg.castTarget = s.CastTarget;
            cfg.radius = s.Radius;
            cfg.delay = s.Delay;
            cfg.effect = s.Effect;
            cfg.damageType = s.DamageType;
            cfg.damage = s.Damage;
            cfg.duration = s.Duration;
            cfg.summonUnitId = s.SummonUnitId;
            cfg.summonCount = s.SummonCount;
        }

        // ---------- 阵容 ----------

        public static void Apply(LoadoutConfig cfg, ReferenceLoadout l)
        {
            cfg.id = l.Id;
            cfg.displayName = l.DisplayName;
            cfg.buildingIds = l.BuildingIds;
            cfg.unitIds = l.UnitIds;

            // 技能按阵容 id 分配（数值表里 iron 用 EMP，blitz 用轨道打击）
            cfg.skillIds = l.Id == "loadout_blitz"
                ? new[] { "skill_orbital_strike" }
                : new[] { "skill_emp_storm" };

            cfg.maxUnitSlots = l.MaxUnitSlots;
            cfg.startGoldMultiplier = 1f;
            cfg.incomeMultiplier = 1f;
            cfg.aggressionBias = l.Id == "loadout_blitz" ? 0.75f : 0.35f;
        }

        // ---------- 关卡 ----------

        public static void Apply(LevelConfig cfg, ReferenceLevel level)
        {
            cfg.id = level.Id;
            cfg.displayName = "第一关";
            cfg.sceneName = "Battle";
            cfg.mapSize = new Vector2(level.MapSize, level.MapSize);
            cfg.buildableAreaCenter = Vector2.zero;
            cfg.buildableAreaSize = new Vector2(level.BuildableSize, level.BuildableSize);
            cfg.allowedTerrain = TerrainMask.Ground;

            cfg.playerBasePositions = new[] { CellToWorld(level, level.PlayerBaseCell) };
            cfg.aiBasePositions = new[] { CellToWorld(level, level.AiBaseCell) };
            cfg.playerBuildRadius = level.PlayerBuildRadius;
            cfg.aiBuildRadius = level.AiBuildRadius;
            cfg.baseMaxHealth = level.BaseMaxHealth;
            cfg.startGold = level.StartGold;
            cfg.startEnergy = 0;
            cfg.goldPerSecond = level.GoldPerSecond;
            cfg.energyPerSecond = level.EnergyPerSecond;
            cfg.maxMatchDuration = level.MaxDuration;
        }

        public static void Apply(AIDifficultyConfig cfg, AIDifficultySpec s)
        {
            cfg.id = s.Id;
            cfg.displayName = DisplayNameOf(s.Id);
            cfg.incomeMultiplier = s.IncomeMultiplier;
            cfg.startGoldMultiplier = s.StartGoldMultiplier;
            cfg.decisionInterval = s.DecisionInterval;
            cfg.reactionDelay = s.ReactionDelay;
            cfg.mistakeChance = s.MistakeChance;
            cfg.buildBuildingWeight = s.BuildBuildingWeight;
            cfg.upgradeBuildingWeight = s.UpgradeBuildingWeight;
            cfg.deployUnitWeight = s.DeployUnitWeight;
            cfg.useSkillWeight = s.UseSkillWeight;
            cfg.aggression = s.Aggression;
            cfg.maxConcurrentUnits = s.MaxConcurrentUnits;
            cfg.counterAttackThreshold = s.CounterAttackThreshold;
            cfg.buildPosJitter = s.BuildPosJitter;
            cfg.skillUseChance = s.SkillUseChance;
        }

        // ---------- 辅助 ----------

        public static Vector3 CellToWorld(ReferenceLevel level, GridPos cell)
        {
            float origin = -level.MapSize * 0.5f;
            return new Vector3(
                origin + (cell.X + 0.5f) * level.CellSize,
                0f,
                origin + (cell.Z + 0.5f) * level.CellSize);
        }

        private static float[] ToLevels(float a, float b, float c)
        {
            return new[] { a, b, c };
        }

        public static string DisplayNameOf(string id)
        {
            switch (id)
            {
                case "bld_rail_sniper": return "磁轨狙击塔";
                case "bld_laser_sentry": return "激光哨戒塔";
                case "bld_plasma_mortar": return "等离子迫击炮";
                case "bld_phase_shield": return "相位护盾发生器";
                case "bld_nano_repair": return "纳米修复塔";
                case "bld_drone_hive": return "无人机蜂巢";
                case "unit_trooper": return "突击步兵";
                case "unit_breacher": return "重装破甲兵";
                case "unit_drone": return "悬浮无人机";
                case "unit_mech": return "攻城机甲";
                case "skill_orbital_strike": return "轨道动能打击";
                case "skill_emp_storm": return "EMP 风暴";
                case "ai_easy": return "简单";
                case "ai_normal": return "普通";
                case "ai_hard": return "困难";
                case "ai_nightmare": return "噩梦";
                default: return id;
            }
        }

        public static BuildingType TypeOf(string id)
        {
            switch (id)
            {
                case "bld_drone_hive": return BuildingType.Production;
                case "bld_phase_shield":
                case "bld_nano_repair": return BuildingType.Support;
                default: return BuildingType.Defense;
            }
        }

        private static UnitRole ResolveRole(string id)
        {
            switch (id)
            {
                case "unit_breacher": return UnitRole.Breacher;
                case "unit_drone": return UnitRole.Skirmisher;
                case "unit_mech": return UnitRole.Siege;
                default: return UnitRole.Assault;
            }
        }

        private static UnitTag ResolveUnitTag(string id)
        {
            switch (id)
            {
                case "unit_breacher": return UnitTag.AntiStructure | UnitTag.Tanky;
                case "unit_drone": return UnitTag.HighMobility | UnitTag.Swarm;
                case "unit_mech": return UnitTag.AntiStructure | UnitTag.Tanky | UnitTag.Ranged;
                default: return UnitTag.None;
            }
        }

        private static BuildingTag ResolveBuildingTag(string id)
        {
            switch (id)
            {
                case "bld_rail_sniper": return BuildingTag.SingleTarget | BuildingTag.AntiArmor;
                case "bld_laser_sentry": return BuildingTag.SingleTarget | BuildingTag.AntiSwarm;
                case "bld_plasma_mortar": return BuildingTag.AreaDamage;
                case "bld_phase_shield": return BuildingTag.Shield;
                case "bld_nano_repair": return BuildingTag.Repair;
                case "bld_drone_hive": return BuildingTag.UnlockUnit;
                default: return BuildingTag.None;
            }
        }
    }
}