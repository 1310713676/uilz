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
    /// <summary>一局对局的运行时上下文。Unity 侧只需持有它并每帧 Tick。</summary>
    public sealed class MatchContext
    {
        public GameState State;
        public GameLoop Loop;
        public RequestProcessor Processor;
        public AIController AI;
        public GameCatalogs Catalogs;
        public LevelConfig Level;

        /// <summary>装配过程中发现的问题（缺配置、坐标越界等）。不阻断创建，但要打出来。</summary>
        public readonly List<string> Warnings = new List<string>();

        public bool Valid => State != null && Loop != null;
    }

    /// <summary>
    /// 对局装配器。把 ScriptableObject 配置 + 关卡 + 阵容 + 难度，
    /// 一次性组装成可直接 Tick 的 GameLoop。
    ///
    /// 这是 Core 层唯一被 Unity 侧直接调用的入口。
    /// Unity 的 BattleSceneController 只需要：
    ///   _ctx = MatchBootstrap.CreateVersusAi(db, levelId, playerLoadout, aiLoadout, diffId, seed);
    ///   void Update() { _ctx.Loop.Tick(Time.deltaTime); }
    /// </summary>
    public static class MatchBootstrap
    {
        /// <summary>1 格 = 2 m，与《美术资产规格书》一致。</summary>
        public const float CellSize = 2f;

        public const byte HumanPlayerId = 1;
        public const byte AiPlayerId = 2;

        public static MatchContext CreateVersusAi(GameDatabase db, string levelId,
            string playerLoadoutId, string aiLoadoutId, string difficultyId, uint seed)
        {
            var ctx = new MatchContext();

            if (db == null)
            {
                ctx.Warnings.Add("GameDatabase 为空");
                return ctx;
            }

            LevelConfig level = db.GetLevel(levelId);
            if (level == null)
            {
                ctx.Warnings.Add("关卡不存在: " + levelId);
                return ctx;
            }

            LoadoutConfig playerLoadout = db.GetLoadout(playerLoadoutId);
            if (playerLoadout == null)
            {
                ctx.Warnings.Add("玩家阵容不存在: " + playerLoadoutId);
                return ctx;
            }

            LoadoutConfig aiLoadout = db.GetLoadout(aiLoadoutId);
            if (aiLoadout == null)
            {
                ctx.Warnings.Add("AI 阵容不存在: " + aiLoadoutId);
                return ctx;
            }

            AIDifficultyConfig difficultyConfig = db.GetDifficulty(difficultyId);
            if (difficultyConfig == null)
            {
                ctx.Warnings.Add("难度不存在，回退到普通: " + difficultyId);
                difficultyConfig = db.GetDifficulty("ai_normal");
            }

            GameCatalogs catalogs = ConfigBridge.BuildCatalogs(db);
            ctx.Catalogs = catalogs;
            ctx.Level = level;

            if (catalogs.BuildingCount == 0)
            {
                ctx.Warnings.Add("没有任何建筑配置");
                return ctx;
            }

            PlacementGrid grid = BuildGrid(level, ctx.Warnings);
            var state = new GameState(grid);

            // 可建造区域：LevelConfig 用中心 + 尺寸描述
            state.BuildableAreas = new[]
            {
                RectXZ.FromCenterSize(level.buildableAreaCenter.x, level.buildableAreaCenter.y,
                    level.buildableAreaSize.x, level.buildableAreaSize.y)
            };

            float startGoldMul = playerLoadout.startGoldMultiplier;
            if (difficultyConfig != null)
            {
                startGoldMul *= difficultyConfig.startGoldMultiplier;
            }

            PlayerState human = BuildPlayer(HumanPlayerId, level, playerLoadout,
                level.playerBasePositions, level.playerBuildRadius, level.aiBasePositions,
                startGoldMul, ctx.Warnings);

            float aiGoldMul = aiLoadout.startGoldMultiplier;
            if (difficultyConfig != null)
            {
                aiGoldMul *= difficultyConfig.startGoldMultiplier;
            }

            PlayerState ai = BuildPlayer(AiPlayerId, level, aiLoadout,
                level.aiBasePositions, level.aiBuildRadius, level.playerBasePositions,
                aiGoldMul, ctx.Warnings);

            if (human == null || ai == null)
            {
                ctx.Warnings.Add("基地位置数量不足，无法创建玩家");
                return ctx;
            }

            state.Players.Add(human);
            state.Players.Add(ai);

            if (difficultyConfig != null)
            {
                float income = difficultyConfig.incomeMultiplier * human.IncomeMultiplier;
                human.IncomeMultiplier = income;
                ai.IncomeMultiplier = difficultyConfig.incomeMultiplier * ai.IncomeMultiplier;
            }

            var processor = new RequestProcessor(catalogs.Buildings, catalogs.Units);
            var combat = new CombatSystem(catalogs.Buildings, catalogs.Units);

            // 技能执行器接线。没有技能配置时不创建 —— 技能命令会被明确拒绝，而不是静默失效。
            if (catalogs.SkillCount > 0)
            {
                var skillExecutor = new SkillExecutor(catalogs.Skills, catalogs.Units);
                processor.Skills = skillExecutor;
                combat.Skills = skillExecutor;
            }
            else
            {
                ctx.Warnings.Add("没有任何技能配置，技能系统不可用");
            }

            var mode = new VersusAiMode(HumanPlayerId, AiPlayerId, level.maxMatchDuration);
            var loop = new GameLoop(state, processor, combat, mode,
                level.goldPerSecond, level.energyPerSecond);

            ctx.State = state;
            ctx.Processor = processor;
            ctx.Loop = loop;

            if (difficultyConfig != null)
            {
                AIDifficultySpec diffSpec = ConfigBridge.ToSpec(difficultyConfig);
                AIArsenal arsenal = ConfigBridge.BuildArsenal(db, aiLoadout);
                ctx.AI = new AIController(state, processor, catalogs.Buildings, catalogs.Units,
                    diffSpec, arsenal, AiPlayerId, seed);
            }
            else
            {
                ctx.Warnings.Add("没有可用的难度配置，AI 不会行动");
            }

            return ctx;
        }

        /// <summary>按关卡尺寸建网格。整图先铺 Ground，特殊地形后续由关卡美术数据覆盖。</summary>
        private static PlacementGrid BuildGrid(LevelConfig level, List<string> warnings)
        {
            int cellsPerSide = Mathf.Max(8, Mathf.RoundToInt(level.mapSize.x / CellSize));

            if (Mathf.Abs(level.mapSize.x - level.mapSize.y) > 0.01f)
            {
                warnings.Add("地图非正方形（" + level.mapSize.x + " x " + level.mapSize.y
                             + "），当前只按 X 轴生成，Y 轴会被裁切");
            }

            float origin = -level.mapSize.x * 0.5f;
            var grid = new PlacementGrid(cellsPerSide, cellsPerSide, CellSize, origin, origin);
            grid.FillTerrain(TerrainMask.Ground);
            return grid;
        }

        private static PlayerState BuildPlayer(byte id, LevelConfig level, LoadoutConfig loadout,
            Vector3[] basePositions, float buildRadius, Vector3[] enemyBasePositions,
            float startGoldMultiplier, List<string> warnings)
        {
            if (basePositions == null || basePositions.Length == 0)
            {
                return null;
            }

            Vector3 basePos = basePositions[0];
            var baseCell = new GridPos(
                Mathf.FloorToInt((basePos.x + level.mapSize.x * 0.5f) / CellSize),
                Mathf.FloorToInt((basePos.z + level.mapSize.x * 0.5f) / CellSize));

            var enemyCells = new List<GridPos>();
            if (enemyBasePositions != null)
            {
                for (int i = 0; i < enemyBasePositions.Length; i++)
                {
                    enemyCells.Add(new GridPos(
                        Mathf.FloorToInt((enemyBasePositions[i].x + level.mapSize.x * 0.5f) / CellSize),
                        Mathf.FloorToInt((enemyBasePositions[i].z + level.mapSize.x * 0.5f) / CellSize)));
                }
            }

            return new PlayerState
            {
                Id = id,
                LoadoutId = loadout.id,
                Gold = Mathf.RoundToInt(level.startGold * startGoldMultiplier),
                Energy = level.startEnergy,
                IncomeMultiplier = loadout.incomeMultiplier,
                BaseHealth = level.baseMaxHealth,
                BaseMaxHealth = level.baseMaxHealth,
                AllowedBuildingIds = loadout.buildingIds,
                AllowedSkillIds = loadout.skillIds,
                AllowedUnitIds = loadout.unitIds,
                BuildCenters = new[] { new BaseCircle(basePos.x, basePos.z, buildRadius) },
                FriendlyBaseCell = baseCell,
                EnemySpawns = enemyCells.ToArray(),
                MaxUnitSlots = loadout.maxUnitSlots
            };
        }
    }
}