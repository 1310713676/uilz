using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.UI
{
    /// <summary>
    /// HUD 的一帧数据。UI 只需读取它并渲染，不做任何计算。
    /// 所有派生值（血量比例等）都在这里算好，避免 UI 侧出现第二套算法。
    /// </summary>
    public struct HudSnapshot
    {
        public int Gold;
        public int Energy;

        public float BaseHealth;
        public float BaseMaxHealth;
        public float BaseHealthRatio;

        public int UnitSlotsUsed;
        public int UnitSlotsMax;

        public int BuildingCount;
        public float MatchTime;
        public MatchOutcome Outcome;

        /// <summary>本帧基地刚被打（用于播红光/震屏）。只在刷新瞬间为 true。</summary>
        public bool BaseDamaged;
    }

    /// <summary>
    /// HUD 数据模型。核心是「只在变化时通知」——
    /// 每帧无脑刷新会让 UGUI 每帧重建布局，是移动端最常见的性能问题之一。
    /// </summary>
    public sealed class HudModel
    {
        private float _lastBaseHealth = -1f;

        public HudSnapshot Current { get; private set; }

        /// <summary>强制刷新（初始化用）。</summary>
        public void ForceRefresh(GameState state, byte playerId, float now, MatchOutcome outcome)
        {
            _lastBaseHealth = -1f;
            Refresh(state, playerId, now, outcome);
        }

        /// <summary>刷新并返回「是否有变化」。返回 false 时 UI 可以完全跳过重绘。</summary>
        public bool Refresh(GameState state, byte playerId, float now, MatchOutcome outcome)
        {
            if (state == null)
            {
                return false;
            }

            PlayerState player = state.GetPlayer(playerId);
            if (player == null)
            {
                return false;
            }

            bool damaged = _lastBaseHealth >= 0f && player.BaseHealth < _lastBaseHealth - 0.01f;

            var next = new HudSnapshot
            {
                Gold = player.Gold,
                Energy = player.Energy,
                BaseHealth = player.BaseHealth,
                BaseMaxHealth = player.BaseMaxHealth,
                BaseHealthRatio = player.BaseMaxHealth > 0f
                    ? player.BaseHealth / player.BaseMaxHealth
                    : 0f,
                UnitSlotsUsed = player.UsedUnitSlots,
                UnitSlotsMax = player.MaxUnitSlots,
                BuildingCount = CountBuildings(state, playerId),
                MatchTime = now,
                Outcome = outcome,
                BaseDamaged = damaged
            };

            bool changed = !SameAsCurrent(next);

            // 即使没有可显示的变化，也要更新「上一帧血量」，否则掉血判定会漏
            _lastBaseHealth = player.BaseHealth;

            if (changed)
            {
                Current = next;
            }

            return changed;
        }

        private bool SameAsCurrent(HudSnapshot next)
        {
            return next.Gold == Current.Gold
                   && next.Energy == Current.Energy
                   && System.Math.Abs(next.BaseHealth - Current.BaseHealth) < 0.01f
                   && next.UnitSlotsUsed == Current.UnitSlotsUsed
                   && next.UnitSlotsMax == Current.UnitSlotsMax
                   && next.BuildingCount == Current.BuildingCount
                   && next.Outcome == Current.Outcome
                   // BaseDamaged 是瞬态标记，必须参与比较，否则它会永远停在 true
                   && next.BaseDamaged == Current.BaseDamaged;
        }

        private static int CountBuildings(GameState state, byte ownerId)
        {
            int n = 0;
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                if (state.Buildings[i].OwnerId == ownerId && state.Buildings[i].Health > 0f)
                {
                    n++;
                }
            }

            return n;
        }
    }
}