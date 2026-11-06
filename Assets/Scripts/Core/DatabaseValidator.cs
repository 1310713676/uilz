using System.Collections.Generic;
using DefenseCounter.Data;
using UnityEngine;

namespace DefenseCounter.Core
{
    /// <summary>
    /// 配置结构校验。只检查「结构是否完整、引用是否断链」，
    /// 不检查数值是否平衡（那属于策划职责）。
    /// 在编辑器里应由 Editor 脚本在每次保存/构建前调用一次。
    /// </summary>
    public static class DatabaseValidator
    {
        public static List<string> Validate(GameDatabase db)
        {
            var problems = new List<string>();

            if (db == null)
            {
                problems.Add("[DB] GameDatabase 为空");
                return problems;
            }

            ValidateBuildings(db, problems);
            ValidateUnits(db, problems);
            ValidateSkills(db, problems);
            ValidateLoadouts(db, problems);
            ValidateLevels(db, problems);
            ValidateDifficulties(db, problems);
            ValidateDefaults(db, problems);

            return problems;
        }

        // ---------- 建筑 ----------

        private static void ValidateBuildings(GameDatabase db, List<string> p)
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < db.buildings.Count; i++)
            {
                BuildingConfig b = db.buildings[i];
                string slot = "buildings[" + i + "]";

                if (b == null)
                {
                    p.Add(slot + " 为空引用");
                    continue;
                }

                string tag = slot + "(" + b.name + ")";

                if (string.IsNullOrEmpty(b.id))
                {
                    p.Add(tag + " id 为空");
                }
                else if (!seen.Add(b.id))
                {
                    p.Add(tag + " id 重复: " + b.id);
                }

                if (b.maxLevel < 1 || b.maxLevel > 3)
                {
                    p.Add(tag + " maxLevel 必须在 1~3，当前 " + b.maxLevel);
                }

                CheckArrayLength(p, tag + " maxHealthByLevel", b.maxHealthByLevel, b.maxLevel);
                CheckArrayLength(p, tag + " buildTimeByLevel", b.buildTimeByLevel, b.maxLevel);

                if (b.footprint.x < 1 || b.footprint.y < 1)
                {
                    p.Add(tag + " footprint 必须 >= 1x1");
                }

                if (b.buildCost <= 0)
                {
                    p.Add(tag + " buildCost 必须 > 0");
                }

                if (b.combat.enabled)
                {
                    CheckArrayLength(p, tag + " combat.damage", b.combat.damage, b.maxLevel);
                    CheckArrayLength(p, tag + " combat.interval", b.combat.interval, b.maxLevel);
                    CheckArrayLength(p, tag + " combat.range", b.combat.range, b.maxLevel);

                    if (b.combat.SplashAt(1) < 0f)
                    {
                        p.Add(tag + " combat.splashRadius 不得为负");
                    }
                }

                if (b.support.enabled)
                {
                    CheckArrayLength(p, tag + " support.auraRadius", b.support.auraRadius, b.maxLevel);

                    if (b.support.ShieldAt(1) <= 0f && b.support.RepairAt(1) <= 0f)
                    {
                        p.Add(tag + " support 已启用但护盾与修复都为 0");
                    }
                }

                if (b.production.enabled)
                {
                    if (b.production.unlockedUnitIds == null || b.production.unlockedUnitIds.Length == 0)
                    {
                        p.Add(tag + " production 已启用但未解锁任何兵种");
                    }
                    else
                    {
                        for (int u = 0; u < b.production.unlockedUnitIds.Length; u++)
                        {
                            string uid = b.production.unlockedUnitIds[u];
                            if (db.GetUnit(uid) == null)
                            {
                                p.Add(tag + " 解锁的兵种不存在: " + uid);
                            }
                        }
                    }
                }

                if (b.buildingType == BuildingType.Defense && !b.combat.enabled)
                {
                    p.Add(tag + " 是防御建筑但 combat 未启用");
                }
            }
        }

        // ---------- 兵种 ----------

        private static void ValidateUnits(GameDatabase db, List<string> p)
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < db.units.Count; i++)
            {
                UnitConfig u = db.units[i];
                string slot = "units[" + i + "]";

                if (u == null)
                {
                    p.Add(slot + " 为空引用");
                    continue;
                }

                string tag = slot + "(" + u.name + ")";

                if (string.IsNullOrEmpty(u.id))
                {
                    p.Add(tag + " id 为空");
                }
                else if (!seen.Add(u.id))
                {
                    p.Add(tag + " id 重复: " + u.id);
                }

                if (u.maxHealth <= 0f)
                {
                    p.Add(tag + " maxHealth 必须 > 0");
                }

                if (u.moveSpeed <= 0f)
                {
                    p.Add(tag + " moveSpeed 必须 > 0");
                }

                if (u.attackInterval <= 0f)
                {
                    p.Add(tag + " attackInterval 必须 > 0");
                }

                if (u.range <= 0f)
                {
                    p.Add(tag + " range 必须 > 0");
                }

                if (u.acquireRadius < u.range)
                {
                    p.Add(tag + " acquireRadius 小于 range，会导致无法攻击");
                }

                if (u.supplyCost < 1)
                {
                    p.Add(tag + " supplyCost 必须 >= 1");
                }

                if (u.structureDamageMultiplier <= 0f)
                {
                    p.Add(tag + " structureDamageMultiplier 必须 > 0");
                }

                if (u.prefab == null)
                {
                    p.Add(tag + " 未指定 prefab（美术未完成可忽略）");
                }
            }
        }

        // ---------- 技能 ----------

        private static void ValidateSkills(GameDatabase db, List<string> p)
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < db.skills.Count; i++)
            {
                SkillConfig s = db.skills[i];
                string slot = "skills[" + i + "]";

                if (s == null)
                {
                    p.Add(slot + " 为空引用");
                    continue;
                }

                string tag = slot + "(" + s.name + ")";

                if (string.IsNullOrEmpty(s.id))
                {
                    p.Add(tag + " id 为空");
                }
                else if (!seen.Add(s.id))
                {
                    p.Add(tag + " id 重复: " + s.id);
                }

                if (s.energyCost <= 0)
                {
                    p.Add(tag + " energyCost 必须 > 0");
                }

                if (s.cooldown < 0f)
                {
                    p.Add(tag + " cooldown 不得为负");
                }

                if (s.effect == SkillEffect.Damage && s.damage <= 0f)
                {
                    p.Add(tag + " 是伤害型技能但 damage = 0");
                }

                if (s.effect == SkillEffect.Summon)
                {
                    if (db.GetUnit(s.summonUnitId) == null)
                    {
                        p.Add(tag + " 召唤兵种不存在: " + s.summonUnitId);
                    }

                    if (s.summonCount <= 0)
                    {
                        p.Add(tag + " summonCount 必须 > 0");
                    }
                }

                if (!string.IsNullOrEmpty(s.requiredBuildingId) && db.GetBuilding(s.requiredBuildingId) == null)
                {
                    p.Add(tag + " 依赖建筑不存在: " + s.requiredBuildingId);
                }
            }
        }

        // ---------- 阵容 ----------

        private static void ValidateLoadouts(GameDatabase db, List<string> p)
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < db.loadouts.Count; i++)
            {
                LoadoutConfig l = db.loadouts[i];
                string slot = "loadouts[" + i + "]";

                if (l == null)
                {
                    p.Add(slot + " 为空引用");
                    continue;
                }

                string tag = slot + "(" + l.name + ")";

                if (string.IsNullOrEmpty(l.id))
                {
                    p.Add(tag + " id 为空");
                }
                else if (!seen.Add(l.id))
                {
                    p.Add(tag + " id 重复: " + l.id);
                }

                if (l.maxUnitSlots <= 0)
                {
                    p.Add(tag + " maxUnitSlots 必须 > 0");
                }

                CheckRefs(p, tag + " buildingIds", l.buildingIds, id => db.GetBuilding(id) != null);
                CheckRefs(p, tag + " unitIds", l.unitIds, id => db.GetUnit(id) != null);
                CheckRefs(p, tag + " skillIds", l.skillIds, id => db.GetSkill(id) != null);

                if (l.buildingIds == null || l.buildingIds.Length == 0)
                {
                    p.Add(tag + " 阵容未包含任何建筑，无法游玩");
                }

                if (l.unitIds == null || l.unitIds.Length == 0)
                {
                    p.Add(tag + " 阵容未包含任何兵种，无法出兵");
                }
            }
        }

        // ---------- 关卡 ----------

        private static void ValidateLevels(GameDatabase db, List<string> p)
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < db.levels.Count; i++)
            {
                LevelConfig lv = db.levels[i];
                string slot = "levels[" + i + "]";

                if (lv == null)
                {
                    p.Add(slot + " 为空引用");
                    continue;
                }

                string tag = slot + "(" + lv.name + ")";

                if (string.IsNullOrEmpty(lv.id))
                {
                    p.Add(tag + " id 为空");
                }
                else if (!seen.Add(lv.id))
                {
                    p.Add(tag + " id 重复: " + lv.id);
                }

                if (lv.playerBasePositions == null || lv.playerBasePositions.Length == 0)
                {
                    p.Add(tag + " 没有玩家基地位置");
                }

                if (lv.aiBasePositions == null || lv.aiBasePositions.Length == 0)
                {
                    p.Add(tag + " 没有 AI 基地位置");
                }

                if (lv.mapSize.x <= 0f || lv.mapSize.y <= 0f)
                {
                    p.Add(tag + " mapSize 必须 > 0");
                }

                if (lv.buildableAreaSize.x <= 0f || lv.buildableAreaSize.y <= 0f)
                {
                    p.Add(tag + " buildableAreaSize 必须 > 0");
                }

                if (lv.startGold < 0)
                {
                    p.Add(tag + " startGold 不得为负");
                }

                if (lv.goldPerSecond < 0f || lv.energyPerSecond < 0f)
                {
                    p.Add(tag + " 收入速率不得为负");
                }

                if (lv.playerBuildRadius <= 0f || lv.aiBuildRadius <= 0f)
                {
                    p.Add(tag + " 建造半径必须 > 0");
                }

                CheckInsideMap(p, tag, lv);
            }
        }

        private static void CheckInsideMap(List<string> p, string tag, LevelConfig lv)
        {
            float halfX = lv.mapSize.x * 0.5f;
            float halfZ = lv.mapSize.y * 0.5f;

            ReportOutOfBounds(p, tag, "玩家基地", lv.playerBasePositions, halfX, halfZ);
            ReportOutOfBounds(p, tag, "AI 基地", lv.aiBasePositions, halfX, halfZ);
        }

        private static void ReportOutOfBounds(List<string> p, string tag, string label,
            Vector3[] positions, float halfX, float halfZ)
        {
            if (positions == null)
            {
                return;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 v = positions[i];
                if (Mathf.Abs(v.x) > halfX || Mathf.Abs(v.z) > halfZ)
                {
                    p.Add(tag + " " + label + "[" + i + "] 超出地图边界: " + v);
                }
            }
        }

        // ---------- 难度 ----------

        private static void ValidateDifficulties(GameDatabase db, List<string> p)
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < db.difficulties.Count; i++)
            {
                AIDifficultyConfig d = db.difficulties[i];
                string slot = "difficulties[" + i + "]";

                if (d == null)
                {
                    p.Add(slot + " 为空引用");
                    continue;
                }

                string tag = slot + "(" + d.name + ")";

                if (string.IsNullOrEmpty(d.id))
                {
                    p.Add(tag + " id 为空");
                }
                else if (!seen.Add(d.id))
                {
                    p.Add(tag + " id 重复: " + d.id);
                }

                if (d.decisionInterval <= 0f)
                {
                    p.Add(tag + " decisionInterval 必须 > 0");
                }

                if (d.maxConcurrentUnits <= 0)
                {
                    p.Add(tag + " maxConcurrentUnits 必须 > 0");
                }

                if (d.TotalActionWeight <= 0f)
                {
                    p.Add(tag + " 四个行为权重之和为 0，AI 不会做任何事");
                }
            }
        }

        // ---------- 默认项 ----------

        private static void ValidateDefaults(GameDatabase db, List<string> p)
        {
            if (!string.IsNullOrEmpty(db.defaultLoadoutId) && db.GetLoadout(db.defaultLoadoutId) == null)
            {
                p.Add("默认阵容不存在: " + db.defaultLoadoutId);
            }

            if (!string.IsNullOrEmpty(db.defaultLevelId) && db.GetLevel(db.defaultLevelId) == null)
            {
                p.Add("默认关卡不存在: " + db.defaultLevelId);
            }
        }

        // ---------- 工具 ----------

        private static void CheckArrayLength(List<string> p, string label, float[] arr, int required)
        {
            if (arr == null || arr.Length < required)
            {
                p.Add(label + " 长度不足，需要 " + required + "，当前 " + (arr == null ? 0 : arr.Length));
            }
        }

        private static void CheckRefs(List<string> p, string label, string[] ids, System.Func<string, bool> exists)
        {
            if (ids == null)
            {
                return;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                {
                    p.Add(label + "[" + i + "] 为空字符串");
                }
                else if (!exists(ids[i]))
                {
                    p.Add(label + "[" + i + "] 引用不存在: " + ids[i]);
                }
            }
        }
    }
}