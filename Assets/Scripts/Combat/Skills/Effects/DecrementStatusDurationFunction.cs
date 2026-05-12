using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "DecrementStatusDuration", menuName = "MiniChess/Effect Functions/Decrement Status Duration", order = 23)]
    public class DecrementStatusDurationFunction : SkillEffectFunction
    {
        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            return SkillEffectResult.Success();
        }

        public override void Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            var executor = context.Target?.GetComponent<AbilitySystemComponent>();
            if (executor == null) return;

            var activeEffects = executor.ActiveEffects;
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var active = activeEffects[i];
                if (active.Definition.DurationRounds > 0)
                {
                    active.RemainingRounds--;
                    if (active.RemainingRounds <= 0)
                        executor.RemoveActiveEffect(active);
                }
            }
        }
    }
}
