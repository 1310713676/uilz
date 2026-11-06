// ⚠️ 本文件不参与本机编译（依赖 UnityEngine.UI / Physics / Input）。
// 放置位置：Assets/Scripts/UnityIntegration/

using DefenseCounter.Core;
using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.Skill;
using DefenseCounter.Logic.UI;
using UnityEngine;

namespace DefenseCounter.UnityIntegration
{
    /// <summary>
    /// 把三样东西串起来：BattleSceneController（逻辑）、GameHudPresenter（胶水）、UGUI 视图（表现）。
    ///
    /// 挂在一个空物体上，Inspector 里拖 5 个引用即可：
    ///   battle / hudView / buildMenuView / skillBarView / placementView
    ///
    /// 本类只做三件事：每帧推数据、把点击转成命令、把坐标转成格。
    /// </summary>
    [DefaultExecutionOrder(100)]   // 必须在 BattleSceneController 装配之后执行
    public sealed class BattleUIHook : MonoBehaviour
    {
        [Header("核心")]
        public BattleSceneController battle;

        [Header("视图")]
        public UGuiHudView hudView;
        public UGuiBuildMenuView buildMenuView;
        public UGuiSkillBarView skillBarView;
        public UGuiPlacementView placementView;

        [Header("输入")]
        public Camera worldCamera;
        public LayerMask groundMask = ~0;
        public float maxRayDistance = 300f;

        private GameHudPresenter _presenter;
        private bool _hooked;

        private void Start()
        {
            if (battle == null)
            {
                battle = FindObjectOfType<BattleSceneController>();
            }

            if (battle == null)
            {
                Debug.LogError("[UI] 场景里找不到 BattleSceneController");
                enabled = false;
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            TryHook();
        }

        private void Update()
        {
            if (!_hooked)
            {
                TryHook();
                return;
            }

            GameState state = battle.Context.State;
            if (state == null)
            {
                return;
            }

            _presenter.Tick(state, MatchBootstrap.HumanPlayerId, battle.Context.Loop.Outcome);
            HandlePointer(state);
        }

        // ---------- 组装 ----------

        private void TryHook()
        {
            if (_hooked || battle == null || !battle.IsReady)
            {
                return;
            }

            MatchContext ctx = battle.Context;

            _presenter = new GameHudPresenter(ctx.Catalogs.Buildings, ctx.Catalogs.Skills,
                hudView, buildMenuView, skillBarView, placementView);

            if (buildMenuView != null)
            {
                buildMenuView.ItemClicked += OnBuildItemClicked;
                buildMenuView.CancelClicked += OnCancelClicked;
            }

            if (skillBarView != null)
            {
                skillBarView.ItemClicked += OnSkillClicked;
            }

            _hooked = true;
            Debug.Log("[UI] 已接管：" + ctx.Catalogs.BuildingCount + " 建筑可建");
        }

        // ---------- 输入 ----------

        private void HandlePointer(GameState state)
        {
            if (_presenter.Placement.Mode != PlacementMode.Placing)
            {
                return;
            }

            bool pressed = Input.GetMouseButtonDown(0);
            bool held = Input.GetMouseButton(0);

            if (!pressed && !held)
            {
                return;
            }

            if (!TryGetGroundPoint(out Vector3 point))
            {
                return;
            }

            if (held)
            {
                // 拖动只用便宜校验（跳过 BFS 封路），避免每帧上千格搜索
                _presenter.OnPointerMoved(state, MatchBootstrap.HumanPlayerId, point.x, point.z);
            }

            if (pressed
                && _presenter.OnPlacementConfirmed(state, MatchBootstrap.HumanPlayerId, out GameCommand cmd))
            {
                CommandResult result = battle.Context.Loop.Submit(cmd);
                if (!result.Ok)
                {
                    Debug.LogWarning("[UI] 建造被拒绝: " + result);
                }
            }
        }

        private bool TryGetGroundPoint(out Vector3 point)
        {
            point = default;

            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundMask))
            {
                return false;
            }

            point = hit.point;
            return true;
        }

        // ---------- 回调 ----------

        private void OnBuildItemClicked(string buildingId)
        {
            _presenter.OnBuildButtonClicked(buildingId);
        }

        private void OnCancelClicked()
        {
            _presenter.OnPlacementCancelled();
        }

        /// <summary>
        /// 技能施放：直接瞄准敌方基地。
        /// 第一阶段不做到「点技能 → 手动选点」的两段式交互 —— AI 也是这么做的，
        /// 保持人和 AI 的行为一致，也让 UI 少一个状态机。
        /// </summary>
        private void OnSkillClicked(string skillId)
        {
            GameState state = battle.Context.State;
            if (state == null)
            {
                return;
            }

            if (!TryFindEnemyBaseCell(state, out GridPos cell))
            {
                Debug.LogWarning("[UI] 找不到敌方基地");
                return;
            }

            if (!_presenter.Skills.TryCreateCommand(skillId, cell, MatchBootstrap.HumanPlayerId, out GameCommand cmd))
            {
                Debug.Log("[UI] 技能当前不可用: " + skillId);
                return;
            }

            CommandResult result = battle.Context.Loop.Submit(cmd);
            if (!result.Ok)
            {
                Debug.LogWarning("[UI] 技能被拒绝: " + result);
            }
        }

        private static bool TryFindEnemyBaseCell(GameState state, out GridPos cell)
        {
            cell = GridPos.Zero;

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState p = state.Players[i];
                if (p.Id != MatchBootstrap.HumanPlayerId && p.BaseHealth > 0f)
                {
                    cell = p.FriendlyBaseCell;
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (buildMenuView != null)
            {
                buildMenuView.ItemClicked -= OnBuildItemClicked;
                buildMenuView.CancelClicked -= OnCancelClicked;
            }

            if (skillBarView != null)
            {
                skillBarView.ItemClicked -= OnSkillClicked;
            }
        }
    }
}