namespace DefenseCounter.Data
{
    /// <summary>
    /// 克制矩阵的唯一真源。
    /// 建筑与兵种共用这一份，禁止在战斗代码里另写 switch。
    /// 数值见《主体文档》"克制关系"与平衡表。
    /// </summary>
    public static class DamageMatrix
    {
        // 行 = 伤害类型，列 = 目标护甲类型
        // 顺序：Light / Heavy / Shielded / Structure
        private static readonly float[,] Table =
        {
            // Kinetic 动能
            { 1.25f, 0.80f, 1.00f, 0.85f },
            // Energy 能量
            { 1.00f, 0.70f, 1.50f, 0.90f },
            // Explosive 爆炸
            { 1.10f, 1.00f, 1.00f, 1.40f },
            // Piercing 穿甲
            { 0.75f, 1.60f, 1.00f, 1.10f },
        };

        public static float Multiplier(DamageType attack, ArmorType target)
        {
            int r = (int)attack;
            int c = (int)target;

            if (r < 0 || r >= Table.GetLength(0) || c < 0 || c >= Table.GetLength(1))
            {
                return 1f;
            }

            return Table[r, c];
        }
    }
}