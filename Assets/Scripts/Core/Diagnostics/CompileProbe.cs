using UnityEngine;

namespace DefenseCounter.Diagnostics
{
    /// <summary>
    /// 编译链路探针。用于验证本机 dotnet + UnityEngine 参考程序集可用。
    /// 进入 Unity 工程后可直接删除。
    /// </summary>
    public sealed class CompileProbe : ScriptableObject
    {
        [SerializeField] private float _threshold = 0.5f;

        public bool IsInside(Vector3 point, Vector3 center, float radius)
        {
            return (point - center).sqrMagnitude <= radius * radius;
        }

        public float SnapToGrid(float value, float cell)
        {
            if (cell <= 0f)
            {
                return value;
            }

            return Mathf.Round(value / cell) * cell;
        }
    }
}
