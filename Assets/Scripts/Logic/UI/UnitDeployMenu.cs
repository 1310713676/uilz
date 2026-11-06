using System.Collections.Generic;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.UI
{
    /// <summary>出兵项为何不可用。UI 直接按这个显示置灰原因。</summary>
    public enum DeployItemState
    {
        Available = 0,
        NotEnoughGold = 1,
        NoSlots = 2,
        MaxAlive = 3
    }

    public struct UnitDeployItem
    {
        public string UnitId;
        public int Cost;
        public int SupplyCost;
        public int AliveCount;
        public DeployItemState State;

        public bool Enabled => State == DeployItemState.Available;

        public override string ToString()
        {
            return UnitId + "(" + Cost + "," + State + ")";
        }
    }

    /// <summary>
    /// 出兵菜单逻辑。只判断「这个兵种现在能不能出」，不负责生成命令之外的任何事。
    ///
    /// 三条门槛，顺序即优先级：
    /// 1. 同屏数量上限（maxAlive）
    /// 2. 金币
    /// 3. 出兵槽位
    ///
    /// 顺序是刻意的：玩家看到「已达上限」比看到「金币不足」更有信息量。
    /// </summary>
    public sealed class UnitDeployMenu
    {
        private readonly IUnitCatalog _units;
        private readonly List<UnitDeployItem> _items = new List<UnitDeployItem>();

        public UnitDeployMenu(IUnitCatalog units)
        {
            _units = units;
        }

        public IReadOnlyList<UnitDeployItem> Items => _items;

        public static readonly IReadOnlyList<UnitDeployItem> Empty = new UnitDeployItem[0];

        public void Refresh(GameState state, byte playerId)
        {
            _items.Clear();

            if (state == null)
            {
                return;
            }

            PlayerState player = state.GetPlayer(playerId);
            if (player == null || player.AllowedUnitIds == null)
            {
                return;
            }

            int slotsLeft = player.MaxUnitSlots - player.UsedUnitSlots;

            for (int i = 0; i < player.AllowedUnitIds.Length; i++)
            {
                string id = player.AllowedUnitIds[i];
                if (!_units.TryGet(id, out UnitSpec spec))
                {
                    continue;
                }

                int alive = CountAlive(state, playerId, id);

                DeployItemState state2;
                if (spec.MaxAlive > 0 && alive >= spec.MaxAlive)
                {
                    state2 = DeployItemState.MaxAlive;
                }
                else if (player.Gold < spec.DeployCost)
                {
                    state2 = DeployItemState.NotEnoughGold;
                }
                else if (slotsLeft < spec.SupplyCost)
                {
                    state2 = DeployItemState.NoSlots;
                }
                else
                {
                    state2 = DeployItemState.Available;
                }

                _items.Add(new UnitDeployItem
                {
                    UnitId = id,
                    Cost = spec.DeployCost,
                    SupplyCost = spec.SupplyCost,
                    AliveCount = alive,
                    State = state2
                });
            }
        }

        public bool TryGetItem(string unitId, out UnitDeployItem item)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].UnitId == unitId)
                {
                    item = _items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public bool TryCreateCommand(string unitId, GridPos cell, byte playerId, out GameCommand command)
        {
            command = default;

            if (!TryGetItem(unitId, out UnitDeployItem item) || !item.Enabled)
            {
                return false;
            }

            command = GameCommand.Deploy(playerId, unitId, cell);
            return true;
        }

        private static int CountAlive(GameState state, byte ownerId, string unitId)
        {
            int n = 0;
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                if (u.Alive && u.OwnerId == ownerId && u.SpecId == unitId)
                {
                    n++;
                }
            }

            return n;
        }
    }
}