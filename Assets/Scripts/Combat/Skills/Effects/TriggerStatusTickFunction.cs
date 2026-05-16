using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "TriggerStatusTick", menuName = "MiniChess/Effect Functions/Trigger Status Tick", order = 22)]
    public class TriggerStatusTickFunction : SkillEffectFunction
    {
        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            return SkillEffectResult.Success();
        }

        public override SkillEffectResult Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            var executor = context.Target?.GetComponent<AbilitySystemComponent>();
            if (executor == null)
                return SkillEffectResult.Fail(ESkillCastFailure.TargetInvalid, "Target has no AbilitySystemComponent.");

            var activeEffects = executor.ActiveEffects;
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var active = activeEffects[i];
                if (active.Definition.TickPerRound)
                {
                    var tickCtx = new SkillEffectContext
                    {
                        Caster = active.Source,
                        Target = context.Target,
                        Source = active.Source,
                        CasterExecutor = active.Source?.GetComponent<AbilitySystemComponent>(),
                        TargetExecutor = context.TargetExecutor,
                    };
                    active.Definition.Apply(tickCtx, SkillEffectResult.Success());
                }
            }

            return SkillEffectResult.Success();
        }
    }
}
