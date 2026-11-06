using DefenseCounter.Data;

namespace DefenseCounter.Logic
{
    /// <summary>
    /// 自由建造合法性校验。对应《主体文档》第三节的 7 条规则。
    /// 纯逻辑、无 UnityEngine 依赖，可被单元测试直接执行。
    ///
    /// 校验顺序（短路）：配置 -> 阵容 -> 资源 -> 边界 -> 可建造区 -> 建造半径 -> 地形 -> 占用 -> 路径
    /// 顺序是刻意的：越便宜、越直观的检查越靠前，保证 UI 报错信息最贴近玩家直觉。
    /// </summary>
    public static class PlacementValidator
    {
        /// <summary>临时占用 id，用于路径预演。</summary>
        private const int TempOccupantId = int.MaxValue;

        public static PlacementResult Validate(PlacementGrid grid, PlacementRequest req, PlacementContext ctx)
        {
            // ---- 0. 参数合法性 ----
            if (grid == null || ctx == null)
            {
                return PlacementResult.Reject(PlacementFailure.InvalidRequest, "网格或上下文为空", req.Origin);
            }

            if (string.IsNullOrEmpty(req.BuildingId))
            {
                return PlacementResult.Reject(PlacementFailure.InvalidRequest, "建筑 id 为空", req.Origin);
            }

            if (req.FootprintX <= 0 || req.FootprintZ <= 0)
            {
                return PlacementResult.Reject(PlacementFailure.InvalidRequest, "占地尺寸非法", req.Origin);
            }

            // ---- 1. 阵容是否允许 ----
            if (!ctx.IsBuildingAllowed(req.BuildingId))
            {
                return PlacementResult.Reject(PlacementFailure.NotInLoadout,
                    "当前阵容不包含 " + req.BuildingId, req.Origin);
            }

            // ---- 2. 资源是否足够 ----
            if (ctx.Gold < req.BuildCost)
            {
                return PlacementResult.Reject(PlacementFailure.NotEnoughGold,
                    "金币不足：需要 " + req.BuildCost + "，当前 " + ctx.Gold, req.Origin);
            }

            // ---- 3. 网格边界 ----
            if (!IsFootprintInBounds(grid, req.Origin, req.FootprintX, req.FootprintZ))
            {
                return PlacementResult.Reject(PlacementFailure.OutOfGrid,
                    "占地超出网格边界 " + req.Origin + " + " + req.FootprintX + "x" + req.FootprintZ, req.Origin);
            }

            // ---- 4. 可建造矩形 ----
            if (!IsInsideAnyBuildableArea(grid, req, ctx))
            {
                return PlacementResult.Reject(PlacementFailure.OutsideBuildableArea,
                    "位置 " + req.Origin + " 不在可建造区域内", req.Origin);
            }

            // ---- 5. 建造半径 ----
            if (!IsInsideAnyBuildRadius(grid, req, ctx, out float nearest))
            {
                return PlacementResult.Reject(PlacementFailure.OutsideBuildRadius,
                    "距最近建造中心 " + Format(nearest) + "m，超出建造范围", req.Origin);
            }

            // ---- 6. 地形 ----
            if (!grid.IsAreaTerrainAllowed(req.Origin, req.FootprintX, req.FootprintZ, req.AllowedTerrain))
            {
                GridPos bad = FindFirstBadTerrain(grid, req);
                return PlacementResult.Reject(PlacementFailure.TerrainNotAllowed,
                    "格 " + bad + " 地形为 " + grid.GetTerrain(bad) + "，该建筑只能建在 " + req.AllowedTerrain,
                    req.Origin);
            }

            // ---- 7. 重叠 ----
            if (!grid.IsAreaFree(req.Origin, req.FootprintX, req.FootprintZ))
            {
                GridPos busy = FindFirstOccupied(grid, req);
                return PlacementResult.Reject(PlacementFailure.CellOccupied,
                    "格 " + busy + " 已被建筑占用", req.Origin);
            }

            // ---- 8. 是否封死通路 ----
            if (ctx.CheckPathBlocking && !KeepsPathOpen(grid, req, ctx))
            {
                return PlacementResult.Reject(PlacementFailure.BlocksCriticalPath,
                    "该位置会封死敌方进攻路径", req.Origin);
            }

            return PlacementResult.Success(req.Origin);
        }

