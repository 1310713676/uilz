using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.UI
{
    public enum PlacementMode
    {
        Idle = 0,
        Placing = 1
    }

    /// <summary>
    /// 放置流程状态机：Idle → Placing → （确认 / 取消）→ Idle。
    ///
    /// 性能要点：拖动光标时用 cheapCheck=true 跳过 BFS 封路检测，
    /// 只在「确认」时跑完整校验。否则 60fps 拖动 = 每秒 60 次 1024 格 BFS。
    /// </summary>
    public sealed class PlacementController
    {
        private readonly IBuildingCatalog _buildings;

        private string _buildingId;
        private PlacementMode _mode = PlacementMode.Idle;
        private GridPos _cell;
        private bool _valid;
        private PlacementFailure _failure = PlacementFailure.None;
        private string _message = string.Empty;

        public PlacementController(IBuildingCatalog buildings)
        {
            _buildings = buildings;
        }

        public PlacementMode Mode => _mode;

        public string BuildingId => _buildingId;

        /// <summary>当前吸附到的格（占地左下角，与 PlacementRequest.Origin 一致）。</summary>
        public GridPos Cell => _cell;

        /// <summary>当前格是否合法（供 UI 显示绿/红预览）。</summary>
        public bool IsValid => _valid;

        public PlacementFailure Failure => _failure;

        public string FailureMessage => _message;

        public void Begin(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                return;
            }

            _buildingId = buildingId;
            _mode = PlacementMode.Placing;
            _valid = false;
            _failure = PlacementFailure.None;
            _message = string.Empty;
        }

        public void Cancel()
        {
            _mode = PlacementMode.Idle;
            _buildingId = null;
            _valid = false;
            _failure = PlacementFailure.None;
            _message = string.Empty;
        }

        /// <summary>
        /// 光标移动时调用。worldX/worldZ 为射线与地面的交点。
        /// cheapCheck=true 用于拖动过程（跳过 BFS），false 用于停下后的精确判定。
        /// </summary>
        public void UpdateCursor(GameState state, byte playerId, float worldX, float worldZ, bool cheapCheck)
        {
            if (_mode != PlacementMode.Placing || state == null)
            {
                return;
            }

            PlayerState player = state.GetPlayer(playerId);
            if (player == null)
            {
                return;
            }

            // 吸附到格：光标格即占地左下角
            _cell = new GridPos(
                state.Grid.WorldToCellX(worldX),
                state.Grid.WorldToCellZ(worldZ));

            Evaluate(state, player, cheapCheck);
        }

        /// <summary>
        /// 确认放置。跑完整校验（含封路），通过则生成命令并退出放置模式。
        /// 校验失败时保持在放置模式，让玩家换个位置继续。
        /// </summary>
        public bool TryConfirm(GameState state, byte playerId, out GameCommand command)
        {
            command = default;

            if (_mode != PlacementMode.Placing || state == null)
            {
                return false;
            }

            PlayerState player = state.GetPlayer(playerId);
            if (player == null)
            {
                return false;
            }

            if (!Evaluate(state, player, false) || !_valid)
            {
                return false;
            }

            command = GameCommand.Build(playerId, _buildingId, _cell);
            Cancel();
            return true;
        }

        // ---------- 内部 ----------

        private bool Evaluate(GameState state, PlayerState player, bool cheapCheck)
        {
            if (!_buildings.TryGet(_buildingId, out BuildingSpec spec))
            {
                _valid = false;
                _failure = PlacementFailure.InvalidRequest;
                _message = "未知建筑: " + _buildingId;
                return false;
            }

            var request = new PlacementRequest
            {
                BuildingId = spec.Id,
                FootprintX = spec.FootprintX,
                FootprintZ = spec.FootprintZ,
                BuildCost = spec.BuildCost,
                BuildRadiusContribution = spec.BuildRadiusContribution,
                AllowedTerrain = spec.AllowedTerrain,
                Origin = _cell
            };

            PlacementResult result = PlacementValidator.Validate(state.Grid, request,
                BuildContext(state, player, !cheapCheck));

            _valid = result.Ok;
            _failure = result.Failure;
            _message = result.Message;
            return result.Ok;
        }

        private static PlacementContext BuildContext(GameState state, PlayerState player, bool checkPath)
        {
            return new PlacementContext
            {
                Gold = player.Gold,
                AllowedBuildingIds = player.AllowedBuildingIds,
                BuildableAreas = state.BuildableAreas,
                BuildCenters = player.BuildCenters,
                EnemySpawns = player.EnemySpawns,
                FriendlyBaseCell = player.FriendlyBaseCell,
                CheckPathBlocking = checkPath
            };
        }
    }
}