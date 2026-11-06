#if UNITY_EDITOR
using System;
using DefenseCounter.Data;
using DefenseCounter.Logic.AI;
using DefenseCounter.Logic.Combat;
using DefenseCounter.Logic.Reference;
using DefenseCounter.Logic.Skill;
using UnityEditor;
using UnityEngine;

namespace DefenseCounter.Core
{
    /// <summary>
    /// 编辑器内的资产生成入口。
    /// 菜单：DefenseCounter / Seed Data Assets
    ///
    /// 整份文件都在 #if UNITY_EDITOR 内 —— 本机（无 UnityEditor.dll）编译时它等同于空文件，
    /// 所以这一层无法在本机验证。保持它足够薄（只做 CreateAsset + 调用 ConfigSeeder）。
    /// </summary>
    public static class ConfigSeederEditor
    {
        private const string Root = "Assets/Data";

        [MenuItem("DefenseCounter/Seed Data Assets")]
        public static void SeedAllMenu()
        {
            GameDatabase db = SeedAll(Root);
            if (db != null)
            {
                Selection.activeObject = db;
                EditorGUIUtility.PingObject(db);
            }
        }

        public static GameDatabase SeedAll(string root)
        {
            EnsureFolder(root);
            EnsureFolder(root + "/Units");
            EnsureFolder(root + "/Buildings");
            EnsureFolder(root + "/Skills");
            EnsureFolder(root + "/Loadouts");
            EnsureFolder(root + "/Levels");
            EnsureFolder(root + "/AI");

            GameDatabase db = GetOrCreate<GameDatabase>(root + "/GameDatabase.asset");
            db.buildings.Clear();
            db.units.Clear();
            db.skills.Clear();
            db.loadouts.Clear();
            db.levels.Clear();
            db.difficulties.Clear();

            // ---------- 兵种 ----------
            DictionaryUnitCatalog unitCatalog = ReferenceDatabase.CreateUnits();
            for (int i = 0; i < ReferenceDatabase.AllUnitIds.Length; i++)
            {
                string id = ReferenceDatabase.AllUnitIds[i];
                if (!unitCatalog.TryGet(id, out UnitSpec spec))
                {
                    continue;
                }

                var asset = GetOrCreate<UnitConfig>(root + "/Units/Unit_" + id + ".asset");
                ConfigSeeder.Apply(asset, spec);
                EditorUtility.SetDirty(asset);
                db.units.Add(asset);
            }

            // ---------- 建筑 ----------
            DictionaryBuildingCatalog buildingCatalog = ReferenceDatabase.CreateBuildings();
            for (int i = 0; i < ReferenceDatabase.AllBuildingIds.Length; i++)
            {
                string id = ReferenceDatabase.AllBuildingIds[i];
                if (!buildingCatalog.TryGet(id, out BuildingSpec spec))
                {
                    continue;
                }

                var asset = GetOrCreate<BuildingConfig>(root + "/Buildings/Bld_" + id + ".asset");
                ConfigSeeder.Apply(asset, spec);
                EditorUtility.SetDirty(asset);
                db.buildings.Add(asset);
            }

            // ---------- 技能 ----------
            for (int i = 0; i < ReferenceDatabase.AllSkillIds.Length; i++)
            {
                string id = ReferenceDatabase.AllSkillIds[i];
                var asset = GetOrCreate<SkillConfig>(root + "/Skills/Skill_" + id + ".asset");
                ConfigSeeder.Apply(asset, DefaultSkillSpec(id));
                EditorUtility.SetDirty(asset);
                db.skills.Add(asset);
            }

            // ---------- 阵容 ----------
            ReferenceLoadout iron = ReferenceDatabase.IronBulwark();
            ReferenceLoadout blitz = ReferenceDatabase.RapidCounter();
            AddLoadout(db, root, iron);
            AddLoadout(db, root, blitz);

            // ---------- 关卡 ----------
            var level = GetOrCreate<LevelConfig>(root + "/Levels/Level_01.asset");
            ConfigSeeder.Apply(level, ReferenceDatabase.CreateLevel());
            EditorUtility.SetDirty(level);
            db.levels.Add(level);

            // ---------- 难度 ----------
            AIDifficultySpec[] diffs = ReferenceDatabase.CreateDifficulties();
            for (int i = 0; i < diffs.Length; i++)
            {
                var asset = GetOrCreate<AIDifficultyConfig>(root + "/AI/AIDiff_" + diffs[i].Id + ".asset");
                ConfigSeeder.Apply(asset, diffs[i]);
                EditorUtility.SetDirty(asset);
                db.difficulties.Add(asset);
            }

            db.defaultLoadoutId = iron.Id;
            db.defaultLevelId = level.id;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var problems = db.ValidateAll();
            if (problems.Count == 0)
            {
                Debug.Log("[Seeder] 完成：" + db.buildings.Count + " 建筑 / "
                          + db.units.Count + " 兵种 / " + db.skills.Count + " 技能 / "
                          + db.loadouts.Count + " 阵容 / " + db.difficulties.Count + " 难度，校验通过");
            }
            else
            {
                foreach (string p in problems)
                {
                    Debug.LogError("[Seeder] " + p);
                }
            }

            return db;
        }

        private static void AddLoadout(GameDatabase db, string root, ReferenceLoadout loadout)
        {
            var asset = GetOrCreate<LoadoutConfig>(root + "/Loadouts/Loadout_" + loadout.Id + ".asset");
            ConfigSeeder.Apply(asset, loadout);
            EditorUtility.SetDirty(asset);
            db.loadouts.Add(asset);
        }

        /// <summary>
        /// 两个技能的完整 Spec。ReferenceDatabase 里没有技能清单（技能不影响 AI vs AI 战报），
        /// 所以这里按数值表补齐。
        /// </summary>
        private static SkillSpec DefaultSkillSpec(string id)
        {
            if (id == "skill_orbital_strike")
            {
                return new SkillSpec
                {
                    Id = id,
                    EnergyCost = 100,
                    Cooldown = 60f,
                    CastTarget = SkillCastTarget.GroundPoint,
                    Radius = 8f,
                    Delay = 2.5f,
                    Effect = SkillEffect.Damage,
                    DamageType = DamageType.Kinetic,
                    Damage = 400f
                };
            }

            return new SkillSpec
            {
                Id = "skill_emp_storm",
                EnergyCost = 80,
                Cooldown = 45f,
                CastTarget = SkillCastTarget.GroundPoint,
                Radius = 12f,
                Delay = 0f,
                Effect = SkillEffect.Emp,
                DamageType = DamageType.Energy,
                Damage = 80f,
                Duration = 3f
            };
        }

        // ---------- 资产工具 ----------

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int idx = path.LastIndexOf('/');
            if (idx <= 0)
            {
                return;
            }

            string parent = path.Substring(0, idx);
            string leaf = path.Substring(idx + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif