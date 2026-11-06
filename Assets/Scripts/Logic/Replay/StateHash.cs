using System;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.Replay
{
    /// <summary>
    /// 状态哈希。用于证明「同输入 → 同结果」。
    ///
    /// 三条纪律：
    /// 1. 用 float 的位模式参与哈希，不做容差比较 —— 容差会掩盖真实的浮点漂移。
    /// 2. 不依赖 string.GetHashCode：.NET Core 每次进程启动会随机化字符串哈希种子，
    ///    用它做状态哈希会导致「同状态不同哈希」，回放永远失败。
    /// 3. 依赖列表顺序：逻辑层所有遍历都是确定性的，顺序本身就是状态的一部分。
    /// </summary>
    public static class StateHash
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint Compute(GameState state)
        {
            if (state == null)
            {
                return 0u;
            }

            uint h = FnvOffset;

            Mix(ref h, Bits(state.Now));

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState p = state.Players[i];
                Mix(ref h, p.Id);
                Mix(ref h, p.Gold);
                Mix(ref h, p.Energy);
                Mix(ref h, Bits(p.BaseHealth));
                Mix(ref h, p.UsedUnitSlots);
                Mix(ref h, p.MaxUnitSlots);
            }

            for (int i = 0; i < state.Buildings.Count; i++)
            {
                BuildingInstance b = state.Buildings[i];
                Mix(ref h, b.InstanceId);
                MixString(ref h, b.ConfigId);
                Mix(ref h, b.OwnerId);
                Mix(ref h, b.Level);
                Mix(ref h, Bits(b.Health));
                Mix(ref h, Bits(b.Shield));
                Mix(ref h, b.Origin.X);
                Mix(ref h, b.Origin.Z);
                Mix(ref h, b.Level);
            }

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                Mix(ref h, u.InstanceId);
                MixString(ref h, u.SpecId);
                Mix(ref h, u.OwnerId);
                Mix(ref h, Bits(u.X));
                Mix(ref h, Bits(u.Z));
                Mix(ref h, Bits(u.Health));
                Mix(ref h, u.Alive ? 1 : 0);
                Mix(ref h, Bits(u.StunTimer));
                Mix(ref h, Bits(u.SlowTimer));
                Mix(ref h, Bits(u.SlowFactor));
            }

            // 网格占用状态：通过采样每格占用者校验放置一致性
            for (int z = 0; z < state.Grid.Height; z++)
            {
                for (int x = 0; x < state.Grid.Width; x++)
                {
                    int occupant = state.Grid.GetOccupant(new GridPos(x, z));
                    if (occupant != 0)
                    {
                        Mix(ref h, x);
                        Mix(ref h, z);
                        Mix(ref h, occupant);
                    }
                }
            }

            return h;
        }

        private static int Bits(float value)
        {
            // 位级精确：-0.0 与 +0.0 位不同，NaN 也能稳定参与哈希
            return BitConverter.SingleToInt32Bits(value);
        }

        private static void Mix(ref uint h, int value)
        {
            h = (h ^ (uint)value) * FnvPrime;
        }

        private static void MixString(ref uint h, string value)
        {
            if (value == null)
            {
                Mix(ref h, -1);
                return;
            }

            Mix(ref h, value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                Mix(ref h, value[i]);
            }
        }
    }
}