using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "ResetMovement", menuName = "MiniChess/Effect Functions/Reset Movement", order = 20)]
    public class ResetMovementFunction : SkillEffectFunction
    {
        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            return SkillEffectResult.Success();
        }

        public override SkillEffectResult Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            var movement = context.Target?.GetComponent<MovementController>()
                ?? context.Caster?.GetComponent<MovementController>();
            if (movement != null)
                movement.ResetMovementBudget();

            return SkillEffectResult.Success();
        }
    }
}
