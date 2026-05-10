using MiniChess.GameplayTags;

namespace MiniChess.Combat.Skills
{
    public static class AISkillTag
    {
        public static readonly GameplayTag k_Damage = new GameplayTag("AI.Skill.Damage");
        public static readonly GameplayTag k_Heal = new GameplayTag("AI.Skill.Heal");
        public static readonly GameplayTag k_Buff = new GameplayTag("AI.Skill.Buff");
        public static readonly GameplayTag k_Debuff = new GameplayTag("AI.Skill.Debuff");
        public static readonly GameplayTag k_Mobility = new GameplayTag("AI.Skill.Mobility");
        public static readonly GameplayTag k_Control = new GameplayTag("AI.Skill.Control");
        public static readonly GameplayTag k_Protect = new GameplayTag("AI.Skill.Protect");
    }
}
