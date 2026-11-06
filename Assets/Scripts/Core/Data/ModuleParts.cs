using System;
using UnityEngine;

namespace DefenseCounter.Data
{
    /// <summary>
    /// 模块化外观引用。仅用于编辑期预览与美术文档化，
    /// 运行时不参与拼装 —— 运行时直接用 BuildingConfig.prefabsByLevel。
    /// 见《美术资产规格书 v0.1》第五节。
    /// </summary>
    [Serializable]
    public struct ModuleParts
    {
        [Tooltip("底座模块 ID，如 MD_Base_Tripod_01")]
        public string baseId;

        [Tooltip("塔身模块 ID，如 MD_Body_Cylinder_02")]
        public string bodyId;

        [Tooltip("炮管模块 ID，如 MD_Barrel_Rail_03；无武器填 none")]
        public string barrelId;

        [Tooltip("能量核心 ID，如 MD_Core_Cyan")]
        public string coreId;

        [Tooltip("附件 1~2 个，如 MD_Att_HeatFin_01")]
        public string[] attachIds;

        public bool IsComplete()
        {
            return !string.IsNullOrEmpty(baseId)
                && !string.IsNullOrEmpty(bodyId)
                && !string.IsNullOrEmpty(coreId);
        }
    }
}