using System.Collections.Generic;
using DefenseCounter.Logic.GameFlow;
using DefenseCounter.Logic.Skill;

namespace DefenseCounter.Logic.UI
{
    /// <summary>技能栏的一项。UI 只需要读 Ready / CooldownFill / Affordable。</summary>
    public struct SkillBarItem
    {
        public string SkillId;
        public int EnergyCost;
        public float CooldownRemaining;
        public float CooldownTotal;

        /// <summary>能量是否够。</summary>
        public bool Affordable;

        /// <summary>可以点了：能量够 且 冷却结束。</summary>
        public bool Ready;

        /// <summary>冷却进度 0（好）→ 1（刚放）。给 UI 画扇形遮罩用。</summary>
        public float CooldownFill => CooldownTotal > 0.001f ? CooldownRemaining / CooldownTotal : 0f;

        public override string ToString()
        {
            return SkillId + "(cd=" + CooldownRemaining.ToString("0.0") + ",ready=" + Ready + ")";
        }
    }

    /// <summary>
    /// 技能栏逻辑。只回答两个问题：能不能点、冷却还剩多少。
    ///
    /// 不判断「施放位置是否有效」—— 那是 PlacementController / 目标选择层的职责，
    /// 技能栏在按下时只负责把命令送出去，失败原因由 RequestProcessor 返回。
    /// </summary>
    public sealed class SkillBar
    {
        private readonly ISkillCatalog _skills;
        private readonly List<SkillBarItem> _items = new List<SkillBarItem>();

        public SkillBar(ISkillCatalog skills)
        {
            _skills = skills;
        }

        public IReadOnlyList<SkillBarItem> Items => _items;

        /// <summary>按玩家阵容刷新。冷却由 GameLoop 每帧递减，这里只读。</summary>
        public void Refresh(PlayerState player)
        {
            _items.Clear();

            if (player == null || player.AllowedSkillIds == null)
            {
                return;
            }

            for (int i = 0; i < player.AllowedSkillIds.Length; i++)
            {
                string id = player.AllowedSkillIds[i];
                if (!_skills.TryGet(id, out SkillSpec spec))
                {
                    continue;
                }

                float cooldown = player.GetCooldown(id);
                bool affordable = player.Energy >= spec.EnergyCost;

                _items.Add(new SkillBarItem
                {
                    SkillId = id,
                    EnergyCost = spec.EnergyCost,
                    CooldownRemaining = cooldown,
                    CooldownTotal = spec.Cooldown,
                    Affordable = affordable,
                    Ready = cooldown <= 0f && affordable
                });
            }
        }

        public bool TryGetItem(string skillId, out SkillBarItem item)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].SkillId == skillId)
                {
                    item = _items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        /// <summary>生成技能命令。栏上不可点（冷却中 / 能量不足）时返回 false。</summary>
        public bool TryCreateCommand(string skillId, GridPos cell, byte playerId, out GameCommand command)
        {
            command = default;

            if (!TryGetItem(skillId, out SkillBarItem item) || !item.Ready)
            {
                return false;
            }

            command = GameCommand.Skill(playerId, skillId, cell);
            return true;
        }
    }
}