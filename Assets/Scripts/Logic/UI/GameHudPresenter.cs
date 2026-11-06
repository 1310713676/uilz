using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.Skill;

namespace DefenseCounter.Logic.UI
{
    /// <summary>
    /// UI 胶水层。把 HudModel / BuildMenu / SkillBar / PlacementController
    /// 的输出推给视图接口，并在内部做变化检测，避免每帧重绘。
    ///
    /// Unity 侧只需要：
    ///   var presenter = new GameHudPresenter(catalogs.Buildings, catalogs.Skills,
    ///       hudView, menuView, skillView, placementView);
    ///   void Update() { presenter.Tick(state, playerId, loop.Now, loop.Outcome); }
    ///
    /// 以及把按钮回调接到 presenter 的方法上。
    /// </summary>
    public sealed class GameHudPresenter
    {
        private readonly HudModel _hud = new HudModel();
        private readonly BuildMenu _buildMenu;
        private readonly SkillBar _skillBar;
        private readonly PlacementController _placement;
        private readonly IBuildingCatalog _buildings;

        private readonly IHudView _hudView;
        private readonly IBuildMenuView _menuView;
        private readonly ISkillBarView _skillView;
        private readonly IPlacementView _placementView;

        private int _lastMenuGold = int.MinValue;
        private int _lastSkillEnergy = int.MinValue;
        private bool _menuPushedOnce;
        private bool _skillPushedOnce;
        private MatchOutcome _lastOutcome = MatchOutcome.Ongoing;

        public GameHudPresenter(IBuildingCatalog buildings, ISkillCatalog skills,
            IHudView hudView, IBuildMenuView menuView, ISkillBarView skillView, IPlacementView placementView)
        {
            _buildings = buildings;
            _hudView = hudView;
            _menuView = menuView;
            _skillView = skillView;
            _placementView = placementView;

            _buildMenu = new BuildMenu(buildings);
            _skillBar = new SkillBar(skills);
            _placement = new PlacementController(buildings);
        }

        public PlacementController Placement => _placement;

        public BuildMenu Menu => _buildMenu;

        public SkillBar Skills => _skillBar;

        public HudModel Hud => _hud;

        /// <summary>每帧调用。内部逐块判断是否需要重绘。</summary>
        public void Tick(GameState state, byte playerId, MatchOutcome outcome)
        {
            if (state == null)
            {
                return;
            }

            PlayerState player = state.GetPlayer(playerId);
            if (player == null)
            {
                return;
            }

            PushHud(state, playerId, outcome);
            PushMenuIfNeeded(state, playerId, player);
            PushSkillsIfNeeded(player);
            PushPlacementGhost(state, playerId);
        }

        // ---------- 建造流程（供按钮回调） ----------

        public void OnBuildButtonClicked(string buildingId)
        {
            _placement.Begin(buildingId);
            _menuView.SetPlacing(buildingId);
            _placementView.HideGhost();
        }

        public void OnPlacementCancelled()
        {
            _placement.Cancel();
            _menuView.ClearPlacing();
            _placementView.HideGhost();
        }

        /// <summary>手指拖动时调用（cheapCheck = true，跳过 BFS 封路检测）。</summary>
        public void OnPointerMoved(GameState state, byte playerId, float worldX, float worldZ)
        {
            if (_placement.Mode != PlacementMode.Placing)
            {
                return;
            }

            _placement.UpdateCursor(state, playerId, worldX, worldZ, true);
        }

        /// <summary>松手确认。失败时通过 IPlacementView.ShowError 提示。</summary>
        public bool OnPlacementConfirmed(GameState state, byte playerId, out GameCommand command)
        {
            command = default;

            if (_placement.Mode != PlacementMode.Placing)
            {
                return false;
            }

            if (!_placement.TryConfirm(state, playerId, out command))
            {
                _placementView.ShowError(_placement.FailureMessage);
                return false;
            }

            _menuView.ClearPlacing();
            _placementView.HideGhost();
            return true;
        }

        // ---------- 内部推送 ----------

        private void PushHud(GameState state, byte playerId, MatchOutcome outcome)
        {
            bool changed = _hud.Refresh(state, playerId, state.Now, outcome);
            if (!changed)
            {
                return;
            }

            HudSnapshot s = _hud.Current;
            _hudView.SetGold(s.Gold);
            _hudView.SetEnergy(s.Energy);
            _hudView.SetBaseHealth(s.BaseHealth, s.BaseMaxHealth, s.BaseHealthRatio);
            _hudView.SetUnitSlots(s.UnitSlotsUsed, s.UnitSlotsMax);
            _hudView.SetBuildingCount(s.BuildingCount);
            _hudView.SetMatchTime(s.MatchTime);

            if (s.BaseDamaged)
            {
                _hudView.FlashBaseDamaged();
            }

            if (s.Outcome != _lastOutcome)
            {
                _lastOutcome = s.Outcome;
                _hudView.ShowOutcome(s.Outcome);
            }
        }

        private void PushMenuIfNeeded(GameState state, byte playerId, PlayerState player)
        {
            if (_menuPushedOnce && player.Gold == _lastMenuGold)
            {
                return;
            }

            _menuPushedOnce = true;
            _lastMenuGold = player.Gold;

            _buildMenu.Refresh(state, playerId);
            _menuView.ClearItems();

            var items = _buildMenu.Items;
            for (int i = 0; i < items.Count; i++)
            {
                _menuView.AddItem(items[i].BuildingId, items[i].Cost, items[i].Enabled);
            }
        }

        private void PushSkillsIfNeeded(PlayerState player)
        {
            // 能量变化 或 首次 → 刷新。冷却变化由 cooldownFill 每帧体现，这里只在能量变时重建列表。
            if (_skillPushedOnce && player.Energy == _lastSkillEnergy)
            {
                return;
            }

            _skillPushedOnce = true;
            _lastSkillEnergy = player.Energy;

            _skillBar.Refresh(player);
            _skillView.ClearItems();

            var items = _skillBar.Items;
            for (int i = 0; i < items.Count; i++)
            {
                _skillView.AddItem(items[i].SkillId, items[i].EnergyCost,
                    items[i].Ready, items[i].CooldownFill);
            }
        }

        private void PushPlacementGhost(GameState state, byte playerId)
        {
            if (_placement.Mode != PlacementMode.Placing)
            {
                return;
            }

            string id = _placement.BuildingId;
            if (string.IsNullOrEmpty(id) || !_buildings.TryGet(id, out BuildingSpec spec))
            {
                return;
            }

            // 多格建筑：格坐标是左下角，预览要放在占地矩形中心
            GridPos cell = _placement.Cell;
            float halfX = (spec.FootprintX - 1) * state.Grid.CellSize * 0.5f;
            float halfZ = (spec.FootprintZ - 1) * state.Grid.CellSize * 0.5f;

            float wx = state.Grid.CellCenterX(cell.X) + halfX;
            float wz = state.Grid.CellCenterZ(cell.Z) + halfZ;

            _placementView.ShowGhost(wx, wz, spec.FootprintX, spec.FootprintZ, _placement.IsValid);
        }
    }
}