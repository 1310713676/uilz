using DefenseCounter.Data;
using DefenseCounter.Logic.AI;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.Reference
{
    /// <summary>关卡参考数据（对应 LevelConfig 的关键字段）。</summary>
    public struct ReferenceLevel
    {
        public string Id;
        public float MapSize;
        public float BuildableSize;
        public float PlayerBuildRadius;
        public float AiBuildRadius;
        public float BaseMaxHealth;
        public int StartGold;
        public float GoldPerSecond;
        public float EnergyPerSecond;
        public float MaxDuration;
        public GridPos PlayerBaseCell;
        public GridPos AiBaseCell;
        public int CellsPerSide;
        public float CellSize;
    }

    /// <summary>阵容参考数据。</summary>
    public struct ReferenceLoadout
    {
        public string Id;
        public string DisplayName;
        public string[] BuildingIds;
        public string[] UnitIds;
        public int MaxUnitSlots;
    }

    /// <summary>
    /// 《第一阶段数值表 v1.0》的可执行版本。
    ///
    /// 存在的理由：ScriptableObject 无法在本机运行，而数值需要被真实模拟验证。
    /// 这里存的是同一批数字，任何一侧改动都必须同步另一侧 ——
    /// 这是本项目唯一接受的"重复"，因为它买到了「不打开 Unity 也能验证平衡」。
    /// </summary>
    public static class ReferenceDatabase
    {
        // ---------- id 清单（供生成器遍历） ----------

        public static readonly string[] AllBuildingIds =
        {
            "bld_rail_sniper", "bld_laser_sentry", "bld_plasma_mortar",
            "bld_phase_shield", "bld_nano_repair", "bld_drone_hive"
        };

        public static readonly string[] AllUnitIds =
        {
            "unit_trooper", "unit_breacher", "unit_drone", "unit_mech"
        };

        public static readonly string[] AllSkillIds =
        {
            "skill_orbital_strike", "skill_emp_storm"
        };

        public static readonly string[] AllDifficultyIds =
        {
            "ai_easy", "ai_normal", "ai_hard", "ai_nightmare"
        };

        // ---------- 建筑 ----------

        public static DictionaryBuildingCatalog CreateBuildings()
        {
            var c = new DictionaryBuildingCatalog();

            c.Add(Defense("bld_rail_sniper", 120, 150, 220,
                Bt(6f, 8f, 10f), Hp(800f, 1100f, 1500f),
                Dmg(90f, 140f, 200f), Itv(2.2f, 2.0f, 1.8f), Rng(22f, 24f, 26f),
                DamageType.Piercing, Splash(0f, 0f, 0f), 0));

            c.Add(Defense("bld_laser_sentry", 80, 110, 160,
                Bt(4f, 6f, 8f), Hp(500f, 700f, 950f),
                Dmg(12f, 18f, 26f), Itv(0.25f, 0.22f, 0.20f), Rng(14f, 15f, 16f),
                DamageType.Energy, Splash(0f, 0f, 0f), 0));

            c.Add(Defense("bld_plasma_mortar", 180, 230, 320,
                Bt(8f, 10f, 12f), Hp(900f, 1200f, 1600f),
                Dmg(70f, 105f, 150f), Itv(2.5f, 2.3f, 2.1f), Rng(18f, 20f, 22f),
                DamageType.Explosive, Splash(4f, 4.5f, 5f), 0));

            c.Add(Support("bld_phase_shield", 150, 200, 280,
                Bt(6f, 8f, 10f), Hp(600f, 800f, 1050f),
                Aura(10f, 12f, 14f), Shield(400f, 550f, 700f), Repair(0f, 0f, 0f), F3(12f, 10f, 8f)));

            c.Add(Support("bld_nano_repair", 140, 190, 260,
                Bt(6f, 8f, 10f), Hp(550f, 750f, 1000f),
                Aura(9f, 11f, 13f), Shield(0f, 0f, 0f), Repair(25f, 45f, 70f), F3(0f, 0f, 0f)));

            // 无人机蜂巢是出兵建筑，不带支援效果
            c.Add(Base("bld_drone_hive", 200, 260, 340,
                Bt(10f, 12f, 14f), Hp(1000f, 1300f, 1700f)));

            return c;
        }

        // ---------- 兵种 ----------

        public static DictionaryUnitCatalog CreateUnits()
        {
            var c = new DictionaryUnitCatalog();

            c.Add(Unit("unit_trooper", 40, 1, 220f, 3.5f, 18f, 0.8f, 6f, 10f, 0.8f,
                ArmorType.Light, DamageType.Kinetic));

            c.Add(Unit("unit_breacher", 70, 2, 420f, 2.4f, 45f, 1.4f, 3f, 7f, 1.5f,
                ArmorType.Heavy, DamageType.Piercing));

            c.Add(Unit("unit_drone", 55, 1, 120f, 6.0f, 10f, 0.35f, 8f, 12f, 0.6f,
                ArmorType.Light, DamageType.Energy));

            c.Add(Unit("unit_mech", 110, 4, 900f, 1.8f, 120f, 2.5f, 10f, 14f, 1.6f,
                ArmorType.Heavy, DamageType.Explosive));

            return c;
        }

        // ---------- 难度 ----------

        public static AIDifficultySpec[] CreateDifficulties()
        {
            return new[]
            {
                Difficulty("ai_easy", 1.00f, 1.00f, 2.0f, 1.20f, 0.25f,
                    0.35f, 0.15f, 0.35f, 0.15f, 0.30f, 8, 0.50f, 3.0f, 0.40f),
                Difficulty("ai_normal", 1.20f, 1.00f, 1.5f, 0.80f, 0.15f,
                    0.35f, 0.20f, 0.30f, 0.15f, 0.50f, 12, 0.60f, 2.0f, 0.60f),
                Difficulty("ai_hard", 1.45f, 1.15f, 1.0f, 0.45f, 0.07f,
                    0.30f, 0.22f, 0.30f, 0.18f, 0.70f, 16, 0.70f, 1.0f, 0.80f),
                Difficulty("ai_nightmare", 1.75f, 1.30f, 0.7f, 0.20f, 0.02f,
                    0.28f, 0.22f, 0.30f, 0.20f, 0.90f, 20, 0.80f, 0.4f, 0.95f)
            };
        }

        // ---------- 关卡与阵容 ----------

        public static ReferenceLevel CreateLevel()
        {
            return new ReferenceLevel
            {
                Id = "level_01",
                MapSize = 64f,
                BuildableSize = 56f,
                PlayerBuildRadius = 26f,
                AiBuildRadius = 26f,
                BaseMaxHealth = 6000f,
                StartGold = 500,
                GoldPerSecond = 10f,
                EnergyPerSecond = 2.5f,
                MaxDuration = 900f,
                PlayerBaseCell = new GridPos(5, 5),
                AiBaseCell = new GridPos(27, 27),
                CellsPerSide = 32,
                CellSize = 2f
            };
        }

        public static ReferenceLoadout IronBulwark()
        {
            return new ReferenceLoadout
            {
                Id = "loadout_iron",
                DisplayName = "钢铁防线",
                BuildingIds = new[]
                {
                    "bld_rail_sniper", "bld_laser_sentry", "bld_phase_shield",
                    "bld_nano_repair", "bld_drone_hive"
                },
                UnitIds = new[] { "unit_trooper", "unit_breacher" },
                MaxUnitSlots = 20
            };
        }

        public static ReferenceLoadout RapidCounter()
        {
            return new ReferenceLoadout
            {
                Id = "loadout_blitz",
                DisplayName = "快速反击",
                BuildingIds = new[]
                {
                    "bld_laser_sentry", "bld_plasma_mortar", "bld_drone_hive",
                    "bld_nano_repair"
                },
                UnitIds = new[] { "unit_trooper", "unit_drone", "unit_mech" },
                MaxUnitSlots = 26
            };
        }

        // ---------- 构造工具 ----------

        private static float[] Aura(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] Shield(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] Repair(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] F3(float a, float b, float c) { return new[] { a, b, c }; }

        private static float[] Bt(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] Hp(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] Dmg(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] Itv(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] Rng(float a, float b, float c) { return new[] { a, b, c }; }
        private static float[] Splash(float a, float b, float c) { return new[] { a, b, c }; }

        private static BuildingSpec Defense(string id, int cost, int up2, int up3,
            float[] bt, float[] hp, float[] dmg, float[] itv, float[] rng,
            DamageType type, float[] splash, int priority)
        {
            BuildingSpec s = Base(id, cost, up2, up3, bt, hp);
            s.CombatEnabled = true;
            s.DamageType = type;
            s.DamageL1 = dmg[0];
            s.DamageL2 = dmg[1];
            s.DamageL3 = dmg[2];
            s.AttackIntervalL1 = itv[0];
            s.AttackIntervalL2 = itv[1];
            s.AttackIntervalL3 = itv[2];
            s.AttackRangeL1 = rng[0];
            s.AttackRangeL2 = rng[1];
            s.AttackRangeL3 = rng[2];
            s.SplashL1 = splash[0];
            s.SplashL2 = splash[1];
            s.SplashL3 = splash[2];
            s.TargetPriority = priority;
            return s;
        }

        private static BuildingSpec Support(string id, int cost, int up2, int up3, float[] bt, float[] hp,
            float[] aura, float[] shield, float[] repair, float[] cooldown)
        {
            BuildingSpec s = Base(id, cost, up2, up3, bt, hp);
            s.SupportEnabled = true;
            s.SupportRadiusL1 = aura[0];
            s.SupportRadiusL2 = aura[1];
            s.SupportRadiusL3 = aura[2];
            s.ShieldL1 = shield[0];
            s.ShieldL2 = shield[1];
            s.ShieldL3 = shield[2];
            s.RepairL1 = repair[0];
            s.RepairL2 = repair[1];
            s.RepairL3 = repair[2];
            s.SupportCooldownL1 = cooldown[0];
            s.SupportCooldownL2 = cooldown[1];
            s.SupportCooldownL3 = cooldown[2];
            return s;
        }

        private static BuildingSpec Base(string id, int cost, int up2, int up3, float[] bt, float[] hp)
        {
            return new BuildingSpec
            {
                Id = id,
                MaxLevel = 3,
                FootprintX = 1,
                FootprintZ = 1,
                BuildCost = cost,
                UpgradeCostToL2 = up2,
                UpgradeCostToL3 = up3,
                BuildTimeL1 = bt[0],
                BuildTimeL2 = bt[1],
                BuildTimeL3 = bt[2],
                HealthL1 = hp[0],
                HealthL2 = hp[1],
                HealthL3 = hp[2],
                SellRefundRate = 0.5f,
                AllowedTerrain = TerrainMask.Ground,
                CombatEnabled = false,
                SplashL1 = 0f,
                SplashL2 = 0f,
                SplashL3 = 0f
            };
        }

        private static UnitSpec Unit(string id, int cost, int supply, float hp, float speed,
            float damage, float interval, float range, float acquire, float structureMul,
            ArmorType armor, DamageType type)
        {
            return new UnitSpec
            {
                Id = id,
                DeployCost = cost,
                SupplyCost = supply,
                MaxHealth = hp,
                MoveSpeed = speed,
                BodyRadius = supply >= 4 ? 0.80f : (supply == 2 ? 0.45f : 0.35f),
                Armor = armor,
                DamageType = type,
                Damage = damage,
                AttackInterval = interval,
                Range = range,
                SplashRadius = 0f,
                AcquireRadius = acquire,
                StructureDamageMultiplier = structureMul,
                TargetPriority = 0
            };
        }

        private static AIDifficultySpec Difficulty(string id, float income, float start,
            float interval, float reaction, float mistake,
            float wBuild, float wUpgrade, float wDeploy, float wSkill,
            float aggression, int maxUnits, float counterThreshold, float jitter, float skillChance)
        {
            return new AIDifficultySpec
            {
                Id = id,
                IncomeMultiplier = income,
                StartGoldMultiplier = start,
                DecisionInterval = interval,
                ReactionDelay = reaction,
                MistakeChance = mistake,
                BuildBuildingWeight = wBuild,
                UpgradeBuildingWeight = wUpgrade,
                DeployUnitWeight = wDeploy,
                UseSkillWeight = wSkill,
                Aggression = aggression,
                MaxConcurrentUnits = maxUnits,
                CounterAttackThreshold = counterThreshold,
                BuildPosJitter = jitter,
                SkillUseChance = skillChance
            };
        }
    }
}