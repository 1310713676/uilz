using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.AI
{
    /// <summary>
    /// 确定性伪随机（xorshift32）。
    /// 不用 System.Random：它的序列在不同 .NET 版本间不保证一致，
    /// 回放与联机校验都会因此漂移。
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0u ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>[0, 1)</summary>
        public float NextFloat()
        {
            return (NextUInt() & 0xFFFFFFu) / 16777216f;
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
            {
                return 0;
            }

            return (int)(NextUInt() % (uint)maxExclusive);
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }
    }

    /// <summary>难度参数的纯数据快照，由 Core 层从 AIDifficultyConfig 转换。</summary>
    public struct AIDifficultySpec
    {
        public string Id;
        public float IncomeMultiplier;
        public float StartGoldMultiplier;
        public float DecisionInterval;
        public float ReactionDelay;
        public float MistakeChance;
        public float BuildBuildingWeight;
        public float UpgradeBuildingWeight;
        public float DeployUnitWeight;
        public float UseSkillWeight;
        public float Aggression;
        public int MaxConcurrentUnits;
        public float CounterAttackThreshold;
        public float BuildPosJitter;
        public float SkillUseChance;

        public float TotalActionWeight =>
            BuildBuildingWeight + UpgradeBuildingWeight + DeployUnitWeight + UseSkillWeight;
    }

    /// <summary>AI 一次感知的结果。struct，避免每帧分配。</summary>
    public struct AISnapshot
    {
        public int OwnBuildings;
        public int OwnProductionBuildings;
        public int OwnDefenseBuildings;
        public int OwnSupportBuildings;
        public int OwnUnits;
        public int EnemyUnits;
        public int EnemyUnitsNearBase;
        public float BaseHealthRatio;
        public int Gold;
        public int Energy;
        public int MaxUnitSlots;
        public int UsedUnitSlots;

        public bool UnitSlotsFull => UsedUnitSlots >= MaxUnitSlots;

        public bool UnderThreat => EnemyUnitsNearBase > 0;

        public float BaseHealthPercent => BaseHealthRatio * 100f;
    }

    /// <summary>
    /// 感知层：只读 GameState，不修改任何东西。
    /// 职责单一，便于单测 —— 给定状态应得到确定的快照。
    /// </summary>
    public sealed class AIPerception
    {
        private readonly string[] _productionBuildingIds;
        private readonly string[] _supportBuildingIds;

        public AIPerception(AIArsenal arsenal)
        {
            _productionBuildingIds = arsenal.ProductionBuildingIds;
            _supportBuildingIds = arsenal.SupportBuildingIds;
        }

        public AIPerception(string[] productionBuildingIds)
            : this(new AIArsenal { ProductionBuildingIds = productionBuildingIds })
        {
        }

        public AISnapshot Scan(GameState state, byte selfId, float threatRadius)
        {
            var snap = new AISnapshot();

            PlayerState me = state.GetPlayer(selfId);
            if (me == null)
            {
                return snap;
            }

            snap.Gold = me.Gold;
            snap.Energy = me.Energy;
            snap.MaxUnitSlots = me.MaxUnitSlots;
            snap.UsedUnitSlots = me.UsedUnitSlots;
            snap.BaseHealthRatio = me.BaseMaxHealth > 0f ? me.BaseHealth / me.BaseMaxHealth : 0f;

            for (int i = 0; i < state.Buildings.Count; i++)
            {
                BuildingInstance b = state.Buildings[i];
                if (b.OwnerId != selfId || b.Health <= 0f)
                {
                    continue;
                }

                snap.OwnBuildings++;

                if (IsProduction(b.ConfigId))
                {
                    snap.OwnProductionBuildings++;
                }
                else if (IsSupport(b.ConfigId))
                {
                    snap.OwnSupportBuildings++;
                }
                else
                {
                    snap.OwnDefenseBuildings++;
                }
            }

            float baseX = state.Grid.CellCenterX(me.FriendlyBaseCell.X);
            float baseZ = state.Grid.CellCenterZ(me.FriendlyBaseCell.Z);
            float sqrThreat = threatRadius * threatRadius;

            for (int i = 0; i < state.Units.Count; i++)
            {
                Combat.UnitInstance u = state.Units[i];
                if (!u.Alive)
                {
                    continue;
                }

                if (u.OwnerId == selfId)
                {
                    snap.OwnUnits++;
                    continue;
                }

                snap.EnemyUnits++;

                float dx = u.X - baseX;
                float dz = u.Z - baseZ;
                if (dx * dx + dz * dz <= sqrThreat)
                {
                    snap.EnemyUnitsNearBase++;
                }
            }

            return snap;
        }

        private bool IsSupport(string buildingId)
        {
            if (_supportBuildingIds == null)
            {
                return false;
            }

            for (int i = 0; i < _supportBuildingIds.Length; i++)
            {
                if (_supportBuildingIds[i] == buildingId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsProduction(string buildingId)
        {
            if (_productionBuildingIds == null)
            {
                return false;
            }

            for (int i = 0; i < _productionBuildingIds.Length; i++)
            {
                if (_productionBuildingIds[i] == buildingId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}