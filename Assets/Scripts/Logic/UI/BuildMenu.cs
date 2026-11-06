using System.Collections.Generic;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.UI
{
    /// <summary>建造菜单项的状态。UI 只需按 Enabled 决定按钮是否可点。</summary>
    public enum BuildItemState
    {
        Available = 0,
        NotEnoughGold = 1
    }

    /// <summary>
    /// 一个建造项。刻意不含 displayName / icon ——
    /// 那些是美术资源，由 Unity 侧从 ScriptableObject 取，Logic 层不碰。
    /// </summary>
    public struct BuildMenuItem
    {
        public string BuildingId;
        public int Cost;
        public BuildItemState State;

        public bool Enabled => State == BuildItemState.Available;

        public override string ToString()
        {
            return BuildingId + "(" + Cost + "," + State + ")";
        }
    }

    /// <summary>
    /// 建造菜单逻辑。只做两件事：
    /// 1. 按玩家阵容 + 当前金币算出每个项目的可用状态；
    /// 2. 把「点击某项 + 目标格」翻译成 GameCommand。
    ///
    /// 不负责：排序以外的任何展示逻辑、图标、悬停动画。
    /// </summary>
    public sealed class BuildMenu
    {
        private readonly IBuildingCatalog _buildings;
        private readonly List<BuildMenuItem> _items = new List<BuildMenuItem>();

        public BuildMenu(IBuildingCatalog buildings)
        {
            _buildings = buildings;
        }

        public IReadOnlyList<BuildMenuItem> Items => _items;

        // 静态空表，避免 UI 在无数据时判空
        public static readonly IReadOnlyList<BuildMenuItem> Empty = new BuildMenuItem[0];

        /// <summary>按阵容顺序刷新。阵容里查不到的 id 直接跳过（配置错误的容错）。</summary>
        public void Refresh(GameState state, byte playerId)
        {
            _items.Clear();

            if (state == null)
            {
                return;
            }

            PlayerState player = state.GetPlayer(playerId);
            if (player == null || player.AllowedBuildingIds == null)
            {
                return;
            }

            for (int i = 0; i < player.AllowedBuildingIds.Length; i++)
            {
                string id = player.AllowedBuildingIds[i];
                if (!_buildings.TryGet(id, out BuildingSpec spec))
                {
                    continue;
                }

                _items.Add(new BuildMenuItem
                {
                    BuildingId = id,
                    Cost = spec.BuildCost,
                    State = player.Gold >= spec.BuildCost
                        ? BuildItemState.Available
                        : BuildItemState.NotEnoughGold
                });
            }
        }

        public bool TryGetItem(string buildingId, out BuildMenuItem item)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].BuildingId == buildingId)
                {
                    item = _items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        /// <summary>
        /// 生成建造命令。菜单项不可用（金币不够 / 不在列表里）时返回 false，
        /// UI 应该据此不发出请求。
        /// </summary>
        public bool TryCreateCommand(string buildingId, GridPos cell, byte playerId, out GameCommand command)
        {
            command = default;

            if (!TryGetItem(buildingId, out BuildMenuItem item) || !item.Enabled)
            {
                return false;
            }

            command = GameCommand.Build(playerId, buildingId, cell);
            return true;
        }
    }
}