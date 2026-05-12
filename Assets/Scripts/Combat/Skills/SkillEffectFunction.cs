using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public abstract class SkillEffectFunction : ScriptableObject
    {
        public abstract SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect);
        public abstract void Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult result);
    }
}
