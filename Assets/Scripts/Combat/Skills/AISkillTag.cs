using MiniChess.GameplayTags;
using MiniChess.GameplayTags.Generated;

namespace MiniChess.Combat.Skills
{
    public static class AISkillTag
    {
        public static readonly GameplayTag k_Damage = GameplayTagConstants.AI.Skill.Damage;
        public static readonly GameplayTag k_Heal = GameplayTagConstants.AI.Skill.Heal;
        public static readonly GameplayTag k_Buff = GameplayTagConstants.AI.Skill.Buff;
        public static readonly GameplayTag k_Debuff = GameplayTagConstants.AI.Skill.Debuff;
        public static readonly GameplayTag k_Mobility = GameplayTagConstants.AI.Skill.Mobility;
        public static readonly GameplayTag k_Control = GameplayTagConstants.AI.Skill.Control;
        public static readonly GameplayTag k_Protect = GameplayTagConstants.AI.Skill.Protect;
    }
}
