using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "DamageEffect", menuName = "MiniChess/Effects/Damage", order = 1)]
    public class DamageEffectDefinition : EffectDefinition
    {
        [SerializeField] private int m_amount = 20;

        public int Amount => m_amount;

        public override ETargetCapability RequiredCapability => ETargetCapability.Damageable;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null || context.TargetExecutor == null) return;
            if ((context.TargetExecutor.Capabilities & RequiredCapability) == 0) return;
            var attr = context.Target.GetComponent<AttributeSet>();
            if (attr != null && attr.IsAlive)
            {
                attr.Modify("Attribute.HP", -m_amount);
            }
        }
    }
}
