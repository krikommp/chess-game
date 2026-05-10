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
            var attr = context.Target.GetComponent<AttributeSet>();
            if (attr != null && attr.IsAlive)
            {
                attr.Modify("Attribute.HP", m_amount);
            }
        }
    }
}