        // ---------- 分项检查 ----------

        private static bool IsFootprintInBounds(PlacementGrid grid, GridPos origin, int fx, int fz)
        {
            var max = new GridPos(origin.X + fx - 1, origin.Z + fz - 1);
            return grid.InBounds(origin) && grid.InBounds(max);
        }

        private static bool IsInsideAnyBuildableArea(PlacementGrid grid, PlacementRequest req, PlacementContext ctx)
        {
            if (ctx.BuildableAreas == null || ctx.BuildableAreas.Length == 0)
            {
                return false;
            }

            RectXZ footprintRect = grid.AreaToRect(req.Origin, req.FootprintX, req.FootprintZ);

            for (int i = 0; i < ctx.BuildableAreas.Length; i++)
            {
                if (ctx.BuildableAreas[i].ContainsRect(footprintRect))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideAnyBuildRadius(PlacementGrid grid, PlacementRequest req,
            PlacementContext ctx, out float nearestDistance)
        {
            nearestDistance = float.MaxValue;

            if (ctx.BuildCenters == null || ctx.BuildCenters.Length == 0)
            {
                return false;
            }

            RectXZ rect = grid.AreaToRect(req.Origin, req.FootprintX, req.FootprintZ);
            bool inside = false;

            for (int i = 0; i < ctx.BuildCenters.Length; i++)
            {
                BaseCircle c = ctx.BuildCenters[i];
                float d = DistanceToRect(c.X, c.Z, rect);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                }

                if (c.OverlapsRect(rect))
                {
                    inside = true;
                }
            }

            return inside;
        }

        private static GridPos FindFirstBadTerrain(PlacementGrid grid, PlacementRequest req)
        {
            for (int z = 0; z < req.FootprintZ; z++)
            {
                for (int x = 0; x < req.FootprintX; x++)
                {
                    var c = new GridPos(req.Origin.X + x, req.Origin.Z + z);
                    if (!grid.IsTerrainAllowed(c, req.AllowedTerrain))
                    {
                        return c;
                    }
                }
            }

            return req.Origin;
        }

        private static GridPos FindFirstOccupied(PlacementGrid grid, PlacementRequest req)
        {
            for (int z = 0; z < req.FootprintZ; z++)
            {
                for (int x = 0; x < req.FootprintX; x++)
                {
                    var c = new GridPos(req.Origin.X + x, req.Origin.Z + z);
                    if (grid.IsOccupied(c))
                    {
                        return c;
                    }
                }
            }

            return req.Origin;
        }

        /// <summary>
        /// 临时占用 -> BFS -> 释放。
        /// 任何异常路径都必须保证释放，否则会污染网格状态（这类 bug 极难排查）。
        /// </summary>
        private static bool KeepsPathOpen(PlacementGrid grid, PlacementRequest req, PlacementContext ctx)
        {
            if (ctx.EnemySpawns == null || ctx.EnemySpawns.Length == 0)
            {
                return true;
            }

            if (!grid.InBounds(ctx.FriendlyBaseCell))
            {
                return true;
            }

            bool occupied = grid.TryOccupy(req.Origin, req.FootprintX, req.FootprintZ, TempOccupantId);
            if (!occupied)
            {
                return true;
            }

            try
            {
                var checker = new ReachabilityChecker();
                return checker.CanReachAny(grid, ctx.EnemySpawns, ctx.FriendlyBaseCell);
            }
            finally
            {
                grid.Release(req.Origin, req.FootprintX, req.FootprintZ, TempOccupantId);
            }
        }

        // ---------- 数学工具 ----------

        private static float DistanceToRect(float px, float pz, RectXZ r)
        {
            float nx = Clamp(px, r.MinX, r.MaxX);
            float nz = Clamp(pz, r.MinZ, r.MaxZ);
            float dx = px - nx;
            float dz = pz - nz;
            return Sqrt(dx * dx + dz * dz);
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min)
            {
                return min;
            }

            return v > max ? max : v;
        }

        private static float Sqrt(float v)
        {
            return (float)System.Math.Sqrt(v);
        }

        private static string Format(float v)
        {
            return v.ToString("0.0");
        }
    }
}