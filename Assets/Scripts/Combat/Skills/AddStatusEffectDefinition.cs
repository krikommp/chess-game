using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "AddStatusEffect", menuName = "MiniChess/Effects/Add Status", order = 3)]
    public class AddStatusEffectDefinition : EffectDefinition
    {
        [Tooltip("Status to apply. Pending StatusDefinition type.")]
        [SerializeField] private string m_statusId;

        [SerializeField] private int m_durationTurns = 1;

        public string StatusId => m_statusId ?? string.Empty;
        public int DurationTurns => m_durationTurns;
    }
}

