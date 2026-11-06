using DefenseCounter.Data;

namespace DefenseCounter.Logic
{
    /// <summary>建造被拒绝的原因。顺序即校验顺序，UI 只需显示第一个。</summary>
    public enum PlacementFailure
    {
        None = 0,
        InvalidRequest = 1,        // 配置或参数非法
        NotInLoadout = 2,          // 阵容不允许该建筑
        NotEnoughGold = 3,         // 金币不足
        OutOfGrid = 4,             // 超出网格边界
        OutsideBuildableArea = 5,  // 不在可建造矩形内
        OutsideBuildRadius = 6,    // 不在任何建造中心半径内
        TerrainNotAllowed = 7,     // 地形不允许（道路/障碍/水面/悬崖/基地台座）
        CellOccupied = 8,          // 与其他建筑重叠
        BlocksCriticalPath = 9     // 会封死敌方进攻路径
    }

    /// <summary>一次建造请求的纯数据快照（不含 Unity 类型，便于测试与联机传输）。</summary>
    public struct PlacementRequest
    {
        public string BuildingId;
        public int FootprintX;
        public int FootprintZ;
        public int BuildCost;
        public float BuildRadiusContribution;
        public TerrainMask AllowedTerrain;

        /// <summary>已吸附到格的原点（占地区域的左下格）。</summary>
        public GridPos Origin;
    }

    /// <summary>校验结果。</summary>
    public struct PlacementResult
    {
        public bool Ok;
        public PlacementFailure Failure;
        public string Message;
        public GridPos Origin;

        public static PlacementResult Success(GridPos origin)
        {
            return new PlacementResult
            {
                Ok = true,
                Failure = PlacementFailure.None,
                Message = string.Empty,
                Origin = origin
            };
        }

        public static PlacementResult Reject(PlacementFailure failure, string message, GridPos origin)
        {
            return new PlacementResult
            {
                Ok = false,
                Failure = failure,
                Message = message,
                Origin = origin
            };
        }

        public override string ToString()
        {
            return Ok ? ("OK " + Origin) : (Failure + ": " + Message);
        }
    }

    /// <summary>
    /// 校验所需的上下文。由 Host 侧填充；
    /// 客户端只发请求，不做校验（Host 权威）。
    /// </summary>
    public sealed class PlacementContext
    {
        public int Gold;

        public string[] AllowedBuildingIds;

        /// <summary>可建造矩形区域（通常 1 个）。</summary>
        public RectXZ[] BuildableAreas;

        /// <summary>己方建造中心（基地）列表。建筑必须落在其中一个的半径内。</summary>
        public BaseCircle[] BuildCenters;

        /// <summary>敌方出兵起点。用于"是否会封死通路"的检查。</summary>
        public GridPos[] EnemySpawns;

        /// <summary>己方基地所在格。作为路径检查的目标。</summary>
        public GridPos FriendlyBaseCell;

        /// <summary>关闭路径检查（用于性能敏感或调试场景）。</summary>
        public bool CheckPathBlocking = true;

        public bool IsBuildingAllowed(string buildingId)
        {
            if (AllowedBuildingIds == null)
            {
                return false;
            }

            for (int i = 0; i < AllowedBuildingIds.Length; i++)
            {
                if (AllowedBuildingIds[i] == buildingId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}