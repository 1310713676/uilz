using DefenseCounter.Logic.Combat;

namespace DefenseCounter.Logic.GameFlow
{
    public enum MatchOutcome
    {
        Ongoing = 0,
        Win = 1,
        Lose = 2,
        Draw = 3
    }

    /// <summary>
    /// 对局模式。把胜负条件从 GameLoop 里抽出来，
    /// 后期加「出怪波次」「对抗模式」只需新增实现，不改循环。
    /// </summary>
    public interface IGameMode
    {
        MatchOutcome Evaluate(GameState state, float now);
    }

    /// <summary>单人/合作 vs AI：任一方基地归零即判负。</summary>
    public sealed class VersusAiMode : IGameMode
    {
        private readonly byte _humanId;
        private readonly byte _enemyId;
        private readonly float _maxDuration;

        public VersusAiMode(byte humanId, byte enemyId, float maxDuration)
        {
            _humanId = humanId;
            _enemyId = enemyId;
            _maxDuration = maxDuration;
        }

        public MatchOutcome Evaluate(GameState state, float now)
        {
            PlayerState human = state.GetPlayer(_humanId);
            PlayerState enemy = state.GetPlayer(_enemyId);

            if (human == null || enemy == null)
            {
                return MatchOutcome.Draw;
            }

            bool humanDead = human.BaseHealth <= 0f;
            bool enemyDead = enemy.BaseHealth <= 0f;

            if (humanDead && enemyDead) { return MatchOutcome.Draw; }
            if (humanDead) { return MatchOutcome.Lose; }
            if (enemyDead) { return MatchOutcome.Win; }

            // 0.01s 容差：9000 次 float 累加后 Now 会落在 899.9999 左右，
        // 严格比较会导致「打满时限却判不出胜负」。
        if (_maxDuration > 0f && now >= _maxDuration - 0.01f)
            {
                float h = human.BaseMaxHealth > 0f ? human.BaseHealth / human.BaseMaxHealth : 0f;
                float e = enemy.BaseMaxHealth > 0f ? enemy.BaseHealth / enemy.BaseMaxHealth : 0f;

                if (h > e + 0.01f) { return MatchOutcome.Win; }
                if (e > h + 0.01f) { return MatchOutcome.Lose; }
                return MatchOutcome.Draw;
            }

            return MatchOutcome.Ongoing;
        }
    }

    /// <summary>
    /// 对局主循环。固定顺序：推进时间 -> 资源累积 -> 战斗结算 -> 胜负判定。
    /// 顺序不能换：先算资源再战斗，才能保证同一 tick 内买到的兵立刻参与防守。
    /// </summary>
    public sealed class GameLoop
    {
        private readonly GameState _state;
        private readonly RequestProcessor _processor;
        private readonly CombatSystem _combat;
        private readonly IGameMode _mode;
        private readonly float _goldPerSecond;
        private readonly float _energyPerSecond;

        public GameLoop(GameState state, RequestProcessor processor, CombatSystem combat,
            IGameMode mode, float goldPerSecond, float energyPerSecond)
        {
            _state = state;
            _processor = processor;
            _combat = combat;
            _mode = mode;
            _goldPerSecond = goldPerSecond;
            _energyPerSecond = energyPerSecond;
            Outcome = MatchOutcome.Ongoing;
        }

        public MatchOutcome Outcome { get; private set; }

        public int TickCount { get; private set; }

        public float Now => _state.Now;

        /// <summary>推进一帧。对局结束后不再推进（防止结算后继续掉血）。</summary>
        public void Tick(float dt)
        {
            if (Outcome != MatchOutcome.Ongoing || dt <= 0f)
            {
                return;
            }

            _state.AdvanceTo(_state.Now + dt);
            Accrue(dt);
            _combat.Tick(_state, dt);
            Outcome = _mode.Evaluate(_state, _state.Now);
            TickCount++;
        }

        /// <summary>所有操作入口。对局结束后一律拒绝。</summary>
        public CommandResult Submit(GameCommand cmd)
        {
            if (Outcome != MatchOutcome.Ongoing)
            {
                return CommandResult.Reject(CommandFailure.LoadoutLocked, "对局已结束");
            }

            return _processor.Process(_state, cmd);
        }

        /// <summary>
        /// 资源累积。金币/能量是整数，用 carry 保留小数，
        /// 否则 7 金/秒 在 60fps 下每帧 0.116 会被取整成 0，永远攒不起钱。
        /// </summary>
        private void Accrue(float dt)
        {
            // 必须用 double 累加：float 下 7f * 0.1f = 0.69999998，
            // 100 帧后少 1 金币，900 秒一局少约 9 金。
            double energy = (double)_energyPerSecond * dt;

            for (int i = 0; i < _state.Players.Count; i++)
            {
                PlayerState p = _state.Players[i];
                double gold = (double)_goldPerSecond * p.IncomeMultiplier * dt;

                if (gold > 0.0)
                {
                    p.GoldCarry += gold;
                    int whole = (int)p.GoldCarry;
                    if (whole > 0)
                    {
                        p.Gold += whole;
                        p.GoldCarry -= whole;
                    }
                }

if (energy > 0.0)
                {
                    p.EnergyCarry += energy;
                    int wholeE = (int)p.EnergyCarry;
                    if (wholeE > 0)
                    {
                        p.Energy += wholeE;
                        p.EnergyCarry -= wholeE;
                    }
                }

                p.TickCooldowns(dt);
            }
        }
    }
}