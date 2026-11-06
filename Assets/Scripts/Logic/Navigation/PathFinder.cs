using System.Collections.Generic;

namespace DefenseCounter.Logic.Navigation
{
    /// <summary>
    /// 网格 A*。4 邻接、均匀代价（每步 1），缓冲复用、无 GC。
    /// 目标格被建筑占用时，自动退化为「走到目标的相邻可走格」——
    /// 兵要的是能站在塔边上打，不是站到塔里面。
    /// </summary>
    public sealed class PathFinder
    {
        private int[] _g;
        private int[] _parent;
        private int[] _stamp;
        private int[] _closed;
        private int[] _open;
        private int _openCount;
        private int _current;

        public bool TryFindPath(PlacementGrid grid, GridPos start, GridPos goal, List<GridPos> result)
        {
            result.Clear();

            if (grid == null || !grid.InBounds(start) || !grid.InBounds(goal))
            {
                return false;
            }

            EnsureBuffers(grid);

            _current++;
            _openCount = 0;

            int w = grid.Width;
            int h = grid.Height;
            int startIdx = start.Z * w + start.X;
            int goalIdx = goal.Z * w + goal.X;
            bool goalWalkable = grid.IsWalkable(goal);

            _g[startIdx] = 0;
            _parent[startIdx] = -1;
            _stamp[startIdx] = _current;
            _open[_openCount++] = startIdx;

            while (_openCount > 0)
            {
                int cur = PopMin(goalIdx, w);

                if (cur == goalIdx || (!goalWalkable && IsAdjacentTo(cur, goalIdx, w)))
                {
                    Reconstruct(cur, startIdx, w, result);
                    return true;
                }

                if (_closed[cur] == _current)
                {
                    continue;
                }

                _closed[cur] = _current;

                int cx = cur % w;
                int cz = cur / w;

                Expand(grid, cur, cx - 1, cz, w, h);
                Expand(grid, cur, cx + 1, cz, w, h);
                Expand(grid, cur, cx, cz - 1, w, h);
                Expand(grid, cur, cx, cz + 1, w, h);
            }

            return false;
        }

        private void Expand(PlacementGrid grid, int cur, int nx, int nz, int w, int h)
        {
            if (nx < 0 || nx >= w || nz < 0 || nz >= h)
            {
                return;
            }

            int nIdx = nz * w + nx;

            if (_closed[nIdx] == _current || _stamp[nIdx] == _current)
            {
                return;
            }

            if (!grid.IsWalkable(new GridPos(nx, nz)))
            {
                return;
            }

            _stamp[nIdx] = _current;
            _parent[nIdx] = cur;
            _g[nIdx] = _g[cur] + 1;
            _open[_openCount++] = nIdx;
        }

        private int PopMin(int goalIdx, int w)
        {
            int bestSlot = 0;
            int bestF = int.MaxValue;

            for (int i = 0; i < _openCount; i++)
            {
                int idx = _open[i];
                int f = _g[idx] + Heuristic(idx, goalIdx, w);
                if (f < bestF)
                {
                    bestF = f;
                    bestSlot = i;
                }
            }

            int picked = _open[bestSlot];
            _openCount--;
            _open[bestSlot] = _open[_openCount];
            return picked;
        }

        private static int Heuristic(int idx, int goalIdx, int w)
        {
            int dx = idx % w - goalIdx % w;
            int dz = idx / w - goalIdx / w;
            if (dx < 0) { dx = -dx; }
            if (dz < 0) { dz = -dz; }
            return dx + dz;
        }

        private static bool IsAdjacentTo(int idx, int goalIdx, int w)
        {
            int dx = idx % w - goalIdx % w;
            int dz = idx / w - goalIdx / w;
            if (dx < 0) { dx = -dx; }
            if (dz < 0) { dz = -dz; }
            return dx + dz == 1;
        }

        private void Reconstruct(int goalIdx, int startIdx, int w, List<GridPos> result)
        {
            int cur = goalIdx;
            int guard = 0;

            while (cur != startIdx && cur >= 0 && guard++ < 10000)
            {
                result.Add(new GridPos(cur % w, cur / w));
                cur = cur < 0 ? -1 : _parent[cur];
            }

            result.Reverse();
        }

        private void EnsureBuffers(PlacementGrid grid)
        {
            int n = grid.CellCount;
            if (_g != null && _g.Length == n)
            {
                return;
            }

            _g = new int[n];
            _parent = new int[n];
            _stamp = new int[n];
            _closed = new int[n];
            _open = new int[n];
            _current = 0;
        }
    }
}