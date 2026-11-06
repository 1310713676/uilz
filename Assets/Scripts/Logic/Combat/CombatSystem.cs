using System;
using System.Collections.Generic;
using DefenseCounter.Data;
using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.Navigation;

namespace DefenseCounter.Logic.Combat
{
    /// <summary>
    /// 战斗结算。纯逻辑、可确定性重放：所有输入只有 state 与 dt。
    /// 顺序：塔开火 -> 兵行动 -> 清理死亡。
    /// 防御方先手投递，符合塔防的射程优势直觉。
    /// </summary>
    public sealed class CombatSystem
    {
        private readonly IBuildingCatalog _buildings;
        private readonly IUnitCatalog _units;
        private readonly PathFinder _pathFinder = new PathFinder();
        private readonly List<GridPos> _pathBuffer = new List<GridPos>(256);

        /// <summary>技能执行器。由 Host 侧接线；未接线时延迟技能不会推进。</summary>
        public Skill.SkillExecutor Skills;

        public CombatSystem(IBuildingCatalog buildings, IUnitCatalog units)
        {
            _buildings = buildings;
            _units = units;
        }

        public void Tick(GameState state, float dt)
        {
            if (state == null || dt <= 0f)
            {
                return;
            }

            if (Skills != null)
            {
                Skills.Tick(state);
            }

            UpdateSupport(state, dt);
            UpdateTowers(state, dt);
            UpdateUnits(state, dt);
            SeparateUnits(state);
            Cleanup(state);
        }

        // ---------- 支援 ----------

        /// <summary>
        /// 支援建筑：护盾与修复。
        /// 护盾是「一次性快照」（冷却制刷新，取最大值），修复是持续灌注。
        /// 二者都不作用于基地 —— 第一阶段基地不吃支援，避免出现无解的基地回血。
        /// </summary>
        private void UpdateSupport(GameState s, float dt)
        {
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                BuildingInstance source = s.Buildings[i];
                if (source.Health <= 0f || !source.IsComplete(s.Now))
                {
                    continue;
                }

                if (!_buildings.TryGet(source.ConfigId, out BuildingSpec spec) || !spec.SupportEnabled)
                {
                    continue;
                }

                float radius = spec.SupportRadiusAt(source.Level);
                float shield = spec.ShieldAt(source.Level);
                float repair = spec.RepairAt(source.Level);

                if (radius <= 0f || (shield <= 0f && repair <= 0f))
                {
                    continue;
                }

                source.AttackCooldown -= dt;
                bool shieldReady = shield > 0f && source.AttackCooldown <= 0f;

                float sx, sz;
                CenterOfBuilding(s, source, out sx, out sz);
                float sqr = radius * radius;

                for (int j = 0; j < s.Buildings.Count; j++)
                {
                    BuildingInstance target = s.Buildings[j];
                    if (target.Health <= 0f || target.OwnerId != source.OwnerId)
                    {
                        continue;
                    }

                    float tx, tz;
                    CenterOfBuilding(s, target, out tx, out tz);
                    float dx = tx - sx;
                    float dz = tz - sz;

                    if (dx * dx + dz * dz > sqr)
                    {
                        continue;
                    }

                    if (repair > 0f)
                    {
                        target.Health += repair * dt;
                        float max = MaxHealthOf(target);
                        if (target.Health > max)
                        {
                            target.Health = max;
                        }
                    }

                    if (shieldReady && target.Shield < shield)
                    {
                        target.Shield = shield;
                    }
                }

                if (shieldReady)
                {
                    source.AttackCooldown = spec.SupportCooldownAt(source.Level);
                }
            }
        }

        private float MaxHealthOf(BuildingInstance b)
        {
            return _buildings.TryGet(b.ConfigId, out BuildingSpec spec) ? spec.HealthAt(b.Level) : b.Health;
        }

        /// <summary>
        /// 对建筑造成伤害：先扣护盾，溢出部分再扣血。
        /// 所有建筑受伤都必须走这里，否则护盾会在某些伤害路径上被绕过。
        /// </summary>
        private static void DealDamageToBuilding(BuildingInstance b, float damage)
        {
            if (damage <= 0f)
            {
                return;
            }

            float absorbed = damage < b.Shield ? damage : b.Shield;
            b.Shield -= absorbed;
            b.Health -= damage - absorbed;
        }

        // ---------- 塔 ----------

