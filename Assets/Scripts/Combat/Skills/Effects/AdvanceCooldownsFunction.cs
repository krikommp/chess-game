using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "AdvanceCooldowns", menuName = "MiniChess/Effect Functions/Advance Cooldowns", order = 21)]
    public class AdvanceCooldownsFunction : SkillEffectFunction
    {
        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            return SkillEffectResult.Success();
        }

        public override void Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            context.Target?.GetComponent<AbilitySystemComponent>()?.AdvanceCooldowns();
        }
    }
}
