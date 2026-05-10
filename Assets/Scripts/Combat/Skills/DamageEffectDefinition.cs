using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "DamageEffect", menuName = "MiniChess/Effects/Damage", order = 1)]
    public class DamageEffectDefinition : EffectDefinition
    {
        [SerializeField] private int m_amount = 20;

        public int Amount => m_amount;
    }
}
