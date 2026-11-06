using DefenseCounter.Data;
using DefenseCounter.Logic;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;
using UnityEngine;

namespace DefenseCounter.Core
{
    /// <summary>
    /// 对战场景控制器。职责只有一个：装配一局，然后每帧驱动。
    ///
    /// Unity 侧接入方式：
    /// 1. 场景里建一个空物体，挂上本脚本
    /// 2. Inspector 里拖入 GameDatabase.asset
    /// 3. 填 levelId / 阵容 id / 难度 id
    /// 4. Play
    ///
    /// 所有游戏逻辑都在 Logic 层，本类只做「时钟」和「可视化」。
    /// </summary>
    public sealed class BattleSceneController : MonoBehaviour
    {
        [Header("配置（必填）")]
        public GameDatabase database;

        [Header("对局参数")]
        public string levelId = "level_01";
        public string playerLoadoutId = "loadout_iron";
        public string aiLoadoutId = "loadout_blitz";
        public string difficultyId = "ai_normal";

        [Header("调试")]
        [Tooltip("0 = 每次启动随机；固定值可复现同一局")]
        public uint seed = 0;

        [Tooltip("关闭后需要外部手动调用 Tick，用于逐帧调试")]
        public bool autoTick = true;

        public bool drawGizmos = true;

        private MatchContext _ctx;
        private bool _ended;

        public MatchContext Context => _ctx;

        public bool IsReady => _ctx != null && _ctx.Valid;

        private void Start()
        {
            if (database == null)
            {
                Debug.LogError("[Battle] database 未赋值，无法装配对局");
                enabled = false;
                return;
            }

            uint actualSeed = seed != 0u ? seed : (uint)System.DateTime.Now.Ticks;

            _ctx = MatchBootstrap.CreateVersusAi(database, levelId, playerLoadoutId, aiLoadoutId,
                difficultyId, actualSeed);

            for (int i = 0; i < _ctx.Warnings.Count; i++)
            {
                Debug.LogWarning("[Battle] " + _ctx.Warnings[i]);
            }

            if (!_ctx.Valid)
            {
                Debug.LogError("[Battle] 装配失败，检查上面的 Warning");
                enabled = false;
                return;
            }

            Debug.Log("[Battle] 装配完成 seed=" + actualSeed
                      + " 建筑=" + _ctx.Catalogs.BuildingCount
                      + " 兵种=" + _ctx.Catalogs.UnitCount
                      + " 技能=" + _ctx.Catalogs.SkillCount);

            if (_ctx.AI == null)
            {
                Debug.LogWarning("[Battle] 没有 AI 控制器（难度配置缺失），对方不会行动");
            }
        }

        private void Update()
        {
            if (!IsReady || !autoTick)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        /// <summary>推进一帧。顺序不能反：AI 先决策，再推进对局。</summary>
        public void Tick(float dt)
        {
            if (!IsReady)
            {
                return;
            }

            _ctx.AI?.Tick(dt);
            _ctx.Loop.Tick(dt);

            if (!_ended && _ctx.Loop.Outcome != MatchOutcome.Ongoing)
            {
                _ended = true;
                Debug.Log("[Battle] 对局结束: " + _ctx.Loop.Outcome
                          + "  时长 " + _ctx.Loop.Now.ToString("0") + "s");
            }
        }

        // ---------- 可视化 ----------

        private void OnDrawGizmos()
        {
            if (!drawGizmos || _ctx == null || _ctx.State == null)
            {
                return;
            }

            GameState state = _ctx.State;
            float y = 0.5f;

            // 建筑：蓝 = 玩家，红 = AI
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                BuildingInstance b = state.Buildings[i];
                Gizmos.color = b.OwnerId == MatchBootstrap.HumanPlayerId
                    ? new Color(0.2f, 0.8f, 1f, 0.9f)
                    : new Color(1f, 0.35f, 0.35f, 0.9f);

                Vector3 p = new Vector3(
                    state.Grid.CellCenterX(b.Origin.X),
                    y,
                    state.Grid.CellCenterZ(b.Origin.Z));
                Gizmos.DrawCube(p, Vector3.one * (b.FootprintX * state.Grid.CellSize * 0.8f));

                // 建造中的建筑画空心，避免和已完工混淆
                if (!b.IsComplete(state.Now))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(p, Vector3.one * (b.FootprintX * state.Grid.CellSize));
                }
            }

            // 单位：小球
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                if (!u.Alive)
                {
                    continue;
                }

                Gizmos.color = u.OwnerId == MatchBootstrap.HumanPlayerId ? Color.cyan : Color.red;
                Gizmos.DrawSphere(new Vector3(u.X, y, u.Z), 0.4f);
            }

            // 基地：柱子，高度表示剩余血量比例
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState p = state.Players[i];
                float ratio = p.BaseMaxHealth > 0f ? p.BaseHealth / p.BaseMaxHealth : 0f;
                if (ratio < 0f) { ratio = 0f; }

                Vector3 c = new Vector3(
                    state.Grid.CellCenterX(p.FriendlyBaseCell.X),
                    0f,
                    state.Grid.CellCenterZ(p.FriendlyBaseCell.Z));

                Gizmos.color = p.Id == MatchBootstrap.HumanPlayerId
                    ? new Color(0.2f, 1f, 0.6f)
                    : new Color(1f, 0.6f, 0.2f);
                Gizmos.DrawWireCube(c + Vector3.up * (ratio * 2f),
                    new Vector3(3f, ratio * 4f + 0.01f, 3f));
            }

            // 可建造区域
            if (state.BuildableAreas != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
                for (int i = 0; i < state.BuildableAreas.Length; i++)
                {
                    RectXZ r = state.BuildableAreas[i];
                    Vector3 center = new Vector3((r.MinX + r.MaxX) * 0.5f, 0f, (r.MinZ + r.MaxZ) * 0.5f);
                    Vector3 size = new Vector3(r.MaxX - r.MinX, 0.02f, r.MaxZ - r.MinZ);
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }
    }
}