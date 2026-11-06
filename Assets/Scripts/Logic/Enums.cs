namespace DefenseCounter.Data
{
    /// <summary>建筑大类。决定 UI 分组与 AI 建造权重。</summary>
    public enum BuildingType
    {
        Defense = 0,    // 拦截敌方兵力
        Production = 1, // 解锁或强化兵种
        Counter = 2,    // 提供反击技能或增强反击
        Support = 3     // 护盾、修复、干扰、增益
    }

    /// <summary>兵种定位。用于 AI 按克制关系选兵。</summary>
    public enum UnitRole
    {
        Assault = 0,    // 突击：均衡
        Breacher = 1,   // 破甲：对建筑强
        Skirmisher = 2, // 游走：高速、低血
        Siege = 3       // 攻城：高伤、极慢
    }

    /// <summary>伤害类型。与 ArmorType 组成克制矩阵。</summary>
    public enum DamageType
    {
        Kinetic = 0,    // 动能：对轻甲 +25%，对重甲 -20%
        Energy = 1,     // 能量：对护盾 +50%，对重甲 -30%
        Explosive = 2,  // 爆炸：对建筑 +40%，对轻甲 +10%
        Piercing = 3    // 穿甲：对重甲 +60%，对轻甲 -25%
    }

    /// <summary>护甲/结构类型。</summary>
    public enum ArmorType
    {
        Light = 0,
        Heavy = 1,
        Shielded = 2,
        Structure = 3
    }

    /// <summary>
    /// 地形层标志位。LevelConfig 用它声明「哪些层可建造、哪些层挡路」。
    /// 与 Unity Layer 无关，是逻辑枚举，便于配置与序列化。
    /// </summary>
    [System.Flags]
    public enum TerrainMask
    {
        None = 0,
        Ground = 1 << 0,     // 普通地面，默认可建造
        Road = 1 << 1,       // 道路，禁止建造（阻挡关键路径）
        Obstacle = 1 << 2,   // 障碍物，禁止建造
        Water = 1 << 3,      // 水面，禁止建造
        Cliff = 1 << 4,      // 悬崖/高台，禁止建造
        BasePad = 1 << 5     // 基地台座，禁止建造
    }

    /// <summary>建筑标签。AI 用它识别「已有什么」与「缺什么」。</summary>
    [System.Flags]
    public enum BuildingTag
    {
        None = 0,
        SingleTarget = 1 << 0,
        AreaDamage = 1 << 1,
        AntiArmor = 1 << 2,
        AntiSwarm = 1 << 3,
        Shield = 1 << 4,
        Repair = 1 << 5,
        Debuff = 1 << 6,
        UnlockUnit = 1 << 7,
        SkillSource = 1 << 8
    }

    /// <summary>兵种标签。</summary>
    [System.Flags]
    public enum UnitTag
    {
        None = 0,
        AntiStructure = 1 << 0,
        Swarm = 1 << 1,
        HighMobility = 1 << 2,
        Tanky = 1 << 3,
        Ranged = 1 << 4
    }

    /// <summary>技能施放形态。定义在 Logic 层，因为 Executor 要按它分支。</summary>
    public enum SkillCastTarget
    {
        GroundPoint = 0,  // 地面选点（范围技能）
        EnemyUnit = 1,    // 锁定敌方单位
        SelfBase = 2,     // 己方基地
        Global = 3        // 全图立即生效
    }

    /// <summary>技能效果类型。</summary>
    public enum SkillEffect
    {
        Damage = 0,      // 纯伤害
        Emp = 1,         // 瘫痪机械单位
        Summon = 2,      // 召唤兵种
        Teleport = 3,    // 传送己方单位
        Slow = 4,        // 减速
        Hack = 5         // 夺取控制权
    }
}