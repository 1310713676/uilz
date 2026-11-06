using System.Collections.Generic;
using DefenseCounter.Data;

namespace DefenseCounter.Logic.GameFlow
{
    public enum CommandType
    {
        BuildBuilding = 0,
        UpgradeBuilding = 1,
        SellBuilding = 2,
        DeployUnit = 3,
        UseSkill = 4,
        SelectLoadout = 5
    }

    public enum CommandFailure
    {
        None = 0,
        PlayerNotFound,
        UnknownConfig,
        PlacementRejected,
        NotEnoughGold,
        NotEnoughEnergy,
        SkillRejected,
        InstanceNotFound,
        NotOwner,
        BuildingNotReady,
        MaxLevel,
        NotEnoughSlots,
        LoadoutLocked
    }

    /// <summary>统一操作请求。人类与 AI 发出同一种结构，Host 统一校验。</summary>
    public struct GameCommand
    {
        public CommandType Type;
        public byte PlayerId;
        public string ConfigId;
        public GridPos Cell;
        public int InstanceId;

        public static GameCommand Build(byte player, string buildingId, GridPos cell)
        {
            return new GameCommand { Type = CommandType.BuildBuilding, PlayerId = player, ConfigId = buildingId, Cell = cell };
        }

        public static GameCommand Upgrade(byte player, int instanceId)
        {
            return new GameCommand { Type = CommandType.UpgradeBuilding, PlayerId = player, InstanceId = instanceId };
        }

        public static GameCommand Sell(byte player, int instanceId)
        {
            return new GameCommand { Type = CommandType.SellBuilding, PlayerId = player, InstanceId = instanceId };
        }

        public static GameCommand Deploy(byte player, string unitId, GridPos cell)
        {
            return new GameCommand { Type = CommandType.DeployUnit, PlayerId = player, ConfigId = unitId, Cell = cell };
        }

        public static GameCommand Skill(byte player, string skillId, GridPos cell)
        {
            return new GameCommand { Type = CommandType.UseSkill, PlayerId = player, ConfigId = skillId, Cell = cell };
        }
    }

    public struct CommandResult
    {
        public bool Ok;
        public CommandFailure Failure;
        public PlacementFailure Placement;
        public string Message;
        public int InstanceId;

        public static CommandResult Success(int instanceId)
        {
            return new CommandResult { Ok = true, Failure = CommandFailure.None, InstanceId = instanceId };
        }

        public static CommandResult Reject(CommandFailure failure, string message)
        {
            return new CommandResult { Ok = false, Failure = failure, Message = message };
        }

        public static CommandResult PlacementReject(PlacementResult r)
        {
            return new CommandResult
            {
                Ok = false,
                Failure = CommandFailure.PlacementRejected,
                Placement = r.Failure,
                Message = r.Message
            };
        }

        public override string ToString()
        {
            return Ok ? ("OK #" + InstanceId) : (Failure + (Placement != PlacementFailure.None ? "/" + Placement : "") + ": " + Message);
        }
    }

    /// <summary>
    /// 建筑的纯数据快照。Core 层从 BuildingConfig 转换而来，
    /// 这样 Logic 不需要知道 ScriptableObject 的存在。
    /// </summary>
    public struct BuildingSpec
    {
        public string Id;
        public int MaxLevel;
        public int FootprintX;
        public int FootprintZ;
        public int BuildCost;
        public int UpgradeCostToL2;
        public int UpgradeCostToL3;
        public float BuildTimeL1;
        public float BuildTimeL2;
        public float BuildTimeL3;
        public float HealthL1;
        public float HealthL2;
        public float HealthL3;
        public float SellRefundRate;
        public float BuildRadiusContribution;
        public TerrainMask AllowedTerrain;

        // ---- 战斗（仅防御建筑启用） ----
        public bool CombatEnabled;
        public DamageType DamageType;
        public float DamageL1;
        public float DamageL2;
        public float DamageL3;
        public float AttackIntervalL1;
        public float AttackIntervalL2;
        public float AttackIntervalL3;
        public float AttackRangeL1;
        public float AttackRangeL2;
        public float AttackRangeL3;
        public float SplashL1;
        public float SplashL2;
        public float SplashL3;
        public int TargetPriority;

        // ---- 支援（护盾 / 修复） ----
        public bool SupportEnabled;
        public float SupportRadiusL1;
        public float SupportRadiusL2;
        public float SupportRadiusL3;
        public float ShieldL1;
        public float ShieldL2;
        public float ShieldL3;
        public float RepairL1;
        public float RepairL2;
        public float RepairL3;
        public float SupportCooldownL1;
        public float SupportCooldownL2;
        public float SupportCooldownL3;

        public float DamageAt(int level)
        {
            if (level <= 1) { return DamageL1; }
            return level == 2 ? DamageL2 : DamageL3;
        }

        public float AttackIntervalAt(int level)
        {
            float v = level <= 1 ? AttackIntervalL1 : (level == 2 ? AttackIntervalL2 : AttackIntervalL3);
            return v <= 0.05f ? 0.05f : v;
        }

        public float AttackRangeAt(int level)
        {
            if (level <= 1) { return AttackRangeL1; }
            return level == 2 ? AttackRangeL2 : AttackRangeL3;
        }

        public float SplashAt(int level)
        {
            if (level <= 1) { return SplashL1; }
            return level == 2 ? SplashL2 : SplashL3;
        }

        public float SupportRadiusAt(int level)
        {
            if (level <= 1) { return SupportRadiusL1; }
            return level == 2 ? SupportRadiusL2 : SupportRadiusL3;
        }

        public float ShieldAt(int level)
        {
            if (level <= 1) { return ShieldL1; }
            return level == 2 ? ShieldL2 : ShieldL3;
        }

