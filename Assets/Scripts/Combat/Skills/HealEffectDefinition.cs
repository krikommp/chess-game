using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "HealEffect", menuName = "MiniChess/Effects/Heal", order = 2)]
    public class HealEffectDefinition : EffectDefinition
    {
        [SerializeField] private int _amount = 10;

        public int Amount => _amount;
    }
}