        private void UpdateTowers(GameState s, float dt)
        {
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                BuildingInstance b = s.Buildings[i];
                if (b.Health <= 0f || !b.IsComplete(s.Now))
                {
                    continue;
                }

                if (!_buildings.TryGet(b.ConfigId, out BuildingSpec spec) || !spec.CombatEnabled)
                {
                    continue;
                }

                b.AttackCooldown -= dt;
                if (b.AttackCooldown > 0f)
                {
                    continue;
                }

                float range = spec.AttackRangeAt(b.Level);
                float cx, cz;
                CenterOfBuilding(s, b, out cx, out cz);

                UnitInstance best = null;
                float bestScore = float.MaxValue;
                for (int u = 0; u < s.Units.Count; u++)
                {
                    UnitInstance unit = s.Units[u];
                    if (!unit.Alive || unit.OwnerId == b.OwnerId)
                    {
                        continue;
                    }

                    float d = unit.DistanceTo(cx, cz);
                    if (d > range)
                    {
                        continue;
                    }

                    float score = spec.TargetPriority == 1 ? unit.Health : d;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = unit;
                    }
                }

                if (best == null)
                {
                    continue;
                }

                float damage = Compute(spec.DamageAt(b.Level), spec.DamageType, ArmorOfUnitSpec(best.SpecId));
                best.Health -= damage;
                if (best.Health <= 0f)
                {
                    best.Alive = false;
                }

                float splash = spec.SplashAt(b.Level);
                if (splash > 0f)
                {
                    float tx = best.X;
                    float tz = best.Z;
                    for (int u = 0; u < s.Units.Count; u++)
                    {
                        UnitInstance other = s.Units[u];
                        if (!other.Alive || other == best || other.OwnerId == b.OwnerId)
                        {
                            continue;
                        }

                        float d = other.DistanceTo(tx, tz);
                        if (d <= splash)
                        {
                            other.Health -= damage * Falloff(d, splash);
                            if (other.Health <= 0f)
                            {
                                other.Alive = false;
                            }
                        }
                    }
                }

