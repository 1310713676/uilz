using System;
using System.Collections.Generic;
using DefenseCounter.Data;

namespace DefenseCounter.Logic.GameFlow
{
    /// <summary>
    /// 统一请求处理器。人类控制器与 AI 控制器发出同一种 GameCommand，
    /// 全部经过这里校验与执行。AI 没有后门。
    ///
    /// 生命周期：请求 -> 校验 -> 扣资源 -> 改变状态 -> 交给表现层/同步层
    /// </summary>
    public sealed class RequestProcessor
    {
        private readonly IBuildingCatalog _catalog;
        private readonly Combat.IUnitCatalog _units;

        /// <summary>
        /// 技能执行器。由 Host 侧接线（GameLoop / MatchBootstrap）。
        /// 未接线时技能一律拒绝 —— 不做「看起来能用其实没效果」的假实现。
        /// </summary>
        public Skill.SkillExecutor Skills;

        /// <summary>录制开关。开启后所有被接受的命令会追加到 AcceptedCommands，用于回放。</summary>
        public bool RecordAcceptedCommands;

        public readonly List<GameCommand> AcceptedCommands = new List<GameCommand>();

        public RequestProcessor(IBuildingCatalog catalog) : this(catalog, null)
        {
        }

        public RequestProcessor(IBuildingCatalog catalog, Combat.IUnitCatalog units)
        {
            _catalog = catalog;
            _units = units;
        }

        public CommandResult Process(GameState state, GameCommand cmd)
        {
            CommandResult result = ProcessInternal(state, cmd);

            // 录制模式：只记录被接受的命令。回放时按序重放即可还原整局。
            if (RecordAcceptedCommands && result.Ok)
            {
                AcceptedCommands.Add(cmd);
            }

            return result;
        }

        private CommandResult ProcessInternal(GameState state, GameCommand cmd)
        {
            if (state == null)
            {
                return CommandResult.Reject(CommandFailure.PlayerNotFound, "GameState 为空");
            }

            PlayerState player = state.GetPlayer(cmd.PlayerId);
            if (player == null)
            {
                return CommandResult.Reject(CommandFailure.PlayerNotFound, "玩家不存在: " + cmd.PlayerId);
            }

            switch (cmd.Type)
            {
                case CommandType.BuildBuilding:
                    return HandleBuild(state, player, cmd);

                case CommandType.UpgradeBuilding:
                    return HandleUpgrade(state, player, cmd);

                case CommandType.SellBuilding:
                    return HandleSell(state, player, cmd);

                case CommandType.DeployUnit:
                    return HandleDeploy(state, player, cmd);

                case CommandType.UseSkill:
                    return HandleSkill(state, player, cmd);

                default:
                    return CommandResult.Reject(CommandFailure.LoadoutLocked, "阵容在战斗中不可更改");
            }
        }

        // ---------- 建造 ----------

        private CommandResult HandleBuild(GameState state, PlayerState player, GameCommand cmd)
        {
            if (!_catalog.TryGet(cmd.ConfigId, out BuildingSpec spec))
            {
                return CommandResult.Reject(CommandFailure.UnknownConfig, "未知建筑: " + cmd.ConfigId);
            }

            if (player.Gold < spec.BuildCost)
            {
                return CommandResult.Reject(CommandFailure.NotEnoughGold,
                    "金币不足：需要 " + spec.BuildCost + "，当前 " + player.Gold);
            }

            var req = new PlacementRequest
            {
                BuildingId = spec.Id,
                FootprintX = spec.FootprintX,
                FootprintZ = spec.FootprintZ,
                BuildCost = spec.BuildCost,
                BuildRadiusContribution = spec.BuildRadiusContribution,
                AllowedTerrain = spec.AllowedTerrain,
                Origin = cmd.Cell
            };

            PlacementResult check = PlacementValidator.Validate(state.Grid, req, BuildContext(state, player));
            if (!check.Ok)
            {
                return CommandResult.PlacementReject(check);
            }

            int instanceId = state.AllocateInstanceId();
            if (!state.Grid.TryOccupy(cmd.Cell, spec.FootprintX, spec.FootprintZ, instanceId))
            {
                return CommandResult.Reject(CommandFailure.PlacementRejected, "占位失败");
            }

            player.Gold -= spec.BuildCost;

            state.Buildings.Add(new BuildingInstance
            {
                InstanceId = instanceId,
                ConfigId = spec.Id,
                OwnerId = player.Id,
                Level = 1,
                Health = spec.HealthAt(1),
                Origin = cmd.Cell,
                FootprintX = spec.FootprintX,
                FootprintZ = spec.FootprintZ,
                CompleteTime = state.Now + spec.BuildTimeAt(1)
            });

            return CommandResult.Success(instanceId);
        }

