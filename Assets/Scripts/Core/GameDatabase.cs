using System.Collections.Generic;
using DefenseCounter.Data;
using UnityEngine;

namespace DefenseCounter.Core
{
    /// <summary>
    /// 全局配置数据库。所有 Data/*.asset 在这里汇总，运行时按 id 取。
    /// 联机只同步 id，数值表由客户端本地同一份配置还原。
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "DefenseCounter/Game Database", order = 0)]
    public sealed class GameDatabase : ScriptableObject
    {
        [Header("配置表")]
        public List<BuildingConfig> buildings = new List<BuildingConfig>();

        public List<UnitConfig> units = new List<UnitConfig>();

        public List<SkillConfig> skills = new List<SkillConfig>();

        public List<LoadoutConfig> loadouts = new List<LoadoutConfig>();

        public List<LevelConfig> levels = new List<LevelConfig>();

        public List<AIDifficultyConfig> difficulties = new List<AIDifficultyConfig>();

        [Header("默认项")]
        [Tooltip("主菜单默认选中的阵容")]
        public string defaultLoadoutId = "loadout_iron";

        [Tooltip("主菜单默认选中的关卡")]
        public string defaultLevelId = "level_01";

        private Dictionary<string, BuildingConfig> _buildingMap;
        private Dictionary<string, UnitConfig> _unitMap;
        private Dictionary<string, SkillConfig> _skillMap;
        private Dictionary<string, LoadoutConfig> _loadoutMap;
        private Dictionary<string, LevelConfig> _levelMap;
        private Dictionary<string, AIDifficultyConfig> _difficultyMap;

        private bool _indexed;

        // ---------- 索引 ----------

        public void RebuildIndex()
        {
            _buildingMap = BuildMap(buildings, b => b != null ? b.id : null);
            _unitMap = BuildMap(units, u => u != null ? u.id : null);
            _skillMap = BuildMap(skills, s => s != null ? s.id : null);
            _loadoutMap = BuildMap(loadouts, l => l != null ? l.id : null);
            _levelMap = BuildMap(levels, l => l != null ? l.id : null);
            _difficultyMap = BuildMap(difficulties, d => d != null ? d.id : null);
            _indexed = true;
        }

        private static Dictionary<string, T> BuildMap<T>(List<T> list, System.Func<T, string> idSelector)
            where T : class
        {
            var map = new Dictionary<string, T>(list != null ? list.Count : 8);
            if (list == null)
            {
                return map;
            }

            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                if (item == null)
                {
                    continue;
                }

                string key = idSelector(item);
                if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                {
                    map.Add(key, item);
                }
            }

            return map;
        }

        private void EnsureIndexed()
        {
            if (!_indexed)
            {
                RebuildIndex();
            }
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

        // ---------- 查询 ----------

        public BuildingConfig GetBuilding(string id)
        {
            return Lookup(id, ref _indexed, () => RebuildIndex(), _buildingMap);
        }

        public UnitConfig GetUnit(string id)
        {
            return Lookup(id, ref _indexed, () => RebuildIndex(), _unitMap);
        }

        public SkillConfig GetSkill(string id)
        {
            return Lookup(id, ref _indexed, () => RebuildIndex(), _skillMap);
        }

        public LoadoutConfig GetLoadout(string id)
        {
            return Lookup(id, ref _indexed, () => RebuildIndex(), _loadoutMap);
        }

        public LevelConfig GetLevel(string id)
        {
            return Lookup(id, ref _indexed, () => RebuildIndex(), _levelMap);
        }

        public AIDifficultyConfig GetDifficulty(string id)
        {
            return Lookup(id, ref _indexed, () => RebuildIndex(), _difficultyMap);
        }

        private T Lookup<T>(string id, ref bool indexed, System.Action rebuild, Dictionary<string, T> map)
            where T : class
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (!indexed)
            {
                rebuild();
            }

            if (map == null)
            {
                return null;
            }

            T value;
            return map.TryGetValue(id, out value) ? value : null;
        }

        /// <summary>全量校验。返回空列表 = 配置无结构性问题。</summary>
        public List<string> ValidateAll()
        {
            return DatabaseValidator.Validate(this);
        }
    }
}