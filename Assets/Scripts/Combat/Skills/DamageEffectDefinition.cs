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
            if (context.Target == null) return;
            var unit = context.Target.GetComponent<ICombatUnit>();
            if (unit != null && unit.IsAlive)
            {
                unit.TakeDamage(m_amount);
            }
        }
    }
}
