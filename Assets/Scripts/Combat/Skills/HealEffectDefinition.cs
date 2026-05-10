using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "HealEffect", menuName = "MiniChess/Effects/Heal", order = 2)]
    public class HealEffectDefinition : EffectDefinition
    {
        [SerializeField] private int m_amount = 10;

        public int Amount => m_amount;

        public override ETargetCapability RequiredCapability => ETargetCapability.Healable;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null || context.TargetExecutor == null) return;
            if ((context.TargetExecutor.Capabilities & RequiredCapability) == 0) return;
            var unit = context.Target.GetComponent<ICombatUnit>();
            if (unit != null && unit.IsAlive)
            {
                unit.Heal(m_amount);
            }
        }
    }
}
