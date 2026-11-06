using System;
using System.Collections.Generic;
using DefenseCounter.Data;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.Skill
{
    /// <summary>
    /// 技能执行器。六种效果全部在这里实现，不依赖 UnityEngine。
    ///
    /// 架构约定：
    /// - 能量与冷却的扣除发生在「施放」瞬间，效果发生「延迟」之后。
    ///   这样玩家按下按钮就付出代价，不会因为中途被打断而白嫖。
    /// - 延迟技能进入 _pending，由 Tick 推进，保证可重放。
    /// </summary>
    public sealed class SkillExecutor
    {
        private readonly ISkillCatalog _skills;
        private readonly IUnitCatalog _units;
        private readonly List<PendingSkill> _pending = new List<PendingSkill>();
        private readonly List<PendingSkill> _expired = new List<PendingSkill>();

        /// <summary>减速技能的固定倍率（配置里没有单独字段，先统一 0.5）。</summary>
        public const float SlowFactorValue = 0.5f;

        public SkillExecutor(ISkillCatalog skills, IUnitCatalog units)
        {
            _skills = skills;
            _units = units;
        }

        public int PendingCount => _pending.Count;

        /// <summary>施放技能。成功返回 null，失败返回原因。</summary>
        public string TryCast(GameState state, byte casterId, string skillId, float x, float z)
        {
            if (state == null)
            {
                return "状态为空";
            }

            if (!_skills.TryGet(skillId, out SkillSpec spec))
            {
                return "未知技能: " + skillId;
            }

            PlayerState caster = state.GetPlayer(casterId);
            if (caster == null)
            {
                return "施法者不存在";
            }

            if (caster.Energy < spec.EnergyCost)
            {
                return "能量不足: 需要 " + spec.EnergyCost + "，当前 " + caster.Energy;
            }

            if (caster.GetCooldown(skillId) > 0f)
            {
                return "冷却中: 剩余 " + caster.GetCooldown(skillId).ToString("0.0") + "s";
            }

            if (spec.RequiresBuilding && !HasBuilding(state, casterId, spec.RequiredBuildingId))
            {
                return "缺少建筑: " + spec.RequiredBuildingId;
            }

            caster.Energy -= spec.EnergyCost;
            caster.SetCooldown(skillId, spec.Cooldown);

            if (spec.Delay > 0f)
            {
                _pending.Add(new PendingSkill
                {
                    SkillId = skillId,
                    CasterId = casterId,
                    X = x,
                    Z = z,
                    ResolveTime = state.Now + spec.Delay
                });
            }
            else
            {
                Resolve(state, casterId, skillId, x, z);
            }

            return null;
        }

        /// <summary>推进延迟技能。由 CombatSystem 或 GameLoop 每帧调用。</summary>
        public void Tick(GameState state)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            _expired.Clear();
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].ResolveTime <= state.Now)
                {
                    _expired.Add(_pending[i]);
                }
            }

            for (int i = 0; i < _expired.Count; i++)
            {
                PendingSkill p = _expired[i];
                _pending.Remove(p);
                Resolve(state, p.CasterId, p.SkillId, p.X, p.Z);
            }
        }

        // ---------- 效果分发 ----------

        private void Resolve(GameState state, byte casterId, string skillId, float x, float z)
        {
            if (!_skills.TryGet(skillId, out SkillSpec spec))
            {
                return;
            }

            switch (spec.Effect)
            {
                case SkillEffect.Damage:
                    ApplyDamage(state, casterId, spec, x, z);
                    break;

                case SkillEffect.Emp:
                    ApplyStun(state, casterId, spec, x, z);
                    break;

                case SkillEffect.Slow:
                    ApplySlow(state, casterId, spec, x, z);
                    break;

                case SkillEffect.Summon:
                    ApplySummon(state, casterId, spec, x, z);
                    break;

                case SkillEffect.Teleport:
                    ApplyTeleport(state, casterId, spec, x, z);
                    break;

                case SkillEffect.Hack:
                    ApplyHack(state, casterId, spec, x, z);
                    break;
            }
        }

        // ---------- 具体效果 ----------

        private void ApplyDamage(GameState state, byte casterId, SkillSpec spec, float x, float z)
        {
            float radius = spec.Radius;

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                if (!u.Alive || u.OwnerId == casterId)
                {
                    continue;
                }

                float d = Dist(u.X, u.Z, x, z);
                if (d > radius)
                {
                    continue;
                }

                ArmorType armor = ArmorOf(state, u);
                u.Health -= spec.Damage * DamageMatrix.Multiplier(spec.DamageType, armor) * Falloff(d, radius);
                if (u.Health <= 0f)
                {
                    u.Alive = false;
                }
            }

            for (int i = 0; i < state.Buildings.Count; i++)
            {
                BuildingInstance b = state.Buildings[i];
                if (b.OwnerId == casterId || b.Health <= 0f)
                {
                    continue;
                }

                float bx, bz;
                CenterOfBuilding(state, b, out bx, out bz);
                float d = Dist(bx, bz, x, z);
                if (d > radius)
                {
                    continue;
                }

                float dmg = spec.Damage * DamageMatrix.Multiplier(spec.DamageType, ArmorType.Structure)
                            * Falloff(d, radius);
                DealDamageToBuilding(b, dmg);
            }

            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState p = state.Players[i];
                if (p.Id == casterId || p.BaseHealth <= 0f)
                {
                    continue;
                }

                float bx = state.Grid.CellCenterX(p.FriendlyBaseCell.X);
                float bz = state.Grid.CellCenterZ(p.FriendlyBaseCell.Z);
                float d = Dist(bx, bz, x, z);
                if (d > radius)
                {
                    continue;
                }

                p.BaseHealth -= spec.Damage * DamageMatrix.Multiplier(spec.DamageType, ArmorType.Structure)
                                * Falloff(d, radius);
            }
        }

        private void ApplyStun(GameState state, byte casterId, SkillSpec spec, float x, float z)
        {
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                if (!u.Alive || u.OwnerId == casterId)
                {
                    continue;
                }

                if (Dist(u.X, u.Z, x, z) > spec.Radius)
                {
                    continue;
                }

                if (spec.Damage > 0f)
                {
                    ArmorType armor = ArmorOf(state, u);
                    u.Health -= spec.Damage * DamageMatrix.Multiplier(spec.DamageType, armor);
                    if (u.Health <= 0f)
                    {
                        u.Alive = false;
                        continue;
                    }
                }

                if (spec.Duration > u.StunTimer)
                {
                    u.StunTimer = spec.Duration;
                }
            }
        }

        private void ApplySlow(GameState state, byte casterId, SkillSpec spec, float x, float z)
        {
            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                if (!u.Alive || u.OwnerId == casterId)
                {
                    continue;
                }

                if (Dist(u.X, u.Z, x, z) > spec.Radius)
                {
                    continue;
                }

                if (spec.Duration > u.SlowTimer)
                {
                    u.SlowTimer = spec.Duration;
                }

                if (SlowFactorValue < u.SlowFactor)
                {
                    u.SlowFactor = SlowFactorValue;
                }
            }
        }

        private void ApplySummon(GameState state, byte casterId, SkillSpec spec, float x, float z)
        {
            PlayerState caster = state.GetPlayer(casterId);
            if (caster == null || !_units.TryGet(spec.SummonUnitId, out UnitSpec us))
            {
                return;
            }

            int freeSlots = caster.MaxUnitSlots - caster.UsedUnitSlots;
            int count = Math.Min(spec.SummonCount, freeSlots / Math.Max(1, us.SupplyCost));

            for (int i = 0; i < count; i++)
            {
                float angle = (float)(Math.PI * 2.0 * i / Math.Max(1, count));
                state.Units.Add(new UnitInstance
                {
                    InstanceId = state.AllocateInstanceId(),
                    SpecId = us.Id,
                    OwnerId = casterId,
                    X = x + (float)Math.Cos(angle) * 1.5f,
                    Z = z + (float)Math.Sin(angle) * 1.5f,
                    Health = us.MaxHealth
                });
                caster.UsedUnitSlots += us.SupplyCost;
            }
        }

        private void ApplyTeleport(GameState state, byte casterId, SkillSpec spec, float x, float z)
        {
            int moved = 0;

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                if (!u.Alive || u.OwnerId != casterId)
                {
                    continue;
                }

                float angle = (float)(Math.PI * 2.0 * moved / 8.0);
                u.X = x + (float)Math.Cos(angle) * 1.5f;
                u.Z = z + (float)Math.Sin(angle) * 1.5f;
                u.Target = TargetRef.None;
                u.Path = null;
                u.PathIndex = 0;
                u.RepathTimer = 0f;
                moved++;
            }
        }

        private void ApplyHack(GameState state, byte casterId, SkillSpec spec, float x, float z)
        {
            PlayerState caster = state.GetPlayer(casterId);
            if (caster == null)
            {
                return;
            }

            for (int i = 0; i < state.Units.Count; i++)
            {
                UnitInstance u = state.Units[i];
                if (!u.Alive || u.OwnerId == casterId)
                {
                    continue;
                }

                if (Dist(u.X, u.Z, x, z) > spec.Radius)
                {
                    continue;
                }

                if (!_units.TryGet(u.SpecId, out UnitSpec us))
                {
                    continue;
                }

                // 槽位不够就跳过，不做「超编」这种后门
                if (caster.UsedUnitSlots + us.SupplyCost > caster.MaxUnitSlots)
                {
                    continue;
                }

                PlayerState oldOwner = state.GetPlayer(u.OwnerId);
                if (oldOwner != null)
                {
                    oldOwner.UsedUnitSlots -= us.SupplyCost;
                    if (oldOwner.UsedUnitSlots < 0)
                    {
                        oldOwner.UsedUnitSlots = 0;
                    }
                }

                caster.UsedUnitSlots += us.SupplyCost;
                u.OwnerId = casterId;
                u.Target = TargetRef.None;
                u.Path = null;
                u.PathIndex = 0;
                u.RepathTimer = 0f;
            }
        }

        // ---------- 工具 ----------

        private static bool HasBuilding(GameState state, byte owner, string buildingId)
        {
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                BuildingInstance b = state.Buildings[i];
                if (b.OwnerId == owner && b.Health > 0f && b.ConfigId == buildingId)
                {
                    return true;
                }
            }

            return false;
        }

        private ArmorType ArmorOf(GameState state, UnitInstance u)
        {
            return _units.TryGet(u.SpecId, out UnitSpec spec) ? spec.Armor : ArmorType.Light;
        }

        private static void DealDamageToBuilding(BuildingInstance b, float damage)
        {
            float absorbed = damage < b.Shield ? damage : b.Shield;
            b.Shield -= absorbed;
            b.Health -= damage - absorbed;
        }

        private static void CenterOfBuilding(GameState state, BuildingInstance b, out float x, out float z)
        {
            RectXZ r = state.Grid.AreaToRect(b.Origin, b.FootprintX, b.FootprintZ);
            x = (r.MinX + r.MaxX) * 0.5f;
            z = (r.MinZ + r.MaxZ) * 0.5f;
        }

        private static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float Falloff(float distance, float radius)
        {
            if (radius <= 0f)
            {
                return 1f;
            }

            float t = distance / radius;
            if (t > 1f) { t = 1f; }
            return 1f - t * 0.5f;
        }
    }
}