        // ---------- 升级 ----------

        private CommandResult HandleUpgrade(GameState state, PlayerState player, GameCommand cmd)
        {
            BuildingInstance inst = state.GetBuilding(cmd.InstanceId);
            if (inst == null)
            {
                return CommandResult.Reject(CommandFailure.InstanceNotFound, "建筑不存在: " + cmd.InstanceId);
            }

            if (inst.OwnerId != player.Id)
            {
                return CommandResult.Reject(CommandFailure.NotOwner, "不是自己的建筑");
            }

            if (!inst.IsComplete(state.Now))
            {
                return CommandResult.Reject(CommandFailure.BuildingNotReady,
                    "建造尚未完成，剩余 " + (inst.CompleteTime - state.Now).ToString("0.0") + "s");
            }

            if (!_catalog.TryGet(inst.ConfigId, out BuildingSpec spec))
            {
                return CommandResult.Reject(CommandFailure.UnknownConfig, "配置缺失: " + inst.ConfigId);
            }

            if (inst.Level >= spec.MaxLevel)
            {
                return CommandResult.Reject(CommandFailure.MaxLevel, "已达最高等级 " + spec.MaxLevel);
            }

            int cost = spec.UpgradeCostFrom(inst.Level);
            if (player.Gold < cost)
            {
                return CommandResult.Reject(CommandFailure.NotEnoughGold,
                    "升级需要 " + cost + "，当前 " + player.Gold);
            }

            player.Gold -= cost;
            inst.Level++;
            inst.Health = spec.HealthAt(inst.Level);
            inst.CompleteTime = state.Now + spec.BuildTimeAt(inst.Level);

            return CommandResult.Success(inst.InstanceId);
        }

        // ---------- 拆除 ----------

        private CommandResult HandleSell(GameState state, PlayerState player, GameCommand cmd)
        {
            BuildingInstance inst = state.GetBuilding(cmd.InstanceId);
            if (inst == null)
            {
                return CommandResult.Reject(CommandFailure.InstanceNotFound, "建筑不存在: " + cmd.InstanceId);
            }

            if (inst.OwnerId != player.Id)
            {
                return CommandResult.Reject(CommandFailure.NotOwner, "不是自己的建筑");
            }

            if (!_catalog.TryGet(inst.ConfigId, out BuildingSpec spec))
            {
                return CommandResult.Reject(CommandFailure.UnknownConfig, "配置缺失: " + inst.ConfigId);
            }

            int refund = (int)Math.Floor(spec.TotalInvestedAt(inst.Level) * spec.SellRefundRate);
            player.Gold += refund;

            state.Grid.Release(inst.Origin, inst.FootprintX, inst.FootprintZ, inst.InstanceId);
            state.Buildings.Remove(inst);

            CommandResult ok = CommandResult.Success(inst.InstanceId);
            ok.Message = "refund=" + refund;
            return ok;
        }

        // ---------- 出兵 / 技能（第二阶段接入实体层） ----------

