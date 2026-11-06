using System;
using DefenseCounter.Data;

namespace DefenseCounter.Logic
{
    /// <summary>
    /// 建造网格。记录地形层与建筑占用。
    /// 坐标系统：格 (0,0) 的世界左下角 = (OriginX, OriginZ)，格边长 = CellSize。
    /// 世界坐标 -> 格： floor((world - origin) / cellSize)
    /// 格 -> 格中心： origin + (cell + 0.5) * cellSize
    /// </summary>
    public sealed class PlacementGrid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        public readonly float OriginX;
        public readonly float OriginZ;

        private readonly TerrainMask[] _terrain;
        private readonly int[] _occupant; // 0 = 空

        public PlacementGrid(int width, int height, float cellSize, float originX, float originZ)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "网格尺寸必须为正");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "格边长必须为正");
            }

            Width = width;
            Height = height;
            CellSize = cellSize;
            OriginX = originX;
            OriginZ = originZ;
            _terrain = new TerrainMask[width * height];
            _occupant = new int[width * height];
        }

        public int CellCount => Width * Height;

        // ---------- 坐标转换 ----------

        public bool InBounds(GridPos c)
        {
            return c.X >= 0 && c.X < Width && c.Z >= 0 && c.Z < Height;
        }

        public int WorldToCellX(float wx)
        {
            return (int)Math.Floor((wx - OriginX) / CellSize);
        }

        public int WorldToCellZ(float wz)
        {
            return (int)Math.Floor((wz - OriginZ) / CellSize);
        }

        public GridPos WorldToCell(float wx, float wz)
        {
            return new GridPos(WorldToCellX(wx), WorldToCellZ(wz));
        }

        public float CellCenterX(int cx)
        {
            return OriginX + (cx + 0.5f) * CellSize;
        }

        public float CellCenterZ(int cz)
        {
            return OriginZ + (cz + 0.5f) * CellSize;
        }

        /// <summary>把任意世界坐标吸附到格中心（X 轴）。</summary>
        public float SnapX(float wx)
        {
            return CellCenterX(WorldToCellX(wx));
        }

        /// <summary>把任意世界坐标吸附到格中心（Z 轴）。</summary>
        public float SnapZ(float wz)
        {
            return CellCenterZ(WorldToCellZ(wz));
        }

        /// <summary>占地区域的世界矩形。</summary>
        public RectXZ AreaToRect(GridPos origin, int footprintX, int footprintZ)
        {
            float minX = OriginX + origin.X * CellSize;
            float minZ = OriginZ + origin.Z * CellSize;
            float maxX = minX + footprintX * CellSize;
            float maxZ = minZ + footprintZ * CellSize;
            return new RectXZ(minX, maxX, minZ, maxZ);
        }

        // ---------- 地形 ----------

        public TerrainMask GetTerrain(GridPos c)
        {
            if (!InBounds(c))
            {
                return TerrainMask.None;
            }

            return _terrain[c.Z * Width + c.X];
        }

        public void SetTerrain(GridPos c, TerrainMask layer)
        {
            if (InBounds(c))
            {
                _terrain[c.Z * Width + c.X] = layer;
            }
        }

        /// <summary>整格填充地形（用于初始化地图）。</summary>
        public void FillTerrain(TerrainMask layer)
        {
            for (int i = 0; i < _terrain.Length; i++)
            {
                _terrain[i] = layer;
            }
        }

        /// <summary>该格地形是否落入允许集合（用于建造）。</summary>
        public bool IsTerrainAllowed(GridPos c, TerrainMask allowedMask)
        {
            TerrainMask t = GetTerrain(c);
            if (t == TerrainMask.None)
            {
                return false;
            }

            return (t & allowedMask) == t;
        }

        /// <summary>该格是否可通行。Ground 与 Road 可走，其余（障碍/水/悬崖/基地台座）不可走。</summary>
        public bool IsWalkable(GridPos c)
        {
            if (!InBounds(c) || IsOccupied(c))
            {
                return false;
            }

            TerrainMask t = GetTerrain(c);
            return (t & (TerrainMask.Ground | TerrainMask.Road)) != TerrainMask.None;
        }

        // ---------- 占用 ----------

        public bool IsOccupied(GridPos c)
        {
            return InBounds(c) && _occupant[c.Z * Width + c.X] != 0;
        }

        public int GetOccupant(GridPos c)
        {
            return InBounds(c) ? _occupant[c.Z * Width + c.X] : 0;
        }

        /// <summary>整块区域是否完全空闲（不改变状态）。</summary>
        public bool IsAreaFree(GridPos origin, int footprintX, int footprintZ)
        {
            if (footprintX <= 0 || footprintZ <= 0)
            {
                return false;
            }

            for (int z = 0; z < footprintZ; z++)
            {
                for (int x = 0; x < footprintX; x++)
                {
                    var c = new GridPos(origin.X + x, origin.Z + z);
                    if (!InBounds(c) || IsOccupied(c))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>占地区域是否完全在地形允许集合内。</summary>
        public bool IsAreaTerrainAllowed(GridPos origin, int footprintX, int footprintZ, TerrainMask allowedMask)
        {
            for (int z = 0; z < footprintZ; z++)
            {
                for (int x = 0; x < footprintX; x++)
                {
                    var c = new GridPos(origin.X + x, origin.Z + z);
                    if (!InBounds(c) || !IsTerrainAllowed(c, allowedMask))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 尝试占用。全成功或全失败（不会留下半块占用）。
        /// </summary>
        public bool TryOccupy(GridPos origin, int footprintX, int footprintZ, int occupantId)
        {
            if (occupantId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(occupantId), "占用 id 不得为 0");
            }

            if (!IsAreaFree(origin, footprintX, footprintZ))
            {
                return false;
            }

            for (int z = 0; z < footprintZ; z++)
            {
                for (int x = 0; x < footprintX; x++)
                {
                    var c = new GridPos(origin.X + x, origin.Z + z);
                    _occupant[c.Z * Width + c.X] = occupantId;
                }
            }

            return true;
        }

        /// <summary>释放占用。只清除匹配 id 的格，防止误清别的建筑。</summary>
        public void Release(GridPos origin, int footprintX, int footprintZ, int occupantId)
        {
            for (int z = 0; z < footprintZ; z++)
            {
                for (int x = 0; x < footprintX; x++)
                {
                    var c = new GridPos(origin.X + x, origin.Z + z);
                    if (InBounds(c) && _occupant[c.Z * Width + c.X] == occupantId)
                    {
                        _occupant[c.Z * Width + c.X] = 0;
                    }
                }
            }
        }

        public void ClearAllOccupancy()
        {
            Array.Clear(_occupant, 0, _occupant.Length);
        }
    }
}