using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "AddStatusEffect", menuName = "MiniChess/Effects/Add Status", order = 3)]
    public class AddStatusEffectDefinition : EffectDefinition
    {
        [Tooltip("The persistent effect to apply to the target. Must have durationRounds > 0 or grantedTags/Abilities.")]
        [SerializeField] private EffectDefinition m_effectToApply;

        public EffectDefinition EffectToApply => m_effectToApply;

        public override ETargetCapability RequiredCapability => ETargetCapability.Statusable;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null || m_effectToApply == null) return;

            context.TargetExecutor?.ApplyEffect(m_effectToApply, context.Caster);
        }
    }
}