        private CommandResult HandleDeploy(GameState state, PlayerState player, GameCommand cmd)
        {
            if (player.UsedUnitSlots >= player.MaxUnitSlots)
            {
                return CommandResult.Reject(CommandFailure.NotEnoughSlots,
                    "出兵槽位已满 " + player.UsedUnitSlots + "/" + player.MaxUnitSlots);
            }

            if (_units == null || !_units.TryGet(cmd.ConfigId, out Combat.UnitSpec spec))
            {
                return CommandResult.Reject(CommandFailure.UnknownConfig, "未知兵种: " + cmd.ConfigId);
            }

            if (player.Gold < spec.DeployCost)
            {
                return CommandResult.Reject(CommandFailure.NotEnoughGold,
                    "出兵需要 " + spec.DeployCost + "，当前 " + player.Gold);
            }

            if (player.UsedUnitSlots + spec.SupplyCost > player.MaxUnitSlots)
            {
                return CommandResult.Reject(CommandFailure.NotEnoughSlots,
                    "槽位不足：" + player.UsedUnitSlots + "+" + spec.SupplyCost + " > " + player.MaxUnitSlots);
            }

            // 阵型最小实现：目标格不可走或被友军占住时，向外找最近的可用格。
            // 没有这条，连续出兵会全部堆在同一坐标，视觉上像一块"兵团"。
            if (!TryFindDeployCell(state, cmd.Cell, 3, out GridPos deployCell))
            {
                return CommandResult.Reject(CommandFailure.PlacementRejected,
                    "出兵格 " + cmd.Cell + " 及其周围没有可用位置");
            }

            int instanceId = state.AllocateInstanceId();
            player.Gold -= spec.DeployCost;
            player.UsedUnitSlots += spec.SupplyCost;

            state.Units.Add(new Combat.UnitInstance
            {
                InstanceId = instanceId,
                SpecId = spec.Id,
                OwnerId = player.Id,
                X = state.Grid.CellCenterX(deployCell.X),
                Z = state.Grid.CellCenterZ(deployCell.Z),
                Health = spec.MaxHealth
            });

            CommandResult ok = CommandResult.Success(instanceId);
            ok.Message = "deployed " + spec.Id;
            return ok;
        }

        private CommandResult HandleSkill(GameState state, PlayerState player, GameCommand cmd)
        {
            if (Skills == null)
            {
                return CommandResult.Reject(CommandFailure.SkillRejected, "技能执行器未接线");
            }

            if (!AllowsSkill(player, cmd.ConfigId))
            {
                return CommandResult.Reject(CommandFailure.LoadoutLocked, "当前阵容不包含技能 " + cmd.ConfigId);
            }

            float wx = state.Grid.CellCenterX(cmd.Cell.X);
            float wz = state.Grid.CellCenterZ(cmd.Cell.Z);

            string error = Skills.TryCast(state, player.Id, cmd.ConfigId, wx, wz);
            if (error != null)
            {
                return CommandResult.Reject(CommandFailure.SkillRejected, error);
            }

            CommandResult ok = CommandResult.Success(0);
            ok.Message = "cast " + cmd.ConfigId;
            return ok;
        }

        /// <summary>null 或空数组视为不限制 —— 第一阶段先宽松，避免配置疏漏导致技能完全不可用。</summary>
        private static bool AllowsSkill(PlayerState player, string skillId)
        {
            if (player.AllowedSkillIds == null || player.AllowedSkillIds.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < player.AllowedSkillIds.Length; i++)
            {
                if (player.AllowedSkillIds[i] == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从指定格向外做环形搜索，找到第一个可走且无友军站位的格。
        /// 环序搜索保证结果确定（不依赖随机），联机与回放才能一致。
        /// </summary>
        private static bool TryFindDeployCell(GameState state, GridPos origin, int maxRadius, out GridPos cell)
        {
            cell = origin;

            if (IsCellFreeForUnit(state, origin))
            {
                return true;
            }

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r)
                        {
                            continue;
                        }

                        var candidate = new GridPos(origin.X + dx, origin.Z + dz);
                        if (IsCellFreeForUnit(state, candidate))
                        {
                            cell = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsCellFreeForUnit(GameState state, GridPos c)
        {
            if (!state.Grid.IsWalkable(c))
            {
                return false;
            }

            for (int i = 0; i < state.Units.Count; i++)
            {
                Combat.UnitInstance u = state.Units[i];
                if (!u.Alive)
                {
                    continue;
                }

                if (state.Grid.WorldToCell(u.X, u.Z) == c)
                {
                    return false;
                }
            }

            return true;
        }

        // ---------- 上下文 ----------

        private static PlacementContext BuildContext(GameState state, PlayerState player)
        {
            return new PlacementContext
            {
                Gold = player.Gold,
                AllowedBuildingIds = player.AllowedBuildingIds,
                BuildableAreas = state.BuildableAreas,
                BuildCenters = player.BuildCenters,
                EnemySpawns = player.EnemySpawns,
                FriendlyBaseCell = player.FriendlyBaseCell,
                CheckPathBlocking = true
            };
        }
    }
}