using System;

namespace DefenseCounter.Logic
{
    /// <summary>
    /// 网格坐标（整数格）。纯值类型，不依赖 UnityEngine，可在 .NET 下直接运行测试。
    /// </summary>
    public struct GridPos : IEquatable<GridPos>
    {
        public int X;
        public int Z;

        public GridPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public static readonly GridPos Zero = new GridPos(0, 0);

        public static GridPos operator +(GridPos a, GridPos b)
        {
            return new GridPos(a.X + b.X, a.Z + b.Z);
        }

        public static GridPos operator -(GridPos a, GridPos b)
        {
            return new GridPos(a.X - b.X, a.Z - b.Z);
        }

        public static bool operator ==(GridPos a, GridPos b)
        {
            return a.X == b.X && a.Z == b.Z;
        }

        public static bool operator !=(GridPos a, GridPos b)
        {
            return !(a == b);
        }

        public bool Equals(GridPos other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public override string ToString()
        {
            return "(" + X + "," + Z + ")";
        }
    }
}