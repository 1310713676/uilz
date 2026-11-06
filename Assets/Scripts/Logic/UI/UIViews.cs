using DefenseCounter.Logic.GameFlow;

namespace DefenseCounter.Logic.UI
{
    /// <summary>
    /// HUD 视图契约。Unity 侧用 UGUI / UI Toolkit 实现。
    ///
    /// 为什么是接口而不是直接引用 UnityEngine.UI.Button：
    /// Logic 层不能引用 UGUI（它是 UGUI 包里的类型，不是 UnityEngine 核心模块），
    /// 而且把「怎么画」隔离在接口后面，UI 换实现不用动逻辑。
    /// </summary>
    public interface IHudView
    {
        void SetGold(int value);

        void SetEnergy(int value);

        void SetBaseHealth(float current, float max, float ratio);

        void SetUnitSlots(int used, int max);

        void SetBuildingCount(int count);

        void SetMatchTime(float seconds);

        /// <summary>基地刚被打，播一次红光/震屏。调用即触发，不需要复位。</summary>
        void FlashBaseDamaged();

        void ShowOutcome(MatchOutcome outcome);
    }

    /// <summary>建造菜单视图契约。调用顺序：ClearItems → AddItem × N。</summary>
    public interface IBuildMenuView
    {
        void ClearItems();

        void AddItem(string buildingId, int cost, bool enabled);

        /// <summary>进入放置模式，UI 应高亮该按钮并显示取消按钮。</summary>
        void SetPlacing(string buildingId);

        void ClearPlacing();
    }

    /// <summary>技能栏视图契约。cooldownFill：0 = 可用，1 = 刚放。</summary>
    public interface ISkillBarView
    {
        void ClearItems();

        void AddItem(string skillId, int energyCost, bool ready, float cooldownFill);
    }

    /// <summary>放置预览视图契约。负责地面网格高亮与错误提示。</summary>
    public interface IPlacementView
    {
        /// <summary>
        /// 显示/更新幽灵预览。
        /// 传世界坐标而不是格坐标 —— View 层拿不到 PlacementGrid，无法自己换算。
        /// centerX/centerZ 是占地矩形的中心（2×2 建筑已经算好偏移）。
        /// </summary>
        void ShowGhost(float centerX, float centerZ, int sizeX, int sizeZ, bool valid);

        void HideGhost();

        /// <summary>确认失败时提示原因（例如「会封死进攻路径」）。</summary>
        void ShowError(string message);
    }
}