using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public abstract class SkillAbility : ScriptableObject
    {
        public virtual int GetApCost(SkillExecutionContext context)
        {
            return context.Skill != null ? context.Skill.ApCost : 0;
        }

        public virtual SkillCastResult CanApply(SkillExecutionContext context)
        {
            return SkillCastResult.Success();
        }

        public virtual SkillCastResult Apply(SkillExecutionContext context)
        {
            return SkillCastResult.Success();
        }

        public virtual SkillCastResult HandleInput(SkillExecutionContext context, SkillInputRequest inputRequest)
        {
            return SkillCastResult.Success();
        }
    }
}