        public float RepairAt(int level)
        {
            if (level <= 1) { return RepairL1; }
            return level == 2 ? RepairL2 : RepairL3;
        }

        public float SupportCooldownAt(int level)
        {
            if (level <= 1) { return SupportCooldownL1; }
            return level == 2 ? SupportCooldownL2 : SupportCooldownL3;
        }

        public float BuildTimeAt(int level)
        {
            if (level <= 1) { return BuildTimeL1; }
            return level == 2 ? BuildTimeL2 : BuildTimeL3;
        }

        public float HealthAt(int level)
        {
            if (level <= 1) { return HealthL1; }
            return level == 2 ? HealthL2 : HealthL3;
        }

        public int UpgradeCostFrom(int currentLevel)
        {
            if (currentLevel == 1) { return UpgradeCostToL2; }
            return currentLevel == 2 ? UpgradeCostToL3 : 0;
        }

        public int TotalInvestedAt(int level)
        {
            int total = BuildCost;
            if (level >= 2) { total += UpgradeCostToL2; }
            if (level >= 3) { total += UpgradeCostToL3; }
            return total;
        }
    }

    public interface IBuildingCatalog
    {
        bool TryGet(string id, out BuildingSpec spec);
    }

    public sealed class DictionaryBuildingCatalog : IBuildingCatalog
    {
        private readonly Dictionary<string, BuildingSpec> _map = new Dictionary<string, BuildingSpec>();

        public int Count => _map.Count;

        public void Add(BuildingSpec spec)
        {
            _map[spec.Id] = spec;
        }

        public bool TryGet(string id, out BuildingSpec spec)
        {
            if (string.IsNullOrEmpty(id))
            {
                spec = default;
                return false;
            }

            return _map.TryGetValue(id, out spec);
        }
    }

    public sealed class PlayerState
    {
        public byte Id;
        public string LoadoutId;
        public int Gold;
        public int Energy;
        public double GoldCarry;
        public double EnergyCarry;

        /// <summary>收入倍率。难度与阵容的经济修正都乘在这里，默认 1。</summary>
        public float IncomeMultiplier = 1f;

        /// <summary>技能冷却剩余时间，key = skillId。懒创建，无技能时不占内存。</summary>
        public Dictionary<string, float> SkillCooldowns;

        public float GetCooldown(string skillId)
        {
            if (SkillCooldowns == null || string.IsNullOrEmpty(skillId))
            {
                return 0f;
            }

            float v;
            return SkillCooldowns.TryGetValue(skillId, out v) ? v : 0f;
        }

        public void SetCooldown(string skillId, float seconds)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return;
            }

            if (SkillCooldowns == null)
            {
                SkillCooldowns = new Dictionary<string, float>();
            }

            SkillCooldowns[skillId] = seconds;
        }

        /// <summary>每帧递减技能冷却。由 GameLoop 或 CombatSystem 调用。</summary>
        public void TickCooldowns(float dt)
        {
            if (SkillCooldowns == null || SkillCooldowns.Count == 0 || dt <= 0f)
            {
                return;
            }

            // 先收集到临时列表再写回，避免在枚举过程中修改字典
            var keys = new List<string>(SkillCooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string k = keys[i];
                float v = SkillCooldowns[k] - dt;
                SkillCooldowns[k] = v > 0f ? v : 0f;
            }
        }
        public float BaseHealth;
        public float BaseMaxHealth;
        public string[] AllowedBuildingIds;

        /// <summary>允许的技能 id。null 或空 = 不限制（第一阶段宽松处理）。</summary>
        public string[] AllowedSkillIds;

        /// <summary>允许出兵的兵种 id。由阵容决定，UI 的出兵菜单按它列表。</summary>
        public string[] AllowedUnitIds;

        public BaseCircle[] BuildCenters;
        public GridPos FriendlyBaseCell;
        public GridPos[] EnemySpawns;
        public int MaxUnitSlots;
        public int UsedUnitSlots;

        public bool Allows(string buildingId)
        {
            if (AllowedBuildingIds == null) { return false; }
            for (int i = 0; i < AllowedBuildingIds.Length; i++)
            {
                if (AllowedBuildingIds[i] == buildingId) { return true; }
            }
            return false;
        }
    }

    public sealed class BuildingInstance
    {
        public int InstanceId;
        public string ConfigId;
        public byte OwnerId;
        public int Level;
        public float Health;
        public GridPos Origin;
        public int FootprintX;
        public int FootprintZ;
        public float CompleteTime;
        public float AttackCooldown;

        /// <summary>护盾。伤害先扣护盾再扣血；护盾不自然恢复，靠护盾发生器刷新。</summary>
        public float Shield;

        public bool IsComplete(float now) => now >= CompleteTime;
    }

    public sealed class GameState
    {
        public readonly PlacementGrid Grid;
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<BuildingInstance> Buildings = new List<BuildingInstance>();
        public readonly List<Combat.UnitInstance> Units = new List<Combat.UnitInstance>();
        public RectXZ[] BuildableAreas;
        public float Now;

        private int _nextInstanceId = 1;

        public GameState(PlacementGrid grid)
        {
            Grid = grid;
        }

        public int AllocateInstanceId() => _nextInstanceId++;

        public PlayerState GetPlayer(byte id)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].Id == id) { return Players[i]; }
            }
            return null;
        }

        public BuildingInstance GetBuilding(int instanceId)
        {
            for (int i = 0; i < Buildings.Count; i++)
            {
                if (Buildings[i].InstanceId == instanceId) { return Buildings[i]; }
            }
            return null;
        }

        public void AdvanceTo(float now)
        {
            if (now > Now) { Now = now; }
        }
    }
}