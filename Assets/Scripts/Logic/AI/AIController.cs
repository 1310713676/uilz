using System;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.AI
{
    /// <summary>AI 手上的可选单位清单，由阵容配置生成。</summary>
    public sealed class AIArsenal
    {
        public string[] DefenseBuildingIds = new string[0];
        public string[] SupportBuildingIds = new string[0];
        public string[] ProductionBuildingIds = new string[0];
        public string[] UnitIds = new string[0];
        public string[] SkillIds = new string[0];

        /// <summary>技能中最便宜的能量消耗，由 Core 预算好，避免 AI 依赖技能目录。</summary>
        public int MinSkillEnergyCost = int.MaxValue;
    }

    public enum AIIntentKind
    {
        Wait = 0,
        Build = 1,
        Upgrade = 2,
        Deploy = 3,
        UseSkill = 4
    }

    /// <summary>
    /// AI 控制器。只产出 GameCommand，不直接改 GameState ——
    /// 与人类玩家走完全相同的校验链，没有任何后门。
    ///
    /// 节奏：每 decisionInterval 决策一次，决策产生的命令再等 reactionDelay 才提交。
    /// 这两个参数是难度差异的主要来源，而不是单纯加钱。
    /// </summary>
    public sealed class AIController
    {
        private readonly GameState _state;
        private readonly RequestProcessor _processor;
        private readonly IBuildingCatalog _buildings;
        private readonly IUnitCatalog _units;
        private readonly AIDifficultySpec _diff;
        private readonly AIArsenal _arsenal;
        private readonly AIPerception _perception;
        private readonly DeterministicRandom _rng;
        private readonly byte _selfId;

        private float _decisionTimer;
        private float _reactionTimer;
        private bool _hasPending;
        private GameCommand _pending;

        private const float ThreatRadius = 25f;
        private const int PlaceSearchRadius = 6;

        public AIController(GameState state, RequestProcessor processor, IBuildingCatalog buildings,
            IUnitCatalog units, AIDifficultySpec difficulty, AIArsenal arsenal, byte selfId, uint seed)
        {
            _state = state;
            _processor = processor;
            _buildings = buildings;
            _units = units;
            _diff = difficulty;
            _arsenal = arsenal;
            _selfId = selfId;
            _rng = new DeterministicRandom(seed);
            _perception = new AIPerception(arsenal);
        }

        public int CommandsSubmitted { get; private set; }

        public int CommandsRejected { get; private set; }

        public AIIntentKind LastIntent { get; private set; }

        public AISnapshot LastSnapshot { get; private set; }

        public void Tick(float dt)
        {
            if (_hasPending)
            {
                _reactionTimer -= dt;
                if (_reactionTimer <= 0f)
                {
                    Flush();
                }
            }

            _decisionTimer += dt;
            if (_decisionTimer < _diff.DecisionInterval)
            {
                return;
            }

            _decisionTimer -= _diff.DecisionInterval;
            Decide();
        }

        // ---------- 决策 ----------

        private void Decide()
        {
            if (_hasPending)
            {
                return;
            }

            PlayerState me = _state.GetPlayer(_selfId);
            if (me == null || me.BaseHealth <= 0f)
            {
                LastIntent = AIIntentKind.Wait;
                return;
            }

            AISnapshot snap = _perception.Scan(_state, _selfId, ThreatRadius);
            LastSnapshot = snap;

            // 失误：本次决策直接作废。高难度下这个概率接近 0。
            if (_rng.Chance(_diff.MistakeChance))
            {
                LastIntent = AIIntentKind.Wait;
                return;
            }

            AIIntentKind kind = ChooseAction(snap);
            LastIntent = kind;

            switch (kind)
            {
                case AIIntentKind.Build:
                    PrepareBuild(snap);
                    break;

                case AIIntentKind.Upgrade:
                    PrepareUpgrade();
                    break;

                case AIIntentKind.Deploy:
                    PrepareDeploy(snap);
                    break;

                case AIIntentKind.UseSkill:
                    PrepareSkill();
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 施放技能。目标点选敌方基地附近（AOE 技能最稳定的落点），再叠加操作精度抖动。
        /// AI 不预判走位 —— 那属于第二阶段的行为，先保证「会放」而不是「放得准」。
        /// </summary>
        private void PrepareSkill()
        {
            if (_arsenal.SkillIds.Length == 0)
            {
                return;
            }

            float tx = 0f;
            float tz = 0f;
            bool found = false;

            for (int i = 0; i < _state.Players.Count; i++)
            {
                PlayerState p = _state.Players[i];
                if (p.Id == _selfId || p.BaseHealth <= 0f)
                {
                    continue;
                }

                tx = _state.Grid.CellCenterX(p.FriendlyBaseCell.X);
                tz = _state.Grid.CellCenterZ(p.FriendlyBaseCell.Z);
                found = true;
                break;
            }

            if (!found)
            {
                return;
            }

            float jitterX = (_rng.NextFloat() * 2f - 1f) * _diff.BuildPosJitter * 2f;
            float jitterZ = (_rng.NextFloat() * 2f - 1f) * _diff.BuildPosJitter * 2f;

            GridPos cell = _state.Grid.WorldToCell(tx + jitterX, tz + jitterZ);
            string skillId = _arsenal.SkillIds[_rng.NextInt(_arsenal.SkillIds.Length)];

            Queue(GameCommand.Skill(_selfId, skillId, cell));
        }

        /// <summary>
        /// 硬规则优先，其次加权随机。硬规则保证 AI 不会出现「有兵有钱但一直造塔」这种明显呆滞。
        /// </summary>
        private AIIntentKind ChooseAction(AISnapshot snap)
        {
            // 1. 没有出兵建筑就完全无法出兵，必须先补
            if (_arsenal.ProductionBuildingIds.Length > 0 && snap.OwnProductionBuildings == 0)
            {
                return AIIntentKind.Build;
            }

            // 1.5 阵地不足 -> 优先补防御建筑。
            // 缺了这条，AI 会把所有钱倒进出兵，家里一座塔没有，被一波带走。
            // 这是 RTS AI 的标准开局顺序（build order），不是调参能替代的。
            if (snap.OwnDefenseBuildings < MinDefenseBuildings && CanAffordDefense())
            {
                return AIIntentKind.Build;
            }

            // 2. 基地被咬 -> 立刻出兵
            if (snap.UnderThreat && !snap.UnitSlotsFull && _arsenal.UnitIds.Length > 0)
            {
                return AIIntentKind.Deploy;
            }

            // 3. 血量低于阈值且有敌兵 -> 转反击
            if (snap.BaseHealthRatio < _diff.CounterAttackThreshold
                && snap.EnemyUnits > 0
                && !snap.UnitSlotsFull
                && _arsenal.UnitIds.Length > 0)
            {
                return AIIntentKind.Deploy;
            }

            return RollWeighted(snap);
        }

        private AIIntentKind RollWeighted(AISnapshot snap)
        {
            // 经济感知：买不起就别选。人类玩家的建造按钮会变灰，AI 也必须受同样约束，
            // 否则会产生大量注定被 NotEnoughGold 拒绝的废决策。
            float cheapestBuild = CheapestBuildingCost();
            float cheapestUpgrade = CheapestUpgradeCost();
            float cheapestDeploy = CheapestUnitCost();

            bool canBuild = cheapestBuild > 0f && snap.Gold >= cheapestBuild * BuildBankFactor;
            bool canUpgrade = cheapestUpgrade > 0f && snap.Gold >= cheapestUpgrade && HasUpgradable();
            bool canDeploy = !snap.UnitSlotsFull
                             && snap.OwnUnits < _diff.MaxConcurrentUnits
                             && cheapestDeploy > 0f
                             && snap.Gold >= cheapestDeploy;

            float wBuild = canBuild ? _diff.BuildBuildingWeight : 0f;
            float wUpgrade = canUpgrade ? _diff.UpgradeBuildingWeight : 0f;
            float wDeploy = canDeploy ? _diff.DeployUnitWeight * (0.5f + _diff.Aggression) : 0f;

            // 技能：能量够才有意义。MinSkillEnergyCost 由 Core 侧算好传进来。
            bool canSkill = _arsenal.SkillIds.Length > 0
                            && snap.Energy >= _arsenal.MinSkillEnergyCost;

            float wSkill = canSkill ? _diff.UseSkillWeight : 0f;

            float total = wBuild + wUpgrade + wDeploy + wSkill;
            if (total <= 0f)
            {
                return AIIntentKind.Wait;
            }

            float roll = _rng.NextFloat() * total;

            if (roll < wBuild) { return AIIntentKind.Build; }
            roll -= wBuild;
            if (roll < wUpgrade) { return AIIntentKind.Upgrade; }
            roll -= wUpgrade;
            if (roll < wDeploy) { return AIIntentKind.Deploy; }
            return AIIntentKind.UseSkill;
        }

        // ---------- 执行准备 ----------

        private void PrepareBuild(AISnapshot snap)
        {
            string buildingId = PickBuildingToBuild(snap);
            if (string.IsNullOrEmpty(buildingId))
            {
                return;
            }

            if (!TryPickBuildCell(buildingId, out GridPos cell))
            {
                return;
            }

            Queue(GameCommand.Build(_selfId, buildingId, cell));
        }

        private string PickBuildingToBuild(AISnapshot snap)
        {
            bool needProduction = _arsenal.ProductionBuildingIds.Length > 0
                                  && snap.OwnProductionBuildings == 0;

            if (needProduction)
            {
                return _arsenal.ProductionBuildingIds[0];
            }

            // 配比：支援建筑数量不超过塔的一半。
            // 纯随机会让 AI 建出「三座同款塔」，阵容形同虚设。
            bool wantSupport = _arsenal.SupportBuildingIds.Length > 0
                               && snap.OwnSupportBuildings * 2 < snap.OwnDefenseBuildings;

            if (wantSupport)
            {
                return _arsenal.SupportBuildingIds[_rng.NextInt(_arsenal.SupportBuildingIds.Length)];
            }

            int total = _arsenal.DefenseBuildingIds.Length
                        + _arsenal.SupportBuildingIds.Length
                        + _arsenal.ProductionBuildingIds.Length;

            if (total == 0)
            {
                return null;
            }

            int index = _rng.NextInt(total);
            if (index < _arsenal.DefenseBuildingIds.Length)
            {
                return _arsenal.DefenseBuildingIds[index];
            }

            index -= _arsenal.DefenseBuildingIds.Length;
            if (index < _arsenal.SupportBuildingIds.Length)
            {
                return _arsenal.SupportBuildingIds[index];
            }

            return _arsenal.ProductionBuildingIds[index - _arsenal.SupportBuildingIds.Length];
        }

        /// <summary>
        /// 选建造位置：先在半径内掷一个「瞄准点」（受 buildPosJitter 影响，模拟操作精度），
        /// 再从瞄准点向外环形搜索第一个合法格。
        /// 纯随机采样失败率太高 —— 12 次里经常一次都不合法，产生大量废命令。
        /// </summary>
        private bool TryPickBuildCell(string buildingId, out GridPos cell)
        {
            cell = GridPos.Zero;

            PlayerState me = _state.GetPlayer(_selfId);
            if (me == null || !_buildings.TryGet(buildingId, out BuildingSpec spec))
            {
                return false;
            }

            float cx = 0f;
            float cz = 0f;
            float radius = 0f;

            if (me.BuildCenters != null && me.BuildCenters.Length > 0)
            {
                cx = me.BuildCenters[0].X;
                cz = me.BuildCenters[0].Z;
                radius = me.BuildCenters[0].Radius;
            }

            var ctx = new PlacementContext
            {
                Gold = me.Gold,
                AllowedBuildingIds = me.AllowedBuildingIds,
                BuildableAreas = _state.BuildableAreas,
                BuildCenters = me.BuildCenters,
                EnemySpawns = me.EnemySpawns,
                FriendlyBaseCell = me.FriendlyBaseCell
            };

            // 1. 瞄准点 = 半径内随机位置 + 精度抖动
            float angle = _rng.NextFloat() * 6.2831853f;
            float dist = _rng.NextFloat() * radius;
            float jitterX = (_rng.NextFloat() * 2f - 1f) * _diff.BuildPosJitter;
            float jitterZ = (_rng.NextFloat() * 2f - 1f) * _diff.BuildPosJitter;

            float aimX = cx + (float)Math.Cos(angle) * dist + jitterX;
            float aimZ = cz + (float)Math.Sin(angle) * dist + jitterZ;
            GridPos aim = _state.Grid.WorldToCell(aimX, aimZ);

            // 2. 环形搜索最近的合法格
            if (IsPlacementLegal(spec, ctx, aim))
            {
                cell = aim;
                return true;
            }

            for (int ring = 1; ring <= PlaceSearchRadius; ring++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Abs(dx) != ring && Math.Abs(dz) != ring)
                        {
                            continue;
                        }

                        var candidate = new GridPos(aim.X + dx, aim.Z + dz);
                        if (IsPlacementLegal(spec, ctx, candidate))
                        {
                            cell = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsPlacementLegal(BuildingSpec spec, PlacementContext ctx, GridPos cell)
        {
            // 先用最便宜的两条规则筛掉绝大多数无效格，
            // 避免对每个候选格都跑完整校验（其中包含 BFS 封路检查）。
            if (!_state.Grid.InBounds(cell) || _state.Grid.IsOccupied(cell))
            {
                return false;
            }

            var req = new PlacementRequest
            {
                BuildingId = spec.Id,
                FootprintX = spec.FootprintX,
                FootprintZ = spec.FootprintZ,
                BuildCost = spec.BuildCost,
                BuildRadiusContribution = spec.BuildRadiusContribution,
                AllowedTerrain = spec.AllowedTerrain,
                Origin = cell
            };

            return PlacementValidator.Validate(_state.Grid, req, ctx).Ok;
        }

        private void PrepareUpgrade()
        {
            BuildingInstance target = null;
            int bestLevel = int.MaxValue;

            for (int i = 0; i < _state.Buildings.Count; i++)
            {
                BuildingInstance b = _state.Buildings[i];
                if (b.OwnerId != _selfId || b.Health <= 0f || !b.IsComplete(_state.Now))
                {
                    continue;
                }

                if (!_buildings.TryGet(b.ConfigId, out BuildingSpec spec) || b.Level >= spec.MaxLevel)
                {
                    continue;
                }

                if (b.Level < bestLevel)
                {
                    bestLevel = b.Level;
                    target = b;
                }
            }

            if (target != null)
            {
                Queue(GameCommand.Upgrade(_selfId, target.InstanceId));
            }
        }

        private void PrepareDeploy(AISnapshot snap)
        {
            if (_arsenal.UnitIds.Length == 0 || snap.UnitSlotsFull || snap.OwnUnits >= _diff.MaxConcurrentUnits)
            {
                return;
            }

            PlayerState me = _state.GetPlayer(_selfId);
            if (me == null)
            {
                return;
            }

            string unitId = _arsenal.UnitIds[_rng.NextInt(_arsenal.UnitIds.Length)];
            GridPos cell = me.FriendlyBaseCell;

            Queue(GameCommand.Deploy(_selfId, unitId, cell));
        }

        /// <summary>
        /// 建造预算系数：金币达到建造价格的这个倍数才倾向建造。
        /// 1.0 会导致「一有 120 金就立刻把塔拍下去，然后十年没钱升级」。
        /// </summary>
        private const float BuildBankFactor = 1.5f;

        /// <summary>AI 认为「阵地稳了」的最小防御建筑数。低于此值会强制补塔。</summary>
        private const int MinDefenseBuildings = 3;

        private bool CanAffordDefense()
        {
            float cost = CheapestDefenseCost();
            PlayerState me = _state.GetPlayer(_selfId);
            return cost > 0f && me != null && me.Gold >= cost;
        }

        private float CheapestDefenseCost()
        {
            float best = float.MaxValue;

            if (_arsenal.DefenseBuildingIds == null)
            {
                return 0f;
            }

            for (int i = 0; i < _arsenal.DefenseBuildingIds.Length; i++)
            {
                if (_buildings.TryGet(_arsenal.DefenseBuildingIds[i], out BuildingSpec s) && s.BuildCost < best)
                {
                    best = s.BuildCost;
                }
            }

            return best == float.MaxValue ? 0f : best;
        }

        private float CheapestBuildingCost()
        {
            float best = float.MaxValue;

            if (_arsenal.DefenseBuildingIds != null)
            {
                for (int i = 0; i < _arsenal.DefenseBuildingIds.Length; i++)
                {
                    if (_buildings.TryGet(_arsenal.DefenseBuildingIds[i], out BuildingSpec s) && s.BuildCost < best)
                    {
                        best = s.BuildCost;
                    }
                }
            }

            if (_arsenal.ProductionBuildingIds != null)
            {
                for (int i = 0; i < _arsenal.ProductionBuildingIds.Length; i++)
                {
                    if (_buildings.TryGet(_arsenal.ProductionBuildingIds[i], out BuildingSpec s) && s.BuildCost < best)
                    {
                        best = s.BuildCost;
                    }
                }
            }

            return best == float.MaxValue ? 0f : best;
        }

        private float CheapestUnitCost()
        {
            float best = float.MaxValue;

            if (_arsenal.UnitIds == null)
            {
                return 0f;
            }

            for (int i = 0; i < _arsenal.UnitIds.Length; i++)
            {
                if (_units.TryGet(_arsenal.UnitIds[i], out UnitSpec s) && s.DeployCost < best)
                {
                    best = s.DeployCost;
                }
            }

            return best == float.MaxValue ? 0f : best;
        }

        private float CheapestUpgradeCost()
        {
            float best = float.MaxValue;

            for (int i = 0; i < _state.Buildings.Count; i++)
            {
                BuildingInstance b = _state.Buildings[i];
                if (b.OwnerId != _selfId || b.Health <= 0f || !b.IsComplete(_state.Now))
                {
                    continue;
                }

                if (!_buildings.TryGet(b.ConfigId, out BuildingSpec spec) || b.Level >= spec.MaxLevel)
                {
                    continue;
                }

                int cost = spec.UpgradeCostFrom(b.Level);
                if (cost > 0 && cost < best)
                {
                    best = cost;
                }
            }

            return best == float.MaxValue ? 0f : best;
        }

        private bool HasUpgradable()
        {
            for (int i = 0; i < _state.Buildings.Count; i++)
            {
                BuildingInstance b = _state.Buildings[i];
                if (b.OwnerId != _selfId || b.Health <= 0f || !b.IsComplete(_state.Now))
                {
                    continue;
                }

                if (_buildings.TryGet(b.ConfigId, out BuildingSpec spec) && b.Level < spec.MaxLevel)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------- 提交 ----------

        private void Queue(GameCommand cmd)
        {
            _pending = cmd;
            _hasPending = true;
            _reactionTimer = _diff.ReactionDelay;
        }

        private void Flush()
        {
            CommandResult result = _processor.Process(_state, _pending);
            _hasPending = false;

            if (result.Ok)
            {
                CommandsSubmitted++;
            }
            else
            {
                // 失败不重试：AI 下一轮重新决策，避免卡在同一个错误命令上死循环。
                CommandsRejected++;
            }
        }
    }
}