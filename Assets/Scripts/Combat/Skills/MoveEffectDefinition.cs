using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "MoveEffect", menuName = "MiniChess/Effects/Move", order = 4)]
    public class MoveEffectDefinition : EffectDefinition
    {
        public override ETargetCapability RequiredCapability => ETargetCapability.Movable;

        public override void Apply(EffectContext context)
        {
            // Movement is handled by GroundMoveAbility. This effect remains a semantic marker
            // for validation, tags, and future move-result hooks.
            Debug.Log("[Effect] MoveEffect applied (movement delegated to GroundMoveAbility).");
        }
    }
}