                b.AttackCooldown = spec.AttackIntervalAt(b.Level);
            }
        }

        // ---------- 兵 ----------

        private void UpdateUnits(GameState s, float dt)
        {
            for (int i = 0; i < s.Units.Count; i++)
            {
                UnitInstance unit = s.Units[i];
                if (!unit.Alive)
                {
                    continue;
                }

                if (!_units.TryGet(unit.SpecId, out UnitSpec spec))
                {
                    continue;
                }

                // 状态计时：瘫痪期间完全不动、不攻击；减速按 SlowFactor 影响移动
                if (unit.StunTimer > 0f)
                {
                    unit.StunTimer -= dt;
                    continue;
                }

                if (unit.SlowTimer > 0f)
                {
                    unit.SlowTimer -= dt;
                    if (unit.SlowTimer <= 0f)
                    {
                        unit.SlowFactor = 1f;
                    }
                }

                unit.AttackCooldown -= dt;

                if (!IsTargetValid(s, unit))
                {
                    unit.Target = AcquireTarget(s, unit, spec);
                }

                if (unit.Target.IsNone)
                {
                    continue;
                }

                float tx, tz, radius;
                if (!ResolveTargetPoint(s, unit.Target, out tx, out tz, out radius))
                {
                    unit.Target = TargetRef.None;
                    continue;
                }

                float dist = unit.DistanceTo(tx, tz);
                float reach = spec.Range + radius;

                if (dist > reach)
                {
                    FollowPath(s, unit, spec, tx, tz, dt);
                    continue;
                }

                if (unit.AttackCooldown > 0f)
                {
                    continue;
                }

                float damage = Compute(spec.Damage, spec.DamageType, ArmorOfTarget(s, unit.Target));

                if (spec.SplashRadius > 0f)
                {
                    ApplySplash(s, unit, tx, tz, spec, damage);
                }
                else
                {
                    ApplyDamage(s, unit.Target, damage);
                }

                unit.AttackCooldown = spec.AttackInterval;
            }
        }

        private TargetRef AcquireTarget(GameState s, UnitInstance unit, UnitSpec spec)
        {
            TargetRef best = TargetRef.None;
            float bestScore = float.MaxValue;

            // 敌方建筑
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                BuildingInstance b = s.Buildings[i];
                if (b.OwnerId == unit.OwnerId || b.Health <= 0f)
                {
                    continue;
                }

                float cx, cz;
                CenterOfBuilding(s, b, out cx, out cz);
                float d = unit.DistanceTo(cx, cz);
                if (d > spec.AcquireRadius)
                {
                    continue;
                }

                float score = Score(spec.TargetPriority, d, b.Health, true);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = TargetRef.ForBuilding(b.InstanceId);
                }
            }

            // 敌方基地
            for (int i = 0; i < s.Players.Count; i++)
            {
                PlayerState p = s.Players[i];
                if (p.Id == unit.OwnerId || p.BaseHealth <= 0f)
                {
                    continue;
                }

                float bx, bz;
                CenterOfBase(s, p, out bx, out bz);
                float d = unit.DistanceTo(bx, bz);
                if (d > spec.AcquireRadius)
                {
                    continue;
                }

                float score = Score(spec.TargetPriority, d, p.BaseHealth, true);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = TargetRef.ForBase(p.Id);
                }
            }

            // 敌方单位
            for (int i = 0; i < s.Units.Count; i++)
            {
                UnitInstance other = s.Units[i];
                if (!other.Alive || other == unit || other.OwnerId == unit.OwnerId)
                {
                    continue;
                }

                float d = unit.DistanceTo(other.X, other.Z);
                if (d > spec.AcquireRadius)
                {
                    continue;
                }

                float score = Score(spec.TargetPriority, d, other.Health, false);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = TargetRef.ForUnit(other.InstanceId);
                }
            }

            // 兜底：没有可打目标时朝最近的敌方基地推进。
            // 缺了这条，兵会在索敌半径外原地站桩 —— RTS 里最容易被当成 AI bug 的形态。
            if (best.IsNone)
            {
                float baseBest = float.MaxValue;
                for (int i = 0; i < s.Players.Count; i++)
                {
                    PlayerState p = s.Players[i];
                    if (p.Id == unit.OwnerId || p.BaseHealth <= 0f)
                    {
                        continue;
                    }

                    float bx, bz;
                    CenterOfBase(s, p, out bx, out bz);
                    float d = unit.DistanceTo(bx, bz);
                    if (d < baseBest)
                    {
                        baseBest = d;
                        best = TargetRef.ForBase(p.Id);
                    }
                }
            }

            return best;
        }

        /// <summary>targetPriority: 0 = 最近，1 = 血量最低，2 = 建筑/基地优先。</summary>
        private static float Score(int priority, float distance, float health, bool isStructure)
        {
            if (priority == 1) { return health; }
            if (priority == 2) { return isStructure ? distance * 0.2f : distance; }
            return distance;
        }

        private bool IsTargetValid(GameState s, UnitInstance unit)
        {
            if (unit.Target.IsNone)
            {
                return false;
            }

            float x, z, r;
            return ResolveTargetPoint(s, unit.Target, out x, out z, out r);
        }

        private bool ResolveTargetPoint(GameState s, TargetRef t, out float x, out float z, out float radius)
        {
            x = 0f; z = 0f; radius = 0f;

            switch (t.Kind)
            {
                case TargetKind.Building:
                {
                    BuildingInstance b = FindBuilding(s, t.Id);
                    if (b == null || b.Health <= 0f) { return false; }
                    CenterOfBuilding(s, b, out x, out z);
                    radius = Math.Max(b.FootprintX, b.FootprintZ) * s.Grid.CellSize * 0.5f;
                    return true;
                }

                case TargetKind.Unit:
                {
                    UnitInstance u = FindUnit(s, t.Id);
                    if (u == null || !u.Alive) { return false; }
                    x = u.X; z = u.Z;
                    radius = 0.35f;
                    return true;
                }

                case TargetKind.Base:
                {
                    PlayerState p = s.GetPlayer((byte)t.Id);
                    if (p == null || p.BaseHealth <= 0f) { return false; }
                    CenterOfBase(s, p, out x, out z);
                    radius = s.Grid.CellSize * 0.5f;
                    return true;
                }
            }

            return false;
        }

        private void ApplySplash(GameState s, UnitInstance attacker, float x, float z, UnitSpec spec, float damage)
        {
            for (int i = 0; i < s.Units.Count; i++)
            {
                UnitInstance u = s.Units[i];
                if (!u.Alive || u.OwnerId == attacker.OwnerId) { continue; }
                float d = u.DistanceTo(x, z);
                if (d <= spec.SplashRadius)
                {
                    u.Health -= damage * Falloff(d, spec.SplashRadius);
                    if (u.Health <= 0f) { u.Alive = false; }
                }
            }

            for (int i = 0; i < s.Buildings.Count; i++)
            {
                BuildingInstance b = s.Buildings[i];
                if (b.OwnerId == attacker.OwnerId || b.Health <= 0f) { continue; }
                float bx, bz;
                CenterOfBuilding(s, b, out bx, out bz);
                float d = Dist(bx, bz, x, z);
                if (d <= spec.SplashRadius)
                {
                    DealDamageToBuilding(b, damage * spec.StructureDamageMultiplier * Falloff(d, spec.SplashRadius));
                }
            }

            for (int i = 0; i < s.Players.Count; i++)
            {
                PlayerState p = s.Players[i];
                if (p.Id == attacker.OwnerId || p.BaseHealth <= 0f) { continue; }
                float bx, bz;
                CenterOfBase(s, p, out bx, out bz);
                if (Dist(bx, bz, x, z) <= spec.SplashRadius)
                {
                    p.BaseHealth -= damage * spec.StructureDamageMultiplier;
                }
            }
        }

        private void ApplyDamage(GameState s, TargetRef t, float damage)
        {
            switch (t.Kind)
            {
                case TargetKind.Building:
                {
                    BuildingInstance b = FindBuilding(s, t.Id);
                    if (b != null) { DealDamageToBuilding(b, damage); }
                    break;
                }

                case TargetKind.Unit:
                {
                    UnitInstance u = FindUnit(s, t.Id);
                    if (u != null && u.Alive)
                    {
                        u.Health -= damage;
                        if (u.Health <= 0f) { u.Alive = false; }
                    }
                    break;
                }

                case TargetKind.Base:
                {
                    PlayerState p = s.GetPlayer((byte)t.Id);
                    if (p != null) { p.BaseHealth -= damage; }
                    break;
                }
            }
        }

        // ---------- 伤害 ----------

        private static float Compute(float baseDamage, DamageType type, ArmorType armor)
        {
            return baseDamage * DamageMatrix.Multiplier(type, armor);
        }

        private ArmorType ArmorOfUnitSpec(string specId)
        {
            if (!string.IsNullOrEmpty(specId) && _units.TryGet(specId, out UnitSpec spec))
            {
                return spec.Armor;
            }

            return ArmorType.Light;
        }

        /// <summary>建筑与基地一律按 Structure 处理，单位按其 Spec 的护甲。</summary>
        private ArmorType ArmorOfTarget(GameState s, TargetRef t)
        {
            if (t.Kind == TargetKind.Unit)
            {
                UnitInstance u = FindUnit(s, t.Id);
                if (u != null)
                {
                    return ArmorOfUnitSpec(u.SpecId);
                }
            }

            return ArmorType.Structure;
        }

        // ---------- 清理 ----------

        private void Cleanup(GameState s)
        {
            for (int i = s.Units.Count - 1; i >= 0; i--)
            {
                UnitInstance u = s.Units[i];
                if (u.Alive)
                {
                    continue;
                }

                // 归还出兵槽位。不还的话，打几波之后玩家会永久失去出兵能力。
                if (_units != null && _units.TryGet(u.SpecId, out UnitSpec spec))
                {
                    PlayerState owner = s.GetPlayer(u.OwnerId);
                    if (owner != null)
                    {
                        owner.UsedUnitSlots -= spec.SupplyCost;
                        if (owner.UsedUnitSlots < 0)
                        {
                            owner.UsedUnitSlots = 0;
                        }
                    }
                }

                s.Units.RemoveAt(i);
            }

            for (int i = s.Buildings.Count - 1; i >= 0; i--)
            {
                BuildingInstance b = s.Buildings[i];
                if (b.Health > 0f)
                {
                    continue;
                }

                s.Grid.Release(b.Origin, b.FootprintX, b.FootprintZ, b.InstanceId);
                s.Buildings.RemoveAt(i);
            }
        }

        // ---------- 工具 ----------

        /// <summary>
        /// 沿 A* 路径移动。每 0.5s 重算一次（建筑增减会改变可走性）。
        /// 路径为空时退回直线移动，保证「找不到路也至少朝目标走」。
        /// </summary>
        private void FollowPath(GameState s, UnitInstance u, UnitSpec spec, float tx, float tz, float dt)
        {
            if (u.RepathTimer <= 0f || u.Path == null || u.PathIndex >= u.Path.Length)
            {
                RebuildPath(s, u, tx, tz);
                u.RepathTimer = 0.5f;
            }

            u.RepathTimer -= dt;

            if (u.Path == null || u.Path.Length == 0)
            {
                MoveTowards(u, spec, tx, tz, dt);
                return;
            }

            GridPos cell = u.Path[u.PathIndex];
            float wx = s.Grid.CellCenterX(cell.X);
            float wz = s.Grid.CellCenterZ(cell.Z);

            if (Dist(u.X, u.Z, wx, wz) < 0.35f)
            {
                u.PathIndex++;

                if (u.PathIndex >= u.Path.Length)
                {
                    u.Path = null;
                    MoveTowards(u, spec, tx, tz, dt);
                    return;
                }

                cell = u.Path[u.PathIndex];
                wx = s.Grid.CellCenterX(cell.X);
                wz = s.Grid.CellCenterZ(cell.Z);
            }

            MoveTowards(u, spec, wx, wz, dt);
        }

        private void RebuildPath(GameState s, UnitInstance u, float tx, float tz)
        {
            GridPos start = s.Grid.WorldToCell(u.X, u.Z);
            GridPos goal = s.Grid.WorldToCell(tx, tz);

            if (_pathFinder.TryFindPath(s.Grid, start, goal, _pathBuffer) && _pathBuffer.Count > 0)
            {
                u.Path = _pathBuffer.ToArray();
                u.PathIndex = 0;
            }
            else
            {
                u.Path = null;
                u.PathIndex = 0;
            }
        }

        /// <summary>
        /// 单位分离。O(n^2) 在几十个单位的规模下完全可接受。
        /// 完全重合时按索引奇偶决定方向，而不是随机 —— 随机会破坏可重放性。
        /// </summary>
        private void SeparateUnits(GameState s)
        {
            for (int i = 0; i < s.Units.Count; i++)
            {
                UnitInstance a = s.Units[i];
                if (!a.Alive) { continue; }

                for (int j = i + 1; j < s.Units.Count; j++)
                {
                    UnitInstance b = s.Units[j];
                    if (!b.Alive) { continue; }

                    float minDist = RadiusOf(a) + RadiusOf(b);
                    float dx = b.X - a.X;
                    float dz = b.Z - a.Z;
                    float d2 = dx * dx + dz * dz;

                    if (d2 >= minDist * minDist)
                    {
                        continue;
                    }

                    if (d2 > 0.000001f)
                    {
                        float d = (float)Math.Sqrt(d2);
                        float push = (minDist - d) * 0.5f;
                        float nx = dx / d;
                        float nz = dz / d;
                        a.X -= nx * push;
                        a.Z -= nz * push;
                        b.X += nx * push;
                        b.Z += nz * push;
                    }
                    else
                    {
                        // 完全重合：沿 X 轴对称推开一个完整间距。
                        // 一次推到 minDist，否则每帧只挪 0.05，要十几帧才分开。
                        float dir = ((i + j) & 1) == 0 ? 1f : -1f;
                        a.X -= minDist * 0.5f * dir;
                        b.X += minDist * 0.5f * dir;
                    }
                }
            }
        }

        private float RadiusOf(UnitInstance u)
        {
            return _units.TryGet(u.SpecId, out UnitSpec spec) ? spec.BodyRadius : 0.35f;
        }

        private static void MoveTowards(UnitInstance u, UnitSpec spec, float tx, float tz, float dt)
        {
            float dx = tx - u.X;
            float dz = tz - u.Z;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < 0.0001f)
            {
                return;
            }

            float step = spec.MoveSpeed * u.SlowFactor * dt;
            if (step > d) { step = d; }

            u.X += dx / d * step;
            u.Z += dz / d * step;
        }

        private static void CenterOfBuilding(GameState s, BuildingInstance b, out float x, out float z)
        {
            RectXZ r = s.Grid.AreaToRect(b.Origin, b.FootprintX, b.FootprintZ);
            x = (r.MinX + r.MaxX) * 0.5f;
            z = (r.MinZ + r.MaxZ) * 0.5f;
        }

        private static void CenterOfBase(GameState s, PlayerState p, out float x, out float z)
        {
            x = s.Grid.CellCenterX(p.FriendlyBaseCell.X);
            z = s.Grid.CellCenterZ(p.FriendlyBaseCell.Z);
        }

        /// <summary>溅射衰减：中心 100%，边缘 50%。全额溅射会让 AOE 塔的实际价值远超纸面 DPS。</summary>
        private static float Falloff(float distance, float radius)
        {
            if (radius <= 0f) { return 1f; }
            float t = distance / radius;
            if (t > 1f) { t = 1f; }
            return 1f - t * 0.5f;
        }

        private static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static BuildingInstance FindBuilding(GameState s, int id)
        {
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                if (s.Buildings[i].InstanceId == id) { return s.Buildings[i]; }
            }
            return null;
        }

        private static UnitInstance FindUnit(GameState s, int id)
        {
            for (int i = 0; i < s.Units.Count; i++)
            {
                if (s.Units[i].InstanceId == id) { return s.Units[i]; }
            }
            return null;
        }
    }
}