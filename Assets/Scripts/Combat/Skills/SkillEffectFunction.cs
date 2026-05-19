using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public abstract class SkillEffectFunction : ScriptableObject
    {
        public abstract SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect);
        public abstract SkillEffectResult Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult result);

        public virtual SkillEffectResult Update(
            SkillEffectContext context,
            SkillEffect effect,
            SkillEffectResult result,
            float deltaTime)
        {
            return SkillEffectResult.Success();
        }

        public virtual SkillCostPreviewResult PreviewMaxPathLength(
            SkillEffectContext context,
            SkillEffect effect,
            float currentMaxPathLength)
        {
            return SkillCostPreviewResult.Success(currentMaxPathLength);
        }
    }
}
