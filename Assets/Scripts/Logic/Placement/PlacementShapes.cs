using System;

namespace DefenseCounter.Logic
{
    /// <summary>矩形（XZ 平面）。用于可建造区域。</summary>
    public struct RectXZ
    {
        public float MinX;
        public float MaxX;
        public float MinZ;
        public float MaxZ;

        public RectXZ(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public static RectXZ FromCenterSize(float centerX, float centerZ, float sizeX, float sizeZ)
        {
            float hx = sizeX * 0.5f;
            float hz = sizeZ * 0.5f;
            return new RectXZ(centerX - hx, centerX + hx, centerZ - hz, centerZ + hz);
        }

        /// <summary>点是否在矩形内（含边界）。</summary>
        public bool Contains(float x, float z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }

        /// <summary>矩形 A 是否完全包含矩形 B。</summary>
        public bool ContainsRect(RectXZ b)
        {
            return b.MinX >= MinX && b.MaxX <= MaxX && b.MinZ >= MinZ && b.MaxZ <= MaxZ;
        }
    }

    /// <summary>圆形建造半径（基地/建造中心）。</summary>
    public struct BaseCircle
    {
        public float X;
        public float Z;
        public float Radius;

        public BaseCircle(float x, float z, float radius)
        {
            X = x;
            Z = z;
            Radius = radius;
        }

        /// <summary>点是否在半径内。</summary>
        public bool Contains(float px, float pz)
        {
            float dx = px - X;
            float dz = pz - Z;
            return dx * dx + dz * dz <= Radius * Radius;
        }

        /// <summary>
        /// 矩形到圆心的最近距离是否在半径内。
        /// 用最近点而非中心点，否则贴着基地边缘放塔会被误判非法。
        /// </summary>
        public bool OverlapsRect(RectXZ r)
        {
            float nx = Clamp(X, r.MinX, r.MaxX);
            float nz = Clamp(Z, r.MinZ, r.MaxZ);
            float dx = X - nx;
            float dz = Z - nz;
            return dx * dx + dz * dz <= Radius * Radius;
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min)
            {
                return min;
            }

            return v > max ? max : v;
        }
    }
}