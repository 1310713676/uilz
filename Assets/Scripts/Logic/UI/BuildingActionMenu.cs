using System;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.UI
{
    /// <summary>选中一座建筑后的全部可读信息。UI 一次拿全，不做二次查询。</summary>
    public struct BuildingActionInfo
    {
        public bool Found;

        public int InstanceId;
        public string ConfigId;
        public int Level;
        public int MaxLevel;

        public float Health;
        public float MaxHealth;
        public float Shield;

        public bool IsComplete;

        /// <summary>建造进度 0→1。已完工恒为 1。</summary>
        public float BuildProgress;

        public bool CanUpgrade;
        public int UpgradeCost;

        public bool CanSell;
        public int SellRefund;
    }

    /// <summary>
    /// 建筑操作面板逻辑：点地面 → 找到那格上的建筑 → 给出「能不能升级 / 拆除」。
    ///
    /// 只做查询与命令生成，不修改任何状态 —— 真正执行仍走 RequestProcessor。
    /// </summary>
    public sealed class BuildingActionMenu
    {
        private readonly IBuildingCatalog _buildings;

        public BuildingActionMenu(IBuildingCatalog buildings)
        {
            _buildings = buildings;
        }

        /// <summary>
        /// 按格查建筑。空格、他人建筑、未知配置都返回 false，
        /// 这样 UI 可以直接「点空处关闭面板」。
        /// </summary>
        public bool TrySelect(GameState state, byte playerId, GridPos cell, out BuildingActionInfo info)
        {
            info = default;

            if (state == null)
            {
                return false;
            }

            PlayerState player = state.GetPlayer(playerId);
            if (player == null)
            {
                return false;
            }

            int instanceId = state.Grid.GetOccupant(cell);
            if (instanceId == 0)
            {
                return false;
            }

            BuildingInstance b = state.GetBuilding(instanceId);
            if (b == null || b.OwnerId != playerId)
            {
                return false;
            }

            if (!_buildings.TryGet(b.ConfigId, out BuildingSpec spec))
            {
                return false;
            }

            bool complete = b.IsComplete(state.Now);
            float total = spec.BuildTimeAt(b.Level);
            float progress = 1f;

            if (!complete && total > 0f)
            {
                float remaining = b.CompleteTime - state.Now;
                progress = Clamp01(1f - remaining / total);
            }

            int upgradeCost = spec.UpgradeCostFrom(b.Level);

            info = new BuildingActionInfo
            {
                Found = true,
                InstanceId = b.InstanceId,
                ConfigId = b.ConfigId,
                Level = b.Level,
                MaxLevel = spec.MaxLevel,
                Health = b.Health,
                MaxHealth = spec.HealthAt(b.Level),
                Shield = b.Shield,
                IsComplete = complete,
                BuildProgress = progress,
                CanUpgrade = complete && b.Level < spec.MaxLevel && upgradeCost > 0
                             && player.Gold >= upgradeCost,
                UpgradeCost = upgradeCost,
                CanSell = true,
                SellRefund = (int)Math.Floor(spec.TotalInvestedAt(b.Level) * spec.SellRefundRate)
            };

            return true;
        }

        public bool TryCreateUpgrade(GameState state, byte playerId, int instanceId,
            out GameCommand command, out string error)
        {
            command = default;
            error = null;

            BuildingInstance b = state.GetBuilding(instanceId);
            if (b == null)
            {
                error = "建筑不存在";
                return false;
            }

            if (b.OwnerId != playerId)
            {
                error = "不是自己的建筑";
                return false;
            }

            if (!b.IsComplete(state.Now))
            {
                error = "建造尚未完成";
                return false;
            }

            if (!_buildings.TryGet(b.ConfigId, out BuildingSpec spec))
            {
                error = "配置缺失";
                return false;
            }

            if (b.Level >= spec.MaxLevel)
            {
                error = "已达最高等级";
                return false;
            }

            PlayerState player = state.GetPlayer(playerId);
            int cost = spec.UpgradeCostFrom(b.Level);
            if (player != null && player.Gold < cost)
            {
                error = "金币不足，需要 " + cost;
                return false;
            }

            command = GameCommand.Upgrade(playerId, instanceId);
            return true;
        }

        public bool TryCreateSell(GameState state, byte playerId, int instanceId,
            out GameCommand command, out string error)
        {
            command = default;
            error = null;

            BuildingInstance b = state.GetBuilding(instanceId);
            if (b == null)
            {
                error = "建筑不存在";
                return false;
            }

            if (b.OwnerId != playerId)
            {
                error = "不是自己的建筑";
                return false;
            }

            command = GameCommand.Sell(playerId, instanceId);
            return true;
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) { return 0f; }
            return v > 1f ? 1f : v;
        }
    }
}