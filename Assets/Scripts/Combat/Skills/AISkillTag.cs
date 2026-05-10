using MiniChess.GameplayTags;

namespace MiniChess.Combat.Skills
{
    public static class AISkillTag
    {
        public static readonly GameplayTag Damage = new GameplayTag("AI.Skill.Damage");
        public static readonly GameplayTag Heal = new GameplayTag("AI.Skill.Heal");
        public static readonly GameplayTag Buff = new GameplayTag("AI.Skill.Buff");
        public static readonly GameplayTag Debuff = new GameplayTag("AI.Skill.Debuff");
        public static readonly GameplayTag Mobility = new GameplayTag("AI.Skill.Mobility");
        public static readonly GameplayTag Control = new GameplayTag("AI.Skill.Control");
        public static readonly GameplayTag Protect = new GameplayTag("AI.Skill.Protect");
    }
}
