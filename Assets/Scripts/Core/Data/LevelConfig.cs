using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// 关卡配置。决定可建造区域、基地位置、初始资源与收入。
    /// 创建：Assets/Data/Levels/ 右键 Create/DefenseCounter/Level
    /// </summary>
    [CreateAssetMenu(fileName = "Level_New", menuName = "DefenseCounter/Level", order = 50)]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("身份")]
        public string id = "level_01";

        public string displayName = "未命名关卡";

        [Tooltip("对应的 Unity 场景名，Battle 场景内按 id 加载配置")]
        public string sceneName = "Battle";

        [Header("地图")]
        [Tooltip("地图尺寸（米，X = 宽，Y = 深）")]
        public Vector2 mapSize = new Vector2(64f, 64f);

        [Tooltip("可建造矩形区域的中心（世界坐标 XZ）")]
        public Vector2 buildableAreaCenter = Vector2.zero;

        [Tooltip("可建造矩形区域的尺寸（米）")]
        public Vector2 buildableAreaSize = new Vector2(56f, 56f);

        [Tooltip("允许建造的地形层")]
        public TerrainMask allowedTerrain = TerrainMask.Ground;

        [Header("基地")]
        [Tooltip("玩家基地位置，数量 = 玩家数（联机最多 4）")]
        public Vector3[] playerBasePositions = new Vector3[1];

        [Tooltip("AI 基地位置")]
        public Vector3[] aiBasePositions = new Vector3[1];

        [Tooltip("玩家基地初始建造半径（米）")]
        public float playerBuildRadius = 26f;

        [Tooltip("AI 基地初始建造半径（米）")]
        public float aiBuildRadius = 26f;

        [Tooltip("基地核心血量")]
        public float baseMaxHealth = 5000f;

        [Header("初始资源")]
        public int startGold = 500;

        public int startEnergy = 0;

        [Tooltip("金币每秒收入")]
        public float goldPerSecond = 8f;

        [Tooltip("能量每秒回复")]
        public float energyPerSecond = 2.5f;

        [Tooltip("单局时长上限（秒）。超时按剩余基地血量判定")]
        public float maxMatchDuration = 900f;

        [Header("障碍")]
        [Tooltip("障碍物预制体，会按其 Collider 阻挡建造与通行")]
        public GameObject[] obstaclePrefabs;

        // ---------- 查询 ----------

        /// <summary>点是否落在可建造矩形内（忽略 Y 轴）。</summary>
        public bool IsInsideBuildableArea(Vector3 worldPos)
        {
            float minX = buildableAreaCenter.x - buildableAreaSize.x * 0.5f;
            float maxX = buildableAreaCenter.x + buildableAreaSize.x * 0.5f;
            float minZ = buildableAreaCenter.y - buildableAreaSize.y * 0.5f;
            float maxZ = buildableAreaCenter.y + buildableAreaSize.y * 0.5f;

            return worldPos.x >= minX && worldPos.x <= maxX
                && worldPos.z >= minZ && worldPos.z <= maxZ;
        }

        /// <summary>点是否在任一己方基地的建造半径内。</summary>
        public bool IsInsideAnyPlayerBuildRadius(Vector3 worldPos)
        {
            return IsInsideAnyRadius(worldPos, playerBasePositions, playerBuildRadius);
        }

        public bool IsInsideAnyAiBuildRadius(Vector3 worldPos)
        {
            return IsInsideAnyRadius(worldPos, aiBasePositions, aiBuildRadius);
        }

        private static bool IsInsideAnyRadius(Vector3 worldPos, Vector3[] centers, float radius)
        {
            if (centers == null)
            {
                return false;
            }

            float sqr = radius * radius;
            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 c = centers[i];
                c.y = worldPos.y;
                if ((worldPos - c).sqrMagnitude <= sqr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}