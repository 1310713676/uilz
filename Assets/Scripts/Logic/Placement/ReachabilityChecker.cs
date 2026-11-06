namespace DefenseCounter.Logic
{
    /// <summary>
    /// 可达性检查（4 邻接 BFS，不穿角）。
    /// 用途：验证放置建筑后，敌方出兵点是否仍能走到己方基地。
    /// 若不能，说明玩家用建筑把路封死了 —— 这是非法建造。
    /// 缓冲数组复用，单次检查无 GC。
    /// </summary>
    public sealed class ReachabilityChecker
    {
        private int[] _visited;
        private int[] _queue;
        private int _stamp;

        public bool CanReach(PlacementGrid grid, GridPos start, GridPos goal)
        {
            if (grid == null)
            {
                return false;
            }

            EnsureBuffers(grid);

            if (!grid.InBounds(start) || !grid.InBounds(goal))
            {
                return false;
            }

            // 起点允许是被占用的格（出兵建筑所在格），终点必须是可走格
            if (!grid.IsWalkable(goal))
            {
                return false;
            }

            _stamp++;
            int head = 0;
            int tail = 0;

            int startIndex = start.Z * grid.Width + start.X;
            _visited[startIndex] = _stamp;
            _queue[tail++] = startIndex;

            int goalIndex = goal.Z * grid.Width + goal.X;
            int width = grid.Width;
            int height = grid.Height;

            while (head < tail)
            {
                int cur = _queue[head++];
                if (cur == goalIndex)
                {
                    return true;
                }

                int cx = cur % width;
                int cz = cur / width;

                // 左
                if (cx > 0)
                {
                    TryPush(grid, cur - 1, width, height, ref tail);
                }

                // 右
                if (cx < width - 1)
                {
                    TryPush(grid, cur + 1, width, height, ref tail);
                }

                // 下
                if (cz > 0)
                {
                    TryPush(grid, cur - width, width, height, ref tail);
                }

                // 上
                if (cz < height - 1)
                {
                    TryPush(grid, cur + width, width, height, ref tail);
                }
            }

            return false;
        }

        /// <summary>从多个起点出发，任一能到达目标即返回 true。</summary>
        public bool CanReachAny(PlacementGrid grid, GridPos[] starts, GridPos goal)
        {
            if (starts == null || starts.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < starts.Length; i++)
            {
                if (CanReach(grid, starts[i], goal))
                {
                    return true;
                }
            }

            return false;
        }

        private void TryPush(PlacementGrid grid, int index, int width, int height, ref int tail)
        {
            int x = index % width;
            int z = index / width;

            if (!grid.IsWalkable(new GridPos(x, z)))
            {
                return;
            }

            if (_visited[index] == _stamp)
            {
                return;
            }

            _visited[index] = _stamp;
            _queue[tail++] = index;
        }

        private void EnsureBuffers(PlacementGrid grid)
        {
            int n = grid.CellCount;
            if (_visited == null || _visited.Length != n)
            {
                _visited = new int[n];
                _queue = new int[n];
                _stamp = 0;
            }
        }
    }
}